// ## 1. Identity
// Catalogue ID: PS-05
// ID mapping: PS-05 -> _PS_05_RejectedRepricingOpen.Strategy.CS (class _PS_05_RejectedRepricingOpen).
// Name: PS-05 rejected repricing open
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_05_RejectedRepricingOpen
// File: sandbox/_PS_05_RejectedRepricingOpen.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-05. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-05 heading. Do not paste the entry.
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
// logSignal tag: PS-05
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex30RejectedRepricingOpen.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 30 - PS-05 rejected repricing open (Keller IX-3; gap-up side shown in words, both sides coded).
// Filter: RTH open outside the prior RTH value area by >= 0.25 x ATR20d, with |open - prior RTH close| <= 1.5 x ATR20d.
// Signal: by 10:15 ET, two 5m closes back inside the prior VA (gap up: close <= prior VAH). Entry: sell stop 1 tick through the low
// of the second inside bar (buy stop through the high for a gap down). Stop: max(session extreme + 1 tick, 14-tick floor).
// Target: prior POC (the "then opposite VA edge" leg is not coded: one target). The prior profile is skipped if it crosses a roll.
// Chart: Data1 = @ES 5m, dxFeed continuous UNADJUSTED. Features: F-VP (PriorRthProfile), F-SESS (Session), M-DAILY (Atr20d), F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_05_RejectedRepricingOpen : _VolumeProfileFuturesBase
	{
		protected sealed class Ps05Config
		{
			public int contracts = 1, insideCloses = 2;
			public double minOutsideAtr = 0.25, maxGapAtr = 1.5;
			public TimeSpan signalBy = new TimeSpan(10, 15, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps05Config Cfg { get; } = new Ps05Config();

		private IOrderPriced m_leStp, m_seStp, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_day = DateTime.MinValue;
		private bool m_opened, m_done;
		private int m_side, m_inside;
		private double m_vah, m_val, m_poc, m_trigger = double.NaN, m_stop;

		public _PS_05_RejectedRepricingOpen(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   // PriorRthProfile needs Own
			FlatBy = Cfg.flat;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS05LE", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS05SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS05LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS05SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS05LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS05SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS05LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS05ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() { NeedsDaily = true; base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_opened = m_done = false; m_side = m_inside = 0; m_trigger = double.NaN; }
			if (rollOn(1)) m_done = true;
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_poc); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_poc); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || !IsRth()) return;
			SessionStats s = Session;
			double tick = tickSize(), c = Bars.Close[0];
			if (!m_opened)
			{
				m_opened = true;                                   // first closed RTH bar: classify the open once
				VpExtra prior = PriorRthProfile();
				double atr = DailyContext != null ? DailyContext.Atr20d : double.NaN;
				if (prior == null || prior.crossesRoll || !(atr > 0) || s == null || !(s.rthOpen > 0)) { m_done = true; return; }
				m_vah = prior.vah; m_val = prior.val; m_poc = prior.poc;
				double o = s.rthOpen, gap = Math.Abs(o - s.priorRthClose);
				if (gap > Cfg.maxGapAtr * atr) { m_done = true; return; }
				m_side = o >= m_vah + Cfg.minOutsideAtr * atr ? -1 : o <= m_val - Cfg.minOutsideAtr * atr ? +1 : 0;   // fade side
				if (m_side == 0) { m_done = true; return; }
			}
			if (double.IsNaN(m_trigger))
			{
				if (ToEastern(Bars.Time[0]).TimeOfDay > Cfg.signalBy) { m_done = true; return; }
				bool inside = m_side < 0 ? c <= m_vah : c >= m_val;
				m_inside = inside ? m_inside + 1 : 0;
				if (m_inside < Cfg.insideCloses) return;
				double trig = m_side < 0 ? Bars.Low[0] - tick : Bars.High[0] + tick;
				double stop = m_side < 0 ? Math.Max(s.rthHigh + tick, trig + Cfg.costFloorTicks * tick) : Math.Min(s.rthLow - tick, trig - Cfg.costFloorTicks * tick);
				if (m_side * (m_poc - trig) <= 0 || Cfg.contracts * Math.Abs(trig - stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction))
				{ if (journalOn()) trace(ETraceLevel.Journal, $"PS-05 {day:yyyy-MM-dd} skip: target behind trigger or risk budget"); m_done = true; return; }
				m_trigger = trig; m_stop = stop;
				if (journalOn()) journalArm(m_side > 0 ? "PS05LE" : "PS05SE", m_trigger, m_stop, m_poc, null);
			}
			if (m_side > 0) m_leStp.Send(m_trigger, Cfg.contracts); else m_seStp.Send(m_trigger, Cfg.contracts);
		}
	}
}
