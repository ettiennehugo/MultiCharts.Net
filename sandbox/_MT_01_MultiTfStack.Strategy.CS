// ## 1. Identity
// Catalogue ID: MT-01
// ID mapping: MT-01 -> _MT_01_MultiTfStack.Strategy.CS (class _MT_01_MultiTfStack).
// Name: MT-01 multi-timeframe stack, long only
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _MT_01_MultiTfStack
// File: sandbox/_MT_01_MultiTfStack.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for MT-01. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the MT-01 heading. Do not paste the entry.
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
// logSignal tag: MT-01
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex31MultiTfStack.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 31 - MT-01 multi-timeframe stack (long only, as catalogued).
// Filter: M-DAILY Tsmom(20) > 0; last closed 60m structure Up (HH/HL) from a second PRICEACTION engine on Data3; the latest 15m
// pivot low (third engine, Data2) lies within delta15 of the 20-session composite VAH or the developing RTH POC
// (delta15 = 0.25 x the 15m engine ATR: PLACEHOLDER, not a catalogue value; swing-AVWAP level omitted).
// Entry: the next 5m pivot low labelled HL (Data1 engine), buy stop 1 tick above the confirming bar. Stop: max(HL - 1 tick, floor).
// Target: the last 15m pivot high. RTH 09:45-15:00 ET; one trade per day.
// Chart: Data1 = @ES 5m, Data2 = @ES 15m, Data3 = @ES 60m (same symbol, dxFeed continuous UNADJUSTED).
// Features: M-PA (PriceActionConfig + ExtraPriceAction + PriceActionFor, Structure), M-DAILY (Tsmom), F-COMP, F-VP (RthProfile).
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _MT_01_MultiTfStack : _VolumeProfileFuturesBase
	{
		protected sealed class Mt01Config
		{
			public int contracts = 1, tsmomDays = 20, n = 20, ds15 = 2, ds60 = 3;
			public double nearAtr15 = 0.25;   // PLACEHOLDER
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Mt01Config Cfg { get; } = new Mt01Config();

		private IOrderPriced m_leStp, m_lxStp, m_lxLmt;
		private IOrderMarket m_lx;
		private DateTime m_day = DateTime.MinValue;
		private VpExtra m_comp;
		private bool m_done;
		private double m_trigger = double.NaN, m_stop, m_target;

		public _MT_01_MultiTfStack(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			PriceActionConfig.enabled = true; PriceActionConfig.dataStream = 1;                     // 5m trigger engine
			ExtraPriceAction.Add(new PriceActionConfig { enabled = true, dataStream = Cfg.ds15, role = "pb15" });
			ExtraPriceAction.Add(new PriceActionConfig { enabled = true, dataStream = Cfg.ds60, role = "trend60" });
			FlatBy = Cfg.flat; MaxTradesPerDay = 1;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "MT01LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "MT01LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "MT01LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "MT01LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc() {
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_trigger = double.NaN; m_comp = Composite(1, Cfg.n); }
			if (rollOn(1)) m_done = true;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				if (mustFlatten()) { m_lx.Send(); return true; }
				m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); return true;
			
			}
			if (StrategyInfo.MarketPosition > 0 && sendExits()) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || !IsRth() || tod < Cfg.from || tod > Cfg.to) return;
			if (double.IsNaN(m_trigger)) arm(day);
			if (!double.IsNaN(m_trigger)) m_leStp.Send(m_trigger, Cfg.contracts);
		}

		private void arm(DateTime day)
		{
			_PriceActionEngine pa5 = PriceActionEngine, pa15 = PriceActionFor(Cfg.ds15, "pb15"), pa60 = PriceActionFor(Cfg.ds60, "trend60");
			if (pa5 == null || pa15 == null || pa60 == null || !pa5.IsWarm || !pa15.IsWarm || !pa60.IsWarm || !pa5.NewPivotLow) return;
			double ts = DailyContext != null ? DailyContext.Tsmom(Cfg.tsmomDays) : double.NaN;
			if (!(ts > 0) || pa60.Structure(0) != EStructure.Up) return;
			_Pivot pb = pa15.LastLow(0), sh = pa15.LastHigh(0), hl = pa5.LastLow(0);
			if (pb == null || sh == null || hl == null || hl.label != EPivotLabel.HL) return;
			VpExtra dev = RthProfile();
			double near = Cfg.nearAtr15 * pa15.Atr;
			bool atLevel = (m_comp != null && Math.Abs(pb.price - m_comp.vah) <= near) || (dev != null && dev.poc > 0 && Math.Abs(pb.price - dev.poc) <= near);
			if (!atLevel) return;
			double tick = tickSize(), trig = Bars.High[0] + tick, stop = Math.Min(hl.price - tick, trig - Cfg.costFloorTicks * tick);
			string why = sh.price <= trig ? "15m swing high below trigger" : Cfg.contracts * (trig - stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction) ? "risk budget" : null;
			if (why != null) { if (journalOn()) trace(ETraceLevel.Journal, $"MT-01 {day:yyyy-MM-dd} skip: {why}"); m_done = true; return; }
			m_trigger = trig; m_stop = stop; m_target = sh.price;
			if (journalOn()) journalArm("MT01LE", m_trigger, m_stop, m_target, null);
		}
	}
}
