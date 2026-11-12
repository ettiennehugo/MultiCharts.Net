// ## 1. Identity
// Catalogue ID: DV-15
// ID mapping: DV-15 → _DV_15_EmaVwapPullback.Strategy.CS (class _DV_15_EmaVwapPullback). EmaVwapPullback names the EMA20 pullback gated by VWAP slope.
// Name: EMA VWAP pullback
// Header version: 1
// Rules version: catalogue v2.7 session-VWAP arm, as implemented by docs/engines-manual.md §3.9 (example 20)
// Study class: _DV_15_EmaVwapPullback
// File: sandbox/_DV_15_EmaVwapPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "DV-15 · EMA20 pullback gated by VWAP slope". The code is engines-manual example 20.
// Taken from the source: side is the sign of close minus EMA20 held for at least three closed bars; a bar that trades through the EMA, with VWAP slope of the same sign, sends a limit at the EMA. Bracket 1:1.
// Research assumption: the VWAP is the session slot, s = 0.05 and f = 0.15. Those two numbers are on the catalogue grid. The weekly VWAP and the other grid points are not coded. Slope is compared with ATR(14) of the 30-minute bars. The stop and target are sent on the bar after the fill, so the fill bar is unprotected.
//
// ## 3. Hypothesis
// Mechanism: a pullback to a rising or falling EMA20 is a trend entry, and the VWAP slope keeps the trade on the side where the session's average participant is being paid. Dale's quoted test is a tiny in-sample FX sample. This file is the ES port of the coding rules.
// Link: specs/trading-idea-catalogue.md, heading "DV-15 · EMA20 pullback gated by VWAP slope"
//
// ## 4. Trade walkthrough
// Trigger: during RTH from 10:00 through 15:00 ET, after the side has held for three bars before this one, a 30-minute bar trades through the EMA20, the eight-bar session-VWAP slope has that sign and is at least 0.05 × ATR(14), and the close is on the same side of VWAP. Send a limit at the EMA.
// Management: after the fill, a 1:1 bracket of f × ATR20d, floored at 14 ticks, is sent from the next closed bar.
// Exit: the stop, the target, or a market flatten at 15:55 ET.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. The side used is the side before this bar.
// Order sent: Limit DV15LE or DV15SE at the rounded EMA. Stop DV15LS / DV15SS and target Limit DV15LT / DV15ST are sent once the position is open. Flat is MarketNextBar DV15LX / DV15SX.
// Can fill: the limit can fill on a later bar. The bracket is not working on the fill bar.
//
// ## 6. Position and re-entry
// Position: flat or one. No scale-in.
// Re-entry: a later touch can enter after the position is flat. There is no one-trade-per-day cap.
// Same session after a stop: yes, if a new touch passes the filters.
//
// ## 7. Instruments
// Typical: 6E first, then ES and NQ, on 30-minute bars. This file is the ES port.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 30-minute, 24h Globex, dxFeed continuous unadjusted. Session VWAP is slot 1 on Data1.
// Data2: Not applicable
// Further series: Not applicable. ATR20d is M-DAILY aggregated from Data1.
// Warmup: 20 closes for the EMA seed path (the code waits emaLength bars) and a valid daily ATR20d. The slope needs eight stored VWAP values.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; RTH; clock from 10:00 through 15:00; EMA warmed up; not a roll bar; at least eight VWAP samples; daily context valid; prior side held for three bars; this bar's range contains the EMA; slope sign matches; absolute slope at least 0.05 × ATR(14); close on the same side of VWAP; the bracket fits the 1% budget.
// Skip when: the bracket is not positive or breaks the budget.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the bracket distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: Limit DV15LE or DV15SE.
// Exit orders: Stop DV15LS / DV15SS; Limit DV15LT / DV15ST; MarketNextBar DV15LX / DV15SX.
// Fill assumption: limit on a later bar. The fill bar is unprotected.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the stop and the target are re-sent at the prices stored when the limit was sent. They are not moved.
//
// ## 13. Exits
// Hard stop: entry ± max(0.15 × ATR20d, 14 ticks).
// Target: the same distance the other way. The bracket is 1:1.
// Time exit: FlatBy 15:55 ET. This arm does not move FlatBy on an early close.
// Invalidation: a roll bar does not send a new limit.
//
// ## 14. Regimes
// Suitable: a 30-minute trend whose EMA side and session-VWAP slope agree.
// Unsuitable: a flat VWAP (slope under the threshold) and a roll bar.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: M-DAILY (NeedsDaily).
// Engine settings enabled: RollDetection = Series. Vwap1Type = Session and Vwap1DataStream = 1, set in code. FlatBy = 15:55.
//
// ## 16. Pitfalls
// Lookahead: the side is the prior run, the slope uses VWAP values already stored, and the limit works forward.
// Repaint: the EMA is a running value updated on the closed bar. It is not recomputed from the future.
// Survivorship: Not applicable
// Rolls: Series. A roll bar does not enter. ATR20d reseeds after a roll.
// Data quality: which VWAP Dale meant is not stated in the extract. This arm uses the session slot. Not yet verified on a chart.
// Other: the fill bar is unprotected.
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
// Uncertainty: no run yet; session VWAP and one grid cell only; the fill bar is unprotected; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: Not applicable. This arm does not call journalArm.
// logSignal tag: DV-15
// Skip / flatten lines: "DV-15" plus "skip: bracket" when the budget fails.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Uncoded catalogue cells (weekly VWAP, s = 0.15, f = 0.10) are an owner question, not this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _DV_15_EmaVwapPullback : _VolumeProfileFuturesBase
	{
		protected sealed class Dv15Config
		{
			public int contracts = 1, emaLength = 20, sideBars = 3, slopeBars = 8, atrLength = 14;
			public double slopeAtr = 0.05, bracketAtr20d = 0.15;
			public TimeSpan firstEntry = new TimeSpan(10, 0, 0), lastEntry = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Dv15Config Cfg { get; } = new Dv15Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private double m_ema = double.NaN, m_stop, m_target;
		private int m_sideRun, m_side, m_emaBars;
		private readonly List<double> m_vwap = new List<double>();   // closed-bar VWAP values, newest last (session-reset aware)
		private DateTime m_vwapDay = DateTime.MinValue;

		public _DV_15_EmaVwapPullback(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;     // [Input]s on the base: set in code
			FlatBy = Cfg.flat;
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV15LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV15SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV15LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV15SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV15LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV15SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV15LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV15ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			NeedsDaily = true;                                      // before base.StartCalc (M-DAILY is built there)
			base.StartCalc();
			m_ema = double.NaN; m_emaBars = 0; m_sideRun = 0; m_side = 0; m_vwap.Clear(); m_vwapDay = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			double c = Bars.Close[0], tick = tickSize();
			double k = 2.0 / (Cfg.emaLength + 1);
			m_ema = double.IsNaN(m_ema) ? c : m_ema + k * (c - m_ema); m_emaBars++;
			int prevSide = m_side, prevRun = m_sideRun;              // side state up to the previous bar
			int s = Math.Sign(c - m_ema);
			m_sideRun = s != 0 && s == m_side ? m_sideRun + 1 : 1; m_side = s;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_vwapDay) { m_vwapDay = day; m_vwap.Clear(); }
			if (Vwap1Active) m_vwap.Add(CurrentVwap1);

			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return;
			}
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (!IsRth() || tod < Cfg.firstEntry || tod > Cfg.lastEntry || m_emaBars < Cfg.emaLength || rollOn(1)) return;
			if (m_vwap.Count <= Cfg.slopeBars || DailyContext == null || !DailyContext.Valid) return;
			int side = prevRun >= Cfg.sideBars ? prevSide : 0;                                      // side established BEFORE this bar
			if (side == 0 || !(Bars.Low[0] <= m_ema && Bars.High[0] >= m_ema)) return;                // touch
			double vw = m_vwap[m_vwap.Count - 1], slope = vw - m_vwap[m_vwap.Count - 1 - Cfg.slopeBars];
			double atr = averageTrueRange(Bars, Cfg.atrLength);
			if (!(atr > 0) || Math.Sign(slope) != side || Math.Abs(slope) < Cfg.slopeAtr * atr || Math.Sign(c - vw) != side) return;
			double d = Math.Max(Cfg.bracketAtr20d * DailyContext.Atr20d, Cfg.costFloorTicks * tick);
			if (!(d > 0) || overRiskBudget(d)) { if (journalOn()) trace(ETraceLevel.Journal, $"DV-15 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: bracket {d / tick:F0} ticks breaches risk budget"); return; }
			double entry = Math.Round(m_ema / tick) * tick;
			m_stop = entry - side * d; m_target = entry + side * d;      // 1:1 bracket
			if (side > 0) m_leLmt.Send(entry, Cfg.contracts); else m_seLmt.Send(entry, Cfg.contracts);
			if (journalOn()) logSignal("DV-15", side, entry, m_stop, new Dictionary<string, string> { { "vwap", vw.ToString("F2") }, { "slope", slope.ToString("F2") } }, true);
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
