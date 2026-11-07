// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-TM (§5.12), F-JRNL (§5.13), F-EVT (§5.14).
//
// **What it does** (from the file header):
// A2-49 Kaufman afternoon breakout of the morning range (breakout arm, day-only variant, SAR inside 12:00-15:30).
// Chart: 
//   Data1 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED. IBKR = broker only.
// Features: F-TM (NoEntryBefore/NoEntryAfter/FlatBy, entryAllowed, mustFlatten, MaxTradesPerDay), CME clock adapter on a plain
// _CustomSignal, F-JRNL, roll guard, floor rule. R (for the 1.5R disaster stop) = the morning range width: our reading of A2's "R".

// Manual v4 example 12 - A2-49 Kaufman afternoon breakout of the morning range (breakout arm, day-only variant, SAR inside 12:00-15:30).
// Chart: Data1 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED. IBKR = broker only.
// Features: F-TM (NoEntryBefore/NoEntryAfter/FlatBy, entryAllowed, mustFlatten, MaxTradesPerDay), CME clock adapter on a plain
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _A2_49_MiddayBreakout : _CustomSignal
	{
		protected sealed class A249Config
		{
			public int contracts = 1;
			public TimeSpan rthOpen = new TimeSpan(9, 30, 0), midday = new TimeSpan(12, 0, 0);
			public TimeSpan lastEntry = new TimeSpan(15, 30, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public double disasterR = 1.5;
			public int maxEntries = 3;              // the first break plus at most two reversals per afternoon
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A249Config Cfg { get; } = new A249Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private _CmeSessionClock m_clock;
		private DateTime m_day = DateTime.MinValue;
		private double m_hi = double.NaN, m_lo = double.NaN, m_stop = double.NaN;
		private bool m_skipDay;

		public _A2_49_MiddayBreakout(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			// CS-7 primitives: protected properties, set in code (never [Input])
			NoEntryBefore = Cfg.midday; NoEntryAfter = Cfg.lastEntry; FlatBy = Cfg.flat; MaxTradesPerDay = Cfg.maxEntries;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A249LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A249SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A249LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A249SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A249LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A249SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			m_clock = null;
			base.StartCalc();
			m_day = DateTime.MinValue; m_hi = m_lo = m_stop = double.NaN; m_skipDay = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_hi = m_lo = double.NaN; m_skipDay = false; FlatBy = flatAt(day); }
			if (rollOn(1)) m_skipDay = true;                        // the morning range would straddle the splice
			TimeSpan tod = sessionClock(Bars.Time[0]);
			int pos = StrategyInfo.MarketPosition;
			if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }

			// morning range: RTH bars that close in (09:30, 12:00]
			if (tod > Cfg.rthOpen && tod <= Cfg.midday && isRthBar(Bars.Time[0]))
			{
				m_hi = double.IsNaN(m_hi) ? Bars.High[0] : Math.Max(m_hi, Bars.High[0]);
				m_lo = double.IsNaN(m_lo) ? Bars.Low[0] : Math.Min(m_lo, Bars.Low[0]);
			}
			if (pos > 0) m_lxStp.Send(m_stop); else if (pos < 0) m_sxStp.Send(m_stop);
			if (m_skipDay || double.IsNaN(m_hi) || tod <= Cfg.midday) return;

			int side = Bars.Close[0] > m_hi ? +1 : Bars.Close[0] < m_lo ? -1 : 0;
			if (side == 0 || side == Math.Sign(pos)) return;        // SAR: only a break against the current position acts
			string why;
			if (!entryAllowed(out why)) { if (journalOn()) logSignal("A2-49", side, Bars.Close[0], double.NaN, new Dictionary<string, string> { { "blocked", why } }, false); return; }
			double tick = tickSize(), entry = Bars.Close[0];
			double r = Math.Max(Cfg.disasterR * (m_hi - m_lo), Cfg.costFloorTicks * tick);   // floor rule
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-49 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = entry - side * r;
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);   // reverses an open opposite position
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "mHigh", m_hi.ToString("F2") }, { "mLow", m_lo.ToString("F2") }, { "tradesToday", TradesToday.ToString() } };
				journalArm(side > 0 ? "A249LE" : "A249SE", entry, m_stop, double.NaN, ctx);
				logSignal("A2-49", side, entry, m_stop, ctx, true);
			}
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
