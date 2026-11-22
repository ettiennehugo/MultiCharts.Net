// ## 1. Identity
// Catalogue ID: A2-67
// ID mapping: A2-67 -> _A2_67_IntradayRSIOf.Strategy.CS (class _A2_67_IntradayRSIOf).
// Name: A2-67 RSI of highs and lows
// Header version: 1
// Rules version: catalogue specification under the A2-67 heading, rewritten 2026-11-15.
// Study class: _A2_67_IntradayRSIOf
// File: sandbox/_A2_67_IntradayRSIOf.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-67 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: a 5-minute bar is five 1-minute bars, and the decision is the 1-minute bar whose clock minute is a multiple of 5. RSI is the simple average of the last 10 five-minute changes, not a Wilder RSI, because the base class has no RSI of highs and lows. The long needs three rising 5-minute lows, RSI of lows below 20, and RSI of highs minus RSI of lows above 15. The 2 ATR disaster stop is the catalogue's ablated stop.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-67 heading. Do not paste the entry.
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
// Market tag: both
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 5-minute.
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
// Exit orders: sendExits. Market exit when the RSI of 5-minute highs reaches 95, or five minutes before the chart session ends. Disaster stop is 2 times the 14-bar 5-minute range, widened to the cost floor. That RSI exit is not a GX target.
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
// Other: ASSUMPTION: ASSUMPTION: a 5-minute bar is five 1-minute bars, and the decision is the 1-minute bar whose clock minute is a multiple of 5. RSI is the simple average of the last 10 five-minute changes, not a Wilder RSI, because the base class has no RSI of highs and lows. The long needs three rising 5-minute lows, RSI of lows below 20, and RSI of highs minus RSI of lows above 15. The 2 ATR disaster stop is the catalogue's ablated stop.
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
// logSignal tag: A2-67
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: both
// ASSUMPTION: ASSUMPTION: a 5-minute bar is five 1-minute bars, and the decision is the 1-minute bar whose clock minute is a multiple of 5. RSI is the simple average of the last 10 five-minute changes, not a Wilder RSI, because the base class has no RSI of highs and lows. The long needs three rising 5-minute lows, RSI of lows below 20, and RSI of highs minus RSI of lows above 15. The 2 ATR disaster stop is the catalogue's ablated stop.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_67_IntradayRSIOf : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_ls;
		private double m_stop; private bool m_done; private DateTime m_day = DateTime.MinValue;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_67_IntradayRSIOf(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A267LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A267LX", EOrderAction.Sell, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A267LS", EOrderAction.Sell, OrderExit.FromAll));
		}
		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); requireRing(1, 2000, "A2-67 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */
		}
		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd); }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime day = Bars.Time[0].Date;
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; }
			TimeSpan tod = Bars.Time[0].TimeOfDay;
			bool boundary = ((int)tod.TotalMinutes) % 5 == 0;
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0)
			{
				if (sessionFlat() || (boundary && rsiHigh(10) >= 95)) { m_lx.Send(); return; }
				m_ls.Send(m_stop); return;
			}
			if (m_done || !boundary || Bars.CurrentBar < 80) return;
			double l0 = fiveLow(0), l1 = fiveLow(1), l2 = fiveLow(2);
			if (!(l0 > l1 && l1 > l2)) return;
			double rLow = rsiOf(false, 10), rHigh = rsiOf(true, 10);
			if (!(rLow < 20) || !(rHigh - rLow > 15)) return;
			double atr = fiveAtr();
			if (!(atr > 0)) return;
			double entry = Bars.Close[0];
			m_stop = entry - 2 * atr;
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			if (entry - m_stop < ticks * tickSize()) m_stop = entry - ticks * tickSize();
			if ((entry - m_stop) * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Journal, "A2-67 skip: stop breaches risk budget"); m_done = true; return; }
			(entryOpen() ? m_le : null)?.Send(); m_done = true; m_ls.Send(m_stop);
		}
		private double fiveLow(int ago)
		{
			double lo = double.MaxValue;
			for (int i = ago * 5; i < ago * 5 + 5; i++) lo = Math.Min(lo, lbBar(i).l);
			return lo;
		}
		private double fiveHigh(int ago)
		{
			double hi = double.MinValue;
			for (int i = ago * 5; i < ago * 5 + 5; i++) hi = Math.Max(hi, lbBar(i).h);
			return hi;
		}
		private double rsiOf(bool highs, int n)
		{
			double up = 0, dn = 0;
			for (int i = 0; i < n; i++)
			{
				double a = highs ? fiveHigh(i) : fiveLow(i);
				double b = highs ? fiveHigh(i + 1) : fiveLow(i + 1);
				double ch = a - b;
				if (ch > 0) up += ch; else dn -= ch;
			}
			if (up + dn == 0) return 50;
			return 100 - 100 / (1 + up / Math.Max(dn, 1e-9));
		}
		private double rsiHigh(int n) { return rsiOf(true, n); }
		private double fiveAtr()
		{
			double s = 0;
			for (int i = 0; i < 14; i++) s += fiveHigh(i) - fiveLow(i);
			return s / 14.0;
		}
		private bool sessionFlat()
		{
			if (Bars.Sessions == null || Bars.Sessions.Count == 0) return false;
			TimeSpan t = Bars.Time[0].TimeOfDay;
			TimeSpan end = Bars.Sessions[Bars.Sessions.Count - 1].EndTime;
			return t >= end.Subtract(TimeSpan.FromMinutes(5)) && t < end.Add(TimeSpan.FromMinutes(1));
		}
	}
}
