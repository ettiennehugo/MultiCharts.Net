// ## 1. Identity
// Catalogue ID: AV-01
// ID mapping: AV-01 -> _AV_01_SwingHighAVWAP.Strategy.CS (class _AV_01_SwingHighAVWAP).
// Name: AV-01 swing-high AVWAP reclaim
// Header version: 1
// Rules version: catalogue specification under the AV-01 heading, rewritten 2026-11-15.
// Study class: _AV_01_SwingHighAVWAP
// File: sandbox/_AV_01_SwingHighAVWAP.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the AV-01 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: the swing-high anchor is the newest confirmed Data2 (60-minute) zigzag high with a reversal of at least 3 x ATR(20) of Data2 (catalogue AV-01: "the last significant swing high (k = 3 x ATR on 60m), usable from confirmation"), read as AvwapManager.Latest(SwingHigh) with the AVWAP on Data2. The daily filter is the last completed daily close (DailyContext.PrevClose) above DailyContext.Sma200 after DailyConfig.smaLength is set to 50. Zigzag pivots never confirm on their own bar (engine fix 2026-11-22). Ten bars below the AVWAP are ten closed Data2 (60-minute) closes. The delta60 margin above the AVWAP is not applied (spec PROPOSED). A 15-minute higher low is built from 1-minute bars. The target is the anchor swing-high price (the Data2 bar high at the anchor time). The EX-04 trail is not sent.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the AV-01 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: see CalcBar. Closed bar only.
// Management: one contract when this file trades. Size is never cut to fit the stop.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: the order type named in the catalogue (market next bar, stop, or limit).
// Can fill: the next bar, or later for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one, unless the catalogue says a reverse, which this file does not open inside GX.
// Re-entry: one attempt per session unless the catalogue says otherwise.
// Same session after a stop: no new entry that session unless the catalogue says otherwise.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 60-minute, the same symbol as Data1 (required).
// Data1: the chart, the 15-minute higher low and the orders. Tests use data through 2024-12-31 only.
// Data2: the anchor and signal timeframe. PRICEACTION AtrZigZag (3 x ATR(20)) and the AVWAP run on Data2; the ten-bars-below count uses closed Data2 bars.
// Further series: Not applicable
// Warmup: the average or profile the rule names.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue trigger and the 1 percent budget when an order is sent.
// Skip when: rollOn on a futures profile idea, the profile or average is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is InstrumentProfile.costFloorTicks times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: named in Create when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. A stop or limit rests. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when section 13 says GX, otherwise sendExits.
//
// ## 13. Exits
// Exit orders: GX. Stop is the higher low minus one tick, and no closer than 0.3 times Atr20d. Target is the anchor-bar high. Hold up to 10 sessions.
// Hard stop: the catalogue stop, widened to the instrument cost floor.
// Target: the catalogue target when it is a price known at the signal.
// Time exit: the catalogue clock, taken from the session API when the rule says the session end.
// Invalidation: any extra exit named in the assumption.
//
// ## 14. Regimes
// Suitable: the session the code consults.
// Unsuitable: outside that session, and a roll day when the rule skips rolls.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: set in Create.
// Engine settings enabled: set in code. RollDetection = Table (the CME roll-date table from the Data1 profile, contract years 2000-2035, RollTableFromProfile; Off on a non-futures symbol). They are not strategy inputs. AvwapLimits.dataStream = 2, AvwapLimits.backfillBars = 400; PriceActionConfig.dataStream = 2, pivotMode = AtrZigZag, atrMultiple = 3.0, atrLength = 20.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Table (CME roll-date table) when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: the swing-high anchor is the newest confirmed Data2 (60-minute) zigzag high with a reversal of at least 3 x ATR(20) of Data2 (catalogue AV-01: "the last significant swing high (k = 3 x ATR on 60m), usable from confirmation"), read as AvwapManager.Latest(SwingHigh) with the AVWAP on Data2. The daily filter is the last completed daily close (DailyContext.PrevClose) above DailyContext.Sma200 after DailyConfig.smaLength is set to 50. Zigzag pivots never confirm on their own bar (engine fix 2026-11-22). Ten bars below the AVWAP are ten closed Data2 (60-minute) closes. The delta60 margin above the AVWAP is not applied (spec PROPOSED). A 15-minute higher low is built from 1-minute bars. The target is the anchor swing-high price (the Data2 bar high at the anchor time). The EX-04 trail is not sent.
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
// Fixed or research-tuned: the catalogue core, not a sweep. The sweep stays in the catalogue.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name when an entry is sent.
// logSignal tag: AV-01
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: the swing-high anchor is the newest confirmed Data2 (60-minute) zigzag high with a reversal of at least 3 x ATR(20) of Data2 (catalogue AV-01: "the last significant swing high (k = 3 x ATR on 60m), usable from confirmation"), read as AvwapManager.Latest(SwingHigh) with the AVWAP on Data2. The daily filter is the last completed daily close (DailyContext.PrevClose) above DailyContext.Sma200 after DailyConfig.smaLength is set to 50. Zigzag pivots never confirm on their own bar (engine fix 2026-11-22). Ten bars below the AVWAP are ten closed Data2 (60-minute) closes. The delta60 margin above the AVWAP is not applied (spec PROPOSED). A 15-minute higher low is built from 1-minute bars. The target is the anchor swing-high price (the Data2 bar high at the anchor time). The EX-04 trail is not sent.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _AV_01_SwingHighAVWAP : _VolumeProfileFuturesBase
	{
		private IOrderPriced m_le; private bool m_done, m_reclaim; private int m_below, m_anchor = -1;
		// Coder 2026-11-22 (Analyst Set2: AV-01 0 trades; the 1-minute strength-1 SwingHigh anchor changed every few minutes, so ten
		// 60-minute closes below one anchor never happened): the closed Data2 (60-minute) bars, for the count and the anchor high.
		private struct NBar { public DateTime t; public double h; }
		private readonly List<NBar> m_nb = new List<NBar>();
		private const int NativeKeep = 1500;   // ~65 Globex sessions of 60m; anchors expire after AvwapLimits.maxAgeSessions (10)
		private DateTime m_nativeTime = DateTime.MinValue;
		/// <summary>True once per new closed Data2 bar (M-MTF closed-bar rule); its values come back in the out parameters.</summary>
		private bool newNativeBar(out DateTime t, out double h, out double c)
		{
			t = DateTime.MinValue; h = c = double.NaN;
			if (!IsExist(2) || BarsOfData(2).CurrentBar < 3) return false;
			double o, l, v; HtfClosedBar(2, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_nativeTime) return false;
			m_nativeTime = t; return true;
		}
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _AV_01_SwingHighAVWAP(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Table; RollTableFromProfile = true; AvwapEvents = EAvwapAnchor.SwingHigh; GxOrderPrefix = "AV01"; createGxOrders(); m_le = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "AV01LE", EOrderAction.Buy)); }
		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc() {
			// Coder 2026-11-22 (catalogue AV-01: swing high k = 3 x ATR on 60m): the anchor runs on Data2 (60m) as a 3 x ATR(20) zigzag.
			AvwapLimits.dataStream = 2; AvwapLimits.backfillBars = 400;   // a zigzag high can confirm many bars after its extreme
			PriceActionConfig.dataStream = 2; PriceActionConfig.pivotMode = EPivotMode.AtrZigZag; PriceActionConfig.atrMultiple = 3.0; PriceActionConfig.atrLength = 20;
			m_nb.Clear(); m_nativeTime = DateTime.MinValue; m_anchor = -1; m_done = false; m_below = 0; m_reclaim = false; m_chkDay = DateTime.MinValue; for (int i = 0; i < m_chk.Length; i++) m_chk[i] = 0;
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DailyConfig.smaLength = 50; DailyConfig.atrLength = 20; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); requireRing(1, BarRingConfig.MaxAutoBars, "AV-01 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */ }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			
			exitBar();
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime nt; double nh, nc;
			bool fresh = newNativeBar(out nt, out nh, out nc);
			if (fresh) { m_nb.Add(new NBar { t = nt, h = nh }); if (m_nb.Count > NativeKeep) m_nb.RemoveRange(0, m_nb.Count - NativeKeep); }
			chkDay();
			bool slot = ((int)sessionClock(Bars.Time[0]).TotalMinutes) % 15 == 0;   // the daily checks count each 15-minute signal slot once
			_Avwap a = anchor(EAvwapAnchor.SwingHigh);
			if (a == null || !a.Valid) { if (slot) m_chk[0]++; return; }
			if (a.AnchorBar != m_anchor)
			{
				m_anchor = a.AnchorBar;
				if (StrategyInfo.MarketPosition == 0)
				{
					if (m_restOn && journalOn()) trace(ETraceLevel.Journal, $"AV-01 {Bars.Time[0]:yyyy-MM-dd HH:mm} entry stop {m_restPx} cancelled unfilled: a new swing-high anchor");   // Coder 2026-11-23
					m_done = false; m_filledSetup = false; m_restOn = false; m_below = 0; m_reclaim = false;
				}
			}
			if (sandboxBook()) return;   // before any position return, so Rest() sees the fill: one attempt per anchor per day
			if (StrategyInfo.MarketPosition != 0 || rollOn(1)) return;
			if (m_done) { if (slot) m_chk[1]++; return; }
			if (fresh && nt > a.UsableFrom)
			{   // closed 60-minute closes after confirmation: ten or more below the AVWAP, then a close above it is the reclaim
				if (nc < a.Value) { m_below++; m_reclaim = false; }
				else if (m_below >= 10) m_reclaim = true;
				else { m_below = 0; m_reclaim = false; }
			}
			// Coder 2026-11-22 (catalogue AV-01 filter "daily close > 50-day SMA"): the last completed daily close against the 50-day
			// SMA of completed closes. It compared the current 1-minute close.
			double dClose = DailyContext.PrevClose, sma = DailyContext.Sma200;   // Sma200 is the SMA of DailyConfig.smaLength (50) closes
			if (!(dClose > sma)) { if (slot) m_chk[2]++; return; }
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (!m_reclaim) { if (slot) { if (m_below >= 10) m_chk[4]++; else m_chk[3]++; } return; }
			if (Bars.Close[0] <= a.Value) { if (slot) m_chk[5]++; return; }
			if (!slot || Bars.CurrentBar < 30) return;
			double lo = Bars.Low[0], prev = Bars.Low[15], hi = Bars.High[0];
			for (int i = 1; i < 15; i++) { lo = Math.Min(lo, lbBar(i).l); prev = Math.Min(prev, lbBar(15 + i).l); hi = Math.Max(hi, lbBar(i).h); }
			if (!(lo > prev) || !(lo > a.Value)) { m_chk[6]++; return; }
			double target = double.NaN;   // the anchor swing high: the Data2 bar high at the anchor time
			int first = -1;
			for (int i = m_nb.Count - 1; i >= 0; i--) { if (m_nb[i].t < a.AnchorTime) break; first = i; }
			if (first > 0 || (first == 0 && m_nb[0].t <= a.AnchorTime)) target = m_nb[first].h;   // the anchor bar (or the first kept bar after it)
			if (double.IsNaN(target)) { m_chk[7]++; return; }   // anchor older than the kept Data2 bars
			double entry = hi + tickSize();
			double atr = DailyContext.Atr20d;
			double stop = lo - tickSize();
			if (atr > 0 && entry - stop < 0.3 * atr) stop = entry - 0.3 * atr;
			stop = widen(true, entry, stop);
			if (!(target > entry)) { m_chk[8]++; m_done = true; return; }
			if (!budget(entry, stop)) { m_chk[9]++; m_done = true; return; }
			m_chk[10]++;
			arm(true, entry, stop, target, "AV01LE", true);
			honorExit(entry, 0); m_restPx = entry; m_restBuy = true; m_restOn = true; m_restQty = _SandboxRules.ScaleQty(ExitMode, false); { if (hostGate(true)) m_le.Send(entry, m_restQty); }

		}
		
		// One Journal line per Globex day (like A2-45). Counts are 15-minute signal slots: noAnchor, anchorDone, dailyFilter (prior daily
		// close not above SMA50), notTenBelow (fewer than ten 60m closes below the AVWAP), belowNoReclaim (ten or more, no 60m close back
		// above yet), closeNotAbove, noHL, targetUnknown, targetNotAbove, budget, sent.
		private readonly int[] m_chk = new int[11]; private DateTime m_chkDay = DateTime.MinValue;
		private void chkDay()
		{
			DateTime d = GlobexDate(Bars.Time[0]);
			if (d == m_chkDay) return;
			if (m_chkDay != DateTime.MinValue && journalOn())
				trace(ETraceLevel.Journal, "AV-01 " + m_chkDay.ToString("yyyy-MM-dd") + " checks noAnchor(" + m_chk[0] + ") anchorDone(" + m_chk[1] + ") dailyFilter(" + m_chk[2] + ") notTenBelow(" + m_chk[3] + ") belowNoReclaim(" + m_chk[4] + ") closeNotAbove(" + m_chk[5] + ") noHL(" + m_chk[6] + ") targetUnknown(" + m_chk[7] + ") targetNotAbove(" + m_chk[8] + ") budget(" + m_chk[9] + ") sent(" + m_chk[10] + ") below60m(" + m_below + ")");
			m_chkDay = d; for (int i = 0; i < m_chk.Length; i++) m_chk[i] = 0;
		}

		private double widen(bool buy, double entry, double stop)
		{
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			double floor = ticks * tickSize();
			if (buy && entry - stop < floor) return entry - floor;
			if (!buy && stop - entry < floor) return entry + floor;
			return stop;
		}
		private bool budget(double a, double b) { return Math.Abs(a - b) > 0 && Math.Abs(a - b) * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private _Avwap anchor(EAvwapAnchor k) { var m = AvwapManager; return m == null ? null : m.Latest(k); }
		private void arm(bool buy, double entry, double stop, double target, string name, bool hold)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = Math.Abs(target - entry) > 0 ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = hold ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.MaxHoldDays = hold ? 10 : 0;
			GxConfig.TimeStopMinutes = 0; GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
		}


		// Coder 2026-11-22 (Analyst native retest of #152: A2-11 / A2-32 entries filled after the 15:55 ET flat-by cutoff): a study whose
		// position is flat by the session cutoff sends no entry on or after the cutoff bar (session end minus 5 minutes, early-close
		// aware) until 18:00 ET. Entries that GX holds overnight (SessionExit HoldOvernight) are not blocked.
		private DateTime m_cutoffLogDay = DateTime.MinValue;
		private bool entryCutoff()
		{
			if (GxConfig.SessionExit == ESessionExit.HoldOvernight || !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd))) return false;   // #146 date-aware cutoff
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_cutoffLogDay) { m_cutoffLogDay = day; if (journalOn()) trace(ETraceLevel.Journal, "entry blocked: at or after the flat-by cutoff"); }
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


		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			int anchor = m_anchor;
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			bool resting = m_restOn;
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			// Coder 2026-11-23 (Analyst MES run fba67ab7: 78 sent, 63 trades): the buy stop rests until the flat-by cutoff of its day, then
			// expires; that anchor is done until a new swing high. The expiry was silent.
			if (step == ERest.Done && resting && !m_filledSetup && journalOn()) trace(ETraceLevel.Journal, $"AV-01 {Bars.Time[0]:yyyy-MM-dd HH:mm} entry stop {m_restPx} expired unfilled at the flat-by cutoff; anchor done until a new swing high");
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				{ if (hostGate(true)) m_le.Send(m_restPx, m_restQty); }
			}
			else
			{
			}
			return true;
		}

	}
}