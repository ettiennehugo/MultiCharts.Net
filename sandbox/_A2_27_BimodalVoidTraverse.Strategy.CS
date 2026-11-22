// ## 1. Identity
// Catalogue ID: A2-27
// ID mapping: A2-27 -> _A2_27_BimodalVoidTraverse.Strategy.CS (class _A2_27_BimodalVoidTraverse).
// Name: A2-27 bimodal void traverse
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_27_BimodalVoidTraverse
// File: sandbox/_A2_27_BimodalVoidTraverse.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-27. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-27 heading. Do not paste the entry.
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
// logSignal tag: A2-27
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex36BimodalVoidTraverse.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 36 - A2-27 bimodal-profile void traverse (long side shown; the short mirror uses E2 / mu1).
// Setup (at the first bar of the new Globex day, from the PRIOR RTH session): Data1 ring bars -> _ProfileStats.Histogram ->
// _ProfileStats.ProfileMixture (kMax 3, BIC, seed MixtureSeed(day), N_eff = bars). Require K == 2, both pi >= 0.30, void depth >= 0.5,
// mu2 - mu1 >= 1.0 x ATR(14) of Data2 (30m). E1 = mu1 + sd1, E2 = mu2 - sd2.
// Deviation: the catalogue fits the prior session's profile; here the histogram is built from the 5m Data1 ring (not 1m rows).
// Entry: price first closes at/below E1, then 2 consecutive closed 5m bars above E1 inside 09:45-14:30 ET -> buy at the next 5m open.
// Stop: mu1 - 1 tick, capped at 1.0 x ATR(14, 5m) from the signal close, then floored at 14 ticks (user rule). Skip if mu2 - close < 1.2R
// or if the risk budget (0.01 x equity) is breached. Target: mu2. Time stop: 90 minutes, flat by 15:55.
// Not coded: T1 50% at E2 (single-contract example). Thresholds are catalogue values, not results.
// Chart: Data1 = 1-minute. Data2 = 5-minute. Data3 = 30-minute. Features: F-PSTAT (ProfileStats.enabled ring, SessionBars,
// Histogram, ProfileMixture, MixtureSeed), F-TM (FlatBy).
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_27_BimodalVoidTraverse : _VolumeProfileFuturesBase
	{
		protected sealed class A227Config
		{
			public int contracts = 1, kMax = 3, acceptCloses = 2, atrLength = 14, costFloorTicks = 14, timeStopMin = 90;
			public double minPi = 0.30, minVoidDepth = 0.5, minSepAtr30 = 1.0, stopCapAtr5 = 1.0, minRR = 1.2, riskBudgetFraction = 0.01;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(14, 30, 0), flat = new TimeSpan(15, 55, 0);
		}
		protected A227Config Cfg { get; } = new A227Config();

		private IOrderMarket m_le, m_lx, m_se, m_sx;
		private IOrderPriced m_lxStp, m_lxLmt, m_sxStp, m_sxLmt;
		private DateTime m_day = DateTime.MinValue, m_entryTime;
		private bool m_ok, m_done, m_armed;
		private int m_up, m_lastPos;
		private double m_mu1, m_mu2, m_e1, m_e2, m_stop, m_target;
		private int m_dn;
		private bool m_shortArmed;

		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_27_BimodalVoidTraverse(object _ctx) : base(_ctx) { Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			ProfileStats.enabled = true;                      // lazy: Data1 ring for SessionBars
			FlatBy = Cfg.flat;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A227LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A227SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A227LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A227SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A227LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A227LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A227SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A227ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_day = DateTime.MinValue; m_lastPos = 0; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "A2-27 Data2 missing"); return false; }
			if (BarsOfData(2).Status != EBarState.Close) return false;
			int bar = BarsOfData(2).CurrentBar;
			if (bar == m_nativeSeen) return false;
			m_nativeSeen = bar;
			return true;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd); }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (rollOn(1)) m_done = true;
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0 && m_lastPos == 0) m_entryTime = Bars.Time[0];
			m_lastPos = pos;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;
				bool flat = (mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero)) || (Bars.Time[0] - m_entryTime).TotalMinutes >= Cfg.timeStopMin;
				if (pos > 0)
				{
					if (flat) { m_lx.Send(); return true; }
					m_lxStp.Send(m_stop); m_lxLmt.Send(m_target);
				}
				else
				{
					if (flat) { m_sx.Send(); return true; }
					m_sxStp.Send(m_stop); m_sxLmt.Send(m_target);
				}
				return true;
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || !m_ok || !IsRth()) return;
			if (!takeNative()) return;
			double c = BarsOfData(2).Close[0], tick = tickSize();
			bool wantLong = _SandboxRules.Allows(Side, true);
			bool wantShort = _SandboxRules.Allows(Side, false);
			if (wantLong && c <= m_e1) { m_armed = true; m_up = 0; }
			if (wantShort && c >= m_e2) { m_shortArmed = true; m_dn = 0; }
			if (wantLong && m_armed && c > m_e1) m_up++;
			if (wantShort && m_shortArmed && c < m_e2) m_dn++;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (tod < Cfg.from || tod > Cfg.to) return;
			bool goLong = wantLong && m_armed && m_up >= Cfg.acceptCloses;
			bool goShort = wantShort && m_shortArmed && m_dn >= Cfg.acceptCloses;
			if (!goLong && !goShort) return;
			m_done = true;
			double atr5 = averageTrueRange(BarsOfData(2), Cfg.atrLength);
			bool buy = goLong;
			double stop = buy ? Math.Max(m_mu1 - tick, c - Cfg.stopCapAtr5 * atr5) : Math.Min(m_mu2 + tick, c + Cfg.stopCapAtr5 * atr5);
			if (buy && c - stop < Cfg.costFloorTicks * tick) stop = c - Cfg.costFloorTicks * tick;
			if (!buy && stop - c < Cfg.costFloorTicks * tick) stop = c + Cfg.costFloorTicks * tick;
			double r = Math.Abs(c - stop);
			double target = buy ? m_mu2 : m_mu1;
			string why = Math.Abs(target - c) < Cfg.minRR * r ? "target < 1.2R"
				: Cfg.contracts * r * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction) ? "risk budget" : null;
			if (why != null) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-27 {day:yyyy-MM-dd} skip: {why}"); return; }
			m_stop = stop; m_target = target;
			if (journalOn()) journalArm(buy ? "A227LE" : "A227SE", c, m_stop, m_target, null);
			if (buy) { (entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_lxStp.Send(m_stop); }
			else { (entryOpen() ? m_se : null)?.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
		}

		private void newDay(DateTime day)
		{
			m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_armed = false; m_shortArmed = false; m_up = 0; m_dn = 0; m_ok = false;
			DateTime prev = priorTradingDay(day);
			if (prev == DateTime.MinValue) return;
			if (!IsExist(3)) { if (journalOn()) trace(ETraceLevel.Journal, "A2-27 " + day.ToString("yyyy-MM-dd") + " no Data3"); return; }
			List<RingBar> bars = SessionBars(prev, true);
			double tick = tickSize(), atr30 = averageTrueRange(BarsOfData(3), Cfg.atrLength);
			double[] px, vol;
			if (bars.Count < 20 || !_ProfileStats.Histogram(bars, null, tick, 2, out px, out vol)) return;
			ProfileMixtureResult m = _ProfileStats.ProfileMixture(px, vol, Cfg.kMax, _ProfileStats.MixtureSeed(prev), EGmmNeff.Bars, bars.Count, tick);
			if (journalOn()) trace(ETraceLevel.Journal, m.Journal(prev));
			if (m.K != 2 || m.pi[0] < Cfg.minPi || m.pi[1] < Cfg.minPi || !(m.voidDepth.Length > 0 && m.voidDepth[0] >= Cfg.minVoidDepth)) return;
			if (!(m.mu[1] - m.mu[0] >= Cfg.minSepAtr30 * atr30)) return;
			m_mu1 = m.mu[0]; m_mu2 = m.mu[1]; m_e1 = m.mu[0] + m.sd[0]; m_e2 = m.mu[1] - m.sd[1];
			m_ok = m_e1 < m.mu[1] - m.sd[1];                       // E1 below E2: a real void between the edges
		}

		private DateTime priorTradingDay(DateTime day)
		{
			int maxAgo = Math.Min(ProfileStats.ringBars, Bars.CurrentBar - 1);
			for (int ago = 0; ago < maxAgo; ago++) { RingBar rb = ringBarOf(1, ago); if (rb.tradingDay < day) return rb.tradingDay; }
			return DateTime.MinValue;
		}
	}
}
