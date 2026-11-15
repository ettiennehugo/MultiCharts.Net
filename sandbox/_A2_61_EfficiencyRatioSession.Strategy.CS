// ## 1. Identity
// Catalogue ID: A2-61
// ID mapping: A2-61 -> _A2_61_EfficiencyRatioSession.Strategy.CS (class _A2_61_EfficiencyRatioSession).
// Name: A2-61 session efficiency-ratio router
// Header version: 1
// Rules version: catalogue specification under the A2-61 heading, rewritten 2026-11-15.
// Study class: _A2_61_EfficiencyRatioSession
// File: sandbox/_A2_61_EfficiencyRatioSession.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-61 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: The catalogue entry is a router, not an entry. ASSUMPTION: the 11:00 close is the close at RthStart plus 90 minutes, and each efficiency ratio uses 18 five-minute closes built from 1-minute bars. A reading at or above the 70th percentile of the trailing sessions is journalled as continuation, and at or below the 30th as reversion.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-61 heading. Do not paste the entry.
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
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: Not applicable unless CalcBar reads it.
// Further series: Not applicable
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
// No exit orders. This file does not open a position.
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
// Base class: _VolumeProfileFuturesBase
// Engines enabled: set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: The catalogue entry is a router, not an entry. ASSUMPTION: the 11:00 close is the close at RthStart plus 90 minutes, and each efficiency ratio uses 18 five-minute closes built from 1-minute bars. A reading at or above the 70th percentile of the trailing sessions is journalled as continuation, and at or below the 30th as reversion.
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
// logSignal tag: A2-61
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: The catalogue entry is a router, not an entry. ASSUMPTION: the 11:00 close is the close at RthStart plus 90 minutes, and each efficiency ratio uses 18 five-minute closes built from 1-minute bars. A reading at or above the 70th percentile of the trailing sessions is journalled as continuation, and at or below the 30th as reversion.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_61_EfficiencyRatioSession : _VolumeProfileFuturesBase
	{
		private DateTime m_day = DateTime.MinValue;
		private bool m_logged;
		private double m_open = double.NaN;
		private readonly List<double> m_fives = new List<double>();
		private readonly List<double> m_hist = new List<double>();
		public _A2_61_EfficiencyRatioSession(object _ctx) : base(_ctx) { }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Series; }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close || !IsRth()) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_logged = false; m_open = double.NaN; m_fives.Clear(); }
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (double.IsNaN(m_open) && tod >= m_rthStart) m_open = Bars.Open[0];
			if (((int)tod.TotalMinutes) % 5 == 0) m_fives.Add(Bars.Close[0]);
			TimeSpan mark = m_rthStart.Add(TimeSpan.FromMinutes(90));
			if (m_logged || tod < mark || tod > mark.Add(TimeSpan.FromMinutes(1)) || m_fives.Count < 18 || !(m_open > 0)) return;
			double path = 0;
			int n = m_fives.Count;
			for (int i = n - 18; i < n - 1; i++) path += Math.Abs(m_fives[i + 1] - m_fives[i]);
			if (!(path > 0)) return;
			double er = Math.Abs(m_fives[n - 1] - m_open) / path;
			int below = 0;
			foreach (double h in m_hist) if (h <= er) below++;
			double pct = m_hist.Count == 0 ? 50 : 100.0 * below / m_hist.Count;
			string band = pct >= 70 ? "continuation" : pct <= 30 ? "reversion" : "both";
			if (journalOn()) trace(ETraceLevel.Journal, "A2-61 ER " + er.ToString("0.00") + " percentile " + pct.ToString("0") + " " + band + ". No order.");
			m_hist.Add(er);
			if (m_hist.Count > 100) m_hist.RemoveAt(0);
			m_logged = true;
		}
	}
}
