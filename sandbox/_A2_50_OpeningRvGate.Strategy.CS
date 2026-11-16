// ## 1. Identity
// Catalogue ID: A2-50
// ID mapping: A2-50 -> _A2_50_OpeningRvGate.Strategy.CS (class _A2_50_OpeningRvGate).
// Name: A2-50 same-slot relative-volume opening gate
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_50_OpeningRvGate
// File: sandbox/_A2_50_OpeningRvGate.Strategy.CS
// Stage: sandbox
// Non-trading signal: Opening relative-volume gate. It does not enter.
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-50. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-50 heading. Do not paste the entry.
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
// Base class: _CustomSignal
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
// logSignal tag: A2-50
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex04OpeningRvGate.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 4 - A2-50 same-slot relative-volume opening gate (module). Computes RV30 once per session at the 10:00 ET
// bar close and journals HIGH / MID / LOW. It places NO orders: hosts read Rv30State (A2-50 routes hosts, it is not a strategy).
// Chart: Data1 = @ES 30m (bars end 10:00, 10:30, ...), dxFeed continuous UNADJUSTED, 24h CME Globex template. A 30m chart is used
// because a 30m TOD bucket on a 1m chart groups bar ENDS 09:31-10:00 into two buckets (09:31-09:59 and 10:00-10:29).
using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_50_OpeningRvGate : _CustomSignal
	{
		public enum ERvState { Unknown = 0, Low = 1, Mid = 2, High = 3 }

		protected sealed class A250Config
		{
			public TimeSpan slotEnd = new TimeSpan(10, 0, 0);   // ET close-stamp of the 09:30-10:00 bar
			public int ssbSessions = 20;                         // SSB(N = 20)
			public int pctSessions = 250;                        // trailing percentile window
			public double qHi = 0.70, qLo = 0.30;
		}
		protected A250Config Cfg { get; } = new A250Config();

		/// <summary>Today's gate state (Unknown before 10:00 ET, on warm-up, half-days and holidays).</summary>
		public ERvState Rv30State { get; private set; }
		public double Rv30 { get; private set; }

		private _CmeSessionClock m_clock;
		private readonly List<double> m_history = new List<double>();   // prior sessions' RV30, oldest first
		private DateTime m_day = DateTime.MinValue;

		public _A2_50_OpeningRvGate(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }
		/// <summary>Half-days are kept out of the same-slot pools (M-TOD) and out of the gate.</summary>
		protected override bool todHalfDay(DateTime sessionDate) { return _CmeHolidays.Rules.Get(sessionDate).RthCloseEt.HasValue; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			EventConfig.enabled = true;              // only to tag 08:30-report days (reported as a separate stratum)
			EventConfig.classes = EEventClassMask.Macro;
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			TodMetrics = ETodMetric.Volume;
			TodSessions = Cfg.ssbSessions;
			TodMinSessions = Cfg.ssbSessions;        // no SSB until 20 completed sessions
			TodBucketMinutes = 0;                    // 0 = Data1 bar size (30)
			m_clock = null;
			base.StartCalc();
			m_history.Clear(); m_day = DateTime.MinValue; Rv30State = ERvState.Unknown; Rv30 = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();                          // steps M-TOD first: the pool below never holds today
			if (Bars.Status != EBarState.Close) return;
			DateTime t = Bars.Time[0], day = tradingDayKey(t);
			if (day != m_day) { m_day = day; Rv30State = ERvState.Unknown; Rv30 = double.NaN; }
			if (Clock.ToEastern(t).TimeOfDay != Cfg.slotEnd) return;
			if (Bars.Info.Resolution.Size != 30) throw new ArgumentException("_A2_50_OpeningRvGate: Data1 must be 30-minute bars");

			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			if (cme.NoRth || cme.RthCloseEt.HasValue) { journal(day, "skip half-day/no-RTH", double.NaN, double.NaN); return; }

			IList<double> pool = TodNormaliser.VolumePool(todMinuteOfSession(t));
			if (pool == null || pool.Count < Cfg.ssbSessions) { journal(day, "SSB warm-up", double.NaN, double.NaN); return; }
			double ssb = pool.Skip(pool.Count - Cfg.ssbSessions).Average();   // mean of the last 20 completed sessions' 09:30-10:00 volume
			if (!(ssb > 0)) return;
			double rv = Bars.Volume[0] / ssb;
			Rv30 = rv;

			if (m_history.Count >= Cfg.pctSessions)
			{
				var w = m_history.Skip(m_history.Count - Cfg.pctSessions).OrderBy(x => x).ToList();   // prior 250 only (causal)
				double hi = nearestRank(w, Cfg.qHi), lo = nearestRank(w, Cfg.qLo);
				Rv30State = rv >= hi ? ERvState.High : rv <= lo ? ERvState.Low : ERvState.Mid;
				journal(day, Rv30State.ToString(), hi, lo);
			}
			else journal(day, "percentile warm-up " + m_history.Count + "/" + Cfg.pctSessions, double.NaN, double.NaN);
			m_history.Add(rv);
			if (m_history.Count > Cfg.pctSessions + 5) m_history.RemoveAt(0);
		}

		/// <summary>Nearest-rank quantile of a sorted list (the catalogue does not fix the quantile method: an assumption).</summary>
		private static double nearestRank(List<double> sorted, double q)
		{
			int k = (int)Math.Ceiling(q * sorted.Count) - 1;
			return sorted[Math.Max(0, Math.Min(sorted.Count - 1, k))];
		}

		private void journal(DateTime day, string state, double hi, double lo)
		{
			if (!journalOn()) return;
			string rep = "n/a";
			_EventCalendar ev = EventCalendar;
			if (ev != null && !ev.Disabled)
			{
				EEventKind hole;
				if (ev.CoverageBlocks(day, EEventClassMask.Macro, EEventTier.None, out hole)) rep = "unknown(" + hole + ")";
				else rep = ev.EventsOn(day, EEventClassMask.Macro).Any(r => r.etTime.TimeOfDay == EventRules.T0830) ? "yes" : "no";
			}
			trace(ETraceLevel.Journal, $"A2-50 {day:yyyy-MM-dd} RV30({Rv30:F3}) qHi({hi:F3}) qLo({lo:F3}) state({state}) report0830({rep})");
		}
	}
}
