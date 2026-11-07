//
// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-TM (§5.12), F-JRNL (§5.13).
//
// **What it does** (from the file header):
// A2-56 first-hour breakout (core arm, no LBR setup filter): after 10:30 ET a buy stop at FH high + 1 tick and a
// sell stop at FH low - 1 tick until 15:00; stop at the other side of the first hour (+/- 1 tick), floored; flat 15:55.
// Chart:
//   Data1 = @ES 1m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-TM (NoEntryBefore/After, FlatBy, MaxTradesPerDay, entryAllowed, mustFlatten), CME clock adapter, F-JRNL.
// Extension point: hostGate() is virtual; example 24 (RG-07) derives from this class and adds the exclusions.
//
// **Code.**
// Manual v4 example 18 - A2-56 first-hour breakout (core arm, no LBR setup filter): after 10:30 ET a buy stop at FH high + 1 tick and a
// sell stop at FH low - 1 tick until 15:00; stop at the other side of the first hour (+/- 1 tick), floored; flat 15:55.
// Chart: Data1 = @ES 1m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-TM (NoEntryBefore/After, FlatBy, MaxTradesPerDay, entryAllowed, mustFlatten), CME clock adapter, F-JRNL.
// Extension point: hostGate() is virtual; example 24 (RG-07) derives from this class and adds the exclusions.
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _A2_56_FirstHourBreak : _CustomSignal
	{
		protected sealed class A256Config
		{
			public int contracts = 1;
			public TimeSpan open = new TimeSpan(9, 30, 0), fhEnd = new TimeSpan(10, 30, 0);
			public TimeSpan lastEntry = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A256Config Cfg { get; } = new A256Config();

		private IOrderPriced m_leStp, m_seStp, m_lxStp, m_sxStp;
		private IOrderMarket m_lx, m_sx;
		private _CmeSessionClock m_clock;
		private DateTime m_day = DateTime.MinValue;
		private double m_fhH = double.NaN, m_fhL = double.NaN, m_stop = double.NaN;
		private bool m_skip;

		public _A2_56_FirstHourBreak(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		/// <summary>Extra entry gate for derived strategies (true = allowed). Called once per bar while the stops would be sent.</summary>
		protected virtual bool hostGate(DateTime day, out string why) { why = null; return true; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			NoEntryBefore = Cfg.fhEnd; NoEntryAfter = Cfg.lastEntry; FlatBy = Cfg.flat; MaxTradesPerDay = 1;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "A256LE", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "A256SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A256LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A256SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A256LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A256SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			m_clock = null;
			base.StartCalc();
			m_day = DateTime.MinValue; m_fhH = m_fhL = m_stop = double.NaN; m_skip = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_fhH = m_fhL = double.NaN; m_skip = false; FlatBy = flatAt(day); }
			if (rollOn(1)) m_skip = true;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			int pos = StrategyInfo.MarketPosition;
			if (mustFlatten()) { if (pos > 0) m_lx.Send(); else if (pos < 0) m_sx.Send(); return; }
			if (tod > Cfg.open && tod <= Cfg.fhEnd && isRthBar(Bars.Time[0]))
			{
				m_fhH = double.IsNaN(m_fhH) ? Bars.High[0] : Math.Max(m_fhH, Bars.High[0]);
				m_fhL = double.IsNaN(m_fhL) ? Bars.Low[0] : Math.Min(m_fhL, Bars.Low[0]);
			}
			double tick = tickSize();
			if (pos != 0)
			{
				if (double.IsNaN(m_stop)) m_stop = pos > 0 ? m_fhL - tick : m_fhH + tick;
				double e = StrategyInfo.AvgEntryPrice;                                     // floor from the actual fill
				if (Math.Abs(e - m_stop) < Cfg.costFloorTicks * tick) m_stop = e - Math.Sign(pos) * Cfg.costFloorTicks * tick;
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return;
			}
			m_stop = double.NaN;
			if (m_skip || double.IsNaN(m_fhH) || tod <= Cfg.fhEnd) return;
			string why;
			if (!entryAllowed(out why) || !hostGate(day, out why)) return;
			double buy = m_fhH + tick, sell = m_fhL - tick;
			double r = Math.Max(buy - sell, Cfg.costFloorTicks * tick);   // either side risks the first-hour range (+2 ticks), floored
			if (overRiskBudget(r)) { if (!m_skip && journalOn()) trace(ETraceLevel.Journal, $"A2-56 {day:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); m_skip = true; return; }
			m_leStp.Send(buy, Cfg.contracts); m_seStp.Send(sell, Cfg.contracts);
			if (journalOn() && Bars.High[0] < buy && Bars.Low[0] > sell && tod == Cfg.fhEnd + TimeSpan.FromMinutes(1))
			{
				var ctx = new Dictionary<string, string> { { "fhHigh", m_fhH.ToString("F2") }, { "fhLow", m_fhL.ToString("F2") } };
				journalArm("A256LE", buy, sell, double.NaN, ctx);
				journalArm("A256SE", sell, buy, double.NaN, ctx);
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
