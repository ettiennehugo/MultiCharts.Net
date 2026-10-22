using ATCenterProxy.interop;
using PowerLanguage.Function;
using System;
using System.Collections.Generic;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// M-PORT money-management signal for Portfolio Trader (specs/m-port-spec.md §5). Apply it as the portfolio's Money Management
	/// signal. Each calculation it denies entries for every strategy, then re-allows and sizes the chosen ones (EntryContracts is
	/// always set explicitly: RiskParity size, or -1 = code size), applies the 4% heat cap, 2% per correlation cluster, drawdown
	/// throttles 5/10/15%, a daily loss stop (-2% deny, -3% flatten) and per-strategy kill switches (ForceClosePosition, then
	/// Paused once flat). It never blocks exits. Logic lives in _PortMM (_PortMM.Function.CS, unit-tested off-MC).
	/// No [Input]s: change the configuration by subclassing and overriding Config.
	/// </summary>
	public class _PortfolioRiskManager : PortfolioSignalObject
	{
		private _PortMM m_mm;
		private bool m_runLogged, m_currencyLogged;
		private readonly HashSet<string> m_logged = new HashSet<string>();

		public _PortfolioRiskManager(object _ctx) : base(_ctx) { }

		/// <summary>Effective configuration (code defaults). Override in a subclass for another profile.</summary>
		protected virtual PortMmConfig Config { get { return new PortMmConfig(); } }

		protected override void StartCalc()
		{
			m_mm = new _PortMM { config = Config };
			m_mm.journal = s => Output.WriteLine(s);
			string err = m_mm.Configure();
			if (err != null) throw new ArgumentException(err);
			m_runLogged = m_currencyLogged = false; m_logged.Clear();
		}

		protected override void StopCalc() { m_mm = null; }

		protected override void CalcBar()
		{
			if (m_mm == null || Environment.ApplicationCode != EApplicationCode.Portfolio) return;
			int n = PortfolioStrategies.Count;
			if (n == 0) return;
			var views = new List<PortStratView>(n);
			DateTime day = DateTime.MinValue;
			double initialCapital = double.NaN;
			for (int i = 0; i < n; i++)
			{
				IPortfolioStrategy ps = PortfolioStrategies[i];
				views.Add(view(ps, ref day));
				if (double.IsNaN(initialCapital) && ps.Signals != null && ps.Signals.Length > 0) initialCapital = ps.Signals[0].InitialCapital;
			}
			if (day == DateTime.MinValue) for (int i = 0; i < n && day == DateTime.MinValue; i++) { IStrategy sg = PortfolioStrategies[i].Signals != null && PortfolioStrategies[i].Signals.Length > 0 ? PortfolioStrategies[i].Signals[0] : null; if (sg != null) day = sg.Bars.Time[0].Date; }
			if (day == DateTime.MinValue) return;
			bool usd = PortfolioCurrency == MTPA_MCSymbolCurrency.eMTPA_MCSC_USD;
			if (!usd && !m_currencyLogged) { m_currencyLogged = true; Output.WriteLine($"PORT portfolio currency {PortfolioCurrency} is not USD: all entries denied"); }
			if (!m_runLogged)
			{
				m_runLogged = true;
				Output.WriteLine($"PORT RUN strategies({n}) initialCapital({initialCapital}) currency({PortfolioCurrency}) {m_mm.config}");
			}

			PortDecision[] d = m_mm.Decide(day, double.IsNaN(initialCapital) ? 0 : initialCapital, usd, views);

			CommonPortfolioData[_PortKeys.Mult] = m_mm.Mult;
			CommonPortfolioData[_PortKeys.Heat] = m_mm.Heat;
			CommonPortfolioData[_PortKeys.Dd] = m_mm.Drawdown;
			CommonPortfolioData[_PortKeys.Rho] = m_mm.Rho;

			for (int i = 0; i < n; i++)
			{
				IPortfolioStrategy ps = PortfolioStrategies[i];
				PortDecision x = d[i];
				ps.AllowEntries = x.allowEntries;
				ps.AllowEntriesLong = x.allowEntries && x.allowLong;
				ps.AllowEntriesShort = x.allowEntries && x.allowShort;
				ps.EntryContracts = x.entryContracts;   // always explicit: 0 would mean the Strategy Properties size
				if (x.forceClose) ps.ForceClosePosition();
				if (x.pause && !ps.Paused) { ps.Paused = true; Output.WriteLine($"PORT paused {views[i].id}#{ps.MyIndex} (flat)"); }
				ps.Status = x.status;
				if (x.forceClose) logOnce($"fc|{ps.MyIndex}|{day:yyyyMMdd}|{x.reason}", $"PORT ForceClosePosition {views[i].id}#{ps.MyIndex} on {day:yyyy-MM-dd}: {x.reason}");
			}
		}

		private void logOnce(string key, string msg) { if (m_logged.Add(key)) Output.WriteLine(msg); }

		private PortStratView view(IPortfolioStrategy ps, ref DateTime day)
		{
			var v = new PortStratView { index = ps.MyIndex, paused = ps.Paused };
			IPortfolioData pd = ps.PortfolioData;
			IStrategy sig = ps.Signals != null && ps.Signals.Length > 0 ? ps.Signals[0] : null;
			if (sig != null)
			{
				v.position = sig.PositionSide;   // sign only; replaced by pm.pos when published
				v.currencyOk = sig.StrategyCurrencyCode == MTPA_MCSymbolCurrency.eMTPA_MCSC_USD;
			}
			object ver = get(pd, _PortKeys.Ver);
			v.publishes = ver is int && (int)ver == _PortKeys.Version;
			if (!v.publishes) { v.id = sig != null ? sig.Name : "#" + ps.MyIndex; return v; }
			v.id = get(pd, _PortKeys.Id) as string ?? "";
			v.cluster = get(pd, _PortKeys.Cluster) as string ?? "";
			object o;
			o = get(pd, _PortKeys.State); v.state = o is int ? (EPortState)(int)o : EPortState.Flat;
			o = get(pd, _PortKeys.Side); v.side = o is int ? (int)o : 0;
			o = get(pd, _PortKeys.Pos); if (o is int) v.position = (int)o;
			v.rDollar = num(pd, _PortKeys.RDollar);
			o = get(pd, _PortKeys.QtyCode); v.qtyCode = o is int ? (int)o : 0;
			v.riskOpen = num(pd, _PortKeys.RiskOpen); if (double.IsNaN(v.riskOpen)) v.riskOpen = 0;
			v.vol = num(pd, _PortKeys.Vol);
			v.pnl = num(pd, _PortKeys.Pnl); if (double.IsNaN(v.pnl)) v.pnl = 0;
			v.score = num(pd, _PortKeys.Score);
			v.ret = get(pd, _PortKeys.Ret) as _PortReturns;
			v.uRet = get(pd, _PortKeys.URet) as _PortReturns;
			o = get(pd, _PortKeys.Day); if (o is DateTime && day == DateTime.MinValue) day = (DateTime)o;
			o = get(pd, _PortKeys.Stamp);
			if (!(o is DateTime)) v.stale = true;
			else if (sig != null)
			{
				IInstrument b = sig.Bars;
				int back = Math.Min(m_mm.config.staleBars, Math.Max(0, b.CurrentBar - 1));
				v.stale = (DateTime)o < b.Time[back];
			}
			return v;
		}

		private static object get(IPortfolioData pd, string key) { return pd != null && pd.ContainsKey(key) ? pd[key] : null; }
		private static double num(IPortfolioData pd, string key)
		{
			object o = get(pd, key);
			if (o is double) return (double)o;
			if (o is int) return (int)o;
			return double.NaN;
		}
	}
}
