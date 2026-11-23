// ## 1. Identity
// Catalogue ID: KD-05
// ID mapping: KD-05 -> _KD_05_HTFDetrendedRail.Strategy.CS (class _KD_05_HTFDetrendedRail).
// Name: KD-05 is the MT-07 layer
// Header version: 1
// Rules version: catalogue specification under the KD-05 heading, rewritten 2026-11-15.
// Study class: _KD_05_HTFDetrendedRail
// File: sandbox/_KD_05_HTFDetrendedRail.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the KD-05 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: KD-05 is the MT-07 entry: PS-25's 15-minute rail break only while two 60-minute closes have held beyond the detrended VAH.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the KD-05 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: see CalcBar. Closed bar only.
// Management: one contract when this file trades. Size is never cut to fit the stop.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: the order type named in the catalogue (market next bar, stop, or limit).
// Can fill: the next bar, or later for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one, unless the catalogue says a reverse, which this file does not open inside GX.
// Re-entry: one attempt per session unless the catalogue says otherwise.
// Same session after a stop: no new entry that session unless the catalogue says otherwise.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 15-minute (the PS-25 signal stream). Data3 = 60-minute (the HTF stream). Same symbol and session template. Inherited from MT-07.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: the same instrument on 15-minute bars (owner rule: Data2 = native timeframe, the 15m entry stream). The inherited PS-25 rule reads closed Data2 bars only (HtfClosedBar(2, ...) via signalDs).
// Further series: Data3 = the same instrument on 60-minute bars, the HTF acceptance stream, read through HtfClosedBar(3, ...) in MT-07.
// Warmup: the average or profile the rule names.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue trigger and the 1 percent budget when an order is sent.
// Skip when: rollOn on a futures profile idea, the profile or average is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is InstrumentProfile.costFloorTicks times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: named in Create when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. A stop or limit rests. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when section 13 says GX, otherwise sendExits.
//
// ## 13. Exits
// Exit orders: GX, inherited from MT-07 / PS-25. Stop and the 15m +2 sigma target (rail price minus entry, in points). Time stop 41 Data2 bars (41 x 15 = 615 minutes). Flat at the session value end.
// Hard stop: the catalogue stop, widened to the instrument cost floor.
// Target: the catalogue target when it is a price known at the signal.
// Time exit: the catalogue clock, taken from the session API when the rule says the session end.
// Invalidation: any extra exit named in the assumption.
//
// ## 14. Regimes
// Suitable: the session the code consults.
// Unsuitable: outside that session, and a roll day when the rule skips rolls.
//
// ## 15. Dependencies
// Base class: _MT_07_HtfDetrendedRail
// Engines enabled: set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: KD-05 is the MT-07 entry: PS-25's 15-minute rail break only while two 60-minute closes have held beyond the detrended VAH.
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
// Fixed or research-tuned: the catalogue core, not a sweep. The sweep stays in the catalogue.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name when an entry is sent.
// logSignal tag: KD-05
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: KD-05 is the MT-07 entry: PS-25's 15-minute rail break only while two 60-minute closes have held beyond the detrended VAH.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _KD_05_HTFDetrendedRail : _MT_07_HtfDetrendedRail
	{
		private bool m_missingLogged;
		public _KD_05_HTFDetrendedRail(object _ctx) : base(_ctx) { }
		protected override void CalcBar()
		{
			// Coder 2026-11-22 (Analyst follow-up): the MT-07 chart, Data2 = 15m and Data3 = 60m; one Error when either is missing.
			if (Bars.CurrentBar <= 1) m_missingLogged = false;   // a recalc logs again
			if (!IsExist(2) || !IsExist(3)) { if (!m_missingLogged) { m_missingLogged = true; if (journalOn()) trace(ETraceLevel.Error, (!IsExist(2) ? "Data2 (15m)" : "Data3 (60m)") + " missing"); } return; }
			base.CalcBar();
		}
	}
}
