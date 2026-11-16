// ## 1. Identity
// Catalogue ID: A2-46
// ID mapping: A2-46 -> _A2_46_VolumeGapCommitment.Strategy.CS (class _A2_46_VolumeGapCommitment).
// Name: A2-46 volume-gap commitment
// Header version: 1
// Rules version: catalogue specification under the A2-46 heading, rewritten 2026-11-15.
// Study class: _A2_46_VolumeGapCommitment
// File: sandbox/_A2_46_VolumeGapCommitment.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-46 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: adjacent HVNs on the 10-session composite are the gap edges, and the price width must be at least one 30-minute ATR. The 0.35 row-volume test is not applied because the volume of the rows between two nodes is not a public field. The 30-minute ATR is the mean of 14 non-overlapping 30 one-minute ranges. A 5-minute close is a 1-minute bar whose session minute is a multiple of 5. The window is RthStart plus 15 minutes through RthStart plus 5 hours. One contract, so the 50 and 25 percent scales are not sent.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-46 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute. Data2 = 30-minute.
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
// Exit orders: GX exitPlan and exitBar. The stop is the nearer gap edge minus 0.25 times the 5-minute ATR. The target is the far edge. MaxHoldDays is 1.
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
// Other: ASSUMPTION: ASSUMPTION: adjacent HVNs on the 10-session composite are the gap edges, and the price width must be at least one 30-minute ATR. The 0.35 row-volume test is not applied because the volume of the rows between two nodes is not a public field. The 30-minute ATR is the mean of 14 non-overlapping 30 one-minute ranges. A 5-minute close is a 1-minute bar whose session minute is a multiple of 5. The window is RthStart plus 15 minutes through RthStart plus 5 hours. One contract, so the 50 and 25 percent scales are not sent.
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
// logSignal tag: A2-46
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: adjacent HVNs on the 10-session composite are the gap edges, and the price width must be at least one 30-minute ATR. The 0.35 row-volume test is not applied because the volume of the rows between two nodes is not a public field. The 30-minute ATR is the mean of 14 non-overlapping 30 one-minute ranges. A 5-minute close is a 1-minute bar whose session minute is a multiple of 5. The window is RthStart plus 15 minutes through RthStart plus 5 hours. One contract, so the 50 and 25 percent scales are not sent.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_46_VolumeGapCommitment : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se;
		private bool m_done; private DateTime m_day = DateTime.MinValue;
		private double m_lo, m_hi;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_46_VolumeGapCommitment(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   // composites from Own RTH profiles, not MC's Volume Profile (not on the research charts)
			GxOrderPrefix = "A246"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A246LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A246SE", EOrderAction.SellShort));
		}
		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			base.StartCalc();
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; }
			if (StrategyInfo.MarketPosition != 0 || m_done || rollOn(1) || !IsRth()) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (tod < m_rthStart.Add(TimeSpan.FromMinutes(15)) || tod > m_rthStart.Add(TimeSpan.FromHours(5))) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			if (((int)tod.TotalMinutes) % 5 != 0) return;
			double atr30 = blockAtr(30, 14), atr5 = blockAtr(5, 14);
			if (!(atr30 > 0) || !(atr5 > 0) || !findGap(atr30)) return;
			double g = m_hi - m_lo;
			bool up = Bars.Close[0] >= m_lo + 0.15 * g && Bars.Close[0] < m_hi && Bars.Close[1] < m_lo + 0.15 * g;
			bool dn = Bars.Close[0] <= m_hi - 0.15 * g && Bars.Close[0] > m_lo && Bars.Close[1] > m_hi - 0.15 * g;
			if (!up && !dn) return;
			double entry = Bars.Close[0];
			double stop = up ? m_lo - 0.25 * atr5 : m_hi + 0.25 * atr5;
			stop = widen(up, entry, stop);
			if (!budget(entry, stop)) { m_done = true; return; }
			GxConfig.MaxHoldDays = 1;
			arm(up, entry, stop, up ? m_hi : m_lo, up ? "A246LE" : "A246SE");
			if (up) m_le.Send(2); else m_se.Send(2);
		}
		private double blockAtr(int width, int n)
		{
			if (Bars.CurrentBar < width * n + 1) return double.NaN;
			double s = 0;
			for (int k = 0; k < n; k++)
			{
				double hi = double.MinValue, lo = double.MaxValue;
				for (int i = k * width; i < (k + 1) * width; i++) { hi = Math.Max(hi, Bars.High[i]); lo = Math.Min(lo, Bars.Low[i]); }
				s += hi - lo;
			}
			return s / n;
		}
		private bool findGap(double atr)
		{
			VpExtra comp = Composite(1, 10);
			if (comp == null || comp.hvns == null || comp.hvns.Count < 2) return false;
			List<Node> nodes = new List<Node>(comp.hvns);
			nodes.Sort((a, b) => a.low.CompareTo(b.low));
			double best = 0; bool any = false;
			for (int i = 0; i + 1 < nodes.Count; i++)
			{
				double gap = nodes[i + 1].low - nodes[i].high;
				double smaller = Math.Min(nodes[i].peakVol, nodes[i + 1].peakVol);
				if (!(gap >= atr) || !(smaller > 0)) continue;
				if (gap > best) { best = gap; m_lo = nodes[i].high; m_hi = nodes[i + 1].low; any = true; }
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

		private void arm(bool longSide, double entry, double stop, double target, string name)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.HoldOvernight;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
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


		private bool sandboxBook()
		{
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(sessionClock(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
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