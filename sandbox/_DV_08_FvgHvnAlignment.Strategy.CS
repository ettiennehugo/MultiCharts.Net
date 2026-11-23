// ## 1. Identity
// Catalogue ID: DV-08
// ID mapping: DV-08 -> _DV_08_FvgHvnAlignment.Strategy.CS (class _DV_08_FvgHvnAlignment).
// Name: DV-08 FVG and HVN alignment
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _DV_08_FvgHvnAlignment
// File: sandbox/_DV_08_FvgHvnAlignment.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for DV-08. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the DV-08 heading. Do not paste the entry.
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
// Market tag: both
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
// logSignal tag: DV-08
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex33FvgHvnAlignment.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 33 - DV-08 Fair Value Gap + heavy-volume alignment as a level layer (bullish side, as catalogued).
// Level: a new bullish FVG on closed 30m bars (Fvgs, minAtrFrac = g x ATR30m; g = 0.25 is a PLACEHOLDER). The level is the FVG start
// edge (high of bar 1, fvgLevel(f, true)) and is kept only if it lies within 0.2 x ATR30m of an HVN edge of the range profile of the
// last 20 closed 30m bars (RangeProfileBars). Entry (DV-03/04 host style): buy limit at the level while the gap is live and untouched,
// valid until the gap fills or expires. Bracket: +/- f x ATR20d, f = 0.10 (catalogue grid point); the stop is widened to the 14-tick floor if narrower; skipped only on a risk-budget breach.
// Chart: Data1 = 1-minute. Data2 = 30-minute. Features: F-FVG (Fvgs, FvgDetector.NewThisBar/Live, fvgLevel -> LevelRef),
// F-RANGE (RangeProfiles.enabled, RangeProfileBars), M-DAILY (Atr20d), F-TM.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _DV_08_FvgHvnAlignment : _VolumeProfileFuturesBase
	{
		protected sealed class Dv08Config
		{
			public int contracts = 1, profileBars = 20;
			public double gapAtr = 0.25, alignAtr = 0.2, f = 0.10;   // gapAtr PLACEHOLDER; alignAtr and f from the catalogue
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Dv08Config Cfg { get; } = new Dv08Config();

		private IOrderPriced m_leLmt, m_lxStp, m_lxLmt;
		private Fvg m_gap;
		private double m_level, m_br, m_sd;

		private bool m_done;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _DV_08_FvgHvnAlignment(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table; RollTableFromProfile = true;
			Fvgs.enabled = true; Fvgs.dataStream = 2; Fvgs.minAtrFrac = Cfg.gapAtr; Fvgs.atrLen = 14; Fvgs.maxAgeSessions = 5;
			RangeProfiles.enabled = true; RangeProfiles.streams = new[] { 2 };
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV08LE", EOrderAction.Buy));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV08LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV08LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_gap = null; }

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			if (rollOn(1)) m_gap = null;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_gap = null;
				double e = StrategyInfo.AvgEntryPrice;
				m_lxStp.Send(e - m_sd); m_lxLmt.Send(e + m_br); return true;
			
			}
			if (StrategyInfo.MarketPosition > 0 && sendExits()) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "DV-08 Data2 missing"); return; }
			_FvgDetector det = FvgDetector;
			if (det == null) return;
			Fvg nf = det.NewThisBar;
			if (nf != null && nf.dir > 0) consider(nf, det.Atr);
			if (m_gap == null) return;
			if (!m_gap.IsLive || m_gap.touched) { m_gap = null; return; }       // first touch only
			(entryOpen() ? m_leLmt : null)?.Send(m_level, Cfg.contracts);
		}

		private void consider(Fvg f, double atr30)
		{
			LevelRef lv = fvgLevel(f, true);
			VpExtra rp = RangeProfileBars(2, 0, Cfg.profileBars);
			double atrD = DailyContext != null ? DailyContext.Atr20d : double.NaN, tick = tickSize();
			if (lv == null || lv.crossesRoll || rp == null || !(atr30 > 0) || !(atrD > 0)) return;
			bool aligned = false;
			foreach (Node n in rp.hvns)
				if (Math.Abs(lv.price - n.high) <= Cfg.alignAtr * atr30 || Math.Abs(lv.price - n.low) <= Cfg.alignAtr * atr30) { aligned = true; break; }
			double br = Cfg.f * atrD, sd = Math.Max(br, Cfg.costFloorTicks * tick);   // stop = max(structural, 14-tick floor)
			string why = !aligned ? "no HVN edge within 0.2 ATR30m"
				: Cfg.contracts * sd * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction) ? "risk budget" : null;
			if (why != null) { if (journalOn()) trace(ETraceLevel.Journal, $"DV-08 {lv.tag} skip: {why}"); return; }
			m_gap = f; m_level = lv.price; m_br = br; m_sd = sd;
			if (journalOn()) journalArm("DV08LE", m_level, m_level - sd, m_level + br, null);
		}

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				(entryOpen() ? m_leLmt : null)?.Send(m_restPx, m_restQty);
			}
			else
			{
			}
			return true;
		}

	}
}
