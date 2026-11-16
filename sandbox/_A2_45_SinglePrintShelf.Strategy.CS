// ## 1. Identity
// Catalogue ID: A2-45
// ID mapping: A2-45 -> _A2_45_SinglePrintShelf.Strategy.CS (class _A2_45_SinglePrintShelf).
// Name: A2-45 single-print shelf as a destination
// Header version: 1
// Rules version: catalogue specification under the A2-45 heading, rewritten 2026-11-15.
// Study class: _A2_45_SinglePrintShelf
// File: sandbox/_A2_45_SinglePrintShelf.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-45 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: A shelf is a SinglePrints run of at least 4 rows on a completed profile from the last 5 sessions. The near edge is the edge closer to price. ASSUMPTION: one contract, so the 60 percent scale at the near edge is the GX target and the runner to the far edge is not sent. The decision bar is a 1-minute close whose session clock is on a 15-minute boundary. The 10:00 clock is RthStart plus 30 minutes.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-45 heading. Do not paste the entry.
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
// Exit orders: GX exitPlan (stop and the near-edge target) and exitBar. Flat at the session value end.
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
// Other: ASSUMPTION: A shelf is a SinglePrints run of at least 4 rows on a completed profile from the last 5 sessions. The near edge is the edge closer to price. ASSUMPTION: one contract, so the 60 percent scale at the near edge is the GX target and the runner to the far edge is not sent. The decision bar is a 1-minute close whose session clock is on a 15-minute boundary. The 10:00 clock is RthStart plus 30 minutes.
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
// logSignal tag: A2-45
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: A shelf is a SinglePrints run of at least 4 rows on a completed profile from the last 5 sessions. The near edge is the edge closer to price. ASSUMPTION: one contract, so the 60 percent scale at the near edge is the GX target and the runner to the far edge is not sent. The decision bar is a 1-minute close whose session clock is on a 15-minute boundary. The 10:00 clock is RthStart plus 30 minutes.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_45_SinglePrintShelf : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private bool m_done; private DateTime m_day = DateTime.MinValue; private double m_near, m_far;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_45_SinglePrintShelf(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			GxOrderPrefix = "A245"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A245LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A245SE", EOrderAction.SellShort));
		}
		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd);
			base.StartCalc();
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; }
			if (StrategyInfo.MarketPosition != 0 || m_done || rollOn(1) || !IsRth()) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (tod < m_rthStart.Add(TimeSpan.FromMinutes(30))) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			if (((int)tod.TotalMinutes) % 15 != 0 || Bars.CurrentBar < 30 * 15) return;
			double atr30 = atrOf(30, 14), atr15 = atrOf(15, 14);
			double vwap = CurrentVwap1;
			if (!(atr30 > 0) || !(atr15 > 0) || double.IsNaN(vwap)) return;
			if (!pickShelf(atr30)) return;
			bool up = m_near > Bars.Close[0];
			if (migrationAgainst(up) || hvnBetween(up)) { m_done = true; return; }
			double priorHigh = blockExtreme(15, 1, true), priorLow = blockExtreme(15, 1, false);
			double thisLow = blockExtreme(15, 0, false), thisHigh = blockExtreme(15, 0, true);
			bool bos = up ? Bars.Close[0] > priorHigh && Bars.Close[0] > vwap : Bars.Close[0] < priorLow && Bars.Close[0] < vwap;
			if (!bos) return;
			double entry = Bars.Close[0];
			double structural = up ? Math.Min(thisLow, priorLow) : Math.Max(thisHigh, priorHigh);
			structural = up ? structural - tickSize() : structural + tickSize();
			double atrStop = up ? entry - atr15 : entry + atr15;
			double stop = up ? Math.Min(structural, atrStop) : Math.Max(structural, atrStop);
			stop = widen(up, entry, stop);
			if (up ? m_near - entry < Math.Abs(entry - stop) : entry - m_near < Math.Abs(entry - stop)) { m_done = true; return; }
			if (!budget(entry, stop)) { m_done = true; return; }
			arm(up, entry, stop, m_near, up ? "A245LE" : "A245SE", 0);
			if (up) m_le.Send(2); else m_se.Send(2);
			m_done = true;
		}
		private double blockExtreme(int minutes, int ago, bool high)
		{
			double v = high ? double.MinValue : double.MaxValue;
			int from = ago * minutes;
			for (int i = from; i < from + minutes; i++) v = high ? Math.Max(v, Bars.High[i]) : Math.Min(v, Bars.Low[i]);
			return v;
		}
		private double atrOf(int minutes, int length)
		{
			double sum = 0; int n = 0;
			for (int k = 0; k < length; k++)
			{
				double h = blockExtreme(minutes, k, true), l = blockExtreme(minutes, k, false);
				double prev = k + 1 < Bars.CurrentBar / minutes ? Bars.Close[(k + 1) * minutes] : l;
				double tr = Math.Max(h - l, Math.Max(Math.Abs(h - prev), Math.Abs(l - prev)));
				sum += tr; n++;
			}
			return n > 0 ? sum / n : 0;
		}
		private bool migrationAgainst(bool up)
		{
			List<VpExtra> profs = RecentCompletedProfiles(1, 2, Bars.FullSymbolData.Current);
			if (profs == null || profs.Count < 2) return false;
			double mid0 = (profs[0].vah + profs[0].val) / 2.0, mid1 = (profs[1].vah + profs[1].val) / 2.0;
			return up ? mid0 < mid1 : mid0 > mid1;
		}
		private bool hvnBetween(bool up)
		{
			VpExtra comp = Composite(1, 5);
			if (comp == null || comp.hvns == null) return false;
			int n = 0;
			foreach (Node node in comp.hvns)
			{
				bool between = up ? node.peak > Bars.Close[0] && node.peak < m_near : node.peak < Bars.Close[0] && node.peak > m_near;
				if (between) n++;
			}
			return n >= 2;
		}
		
		private bool pickShelf(double atr)
		{
			List<VpExtra> profs = RecentCompletedProfiles(1, 5, Bars.FullSymbolData.Current);
			double best = double.MaxValue; bool any = false;
			if (profs == null) return false;
			foreach (VpExtra e in profs)
			{
				foreach (double[] run in SinglePrints(e, 4))
				{
					double lo = run[0], hi = run[1];
					double near = Math.Abs(Bars.Close[0] - lo) < Math.Abs(Bars.Close[0] - hi) ? lo : hi;
					double far = near == lo ? hi : lo;
					double dist = Math.Abs(near - Bars.Close[0]);
					if (dist < 0.5 * atr || dist > 2.5 * atr || Session == null) continue;
					bool filled = near > Bars.Close[0] ? Session.rthHigh >= near : Session.rthLow > 0 && Session.rthLow <= near;
					if (filled) continue;
					if (dist < best) { best = dist; m_near = near; m_far = far; any = true; }
				}
			}
			return any;
		}
		
		private int floorTicks()
		{
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) return p.costFloorTicks; }
			catch (Exception) { }
			return 1;
		}
		private double widen(bool longSide, double entry, double stop)
		{
			double floor = floorTicks() * tickSize();
			if (longSide && entry - stop < floor) return entry - floor;
			if (!longSide && stop - entry < floor) return entry + floor;
			return stop;
		}
		private bool budget(double entry, double stop)
		{
			double risk = Math.Abs(entry - stop);
			int qty = 2; if (journalOn()) trace(ETraceLevel.Warning, "multi-contract size " + qty); bool ok = risk > 0 && qty * risk * Bars.Info.BigPointValue <= equityAvailable(0.01); if (!ok && journalOn()) trace(ETraceLevel.Error, "skipped: multi-contract risk exceeds 1 percent"); return ok;
		}
		private void arm(bool longSide, double entry, double stop, double target, string name, int holdDays)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = target > 0 || target < 0 ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			if (!(GxConfig.FinalTargetValue > 0)) GxConfig.TargetMode = ETargetMode.None;
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = holdDays > 0 ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.Flat1555;
			GxConfig.MaxHoldDays = holdDays;
			GxConfig.TimeStopMinutes = 0;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 1, mkt, name);
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

	}
}