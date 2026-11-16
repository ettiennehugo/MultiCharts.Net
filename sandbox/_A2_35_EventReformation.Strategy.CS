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
// Research assumption: ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default. Its stop, point target and flat time go through GX. The 50 percent scale is not sent because size is 1. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-35 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: one contract.
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
// Size: 1 contract where this file trades. journalArm is written by exitPlan when GX is used.
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
// Other: ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default. Its stop, point target and flat time go through GX. The 50 percent scale is not sent because size is 1. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
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
// ASSUMPTION: CPI, NFP and FOMC decision times come from EventCalendar.EventsOn. The T+1 to T+20 profile is RangeProfileAfter. The acceptance arm is the default. Its stop, point target and flat time go through GX. The 50 percent scale is not sent because size is 1. The failed-break arm is the other enum value and also uses GX, with the new POC as the point target. Nothing is sent before T+20.
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
		private DateTime m_day = DateTime.MinValue, m_event = DateTime.MinValue;
		private bool m_done, m_built, m_out;
		private int m_outCount, m_back;
		private double m_vah, m_val, m_poc;

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_35_EventReformation(object _ctx) : base(_ctx) { Arm = EArm.Acceptance;  Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			EventConfig.enabled = true;
			GxOrderPrefix = "A235"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A235SE", EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EArm), Arm)) throw new ArgumentException("_A2_35_EventReformation: Arm is not a defined EArm");
			base.StartCalc();
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
			if (m_done || StrategyInfo.MarketPosition != 0 || m_event == DateTime.MinValue) return;
			if (!m_built)
			{
				if (et < m_event.AddMinutes(20)) return;
				VpExtra prof = RangeProfileAfter(1, m_event.AddMinutes(1), m_event.AddMinutes(20));
				if (prof == null || !(prof.vah > prof.val)) { m_done = true; return; }
				m_vah = prof.vah; m_val = prof.val; m_poc = prof.poc; m_built = true;
			}
			if (Arm == EArm.Acceptance) accept(et); else fail(et);
		}

		private void accept(DateTime et)
		{
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
			plan(longSide, entry, stop, target, flat);
			if (longSide) m_le.Send(2); else m_se.Send(2);
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
			plan(longSide, entry, stop, m_poc, m_event.Hour < 12 ? _SandboxRules.EtToExchangeHhmm(1130, Bars.Info.Exchange) : _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			if (longSide) m_le.Send(2); else m_se.Send(2);
		}

		private bool ok(double entry, double stop, double target, bool longSide)
		{
			double risk = Math.Abs(entry - stop);
			double room = longSide ? target - entry : entry - target;
			return risk > 0 && room > 0 && risk * Bars.Info.BigPointValue <= equityAvailable(0.01);
		}

		private void plan(bool longSide, double entry, double stop, double target, int flat)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
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
			exitPlan(longSide, entry, stop, 1, mkt, longSide ? "A235LE" : "A235SE");
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

		private bool hostGate(bool buy)
		{
			if (Gate == ESandboxGate.Off) return true;
			VpExtra prior = null;
			try { prior = PriorRthProfile(); } catch (Exception) { }
			double poc = prior != null ? prior.poc : double.NaN;
			double hi = Session != null ? Session.rthHigh : Bars.High[0];
			double lo = Session != null ? Session.rthLow : Bars.Low[0];
			double atr = averageTrueRange(Bars, 14);
			double sma = double.NaN;
			try { sma = DailyContext.Sma200; } catch (Exception) { }
			double prev = Session != null ? Session.priorRthClose : double.NaN;
			int fam = 0, ranges = 0;
			if (Gate == ESandboxGate.ThreeFamily) dvpFamiliesAt(1, Bars.Close[0], 0.3, out fam, out ranges);
			bool ok = _SandboxRules.GateAllows(Gate, buy, Bars.Close[0], poc, hi, lo, atr, sma, prev, fam);
			if (!ok && journalOn()) trace(ETraceLevel.Journal, "gate " + Gate + " blocked");
			return ok;
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


		private bool sandboxBook()
		{
			bool window = !_SandboxRules.PastFlat(sessionClock(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
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