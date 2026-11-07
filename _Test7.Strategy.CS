
// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-VWAP (§5.10), F-JRNL (§5.13), F-EVT (§5.14).
//
// **Catalogue rules (AV-08).** 
// A layer, not a strategy: `aboveWTD && aboveMTD` allows longs only, `belowWTD && belowMTD` allows shorts only. It is applied to host events
// (PS-01, PS-07, MT-01, AV-02). Sweep: {WTD only, WTD+MTD, MTD+QTD}. Multi-day anchors reset at rolls.
//
// **Chart.**
//   @ES 1m, dxFeed unadjusted, 24h Globex template. The class derives from `_VolumeProfileFuturesBase` and sets the two VWAP slots on Data1 in
//   `Create`: `Vwap1Type = EVwapType.Week`, `Vwap2Type = EVwapType.Month`. `StartCalc` throws if they are changed.
//
// **Walk-through.**
// - Only the **long** side of the layer is coded: `layerLong` = both slots active, close > WTD VWAP and close > MTD VWAP, and both periods 
//   "clean".
// - A period is clean only if no roll happened since the week start (Monday Globex date) or the month start, because the Week/Month VWAPs do
//   **not** reset at a roll. RollDetection is Series.
// - The host is a close above the prior 20-bar high between 09:45 and 15:00 ET, with a 16-tick stop (above the 14-tick cost floor), flat at
//   15:55, or at `RthCloseEt` − 5 min on an early-close day (`flatAt`). **It is illustrative only, not a catalogue rule**: it exists so the
//   layer has events to filter. The 16-tick stop is above the floor; 1 contract at $200 risk passes the 1% check only with equity ≥ $20,000,
//   otherwise the signal is skipped and journaled.
// - Host signals the layer blocks are logged with `logSignal(..., false)`, so the ablation (gated vs ungated) can be run from `MCEvents`
//   without a second backtest.
//
// **Pitfalls.**
// - AV-08's test is a layer ablation on real hosts. Don't read anything into the illustrative host.
// - The Week VWAP slot starts at the week's first bar; the catalogue's A2-07 merge anchors at Sunday 18:00. On a Globex chart those are the same bar.
// - Not coded: the short side (`belowWTD && belowMTD`), the WTD-only arm and the MTD+QTD arm (`EVwapType.Quarter`).
// - **Coding choice (labelled):** `wtdClean`/`mtdClean` switch the layer off for the rest of a week or month that holds a roll. The catalogue says multi-day anchors reset at rolls. The other reading is to re-anchor both VWAPs at `rollOn(1)`; it is not coded (Appendix F).

// **Full strategy** — `_AV_08_PeriodAvwapLayer.Strategy.CS`:
// Manual v4 example 3 - AV-08 week-open / month-open VWAP regime layer (aboveWTD && aboveMTD for longs only).
// The host trigger below is ILLUSTRATIVE ONLY (not a catalogue rule): AV-08 is a layer for PS-01 / PS-07 / MT-01 / AV-02.
// Chart: @ES 1m, dxFeed continuous UNADJUSTED, 24h CME Globex template.
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _Test7 : _VolumeProfileFuturesBase
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

		public _Test7(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "AV08LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AV08LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "AV08LS", EOrderAction.Sell, OrderExit.FromAll));
			// The VWAP slots are [Input]s of the VP base: these are defaults, the Format Signal dialog can override them.
			Vwap1Type = EVwapType.Week; Vwap1DataStream = 1;   // WTD: resets on the first bar of a new Monday-start Globex week
			Vwap2Type = EVwapType.Month; Vwap2DataStream = 1;   // MTD
			Vwap3Type = EVwapType.Disabled;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (Vwap1Type != EVwapType.Week || Vwap2Type != EVwapType.Month || Vwap1DataStream != 1 || Vwap2DataStream != 1)
				throw new ArgumentException("_Ex03PeriodAvwapLayer: needs Vwap1Type = Week and Vwap2Type = Month on Data1");
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
