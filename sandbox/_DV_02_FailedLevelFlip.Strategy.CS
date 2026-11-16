// ## 1. Identity
// Catalogue ID: DV-02
// ID mapping: DV-02 -> _DV_02_FailedLevelFlip.Strategy.CS (class _DV_02_FailedLevelFlip).
// Name: DV-02 failed level flip
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _DV_02_FailedLevelFlip
// File: sandbox/_DV_02_FailedLevelFlip.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for DV-02. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the DV-02 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract where the example fixes size.
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
// logSignal tag: DV-02
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex32FailedLevelFlip.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 32 - DV-02 failed level -> reversal (flip) limit, with the CS-8 event log and control layer.
// Level L: the prior completed RTH POC (a DV-01 level), passed through controlAdjustLevel() so a ShiftLevels control run moves it.
// Failure: after the first touch of L, a 5m close >= s x the bracket distance beyond L on the far side within M bars, with
// "no reaction" (max adverse excursion against the break after the touch < 0.25 x bracket). Entry: limit at L in the break
// direction, valid to the session end (X = 1 session), one flip per level. Bracket: +/- f x ATR20d (f = 0.10); the stop side is widened to
// the 14-tick floor if narrower (target unchanged), skipped only on a risk-budget breach, never resized. s = 1.0 and f = 0.10 are catalogue grid points; M = 12 bars is a PLACEHOLDER.
// Every armed flip is logged with logSignal(taken = true); skipped ones with taken = false (needs TraceLevel >= Journal).
// Chart: Data1 = @ES 5m, dxFeed continuous UNADJUSTED. Features: F-EVT/CS-8 (logSignal, ControlMode, controlAdjustLevel,
// controlAdjustDelayBars), F-VP (PriorRthProfile), M-DAILY (Atr20d), F-TM.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _DV_02_FailedLevelFlip : _VolumeProfileFuturesBase
	{
		protected sealed class Dv02Config
		{
			public int contracts = 1, breakWithinBars = 12;   // M: PLACEHOLDER
			public double s = 1.0, f = 0.10, noReaction = 0.25;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
			public TimeSpan flat = new TimeSpan(15, 55, 0);
		}
		protected Dv02Config Cfg { get; } = new Dv02Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_day = DateTime.MinValue;
		private double m_level, m_br, m_sd, m_hiSince, m_loSince;
		private int m_touchBar, m_side, m_armBar;
		private bool m_done;

		public _DV_02_FailedLevelFlip(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			FlatBy = Cfg.flat;
			// Placebo run: ControlMode = EControlMode.ShiftLevels; ControlShiftTicks = 8;  (protected properties, never [Input])
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV02LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV02SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV02LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV02SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV02LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV02SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV02LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV02ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() {
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (rollOn(1)) m_done = true;
			int pos = StrategyInfo.MarketPosition, bar = Bars.FullSymbolData.Current;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				double e = StrategyInfo.AvgEntryPrice;
				if (pos > 0) { m_lxStp.Send(e - m_sd); m_lxLmt.Send(e + m_br); } else { m_sxStp.Send(e + m_sd); m_sxLmt.Send(e - m_br); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || !(m_br > 0) || !IsRth()) return;
			double h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0];
			if (m_side != 0)                                           // armed: work the limit at L until the session ends
			{
				if (bar >= m_armBar + controlAdjustDelayBars())
				{ if (m_side > 0) m_leLmt.Send(m_level, Cfg.contracts); else m_seLmt.Send(m_level, Cfg.contracts); }
				return;
			}
			if (m_touchBar == 0)
			{
				if (l <= m_level && h >= m_level) { m_touchBar = bar; m_hiSince = h; m_loSince = l; }
				return;
			}
			m_hiSince = Math.Max(m_hiSince, h); m_loSince = Math.Min(m_loSince, l);
			if (bar - m_touchBar > Cfg.breakWithinBars) { m_done = true; return; }    // no failure within M bars: level held
			int side = c >= m_level + Cfg.s * m_br ? +1 : c <= m_level - Cfg.s * m_br ? -1 : 0;
			if (side == 0) return;
			double mae = side > 0 ? m_level - m_loSince : m_hiSince - m_level;
			bool reaction = mae >= Cfg.noReaction * m_br;
			bool budget = Cfg.contracts * m_sd * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction);
			bool taken = !reaction && !budget;
			logSignal("DV02", side, m_level, m_level - side * m_sd, new Dictionary<string, string> { { "mae", mae.ToString("F2") }, { "control", ControlMode.ToString() } }, taken);
			if (!taken) { m_done = true; return; }
			m_side = side; m_armBar = bar;
			if (journalOn()) journalArm(side > 0 ? "DV02LE" : "DV02SE", m_level, m_level - side * m_sd, m_level + side * m_br, null);
		}

		/// <summary>Hook for POC gate modules (Ex35: A2-25/A2-26). May widen the bracket; return false to skip the day.</summary>
		protected virtual bool pocGate(DateTime day, double poc, ref double bracket, out string why) { why = null; return true; }

		private void newDay(DateTime day)
		{
			m_day = day; m_done = false; m_touchBar = m_side = 0;
			VpExtra prior = PriorRthProfile();
			double atr = DailyContext != null ? DailyContext.Atr20d : double.NaN;
			m_br = atr > 0 ? Cfg.f * atr : double.NaN;
			if (prior == null || prior.crossesRoll || !(prior.poc > 0)) { m_done = true; return; }
			m_level = controlAdjustLevel(prior.poc);
			string gw;
			if (!pocGate(day, prior.poc, ref m_br, out gw)) { if (journalOn()) trace(ETraceLevel.Journal, $"DV-02 {day:yyyy-MM-dd} gate: {gw}"); m_done = true; return; }
			m_sd = Math.Max(m_br, Cfg.costFloorTicks * tickSize());   // stop = max(structural bracket, 14-tick floor); the target stays f x ATR20d
		}
	}
}
