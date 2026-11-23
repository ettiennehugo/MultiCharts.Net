// ## 1. Identity
// Catalogue ID: PS-21
// ID mapping: PS-21 -> _PS_21_NakedLevelReaction.Strategy.CS (class _PS_21_NakedLevelReaction).
// Name: PS-21 first touch of the nearest naked POC, VAH or VAL
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_21_NakedLevelReaction
// File: sandbox/_PS_21_NakedLevelReaction.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-21. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-21 heading. Do not paste the entry.
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
// logSignal tag: PS-21
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex06NakedLevelReaction.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 6 - PS-21 first touch of the nearest naked POC / VAH / VAL (<= 20 sessions) + 5m rejection close, target 1R.
// Chart: Data1 = @ES 1m with the MultiCharts Volume Profile ON (session profiles), Data2 = @ES 5m, dxFeed continuous UNADJUSTED, 24h.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_21_NakedLevelReaction : _VolumeProfileFuturesBase
	{
		public enum ELevelSet { Poc = 1, ValueEdges = 2 }

		protected sealed class Ps21Config
		{
			public int contracts = 1;
			public int maxAgeSessions = 20;          // sweep {5, 20}
			public ELevelSet levels = ELevelSet.Poc; // sweep {POC, VAH/VAL}
			public int touchTicks = 1;               // delta of "first touch +/- delta"
			public int stopBufferTicks = 4;          // structural stop: this far beyond the level (an assumption of this example)
			public int costFloorTicks = 14;          // catalogue 2.1: ES cost floor; stop distance = max(structural, floor), size never shrunk
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity (PF-04's 0.5% is not the default here)
			public TimeSpan from = new TimeSpan(9, 30, 0), to = new TimeSpan(15, 30, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);   // early-close days: flat at RthCloseEt - 5 min
		}
		protected Ps21Config Cfg { get; } = new Ps21Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private int m_lastRollBar;                       // 1-based Status Bar # of the last roll bar
		private double m_level = double.NaN; private int m_side; private DateTime m_touch5 = DateTime.MinValue, m_last5 = DateTime.MinValue;
		private readonly HashSet<double> m_used = new HashSet<double>();   // first touch only: a level is never traded twice
		private double m_stop = double.NaN, m_target = double.NaN;

		/// <summary>Catalogue 2.1 / App F #9 (resolved): true when Cfg.contracts at this stop distance risk more than the per-trade budget.</summary>
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _PS_21_NakedLevelReaction(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			TrackNakedPoc = true;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS21LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "PS21SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS21LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS21SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS21LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS21SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS21LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS21ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			HtfStreams = new[] { 2 };
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			if (!TrackNakedPoc) throw new ArgumentException("_PS_21_NakedLevelReaction: TrackNakedPoc must be true");
			m_lastRollBar = 0; m_level = double.NaN; m_side = 0; m_touch5 = m_last5 = DateTime.MinValue; m_used.Clear();
			m_stop = m_target = double.NaN;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			int barNo = Bars.FullSymbolData.Current;           // 1-based Status Bar #: the unit of asOfBar (NOT Bars.CurrentBar)
			if (rollOn(1)) { m_lastRollBar = barNo; m_level = double.NaN; m_side = 0; }
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			TimeSpan flat = flatAt(GlobexDate(Bars.Time[0]));
			TimeSpan entryEnd = Cfg.to < flat ? Cfg.to : flat;
			double tick = tickSize();
			int pos = StrategyInfo.MarketPosition;

						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			
			}
			if (pos != 0 && sendExits()) return;

			// a. pending touch: wait for the first 5m bar that CLOSES after the touch (closed 5m bars only)
			DateTime t5; double o5, h5, l5, c5, v5;
			HtfClosedBar(2, 0, out t5, out o5, out h5, out l5, out c5, out v5);
			bool new5 = t5 != m_last5; m_last5 = t5;
			if (m_side != 0 && new5 && t5 >= m_touch5) { checkReject(c5, tod, entryEnd, tick); return; }
			if (m_side != 0 || tod < Cfg.from || tod >= entryEnd) return;

			// b. levels naked as of the PREVIOUS bar, from the last maxAgeSessions completed profiles, none older than the last roll
			double best = double.NaN; int side = 0; double c1 = Bars.Close[1];
			foreach (VpExtra e in RecentCompletedProfiles(1, Cfg.maxAgeSessions, barNo - 1))
			{
				if (e.crossesRoll || e.startBar <= m_lastRollBar) continue;   // multi-day levels reset at rolls
				var cand = new List<double>();
				if (Cfg.levels == ELevelSet.Poc) { if (e.IsNakedPoc(barNo - 1)) cand.Add(e.poc); }
				else { if (IsNakedVah(e, barNo - 1)) cand.Add(e.vah); if (IsNakedVal(e, barNo - 1)) cand.Add(e.val); }
				foreach (double lv in cand)
				{
					if (m_used.Contains(lv)) continue;
					int s = c1 > lv ? +1 : c1 < lv ? -1 : 0;     // approached from above = support (long); from below = resistance (short)
					if (s == 0) continue;
					if (double.IsNaN(best) || Math.Abs(c1 - lv) < Math.Abs(c1 - best)) { best = lv; side = s; }
				}
			}
			if (double.IsNaN(best)) return;
			bool touched = side > 0 ? Bars.Low[0] <= best + Cfg.touchTicks * tick : Bars.High[0] >= best - Cfg.touchTicks * tick;
			if (!touched) return;
			m_used.Add(best); m_level = best; m_side = side; m_touch5 = Bars.Time[0];
			if (journalOn()) trace(ETraceLevel.Journal, $"PS-21 touch {Bars.Time[0]:yyyy-MM-dd HH:mm} level({best}) side({side})");
			if (new5 && t5 >= m_touch5) checkReject(c5, tod, entryEnd, tick);   // the touch bar also closed the 5m bar that holds it
		}

		/// <summary>The first 5m close at or after the touch decides: back on the entry side of the level = rejection, else discard.
		/// Either way the level stays in m_used: a discarded touch uses the level up for good.</summary>
		private void checkReject(double c5, TimeSpan tod, TimeSpan entryEnd, double tick)
		{
			bool reject = m_side > 0 ? c5 > m_level : c5 < m_level;
			if (reject && tod < entryEnd) arm(m_side, Bars.Close[0], tick);
			else if (journalOn()) logSignal("PS-21", m_side, m_level, double.NaN, null, false);
			m_side = 0; m_level = double.NaN;
		}

		private void arm(int side, double planned, double tick)
		{
			double stop = m_level - side * Cfg.stopBufferTicks * tick;
			double r = Math.Abs(planned - stop);
			if (r < Cfg.costFloorTicks * tick) { r = Cfg.costFloorTicks * tick; stop = planned - side * r; }   // catalogue 2.1: widen to the cost floor
			if (overRiskBudget(r))   // widened stop breaches the per-trade risk budget: skipped and logged, not resized
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"PS-21 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}");
				return;
			}
			m_stop = stop;
			m_target = planned + side * r;                     // 1R from the planned (signal-close) price
			var ctx = new Dictionary<string, string> { { "level", m_level.ToString() } };
			if (journalOn()) { journalArm(side > 0 ? "PS21LE" : "PS21SE", planned, m_stop, m_target, ctx); logSignal("PS-21", side, planned, m_stop, ctx, true); }
			if (side > 0) { (entryOpen() ? m_le : null)?.Send(Cfg.contracts); m_lxStp.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(Cfg.contracts); m_sxStp.Send(m_stop); }
		}

		/// <summary>Flat time for a Globex day: Cfg.flat, or on a CME early-close day RthCloseEt - halfDayExitBefore if earlier
		/// (_CmeHolidays.Rules.Get(day).RthCloseEt; same rule as A263Config.halfDayExitBefore).</summary>
		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
