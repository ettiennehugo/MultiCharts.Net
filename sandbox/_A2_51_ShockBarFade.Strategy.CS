// ## 1. Identity
// Catalogue ID: A2-51
// ID mapping: A2-51 -> _A2_51_ShockBarFade.Strategy.CS (class _A2_51_ShockBarFade).
// Name: A2-51 intraday shock-bar fade with the delayed stop
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_51_ShockBarFade
// File: sandbox/_A2_51_ShockBarFade.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-51. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-51 heading. Do not paste the entry.
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
// logSignal tag: A2-51
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex25ShockBarFade.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 25 - A2-51 intraday shock-bar fade with the delayed stop (Kaufman Ch 16), exit arm E1 (volume decay).
// Signal (closed 5m bar b, 10:00-15:00 ET): TR_b >= k x same-slot TOD median TR (N = 100 sessions; the catalogue's SSB average is
// approximated by the normaliser's MEDIAN), close in the top 20% of the bar (-> short) or bottom 20% (-> long). Bars whose window contains
// 08:30 / 10:00 / 14:00 ET +/- 2 min are skipped. Entry: market next bar. Stop: disaster entry -/+ 1.5 x TR_b throughout; from the close of
// b+2 a stop beyond the b..b+2 extreme + 1 tick (both floored at 14 ticks). Exit E1: the first closed bar with volume <= the same-slot TOD
// median volume; E0 15:55 as backstop. k = 2.5.
// Chart: Data1 = 1-minute. Data2 = 5-minute. dxFeed continuous UNADJUSTED.
// Features: M-TOD (TodMetrics = TrueRange | Volume, TodSessions = 100, Valid, Median), F-SESS, F-TM (FlatBy, mustFlatten).
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_51_ShockBarFade : _VolumeProfileFuturesBase
	{
		protected sealed class A251Config
		{
			public int contracts = 1;
			public double k = 2.5, closeZone = 0.2, disasterTr = 1.5;
			public TimeSpan from = new TimeSpan(10, 0, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A251Config Cfg { get; } = new A251Config();
		private static readonly TimeSpan[] Releases = { new TimeSpan(8, 30, 0), new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0) };

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private int m_shockBar = -1, m_side;
		private double m_shockTr, m_stop;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_51_ShockBarFade(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			TodMetrics = ETodMetric.TrueRange | ETodMetric.Volume; TodSessions = 100; TodMinSessions = 20; TodBucketMinutes = 5;
			FlatBy = Cfg.flat;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A251LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A251SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A251LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A251SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A251LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A251SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 base.StartCalc(); m_shockBar = -1; m_side = 0; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "A2-51 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close || Bars.CurrentBar < 2) return;
			double tick = tickSize(), floor = Cfg.costFloorTicks * tick;
			int pos = StrategyInfo.MarketPosition;
			_TodNormaliser tod = TodNormaliser;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if ((mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero))) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				int since = (IsExist(2) ? BarsOfData(2).CurrentBar : Bars.CurrentBar) - m_shockBar;   // 1 = next 5-minute bar
				if (since == 1)
				{
					double e = StrategyInfo.AvgEntryPrice, d = Math.Max(Cfg.disasterTr * m_shockTr, floor);
					m_stop = e - Math.Sign(pos) * d;
				}
				else if (since >= 2)
				{
					double ext = pos > 0 ? Math.Min(BarsOfData(2).Low[0], Math.Min(BarsOfData(2).Low[1], BarsOfData(2).Low[2])) - tick
					                     : Math.Max(BarsOfData(2).High[0], Math.Max(BarsOfData(2).High[1], BarsOfData(2).High[2])) + tick;
					if (since == 2) { double e = StrategyInfo.AvgEntryPrice; if (Math.Abs(e - ext) < floor) ext = e - Math.Sign(pos) * floor; m_stop = pos > 0 ? Math.Max(m_stop, ext) : Math.Min(m_stop, ext); }
				}
				if (since >= 2 && tod != null && tod.Valid(ETodMetric.Volume) && IsExist(2) && BarsOfData(2).Volume[0] <= tod.Median(ETodMetric.Volume))
				{ if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }      // E1 volume decay
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			if (rollOn(1) || tod == null || !tod.Valid(ETodMetric.TrueRange)) return;
			DateTime et = ToEastern(Bars.Time[0]);
			TimeSpan t = et.TimeOfDay;
			if (!IsRth() || t < Cfg.from || t > Cfg.to || nearRelease(t)) return;
			if (!takeNative()) return;
			double h = BarsOfData(2).High[0], l = BarsOfData(2).Low[0], c = BarsOfData(2).Close[0];
			double tr = Math.Max(h, BarsOfData(2).Close[1]) - Math.Min(l, BarsOfData(2).Close[1]);
			double med = tod.Median(ETodMetric.TrueRange);
			if (!(med > 0) || tr < Cfg.k * med || !(h > l)) return;
			double loc = (c - l) / (h - l);
			int side = loc >= 1 - Cfg.closeZone ? -1 : loc <= Cfg.closeZone ? +1 : 0;
			if (side == 0) return;
			double r = Math.Max(Cfg.disasterTr * tr, floor);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-51 {et:yyyy-MM-dd HH:mm} skip: disaster stop {r / tick:F0} ticks breaches risk budget"); return; }
			m_shockBar = BarsOfData(2).CurrentBar; m_shockTr = tr; m_stop = c - side * r;
			if (side > 0) { m_le.Send(Cfg.contracts); m_lxStp.Send(m_stop); } else { m_se.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
			if (journalOn()) trace(ETraceLevel.Journal, $"A2-51 {et:yyyy-MM-dd HH:mm} shock TR {tr:F2} = {tr / med:F2} x median; fade {(side > 0 ? "long" : "short")}");
		}

		private bool nearRelease(TimeSpan barEndEt)
		{
			TimeSpan start = barEndEt - TimeSpan.FromMinutes(5);
			foreach (TimeSpan r in Releases) if (r >= start - TimeSpan.FromMinutes(2) && r <= barEndEt + TimeSpan.FromMinutes(2)) return true;
			return false;
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
