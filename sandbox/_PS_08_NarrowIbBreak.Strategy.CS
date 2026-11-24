// ## 1. Identity
// Catalogue ID: PS-08
// ID mapping: PS-08 -> _PS_08_NarrowIbBreak.Strategy.CS (class _PS_08_NarrowIbBreak).
// Name: PS-08 narrow initial-balance breakout
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_08_NarrowIbBreak
// File: sandbox/_PS_08_NarrowIbBreak.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-08. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-08 heading. Do not paste the entry.
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
// Engine settings enabled: RollDetection = Table (the CME roll-date table from the Data1 profile, contract years 2000-2035, RollTableFromProfile; Off on a non-futures symbol), with the other protected settings the example sets. They are not strategy inputs.
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
// logSignal tag: PS-08
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex15NarrowIbBreak.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 15 - PS-08 narrow initial balance breakout (core arm). IB width <= 33rd percentile of the last 20 sessions;
// after 10:30 ET, 2 consecutive closed 5m bars beyond IB +/- delta -> market; stop back inside by 0.25 x IB (floored); target 1 x IB beyond.
// Chart: Data1 = 1-minute. Data2 = 5-minute. IbMinutes = 60 (default).
// Features: F-SESS (Session.ibHigh/ibLow/ibComplete, IbRange), F-CAL half-days, F-JRNL, roll guard, floor rule.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_08_NarrowIbBreak : _VolumeProfileFuturesBase
	{
		protected sealed class Ps08Config
		{
			public int contracts = 1;
			public TimeSpan from = new TimeSpan(10, 30, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public int lookback = 20;
			public double maxPercentile = 0.33;
			public int deltaTicks = 1, closesNeeded = 2;
			public double stopInsideIb = 0.25, targetIb = 1.0;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps08Config Cfg { get; } = new Ps08Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private readonly List<double> m_ibHist = new List<double>();   // completed IB widths, oldest first
		private DateTime m_day = DateTime.MinValue;
		private bool m_ibLogged, m_narrow, m_done;
		private int m_up, m_dn;
		private double m_stop = double.NaN, m_target = double.NaN;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		[Input] public ESandboxSide Side { get; set; }
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _PS_08_NarrowIbBreak(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25;  Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table; RollTableFromProfile = true;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS08LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS08SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS08LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS08SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS08LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS08SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS08LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS08ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			m_ibHist.Clear(); m_day = DateTime.MinValue; m_ibLogged = m_narrow = m_done = false; m_filledSetup = false; m_restOn = false; m_up = m_dn = 0;
			m_stop = m_target = double.NaN;
		}

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "PS-08 Data2 missing"); return false; }
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
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_ibLogged = m_narrow = m_done = false; m_filledSetup = false; m_restOn = false; m_up = m_dn = 0; }
			if (rollOn(1)) m_done = true;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay, flat = flatAt(day);
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			SessionStats s = Session;
			if (s == null || !s.ibComplete) return;
			double ib = s.IbRange;
			if (!m_ibLogged)                                      // once per session, the first bar after the IB completes
			{
				m_ibLogged = true;
				m_narrow = m_ibHist.Count >= Cfg.lookback && percentileRank(ib) <= Cfg.maxPercentile;
				m_ibHist.Add(ib); if (m_ibHist.Count > Cfg.lookback) m_ibHist.RemoveAt(0);
				if (journalOn()) trace(ETraceLevel.Journal, $"PS-08 {day:yyyy-MM-dd} IB {s.ibLow:F2}-{s.ibHigh:F2} width {ib:F2} narrow({m_narrow})");
			}
			if (!m_narrow || m_done || tod < Cfg.from || tod >= Cfg.to || tod >= flat) return;
			if (!takeNative()) return;
			double tick = tickSize(), c2 = BarsOfData(2).Close[0];
			m_up = c2 > s.ibHigh + Cfg.deltaTicks * tick ? m_up + 1 : 0;
			m_dn = c2 < s.ibLow - Cfg.deltaTicks * tick ? m_dn + 1 : 0;
			int side = m_up >= Cfg.closesNeeded ? +1 : m_dn >= Cfg.closesNeeded ? -1 : 0;
			if (side == 0) return;
			m_done = true;                                        // one attempt per session
			string why;
			if (!hostGate(side, out why)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-08 {Bars.Time[0]:yyyy-MM-dd HH:mm} gate blocked side {side}: {why}"); return; }
			double edge = side > 0 ? s.ibHigh : s.ibLow, entry = BarsOfData(2).Close[0];
			double stop = edge - side * Cfg.stopInsideIb * ib;
			if (side * (entry - stop) < Cfg.costFloorTicks * tick) stop = entry - side * Cfg.costFloorTicks * tick;
			double r = Math.Abs(entry - stop);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-08 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = stop; m_target = edge + side * Cfg.targetIb * ib;
			if (side > 0) { (entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_lxStp.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "ib", ib.ToString("F2") }, { "edge", edge.ToString("F2") } };
				journalArm(side > 0 ? "PS08LE" : "PS08SE", entry, m_stop, m_target, ctx);
				logSignal("PS-08", side, entry, m_stop, ctx, true);
			}
		}

		/// <summary>Fraction of the stored IB widths that are &lt;= w.</summary>
		/// <summary>Extra entry gate for derived strategies (example 21 / MT-03). true = allowed.</summary>
		protected virtual bool hostGate(int side, out string why) { why = null; return true; }

		private double percentileRank(double w)
		{
			int le = 0; foreach (double x in m_ibHist) if (x <= w) le++;
			return m_ibHist.Count == 0 ? 1.0 : (double)le / m_ibHist.Count;
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }

		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
