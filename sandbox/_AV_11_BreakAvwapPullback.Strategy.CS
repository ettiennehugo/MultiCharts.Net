// ## 1. Identity
// Catalogue ID: AV-11
// ID mapping: AV-11 -> _AV_11_BreakAvwapPullback.Strategy.CS (class _AV_11_BreakAvwapPullback).
// Name: AV-11 re-entry at the break-anchored AVWAP
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _AV_11_BreakAvwapPullback
// File: sandbox/_AV_11_BreakAvwapPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for AV-11. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the AV-11 heading. Do not paste the entry.
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
// logSignal tag: AV-11
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex28BreakAvwapPullback.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 28 - AV-11 PS-01 break re-entry at the break-anchored AVWAP (long side shown; short mirrors).
// Context: the PS-01 filter (IsBalanced(RTH, 3, 0.6), V-score >= +1). Anchor: the FIRST 5m bar that closes > composite VAH + delta
// (09:45-14:30 ET). The AVWAP and its volume-weighted sigma are accumulated here from that bar (typical price), because the
// M-AVWAP Breakout anchor is a PA break-of-structure event, not a composite-edge break (see pitfall in section 5).
// Entry: the first pullback whose low trades into AVWAP +/- 0.25 sigma and that prints a PA HL; buy stop 1 tick above that bar.
// Stop: max(AVWAP - 1 sigma, 14-tick floor). Target: VAH + 1.0 W (PS-01 target). Invalidation: a 5m close < composite VAH - delta.
// Chart: Data1 = 1-minute. Data2 = 5-minute. Features: F-COMP, F-VP, M-PA, F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _AV_11_BreakAvwapPullback : _VolumeProfileFuturesBase
	{
		protected sealed class Av11Config
		{
			public int contracts = 1, n = 3, deltaTicks = 2;
			public double zoneSigma = 0.25, stopSigma = 1.0, targetW = 1.0, balanceOverlap = 0.6;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(14, 30, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Av11Config Cfg { get; } = new Av11Config();

		private IOrderPriced m_leStp, m_seStp, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private double m_val;
		private DateTime m_day = DateTime.MinValue;
		private bool m_ok, m_done, m_anchored, m_touched, m_longBreak = true;
		private double m_vah, m_w, m_spv, m_sp2v, m_sv, m_trigger = double.NaN, m_stop, m_target;

		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _AV_11_BreakAvwapPullback(object _ctx) : base(_ctx) { Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			PriceActionConfig.enabled = true; PriceActionConfig.dataStream = 2;
			FlatBy = Cfg.flat;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "AV11LE", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "AV11SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV11LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV11SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "AV11LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "AV11SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "AV11LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "AV11ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   /* Analyst MC17 (A2-13): DevelopingSource is an [Input]; the chart's saved value replaces the Create() value, so re-apply it */ base.StartCalc(); m_day = DateTime.MinValue; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "AV-11 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (rollOn(1)) m_done = true;
			double tick = tickSize(), c = Bars.Close[0], d = Cfg.deltaTicks * tick;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				bool invalid = m_longBreak ? c < m_vah - d : c > m_val + d;
				if ((mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero)) || invalid)
				{
					if (StrategyInfo.MarketPosition > 0) m_lx.Send(); else m_sx.Send();
					return true;
				}
				if (StrategyInfo.MarketPosition > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); }
				else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			
			}
			if (StrategyInfo.MarketPosition != 0 && sendExits()) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || !m_ok || !IsRth()) return;
			if (sandboxBook()) return;
			if (!double.IsNaN(m_trigger))
			{
				m_restPx = m_trigger; m_restBuy = m_longBreak; m_restOn = true; m_restQty = Cfg.contracts;
				if (m_longBreak) { m_leStp.Send(m_trigger, Cfg.contracts); m_lxStp.Send(m_stop); }
				else { m_seStp.Send(m_trigger, Cfg.contracts); m_sxStp.Send(m_stop); }
				return;
			}
			if (!takeNative()) return;
			c = BarsOfData(2).Close[0];
			bool wantLong = _SandboxRules.Allows(Side, true);
			bool wantShort = _SandboxRules.Allows(Side, false);
			if (!m_anchored)
			{
				if (tod < Cfg.from || tod > Cfg.to) return;
				if (wantLong && c > m_vah + d) { m_anchored = true; m_longBreak = true; }
				else if (wantShort && c < m_val - d) { m_anchored = true; m_longBreak = false; }
				else return;
				if (journalOn()) trace(ETraceLevel.Journal, "AV-11 " + day.ToString("yyyy-MM-dd") + (m_longBreak ? " long break above VAH" : " short break below VAL"));
			}
			double tp = (BarsOfData(2).High[0] + BarsOfData(2).Low[0] + c) / 3, v = BarsOfData(2).Volume[0];
			m_spv += tp * v; m_sp2v += tp * tp * v; m_sv += v;
			if (!(m_sv > 0)) return;
			double av = m_spv / m_sv, sig = Math.Sqrt(Math.Max(0, m_sp2v / m_sv - av * av));
			if (m_longBreak ? c < m_vah - d : c > m_val + d) { m_done = true; return; }
			if (BarsOfData(2).Low[0] <= av + Cfg.zoneSigma * sig && BarsOfData(2).High[0] >= av - Cfg.zoneSigma * sig) m_touched = true;
			_PriceActionEngine pa = PriceActionEngine;
			bool pivot = m_longBreak ? pa != null && pa.IsWarm && pa.NewPivotLow : pa != null && pa.IsWarm && pa.NewPivotHigh;
			if (m_touched && double.IsNaN(m_trigger) && tod <= Cfg.to && pivot)
			{
				_Pivot p = m_longBreak ? pa.LastLow(0) : pa.LastHigh(0);
				EPivotLabel want = m_longBreak ? EPivotLabel.HL : EPivotLabel.LH;
				if (p != null && p.label == want)
				{
					double trig = m_longBreak ? BarsOfData(2).High[0] + tick : BarsOfData(2).Low[0] - tick;
					double stop = m_longBreak ? Math.Min(av - Cfg.stopSigma * sig, trig - Cfg.costFloorTicks * tick) : Math.Max(av + Cfg.stopSigma * sig, trig + Cfg.costFloorTicks * tick);
					if (Cfg.contracts * Math.Abs(trig - stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction))
					{ if (journalOn()) trace(ETraceLevel.Journal, "AV-11 " + day.ToString("yyyy-MM-dd") + " skip: risk budget"); m_done = true; return; }
					m_trigger = trig; m_stop = stop; m_target = m_longBreak ? m_vah + Cfg.targetW * m_w : m_val - Cfg.targetW * m_w;
					if (journalOn()) journalArm(m_longBreak ? "AV11LE" : "AV11SE", m_trigger, m_stop, m_target, null);
				}
			}
			if (!double.IsNaN(m_trigger))
			{
				m_restPx = m_trigger; m_restBuy = m_longBreak; m_restOn = true; m_restQty = Cfg.contracts;
				if (m_longBreak) { m_leStp.Send(m_trigger, Cfg.contracts); m_lxStp.Send(m_stop); }
				else { m_seStp.Send(m_trigger, Cfg.contracts); m_sxStp.Send(m_stop); }
			}
		}

		private void newDay(DateTime day)
		{
			m_day = day; m_done = m_anchored = m_touched = false; m_trigger = double.NaN; m_spv = m_sp2v = m_sv = 0;
			VpExtra comp = Composite(1, Cfg.n);
			m_ok = comp != null && IsBalanced(1, Cfg.n, Cfg.balanceOverlap) && (ValueMigrationScore(1, Cfg.n, 0.10, 20, false) ?? 0) >= 1;
			if (comp != null) { m_vah = comp.vah; m_val = comp.val; m_w = comp.vah - comp.val; m_ok &= m_w > 0; }
		}

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; }   // trading day never blocks today (Rest returned Done forever)
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				m_leStp.Send(m_restPx, m_restQty);
			}
			else
			{
			}
			return true;
		}

	}
}