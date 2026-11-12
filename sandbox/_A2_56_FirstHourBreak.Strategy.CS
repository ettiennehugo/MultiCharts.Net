// ## 1. Identity
// Catalogue ID: A2-56
// ID mapping: A2-56 → _A2_56_FirstHourBreak.Strategy.CS (class _A2_56_FirstHourBreak). FirstHourBreak names the core arm, the first-hour break without the daily setup.
// Name: First-hour break
// Header version: 1
// Rules version: catalogue v2.7 core arm (no LBR setup), as implemented by docs/engines-manual.md §3.8 (example 18)
// Study class: _A2_56_FirstHourBreak
// File: sandbox/_A2_56_FirstHourBreak.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "A2-56 · Momentum Pinball". The code is engines-manual example 18.
// Taken from the source: after 10:30 ET, a buy stop at the first-hour high plus one tick and a sell stop at the first-hour low minus one tick, until 15:00. The protective stop is the other side of the first hour. Flat at 15:55.
// Research assumption: the LBR/RSI daily setup, the one re-entry after a stop, and the conditional overnight hold are not coded. hostGate is virtual so a later exclusion layer can refuse the entry without editing this arm. MaxTradesPerDay is 1.
//
// ## 3. Hypothesis
// Mechanism: the first hour is the session's opening auction. A break of it is the book's intraday trigger. The catalogue's claim is that the daily LBR setup adds something over this trigger alone. This file is that trigger, so the setup can be compared later.
// Link: specs/trading-idea-catalogue.md, heading "A2-56 · Momentum Pinball: daily LBR/RSI setup, first-hour break, conditional overnight hold"
//
// ## 4. Trade walkthrough
// Trigger: once the 09:30–10:30 ET range is known, rest a buy stop one tick above its high and a sell stop one tick below its low.
// Management: after a fill, the stop sits on the other side of the first hour, one tick beyond it, and is widened to 14 ticks from the fill if that side is closer.
// Exit: the stop, or a market flatten at 15:55 ET (or five minutes before an early close when that is earlier). No overnight hold.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. The first-hour high and low include bars that close after 09:30 and at or before 10:30.
// Order sent: Stop A256LE at the high plus one tick, and Stop A256SE at the low minus one tick. The protective stop is A256LS or A256SS. The flat is MarketNextBar A256LX or A256SX.
// Can fill: a stop can fill on a later bar. The flat fills on the bar after the flat time.
//
// ## 6. Position and re-entry
// Position: flat or one. Both stops are sent while flat; the first fill uses the day.
// Re-entry: not in this arm. MaxTradesPerDay is 1, and a risk-budget failure sets m_skip for the day.
// Same session after a stop: no.
//
// ## 7. Instruments
// Typical: ES and NQ. This file is written for @ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted. Eastern clock through the CME session adapter.
// Data2: Not applicable
// Further series: Not applicable
// Warmup: the first-hour bars of the same session.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: a first-hour high and low exist; the bar is after 10:30 and entryAllowed allows it; hostGate allows it; the position is flat; the day is not a roll day; the widened stop fits the 1% budget.
// Skip when: rollOn(1); the budget fails; entryAllowed or hostGate refuses. A budget failure is journaled once.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when 1 × the first-hour range (high+tick to low−tick), floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: Stop A256LE and Stop A256SE, both resting while flat inside the window.
// Exit orders: Stop A256LS / A256SS; MarketNextBar A256LX / A256SX.
// Fill assumption: stop fill when price trades through the stop. The protective stop is sent after the position is open, from the fill price.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the protective stop is re-sent. It is widened to 14 ticks from AvgEntryPrice when the structural stop is closer.
//
// ## 13. Exits
// Hard stop: the other side of the first hour, ± 1 tick, floored at 14 ticks from the fill.
// Target: Not applicable
// Time exit: FlatBy, default 15:55 ET, earlier on a CME early close.
// Invalidation: a roll day does not arm. hostGate can refuse the arm.
//
// ## 14. Regimes
// Suitable: an RTH session with a completed first hour.
// Unsuitable: a roll day. The daily LBR filter is not applied.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: none. The CME session clock is constructed in the signal.
// Engine settings enabled: RollDetection = Series. NoEntryBefore = 10:30, NoEntryAfter = 15:00, FlatBy and MaxTradesPerDay = 1 are set in code.
//
// ## 16. Pitfalls
// Lookahead: the first hour is closed before the stops are priced, and the stops work forward.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. A splice day is skipped.
// Data quality: the journal arm lines are written once, on the bar one minute after 10:30, and only when that bar has not already touched either stop. Not yet verified on a chart.
// Other: both stops rest at once. Whichever fills first is the trade. The other entry is not a second position because MaxTradesPerDay is 1.
//
// ## 17. Parameters
// This file declares no strategy [Input]. TraceLevel stays the base [Input] on _CustomSignal.
// Fixed example settings stay in the protected config class. They are not a sweep.
// Input: none
// Purpose: Not applicable
// Default: Not applicable
// Units: Not applicable
// Range: Not applicable
// Effect: Not applicable
// Interactions: Not applicable
// Fixed or research-tuned: Not applicable
//
// ## 18. Acceptance policy
// Policy: catalogue-default
// Predeclared before any result was read: yes, 2026-11-12. No result for this file has been read. The coded arm is the engines-manual example, not a fitted set.
// catalogue-default: judge this file by its catalogue kill, catalogue §2.3, and the A2 generic kill when the idea is A2-origin. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1% of winning trades is not a whole number, remove K = max(1, ceil(0.01 × the number of winning trades)). If there are no winning trades, remove none. This is the programme overview's proposed rounding, adopted here before any run.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025–Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
//   Catalogue M19 discusses sample error. A minimum count for this strategy is written here before incubation, or it stays Not yet verified.
// Drift: Not yet verified
//   Compare regime mix, fill versus system price, and costs with the tested profile. Numbers come from this strategy's own test, written down before incubation.
// Warning: Not yet verified
//   Warning means look. It is not an order.
// Pause: Not yet verified
//   Pause means stop new entries. Open positions keep the exits in section 13.
// Review: Not yet verified
//   Review means the owner looks before new entries resume.
// Uncertainty: no run yet; the LBR setup and the overnight hold are not in this arm; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: A256LE and A256SE (both, on the arm bar described in section 16)
// logSignal tag: Not applicable. This arm journals with trace, not logSignal.
// Skip / flatten lines: "A2-56" plus "skip: stop" when the budget fails.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// hostGate stays virtual and returns true. A derived exclusion layer can override it. The class name is not in that method.
// Uncoded catalogue arms (LBR/RSI setup, one re-entry, overnight hold) are an owner question, not this file.
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
