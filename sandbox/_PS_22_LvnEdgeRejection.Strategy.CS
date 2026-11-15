// ## 1. Identity
// Catalogue ID: PS-22
// ID mapping: PS-22 -> _PS_22_LvnEdgeRejection.Strategy.CS (class _PS_22_LvnEdgeRejection).
// Name: PS-22 composite LVN-edge rejection
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_22_LvnEdgeRejection
// File: sandbox/_PS_22_LvnEdgeRejection.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-22. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-22 heading. Do not paste the entry.
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
// logSignal tag: PS-22
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex29LvnEdgeRejection.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 29 - PS-22 composite LVN-edge rejection, with the HVN-ceiling veto as a separate (switchable) layer.
// Level: LVN edges of the 20-session RTH composite (Composite(1, 20).lvns). Long: a 5m bar trades down into an LVN (low <= lvn.high)
// from above and closes back above lvn.high + delta in its upper half (rejection close, PS-16 without the slowness filter).
// Short mirrors at lvn.low. Entry: market next bar. Stop: max(bar extreme -/+ 1 tick, 14-tick floor). Target 1 R.
// Veto (Cfg.hvnVeto, tested separately): skip a long whose 1 R target lies beyond the low of the nearest composite HVN above.
// Window 09:45-15:00 ET, one trade per day. Chart: Data1 = @ES 5m, dxFeed continuous UNADJUSTED. Features: F-COMP (lvns/hvns), F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_22_LvnEdgeRejection : _VolumeProfileFuturesBase
	{
		protected sealed class Ps22Config
		{
			public int contracts = 1, n = 20, deltaTicks = 1;
			public bool hvnVeto = false;              // the veto layer is its own test arm
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps22Config Cfg { get; } = new Ps22Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private DateTime m_day = DateTime.MinValue;
		private VpExtra m_comp;
		private bool m_done;
		private double m_stop, m_target;

		public _PS_22_LvnEdgeRejection(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			FlatBy = Cfg.flat; MaxTradesPerDay = 1;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS22LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS22SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS22LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS22SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS22LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS22SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS22LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS22ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() { base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_comp = Composite(1, Cfg.n); }
			if (rollOn(1)) m_done = true;
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || m_comp == null || m_comp.crossesRoll || !IsRth() || tod < Cfg.from || tod > Cfg.to || !entryAllowed(out string _)) return;
			double tick = tickSize(), d = Cfg.deltaTicks * tick, h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0], mid = (h + l) / 2;
			foreach (Node z in m_comp.lvns)
			{
				int side = 0;
				if (l <= z.high && Bars.Close[1] > z.high && c > z.high + d && c >= mid) side = +1;       // support edge held
				else if (h >= z.low && Bars.Close[1] < z.low && c < z.low - d && c <= mid) side = -1;    // resistance edge held
				if (side == 0) continue;
				double stop = side > 0 ? Math.Min(l - tick, c - Cfg.costFloorTicks * tick) : Math.Max(h + tick, c + Cfg.costFloorTicks * tick);
				double r = Math.Abs(c - stop), target = c + side * r;
				string why = null;
				if (Cfg.contracts * r * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction)) why = "risk budget";
				else if (Cfg.hvnVeto && side > 0 && hvnCeilingBelow(c, target)) why = "1R target beyond HVN ceiling";
				m_done = true;
				if (why != null) { logSignal("PS22", side, c, stop, null, false); if (journalOn()) trace(ETraceLevel.Journal, $"PS-22 {day:yyyy-MM-dd} skip: {why}"); return; }
				m_stop = stop; m_target = target;
				logSignal("PS22", side, c, stop, null, true);
				if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
				return;
			}
		}

		private bool hvnCeilingBelow(double entry, double target)
		{
			double best = double.MaxValue;
			foreach (Node n in m_comp.hvns) if (n.low > entry && n.low < best) best = n.low;
			return best < target;
		}
	}
}
