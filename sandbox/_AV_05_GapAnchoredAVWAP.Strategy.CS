// ## 1. Identity
// Catalogue ID: AV-05
// ID mapping: AV-05 -> _AV_05_GapAnchoredAVWAP.Strategy.CS (class _AV_05_GapAnchoredAVWAP).
// Name: AV-05 gap-anchored AVWAP
// Header version: 1
// Rules version: catalogue specification under the AV-05 heading, rewritten 2026-11-15.
// Study class: _AV_05_GapAnchoredAVWAP
// File: sandbox/_AV_05_GapAnchoredAVWAP.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the AV-05 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the gap anchor is an average this file starts at the RTH open of a day whose gap is at least 0.75 times Atr20d and whose close is in the gap direction. Roll days are skipped. The filter is two later sessions that do not trade back into the prior RTH range. The entry is the first 60-minute low within 0.5 times Atr20d of that average. The stop is the gap-fill price, the prior RTH extreme.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the AV-05 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute. Data2 = 60-minute.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: Not applicable unless CalcBar reads it.
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
// Exit orders: GX. Stop at the gap-fill price, widened to the cost floor. Hold up to 10 sessions. No trail, because GX has no AVWAP trail.
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
// Other: ASSUMPTION: ASSUMPTION: the gap anchor is an average this file starts at the RTH open of a day whose gap is at least 0.75 times Atr20d and whose close is in the gap direction. Roll days are skipped. The filter is two later sessions that do not trade back into the prior RTH range. The entry is the first 60-minute low within 0.5 times Atr20d of that average. The stop is the gap-fill price, the prior RTH extreme.
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
// logSignal tag: AV-05
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: the gap anchor is an average this file starts at the RTH open of a day whose gap is at least 0.75 times Atr20d and whose close is in the gap direction. Roll days are skipped. The filter is two later sessions that do not trade back into the prior RTH range. The entry is the first 60-minute low within 0.5 times Atr20d of that average. The stop is the gap-fill price, the prior RTH extreme.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _AV_05_GapAnchoredAVWAP : _VolumeProfileFuturesBase
	{
		
		private IOrderMarket m_le, m_se; private bool m_gapChecked;
		private DateTime m_day = DateTime.MinValue;
		private double m_pv, m_vv, m_fill; private int m_dir, m_held; private bool m_done, m_alive;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _AV_05_GapAnchoredAVWAP(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Series; GxOrderPrefix = "AV05"; createGxOrders(); m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV05LE", EOrderAction.Buy)); m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV05SE", EOrderAction.SellShort)); }
		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DailyConfig.atrLength = 20; base.StartCalc(); requireRing(1, 2000, "AV-05 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */ }

		/// <summary>Analyst MC17 (A2-35 held over Good Friday 2023): GX resolves one flat-by clock per run, so on an early close,
		/// holiday halt or abbreviated session the open position is closed here at that day's session end minus 5 minutes.</summary>
		private void sandboxEarlyFlat()
		{
			int pos = StrategyInfo.MarketPosition;
			if (pos == 0 || GxConfig == null || GxConfig.SessionExit != ESessionExit.FlatBy) return;
			string why;
			if (!_SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd, out why)) return;
			if (journalOn()) trace(ETraceLevel.Journal, "AV-05 " + GlobexDate(Bars.Time[0]).ToString("yyyy-MM-dd") + " flat-by early close: " + why + ", closing " + Math.Abs(pos));
			gxNoteExit("FLATBY");
			gxSendExit("FLATBY", false, new ExitPosition(), pos > 0, Math.Abs(pos));
		}
		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar(); sandboxEarlyFlat();
			if (sandboxBook()) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day)
			{
				m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_gapChecked = false;
				if (m_alive && !IsNoRthDay(day)) m_held++;   // Analyst MC17: count RTH trading days, not holiday Globex dates
			}
			// Analyst MC17: the gap is read on the first RTH bar. At the 18:00 rollover Session.rthOpen is 0, so Gap was always 0 and
			// the study never armed (the A2-13 defect class).
			if (!m_gapChecked && IsRth() && Session != null && Session.rthOpen > 0)
			{
				m_gapChecked = true;
				SessionStats s = Session;
				double atr = DailyContext.Atr20d;
				if (!m_alive && !rollOn(1) && atr > 0 && s.priorRthClose > 0 && Math.Abs(s.Gap) >= 0.75 * atr)
				{
					m_dir = s.Gap > 0 ? 1 : -1; m_fill = m_dir > 0 ? s.priorRthHigh : s.priorRthLow;
					m_pv = m_vv = 0; m_held = 0; m_alive = true;
					if (journalOn()) trace(ETraceLevel.Journal, $"AV-05 {day:yyyy-MM-dd} gap {s.Gap:F2} (0.75 x atr20d {0.75 * atr:F2}) armed {(m_dir > 0 ? "long" : "short")}");
				}
			}
			if (!m_alive || !IsRth()) return;
			double tp = (Bars.High[0] + Bars.Low[0] + Bars.Close[0]) / 3.0;
			m_pv += tp * Math.Max(1, Bars.Volume[0]); m_vv += Math.Max(1, Bars.Volume[0]);
			if (m_dir > 0 && Bars.Low[0] < Session.priorRthHigh && m_held < 2) { m_alive = false; return; }
			if (m_dir < 0 && Bars.High[0] > Session.priorRthLow && m_held < 2) { m_alive = false; return; }
			if (StrategyInfo.MarketPosition != 0 || m_done || m_held < 2 || m_held > 10) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			if (((int)tod.TotalMinutes) % 60 != 0) return;
			double av = m_pv / m_vv, atrDay = DailyContext.Atr20d;
			double lo = Bars.Low[0], hi = Bars.High[0];
			for (int i = 1; i < 60 && i <= Bars.CurrentBar; i++) { lo = Math.Min(lo, lbBar(i).l); hi = Math.Max(hi, lbBar(i).h); }
			bool buy = m_dir > 0 && lo <= av + 0.5 * atrDay && lo >= av - 0.5 * atrDay;
			bool sell = m_dir < 0 && hi >= av - 0.5 * atrDay && hi <= av + 0.5 * atrDay;
			if (!buy && !sell) return;
			double entry = Bars.Close[0];
			double stop = widen(buy, entry, m_fill);
			if (!budget(entry, stop)) { m_done = true; return; }
			arm(buy, entry, stop, entry + m_dir * atrDay, buy ? "AV05LE" : "AV05SE", true);
			if (buy) m_le.Send(); else m_se.Send();
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
		private void arm(bool buy, double entry, double stop, double target, string name, bool hold)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = hold ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.MaxHoldDays = hold ? 10 : 0; GxConfig.TimeStopMinutes = 0;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
		}

		private bool hostGate(bool buy)
		{
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
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; }   // trading day never blocks today (Rest returned Done forever)
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
			}
			else
			{
			}
			return true;
		}

	}
}