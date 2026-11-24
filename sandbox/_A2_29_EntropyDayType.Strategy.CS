// ## 1. Identity
// Catalogue ID: A2-29
// ID mapping: A2-29 -> _A2_29_EntropyDayType.Strategy.CS (class _A2_29_EntropyDayType).
// Name: A2-29 developing-profile entropy day type at 11:00 ET
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_29_EntropyDayType
// File: sandbox/_A2_29_EntropyDayType.Strategy.CS
// Stage: sandbox
// Non-trading signal: Entropy day-type gate. It classifies the session and does not enter.
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-29. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-29 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
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
// Market tag: futures-only
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
// Size: 1 contract where the example fixes size. journalArm records the planned entry.
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
// Engine settings enabled: RollDetection = Table (the CME roll-date table from the Data1 profile, contract years 2000-2035, RollTableFromProfile; Off on a non-futures symbol), with the other protected settings the example sets. They are not strategy inputs.
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
// logSignal tag: A2-29
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex08EntropyDayType.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 8 - A2-29 developing-profile entropy day type at 11:00 ET (module: journals TREND / BALANCE / NEUTRAL, no orders).
// Chart: Data1 = @ES 1m, dxFeed continuous UNADJUSTED, 24h CME Globex template. Range profiles ON.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_29_EntropyDayType : _VolumeProfileFuturesBase
	{
		public enum EDayType { Unknown = 0, Neutral = 1, Trend = 2, Balance = 3 }

		protected sealed class A229Config
		{
			public TimeSpan rthOpen = new TimeSpan(9, 30, 0), decision = new TimeSpan(11, 0, 0);
			public double trendPct = 80, balancePct = 30;
		}
		protected A229Config Cfg { get; } = new A229Config();

		public EDayType DayType { get; private set; }
		private _EntropyTracker m_hnorm, m_neff;
		private DateTime m_day = DateTime.MinValue;
		private int m_slot;

		public _A2_29_EntropyDayType(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table; RollTableFromProfile = true;
			RangeProfiles.enabled = true;
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			m_slot = Cfg.decision.Hours * 60 + Cfg.decision.Minutes;   // = _EntropyTracker.Slot(11:00) = 660
			m_hnorm = new _EntropyTracker(ProfileStats.pctLookbackA229, ProfileStats.pctMinFill, new[] { m_slot });
			m_neff = new _EntropyTracker(ProfileStats.pctLookbackA229, ProfileStats.pctMinFill, new[] { m_slot });
			m_day = DateTime.MinValue; DayType = EDayType.Unknown;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime t = Bars.Time[0], et = ToEastern(t), day = GlobexDate(t);
			if (day != m_day) { m_day = day; DayType = EDayType.Unknown; }
			if (et.TimeOfDay != Cfg.decision) return;
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			if (cme.NoRth) return;

			// developing RTH profile 09:30-11:00: 1m bars ENDING in (09:30, 11:00] -> exclusive t0, in chart time (t is the 11:00 bar end)
			DateTime t0 = t - (Cfg.decision - Cfg.rthOpen);
			VpExtra e = RangeProfileAfter(1, t0, t);
			RangeStats st = RangeStatsOf(e);
			if (e == null || st == null || st.bars < 1) { journal(day, "no profile", st); return; }
			EntropyResult r = ProfileEntropy(e, st.bars);   // Miller-Madow N = contributing bars (VP-4 default basis)

			// percentiles vs the prior 250 sessions FIRST, then push today (strictly earlier sessions only)
			double pH = m_hnorm.Percentile(m_slot, r.HnormMm), pN = m_neff.Percentile(m_slot, r.Neff);
			m_hnorm.Push(m_slot, day, r.HnormMm); m_neff.Push(m_slot, day, r.Neff);
			if (double.IsNaN(pH) || double.IsNaN(pN)) { journal(day, "warm-up", st, r, pH, pN); return; }
			DayType = pH >= Cfg.trendPct ? EDayType.Trend : (pH <= Cfg.balancePct && pN <= Cfg.balancePct) ? EDayType.Balance : EDayType.Neutral;
			journal(day, DayType.ToString(), st, r, pH, pN);
		}

		private void journal(DateTime day, string state, RangeStats st, EntropyResult r = default(EntropyResult), double pH = double.NaN, double pN = double.NaN)
		{
			if (!journalOn()) return;
			trace(ETraceLevel.Journal, $"A2-29 {day:yyyy-MM-dd} 11:00 HnormMm({r.HnormMm:F4}) Neff({r.Neff:F1}) pctH({pH:F1}) pctNeff({pN:F1}) state({state}) stats({st})");
		}
	}
}
