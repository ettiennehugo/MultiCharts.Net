// ## 1. Identity
// Catalogue ID: TS-01
// ID mapping: TS-01 → _TS_01_EuropeanOpenDrift.Strategy.CS (class _TS_01_EuropeanOpenDrift). EuropeanOpenDrift names the 01:30–04:30 ET drift. This file used to be named TS-02; it implements TS-01.
// Name: European-open drift
// Header version: 1
// Rules version: catalogue v2.7, as implemented by docs/engines-manual.md §3.2 (example 2)
// Study class: _TS_01_EuropeanOpenDrift
// File: sandbox/_TS_01_EuropeanOpenDrift.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "TS-01 · European-open overnight drift". The code is engines-manual example 2.
// Taken from the source: buy at the window start and sell at the window end, every trading day. The sweep is two windows times an event filter. A disaster stop sits at 2 × ATR60m.
// Research assumption: ATR60m length is 14, a simple average of true range. The catalogue does not give the length. RollDetection stays off, so an ATR window can include a roll gap. The catalogue kill is an MES cost. This file is written for @ES and does not change fees.
//
// ## 3. Hypothesis
// Mechanism: inventory taken at the US close is unwound when European liquidity opens, which leaves a small positive drift in the hours around that open. The event filter skips the night before a tier-1 08:30 release.
// Link: specs/trading-idea-catalogue.md, heading "TS-01 · European-open overnight drift (long MES 01:30–04:30 ET)"
//
// ## 4. Trade walkthrough
// Trigger: on the first closed 1-minute bar at or after one minute before the window start, and before the window end, buy if the day is not a CME holiday, halt or NoRth day, and the event filter (when on) does not block.
// Management: the disaster stop is re-sent every bar. It is 2 × ATR60m under the signal close, widened to 14 ticks when that is larger.
// Exit: a market flatten on the first bar at or after the window end and before 17:00 ET, or the disaster stop.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. ATR60m is the simple average of the last 14 closed Data2 bars.
// Order sent: MarketNextBar TS01LE. The stop TS01DS is sent with the entry and re-sent while long. The exit is MarketNextBar TS01LX.
// Can fill: the next bar. Sending on the bar one minute before the window means the usual fill is the window-start bar. A missing bar delays the entry; it does not skip the night. The stop protects the entry bar because it is sent with the entry.
//
// ## 6. Position and re-entry
// Position: flat or one long. No scale-in and no short.
// Re-entry: one attempt per Globex day. m_doneDay blocks a second send.
// Same session after a stop: no. The day is already marked done.
//
// ## 7. Instruments
// Typical: MES and MNQ, 30-minute or 60-minute. This file is @ES 1-minute with a 60-minute stream, so the cost unit is ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted.
// Data2: @ES 60-minute, same symbol and session. HtfStreams is { 2 }.
// Further series: Not applicable
// Warmup: 15 closed 60-minute bars before ATR60m is available (n + 1).
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; the Globex day has not been attempted; the bar is inside the send window; the day is not Closed, NoRth or a halt; ATR60m is positive; the widened stop fits the 1% budget. When the event filter is on, M-EVENT must be enabled, coverage must not be Unknown, and the day must not have a tier-1 event at 08:30 ET.
// Skip when: the holiday, coverage, event, warmup or budget check fails. A window with no closed bar is journaled and not traded. A disabled event engine fails closed when the filter is on.
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the disaster distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar TS01LE.
// Exit orders: MarketNextBar TS01LX; Stop TS01DS.
// Fill assumption: next bar's open. The stop is sent on the signal bar.
// Costs: the stop floor is InstrumentProfile.costFloorTicks for Data1, and the journal records profile.cost (the preset cost key, for example Future:ES). This file does not set a fee or a slippage number. MultiCharts commission and slippage stay the chart settings. Not yet verified
//
// ## 12. Trade management
// While open: the disaster stop is re-sent at the price stored at the signal. It is not trailed.
//
// ## 13. Exits
// Hard stop: signal close minus max(2 × ATR60m, 14 ticks). This is a disaster stop, not a trade stop in the catalogue's wording.
// Target: Not applicable
// Time exit: the first closed bar at or after the window end and before 17:00 ET.
// Invalidation: a holiday, halt, NoRth day, coverage hole or 08:30 tier-1 event (filter on) skips the night before the order is sent.
//
// ## 14. Regimes
// Suitable: an ordinary Globex night. The narrow window is the 02:00–03:00 ET cell.
// Unsuitable: CME holidays and halts. With the filter on, the night before a tier-1 08:30 release, and any day whose macro coverage is Unknown.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: M-MTF on Data2. M-EVENT (Macro, tier T1) only when Variant selects the event filter. It is enabled in StartCalc before base.StartCalc.
// Engine settings enabled: RollDetection is left off. HtfStreams = { 2 }. EventConfig.minTier and EventConfig.classes are set in Create; EventConfig.enabled follows the variant.
//
// ## 16. Pitfalls
// Lookahead: ATR uses HtfClosedBar, so the 60-minute bar has closed at or before the 1-minute decision. The event list is as-of the bar.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: detection is off. An ATR60m window can include the roll gap.
// Data quality: 01:30 ET belongs to the Globex date of the following RTH session, which is the same date as an 08:30 release. GDP and PCE coverage holes are what CoverageBlocks is for. Not yet verified on a chart.
// Other: if the send window passes with no closed bar, the night is skipped.
//
// ## 17. Parameters
// Input: Variant
// Purpose: which of the four catalogue cells to trade. The window is 01:30–04:30 or 02:00–03:00. The event filter is off or on.
// Default: Window0130To0430 — the catalogue's primary window with the event filter off. This was the old integer 1. It is a predeclared choice, not a fitted optimum.
// Units: Not applicable
// Range: Window0130To0430, Window0200To0300, Window0130To0430EventFilter, Window0200To0300EventFilter. Any other value throws in StartCalc and the study does not run.
// Effect: the narrow members use 02:00–03:00 ET. The EventFilter members also skip a night that fails the tier-1 08:30 check.
// Interactions: the event-filter members turn EventConfig on. The other two leave M-EVENT disabled.
// Fixed or research-tuned: research-tuned. The four members are the catalogue sweep. The default is the primary cell.
// Input: atrLength
// Purpose: simple-average true-range length on the closed 60-minute bars. ASSUMPTION – to be investigated. It is not an [Input]; it stays 14 in Ts01Config until the length is settled.
// Default: 14 — predeclared assumption, not a fitted optimum
// Units: bars
// Range: the code uses 14. A searched range is Not yet verified.
// Effect: a longer length smooths the disaster stop
// Interactions: the stop is max(2 × that ATR, the profile cost-floor distance)
// Fixed or research-tuned: fixed assumption, marked for investigation
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
// Uncertainty: no run yet; ATR60m length 14 is ASSUMPTION – to be investigated; rolls are off; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: TS01LE
// logSignal tag: TS-01
// Skip / flatten lines: "TS-01" plus "skip: no bar", "skip:" and the reason, or "skip: stop" when the budget fails. The context field variant is the enum name.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class, its constructor, and the ArgumentException text that names the class. A _TestNN slot renames those to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// The old file name was _TS_02_EuropeanOpenDrift.Strategy.CS. The class was _TS_02_EuropeanOpenDrift. Both were TS-01. Variant was an int 1–4; it is now EVariant, and the default still selects the wide window with the filter off.
// ASSUMPTION – to be investigated: ATR60m length is 14. The cost floor is the Data1 instrument profile, not a hardcoded 14-tick ES floor. Fee and slippage numbers in the preset cost table are not changed by this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _TS_01_EuropeanOpenDrift : _CustomSignal
	{
		/// <summary>Catalogue sweep: window {01:30-04:30, 02:00-03:00} times the event filter {off, on}. Default is the primary window with the filter off.</summary>
		public enum EVariant
		{
			Window0130To0430,
			Window0200To0300,
			Window0130To0430EventFilter,
			Window0200To0300EventFilter,
		}

		[Input] public EVariant Variant { get; set; }

		protected sealed class Ts01Config
		{
			public int contracts = 1;
			public double disasterAtr = 2.0;
			public int atrLength = 14;       // ASSUMPTION – to be investigated. The catalogue does not give the ATR60m length. This file uses 14 until that is settled.
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity
		}
		protected Ts01Config Cfg { get; } = new Ts01Config();

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private _CmeSessionClock m_clock;
		private TimeSpan m_start, m_end;
		private bool m_filter;
		private int m_floorTicks;
		private string m_costText = "";
		private DateTime m_doneDay = DateTime.MinValue;
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		private double m_stop = double.NaN;

		public _TS_01_EuropeanOpenDrift(object _ctx) : base(_ctx) { Variant = EVariant.Window0130To0430; }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS01LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS01LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS01DS", EOrderAction.Sell, OrderExit.FromAll));
			// M-EVENT: created only when enabled (StartCalc). T1 only; coverage holes of lower tiers never block (EV-4.6b).
			EventConfig.minTier = EEventTier.T1;
			EventConfig.classes = EEventClassMask.Macro;
		}

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(EVariant), Variant)) throw new ArgumentException("_TS_01_EuropeanOpenDrift: Variant is not a defined EVariant");
			bool narrow = Variant == EVariant.Window0200To0300 || Variant == EVariant.Window0200To0300EventFilter;
			m_start = narrow ? new TimeSpan(2, 0, 0) : new TimeSpan(1, 30, 0);
			m_end = narrow ? new TimeSpan(3, 0, 0) : new TimeSpan(4, 30, 0);
			m_filter = Variant == EVariant.Window0130To0430EventFilter || Variant == EVariant.Window0200To0300EventFilter;
			EventConfig.enabled = m_filter;   // set before base.StartCalc: the engine is built there
			HtfStreams = new[] { 2 };         // M-MTF: StartCalc fails loudly if Data2 is missing or another root
			m_clock = null;
			base.StartCalc();
			InstrumentProfile profile = InstrumentProfileOf(1);
			if (profile == null || !profile.cost.known || profile.costFloorTicks < 1)
				throw new ArgumentException("_TS_01_EuropeanOpenDrift: Data1 instrument cost is unknown; the floor comes from InstrumentProfile, not a hardcoded ES or MES tick count");
			m_floorTicks = profile.costFloorTicks;
			m_costText = profile.cost.Text();
			if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 cost({m_costText}) floorTicks({m_floorTicks}) atr60Length({Cfg.atrLength}) ASSUMPTION");
			m_doneDay = DateTime.MinValue; m_stop = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime et = Clock.ToEastern(Bars.Time[0]);
			DateTime day = tradingDayKey(Bars.Time[0]);   // the 01:30 ET bar belongs to the Globex day that opened 18:00 ET the evening before
			TimeSpan tod = et.TimeOfDay;
			int pos = StrategyInfo.MarketPosition;

			if (pos > 0)
			{
				if (tod >= m_end && tod < new TimeSpan(17, 0, 0)) { m_lx.Send(); return; }
				if (!double.IsNaN(m_stop)) m_lxStp.Send(m_stop);   // re-send every bar (MC cancels unsent orders)
				return;
			}
			m_stop = double.NaN;
			if (day == m_doneDay) return;
			// the entry order is sent on the close of the FIRST closed bar stamped at or after m_start - 1 min (normally that
			// bar, so it fills at the m_start bar's open); a missing bar delays the entry, it does not skip the night
			TimeSpan sendFrom = m_start - TimeSpan.FromMinutes(1);
			if (tod >= m_end && tod < new TimeSpan(17, 0, 0))
			{
				m_doneDay = day;   // the window passed with no closed bar in it
				if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: no bar in {sendFrom:hh\\:mm}-{m_end:hh\\:mm} ET");
				return;
			}
			if (tod < sendFrom || tod >= m_end) return;
			m_doneDay = day;

			string skip = skipReason(day);
			double atr = atrClosed(2, Cfg.atrLength);
			if (skip == null && !(atr > 0)) skip = "ATR60m warm-up";
			if (skip != null) { if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: {skip}"); return; }

			double planned = Bars.Close[0];
			m_stop = planned - Cfg.disasterAtr * atr;   // planned from the signal close; the fill is the next bar's open
			double tick = tickSize();
			if (planned - m_stop < m_floorTicks * tick) m_stop = planned - m_floorTicks * tick;   // max(structural, profile cost floor)
			if (overRiskBudget(planned - m_stop))   // widened stop breaches the 1% budget: skip and log, never resize
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: stop {(planned - m_stop) / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}");
				m_stop = double.NaN; return;
			}
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "variant", Variant.ToString() }, { "atr60", atr.ToString("F2") }, { "sendBar", tod.ToString(@"hh\:mm") }, { "cost", m_costText }, { "floorTicks", m_floorTicks.ToString() } };
				journalArm("TS01LE", planned, m_stop, double.NaN, ctx);
				logSignal("TS-01", +1, planned, m_stop, ctx, true);
			}
			m_le.Send(Cfg.contracts);
			m_lxStp.Send(m_stop);   // also protects the entry bar
		}

		private string skipReason(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			if (cme.Closed || cme.NoRth || cme.HaltEt.HasValue) return "CME holiday/halt " + cme.Name;
			if (!m_filter) return null;
			_EventCalendar ev = EventCalendar;
			if (ev == null || ev.Disabled) return "event filter on but M-EVENT disabled";   // fail closed
			EEventKind hole;
			if (ev.CoverageBlocks(day, EEventClassMask.Macro, EEventTier.T1, out hole)) return "coverage Unknown " + hole;
			foreach (EventRec r in ev.EventsOn(day, EEventClassMask.Macro))
				if (r.tier == EEventTier.T1 && r.etTime.TimeOfDay == EventRules.T0830) return "T1 08:30 " + r.KindName;
			return null;
		}

		/// <summary>Simple-average true range over the last n CLOSED bars of stream ds (needs n + 1 closed bars).</summary>
		private double atrClosed(int ds, int n)
		{
			if (BarsOfData(ds).CurrentBar <= n + 1) return double.NaN;
			double sum = 0;
			for (int k = 0; k < n; k++)
			{
				DateTime t; double o, h, l, c, v, pc;
				HtfClosedBar(ds, k + 1, out t, out o, out h, out l, out pc, out v);
				HtfClosedBar(ds, k, out t, out o, out h, out l, out c, out v);
				sum += Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc)));
			}
			return sum / n;
		}
	}
}
