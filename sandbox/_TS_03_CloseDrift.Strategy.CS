// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-TM (§5.12), F-JRNL (§5.13), F-EVT (§5.14), M-MTF (§5.17).
//
// **What it does** (from the file header):
// TS-03 close regime, variant A: at 15:50 ET trade the 15:30-15:50 drift, exit at 16:00. Stop 1 x ATR5, floored.
// Chart: Data1 = @ES 1m, Data2 = @ES 5m (24h Globex template), dxFeed continuous UNADJUSTED. MOC imbalance data is NOT available:
// the drift is a proxy only (catalogue). Early-close days (F-CAL) are skipped: there is no 15:30-16:00 window.
// Features: F-CAL (_CmeHolidays.Rules), F-TM (MaxTradesPerDay, entryAllowed), M-MTF (HtfClosedBar), CME clock adapter, F-JRNL.
//
// Manual v4 example 13 - TS-03 close regime, variant A: at 15:50 ET trade the 15:30-15:50 drift, exit at 16:00. Stop 1 x ATR5, floored.
// 
// Chart: 
//   Data1 = @ES 1m, 
//   Data2 = @ES 5m (24h Globex template), dxFeed continuous UNADJUSTED. MOC imbalance data is NOT available:
// the drift is a proxy only (catalogue). Early-close days (F-CAL) are skipped: there is no 15:30-16:00 window.
// Features: F-CAL (_CmeHolidays.Rules), F-TM (MaxTradesPerDay, entryAllowed), M-MTF (HtfClosedBar), CME clock adapter, F-JRNL.

using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _TS_03_CloseDrift : _CustomSignal
	{
		protected sealed class Ts03Config
		{
			public int contracts = 1;
			public TimeSpan driftFrom = new TimeSpan(15, 30, 0), entryAt = new TimeSpan(15, 50, 0), exitAt = new TimeSpan(16, 0, 0);
			public int minDriftTicks = 1;
			public int atrLength = 14;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ts03Config Cfg { get; } = new Ts03Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private _CmeSessionClock m_clock;
		private double m_ref = double.NaN, m_stop = double.NaN;
		private DateTime m_day = DateTime.MinValue;

		public _TS_03_CloseDrift(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			MaxTradesPerDay = 1;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS03LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS03SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS03LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS03SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS03LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS03SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2 };
			m_clock = null;
			base.StartCalc();
			m_ref = m_stop = double.NaN; m_day = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_ref = double.NaN; }
			TimeSpan tod = sessionClock(Bars.Time[0]);
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (tod >= Cfg.exitAt && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }   // next-bar market: fills at the open of the 16:01 bar
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return;
			}
			if (rollOn(1)) { m_ref = double.NaN; return; }
			if (_CmeHolidays.Rules.Get(day).RthCloseEt.HasValue) return;     // F-CAL: early close, no MOC window
			if (tod == Cfg.driftFrom) m_ref = Bars.Close[0];                  // 1m bar closing at 15:30
			if (tod != Cfg.entryAt || double.IsNaN(m_ref)) return;
			double tick = tickSize(), drift = Bars.Close[0] - m_ref;
			if (Math.Abs(drift) < Cfg.minDriftTicks * tick) return;
			int side = Math.Sign(drift);
			string why;
			if (!entryAllowed(out why)) return;
			double atr = atr5();
			if (double.IsNaN(atr)) return;
			double r = Math.Max(atr, Cfg.costFloorTicks * tick);               // floor rule
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"TS-03 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			double entry = Bars.Close[0];
			m_stop = entry - side * r;
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "drift", drift.ToString("F2") }, { "atr5", atr.ToString("F2") } };
				journalArm(side > 0 ? "TS03LE" : "TS03SE", entry, m_stop, double.NaN, ctx);
				logSignal("TS-03", side, entry, m_stop, ctx, true);
			}
		}

		private double atr5()
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
	}
}
