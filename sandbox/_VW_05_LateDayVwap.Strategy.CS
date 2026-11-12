// ## 1. Identity
// Catalogue ID: VW-05
// ID mapping: VW-05 → _VW_05_LateDayVwap.Strategy.CS (class _VW_05_LateDayVwap). LateDayVwap names the late-day VWAP continuation.
// Name: Late-day VWAP
// Header version: 1
// Rules version: catalogue v2.7 v1 15:00 arm, as implemented by docs/engines-manual.md §3.7 (example 14)
// Study class: _VW_05_LateDayVwap
// File: sandbox/_VW_05_LateDayVwap.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "VW-05 · Late-day continuation". The code is engines-manual example 14.
// Taken from the source: at 15:00, if the sign of price minus the prior RTH close equals the sign of price minus session VWAP, enter that way. Exit at 15:55. Stop 1 × ATR of the 30-minute stream.
// Research assumption: this is the 15:00 arm. The catalogue sweep also names 14:30 and 15:30, and the merged A2-06 rule (15:29, slope, a minimum move) is not coded. ATR length is 14 on the 30-minute bars. A roll day does not trade, because the prior close is the old contract.
//
// ## 3. Hypothesis
// Mechanism: the rest of the day and the VWAP side agree when late rebalancing and VWAP-benchmarked flow are pushing the same way into the close. Published intraday-momentum results were fragile out of sample. This file does not claim they survived.
// Link: specs/trading-idea-catalogue.md, heading "VW-05 · Late-day continuation: VWAP side × rest-of-day return"
//
// ## 4. Trade walkthrough
// Trigger: the 1-minute bar that closes at 15:00 ET, when the close is on the same side of the prior RTH close and of the session VWAP.
// Management: the stop stays one ATR(14) of the 30-minute bars away from the signal close, floored at 14 ticks.
// Exit: the stop, or a market flatten at 15:55 ET (or five minutes before an early close when that is earlier).
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. ATR uses closed Data2 bars. Session VWAP is the closed-bar slot.
// Order sent: MarketNextBar VW05LE or VW05SE. Stop VW05LS or VW05SS is sent while the position is open. Flat is MarketNextBar VW05LX or VW05SX.
// Can fill: the next bar. The stop starts after the fill bar.
//
// ## 6. Position and re-entry
// Position: flat or one. No scale-in.
// Re-entry: one decision per Globex day. The day is marked done even when the signs disagree.
// Same session after a stop: no. The entry clock has passed.
//
// ## 7. Instruments
// Typical: ES, NQ and ZN. This file is written for @ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted. Session VWAP is slot 1 on Data1.
// Data2: @ES 30-minute, same symbol and session. HtfStreams is { 2 }.
// Further series: Not applicable
// Warmup: a prior RTH close on the session object, an active session VWAP, and 16 closed 30-minute bars for ATR.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; the bar closes at 15:00 and before the flat; the day has not been decided; not a roll day; the prior RTH close and the session VWAP are available; the two signs are equal and not zero; ATR is available; the widened stop fits the 1% budget.
// Skip when: the signs disagree or one is zero (logSignal false); the budget fails; a roll day (no line, the day is marked done).
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the stop distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar VW05LE or VW05SE.
// Exit orders: Stop VW05LS / VW05SS; MarketNextBar VW05LX / VW05SX.
// Fill assumption: next bar's open. The stop starts on the bar after the fill.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the stop is re-sent at the price stored at the signal.
//
// ## 13. Exits
// Hard stop: signal close ± max(ATR(14) of the 30-minute bars, 14 ticks).
// Target: Not applicable
// Time exit: 15:55 ET, earlier on a CME early close.
// Invalidation: a roll day does not enter. A zero sign does not enter.
//
// ## 14. Regimes
// Suitable: a day whose 15:00 price agrees with both the rest-of-day move and the session VWAP.
// Unsuitable: a mixed sign, a flat comparison, and a roll day. The A2-06 slope filter is not applied.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: M-MTF on Data2. The session VWAP slot is on the base.
// Engine settings enabled: RollDetection = Series. Vwap1Type = Session and Vwap1DataStream = 1, set in code. StartCalc throws if Vwap1Type is not Session. HtfStreams = { 2 }.
//
// ## 16. Pitfalls
// Lookahead: the prior close is the session object's prior RTH close, VWAP is the closed slot, and ATR uses HtfClosedBar.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. The splice day is marked done and does not trade.
// Data quality: the entry test is an exact 15:00 clock match. A missing bar skips the day. Not yet verified on a chart.
// Other: the v1 kill in the catalogue applies to this 15:00 arm. A2-06 is a different rule and is not this file.
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
// Uncertainty: no run yet; the 15:00 arm only; ATR length 14 on the 30-minute stream; the fill bar is unprotected; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: VW05LE or VW05SE
// logSignal tag: VW-05
// Skip / flatten lines: logSignal false when the signs disagree; "VW-05" plus "skip: stop" when the budget fails.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class, its constructor, and the ArgumentException text that names the class. A _TestNN slot renames those to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Uncoded catalogue arms (entry at 14:30 or 15:30, and the A2-06 15:29 rule) are an owner question, not this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _VW_05_LateDayVwap : _VolumeProfileFuturesBase
	{
		protected sealed class Vw05Config
		{
			public int contracts = 1;
			public TimeSpan entryAt = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public int atrLength = 14;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Vw05Config Cfg { get; } = new Vw05Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp;
		private double m_stop = double.NaN;
		private DateTime m_tradedDay = DateTime.MinValue;

		public _VW_05_LateDayVwap(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			// VWAP slot 1 = session VWAP on Data1. These VP-base settings are [Input]s on main: set them in code AND do not edit them in
			// the Format Signal dialog (see the F-VWAP pitfalls).
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "VW05LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "VW05SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "VW05LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "VW05SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "VW05LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "VW05SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2 };
			base.StartCalc();
			if (Vwap1Type != EVwapType.Session) throw new ArgumentException("_VW_05_LateDayVwap: Vwap1Type must be Session");
			m_stop = double.NaN; m_tradedDay = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay, flat = flatAt(day);
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return;
			}
			if (rollOn(1)) { m_tradedDay = day; return; }                         // prior close is the old contract on the splice day
			if (tod != Cfg.entryAt || m_tradedDay == day || tod >= flat) return;
			SessionStats s = Session;
			if (s == null || s.priorRthClose <= 0 || !Vwap1Active) return;
			double px = Bars.Close[0], vwap = CurrentVwap1;
			if (vwap <= 0) return;
			int a = Math.Sign(px - s.priorRthClose), b = Math.Sign(px - vwap);
			m_tradedDay = day;
			if (a == 0 || a != b) { if (journalOn()) logSignal("VW-05", a, px, double.NaN, new Dictionary<string, string> { { "vwapSide", b.ToString() } }, false); return; }
			double tick = tickSize(), atr = atr30();
			if (double.IsNaN(atr)) return;
			double r = Math.Max(atr, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"VW-05 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = px - a * r;
			if (a > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "vwap", vwap.ToString("F2") }, { "priorClose", s.priorRthClose.ToString("F2") }, { "atr30", atr.ToString("F2") } };
				journalArm(a > 0 ? "VW05LE" : "VW05SE", px, m_stop, double.NaN, ctx);
				logSignal("VW-05", a, px, m_stop, ctx, true);
			}
		}

		private double atr30()
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

		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
