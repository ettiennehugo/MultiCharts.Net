// ## 1. Identity
// Catalogue ID: PS-25
// ID mapping: PS-25 -> _PS_25_AcceptBeyondRail.Strategy.CS (class _PS_25_AcceptBeyondRail).
// Name: PS-25 accept beyond the VAH rail
// Header version: 1
// Rules version: catalogue specification under the PS-25 heading, rewritten 2026-11-15.
// Study class: _PS_25_AcceptBeyondRail
// File: sandbox/_PS_25_AcceptBeyondRail.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the PS-25 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the rail is dvpRailPrice of DvpVah. DvpConfig is enabled. Two closes beyond the rail by 0.10 times the 14-bar ATR buy a stop one tick above the second bar. The stop is the lower of that bar's low and the rail minus 0.5 ATR. The target is the +2 sigma rail from dvpRailPrice. TimeStopBars is 41. A close back through the rail is not a separate exit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the PS-25 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute.
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
// Exit orders: GX. Stop and the +2 sigma target. TimeStopBars is 41. Flat at the session value end.
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
// Other: ASSUMPTION: ASSUMPTION: the rail is dvpRailPrice of DvpVah. DvpConfig is enabled. Two closes beyond the rail by 0.10 times the 14-bar ATR buy a stop one tick above the second bar. The stop is the lower of that bar's low and the rail minus 0.5 ATR. The target is the +2 sigma rail from dvpRailPrice. TimeStopBars is 41. A close back through the rail is not a separate exit.
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
// logSignal tag: PS-25
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: the rail is dvpRailPrice of DvpVah. DvpConfig is enabled. Two closes beyond the rail by 0.10 times the 14-bar ATR buy a stop one tick above the second bar. The stop is the lower of that bar's low and the rail minus 0.5 ATR. The target is the +2 sigma rail from dvpRailPrice. TimeStopBars is 41. A close back through the rail is not a separate exit.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_25_AcceptBeyondRail : _VolumeProfileFuturesBase
	{
		private IOrderPriced m_le; private int m_hold; private bool m_done; private DateTime m_day = DateTime.MinValue;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _PS_25_AcceptBeyondRail(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Series; GxOrderPrefix = "PS25"; createGxOrders(); m_le = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS25LE", EOrderAction.Buy)); }
		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 m_sigTime = DateTime.MinValue; m_hold = 0; DvpConfig.enabled = true; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); }

		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar();
			htfStep();   // Coder 2026-11-22: MT-07's HTF acceptance count, after the engines have stepped this bar
			DateTime day = GlobexDate(Bars.Time[0]); if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_hold = 0; }
			if (sandboxBook()) return;   // before any position return, so Rest() sees the fill: one attempt per anchor per day
			if (StrategyInfo.MarketPosition != 0 || m_done || !IsRth() || rollOn(1)) return;
			// Coder 2026-11-22 (Analyst MT-07 retest: 22 of 36 trades exit on the +2 sigma target, 13 on the bar after entry): the rails,
			// the ATR, the acceptance closes and the target come from the signal stream ds (signalDs). PS-25 keeps Data1; MT-07 passes the
			// 15-minute Data2 stream (catalogue MT-07 "15m PS-25 entries"), read through closed bars only, once per new closed bar.
			int ds = signalDs();
			if (ds > 1 && !newSignalBar(ds)) return;
			double h0, l0, c0, h1, l1, c1; sigBar(ds, 0, out h0, out l0, out c0); sigBar(ds, 1, out h1, out l1, out c1);
			double vah = dvpRailPrice(ds, ELevelKind.DvpVah, 0);
			double up2 = dvpRailPrice(ds, ELevelKind.DvpSigma, 2);
			double atr = ds == 1 ? averageTrueRange(Bars, 14) : sigAtr(ds, 14);
			if (double.IsNaN(vah) || double.IsNaN(up2) || !(atr > 0)) return;
			if (c0 > vah + 0.10 * atr) m_hold++; else m_hold = 0;
			if (m_hold < 2 || !htfAccepted()) return;
			double entry = Math.Max(h0, h1) + tickSize();
			double stop = widen(true, entry, Math.Min(Math.Min(l0, l1), vah - 0.5 * atr));
			string why = null;
			if (!(up2 > entry)) why = "+2 sigma rail " + up2.ToString("0.00") + " not above the entry " + entry.ToString("0.00");
			else if (!budget(entry, stop)) why = "risk " + (entry - stop).ToString("0.00") + " pts over the 1 percent budget";
			else why = targetBlock(up2);
			if (why != null)
			{   // Coder 2026-11-22 (Analyst #152 retest, MT-07 had no 2024 trades): the skip reason is journalled (once per day: m_done)
				if (journalOn()) trace(ETraceLevel.Journal, GxOrderPrefix + " skip: " + why);
				m_done = true; return;
			}
			// The catalogue's 41-bar time stop counts signal-stream bars: 41 Data1 bars for PS-25, 41 x the Data2 bar minutes for MT-07.
			int sigMin = ds == 1 ? 1 : barMinutes(ds);
			GxConfig.TimeStopBars = ds == 1 ? 41 : 0; m_timeStop = ds == 1 ? 0 : 41 * sigMin;
			m_sigLo = l0; m_sigHi = h0;
			if (journalOn())
			{
				double s1 = dvpRailPrice(ds, ELevelKind.DvpSigma, 1);
				trace(ETraceLevel.Journal, GxOrderPrefix + " plan Data" + ds + " (" + sigMin + "m) entry " + entry.ToString("0.00") + " stop " + stop.ToString("0.00") + " vah " + vah.ToString("0.00")
					+ " target +2 sigma " + up2.ToString("0.00") + " (" + (up2 - entry).ToString("0.00") + " pts, sigma " + (up2 - s1).ToString("0.00") + ", atr " + atr.ToString("0.00") + ")"
					+ " time stop 41 bars = " + (41 * sigMin) + " min");   // Coder 2026-11-22 (Analyst follow-up): the clock GX was given
			}
			honorExit(entry, 0); m_restPx = entry; m_restBuy = true; m_restOn = true; m_restQty = _SandboxRules.ScaleQty(ExitMode, false); arm(true, entry, stop, up2, "PS25LE", 0); { if (hostGate(true)) m_le.Send(entry, m_restQty); }
		}

		/// <summary>The stream the PS-25 rule reads: 1 = Data1 (PS-25). A derived file returns its native-timeframe stream (MT-07: Data2 15m).</summary>
		protected virtual int signalDs() { return 1; }
		/// <summary>Null when the target may be used; otherwise the skip reason (MT-07: the target rail must lie inside the HTF next-rail distance).</summary>
		protected virtual string targetBlock(double target) { return null; }
		private double m_sigLo, m_sigHi;
		private DateTime m_sigTime = DateTime.MinValue;
		private bool sigReady(int ds, int ago) { return IsExist(ds) && BarsOfData(ds).CurrentBar > ago + 2; }
		private bool newSignalBar(int ds)
		{
			if (!IsExist(ds)) { if (m_sigTime != DateTime.MaxValue) { m_sigTime = DateTime.MaxValue; if (journalOn()) trace(ETraceLevel.Error, "Data" + ds + " missing"); } return false; }
			if (!sigReady(ds, 15)) return false;
			DateTime t; double o, h, l, c, v; HtfClosedBar(ds, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_sigTime) return false;
			m_sigTime = t; return true;
		}
		private void sigBar(int ds, int ago, out double h, out double l, out double c)
		{
			if (ds == 1) { int k = ago > 0 ? 1 : 0; h = k == 0 ? Bars.High[0] : Bars.High[1]; l = k == 0 ? Bars.Low[0] : Bars.Low[1]; c = k == 0 ? Bars.Close[0] : Bars.Close[1]; return; }   // Data1: bars 0 and 1 only
			DateTime t; double o, v; HtfClosedBar(ds, ago, out t, out o, out h, out l, out c, out v);
		}
		/// <summary>Mean true range of the last n closed bars of stream ds.</summary>
		private double sigAtr(int ds, int n)
		{
			if (!sigReady(ds, n)) return double.NaN;
			double sum = 0, h, l, c, ph, pl, pc;
			for (int k = 0; k < n; k++) { sigBar(ds, k, out h, out l, out c); sigBar(ds, k + 1, out ph, out pl, out pc); sum += Math.Max(h, pc) - Math.Min(l, pc); }
			return sum / n;
		}
		private int barMinutes(int ds)
		{
			try { Resolution r = BarsOfData(ds).Info.Resolution; if (r.Type == EResolution.Minute && r.Size > 0) return (int)r.Size; } catch (Exception) { }
			return 1;
		}

		protected virtual bool htfAccepted() { return true; }
		protected virtual void htfStep() { }
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
		private bool budget(double a, double b) { double r = Math.Abs(a - b); return r > 0 && r * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private void arm(bool buy, double entry, double stop, double target, string name, int holdDays)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = holdDays > 0 ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.MaxHoldDays = holdDays;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(m_flatOverride, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.TimeStopMinutes = m_timeStop; GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = m_sigLo; mkt.SignalHigh = m_sigHi;   // the signal-stream bar (Data1 for PS-25)
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
				if (Gate == ESandboxGate.ThreeFamily && DvpConfig.enabled) dvpFamiliesAt(signalDs(), Bars.Close[0], 0.3, out fam, out ranges);
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
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
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