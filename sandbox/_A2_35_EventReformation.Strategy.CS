// ## 1. Identity
// Catalogue ID: A2-35
// ID mapping: A2-35 -> _A2_35_EventReformation.Strategy.CS (class _A2_35_EventReformation).
// Name: A2-35 macro-event re-formation value area
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_35_EventReformation
// File: sandbox/_A2_35_EventReformation.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-35.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default; it counts 5-minute closes (the 1m close on each 5-minute boundary after T+20) and enters at the next 5-minute open. Its stop, point target and flat time go through GX. Size is 2 contracts (catalogue "T1 50% at 0.5x" and coding-plan decision 10): GX takes T1, 1 lot, at 0.5 x the VA height beyond the edge and the other lot at 1.0 x. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-35 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: two contracts (T1 lot and the other lot).
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades. Acceptance arm: sent on the 1m bar that closes a 5-minute bar, so it fills at the next 5-minute open.
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
// Size: 2 contracts where this file trades (catalogue T1 50 percent scale; coding-plan decision 10); the 1 percent budget is on both. journalArm is written by exitPlan when GX is used.
// Risk limit: skip when 1 contract times the stop distance times BigPointValue exceeds 1 percent of equity.
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
// Other: ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default; it counts 5-minute closes (the 1m close on each 5-minute boundary after T+20) and enters at the next 5-minute open. Its stop, point target and flat time go through GX. Size is 2 contracts (catalogue "T1 50% at 0.5x" and coding-plan decision 10): GX takes T1, 1 lot, at 0.5 x the VA height beyond the edge and the other lot at 1.0 x. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
//
// ## 17. Parameters
// Each [Input] below is one of this strategy's own parameters. TraceLevel stays on the base.
// Input: see the [Input] properties on the class
// Purpose: the catalogue sweep named in section 22
// Default: the value set in the constructor
// Units: as the property name states
// Range: the enum members, checked with Enum.IsDefined in StartCalc
// Effect: selects the arm. It does not resize a position.
// Interactions: GX settings stay on GxConfig, not on a new production input
// Fixed or research-tuned: research-tuned where the catalogue names a sweep
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
// logSignal tag: A2-35
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default; it counts 5-minute closes (the 1m close on each 5-minute boundary after T+20) and enters at the next 5-minute open. Its stop, point target and flat time go through GX. Size is 2 contracts (catalogue "T1 50% at 0.5x" and coding-plan decision 10): GX takes T1, 1 lot, at 0.5 x the VA height beyond the edge and the other lot at 1.0 x. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_35_EventReformation : _VolumeProfileFuturesBase
	{
		public enum EArm { Acceptance, Failed }
		[Input] public EArm Arm { get; set; }
		private IOrderMarket m_le, m_se;
		private IOrderMarket m_lxFlat, m_sxFlat;   // Analyst MC17: closes every contract still open at flat-by or from an earlier day
		private DateTime m_entryDay = DateTime.MinValue, m_openSkipDay = DateTime.MinValue;
		private DateTime m_day = DateTime.MinValue, m_event = DateTime.MinValue;
		private bool m_done, m_built, m_out;
		private int m_outCount, m_back;
		private double m_vah, m_val, m_poc;

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_35_EventReformation(object _ctx) : base(_ctx) { Arm = EArm.Acceptance;  Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }

		protected override void Create()
		{
			base.Create();
			RangeProfiles.enabled = true;   // RangeProfileAfter returns null while RangeProfiles is off
			RollDetection = ERollDetection.Series;
			EventConfig.enabled = true;
			GxOrderPrefix = "A235"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235SE", EOrderAction.SellShort));
			m_lxFlat = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235FLX", EOrderAction.Sell));
			m_sxFlat = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235FSX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EArm), Arm)) throw new ArgumentException("_A2_35_EventReformation: Arm is not a defined EArm");
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
		}


		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			DateTime et = ToEastern(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = m_built = m_out = false; m_outCount = m_back = 0; m_event = findEvent(day); }
			int posNow = StrategyInfo.MarketPosition;
			if (posNow != 0 && (m_entryDay != day || _SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)))
			{   // Analyst MC17: close any remaining contract at flat-by or the next day (Coder 2026-11-22: the GX plan now covers both contracts; this stays as a backstop)
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-35 {day:yyyy-MM-dd} flat: closing {Math.Abs(posNow)} remaining contract(s) from {m_entryDay:yyyy-MM-dd}");
				if (posNow > 0) m_lxFlat.Send(posNow); else m_sxFlat.Send(-posNow);
			}
			if (posNow != 0 && m_entryDay != day && m_openSkipDay != day)
			{ m_openSkipDay = day; if (journalOn()) trace(ETraceLevel.Journal, $"A2-35 {day:yyyy-MM-dd} entry skipped: position still open ({posNow} from {m_entryDay:yyyy-MM-dd})"); }
			if (m_done || StrategyInfo.MarketPosition != 0 || m_event == DateTime.MinValue) return;
			if (!m_built)
			{
				if (et < m_event.AddMinutes(20)) return;
				DateTime evBar = SessionClock.FromEastern(m_event);   // m_event is ET wall clock; the bar ring is in chart time
				VpExtra prof = RangeProfileAfter(1, evBar.AddMinutes(1), evBar.AddMinutes(20));
				if (prof == null || !(prof.vah > prof.val)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-35 {day:yyyy-MM-dd} skip: no event profile T+1..T+20"); m_done = true; return; }
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-35 {day:yyyy-MM-dd} event {m_event:HH:mm} profile VAL {prof.val:F2} POC {prof.poc:F2} VAH {prof.vah:F2}");
				m_vah = prof.vah; m_val = prof.val; m_poc = prof.poc; m_built = true;
			}
			if (_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)) return;   // Analyst MC17: no entry after that day's flat-by cutoff (Good Friday 2023 ended 09:15 ET)
			if (Arm == EArm.Acceptance) accept(et); else fail(et);
		}

		private void accept(DateTime et)
		{   // Coder 2026-11-22 (catalogue Rev 6: "2 consecutive 5m closes outside the re-formation VA -> enter at the next 5m open"):
			// the 5m close is the 1m close stamped on a 5-minute boundary (bars are end-stamped), counted only after T+20; the market
			// order sent on that bar fills on the next 1m open, which is the next 5m bar's open.
			if (et.Minute % 5 != 0 || et <= m_event.AddMinutes(20)) return;
			bool up = Bars.Close[0] > m_vah;
			bool dn = Bars.Close[0] < m_val;
			m_outCount = (up || dn) ? m_outCount + 1 : 0;
			if (m_outCount < 2) return;
			bool longSide = Bars.Close[0] > m_vah;
			double entry = Bars.Close[0];
			double height = m_vah - m_val;
			double tick = tickSize();
			double stop = longSide ? m_vah - 0.3 * height : m_val + 0.3 * height;
			double floor = 14 * tick;
			if ((longSide ? entry - stop : stop - entry) < floor) stop = entry - (longSide ? floor : -floor);
			double target = longSide ? m_vah + height : m_val - height;
			int flat = m_event.Hour < 12 ? _SandboxRules.EtToExchangeHhmm(1130, Bars.Info.Exchange) : _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			if (!ok(entry, stop, target, longSide)) { m_done = true; return; }
			plan(longSide, entry, stop, longSide ? m_vah + 0.5 * height : m_val - 0.5 * height, target, flat);   // Coder 2026-11-22 (catalogue: "T1 50% at 0.5x"): T1 1 lot at 0.5 x VA height beyond the edge
			m_entryDay = m_day; if (longSide) { if (hostGate(true)) m_le.Send(2); } else { if (hostGate(false)) m_se.Send(2); }
		}

		private void fail(DateTime et)
		{
			bool outside = Bars.Close[0] > m_vah || Bars.Close[0] < m_val;
			if (outside && !m_out) { m_out = true; m_back = 0; return; }
			if (!m_out) return;
			m_back++;
			bool back = Bars.Close[0] <= m_vah && Bars.Close[0] >= m_val;
			if (!back || m_back > 2) { if (!back && m_back > 2) m_done = true; return; }
			bool longSide = Bars.Close[0] >= m_poc;
			double entry = Bars.Close[0];
			double tick = tickSize();
			double stop = longSide ? Bars.Low[0] - tick : Bars.High[0] + tick;
			if (!ok(entry, stop, m_poc, longSide)) { m_done = true; return; }
			plan(longSide, entry, stop, m_poc, m_poc, m_event.Hour < 12 ? _SandboxRules.EtToExchangeHhmm(1130, Bars.Info.Exchange) : _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			m_entryDay = m_day; if (longSide) { if (hostGate(true)) m_le.Send(2); } else { if (hostGate(false)) m_se.Send(2); }
		}

		private bool ok(double entry, double stop, double target, bool longSide)
		{
			double risk = Math.Abs(entry - stop);
			double room = longSide ? target - entry : entry - target;
			if (!(risk > 0) || !(room > 0)) { if (journalOn()) trace(ETraceLevel.Journal, "A2-35 skip: no room to the target"); return false; }
			if (journalOn()) trace(ETraceLevel.Warning, "A2-35 multi-contract size 2");   // entries are sent with Send(2)
			if (2 * risk * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Error, "A2-35 skipped: multi-contract risk exceeds 1 percent"); return false; }
			return true;
		}

		private void plan(bool longSide, double entry, double stop, double t1Price, double target, int flat)
		{   // Coder 2026-11-22: the 2-contract entry (catalogue scale, coding-plan decision 10) is planned as 2 lots: T1 (50 percent) at t1Price
			// when it lies before the target, the other lot at the target. It was planned as 1 lot against Send(2).
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			double t1 = longSide ? t1Price - entry : entry - t1Price, fin = Math.Abs(target - entry);
			bool scale = t1 > 0 && t1 < fin;
			GxConfig.TargetMode = scale ? ETargetMode.ScaleOutThenRunner : ETargetMode.SingleTarget;
			GxConfig.T1Unit = ETriggerUnit.Points; GxConfig.T1Value = scale ? t1 : 0; GxConfig.T1ScalePct = 50;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = flat;
			GxConfig.TimeStopMinutes = 90;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 2, mkt, longSide ? "A235LE" : "A235SE");
		}

		private DateTime findEvent(DateTime day)
		{
			if (EventCalendar == null) return DateTime.MinValue;
			foreach (EventRec r in EventCalendar.EventsOn(day))
			{
				if (r.kind == EEventKind.Cpi || r.kind == EEventKind.Nfp || r.kind == EEventKind.FomcDecision) return r.etTime;
			}
			return DateTime.MinValue;
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
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
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