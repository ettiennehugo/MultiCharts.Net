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
// Research assumption: ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. One contract, so the 50 percent scale at 0.5 of the way is not sent.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-32 heading. Do not paste the entry.
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
// Typical: ES or NQ as the catalogue says. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
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
// Other: ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. One contract, so the 50 percent scale at 0.5 of the way is not sent.
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
// ASSUMPTION: Levels come from _GmmProfileTracker.LastSigma.levels after each RTH close. The break is of the nearest translated component centre. The catalogue edge is z_k plus or minus one component sigma; that edge is not a separate field on SigmaMixtureResult, so the centre is used. The target is the next centre, passed to GX as a point distance. Flat 15:55. One contract, so the 50 percent scale at 0.5 of the way is not sent.
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
		private _GmmProfileTracker m_gmm;
		private DateTime m_day = DateTime.MinValue;
		private bool m_fed, m_done;
		private int m_up, m_dn;

		public _A2_32_SigmaMixture(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			ProfileStats.enabled = true;
			GxOrderPrefix = "A232"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A232LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A232SE", EOrderAction.SellShort));
		}

		protected override void StartCalc() { base.StartCalc(); m_gmm = new _GmmProfileTracker(tickSize()); }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_fed = m_done = false; m_up = m_dn = 0; }
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
			m_up = !double.IsNaN(above) && c > above ? m_up + 1 : 0;
			m_dn = !double.IsNaN(below) && c < below ? m_dn + 1 : 0;
			if (m_up < 2 && m_dn < 2) return;
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
			if (!(risk > 0) || double.IsNaN(target) || (longSide ? target - entry : entry - target) < 1.2 * risk || risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ m_done = true; return; }
			arm(longSide, entry, stop, target);
			if (longSide) m_le.Send(); else m_se.Send();
			m_done = true;
		}

		private void arm(bool longSide, double entry, double stop, double target)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = 1555;
			GxConfig.TimeStopMinutes = 0;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 1, mkt, longSide ? "A232LE" : "A232SE");
		}
	}
}
