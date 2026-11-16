// ## 1. Identity
// Catalogue ID: A2-15
// ID mapping: A2-15 -> _A2_15_LegSkew.Strategy.CS (class _A2_15_LegSkew).
// Name: A2-15 breakout-leg POC versus leg AVWAP
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_15_LegSkew
// File: sandbox/_A2_15_LegSkew.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-15.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: This is a gate on PS-01, not a second entry. Leg POC is RangeProfileAfter from the acceptance bar to now. Leg AVWAP is the volume-weighted close of those same bars. ATR5 is averageTrueRange(Bars, 14); the host chart is the PS-01 chart. Favourable means skew above the threshold in the trade direction. Neutral means absolute skew at or under 0.2. The PS-01 base gate returns true, so the host trade list is unchanged.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-15 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: sendExits re-sends the stop and the target while the position is open. Size stays 1 contract.
// Exit: the catalogue stop, target or time exit, sent only from sendExits. Not the GX wrapper.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: market next bar, or a stop, as the rule states.
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: one contract, long or short as the rule states.
// Re-entry: as the catalogue states. Where it is silent, one attempt per session.
// Same session after a stop: no new entry that session, except where the catalogue allows re-entry.
//
// ## 7. Instruments
// Typical: the catalogue market. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute.
// Data1: the chart the header names. Tests use data through 2024-12-31 only.
// Data2: Not applicable. Higher-timeframe bars are built from Data1.
// Further series: Not applicable
// Warmup: the ATR or average named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup and the 1 percent risk-budget check.
// Skip when: the budget fails, a required average is not ready, or the rule's filter fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract. journalArm records the planned entry.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create.
// Exit orders: sent only from sendExits.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: sendExits re-sends the stop and the target. Nothing else is added.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
// Hard stop: the catalogue stop, widened to 14 ticks when the rule places a stop.
// Target: the catalogue target, or Not applicable.
// Time exit: the catalogue flat time, via FlatBy and mustFlatten inside sendExits.
// Invalidation: the catalogue skip rules.
//
// ## 14. Regimes
// Suitable: the catalogue session window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _PS_01_BalanceBreakPullback
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar.
// Repaint: Not applicable. Closed bars only.
// Survivorship: Not applicable
// Rolls: RollDetection Series, unless the file says otherwise.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: This is a gate on PS-01, not a second entry. Leg POC is RangeProfileAfter from the acceptance bar to now. Leg AVWAP is the volume-weighted close of those same bars. ATR5 is averageTrueRange(Bars, 14); the host chart is the PS-01 chart. Favourable means skew above the threshold in the trade direction. Neutral means absolute skew at or under 0.2. The PS-01 base gate returns true, so the host trade list is unchanged.
//
// ## 17. Parameters
// Each [Input] below is one of this strategy's own parameters. TraceLevel stays on the base.
// Input: see the [Input] properties on the class
// Purpose: the catalogue sweep named in section 22
// Default: the value set in the constructor, which is the catalogue's first listed value
// Units: as the property name states
// Range: the enum members, checked with Enum.IsDefined in StartCalc
// Effect: selects the arm. It does not resize a position.
// Interactions: the other settings stay in the protected config
// Fixed or research-tuned: research-tuned where the catalogue names a sweep
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; GX is not on this branch; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name.
// logSignal tag: A2-15
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: This is a gate on PS-01, not a second entry. Leg POC is RangeProfileAfter from the acceptance bar to now. Leg AVWAP is the volume-weighted close of those same bars. ATR5 is averageTrueRange(Bars, 14); the host chart is the PS-01 chart. Favourable means skew above the threshold in the trade direction. Neutral means absolute skew at or under 0.2. The PS-01 base gate returns true, so the host trade list is unchanged.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_15_LegSkew : _PS_01_BalanceBreakPullback
	{
		public enum ESkewMin { T03, T05 }
		public enum ESkewBand { Favourable, Neutral }
		[Input] public ESkewMin SkewMin { get; set; }
		[Input] public ESkewBand Band { get; set; }

		public _A2_15_LegSkew(object _ctx) : base(_ctx) { SkewMin = ESkewMin.T03; Band = ESkewBand.Favourable; }

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(ESkewMin), SkewMin)) throw new ArgumentException("_A2_15_LegSkew: SkewMin is not a defined ESkewMin");
			if (!Enum.IsDefined(typeof(ESkewBand), Band)) throw new ArgumentException("_A2_15_LegSkew: Band is not a defined ESkewBand");
			base.StartCalc();
		}

		protected override bool legSkewAllows(int side)
		{
			if (m_acceptTime == DateTime.MinValue || side == 0) return false;
			VpExtra leg = RangeProfileAfter(1, m_acceptTime, Bars.Time[0]);
			double avwap = legVwap();
			double atr = averageTrueRange(Bars, 14);
			if (leg == null || !(leg.poc > 0) || double.IsNaN(avwap) || !(atr > 0)) return false;
			double skew = (avwap - leg.poc) / atr;
			if (Band == ESkewBand.Neutral) return Math.Abs(skew) <= 0.2;
			double thr = SkewMin == ESkewMin.T05 ? 0.5 : 0.3;
			return side > 0 ? skew > thr : skew < -thr;
		}

		private double legVwap()
		{
			double num = 0, den = 0;
			int n = Math.Min(Bars.CurrentBar, 500);
			for (int i = 0; i < n; i++)
			{
				if (Bars.Time[i] < m_acceptTime) break;
				double v = Bars.Volume[i] > 0 ? Bars.Volume[i] : 1;
				num += Bars.Close[i] * v; den += v;
			}
			return den > 0 ? num / den : double.NaN;
		}
	}
}
