// ## 1. Identity
// Catalogue ID: DV-06
// ID mapping: DV-06 -> _DV_06_EndOfTrend.Strategy.CS (class _DV_06_EndOfTrend).
// Name: DV-06 end-of-trend cluster pullback
// Header version: 1
// Rules version: catalogue specification under the DV-06 heading, rewritten 2026-11-15.
// Study class: _DV_06_EndOfTrend
// File: sandbox/_DV_06_EndOfTrend.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the DV-06 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the 14:00 trend check is RthStart plus 270 minutes. A trend day has a close-to-open of at least 0.7 times the session range so far. The level is the HVN on today's RangeProfileAfter whose peak is nearest the trend extreme. The limit is that node's near edge. The core bracket is 0.15 times Atr20d. The window ends at the session value end.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the DV-06 heading. Do not paste the entry.
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
// Exit orders: GX. Stop and target are 0.15 times Atr20d from the limit. Flat at the session value end.
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
// Other: ASSUMPTION: ASSUMPTION: the 14:00 trend check is RthStart plus 270 minutes. A trend day has a close-to-open of at least 0.7 times the session range so far. The level is the HVN on today's RangeProfileAfter whose peak is nearest the trend extreme. The limit is that node's near edge. The core bracket is 0.15 times Atr20d. The window ends at the session value end.
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
// logSignal tag: DV-06
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: the 14:00 trend check is RthStart plus 270 minutes. A trend day has a close-to-open of at least 0.7 times the session range so far. The level is the HVN on today's RangeProfileAfter whose peak is nearest the trend extreme. The limit is that node's near edge. The core bracket is 0.15 times Atr20d. The window ends at the session value end.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _DV_06_EndOfTrend : _VolumeProfileFuturesBase
	{
		
		private IOrderPriced m_le, m_se; private bool m_done; private DateTime m_day = DateTime.MinValue, m_open = DateTime.MinValue;
		public _DV_06_EndOfTrend(object _ctx) : base(_ctx) { }
		protected override void Create() { base.Create(); RollDetection = ERollDetection.Series; GxOrderPrefix = "DV06"; createGxOrders(); m_le = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV06LE", EOrderAction.Buy)); m_se = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV06SE", EOrderAction.SellShort)); }
		protected override void StartCalc() {
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrLength = 20; base.StartCalc(); }
		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_open = DateTime.MinValue; }
			if (IsRth() && m_open == DateTime.MinValue) m_open = Bars.Time[0];
			if (StrategyInfo.MarketPosition != 0 || m_done || !IsRth() || rollOn(1)) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (tod < m_rthStart.Add(TimeSpan.FromMinutes(270)) || tod > m_rthValueEnd) return;
			SessionStats s = Session;
			double span = s.rthHigh - s.rthLow;
			if (!(span > 0) || Math.Abs(Bars.Close[0] - s.rthOpen) < 0.7 * span) return;
			VpExtra prof = RangeProfileAfter(1, m_open, Bars.Time[0]);
			if (prof == null || prof.hvns == null || prof.hvns.Count == 0) return;
			bool up = Bars.Close[0] > s.rthOpen;
			double best = double.MaxValue; Node pick = prof.hvns[0]; bool any = false;
			foreach (Node n in prof.hvns)
			{
				double d = up ? Math.Abs(n.low - s.rthLow) : Math.Abs(n.high - s.rthHigh);
				if (d < best) { best = d; pick = n; any = true; }
			}
			if (!any) return;
			double atr = DailyContext.Atr20d; if (!(atr > 0)) return;
			double entry = up ? pick.high : pick.low;
			double stop = up ? entry - 0.15 * atr : entry + 0.15 * atr;
			stop = widen(up, entry, stop);
			double target = up ? entry + 0.15 * atr : entry - 0.15 * atr;
			if (!budget(entry, stop)) { m_done = true; return; }
			arm(up, entry, stop, target, up ? "DV06LE" : "DV06SE", false);
			if (up) m_le.Send(entry); else m_se.Send(entry);
			m_done = true;
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
		private bool budget(double a, double b) { double r = Math.Abs(a - b); return r > 0 && r * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private void arm(bool buy, double entry, double stop, double target, string name, bool hold)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = hold ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.Flat1555;
			GxConfig.MaxHoldDays = hold ? 5 : 0; GxConfig.TimeStopMinutes = 0;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
		}
	}
}
