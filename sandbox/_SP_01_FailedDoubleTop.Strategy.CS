// ## 1. Identity
// Catalogue ID: SP-01
// ID mapping: SP-01 -> _SP_01_FailedDoubleTop.Strategy.CS (class _SP_01_FailedDoubleTop).
// Name: SP-01 failed double top
// Header version: 1
// Rules version: catalogue specification under the SP-01 heading, rewritten 2026-11-15.
// Study class: _SP_01_FailedDoubleTop
// File: sandbox/_SP_01_FailedDoubleTop.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the SP-01 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: a 15-minute swing high is a 15-minute high above the two 15-minute highs on each side. Two such highs within 0.25 of their range, at least six 15-minute bars apart, with a pullback of at least that range, then a 15-minute close above both, buys. The developing POC must be at least the midpoint. The stop is the breakout bar's low. The target is one pattern height.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the SP-01 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute. Data2 = 15-minute.
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
// Exit orders: GX. Stop under the breakout bar, widened to the cost floor. Target is one pattern height. Flat at the session value end.
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
// Other: ASSUMPTION: ASSUMPTION: a 15-minute swing high is a 15-minute high above the two 15-minute highs on each side. Two such highs within 0.25 of their range, at least six 15-minute bars apart, with a pullback of at least that range, then a 15-minute close above both, buys. The developing POC must be at least the midpoint. The stop is the breakout bar's low. The target is one pattern height.
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
// logSignal tag: SP-01
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: a 15-minute swing high is a 15-minute high above the two 15-minute highs on each side. Two such highs within 0.25 of their range, at least six 15-minute bars apart, with a pullback of at least that range, then a 15-minute close above both, buys. The developing POC must be at least the midpoint. The stop is the breakout bar's low. The target is one pattern height.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _SP_01_FailedDoubleTop : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le; private bool m_done; private DateTime m_day = DateTime.MinValue, m_open = DateTime.MinValue;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _SP_01_FailedDoubleTop(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create() { base.Create(); RangeProfiles.enabled = true; RollDetection = ERollDetection.Series; GxOrderPrefix = "SP01"; createGxOrders(); m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SP01LE", EOrderAction.Buy)); }
		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			base.StartCalc(); requireRing(1, 2000, "SP-01 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */
		}

		/// <summary>Analyst MC17 (A2-35 held over Good Friday 2023): GX resolves one flat-by clock per run, so on an early close,
		/// holiday halt or abbreviated session the open position is closed here at that day's session end minus 5 minutes.</summary>
		private void sandboxEarlyFlat()
		{
			int pos = StrategyInfo.MarketPosition;
			if (pos == 0 || GxConfig == null || GxConfig.SessionExit != ESessionExit.FlatBy) return;
			string why;
			if (!_SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd, out why)) return;
			if (journalOn()) trace(ETraceLevel.Journal, "SP-01 " + GlobexDate(Bars.Time[0]).ToString("yyyy-MM-dd") + " flat-by early close: " + why + ", closing " + Math.Abs(pos));
			gxNoteExit("FLATBY");
			gxSendExit("FLATBY", false, new ExitPosition(), pos > 0, Math.Abs(pos));
		}
		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar(); sandboxEarlyFlat();
			if (sandboxBook()) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_open = DateTime.MinValue; }
			if (IsRth() && m_open == DateTime.MinValue) m_open = Bars.Time[0];
			if (StrategyInfo.MarketPosition != 0 || m_done || !IsRth() || Bars.CurrentBar < 200 || m_open == DateTime.MinValue) return;
			TimeSpan tod = sessionClock(Bars.Time[0]); if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			if (((int)tod.TotalMinutes) % 15 != 0) return;
			if (!swingHigh(2) || !swingHigh(8)) return;
			double h1 = blockHigh(2), h2 = blockHigh(8), atr = blockHigh(0) - blockLow(0);
			if (!(atr > 0) || Math.Abs(h1 - h2) > 0.25 * atr) return;
			double pull = Math.Max(h1, h2) - blockLow(4);
			if (pull < atr || !(Bars.Close[0] > Math.Max(h1, h2))) return;
			VpExtra dev = RangeProfileAfter(1, m_open, Bars.Time[0]);
			if (dev == null || dev.poc < (h1 + h2) / 2.0) return;
			double entry = Bars.Close[0];
			double stop = widen(true, entry, blockLow(0));
			double target = entry + Math.Abs(h1 - h2) + pull;
			if (!budget(entry, stop)) { m_done = true; return; }
			arm(true, entry, stop, target, "SP01LE", 0); m_le.Send();
		}
		private double blockHigh(int ago) { double h = double.MinValue; for (int i = ago * 15; i < ago * 15 + 15; i++) h = Math.Max(h, lbBar(i).h); return h; }
		private double blockLow(int ago) { double l = double.MaxValue; for (int i = ago * 15; i < ago * 15 + 15; i++) l = Math.Min(l, lbBar(i).l); return l; }
		private bool swingHigh(int ago) { return blockHigh(ago) >= blockHigh(ago - 1) && blockHigh(ago) >= blockHigh(ago + 1); }

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
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
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