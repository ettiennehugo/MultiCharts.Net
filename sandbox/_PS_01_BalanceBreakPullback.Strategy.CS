// ## 1. Identity
// Catalogue ID: PS-01
// ID mapping: PS-01 -> _PS_01_BalanceBreakPullback.Strategy.CS (class _PS_01_BalanceBreakPullback).
// Name: PS-01 balance break, then the first higher-low pullback
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_01_BalanceBreakPullback
// File: sandbox/_PS_01_BalanceBreakPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-01. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-01 heading. Do not paste the entry.
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
// logSignal tag: PS-01
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex26CompositeBreakPullback.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 26 - PS-01 balance break, acceptance, then the first higher-low pullback (long side shown; short mirrors).
// Filter: IsBalanced(Own RTH profiles, n = 3, 0.6) at the prior close; V-score >= +1 for longs (<= -1 shorts); break 09:45-14:30 ET.
// Acceptance: 2 closed 5m bars > composite VAH + delta (W = composite VAH - VAL). Entry: the first pullback that holds above
// VAH - 0.25 W and prints a PA pivot low labelled HL; buy stop 1 tick above the confirming bar. Stop: max(1 tick below the HL, floor);
// hard invalidation on a 5m close < VAH - delta. Target: VAH + 1.0 W; stop to break-even at +1 R.
// Chart: Data1 = @ES 5m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-COMP (Composite), F-VP (IsBalanced, ValueMigrationScore, OwnProfileWindow), M-PA (HL pivots), F-TM (FlatBy).
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_01_BalanceBreakPullback : _VolumeProfileFuturesBase
	{
		protected sealed class Ps01Config
		{
			public int contracts = 1, n = 3, deltaTicks = 2, acceptCloses = 2;
			public double holdW = 0.25, targetW = 1.0, balanceOverlap = 0.6;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(14, 30, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps01Config Cfg { get; } = new Ps01Config();

		private IOrderPriced m_leStp, m_seStp, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_day = DateTime.MinValue;
		private bool m_balanced, m_done;
		private int m_vscore, m_up, m_dn;
		protected int m_side;
		protected double m_vah, m_val, m_w;
		protected DateTime m_acceptTime = DateTime.MinValue;
		private double m_trigger = double.NaN, m_stop, m_target, m_entryRisk;

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _PS_01_BalanceBreakPullback(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off;  Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			PriceActionConfig.enabled = true; PriceActionConfig.dataStream = 1;
			FlatBy = Cfg.flat;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS01LE", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS01SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS01LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS01SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS01LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS01SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS01LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS01ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			base.StartCalc();
			m_day = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (rollOn(1)) m_done = true;
			double tick = tickSize(), c = Bars.Close[0], d = Cfg.deltaTicks * tick;
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				if (mustFlatten() || (pos > 0 ? c < m_vah - d : c > m_val + d)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }  // hard invalidation
				double e = StrategyInfo.AvgEntryPrice;
				if (pos > 0 ? Bars.High[0] >= e + m_entryRisk : Bars.Low[0] <= e - m_entryRisk) m_stop = pos > 0 ? Math.Max(m_stop, e) : Math.Min(m_stop, e);   // BE at +1R
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || !m_balanced || !(m_w > 0) || !IsRth() || tod > Cfg.to) return;
			if (sandboxBook()) return;
			if (m_side == 0)
			{
				if (tod < Cfg.from) return;
				m_up = c > m_vah + d ? m_up + 1 : 0; m_dn = c < m_val - d ? m_dn + 1 : 0;
				if (m_up >= Cfg.acceptCloses && m_vscore >= 1) { m_side = +1; m_acceptTime = Bars.Time[0]; }
				else if (m_dn >= Cfg.acceptCloses && m_vscore <= -1) { m_side = -1; m_acceptTime = Bars.Time[0]; }
				if (m_side != 0 && journalOn()) trace(ETraceLevel.Journal, $"PS-01 {day:yyyy-MM-dd} accepted {(m_side > 0 ? "above VAH" : "below VAL")} W={m_w:F2} V={m_vscore}");
				return;
			}
			double hold = m_side > 0 ? m_vah - Cfg.holdW * m_w : m_val + Cfg.holdW * m_w;
			if (m_side > 0 ? Bars.Low[0] < hold : Bars.High[0] > hold) { m_done = true; return; }   // pullback failed to hold
			_PriceActionEngine pa = PriceActionEngine;
			if (pa != null && pa.IsWarm)
			{
				_Pivot p = m_side > 0 ? (pa.NewPivotLow ? pa.LastLow(0) : null) : (pa.NewPivotHigh ? pa.LastHigh(0) : null);
				if (p != null && p.label == (m_side > 0 ? EPivotLabel.HL : EPivotLabel.LH))
				{
					m_trigger = m_side > 0 ? Bars.High[0] + tick : Bars.Low[0] - tick;
					double stop = p.price - m_side * tick;
					if (m_side * (m_trigger - stop) < Cfg.costFloorTicks * tick) stop = m_trigger - m_side * Cfg.costFloorTicks * tick;
					m_entryRisk = Math.Abs(m_trigger - stop);
					if (overRiskBudget(m_entryRisk)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-01 {day:yyyy-MM-dd} skip: stop {m_entryRisk / tick:F0} ticks breaches risk budget"); m_done = true; return; }
					m_stop = stop; m_target = m_side > 0 ? m_vah + Cfg.targetW * m_w : m_val - Cfg.targetW * m_w;
					if (journalOn()) journalArm(m_side > 0 ? "PS01LE" : "PS01SE", m_trigger, m_stop, m_target, null);
				}
			}
			if (double.IsNaN(m_trigger)) return;
			if (!legSkewAllows(m_side)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-01 {day:yyyy-MM-dd} skip: leg skew gate"); m_done = true; return; }
			if (!hostGate(m_side > 0)) { if (journalOn()) trace(ETraceLevel.Journal, "PS-01 gate " + Gate + " blocked"); return; }
			int qty = ExitMode == ESandboxExit.Partial1R || ExitMode == ESandboxExit.Partial15R ? 2 : Cfg.contracts;
			if (qty > 1 && journalOn()) trace(ETraceLevel.Warning, "PS-01 multi-contract size " + qty);
			if (qty > 1 && qty * m_entryRisk * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Error, "PS-01 skipped: multi-contract risk exceeds 1 percent"); m_done = true; return; }
			if (m_side > 0) { m_leStp.Send(m_trigger, qty); m_lxStp.Send(m_stop); }
			else { m_seStp.Send(m_trigger, qty); m_sxStp.Send(m_stop); }
		}

		private bool hostGate(bool buy)
		{
			if (Gate == ESandboxGate.Off) return true;
			if (Session == null) return false;
			VpExtra prior = PriorRthProfile();
			if (Gate == ESandboxGate.ValueMigration)
			{
				if (prior == null) return false;
				return buy ? Bars.Close[0] >= prior.poc : Bars.Close[0] <= prior.poc;
			}
			double atr = averageTrueRange(Bars, 14);
			if (Gate == ESandboxGate.Balance) return atr > 0 && (Session.rthHigh - Session.rthLow) < atr;
			if (Gate == ESandboxGate.Noise) return atr > 0 && (Bars.High[0] - Bars.Low[0]) < atr;
			if (Gate == ESandboxGate.Trend) return !double.IsNaN(DailyContext.Sma200) && (buy ? Session.priorRthClose > DailyContext.Sma200 : Session.priorRthClose < DailyContext.Sma200);
			if (Gate == ESandboxGate.Compression) return !double.IsNaN(DailyContext.Atr20d) && (Session.rthHigh - Session.rthLow) < 0.6 * DailyContext.Atr20d;
			return true;
		}
		private void newDay(DateTime day)
		{
			m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_up = m_dn = m_side = 0; m_trigger = double.NaN; m_acceptTime = DateTime.MinValue;
			VpExtra comp = Composite(1, Cfg.n);                       // the n completed RTH profiles before today
			m_balanced = comp != null && IsBalanced(1, Cfg.n, Cfg.balanceOverlap);
			int? v = ValueMigrationScore(1, Cfg.n, 0.10, 20, false);  // completed sessions only (as of the prior close)
			m_vscore = v ?? 0;
			if (comp != null) { m_vah = comp.vah; m_val = comp.val; m_w = comp.vah - comp.val; } else m_w = double.NaN;
		}

		// A2-15 overrides this. The base returns true, so PS-01's orders are unchanged.
		protected virtual bool legSkewAllows(int side) { return true; }

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }

		private bool sandboxBook()
		{
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(sessionClock(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				m_leStp.Send(m_restPx, m_restQty);
			}
			else
			{
				m_seStp.Send(m_restPx, m_restQty);
			}
			return true;
		}

	}
}