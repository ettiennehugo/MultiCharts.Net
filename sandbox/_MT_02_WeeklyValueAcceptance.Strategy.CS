// ## 1. Identity
// Catalogue ID: MT-02
// ID mapping: MT-02 -> _MT_02_WeeklyValueAcceptance.Strategy.CS (class _MT_02_WeeklyValueAcceptance).
// Name: MT-02 weekly accepted value plus a 60-minute trigger
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _MT_02_WeeklyValueAcceptance
// File: sandbox/_MT_02_WeeklyValueAcceptance.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for MT-02. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the MT-02 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
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
// Input: AcceptanceDays
// Purpose: how many daily closes beyond weekly value count as acceptance
// Default: Two - the catalogue default of 2 closes. Predeclared, not a fitted optimum.
// Units: closes
// Range: One, Two. Any other value throws in StartCalc.
// Effect: One accepts on the first close beyond value. Two waits for a second close.
// Interactions: Not applicable
// Fixed or research-tuned: fixed. Two is the catalogue default. One is the other sweep cell.
//
// ## 18. Acceptance policy
// Policy: catalogue-default
// Predeclared before any result was read: yes, 2026-11-15. No result for this file has been read.
// catalogue-default: judge this file by its catalogue kill, catalogue section 2.3. The stricter stated rule decides. This header does not change the catalogue.
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
// logSignal tag: MT-02
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex10WeeklyValueAcceptance.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 10 - MT-02 weekly accepted value + 60m trigger (swing, long side as written in the catalogue).
// Chart: Data1 = @ES 1m, Data2 = @ES 60m, dxFeed continuous UNADJUSTED, 24h CME Globex template. Weekly profiles ON (RTH sessions).
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _MT_02_WeeklyValueAcceptance : _VolumeProfileFuturesBase
	{
		public enum EAcceptanceDays { One, Two }
		[Input] public EAcceptanceDays AcceptanceDays { get; set; }

		protected sealed class Mt02Config
		{
			public int contracts = 1;
			public double zoneAtr = 0.25, stopHeight = 0.25, targetHeight = 1.0;
			public int costFloorTicks = 14;                    // catalogue 2.1: ES cost floor; stop distance = max(structural, floor), size never shrunk
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity (PF-04's 0.5% is not the default here)
			public int timeStopSessions = 7;
		}
		protected Mt02Config Cfg { get; } = new Mt02Config();

		private IOrderMarket m_le, m_lxTime; private IOrderPriced m_lxStp, m_lxLmt;
		private DateTime m_day = DateTime.MinValue, m_week = DateTime.MinValue, m_last60 = DateTime.MinValue;
		private double m_vah = double.NaN, m_height = double.NaN;
		private int m_accept; private bool m_touched, m_doneWeek, m_rollThisWeek;
		private double m_stop, m_target; private int m_heldSessions;

		/// <summary>Catalogue 2.1 / App F #9 (resolved): true when Cfg.contracts at this stop distance risk more than the per-trade budget.</summary>
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _MT_02_WeeklyValueAcceptance(object _ctx) : base(_ctx) { AcceptanceDays = EAcceptanceDays.Two; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */                               // Atr20d (NaN for ~20 sessions after each roll: those weeks are skipped)
			WeeklyProfiles.enabled = true;                   // source RthSessions, rollPolicy Reset, minSessions 3
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "MT02LE", EOrderAction.Buy));
			m_lxTime = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "MT02LX", EOrderAction.Sell, OrderExit.FromAll));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "MT02LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "MT02LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EAcceptanceDays), AcceptanceDays)) throw new ArgumentException("_MT_02_WeeklyValueAcceptance: AcceptanceDays is not a defined EAcceptanceDays");
			HtfStreams = new[] { 2 };
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			m_day = m_week = m_last60 = DateTime.MinValue; m_vah = m_height = double.NaN; m_heldSessions = 0;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd)); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime t = Bars.Time[0], et = ToEastern(t), day = GlobexDate(t);
			bool roll = rollOn(1);
			bool newDay = day != m_day;
			if (newDay) { m_day = day; if (StrategyInfo.MarketPosition > 0) m_heldSessions++; }
			DateTime wk = _WeeklyProfiles.WeekKeyOf(day);
			if (wk != m_week) { m_week = wk; m_vah = double.NaN; m_accept = 0; m_touched = m_doneWeek = m_rollThisWeek = false; }
			if (roll) m_rollThisWeek = true;                 // multi-day levels reset at rolls: no new setup this week

						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if (m_heldSessions >= Cfg.timeStopSessions) { m_lxTime.Send(); return true; }
				m_lxStp.Send(m_stop); m_lxLmt.Send(m_target);
				return true;
			
			}
			if (StrategyInfo.MarketPosition > 0 && sendExits()) return;
			m_heldSessions = 0;
			if (m_doneWeek || m_rollThisWeek) return;
			if (double.IsNaN(m_vah) && !loadPriorWeek(wk)) return;

			DayOfWeek dow = day.DayOfWeek;
			TimeSpan tod = et.TimeOfDay;
			bool rth = isRthBar(t);
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan closeBar = cme.RthCloseEt ?? new TimeSpan(16, 0, 0);   // the RTH close bar (close-stamped 16:00 ET, or the early close)
			// acceptance: RTH closes (the 16:00 ET bar close on 1m) above the prior-week VAH, Monday and Tuesday
			if ((dow == DayOfWeek.Monday || dow == DayOfWeek.Tuesday) && rth && tod == closeBar && Bars.Close[0] > m_vah) m_accept++;
			if (dow < DayOfWeek.Wednesday || dow > DayOfWeek.Friday || cme.NoRth || !rth) return;
			if (m_accept < (AcceptanceDays == EAcceptanceDays.One ? 1 : 2)) { m_doneWeek = true; journal("no acceptance"); return; }

			double atr = DailyContext != null ? DailyContext.Atr20d : double.NaN;
			if (!(atr > 0)) { m_doneWeek = true; journal("Atr20d NaN (post-roll seeding)"); return; }
			double zLo = m_vah - Cfg.zoneAtr * atr, zHi = m_vah + Cfg.zoneAtr * atr;
			if (!m_touched && Bars.Low[0] <= zHi && Bars.High[0] >= zLo) m_touched = true;   // first pullback into the zone
			if (!m_touched) return;
			if (Bars.Close[0] < zLo) { m_doneWeek = true; journal("pullback broke the zone"); return; }   // coding choice (VERIFY)

			// trigger: the first new CLOSED 60m bar after the touch whose low is above the prior 60m low (a 60m higher low)
			DateTime t60; double o, h, l, c, v, o1, h1, l1, c1, v1;
			HtfClosedBar(2, 0, out t60, out o, out h, out l, out c, out v);
			if (t60 == m_last60) return;
			m_last60 = t60;
			DateTime tPrev; HtfClosedBar(2, 1, out tPrev, out o1, out h1, out l1, out c1, out v1);
			if (!(l > l1)) return;
			m_stop = m_vah - Cfg.stopHeight * m_height; m_target = m_vah + Cfg.targetHeight * m_height;
			if (Bars.Close[0] <= m_stop || Bars.Close[0] >= m_target) { m_doneWeek = true; return; }
			double r = Bars.Close[0] - m_stop;   // R from the planned (signal-close) entry
			if (r < Cfg.costFloorTicks * tickSize()) { r = Cfg.costFloorTicks * tickSize(); m_stop = Bars.Close[0] - r; }   // catalogue 2.1: widen to the cost floor
			if (overRiskBudget(r)) { m_doneWeek = true; journal($"skip: stop {r / tickSize():F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }   // not resized
			(entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_doneWeek = true; m_lxStp.Send(m_stop);
			var ctx = new Dictionary<string, string> { { "vah", m_vah.ToString() }, { "height", m_height.ToString() }, { "atr20d", atr.ToString("F2") }, { "accept", m_accept.ToString() } };
			if (journalOn()) { journalArm("MT02LE", Bars.Close[0], m_stop, m_target, ctx); logSignal("MT-02", +1, Bars.Close[0], m_stop, ctx, true); }
		}

		/// <summary>Prior-week profile by KEY: a week is completed only when the next week's first session completes,
		/// so on Monday the prior week is still Week(0) and Week(1) is two weeks back.</summary>
		private bool loadPriorWeek(DateTime wk)
		{
			DateTime prior = wk.AddDays(-7);
			VpExtra e = WeekKey(0) == prior ? Week(0) : WeekKey(1) == prior ? Week(1) : null;
			if (e == null || e.crossesRoll || e.isPartial || !(e.vah > 0) || !(e.val > 0) || e.vah <= e.val) return false;
			m_vah = e.vah; m_height = e.vah - e.val;
			return true;
		}

		private void journal(string why) { if (journalOn()) trace(ETraceLevel.Journal, $"MT-02 week {m_week:yyyy-MM-dd} {why}"); }
	}
}
