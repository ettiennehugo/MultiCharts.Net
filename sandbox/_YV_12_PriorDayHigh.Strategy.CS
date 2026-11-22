// ## 1. Identity
// Catalogue ID: YV-12
// ID mapping: YV-12 -> _YV_12_PriorDayHigh.Strategy.CS (class _YV_12_PriorDayHigh).
// Name: YV-12 prior-day wick rejection
// Header version: 1
// Rules version: catalogue specification under the YV-12 heading, rewritten 2026-11-15.
// Study class: _YV_12_PriorDayHigh
// File: sandbox/_YV_12_PriorDayHigh.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the YV-12 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: a Day-2 session has a prior RTH range of at least 1.5 times Atr20d. A short needs a 5-minute bar that tags the prior high, with an upper wick of at least half the bar, a close in the bottom quarter and a range of at least 1.0 times the 14-bar 5-minute ATR (the 5-minute bar is Data2). The open must not already be at the prior high. The sell stop is one tick under that bar. The stop is the wick high. The target is the prior low. The long at the prior low is the mirror. The time stop is 30 minutes.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the YV-12 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute. Data2 = 5-minute.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: the same instrument on 5-minute bars (owner rule: Data2 = native timeframe). CalcBar reads closed Data2 bars only, through HtfClosedBar.
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
// Exit orders: GX. Stop at the wick extreme, widened to the cost floor. Target is the other side of the prior range. TimeStopMinutes is 30. Flat at the session value end.
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
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: ASSUMPTION: a Day-2 session has a prior RTH range of at least 1.5 times Atr20d. A short needs a 5-minute bar that tags the prior high, with an upper wick of at least half the bar, a close in the bottom quarter and a range of at least 1.0 times the 14-bar 5-minute ATR (the 5-minute bar is Data2). The open must not already be at the prior high. The sell stop is one tick under that bar. The stop is the wick high. The target is the prior low. The long at the prior low is the mirror. The time stop is 30 minutes.
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
// logSignal tag: YV-12
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: a Day-2 session has a prior RTH range of at least 1.5 times Atr20d. A short needs a 5-minute bar that tags the prior high, with an upper wick of at least half the bar, a close in the bottom quarter and a range of at least 1.0 times the 14-bar 5-minute ATR (the 5-minute bar is Data2). The open must not already be at the prior high. The sell stop is one tick under that bar. The stop is the wick high. The target is the prior low. The long at the prior low is the mirror. The time stop is 30 minutes.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _YV_12_PriorDayHigh : _VolumeProfileFuturesBase
	{
		private IOrderPriced m_le, m_se; private bool m_done; private DateTime m_day = DateTime.MinValue;
		// Coder 2026-11-22: Data2 (the native timeframe) read through the M-MTF closed-bar rule. newNativeBar() is true once per new
		// closed Data2 bar; nativeBar(ago) is the ago-th closed Data2 bar; nativeAtr(n) is the mean true range of the last n closed bars.
		private DateTime m_nativeTime = DateTime.MinValue;
		private bool nativeReady(int ago) { return IsExist(2) && BarsOfData(2).CurrentBar > ago + 2; }
		private bool newNativeBar()
		{
			if (!nativeReady(0)) return false;
			DateTime t; double o, h, l, c, v; HtfClosedBar(2, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_nativeTime) return false;
			m_nativeTime = t; return true;
		}
		private void nativeBar(int ago, out double o, out double h, out double l, out double c, out double v) { DateTime t; HtfClosedBar(2, ago, out t, out o, out h, out l, out c, out v); }
		private double nativeAtr(int n)
		{
			if (!nativeReady(n)) return double.NaN;
			double sum = 0, o, h, l, c, v, po, ph, pl, pc, pv;
			for (int k = 0; k < n; k++)
			{
				nativeBar(k, out o, out h, out l, out c, out v); nativeBar(k + 1, out po, out ph, out pl, out pc, out pv);
				sum += Math.Max(h, pc) - Math.Min(l, pc);
			}
			return sum / n;
		}
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _YV_12_PriorDayHigh(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; Side = ESandboxSide.Both; }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Series; GxOrderPrefix = "YV12"; createGxOrders(); m_le = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "YV12LE", EOrderAction.Buy)); m_se = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "YV12SE", EOrderAction.SellShort)); }
		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DailyConfig.atrLength = 20; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); }

		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar();
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime day = GlobexDate(Bars.Time[0]); if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; }
			if (sandboxBook()) return;   // before any position return, so Rest() sees the fill: one attempt per anchor per day
			if (StrategyInfo.MarketPosition != 0 || m_done || !IsRth() || Bars.CurrentBar < 20) return;
			double pdh = Session.priorRthHigh, pdl = Session.priorRthLow, atr = DailyContext.Atr20d;
			if (!(pdh > pdl) || !(atr > 0) || pdh - pdl < 1.5 * atr) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			// Coder 2026-11-22: the wick candle is the last closed Data2 5-minute bar, and it must be "distinct": its range at least
			// 1.0 x ATR14 of the 5-minute bars (spec). It was one 1-minute bar compared with the daily ATR20, so it almost never fired.
			if (tod < m_rthStart.Add(TimeSpan.FromMinutes(15)) || tod > m_rthStart.Add(TimeSpan.FromMinutes(120)) || !newNativeBar()) return;
			double bo, bh, bl, bc, bv; nativeBar(0, out bo, out bh, out bl, out bc, out bv);
			double atr14 = nativeAtr(14);
			double span = bh - bl; if (!(atr14 > 0) || !(span >= 1.0 * atr14)) return;
			double upper = bh - Math.Max(bo, bc);
			double lower = Math.Min(bo, bc) - bl;
			bool openAtHigh = Session.rthOpen >= pdh - 0.10 * (pdh - pdl);
			bool openAtLow = Session.rthOpen <= pdl + 0.10 * (pdh - pdl);
			bool sell = !openAtHigh && bh >= pdh - 0.10 * atr && bh <= pdh + 0.5 * atr && upper >= 0.5 * span && bc <= bl + 0.25 * span;
			bool buy = !openAtLow && bl <= pdl + 0.10 * atr && bl >= pdl - 0.5 * atr && lower >= 0.5 * span && bc >= bh - 0.25 * span;
			if (!sell && !buy) return;
			double entry = sell ? bl - tickSize() : bh + tickSize();
			double stop = widen(!sell, entry, sell ? bh + tickSize() : bl - tickSize());
			double target = sell ? pdl : pdh;
			if ((sell && !(target < entry)) || (!sell && !(target > entry)) || !budget(entry, stop)) { m_done = true; return; }
			m_timeStop = 30;
			arm(!sell, entry, stop, target, sell ? "YV12SE" : "YV12LE", 0);
			m_restPx = entry; m_restBuy = !sell; m_restOn = true; m_restQty = 1; if (sell) { if (hostGate(false)) m_se.Send(entry); } else { if (hostGate(true)) m_le.Send(entry); }
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
		private int m_timeStop, m_flatOverride;
		private bool m_atrTrail;
		private bool budget(double a, double b) { double r = Math.Abs(a - b); return r > 0 && r * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private void arm(bool buy, double entry, double stop, double target, string name, int holdDays)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = m_atrTrail ? ETrailMode.Atr : ETrailMode.None;
			GxConfig.SessionExit = holdDays > 0 ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.MaxHoldDays = holdDays;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(m_flatOverride, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.TimeStopMinutes = m_timeStop; GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
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
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				{ if (hostGate(true)) m_le.Send(m_restPx, m_restQty); }
			}
			else
			{
				{ if (hostGate(false)) m_se.Send(m_restPx, m_restQty); }
			}
			return true;
		}

	}
}