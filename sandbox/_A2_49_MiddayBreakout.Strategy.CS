// ## 1. Identity
// Catalogue ID: A2-49
// ID mapping: A2-49 → _A2_49_MiddayBreakout.Strategy.CS (class _A2_49_MiddayBreakout). MiddayBreakout shortens the breakout arm of the midday range.
// Name: Kaufman afternoon breakout
// Header version: 1
// Rules version: catalogue v2.7 breakout arm, day-only, as implemented by docs/engines-manual.md §3.4 (example 12)
// Study class: _A2_49_MiddayBreakout
// File: sandbox/_A2_49_MiddayBreakout.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "A2-49 · Midday support/resistance and afternoon breakout". The code is engines-manual example 12.
// Taken from the source: morning range 09:30–12:00 ET, afternoon break of that range, day-only flat at 15:55, disaster stop 1.5R.
// Research assumption: this arm enters with a market order on a close beyond the morning high or low, and reverses while 12:00–15:30. The book's stop at the level ± 1 tick, the SR zone arm, and the overnight hold are not coded. R for the disaster stop is the morning range width.
//
// ## 3. Hypothesis
// Mechanism: the open is more likely than midday to hold the day's extreme, so a break of the morning range in the afternoon tends to close in that direction. Costs on an always-in afternoon are the risk the catalogue names.
// Link: specs/trading-idea-catalogue.md, heading "A2-49 · Midday support/resistance and afternoon breakout (Kaufman TSM)"
//
// ## 4. Trade walkthrough
// Trigger: after 12:00 ET, a closed bar beyond the morning high buys, and a closed bar beyond the morning low sells. A break against an open position reverses it. At most three entries in the afternoon.
// Management: the disaster stop stays at 1.5 × the morning range from the signal close, widened to 14 ticks when that is larger. Nothing else moves it.
// Exit: the stop, a reversal, or a market flatten at 15:55 ET (or five minutes before an early close when that is earlier).
//
// ## 5. Timing
// Signal evaluated: closed bar only (CalcBar returns unless Bars.Status is EBarState.Close), after base.CalcBar.
// Order sent: MarketNextBar A249LE or A249SE. The stop is a Stop order A249LS or A249SS, re-sent while the position is open.
// Can fill: the next bar after the signal close. The stop can fill on a later bar once it is working.
//
// ## 6. Position and re-entry
// Position: flat or one. A break the other way reverses. No scale-in.
// Re-entry: allowed on a later break the same afternoon, up to MaxTradesPerDay = 3.
// Same session after a stop: yes, if a new break prints before 15:30 and the day's entry count allows it.
//
// ## 7. Instruments
// Typical: ES, with NQ, YM and RTY named as cross-market checks. This file is written for @ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 30-minute, 24h Globex, dxFeed continuous unadjusted. The clock is Eastern through the CME session adapter.
// Data2: Not applicable
// Further series: Not applicable
// Warmup: the morning range of the same session (bars that close after 09:30 and at or before 12:00 ET).
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: a morning high and low exist; the bar closes after 12:00 and the entry window allows it; the close is beyond the morning high or low; the side is against the current position; the widened stop fits the 1% risk budget.
// Skip when: the day contains a roll (the range would straddle the splice); the risk budget fails; entryAllowed blocks (before 12:00, at or after 15:30, at or after the flat time, or three entries already).
//
// ## 10. Position sizing and risk
// Size: 1 contract (A249Config.contracts).
// Risk limit: the trade is skipped when 1 × stop distance × BigPointValue exceeds 1% of equity (riskBudgetFraction).
// Size is never shrunk to fit a stop: yes. The stop is widened to 14 ticks and the trade is skipped if that width breaks the budget.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar A249LE (buy) or A249SE (sell short).
// Exit orders: MarketNextBar A249LX / A249SX; Stop A249LS / A249SS.
// Fill assumption: market on the next bar; the stop works from the bar after it is sent.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the disaster stop is re-sent each bar. FlatBy is set at the start of the Globex day, including an earlier flat on a CME early close.
//
// ## 13. Exits
// Hard stop: entry ± max(1.5 × morning range, 14 ticks).
// Target: Not applicable
// Time exit: FlatBy, default 15:55 ET, or RthCloseEt minus 5 minutes when that is earlier.
// Invalidation: a roll marks the day skipped, so no new entry is sent. mustFlatten still runs.
//
// ## 14. Regimes
// Suitable: the afternoon of an RTH session with a completed morning range.
// Unsuitable: a roll day, and the hours outside 12:00–15:30 ET. No volatility gate is coded.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: none. The CME session clock is constructed in the signal.
// Engine settings enabled: RollDetection = Series. NoEntryBefore, NoEntryAfter, FlatBy and MaxTradesPerDay are set in code from A249Config.
//
// ## 16. Pitfalls
// Lookahead: the range uses bars that have already closed, and the order is for the next bar.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. A splice day does not trade the morning range.
// Data quality: on a 30-minute chart the 12:00 bar is the last bar of the range. An early close moves FlatBy. Not yet verified on a chart.
// Other: mustFlatten is false when the position is flat, so the cover order is sent only while short.
//
// ## 17. Parameters
// Input: Arm
// Purpose: which afternoon rule to run
// Default: CloseBreak — market on a close beyond the morning range. This is the previous trade list. Not a fitted optimum.
// Units: Not applicable
// Range: CloseBreak, SrZone, BreakoutStop, Both. Any other value throws in StartCalc.
// Effect: SrZone trades closes in the outer zone. BreakoutStop rests a stop at the morning high or low ± 1 tick. Both runs both.
// Interactions: Hold and Zone. CloseBreak ignores Zone.
// Fixed or research-tuned: fixed default. The other members are the book arms, not a searched optimum.
// Input: Hold
// Purpose: whether the position is flat at 15:55 or held to the next session
// Default: DayFlat — flat at 15:55, or five minutes before an early close. This is the previous trade list.
// Units: Not applicable
// Range: DayFlat, Overnight. Any other value throws in StartCalc.
// Effect: Overnight does not use FlatBy. It exits on the first RTH bar of the next session.
// Interactions: Arm
// Fixed or research-tuned: fixed default. Overnight is the book hold.
// Input: Zone
// Purpose: SR zone width as a fraction of the morning range
// Default: Pct25 — 25 percent, the book core. CloseBreak does not read it, so the default trade list is unchanged.
// Units: fraction of the morning range
// Range: Pct20 = 0.20, Pct25 = 0.25, Pct33 = 0.33. Any other value throws in StartCalc.
// Effect: a wider zone flags more closes as support or resistance
// Interactions: used only by SrZone and Both
// Fixed or research-tuned: fixed at the book core. The other two are the catalogue sweep.
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
// Uncertainty: no run yet; breakout arm only; the stop is measured from the signal close, not the fill; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: A249LE or A249SE
// logSignal tag: A2-49
// Skip / flatten lines: "A2-49" plus "skip: stop" when the budget fails; logSignal with blocked set when entryAllowed refuses.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Book arms are inputs. Default Arm CloseBreak and Hold DayFlat reproduce the previous trade list. On a 30-minute chart the overnight exit is the first RTH bar, whose close is 10:00, not a 09:31 one-minute bar. Midday stays 12:00; the 11:30 and 12:30 clocks are not inputs.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _A2_49_MiddayBreakout : _CustomSignal
	{
		/// <summary>CloseBreak is the current market-on-close-beyond-the-range arm. SrZone is book rules 1-2. BreakoutStop is book rules 3-4 (stop at the level ± 1 tick). Both is the book combination.</summary>
		public enum EArm { CloseBreak, SrZone, BreakoutStop, Both }
		/// <summary>DayFlat exits at 15:55. Overnight holds and exits on the first RTH bar of the next session.</summary>
		public enum ESessionHold { DayFlat, Overnight }
		/// <summary>Zone width for the SR arm, as a fraction of the morning range. The book core is 25 percent. CloseBreak ignores it.</summary>
		public enum EZone { Pct20, Pct25, Pct33 }

		[Input] public EArm Arm { get; set; }
		[Input] public ESessionHold Hold { get; set; }
		[Input] public EZone Zone { get; set; }

		protected sealed class A249Config
		{
			public int contracts = 1;
			public TimeSpan rthOpen = new TimeSpan(9, 30, 0), midday = new TimeSpan(12, 0, 0);
			public TimeSpan lastEntry = new TimeSpan(15, 30, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public double disasterR = 1.5;
			public int maxEntries = 3;              // the first break plus at most two reversals per afternoon
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A249Config Cfg { get; } = new A249Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_leLvl, m_seLvl, m_lxStp, m_sxStp;
		private _CmeSessionClock m_clock;
		private DateTime m_day = DateTime.MinValue;
		private double m_hi = double.NaN, m_lo = double.NaN, m_stop = double.NaN;
		private bool m_skipDay, m_exitNextRth;

		public _A2_49_MiddayBreakout(object _ctx) : base(_ctx) { Arm = EArm.CloseBreak; Hold = ESessionHold.DayFlat; Zone = EZone.Pct25; }

		private double zoneFrac() { return Zone == EZone.Pct20 ? 0.20 : Zone == EZone.Pct33 ? 0.33 : 0.25; }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			// CS-7 primitives: protected properties, set in code (never [Input])
			NoEntryBefore = Cfg.midday; NoEntryAfter = Cfg.lastEntry; FlatBy = Cfg.flat; MaxTradesPerDay = Cfg.maxEntries;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A249LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A249SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A249LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A249SX", EOrderAction.BuyToCover));
			m_leLvl = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "A249LB", EOrderAction.Buy));
			m_seLvl = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "A249SB", EOrderAction.SellShort));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A249LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A249SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(EArm), Arm)) throw new ArgumentException("_A2_49_MiddayBreakout: Arm is not a defined EArm");
			if (!Enum.IsDefined(typeof(ESessionHold), Hold)) throw new ArgumentException("_A2_49_MiddayBreakout: Hold is not a defined ESessionHold");
			if (!Enum.IsDefined(typeof(EZone), Zone)) throw new ArgumentException("_A2_49_MiddayBreakout: Zone is not a defined EZone");
			m_clock = null;
			base.StartCalc();
			m_day = DateTime.MinValue; m_hi = m_lo = m_stop = double.NaN; m_skipDay = false; m_exitNextRth = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_day)
			{
				bool held = StrategyInfo.MarketPosition != 0;
				m_day = day; m_hi = m_lo = double.NaN; m_skipDay = false;
				FlatBy = Hold == ESessionHold.Overnight ? (TimeSpan?)null : flatAt(day);
				m_exitNextRth = Hold == ESessionHold.Overnight && held;
			}
			if (rollOn(1)) m_skipDay = true;                        // the morning range would straddle the splice
			TimeSpan tod = sessionClock(Bars.Time[0]);
			int pos = StrategyInfo.MarketPosition;
			if (m_exitNextRth && isRthBar(Bars.Time[0]) && tod > Cfg.rthOpen)
			{
				if (pos > 0) m_lx.Send(); else if (pos < 0) m_sx.Send();
				m_exitNextRth = false;
				return;
			}
			if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }

			// morning range: RTH bars that close in (09:30, 12:00]
			if (tod > Cfg.rthOpen && tod <= Cfg.midday && isRthBar(Bars.Time[0]))
			{
				m_hi = double.IsNaN(m_hi) ? Bars.High[0] : Math.Max(m_hi, Bars.High[0]);
				m_lo = double.IsNaN(m_lo) ? Bars.Low[0] : Math.Min(m_lo, Bars.Low[0]);
			}
			if (pos > 0) m_lxStp.Send(m_stop); else if (pos < 0) m_sxStp.Send(m_stop);
			if (m_skipDay || double.IsNaN(m_hi) || tod <= Cfg.midday) return;

			double tick = tickSize();
			if (Arm == EArm.CloseBreak) closeBreak(pos, tick);
			else bookArm(pos, tick);
		}

		private void closeBreak(int pos, double tick)
		{
			int side = Bars.Close[0] > m_hi ? +1 : Bars.Close[0] < m_lo ? -1 : 0;
			if (side == 0 || side == Math.Sign(pos)) return;        // SAR: only a break against the current position acts
			string why;
			if (!entryAllowed(out why)) { if (journalOn()) logSignal("A2-49", side, Bars.Close[0], double.NaN, new Dictionary<string, string> { { "blocked", why } }, false); return; }
			double entry = Bars.Close[0];
			double r = Math.Max(Cfg.disasterR * (m_hi - m_lo), Cfg.costFloorTicks * tick);   // floor rule
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-49 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = entry - side * r;
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);   // reverses an open opposite position
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "mHigh", m_hi.ToString("F2") }, { "mLow", m_lo.ToString("F2") }, { "tradesToday", TradesToday.ToString() } };
				journalArm(side > 0 ? "A249LE" : "A249SE", entry, m_stop, double.NaN, ctx);
				logSignal("A2-49", side, entry, m_stop, ctx, true);
			}
		}

		/// <summary>Book SR zone (close in the outer fraction of the morning range) and breakout stops at ML-1 / MH+1 tick. A bar that trades through the level is a breakout, not an SR close.</summary>
		private void bookArm(int pos, double tick)
		{
			double width = m_hi - m_lo;
			if (!(width > 0)) return;
			double frac = zoneFrac();
			double lower = m_lo + frac * width, upper = m_hi - frac * width;
			bool brokeLow = Bars.Low[0] < m_lo, brokeHigh = Bars.High[0] > m_hi;
			bool sr = Arm == EArm.SrZone || Arm == EArm.Both;
			bool bo = Arm == EArm.BreakoutStop || Arm == EArm.Both;
			int side = 0;
			bool stopEntry = false;
			double entry = Bars.Close[0];
			if (bo && (brokeLow || brokeHigh))
			{
				if (brokeHigh && pos <= 0) { side = +1; stopEntry = true; entry = m_hi + tick; }
				else if (brokeLow && pos >= 0) { side = -1; stopEntry = true; entry = m_lo - tick; }
			}
			else if (sr)
			{
				if (pos <= 0 && !brokeLow && Bars.Close[0] <= lower) { side = +1; entry = Bars.Close[0]; }
				else if (pos >= 0 && Bars.Close[0] >= upper) { side = -1; entry = Bars.Close[0]; }
			}
			if (side == 0 || side == Math.Sign(pos)) return;
			string why;
			if (!entryAllowed(out why)) { if (journalOn()) logSignal("A2-49", side, entry, double.NaN, new Dictionary<string, string> { { "blocked", why }, { "arm", Arm.ToString() } }, false); return; }
			double r = Math.Max(Cfg.disasterR * width, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-49 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = entry - side * r;
			if (stopEntry) { if (side > 0) m_leLvl.Send(entry, Cfg.contracts); else m_seLvl.Send(entry, Cfg.contracts); }
			else if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "arm", Arm.ToString() }, { "mHigh", m_hi.ToString("F2") }, { "mLow", m_lo.ToString("F2") }, { "zone", frac.ToString("F2") } };
				journalArm(side > 0 ? (stopEntry ? "A249LB" : "A249LE") : (stopEntry ? "A249SB" : "A249SE"), entry, m_stop, double.NaN, ctx);
				logSignal("A2-49", side, entry, m_stop, ctx, true);
			}
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
