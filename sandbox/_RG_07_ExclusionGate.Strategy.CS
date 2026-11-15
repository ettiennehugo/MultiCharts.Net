// ## 1. Identity
// Catalogue ID: RG-07
// ID mapping: RG-07 -> _RG_07_ExclusionGate.Strategy.CS (class _RG_07_ExclusionGate).
// Name: RG-07 event and roll exclusion gate
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _RG_07_ExclusionGate
// File: sandbox/_RG_07_ExclusionGate.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for RG-07. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the RG-07 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract where the example fixes size.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: the example's order type (market next bar, stop or limit).
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: the example's position rule, flat or one unless the example says otherwise.
// Re-entry: as the example codes it.
// Same session after a stop: as the example codes it.
//
// ## 7. Instruments
// Market tag: both
// Typical: @ES, the example's chart. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the example's chart, dxFeed continuous unadjusted. Tests use data through 2024-12-31 only.
// Data2: the example's second series, or Not applicable when the example has none.
// Further series: Not applicable unless the example names one.
// Warmup: the example's indicator warmup.
//
// ## 9. Entry and filters
// All of these must be true: the example's entry conditions, and the 1 percent risk-budget check when the example has one.
// Skip when: the example journals a skip, a roll day when the example skips rolls, or the risk budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract where the example fixes size. journalArm records the planned entry when the example calls it.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity, when the example codes that check.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the example's entry order name.
// Exit orders: the example's exit order names.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: the example re-sends its stop or target. Nothing else is added.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
// Hard stop: the example's stop, or Not applicable when the example has none.
// Target: the example's target, or Not applicable.
// Time exit: the example's flat time, or Not applicable.
// Invalidation: the example's skip rules.
//
// ## 14. Regimes
// Suitable: the example's session window.
// Unsuitable: outside that window, and where the example stands aside.
//
// ## 15. Dependencies
// Base class: _A2_56_FirstHourBreak
// Engines enabled: those the example turns on in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings the example sets. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar. HTF reads use HtfClosedBar when the example does.
// Repaint: Not applicable when the example uses closed bars only.
// Survivorship: Not applicable
// Rolls: as the example sets RollDetection.
// Data quality: Not yet verified on a chart.
// Other: this is a port of the worked example, not a new rule set. ASSUMPTION: the example's labelled choices stand until an owner changes them.
//
// ## 17. Parameters
// This file declares no strategy [Input] besides the inherited TraceLevel.
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
// Predeclared before any result was read: yes, 2026-11-15. No result for this file has been read.
// catalogue-default: judge this file by its catalogue kill, catalogue section 2.3 and the A2 generic kill. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1 percent of winning trades is not a whole number, remove K = max(1, ceil(0.01 times the number of winning trades)). If there are no winning trades, remove none.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025-Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
// Drift: Not yet verified
// Warning: Not yet verified
// Pause: Not yet verified
// Review: Not yet verified
// Uncertainty: no run yet; the example's assumptions; GX is not in the build; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the example's entry order name, when it calls journalArm.
// logSignal tag: RG-07
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Slot copy: this class extends _A2_56_FirstHourBreak. That file must be in the same compile or the slot does not build.
// The worked example class was _Ex24ExclusionGate.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 24 - RG-07 event, roll and thin-session exclusions, applied as a gate on the example 18 host (A2-56).
// Rules: tier-1 news -2/+10 min blackout (and flatten); FOMC day after 14:00 ET -> no new entries; roll week -> no new entries;
// CME half-days and holiday-adjacent sessions -> off. Unscheduled news is NOT covered. GDP/PCE coverage holes 2016-24 (EV-4.6b):
// allowPartialCoverage = false makes an Unknown-coverage day block (conservative).
// Chart: as example 18. M-EVENT uses its embedded table unless EventConfig.csvPath is set.
// Features: M-EVENT (EventConfig, EventBlackout, EventFlattenNow, EventCalendar.Classify, IsRollWeek, CoverageBlocks), F-CAL, F-ROLL.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _RG_07_ExclusionGate : _A2_56_FirstHourBreak
	{
		private IOrderMarket m_flatL, m_flatS;
		public _RG_07_ExclusionGate(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			EventConfig.enabled = true;                         // lazy: the calendar is built only when enabled
			EventConfig.blackoutTier = EEventTier.T1;
			EventConfig.blackoutPreMin = 2; EventConfig.blackoutPostMin = 10;
			EventConfig.blackoutFlatten = true;                 // the base only RAISES EventFlattenNow; this strategy acts on it
			EventConfig.allowPartialCoverage = false;
			m_flatL = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "RG07LX", EOrderAction.Sell));
			m_flatS = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "RG07SX", EOrderAction.BuyToCover));
		}

		protected override void CalcBar()
		{
			base.CalcBar();                                     // runs the host (which calls hostGate) and the event engine
			if (Bars.Status != EBarState.Close) return;
			if (EventFlattenNow && sendExits()) { }
		}

		// Exits. The only method that sends an exit order. The host's own exits stay in the host sendExits. A later GX exitPlan replaces this method.
		private bool sendExits()
		{
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0) m_flatL.Send(); else if (pos < 0) m_flatS.Send();
			if (pos != 0 && journalOn()) trace(ETraceLevel.Journal, $"RG-07 {Bars.Time[0]:yyyy-MM-dd HH:mm} tier-1 blackout: flatten");
			return true;
		}

		protected override bool hostGate(DateTime day, out string why)
		{
			why = null;
			if (EventBlackout) { why = "T1 blackout"; return false; }
			_EventCalendar ev = EventCalendar;
			if (ev == null) { why = "no calendar"; return false; }
			EventDay d = ev.Classify(day);
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (d.hasFomc && tod >= new TimeSpan(14, 0, 0)) why = "FOMC day after 14:00";
			else if (d.isRollWeek == true) why = "roll week";   // null = not evaluated (Unavailable or NotSelected), not "not a roll week". IsRollWeek() throws in those states.
			else if (d.isHalfDay || _CmeHolidays.Rules.Get(day).RthCloseEt.HasValue) why = "half-day";
			else if (holidayAdjacent(day)) why = "holiday-adjacent";
			else if (ev.CoverageBlocks(day, EEventClassMask.Macro)) why = "coverage hole";
			if (why != null && journalOn()) logSignal("RG-07", 0, Bars.Close[0], double.NaN, new Dictionary<string, string> { { "blocked", why } }, false);
			return why == null;
		}

		/// <summary>Day before or after a weekday CME holiday (no RTH session) or early close.</summary>
		private static bool holidayAdjacent(DateTime day)
		{
			foreach (int k in new[] { -1, 1 })
			{
				DateTime d = day.AddDays(k);
				while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(k);
				_CmeHolidays.Rules.Day r = _CmeHolidays.Rules.Get(d);
				if (r.Closed || r.NoRth || r.RthCloseEt.HasValue) return true;
			}
			return false;
		}
	}
}
