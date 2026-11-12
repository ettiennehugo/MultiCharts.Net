// ## 1. Identity
// Catalogue ID: TS-03
// ID mapping: TS-03 → _TS_03_CloseDrift.Strategy.CS (class _TS_03_CloseDrift). CloseDrift names the close-regime drift.
// Name: Close drift
// Header version: 1
// Rules version: catalogue v2.7 variant A, as implemented by docs/engines-manual.md §3.3 (example 13)
// Study class: _TS_03_CloseDrift
// File: sandbox/_TS_03_CloseDrift.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "TS-03 · Close regime". The code is engines-manual example 13.
// Taken from the source: at 15:50 ET, trade the sign of the 15:30–15:50 move, exit at 16:00, stop 1 × ATR of the 5-minute stream.
// Research assumption: variant B (the post-close reversion) is not coded. MOC imbalance data is not available, so the drift is the price proxy. ATR length is 14 on the 5-minute bars, the same length assumption as the other examples. Early-close days are skipped.
//
// ## 3. Hypothesis
// Mechanism: the published imbalance into the close pushes price from 15:30 to 15:50, and that push often extends into 16:00. Without the imbalance feed, the 20-minute price change is the proxy.
// Link: specs/trading-idea-catalogue.md, heading "TS-03 · Close regime: 15:30–16:00 MOC drift then reversion"
//
// ## 4. Trade walkthrough
// Trigger: the 1-minute bar that closes at 15:50 ET, if the close has moved at least one tick from the 15:30 close, buy or sell that direction.
// Management: the stop stays one ATR(14) of the 5-minute bars away from the signal close, floored at 14 ticks.
// Exit: the stop, or a market flatten on the first bar at or after 16:00 and before 17:00. That fill is the 16:01 open.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. The 15:30 reference is the 1-minute bar whose close time is 15:30. ATR uses closed Data2 bars.
// Order sent: MarketNextBar TS03LE or TS03SE. Stop TS03LS or TS03SS is sent on later bars while the position is open, not on the signal bar. Flat is MarketNextBar TS03LX or TS03SX.
// Can fill: the next bar. The stop is not working on the fill bar.
//
// ## 6. Position and re-entry
// Position: flat or one. No scale-in.
// Re-entry: MaxTradesPerDay is 1.
// Same session after a stop: no. The entry time has passed, and the day's entry count is used once a fill exists.
//
// ## 7. Instruments
// Typical: ES and NQ, 1-minute or 5-minute. This file is written for @ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted.
// Data2: @ES 5-minute, same symbol and session. HtfStreams is { 2 }.
// Further series: Not applicable
// Warmup: 16 closed 5-minute bars before ATR is available.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; not a roll bar; the day is not an early close; the 15:30 reference was stored; the bar closes at 15:50; the drift is at least one tick; entryAllowed allows it; ATR is available; the widened stop fits the 1% budget.
// Skip when: an early close (no 15:30–16:00 window); a roll; a drift under one tick; the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the stop distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar TS03LE or TS03SE.
// Exit orders: Stop TS03LS / TS03SS; MarketNextBar TS03LX / TS03SX.
// Fill assumption: next bar's open. The stop starts on the bar after the fill.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the stop is re-sent at the price stored at the signal.
//
// ## 13. Exits
// Hard stop: signal close ± max(ATR(14) of the 5-minute bars, 14 ticks).
// Target: Not applicable
// Time exit: 16:00 ET, market on the next bar.
// Invalidation: an early-close day never enters. A roll bar clears the 15:30 reference.
//
// ## 14. Regimes
// Suitable: a full RTH close with a 15:30–15:50 move of at least one tick.
// Unsuitable: CME early-close days, and a flat 20-minute window.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: M-MTF on Data2.
// Engine settings enabled: RollDetection = Series. HtfStreams = { 2 }. MaxTradesPerDay = 1.
//
// ## 16. Pitfalls
// Lookahead: the drift uses two closed 1-minute bars, and ATR uses HtfClosedBar.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. A splice during the drift window clears the reference and does not enter.
// Data quality: the 15:30 and 15:50 tests use an exact clock match, so a missing bar skips the day. Not yet verified on a chart.
// Other: the stop is not sent on the signal bar, so the fill bar is unprotected. Variant B is ReversionTo1645.
//
// ## 17. Parameters
// Input: Variant
// Purpose: which close-regime rule to run
// Default: DriftTo1600 — at 15:50 trade the sign of the 15:30-15:50 move, exit at 16:00. This is the previous trade list.
// Units: Not applicable
// Range: DriftTo1600, ReversionTo1645. Reversion fades that same 15:30-15:50 move at 16:00 and exits at 16:45. Any other value throws in StartCalc.
// Effect: the stop is still ATR(14) of the 5-minute stream, floored at 14 ticks. The name ATR5 in the catalogue means that stream, not a length of 5.
// Interactions: early-close days are skipped in both variants
// Fixed or research-tuned: fixed default. ReversionTo1645 is catalogue variant B.
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
// Uncertainty: no run yet; variant A only; ATR length 14 on the 5-minute stream; the fill bar is unprotected; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: TS03LE or TS03SE
// logSignal tag: TS-03
// Skip / flatten lines: "TS-03" plus "skip: stop" when the budget fails. An early close and a small drift return without a line.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Variant defaults to DriftTo1600, the previous trade list. ReversionTo1645 is catalogue variant B. ATR5 in the catalogue is ATR(14) on the 5-minute Data2 stream. The journal field is atr14of5m.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _TS_03_CloseDrift : _CustomSignal
	{
		/// <summary>Variant A fades nothing: it trades the 15:30-15:50 drift at 15:50 and exits at 16:00. Variant B fades that same drift at 16:00 and exits at 16:45.</summary>
		public enum EVariant { DriftTo1600, ReversionTo1645 }

		[Input] public EVariant Variant { get; set; }

		protected sealed class Ts03Config
		{
			public int contracts = 1;
			public TimeSpan driftFrom = new TimeSpan(15, 30, 0), entryAt = new TimeSpan(15, 50, 0), exitAt = new TimeSpan(16, 0, 0);
			public int minDriftTicks = 1;
			public int atrLength = 14;   // ATR(14) of the 5-minute Data2 stream. The catalogue name "ATR5" means that stream, not a length of 5.
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ts03Config Cfg { get; } = new Ts03Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private _CmeSessionClock m_clock;
		private double m_ref = double.NaN, m_drift = double.NaN, m_stop = double.NaN;
		private DateTime m_day = DateTime.MinValue;

		public _TS_03_CloseDrift(object _ctx) : base(_ctx) { Variant = EVariant.DriftTo1600; }

		private TimeSpan exitAt() { return Variant == EVariant.ReversionTo1645 ? new TimeSpan(16, 45, 0) : Cfg.exitAt; }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			MaxTradesPerDay = 1;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS03LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS03SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS03LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS03SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS03LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS03SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(EVariant), Variant)) throw new ArgumentException("_TS_03_CloseDrift: Variant is not a defined EVariant");
			HtfStreams = new[] { 2 };
			m_clock = null;
			base.StartCalc();
			m_ref = m_drift = m_stop = double.NaN; m_day = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_ref = m_drift = double.NaN; }
			TimeSpan tod = sessionClock(Bars.Time[0]);
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (tod >= exitAt() && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }   // next-bar market: variant A fills at the 16:01 open
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return;
			}
			if (rollOn(1)) { m_ref = double.NaN; return; }
			if (_CmeHolidays.Rules.Get(day).RthCloseEt.HasValue) return;     // F-CAL: early close, no MOC window
			if (tod == Cfg.driftFrom) m_ref = Bars.Close[0];                  // 1m bar closing at 15:30
			if (tod == Cfg.entryAt && !double.IsNaN(m_ref)) m_drift = Bars.Close[0] - m_ref;   // 15:30-15:50 drift, frozen at 15:50
			double tick = tickSize(), drift;
			int side;
			if (Variant == EVariant.ReversionTo1645)
			{
				if (tod != new TimeSpan(16, 0, 0) || double.IsNaN(m_drift)) return;
				drift = m_drift;
				if (Math.Abs(drift) < Cfg.minDriftTicks * tick) return;
				side = -Math.Sign(drift);                                 // fade the 15:30-15:50 drift
			}
			else
			{
				if (tod != Cfg.entryAt || double.IsNaN(m_ref)) return;
				drift = Bars.Close[0] - m_ref;
				if (Math.Abs(drift) < Cfg.minDriftTicks * tick) return;
				side = Math.Sign(drift);
			}
			string why;
			if (!entryAllowed(out why)) return;
			double atr = atr14Of5Minute();
			if (double.IsNaN(atr)) return;
			double r = Math.Max(atr, Cfg.costFloorTicks * tick);               // floor rule
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"TS-03 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			double entry = Bars.Close[0];
			m_stop = entry - side * r;
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "variant", Variant.ToString() }, { "drift", drift.ToString("F2") }, { "atr14of5m", atr.ToString("F2") } };
				journalArm(side > 0 ? "TS03LE" : "TS03SE", entry, m_stop, double.NaN, ctx);
				logSignal("TS-03", side, entry, m_stop, ctx, true);
			}
		}

		/// <summary>ATR(14) of closed Data2 bars. Data2 is the 5-minute stream, so this is the catalogue's ATR5, length 14.</summary>
		private double atr14Of5Minute()
		{
			int n = Cfg.atrLength;
			if (BarsOfData(2).CurrentBar < n + 2) return double.NaN;
			double sum = 0;
			for (int k = 0; k < n; k++)
			{
				DateTime t; double o, h, l, c, v, o1, h1, l1, pc, v1;
				HtfClosedBar(2, k, out t, out o, out h, out l, out c, out v);
				HtfClosedBar(2, k + 1, out t, out o1, out h1, out l1, out pc, out v1);
				sum += Math.Max(h, pc) - Math.Min(l, pc);
			}
			return sum / n;
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
