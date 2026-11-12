// ## 1. Identity
// Catalogue ID: SW-02
// ID mapping: SW-02 → _SW_02_SwingPullback.Strategy.CS (class _SW_02_SwingPullback). SwingPullback names the RSI(2) dip with the swing-low AVWAP filter.
// Name: Swing pullback
// Header version: 1
// Rules version: catalogue v2.7 AVWAP-filter arm, as implemented by docs/engines-manual.md §3.5 (example 16)
// Study class: _SW_02_SwingPullback
// File: sandbox/_SW_02_SwingPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "SW-02 · RSI(2) dip-buy". The code is engines-manual example 16.
// Taken from the source: close above the 200-day average and RSI(2) under 10 buys; exit when the close is above the 5-day average or after 5 bars. The filter variant also requires the close above the latest swing-low AVWAP. Disaster stop 3 × ATR(20).
// Research assumption: the RSI is Cutler's simple-average RSI, labelled in the method, not Wilder's. The filter is on. The composite-VAH filter and the unfiltered base are not coded. The buy is at the signal close for the next bar's open.
//
// ## 3. Hypothesis
// Mechanism: a short-term oversold dip in an index that is still above its long average tends to revert. The AVWAP filter tries to keep the dip on the side of the last swing-low cohort. The catalogue grades the filter as a hypothesis.
// Link: specs/trading-idea-catalogue.md, heading "SW-02 · RSI(2) dip-buy with a profile/AVWAP location filter"
//
// ## 4. Trade walkthrough
// Trigger: on a daily close, if the close is above the 200-bar average, RSI(2) is under 10, and the close is above the latest valid swing-low AVWAP, buy the next bar.
// Management: the disaster stop stays 3 × ATR(20) under the signal close, floored at 14 ticks.
// Exit: the stop, a close above the 5-bar average, or the fifth daily bar after the entry bar.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar, once CurrentBar is past the 200-bar average.
// Order sent: MarketNextBar SW02LE. The stop is SW02LS. The time or average exit is MarketNextBar SW02LX.
// Can fill: the next daily bar's open.
//
// ## 6. Position and re-entry
// Position: flat or one long. No scale-in and no short.
// Re-entry: a later day can enter after the position is flat.
// Same session after a stop: a daily bar is the session. The next bar may enter if the rules pass. A roll bar does not.
//
// ## 7. Instruments
// Typical: MES and MNQ, also GC, on daily bars. This file is written for @ES daily.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES daily, RTH session template, dxFeed continuous unadjusted. Roll gaps sit inside the 200-bar average.
// Data2: Not applicable
// Further series: Not applicable. The swing-low AVWAP is built on Data1.
// Warmup: 201 daily bars before an entry is considered (CurrentBar > smaLong + 1).
// Tests use daily data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; not a roll bar; close above the 200-bar average; RSI(2) under 10; if the filter is on, a valid swing-low AVWAP exists and the close is above it; ATR(20) is positive; the widened stop fits the 1% budget.
// Skip when: the AVWAP is missing or the close is not above it (logSignal false); the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the disaster distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar SW02LE.
// Exit orders: MarketNextBar SW02LX; Stop SW02LS.
// Fill assumption: next bar's open. The stop is sent on the signal bar, but the stored stop is measured from the signal close.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. The catalogue's market is MES. Not yet verified
//
// ## 12. Trade management
// While open: the stop is re-sent. The average exit and the five-bar exit are checked on each close.
//
// ## 13. Exits
// Hard stop: signal close minus max(3 × ATR(20), 14 ticks).
// Target: Not applicable. The 5-bar average is an exit, not a limit target.
// Time exit: five daily bars after the entry bar, counted with CurrentBar.
// Invalidation: a roll bar does not enter. An open trade is not flattened by the roll.
//
// ## 14. Regimes
// Suitable: a daily uptrend (close above the 200-bar average) with a short-term dip.
// Unsuitable: close at or below the 200-bar average, and a close that is not above the swing-low AVWAP.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: M-AVWAP with AvwapEvents = SwingLow. That anchor turns price action on for Data1.
// Engine settings enabled: RollDetection = Series. AvwapLimits.maxAgeSessions = 60.
//
// ## 16. Pitfalls
// Lookahead: the averages, RSI and ATR use bars that have closed, including the signal bar, and the order is for the next bar. The AVWAP is the latest valid swing-low anchor.
// Repaint: Not applicable. The anchor is used from the manager's latest valid value.
// Survivorship: Not applicable
// Rolls: Series. A roll bar does not enter. An unadjusted splice still sits inside the 200-bar average.
// Data quality: an RTH daily template is required. A 24-hour daily bar is a different series. Not yet verified on a chart.
// Other: RSI here is Cutler's simple average, not Wilder's smoothing.
//
// ## 17. Parameters
// This file declares no strategy [Input]. TraceLevel stays the base [Input] on _CustomSignal.
// Fixed example settings stay in the protected config class. They are not a sweep.
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
// Predeclared before any result was read: yes, 2026-11-12. No result for this file has been read. The coded arm is the engines-manual example, not a fitted set.
// catalogue-default: judge this file by its catalogue kill, catalogue §2.3, and the A2 generic kill when the idea is A2-origin. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1% of winning trades is not a whole number, remove K = max(1, ceil(0.01 × the number of winning trades)). If there are no winning trades, remove none. This is the programme overview's proposed rounding, adopted here before any run.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025–Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
//   Catalogue M19 discusses sample error. A minimum count for this strategy is written here before incubation, or it stays Not yet verified.
// Drift: Not yet verified
//   Compare regime mix, fill versus system price, and costs with the tested profile. Numbers come from this strategy's own test, written down before incubation.
// Warning: Not yet verified
//   Warning means look. It is not an order.
// Pause: Not yet verified
//   Pause means stop new entries. Open positions keep the exits in section 13.
// Review: Not yet verified
//   Review means the owner looks before new entries resume.
// Uncertainty: no run yet; Cutler RSI and the AVWAP filter are example choices; unadjusted daily gaps sit in the 200-bar average; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: SW02LE
// logSignal tag: SW-02
// Skip / flatten lines: logSignal false when the AVWAP filter blocks; "SW-02" plus "skip: stop" when the budget fails.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// avwapFilter stays true inside the protected config. It is a bool, not a multi-value input.
// Uncoded catalogue arms (filter off, composite VAH, RSI threshold 5, Wilder RSI) are an owner question, not this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _SW_02_SwingPullback : _CustomSignal
	{
		protected sealed class Sw02Config
		{
			public int contracts = 1;
			public int smaLong = 200, smaExit = 5, rsiLength = 2, maxHoldBars = 5, atrLength = 20;
			public double rsiEntry = 10, disasterAtr = 3.0;
			public bool avwapFilter = true;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Sw02Config Cfg { get; } = new Sw02Config();

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private double m_stop = double.NaN;
		private int m_entryBar = -1;

		public _SW_02_SwingPullback(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			AvwapEvents = EAvwapAnchor.SwingLow;      // lazy: creates the AVWAP manager; swing anchors force PRICEACTION on (Data1)
			AvwapLimits.maxAgeSessions = 60;          // daily bars: one session per bar
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SW02LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SW02LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SW02LS", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_stop = double.NaN; m_entryBar = -1;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close || Bars.CurrentBar <= Cfg.smaLong + 1) return;
			double c = Bars.Close[0];
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0)
			{
				if (m_entryBar < 0) m_entryBar = Bars.CurrentBar;
				if (c > sma(Cfg.smaExit) || Bars.CurrentBar - m_entryBar >= Cfg.maxHoldBars) { m_lx.Send(); return; }
				m_lxStp.Send(m_stop);
				return;
			}
			m_entryBar = -1;
			if (rollOn(1)) return;
			if (!(c > sma(Cfg.smaLong)) || !(rsi(Cfg.rsiLength) < Cfg.rsiEntry)) return;
			double av = double.NaN;
			if (Cfg.avwapFilter)
			{
				_AvwapManager mgr = AvwapManager;
				_Avwap a = mgr != null ? mgr.Latest(EAvwapAnchor.SwingLow) : null;
				av = a != null && a.Valid ? a.Value : double.NaN;
				if (double.IsNaN(av) || c <= av) { if (journalOn()) logSignal("SW-02", +1, c, double.NaN, new Dictionary<string, string> { { "avwap", double.IsNaN(av) ? "na" : av.ToString("F2") } }, false); return; }
			}
			double tick = tickSize(), atr = averageTrueRange(Bars, Cfg.atrLength);
			if (!(atr > 0)) return;
			double r = Math.Max(Cfg.disasterAtr * atr, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"SW-02 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = c - r;
			m_le.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "rsi2", rsi(Cfg.rsiLength).ToString("F1") }, { "atr20", atr.ToString("F2") }, { "avwap", double.IsNaN(av) ? "off" : av.ToString("F2") } };
				journalArm("SW02LE", c, m_stop, double.NaN, ctx);
				logSignal("SW-02", +1, c, m_stop, ctx, true);
			}
		}

		private double sma(int n) { double s = 0; for (int i = 0; i < n; i++) s += Bars.Close[i]; return s / n; }

		/// <summary>Cutler RSI (simple averages) over n closes; 50 when flat.</summary>
		private double rsi(int n)
		{
			double up = 0, dn = 0;
			for (int i = 0; i < n; i++) { double d = Bars.Close[i] - Bars.Close[i + 1]; if (d > 0) up += d; else dn -= d; }
			return up + dn == 0 ? 50.0 : 100.0 * up / (up + dn);
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
