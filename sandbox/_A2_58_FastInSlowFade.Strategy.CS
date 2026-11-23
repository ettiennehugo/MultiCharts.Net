// ## 1. Identity
// Catalogue ID: A2-58
// ID mapping: A2-58 -> _A2_58_FastInSlowFade.Strategy.CS (class _A2_58_FastInSlowFade).
// Name: A2-58 fade the fast trend inside the slow trend
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_58_FastInSlowFade
// File: sandbox/_A2_58_FastInSlowFade.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-58. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-58 heading. Do not paste the entry.
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
// logSignal tag: A2-58
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex23FastInSlowFade.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 23 - A2-58 fade the fast trend inside the slow trend (Kaufman), intraday 5m.
// Slow direction = sign(SMA_S[t] - SMA_S[t-1]); fast direction likewise with SMA_F. Long: slow up and fast turns from up to down on this
// closed bar, 09:45-15:00 ET; market next bar. Mirror short. Exit: slow direction turns, or 15:55. Disaster stop 2.0 x ATR(14, 5m), floored.
// S = 30, F = 3 (one point of the catalogue's 4 x 3 grid, not a tuned value).
// Chart: Data1 = 1-minute. Data2 = 5-minute. dxFeed continuous UNADJUSTED.
// Features: CME clock adapter on a plain _CustomSignal, F-TM (NoEntryBefore/After, FlatBy, entryAllowed, mustFlatten), averageTrueRange.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_58_FastInSlowFade : _CustomSignal
	{
		protected sealed class A258Config
		{
			public int contracts = 1, slow = 30, fast = 3, atrLength = 14;
			public double stopAtr = 2.0;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A258Config Cfg { get; } = new A258Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private _CmeSessionClock m_clock;
		private double m_slowPrev = double.NaN, m_fastPrev = double.NaN, m_stop;
		private int m_slowDir, m_fastDir;

		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_58_FastInSlowFade(object _ctx) : base(_ctx) { Side = ESandboxSide.Both; }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			NoEntryBefore = new TimeSpan(9, 45, 0); NoEntryAfter = new TimeSpan(15, 0, 0); FlatBy = new TimeSpan(15, 55, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A258LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A258SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A258LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A258SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A258LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A258SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}


		protected virtual void configureSessions()
		{
			TimeSpan pitStart, pitEnd, pitValue;
			TimeSpan listed = Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero;
			_SandboxRules.PitWindows(Bars.Info.Name, listed, out pitStart, out pitEnd, out pitValue);
			Clock.RthStart = pitStart;
			Clock.RthEnd = pitEnd;
			Clock.RthValueEnd = pitValue;
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			m_clock = null;
			configureSessions();
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			m_slowPrev = m_fastPrev = double.NaN; m_slowDir = m_fastDir = 0;
		}

		private double sma(int n) { double s = 0; for (int i = 0; i < n; i++) s += BarsOfData(2).Close[i]; return s / n; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "A2-58 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, Clock.RthValueEnd)); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			bool step = takeNative() && BarsOfData(2).CurrentBar > Cfg.slow;
			int prevSlow = m_slowDir, prevFast = m_fastDir;
			if (step)
			{
				if (rollOn(1)) { m_slowPrev = m_fastPrev = double.NaN; m_slowDir = m_fastDir = 0; }   // directions restart; the SMAs still span the roll gap for S bars
				double sl = sma(Cfg.slow), fs = sma(Cfg.fast);
				int slowDir = double.IsNaN(m_slowPrev) ? 0 : Math.Sign(sl - m_slowPrev);
				int fastDir = double.IsNaN(m_fastPrev) ? 0 : Math.Sign(fs - m_fastPrev);
				prevSlow = m_slowDir; prevFast = m_fastDir;
				m_slowPrev = sl; m_fastPrev = fs;
				if (slowDir != 0) m_slowDir = slowDir;
				if (fastDir != 0) m_fastDir = fastDir;
			}

			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if (mustFlatten() || m_slowDir != Math.Sign(pos)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			string why;
			if (!entryAllowed(out why) || !isRthBar(Bars.Time[0])) return;
			int side = 0;
			if (m_slowDir > 0 && prevFast > 0 && m_fastDir < 0 && prevSlow > 0) side = +1;
			else if (m_slowDir < 0 && prevFast < 0 && m_fastDir > 0 && prevSlow < 0) side = -1;
			if (side == 0 || !step) return;
			double atr = averageTrueRange(BarsOfData(2), Cfg.atrLength), tick = tickSize(), c = BarsOfData(2).Close[0];
			if (!(atr > 0)) return;
			double r = Math.Max(Cfg.stopAtr * atr, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-58 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget"); return; }
			m_stop = c - side * r;                                    // approximate: from the signal close, not the fill
			if (side > 0) { (entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_lxStp.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
			if (journalOn()) journalArm(side > 0 ? "A258LE" : "A258SE", c, m_stop, double.NaN, null);
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
