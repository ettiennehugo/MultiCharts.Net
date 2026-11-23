// ## 1. Identity
// Catalogue ID: OF-07
// ID mapping: OF-07 -> _OF_07_BurstExtremeBreak.Strategy.CS (class _OF_07_BurstExtremeBreak).
// Name: OF-07 bar-level burst at a fresh extreme
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _OF_07_BurstExtremeBreak
// File: sandbox/_OF_07_BurstExtremeBreak.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for OF-07. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the OF-07 heading. Do not paste the entry.
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
// Entry cutoff: no entry order is sent when its fill bar would end within NoEntryMinutesBeforeFlat (default 25) minutes of the date-aware flat-by cutoff, or after it (catalogue 2.1, early closes included); evening-session entries are allowed.
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
// Strategy inputs besides the inherited TraceLevel are listed below.
// Fixed or research-tuned: Not applicable
//
// Input: NoEntryMinutesBeforeFlat
// Purpose: no new entry in the last N minutes before the date-aware flat-by cutoff (catalogue 2.1: no new entries after 15:30; flat by 15:55).
// Default: 25 minutes (owner 2026-11-22 22:50 CT).
// Units: minutes
// Range: 0 or more. 0 disables the window (the flat-by cutoff itself still blocks). A negative value acts as 0.
// Effect: an entry order is not sent when its fill bar (the next bar) would end at or after the cutoff minus N minutes. An early close moves it (ES 13:10 ET flat: none from 12:45 ET). Evening-session entries are not affected.
// Interactions: the flat-by cutoff (FlatBy, early-close aware).
// Fixed or research-tuned: fixed
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
// logSignal tag: OF-07
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex37BurstExtremeBreak.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 37 - OF-07 bar-level burst at a fresh extreme, standalone form (both sides).
// Event: a closed 1m RTH bar takes out the session's RTH high (low) so far, M-TICK flags a print-rate burst on that bar
// (TickStats.valid && TickStats.burst, z >= TickConfig.burstZ), and its range >= 1.5 x the mean range of the prior 20 bars.
// Deviations (stated): the catalogue's volume z >= 2 and "clock-minute norm" are operationalised as the M-TICK print-rate burst and a
// rolling 20-bar range mean; ON extremes are not used. Entry: market next bar in the break direction, 09:35-15:30 ET, max 3 per day.
// Stop: the break bar's midpoint, floored at 14 ticks. Target b = 0.75R (catalogue grid {0.5, 0.75, 1}). Risk budget 0.01 x equity.
// Chart: Data1 @ES 1m, dxFeed continuous UNADJUSTED (M-TICK loads the same symbol's 1-tick Trade stream via DataLoader).
// Features: M-TICK (TickConfig, TickStats), CS-7 (MaxTradesPerDay, FlatBy, entryAllowed), M-PORT (portfolioArm, portfolioStop).
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _OF_07_BurstExtremeBreak : _VolumeProfileFuturesBase
	{
		protected sealed class Of07Config
		{
			public int contracts = 1, rangeLen = 20, costFloorTicks = 14;
			public double minRangeMult = 1.5, targetR = 0.75, riskBudgetFraction = 0.01;
			public TimeSpan from = new TimeSpan(9, 35, 0), to = new TimeSpan(15, 30, 0), flat = new TimeSpan(15, 55, 0);
		}
		protected Of07Config Cfg { get; } = new Of07Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private DateTime m_day = DateTime.MinValue;
		private double m_hi, m_lo, m_stop, m_target;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _OF_07_BurstExtremeBreak(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			TickConfig.enabled = true;                         // lazy: no DataLoader tick request unless enabled
			TickConfig.burstZ = 3.0;
			MaxTradesPerDay = 3; FlatBy = Cfg.flat;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "OF07LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "OF07SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "OF07LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "OF07SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "OF07LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "OF07SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "OF07LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "OF07ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); requireRing(1, 2000, "OF-07 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */ m_day = DateTime.MinValue; }

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_hi = double.NaN; m_lo = double.NaN; }
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if ((mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero))) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				portfolioStop(m_stop);                        // M-PORT: no-op outside Portfolio Trader
			
			return false;
			}
			if (pos != 0 && sendExits()) return;
			if (!IsRth()) return;
			double h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0], tick = tickSize();
			bool first = double.IsNaN(m_hi);
			int side = first ? 0 : h > m_hi ? +1 : l < m_lo ? -1 : 0;
			if (h > m_hi || first) m_hi = h;
			if (l < m_lo || first) m_lo = l;
			if (side == 0 || pos != 0 || rollOn(1)) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (tod < Cfg.from || tod > Cfg.to) return;
			string reason;
			if (!entryAllowed(out reason)) return;
			TickBarStats ts = TickStats;
			if (ts == null || !ts.valid || !ts.burst) return;
			if (Bars.CurrentBar <= Cfg.rangeLen) return;
			double mean = 0;
			for (int i = 1; i <= Cfg.rangeLen; i++) mean += lbBar(i).h - lbBar(i).l;
			mean /= Cfg.rangeLen;
			if (h - l < Cfg.minRangeMult * mean) return;
			double stop = (h + l) / 2;
			if (side * (c - stop) < Cfg.costFloorTicks * tick) stop = c - side * Cfg.costFloorTicks * tick;
			double r = Math.Abs(c - stop);
			if (Cfg.contracts * r * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction))
			{ if (journalOn()) trace(ETraceLevel.Journal, $"OF-07 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: risk budget"); return; }
			m_stop = stop; m_target = c + side * Cfg.targetR * r;
			if (journalOn()) journalArm(side > 0 ? "OF07LE" : "OF07SE", c, m_stop, m_target, null);
			portfolioArm(side, c, m_stop, Cfg.contracts);    // M-PORT: no-op outside Portfolio Trader
			if (side > 0) { (entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_lxStp.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
		}
	}
}
