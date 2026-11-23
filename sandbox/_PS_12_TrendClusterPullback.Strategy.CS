// ## 1. Identity
// Catalogue ID: PS-12
// ID mapping: PS-12 -> _PS_12_TrendClusterPullback.Strategy.CS (class _PS_12_TrendClusterPullback).
// Name: PS-12 trend-cluster pullback
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_12_TrendClusterPullback
// File: sandbox/_PS_12_TrendClusterPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-12. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-12 heading. Do not paste the entry.
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
// Entry cutoff: no entry order is sent when its fill bar would end within NoEntryMinutesBeforeFlat (default 25) minutes of the date-aware flat-by cutoff, or after it (catalogue 2.1, early closes included); evening-session entries are allowed.
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
// logSignal tag: PS-12
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex34TrendClusterPullback.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 34 - PS-12 trend-cluster pullback, Dale limit variant (long side, as catalogued).
// Filter: 60m PA structure Up (HH/HL; ExtraPriceAction engine on Data2). Cluster: a closed 30m bar whose largest single-price row
// (BarLadders, bid/ask tiers only) >= 2 x the bar's median row volume, inside a pause (bar range <= 0.7 x ATR30m).
// Validity: 2 later 30m closes above cluster + delta. Entry: buy limit at the cluster price (first touch). Stop: the next composite
// LVN low below the cluster, floored at 14 ticks. Target: the last 30m pivot high (skip if not above the entry). One cluster at a time.
// Chart: Data1 = 1-minute. Data2 = 30-minute. Data3 = 60-minute. dxFeed continuous UNADJUSTED.
// Features: F-LADDER (BarLadders.enabled -> OrderFlowEnabled, Ladder(ago), LadderBar rows/total/HasBidAsk), M-PA (ExtraPriceAction),
// F-COMP (lvns), F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_12_TrendClusterPullback : _VolumeProfileFuturesBase
	{
		protected sealed class Ps12Config
		{
			public int contracts = 1, atrLength = 14, validCloses = 2, deltaTicks = 2, n = 20, ds60 = 3;
			public double clusterMult = 2.0, pauseAtr = 0.7;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps12Config Cfg { get; } = new Ps12Config();

		private IOrderPriced m_leLmt, m_lxStp, m_lxLmt;
		private double m_cluster = double.NaN, m_stop, m_target;
		private int m_above;
		private bool m_valid;

		private bool m_done;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _PS_12_TrendClusterPullback(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			BarLadders.enabled = true; BarLadders.keepBars = 500; BarLadders.htfStreams = new[] { 2 };
			PriceActionConfig.enabled = true; PriceActionConfig.dataStream = 2;
			ExtraPriceAction.Add(new PriceActionConfig { enabled = true, dataStream = Cfg.ds60, role = "trend60" });
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "PS12LE", EOrderAction.Buy));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS12LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS12LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   /* Analyst MC17 (A2-13): DevelopingSource is an [Input]; the chart's saved value replaces the Create() value, so re-apply it */ DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_cluster = double.NaN; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "PS-12 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			if (!IsExist(2) || !IsExist(3)) { if (journalOn()) trace(ETraceLevel.Error, "PS-12 Data2 or Data3 missing"); return; }
			if (rollOn(1)) m_cluster = double.NaN;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{ m_cluster = double.NaN; m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); return true; 
			}
			if (StrategyInfo.MarketPosition > 0 && sendExits()) return;
			_PriceActionEngine pa60 = PriceActionFor(Cfg.ds60, "trend60");
			bool up = pa60 != null && pa60.IsWarm && pa60.Structure(0) == EStructure.Up;
			if (!up) { m_cluster = double.NaN; return; }
			double tick = tickSize();
			if (double.IsNaN(m_cluster)) { findCluster(); return; }
			if (!m_valid)
			{
				if (Bars.Low[0] <= m_cluster) { m_cluster = double.NaN; return; }       // touched before it was validated
				if (!takeNative()) return;
				m_above = BarsOfData(2).Close[0] > m_cluster + Cfg.deltaTicks * tick ? m_above + 1 : 0;
				if (m_above >= Cfg.validCloses) m_valid = arm();
				return;
			}
			(entryOpen() ? m_leLmt : null)?.Send(m_cluster, Cfg.contracts); m_lxStp.Send(m_stop);
			if (Bars.Low[0] <= m_cluster) m_cluster = double.NaN;                      // first touch consumed (filled or not)
		}

		private void findCluster()
		{
			LadderBar lb = HtfLadder(2, 0);
			double atr = averageTrueRange(BarsOfData(2), Cfg.atrLength);
			if (lb == null || !lb.HasBidAsk || !lb.HasRows || lb.crossesRoll || !(atr > 0) || BarsOfData(2).High[0] - BarsOfData(2).Low[0] > Cfg.pauseAtr * atr) return;
			double[] t = (double[])lb.total.Clone();
			Array.Sort(t);
			double med = t[t.Length / 2];
			int best = 0;
			for (int i = 1; i < lb.total.Length; i++) if (lb.total[i] > lb.total[best]) best = i;
			if (!(med > 0) || lb.total[best] < Cfg.clusterMult * med) return;
			m_cluster = lb.rows[best] * lb.tick; m_above = 0; m_valid = false;
		}

		private bool arm()
		{
			double tick = tickSize(), lvn = double.NaN;
			VpExtra comp = Composite(1, Cfg.n);
			if (comp != null) foreach (Node z in comp.lvns) if (z.high < m_cluster && (double.IsNaN(lvn) || z.low > lvn)) lvn = z.low;
			double stop = double.IsNaN(lvn) ? m_cluster - Cfg.costFloorTicks * tick : Math.Min(lvn - tick, m_cluster - Cfg.costFloorTicks * tick);
			_PriceActionEngine pa = PriceActionEngine;
			_Pivot sh = pa != null ? pa.LastHigh(0) : null;
			string why = sh == null || sh.price <= m_cluster ? "no swing high above" : Cfg.contracts * (m_cluster - stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction) ? "risk budget" : null;
			if (why != null) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-12 cluster {m_cluster:F2} skip: {why}"); m_cluster = double.NaN; return false; }
			m_stop = stop; m_target = sh.price;
			if (journalOn()) journalArm("PS12LE", m_cluster, m_stop, m_target, null);
			return true;
		}

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				(entryOpen() ? m_leLmt : null)?.Send(m_restPx, m_restQty);
			}
			else
			{
			}
			return true;
		}

	}
}
