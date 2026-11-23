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
// Exit orders go through exitPlan and exitBar.
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
// Chart: Data1 = 1-minute. Data2 = 5-minute.
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
// Interactions: the flat-by cutoff (FlatBy, early-close aware); entries GX holds overnight (SessionExit HoldOvernight) are not blocked.
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
// logSignal tag: PS-02
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex27CompositeBreakMarket.
// IOG is disabled. Exits go through exitPlan and exitBar.

// Manual v4 example 27 - PS-02 balance break, market entry after acceptance (long side shown; short mirrors).
// Filter: as PS-01 (IsBalanced(RTH, n = 3, 0.6)) + V-score aligned (>= +1 long, <= -1 short) + composite entropy Hnorm <= 0.93.
// Entry: at market on the next bar after the 2nd consecutive closed 5m bar beyond composite VAH/VAL + delta, 09:45-14:30 ET.
// Stop: composite VAH - 0.30 W (long) / VAL + 0.30 W (short), floored at 14 ticks from the signal close. Skip the setup (never resize)
// if 0.30 W < ATR14(5m) + 2 ticks (noise) or 0.30 W > 1.5 x daily ATR (catalogue "daily ATR"; operationalised as M-DAILY Atr20d).
// Target: 1.0 W beyond the broken edge. Chart: Data1 = 1-minute. Data2 = 5-minute. dxFeed continuous UNADJUSTED.
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

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _PS_02_BalanceBreakClose(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			GxOrderPrefix = "PS02"; createGxOrders();
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

		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   /* Analyst MC17 (A2-13): DevelopingSource is an [Input]; the chart's saved value replaces the Create() value, so re-apply it */ DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_day = DateTime.MinValue; }

		private int m_nativeSeen = -1;
		private bool takeNative()
		{
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "PS-02 Data2 missing"); return false; }
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
			int pos = StrategyInfo.MarketPosition;
			exitBar();
			if (pos != 0) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (m_done || !m_ok || !IsRth() || tod < Cfg.from || tod > Cfg.to) return;
			if (!takeNative()) return;
			double tick = tickSize(), c = BarsOfData(2).Close[0], d = Cfg.deltaTicks * tick;
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
			takeGx(side > 0, c, m_stop, m_target, side > 0 ? "PS02LE" : "PS02SE");
			if (side > 0) { if (hostGate(true)) m_le.Send(Cfg.contracts); } else { if (hostGate(false)) m_se.Send(Cfg.contracts); }
		}

		private void takeGx(bool longSide, double entry, double stop, double target, string name)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 1, mkt, name);
		}

		private void newDay(DateTime day)
		{
			m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_up = m_dn = 0;
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

		// Coder 2026-11-22 (Analyst native retest of #152: A2-11 / A2-32 entries filled after the 15:55 ET flat-by cutoff): a study whose
		// position is flat by the session cutoff sends no entry on or after the cutoff bar (session end minus 5 minutes, early-close
		// aware) until 18:00 ET. Entries that GX holds overnight (SessionExit HoldOvernight) are not blocked.
		private DateTime m_cutoffLogDay = DateTime.MinValue;
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryCutoff()
		{
			if (GxConfig.SessionExit == ESessionExit.HoldOvernight || !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat)) return false;   // #146 date-aware cutoff
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_cutoffLogDay) { m_cutoffLogDay = day; if (journalOn()) trace(ETraceLevel.Journal, "entry blocked: " + gxEntryCutoffNote(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat)); }
			return true;
		}
		private bool hostGate(bool buy)
		{
			if (entryCutoff()) return false;   // Coder 2026-11-22: no entry on or after the flat-by cutoff bar (it would fill after the cutoff)
			if (Gate == ESandboxGate.Off) return true;
			double poc = double.NaN, sma = double.NaN, prev = double.NaN, atr = double.NaN, hi = Bars.High[0], lo = Bars.Low[0];
			int fam = 0, ranges = 0;
			string notReady;
			try
			{
				SessionStats s = Session;
				if (s != null) { hi = s.rthHigh; lo = s.rthLow; prev = s.priorRthClose; }
				atr = averageTrueRange(Bars, 14);
				if (Gate == ESandboxGate.ValueMigration) { VpExtra prior = PriorRthProfile(); if (prior != null) poc = prior.poc; }
				if (_SandboxRules.GateReadsDaily(Gate) && NeedsDaily) { _DailyContext d = DailyContext; if (d != null) sma = d.Sma200; }
				if (Gate == ESandboxGate.ThreeFamily && DvpConfig.enabled) dvpFamiliesAt(1, Bars.Close[0], 0.3, out fam, out ranges);
				notReady = _SandboxRules.GateNotReady(Gate, poc, sma, prev, atr, DvpConfig.enabled);
			}
			catch (Exception ex) { notReady = ex.GetType().Name; }
			if (notReady != null) { gateWarn(notReady); return false; }
			bool ok = _SandboxRules.GateAllows(Gate, buy, Bars.Close[0], poc, hi, lo, atr, sma, prev, fam);
			gateLog(ok, buy);
			return ok;
		}

		// Gate dependencies (owner-approved follow-up to batch 1). Gate is an input, so what it reads is turned on in StartCalc,
		// before base.StartCalc builds the engines. Gate = Off changes nothing; switching back to Off restores what was forced.
		private bool m_gateOwnForced, m_gateDailyForced, m_gateDvpForced;
		private EDevelopingSource m_gateSrcWas; private EOwnProfileWindow m_gateWinWas;
		private DateTime m_gateWarnDay = DateTime.MinValue;
		private void applyGateNeeds()
		{
			if (m_gateOwnForced) { DevelopingSource = m_gateSrcWas; OwnProfileWindow = m_gateWinWas; m_gateOwnForced = false; }
			if (m_gateDailyForced) { NeedsDaily = false; m_gateDailyForced = false; }
			if (m_gateDvpForced) { DvpConfig.enabled = false; m_gateDvpForced = false; }
			m_gateWarnDay = m_gateLogDay = DateTime.MinValue;
			bool own, daily, dvp;
			_SandboxRules.GateNeeds(Gate, false, out own, out daily, out dvp);
			if (own && !(DevelopingSource == EDevelopingSource.Own && OwnProfileWindow == EOwnProfileWindow.Rth))
			{ m_gateSrcWas = DevelopingSource; m_gateWinWas = OwnProfileWindow; DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth; m_gateOwnForced = true; }
			if (daily && !NeedsDaily) { NeedsDaily = true; m_gateDailyForced = true; }
			if (dvp && !DvpConfig.enabled) { DvpConfig.enabled = true; m_gateDvpForced = true; }
			if (Gate != ESandboxGate.Off && traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, "gate " + Gate + " needs ownRth(" + own + ") daily(" + daily + ") dvp(" + dvp + ") forced(" + m_gateOwnForced + "," + m_gateDailyForced + "," + m_gateDvpForced + ")");
		}

		// The decision is journalled when it changes within a Globex day, not on every bar.
		private DateTime m_gateLogDay = DateTime.MinValue; private int m_gateLogState;
		private void gateLog(bool ok, bool buy)
		{
			int st = (ok ? 1 : 2) + (buy ? 0 : 2);
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day == m_gateLogDay && st == m_gateLogState) return;
			m_gateLogDay = day; m_gateLogState = st;
			if (journalOn()) trace(ETraceLevel.Journal, "gate " + Gate + (ok ? " allowed " : " blocked ") + (buy ? "buy" : "sell"));
		}

		// A gate whose data is not ready blocks; the reason is a Warning once per Globex day.
		private void gateWarn(string why)
		{
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day == m_gateWarnDay) return;
			m_gateWarnDay = day;
			if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "gate " + Gate + " not ready (" + why + "): blocked");
		}
		private void honorExit(double entry, double risk)
		{
			double t1; int pct; bool be;
			if (!_SandboxRules.ScaleExit(ExitMode, out t1, out pct, out be)) return;
			int qty = _SandboxRules.ScaleQty(ExitMode, false);
			if (qty > 1 && journalOn()) trace(ETraceLevel.Warning, "multi-contract size " + qty);
			if (qty > 1 && risk > 0 && qty * risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{
				if (journalOn()) trace(ETraceLevel.Error, "skipped: multi-contract risk exceeds 1 percent");
				m_done = true; m_restOn = false; return;
			}
			GxConfig.TargetMode = pct > 0 ? ETargetMode.ScaleOutThenRunner : ETargetMode.SingleTarget;
			GxConfig.T1Unit = ETriggerUnit.RMultiple;
			GxConfig.T1Value = t1;
			GxConfig.T1ScalePct = pct;
			GxConfig.FinalTargetUnit = ETriggerUnit.RMultiple;
			GxConfig.FinalTargetValue = t1;
			if (be) GxConfig.BreakEven = EBreakEven.AfterT1;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
		}

	}
}