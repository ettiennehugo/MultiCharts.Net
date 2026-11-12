// ## 1. Identity
// Catalogue ID: AV-08
// ID mapping: AV-08 → _AV_08_PeriodAvwapLayer.Strategy.CS (class _AV_08_PeriodAvwapLayer). PeriodAvwapLayer names the week and month VWAP layer.
// Name: Period AVWAP layer
// Header version: 1
// Rules version: catalogue v2.7 long WTD+MTD layer, as implemented by docs/engines-manual.md §3.6 (example 3)
// Study class: _AV_08_PeriodAvwapLayer
// File: sandbox/_AV_08_PeriodAvwapLayer.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "AV-08 · Week-open / month-open / quarter-open AVWAP as regime layer". The code is engines-manual example 3.
// Taken from the source: above the week VWAP and above the month VWAP allows a long. The layer is the filter. The host is not a catalogue rule.
// Research assumption: the host is a close above the prior 20-bar high between 09:45 and 15:00, with a 16-tick stop, so the layer has events to filter. The short side, the WTD-only arm and the MTD+QTD arm are not coded. A week or month that contains a roll turns the layer off until the next period, because the Week and Month VWAP slots do not reset at a roll.
//
// ## 3. Hypothesis
// Mechanism: week and month VWAP say which side has been paying up this period. The catalogue uses that only as a filter on other hosts. A result on the illustrative host is not a test of AV-08.
// Link: specs/trading-idea-catalogue.md, heading "AV-08 · Week-open / month-open / quarter-open AVWAP as regime layer"
//
// ## 4. Trade walkthrough
// Trigger: a 1-minute bar closes above the prior 20-bar high, inside 09:45–15:00 ET, and the close is above both the week VWAP and the month VWAP, and neither period contains a roll.
// Management: the stop stays 16 ticks under the signal close. Nothing else moves it.
// Exit: the stop, or a market flatten at 15:55 ET (or five minutes before an early close when that is earlier).
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. The VWAP slots include that closed bar.
// Order sent: MarketNextBar AV08LE. The stop is sent with it (AV08LS). The flat is MarketNextBar AV08LX.
// Can fill: the next bar. The stop is working on the fill bar because it is sent with the entry.
//
// ## 6. Position and re-entry
// Position: flat or one long. No scale-in and no short.
// Re-entry: a later host bar can enter again after the position is flat. There is no one-trade-per-day cap.
// Same session after a stop: yes, if a new host bar passes the layer.
//
// ## 7. Instruments
// Typical: a layer on any host. This file is written for @ES so the layer can be journaled.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted.
// Data2: Not applicable
// Further series: Not applicable. Week and month VWAP are slots on Data1.
// Warmup: 20 bars for the host high, plus a week and a month of VWAP before those slots are active.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; clock inside 09:45–15:00 and before the flat; close above the prior 20 highs; both VWAP slots active; close above both; the week and the month have not contained a roll; the 16-tick stop fits the 1% budget.
// Skip when: the layer is false (journaled with logSignal false); the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when 1 × 16 ticks × BigPointValue exceeds 1% of equity. On ES that is $200, so equity under $20,000 skips every trade.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar AV08LE.
// Exit orders: Stop AV08LS; MarketNextBar AV08LX.
// Fill assumption: next bar's open. The stop is sent on the signal bar.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the stop is re-sent. The layer is not re-checked.
//
// ## 13. Exits
// Hard stop: signal close minus 16 ticks.
// Target: Not applicable
// Time exit: 15:55 ET, earlier on a CME early close.
// Invalidation: a roll makes the current week and month unclean, so later host bars do not enter.
//
// ## 14. Regimes
// Suitable: a week and month with price above both period VWAPs and no roll inside either period.
// Unsuitable: below either VWAP, a roll still inside the week or month, and the short side (not coded).
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: the base VWAP slots. No separate engine is switched on.
// Engine settings enabled: RollDetection = Series. Vwap1Type = Week and Vwap2Type = Month on Data1, set in code. StartCalc throws if the dialog changes them. Vwap3Type = Disabled.
//
// ## 16. Pitfalls
// Lookahead: the host high excludes the current bar (High[1] through High[20]). VWAP is the closed-bar slot.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: the Week and Month slots do not reset at a roll. This file turns the layer off for the rest of that week and month. The other reading, re-anchor at the roll, is not coded.
// Data quality: the week slot starts at the week's first bar. On a Globex chart that is Sunday 18:00, the same bar as the A2-07 anchor. Not yet verified on a chart.
// Other: the host is illustrative. Do not read its trades as an AV-08 result. G0-d still applies to an AVWAP level when this layer is later put on a real host.
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
// Uncertainty: no run yet; the host is illustrative; one layer arm; the roll-off choice; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: AV08LE
// logSignal tag: AV-08
// Skip / flatten lines: logSignal with the signal flag false when the layer blocks a host bar; "AV-08" plus "skip: risk budget" when the 16-tick stop is too wide for equity.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class, its constructor, and the ArgumentException text that names the class. A _TestNN slot renames those to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Uncoded catalogue arms (short side, WTD only, MTD+QTD) and re-anchoring at the roll are an owner question, not this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _AV_08_PeriodAvwapLayer : _VolumeProfileFuturesBase
	{
		protected sealed class Av08Config
		{
			public int contracts = 1;
			public int hostLookback = 20;                       // illustrative host: close above the prior 20-bar high
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);   // early-close days: flat at RthCloseEt - 5 min
			public double stopTicks = 16;                       // illustrative host stop (>= the ES cost floor of 14 ticks)
			public double riskBudgetFraction = 0.01;            // Ettienne, 2026-10-23: skip if q x stop x $50 > 1% of equity (16 ticks, 1 ct = $200: equity >= $20,000)
		}
		protected Av08Config Cfg { get; } = new Av08Config();

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private DateTime m_lastRollDay = DateTime.MinValue;
		private double m_stop = double.NaN;

		public _AV_08_PeriodAvwapLayer(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "AV08LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV08LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "AV08LS", EOrderAction.Sell, OrderExit.FromAll));
			// The VWAP slots are [Input]s of the VP base: these are defaults, the Format Signal dialog can override them.
			Vwap1Type = EVwapType.Week;  Vwap1DataStream = 1;   // WTD: resets on the first bar of a new Monday-start Globex week
			Vwap2Type = EVwapType.Month; Vwap2DataStream = 1;   // MTD
			Vwap3Type = EVwapType.Disabled;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (Vwap1Type != EVwapType.Week || Vwap2Type != EVwapType.Month || Vwap1DataStream != 1 || Vwap2DataStream != 1)
				throw new ArgumentException("_AV_08_PeriodAvwapLayer: needs Vwap1Type = Week and Vwap2Type = Month on Data1");
			m_lastRollDay = DateTime.MinValue; m_stop = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime t = Bars.Time[0], day = GlobexDate(t);
			TimeSpan tod = ToEastern(t).TimeOfDay;
			if (rollOn(1)) m_lastRollDay = day;
			int pos = StrategyInfo.MarketPosition;
			TimeSpan flat = flatAt(day);

			if (pos > 0)
			{
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) m_lx.Send();
				else if (!double.IsNaN(m_stop)) m_lxStp.Send(m_stop);
				return;
			}
			if (tod < Cfg.from || tod >= Cfg.to || tod >= flat || Bars.CurrentBar <= Cfg.hostLookback + 1) return;

			// --- the AV-08 layer (closed bar: the VWAP slots include this bar) ---
			double wtd = CurrentVwap1, mtd = CurrentVwap2, c = Bars.Close[0];
			DateTime weekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));   // Monday of the Globex week
			DateTime monthStart = new DateTime(day.Year, day.Month, 1);
			// Week/Month VWAPs do NOT reset at a roll. CODING CHOICE: the layer is off for the rest of a week/month that holds a roll
			// (the catalogue says multi-day anchors reset at rolls; the alternative is to re-anchor the VWAPs at rollOn(1))
			bool wtdClean = m_lastRollDay < weekStart, mtdClean = m_lastRollDay < monthStart;
			bool layerLong = Vwap1Active && Vwap2Active && wtdClean && mtdClean && c > wtd && c > mtd;

			// --- illustrative host event ---
			double hh = double.MinValue;
			for (int i = 1; i <= Cfg.hostLookback; i++) hh = Math.Max(hh, Bars.High[i]);
			if (!(c > hh)) return;

			var ctx = new Dictionary<string, string> { { "wtd", wtd.ToString("F2") }, { "mtd", mtd.ToString("F2") },
				{ "wtdClean", wtdClean.ToString() }, { "mtdClean", mtdClean.ToString() } };
			if (!layerLong) { if (journalOn()) logSignal("AV-08", +1, c, double.NaN, ctx, false); return; }
			m_stop = c - Cfg.stopTicks * tickSize();
			if (Cfg.contracts * (c - m_stop) * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction))
			{
				if (journalOn()) { trace(ETraceLevel.Journal, $"AV-08 {t:yyyy-MM-dd HH:mm} skip: risk budget {Cfg.riskBudgetFraction:P2}"); logSignal("AV-08", +1, c, m_stop, ctx, false); }
				m_stop = double.NaN; return;
			}
			if (journalOn()) { journalArm("AV08LE", c, m_stop, double.NaN, ctx); logSignal("AV-08", +1, c, m_stop, ctx, true); }
			m_le.Send(Cfg.contracts);
			m_lxStp.Send(m_stop);
		}

		/// <summary>Flat time for a Globex day: Cfg.flat, or on a CME early-close day RthCloseEt - halfDayExitBefore if earlier
		/// (_CmeHolidays.Rules.Get(day).RthCloseEt; same rule as A263Config.halfDayExitBefore).</summary>
		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
