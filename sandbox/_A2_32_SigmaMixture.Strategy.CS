// ## 1. Identity
// Catalogue ID: A2-32
// ID mapping: A2-32 -> _A2_32_SigmaMixture.Strategy.CS (class _A2_32_SigmaMixture).
// Name: A2-32 sigma-space mixture break
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_32_SigmaMixture
// File: sandbox/_A2_32_SigmaMixture.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-32.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. Two contracts (owner decision 2026-11-22 14:03 ET, "Make A2-32 2 contracts"): T1, lot 1, at the next centre; lot 2 runs to the centre beyond it, with the stop at entry after T1. A 50 percent scale at 0.5 of the way is not used.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-32 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: two contracts (owner decision 2026-11-22 14:03 ET, coding-plan decision 10): T1 (lot 1) at the next component centre, lot 2 a runner to the centre beyond, stop to entry after T1.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades.
// Can fill: the next bar. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one.
// Re-entry: one attempt per session where the file trades.
// Same session after a stop: no new entry that session.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: ES or NQ as the catalogue says. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute.
// Data1: 1-minute unless section 22 says daily. Tests use data through 2024-12-31 only.
// Data2: named in section 22, or Not applicable.
// Further series: Not applicable
// Warmup: the statistic named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup, and the 1 percent budget when an order is sent.
// Skip when: the statistic is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 2 contracts where this file trades (owner decision 2026-11-22: "Make A2-32 2 contracts"). journalArm is written by exitPlan when GX is used.
// Risk limit: skip when 2 contracts times the stop distance times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create, when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar, or sendExits, as section 13 says.
//
// ## 13. Exits
// Exit orders: GX exitPlan on the entry bar and exitBar on each later closed bar. sendExits is not used.
// Hard stop: the catalogue stop when this file trades.
// Target: the catalogue target when GX or sendExits names one.
// Time exit: the catalogue flat time.
// Invalidation: the catalogue skip.
//
// ## 14. Regimes
// Suitable: the catalogue window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series where this file trades.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. Two contracts (owner decision 2026-11-22 14:03 ET, "Make A2-32 2 contracts"): T1, lot 1, at the next centre; lot 2 runs to the centre beyond it, with the stop at entry after T1. A 50 percent scale at 0.5 of the way is not used.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name, when an entry is sent.
// logSignal tag: A2-32
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. Two contracts (owner decision 2026-11-22 14:03 ET, "Make A2-32 2 contracts"): T1, lot 1, at the next centre; lot 2 runs to the centre beyond it, with the stop at entry after T1. A 50 percent scale at 0.5 of the way is not used.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_32_SigmaMixture : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se;
		private IOrderMarket m_lxFlat, m_sxFlat;   // Analyst MC17: closes every contract still open at flat-by or from an earlier day
		private DateTime m_entryDay = DateTime.MinValue, m_openSkipDay = DateTime.MinValue;
		private _GmmProfileTracker m_gmm;
		private DateTime m_day = DateTime.MinValue;
		private bool m_fed, m_done;
		private int m_up, m_dn;
		private double m_upLvl = double.NaN, m_dnLvl = double.NaN;   // the nearest centre above / below, frozen while a break run is counting

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_32_SigmaMixture(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			ProfileStats.enabled = true;
			GxOrderPrefix = "A232"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A232LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A232SE", EOrderAction.SellShort));
			m_lxFlat = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A232FLX", EOrderAction.Sell));
			m_sxFlat = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A232FSX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_gmm = new _GmmProfileTracker(tickSize()); }


		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_fed = m_done = false; m_filledSetup = false; m_restOn = false; m_up = m_dn = 0; m_upLvl = m_dnLvl = double.NaN; }
			int posNow = StrategyInfo.MarketPosition;
			if (posNow != 0 && (m_entryDay != day || _SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)))
			{   // Analyst MC17: close any remaining contract at flat-by or the next day (Coder 2026-11-22: the GX plan now covers both contracts; this stays as a backstop)
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-32 {day:yyyy-MM-dd} flat: closing {Math.Abs(posNow)} remaining contract(s) from {m_entryDay:yyyy-MM-dd}");
				if (posNow > 0) m_lxFlat.Send(posNow); else m_sxFlat.Send(-posNow);
			}
			if (posNow != 0 && m_entryDay != day && m_openSkipDay != day)
			{ m_openSkipDay = day; if (journalOn()) trace(ETraceLevel.Journal, $"A2-32 {day:yyyy-MM-dd} entry skipped: position still open ({posNow} from {m_entryDay:yyyy-MM-dd})"); }
			SessionStats s = Session;
			if (s != null && s.rthComplete && !m_fed)
			{
				m_fed = true;
				List<RingBar> bars = SessionBars(day, true);
				if (bars != null) m_gmm.OnSessionClose(day, bars, rollOn(1));
				m_gmm.Levels();
			}
			if (StrategyInfo.MarketPosition != 0 || m_done || !IsRth()) return;
			if (ToEastern(Bars.Time[0]).TimeOfDay < new TimeSpan(10, 0, 0)) return;
			if (ToEastern(Bars.Time[0]).Minute % 15 != 0) return;   // Coder 2026-11-22 (catalogue: "2 consecutive 15m closes ... enter at the next 15m open"): only the 1m close that ends a 15-minute bar counts
			SigmaMixtureResult sig = m_gmm.LastSigma;
			if (sig == null || !sig.ok || sig.levels == null || sig.levels.Length < 2) return;
			double c = Bars.Close[0];
			double above = double.NaN, below = double.NaN;
			for (int i = 0; i < sig.levels.Length; i++)
			{
				double lv = sig.levels[i];
				if (lv > c && (double.IsNaN(above) || lv < above)) above = lv;
				if (lv < c && (double.IsNaN(below) || lv > below)) below = lv;
			}
			// A break is two closes beyond the centre that was nearest before the run began (the prior bar's nearest level).
			// Comparing with the nearest level to this close can never be true, because that level is beyond the close.
			m_up = !double.IsNaN(m_upLvl) && c > m_upLvl ? m_up + 1 : 0;
			m_dn = !double.IsNaN(m_dnLvl) && c < m_dnLvl ? m_dn + 1 : 0;
			if (m_up == 0) m_upLvl = above;
			if (m_dn == 0) m_dnLvl = below;
			if (m_up < 2 && m_dn < 2) return;
			if (journalOn()) trace(ETraceLevel.Journal, $"A2-32 {day:yyyy-MM-dd} break {(m_up >= 2 ? "above " + m_upLvl.ToString("F2") : "below " + m_dnLvl.ToString("F2"))} close {c:F2}");
			bool longSide = m_up >= 2;
			double entry = c;
			double tick = tickSize();
			double stop = longSide ? below : above;
			if (double.IsNaN(stop)) { m_done = true; return; }
			stop = longSide ? stop - tick : stop + tick;
			double floor = 14 * tick;
			if ((longSide ? entry - stop : stop - entry) < floor) stop = entry - (longSide ? floor : -floor);
			double target = longSide ? above : below;
			double risk = Math.Abs(entry - stop);
			if (!(risk > 0) || double.IsNaN(target) || (longSide ? target - entry : entry - target) < 1.2 * risk)
			{ if (journalOn()) trace(ETraceLevel.Journal, $"A2-32 {day:yyyy-MM-dd} skip: next centre under 1.2R or missing"); m_done = true; return; }
			// Owner decision 2026-11-22 14:03 ET ("Make A2-32 2 contracts", coding-plan decision 10): 2 contracts, budget on both.
			if (journalOn()) trace(ETraceLevel.Warning, "A2-32 multi-contract size 2");
			if (2 * risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ if (journalOn()) trace(ETraceLevel.Error, "A2-32 skipped: multi-contract risk exceeds 1 percent"); m_done = true; return; }
			double runner = double.NaN;   // the next component centre beyond the target, for the second lot
			for (int i = 0; i < sig.levels.Length; i++)
			{
				double lv = sig.levels[i];
				if (longSide ? lv > target && (double.IsNaN(runner) || lv < runner) : lv < target && (double.IsNaN(runner) || lv > runner)) runner = lv;
			}
			arm(longSide, entry, stop, target, runner);
			m_entryDay = m_day; if (longSide) { if (hostGate(true)) m_le.Send(2); } else { if (hostGate(false)) m_se.Send(2); }
		}

		private void arm(bool longSide, double entry, double stop, double target, double runner)
		{   // Owner decision 2026-11-22 (2 contracts): 2 lots, the same pattern as A2-28 / A2-45. T1 (50 percent, lot 1) is the catalogue's
			// single target (the next component centre); lot 2 runs to the next centre beyond it when there is one (otherwise it has no
			// price target), with the stop moved to entry after T1 and the 15:55 flat-by.
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			double t1 = Math.Abs(target - entry), fin = double.IsNaN(runner) ? 0 : Math.Abs(runner - entry);
			GxConfig.TargetMode = ETargetMode.ScaleOutThenRunner;
			GxConfig.T1Unit = ETriggerUnit.Points; GxConfig.T1Value = t1; GxConfig.T1ScalePct = 50;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = fin > t1 ? fin : 0;
			GxConfig.BreakEven = EBreakEven.AfterT1;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.TimeStopMinutes = 0;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 2, mkt, longSide ? "A232LE" : "A232SE");   // the plan covers both contracts sent with Send(2)
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
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
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