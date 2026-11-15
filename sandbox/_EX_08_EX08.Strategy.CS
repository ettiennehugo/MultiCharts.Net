// ## 1. Identity
// Catalogue ID: EX-08
// ID mapping: EX-08 -> _EX_08_EX08.Strategy.CS (class _EX_08_EX08).
// Name: EX-08 (no heading; id is cited in the source)
// Header version: 1
// Rules version: catalogue heading, coded 2026-11-15. Instrument-agnostic.
// Study class: _EX_08_EX08
// File: sandbox/_EX_08_EX08.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md for EX-08.
// Taken from the source: the heading and its rules.
// Research assumption: ASSUMPTION: (no heading; id is cited in the source) This file records the rule and does not send an order, and it does not change a fee or a slippage value. Market tag both.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the EX-08 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the coded arm in CalcBar, on a closed bar.
// Management: one contract when this file trades.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades.
// Can fill: the next bar. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one.
// Re-entry: one attempt per session when this file trades.
// Same session after a stop: no new entry that session.
//
// ## 7. Instruments
// Market tag: both
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: only if the code reads BarsOfData(2).
// Further series: Not applicable
// Warmup: ATR(14) or the session profile, when used.
//
// ## 9. Entry and filters
// All of these must be true: the coded arm, a ready profile or average when the arm reads one, and the 1 percent budget.
// Skip when: the budget fails or the session API says the bar is outside the traded session.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is the instrument profile costFloorTicks, times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create, when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when GX is armed, otherwise sendExits.
//
// ## 13. Exits
// Exit orders: this file sends none.
// Hard stop: the structural stop widened to the instrument cost floor, when this file trades.
// Target: one R, or the level named in the assumption, passed to GX as points from the signal close.
// Time exit: the chart session end, from the session API, as GX FlatByHHMM.
// Invalidation: stated in the assumption when it is not a GX flat.
//
// ## 14. Regimes
// Suitable: the instrument session the code consults.
// Unsuitable: outside that session.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: those set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series on the futures base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: (no heading; id is cited in the source) This file records the rule and does not send an order, and it does not change a fee or a slippage value. Market tag both.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name when GX arms.
// logSignal tag: EX-08
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: both
// ASSUMPTION: (no heading; id is cited in the source) This file records the rule and does not send an order, and it does not change a fee or a slippage value. Market tag both.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _EX_08_EX08 : _CustomSignal
	{
		
		private bool m_done, m_logged;
		private int m_hold;
		private double m_orh = double.NaN, m_orl = double.NaN;
		private DateTime m_day = DateTime.MinValue;
		public _EX_08_EX08(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create();
			
			
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			
			
			DateTime day = Bars.Time[0].Date;
			if (day != m_day) { m_day = day; m_done = m_logged = false; m_hold = 0; m_orh = m_orl = double.NaN; }
			
			
			if (m_logged) return;
			m_logged = true;
			if (journalOn()) trace(ETraceLevel.Journal, "EX-08 " + Bars.Time[0].ToString("yyyy-MM-dd") + " no order");
		}
		
	}
}
