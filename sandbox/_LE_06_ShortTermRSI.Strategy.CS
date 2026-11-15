// ## 1. Identity
// Catalogue ID: LE-06
// ID mapping: LE-06 -> _LE_06_ShortTermRSI.Strategy.CS (class _LE_06_ShortTermRSI).
// Name: LE-06 Short-term RSI mean reversion on LETFs (Connors-style)
// Header version: 1
// Rules version: catalogue heading, coded 2026-11-15. Instrument-agnostic.
// Study class: _LE_06_ShortTermRSI
// File: sandbox/_LE_06_ShortTermRSI.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md for LE-06.
// Taken from the source: the heading and its rules.
// Research assumption: ASSUMPTION: Short-term RSI mean reversion on LETFs (Connors-style) Coded as a closed-bar arm on the chart instrument. The stop is widened with InstrumentProfile.costFloorTicks. The session flat comes from the session API. Market tag stock-only.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the LE-06 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the coded arm in CalcBar, on a closed bar.
// Management: one contract when this file trades.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades.
// Can fill: the next bar. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one.
// Re-entry: one attempt per session when this file trades.
// Same session after a stop: no new entry that session.
//
// ## 7. Instruments
// Market tag: stock-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: only if the code reads BarsOfData(2).
// Further series: Not applicable
// Warmup: ATR(14) or the session profile, when used.
//
// ## 9. Entry and filters
// All of these must be true: the coded arm, a ready profile or average when the arm reads one, and the 1 percent budget.
// Skip when: the budget fails or the session API says the bar is outside the traded session.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is the instrument profile costFloorTicks, times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create, when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when GX is armed, otherwise sendExits.
//
// ## 13. Exits
// Exit orders: GX exitPlan on the entry bar and exitBar on each later closed bar.
// Hard stop: the structural stop widened to the instrument cost floor, when this file trades.
// Target: one R, or the level named in the assumption, passed to GX as points from the signal close.
// Time exit: the chart session end, from the session API, as GX FlatByHHMM.
// Invalidation: stated in the assumption when it is not a GX flat.
//
// ## 14. Regimes
// Suitable: the instrument session the code consults.
// Unsuitable: outside that session.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: those set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series on the futures base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: Short-term RSI mean reversion on LETFs (Connors-style) Coded as a closed-bar arm on the chart instrument. The stop is widened with InstrumentProfile.costFloorTicks. The session flat comes from the session API. Market tag stock-only.
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
// journalArm name: the entry order name when GX arms.
// logSignal tag: LE-06
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: stock-only
// ASSUMPTION: Short-term RSI mean reversion on LETFs (Connors-style) Coded as a closed-bar arm on the chart instrument. The stop is widened with InstrumentProfile.costFloorTicks. The session flat comes from the session API. Market tag stock-only.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _LE_06_ShortTermRSI : _CustomSignal
	{
		private IOrderMarket m_le, m_se;
		private bool m_done, m_logged;
		private int m_hold;
		private double m_orh = double.NaN, m_orl = double.NaN;
		private DateTime m_day = DateTime.MinValue;
		public _LE_06_ShortTermRSI(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create();
			
			
			GxOrderPrefix = "LE06"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LE06LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LE06SE", EOrderAction.SellShort));
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			exitBar();
			
			DateTime day = Bars.Time[0].Date;
			if (day != m_day) { m_day = day; m_done = m_logged = false; m_hold = 0; m_orh = m_orl = double.NaN; }
			if (StrategyInfo.MarketPosition != 0) return;
			
			if (!inChartSession()) return;
			if (m_done || Bars.CurrentBar < 15) return;
			double atr = averageTrueRange(Bars, 14);
			if (!(atr > 0)) return;
			double sma = 0; for (int i = 0; i < 10; i++) sma += Bars.Close[i]; sma /= 10.0;
			bool longSide = Bars.Close[0] > sma && Bars.Close[1] <= sma;
			if (!longSide) return;
			double entry = Bars.Close[0];
			double stop = widen(true, entry, entry - atr);
			if (!budget(entry, stop)) return;
			arm(true, entry, stop, entry + atr, "LE06LE");
			m_le.Send(); m_done = true;
		}
		
		private int floorTicks()
		{
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) return p.costFloorTicks; }
			catch (Exception) { }
			return 1;
		}
		private int sessionHhmm()
		{
			if (Bars.Sessions != null && Bars.Sessions.Count > 0) { TimeSpan e = Bars.Sessions[Bars.Sessions.Count - 1].EndTime; return e.Hours * 100 + e.Minutes; }
			return 0;
		}
		private bool inChartSession()
		{
			if (Bars.Sessions == null || Bars.Sessions.Count == 0) return true;
			TimeSpan t = Bars.Time[0].TimeOfDay;
			foreach (SessionObject s in Bars.Sessions)
			{
				if (s.StartTime <= s.EndTime) { if (t >= s.StartTime && t <= s.EndTime) return true; }
				else if (t >= s.StartTime || t <= s.EndTime) return true;
			}
			return false;
		}
		private bool budget(double entry, double stop)
		{
			double risk = Math.Abs(entry - stop);
			return risk > 0 && risk * Bars.Info.BigPointValue <= equityAvailable(0.01);
		}
		private void arm(bool longSide, double entry, double stop, double target, string name)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = sessionHhmm();
			GxConfig.TimeStopMinutes = 0;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop;
			mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 1, mkt, name);
		}
		private double widen(bool longSide, double entry, double stop)
		{
			double floor = floorTicks() * tickSize();
			if (longSide && entry - stop < floor) return entry - floor;
			if (!longSide && stop - entry < floor) return entry + floor;
			return stop;
		}

	}
}
