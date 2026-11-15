// ## 1. Identity
// Catalogue ID: PS-02
// ID mapping: PS-02 -> _PS_02_BalanceBreakClose.Strategy.CS (class _PS_02_BalanceBreakClose).
// Name: PS-02 balance break, market entry after acceptance
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_02_BalanceBreakClose
// File: sandbox/_PS_02_BalanceBreakClose.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-02. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-02 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
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
// logSignal tag: PS-02
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex27CompositeBreakMarket.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 27 - PS-02 balance break, market entry after acceptance (long side shown; short mirrors).
// Filter: as PS-01 (IsBalanced(RTH, n = 3, 0.6)) + V-score aligned (>= +1 long, <= -1 short) + composite entropy Hnorm <= 0.93.
// Entry: at market on the next bar after the 2nd consecutive closed 5m bar beyond composite VAH/VAL + delta, 09:45-14:30 ET.
// Stop: composite VAH - 0.30 W (long) / VAL + 0.30 W (short), floored at 14 ticks from the signal close. Skip the setup (never resize)
// if 0.30 W < ATR14(5m) + 2 ticks (noise) or 0.30 W > 1.5 x daily ATR (catalogue "daily ATR"; operationalised as M-DAILY Atr20d).
// Target: 1.0 W beyond the broken edge. Chart: Data1 = @ES 5m, dxFeed continuous UNADJUSTED.
// Features: F-COMP, F-VP (IsBalanced, ValueMigrationScore), F-PSTAT (ProfileStats.enabled, ProfileEntropy), M-DAILY (Atr20d), F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_02_BalanceBreakClose : _VolumeProfileFuturesBase
	{
		protected sealed class Ps02Config
		{
			public int contracts = 1, n = 3, deltaTicks = 2, acceptCloses = 2, atrLength = 14, noiseTicks = 2;
			public double stopW = 0.30, targetW = 1.0, maxEntropy = 0.93, maxDailyAtr = 1.5, balanceOverlap = 0.6;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(14, 30, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps02Config Cfg { get; } = new Ps02Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private DateTime m_day = DateTime.MinValue;
		private bool m_ok, m_done;
		private int m_vscore, m_up, m_dn;
		private double m_vah, m_val, m_w, m_stop, m_target;

		public _PS_02_BalanceBreakClose(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;
			ProfileStats.enabled = true;              // lazy: F-PSTAT helpers (entropy) only exist when enabled
			FlatBy = Cfg.flat;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS02LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS02SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS02LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS02SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS02LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS02SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS02LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS02ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc() { NeedsDaily = true; base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (rollOn(1)) m_done = true;
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				m_done = true;
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return;
			}
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || !m_ok || !IsRth() || tod < Cfg.from || tod > Cfg.to) return;
			double tick = tickSize(), c = Bars.Close[0], d = Cfg.deltaTicks * tick;
			m_up = c > m_vah + d ? m_up + 1 : 0; m_dn = c < m_val - d ? m_dn + 1 : 0;
			int side = m_up >= Cfg.acceptCloses && m_vscore >= 1 ? +1 : m_dn >= Cfg.acceptCloses && m_vscore <= -1 ? -1 : 0;
			if (side == 0) return;
			m_done = true;                                          // one attempt per day, whatever the skip outcome
			double sw = Cfg.stopW * m_w, atr = averageTrueRange(Bars, Cfg.atrLength);
			double atrD = DailyContext != null ? DailyContext.Atr20d : double.NaN;
			string why = !(atrD > 0) ? "Atr20d NaN" : sw < atr + Cfg.noiseTicks * tick ? "0.30W inside 5m noise" : sw > Cfg.maxDailyAtr * atrD ? "0.30W > 1.5 daily ATR" : null;
			double stop = side > 0 ? m_vah - sw : m_val + sw;
			if (side * (c - stop) < Cfg.costFloorTicks * tick) stop = c - side * Cfg.costFloorTicks * tick;
			if (why == null && Cfg.contracts * Math.Abs(c - stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction)) why = "risk budget";
			if (why != null) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-02 {day:yyyy-MM-dd} skip: {why}"); return; }
			m_stop = stop; m_target = side > 0 ? m_vah + Cfg.targetW * m_w : m_val - Cfg.targetW * m_w;
			if (journalOn()) journalArm(side > 0 ? "PS02LE" : "PS02SE", c, m_stop, m_target, null);
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
		}

		private void newDay(DateTime day)
		{
			m_day = day; m_done = false; m_up = m_dn = 0;
			VpExtra comp = Composite(1, Cfg.n);
			m_ok = comp != null && IsBalanced(1, Cfg.n, Cfg.balanceOverlap);
			if (m_ok)
			{
				EntropyResult h = ProfileEntropy(comp, comp.endBar - comp.startBar + 1);   // mmN = bars in the window (EMmN.Bars convention)
				m_ok = h.Hnorm <= Cfg.maxEntropy;
				m_vah = comp.vah; m_val = comp.val; m_w = comp.vah - comp.val;
				m_ok &= m_w > 0;
			}
			m_vscore = ValueMigrationScore(1, Cfg.n, 0.10, 20, false) ?? 0;
		}
	}
}
