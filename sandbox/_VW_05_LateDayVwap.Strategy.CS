
// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-VWAP (§5.10), F-JRNL (§5.13), F-EVT (§5.14), M-MTF (§5.17).
//
// **What it does** (from the file header):
// VW-05 late-day continuation (v1 15:00 arm): sign(15:00 price - prior RTH close) == sign(price - session VWAP)
// -> 15:00 market in that direction, exit 15:55, stop 1 x ATR30m floored. (A2-06's 15:29 primary rule is a variant; not coded here.)
// Chart: 
//   Data1 = @ES 1m, 
//   Data2 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-VWAP (Vwap1Type = Session -> CurrentVwap1, Vwap1Active), F-SESS (Session.priorRthClose), F-TM, F-CAL half-days.
//
// **Code.**
// Manual v4 example 14 - VW-05 late-day continuation (v1 15:00 arm): sign(15:00 price - prior RTH close) == sign(price - session VWAP)
// -> 15:00 market in that direction, exit 15:55, stop 1 x ATR30m floored. (A2-06's 15:29 primary rule is a variant; not coded here.)
// Chart: Data1 = @ES 1m, Data2 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-VWAP (Vwap1Type = Session -> CurrentVwap1, Vwap1Active), F-SESS (Session.priorRthClose), F-TM, F-CAL half-days.
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _VW_05_LateDayVwap : _VolumeProfileFuturesBase
	{
		protected sealed class Vw05Config
		{
			public int contracts = 1;
			public TimeSpan entryAt = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public int atrLength = 14;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Vw05Config Cfg { get; } = new Vw05Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private double m_stop = double.NaN;
		private DateTime m_tradedDay = DateTime.MinValue;

		public _VW_05_LateDayVwap(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			// VWAP slot 1 = session VWAP on Data1. These VP-base settings are [Input]s on main: set them in code AND do not edit them in
			// the Format Signal dialog (see the F-VWAP pitfalls).
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "VW05LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "VW05SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "VW05LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "VW05SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "VW05LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "VW05SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2 };
			base.StartCalc();
			if (Vwap1Type != EVwapType.Session) throw new ArgumentException("_Ex14LateDayVwap: Vwap1Type must be Session");
			m_stop = double.NaN; m_tradedDay = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay, flat = flatAt(day);
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return;
			}
			if (rollOn(1)) { m_tradedDay = day; return; }                         // prior close is the old contract on the splice day
			if (tod != Cfg.entryAt || m_tradedDay == day || tod >= flat) return;
			SessionStats s = Session;
			if (s == null || s.priorRthClose <= 0 || !Vwap1Active) return;
			double px = Bars.Close[0], vwap = CurrentVwap1;
			if (vwap <= 0) return;
			int a = Math.Sign(px - s.priorRthClose), b = Math.Sign(px - vwap);
			m_tradedDay = day;
			if (a == 0 || a != b) { if (journalOn()) logSignal("VW-05", a, px, double.NaN, new Dictionary<string, string> { { "vwapSide", b.ToString() } }, false); return; }
			double tick = tickSize(), atr = atr30();
			if (double.IsNaN(atr)) return;
			double r = Math.Max(atr, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"VW-05 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = px - a * r;
			if (a > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "vwap", vwap.ToString("F2") }, { "priorClose", s.priorRthClose.ToString("F2") }, { "atr30", atr.ToString("F2") } };
				journalArm(a > 0 ? "VW05LE" : "VW05SE", px, m_stop, double.NaN, ctx);
				logSignal("VW-05", a, px, m_stop, ctx, true);
			}
		}

		private double atr30()
		{
			int n = Cfg.atrLength;
			if (BarsOfData(2).CurrentBar < n + 2) return double.NaN;
			double sum = 0;
			for (int k = 0; k < n; k++)
			{
				DateTime t; double o, h, l, c, v, o1, h1, l1, pc, v1;
				HtfClosedBar(2, k, out t, out o, out h, out l, out c, out v);
				HtfClosedBar(2, k + 1, out t, out o1, out h1, out l1, out pc, out v1);
				sum += Math.Max(h, pc) - Math.Min(l, pc);
			}
			return sum / n;
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }

		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
