// ## 1. Identity
// Catalogue ID: KD-08
// ID mapping: KD-08 -> _KD_08_ForwardConeDiagnostic.Strategy.CS (class _KD_08_ForwardConeDiagnostic).
// Name: KD-08 forward-cone diagnostic
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _KD_08_ForwardConeDiagnostic
// File: sandbox/_KD_08_ForwardConeDiagnostic.Strategy.CS
// Stage: sandbox
// Non-trading signal: Forward-cone diagnostic. It does not enter.
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for KD-08. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the KD-08 heading. Do not paste the entry.
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
// Exit orders: this file sends none. Not applicable. The host, when this class extends one, sends its own from sendExits.
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
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those the example turns on in Create or StartCalc.
// Engine settings enabled: RollDetection = Table (the CME roll-date table from the Data1 profile, contract years 2000-2035, RollTableFromProfile; Off on a non-futures symbol), with the other protected settings set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar. HTF reads use HtfClosedBar when the example does.
// Repaint: Not applicable when the example uses closed bars only.
// Survivorship: Not applicable
// Rolls: RollDetection Table (CME roll-date table; owner ruling 2026-11-23: the studies that had rolls off move to the table). rollOn(1) skips the roll bar's journal line and the DVP snapshot drops roll-crossing windows (F-ROLL), which were inert while rolls were off.
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
// logSignal tag: KD-08
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex42ForwardConeDiagnostic.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 42 - KD-08 forward-cone diagnostic, RESEARCH SNIPPET (no orders; KD-08 itself is not buildable on main).
// Status (origin/main 628542f): M-DVP is hosted on Data1 (PR-D3, #36) with the Fib lattice (PR-D4, #37). This file uses the host API
// (DvpConfig, dvpSnapshot, dvpZ, dvpRailPrice, dvpRails). The engine itself measures forward coverage: with journalFits = true it
// writes one DVP FIT line per fit and, on the last historical bar, a DVP RUN line with meanCovIn / meanCovFwd / meanCovFwdFlat
// (cov_fwd over projBars, roll-crossing horizons dropped and counted in fwdDropped). KD-08 still needs PR-D8 (Calibrate study) and
// PR-D9 (rev 6, online self-calibration): degMode = SelfCalibrate (default) picks the degree online (DV-3.10); d = 1 while warming (one "DVP DEGREE warming" warning).
// Every Step closed bars it journals the as-of t-1 snapshot, z of the close and the +-2 sigma rail prices. winLen 240 / projBars 60 /
// degMax 3 are DvpConfig defaults, not tuned values; Step 12 is a journal-thinning choice, not a parameter of the measurement.
// Chart: Data1 = 1-minute. Data2 = 5-minute. Features: M-DVP host API (+ F-ROLL via the snapshot's roll skip).
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _KD_08_ForwardConeDiagnostic : _VolumeProfileFuturesBase
	{
		protected int Step { get; set; } = 12;

		public _KD_08_ForwardConeDiagnostic(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table; RollTableFromProfile = true;
			DvpConfig.enabled = true; DvpConfig.streams = new[] { 2 };
			DvpConfig.journalFits = true;    // DVP FIT per fit + DVP RUN (cov_fwd) at run end - the KD-08 measurement
			DvpConfig.coneMode = EDvpConeMode.Prediction;
		}

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "KD-08 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		protected override void StartCalc() { if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off; base.StartCalc(); }

		protected override void CalcBar()
		{
			base.CalcBar();                  // stepDvp runs inside; the snapshot below is as of t-1
			if (Bars.Status != EBarState.Close || !takeNative() || BarsOfData(2).CurrentBar % Step != 0 || rollOn(1)) return;
			DvpSnapshot s = dvpSnapshot(2);
			if (s == null || !s.valid) return;   // warm-up, or the window crosses a roll
			double z = dvpZ(2, BarsOfData(2).Close[0]);
			double up2 = dvpRailPrice(2, ELevelKind.DvpSigma, 2), dn2 = dvpRailPrice(2, ELevelKind.DvpSigma, -2);
			IReadOnlyList<LevelRef> rails = dvpRails(2);   // reused by the next call; only counted here
			if (journalOn()) trace(ETraceLevel.Journal, $"KD-08sketch {Bars.Time[0]:yyyy-MM-dd HH:mm} deg {s.deg} calibrating {s.calibrating} R2 {s.r2:F2} sigmaAtr {s.sigmaAtr:F2} drift {s.endpointDrift:F2} cov2s(in) {s.inSampleCov2:F2} z {z:F2} band {dn2:F2}..{up2:F2} rails {rails.Count}");
		}
	}
}
