// ## 1. Identity
// Catalogue ID: PS-13
// ID mapping: PS-13 -> _PS_13_RotationVolumeLevel.Strategy.CS (class _PS_13_RotationVolumeLevel).
// Name: PS-13 rotation volume level, first touch
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_13_RotationVolumeLevel
// File: sandbox/_PS_13_RotationVolumeLevel.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-13. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-13 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: the example's order type (market next bar, stop or limit).
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: the example's position rule, flat or one unless the example says otherwise.
// Re-entry: as the example codes it.
// Same session after a stop: as the example codes it.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: @ES, the example's chart. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the example's chart, dxFeed continuous unadjusted. Tests use data through 2024-12-31 only.
// Data2: the example's second series, or Not applicable when the example has none.
// Further series: Not applicable unless the example names one.
// Warmup: the example's indicator warmup.
//
// ## 9. Entry and filters
// All of these must be true: the example's entry conditions, and the 1 percent risk-budget check when the example has one.
// Skip when: the example journals a skip, a roll day when the example skips rolls, or the risk budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract where the example fixes size. journalArm records the planned entry.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity, when the example codes that check.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the example's entry order name.
// Exit orders: the example's exit order names.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: the example re-sends its stop or target. Nothing else is added.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
// Hard stop: the example's stop, or Not applicable when the example has none.
// Target: the example's target, or Not applicable.
// Time exit: the example's flat time, or Not applicable.
// Invalidation: the example's skip rules.
//
// ## 14. Regimes
// Suitable: the example's session window.
// Unsuitable: outside that window, and where the example stands aside.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those the example turns on in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings the example sets. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar. HTF reads use HtfClosedBar when the example does.
// Repaint: Not applicable when the example uses closed bars only.
// Survivorship: Not applicable
// Rolls: as the example sets RollDetection.
// Data quality: Not yet verified on a chart.
// Other: this is a port of the worked example, not a new rule set. ASSUMPTION: the example's labelled choices stand until an owner changes them.
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
// catalogue-default: judge this file by its catalogue kill, catalogue section 2.3. The stricter stated rule decides. This header does not change the catalogue.
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
// Uncertainty: no run yet; the example's assumptions; GX is not in the build; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the example's entry order name, when it calls journalArm.
// logSignal tag: PS-13
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex07RotationVolumeLevel.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 7 - PS-13 / DV-03 rotation -> breakout -> first touch of the rotation's highest-volume price (limit arm).
// Chart: Data1 = @ES 1m, Data2 = @ES 15m (same symbol, 24h Globex), dxFeed continuous UNADJUSTED. Range profiles ON (RangeProfiles.enabled).
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_13_RotationVolumeLevel : _VolumeProfileFuturesBase
	{
		protected sealed class Ps13Config
		{
			public int contracts = 1;
			public int rotationBars = 6, atr15Length = 14;   // ATR15m length is not in the catalogue: an assumption
			public double boxAtr = 1.5, breakoutAtr = 1.5;
			public int atrDailyLength = 200;
			public double stopFraction = 0.10;                 // sweep {0.10, 0.20} x ATR200d
			public int costFloorTicks = 14;                     // catalogue 2.1: ES cost floor; stop distance = max(structural, floor), size never shrunk
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity (PF-04's 0.5% is not the default here)
			public int validSessions = 5;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 30, 0), flat = new TimeSpan(15, 55, 0);   // catalogue 2.1 intraday rules
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);   // early-close days: flat at RthCloseEt - 5 min
			public double targetR = 2.0;                        // "before the next cluster" is not coded here: a fixed-R stand-in (coding choice)
		}
		protected Ps13Config Cfg { get; } = new Ps13Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_last15 = DateTime.MinValue;
		// own ATR200d: per-Globex-day TR with the roll day's TR dropped (M-DAILY reseeds its ATR at every roll, see section 4)
		private readonly List<double> m_tr = new List<double>();
		private DateTime m_aggDay = DateTime.MinValue; private double m_dH, m_dL, m_dC = double.NaN, m_prevC = double.NaN; private bool m_dRoll;
		private double m_level = double.NaN; private int m_side; private DateTime m_levelDay; private int m_levelSessions;
		private double m_stop = double.NaN, m_target = double.NaN;

		/// <summary>Catalogue 2.1 / App F #9 (resolved): true when Cfg.contracts at this stop distance risk more than the per-trade budget.</summary>
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		private bool m_done;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _PS_13_RotationVolumeLevel(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			RangeProfiles.enabled = true;              // M-RANGE: needs the bar ring (ringBars 2880 = two days of 1m bars)
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "PS13LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "PS13SE", EOrderAction.SellShort));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS13LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS13SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS13LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "PS13ST", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS13LX", EOrderAction.Sell, OrderExit.FromAll));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS13SX", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			HtfStreams = new[] { 2 };
			base.StartCalc();
			m_last15 = m_aggDay = DateTime.MinValue; m_tr.Clear(); m_dC = m_prevC = double.NaN; m_dRoll = false;
			m_level = m_stop = m_target = double.NaN; m_side = 0; m_levelSessions = 0;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime t = Bars.Time[0], day = GlobexDate(t);
			TimeSpan tod = ToEastern(t).TimeOfDay, flat = flatAt(day);
			bool roll = rollOn(1);
			stepDailyAtr(day, roll);
			if (roll) { m_level = double.NaN; m_side = 0; }       // multi-day rotations reset at rolls

			int pos = StrategyInfo.MarketPosition;
			// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_level = double.NaN; m_side = 0;                // filled: the level is spent (first touch only, no re-entry)
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return true;
			}
			if (pos != 0 && sendExits()) return;
			if (!double.IsNaN(m_level) && m_side != 0 && m_levelSessions > Cfg.validSessions) { m_level = double.NaN; m_side = 0; }

			step15();

			// first touch only: the resting limit IS the touch; once price reaches the level without a fill the level is spent.
			// A touch outside the entry window (no limit resting) also spends it.
			if (!double.IsNaN(m_level) && m_side != 0)
			{
				bool traded = m_side > 0 ? Bars.Low[0] <= m_level : Bars.High[0] >= m_level;
				if (traded && Bars.CurrentBar > 1 && !justArmed) { m_level = double.NaN; m_side = 0; return; }
				justArmed = false;
				// entries 09:45-15:30 ET only (and before an early-close flat time): the limit sent on this close works on the next bar
				bool window = tod >= Cfg.from && tod < Cfg.to && tod < flat;
				if (window) { if (m_side > 0) m_leLmt.Send(m_level, Cfg.contracts); else m_seLmt.Send(m_level, Cfg.contracts); } m_lxStp.Send(m_stop); m_sxStp.Send(m_stop);
			}
		}
		// true only on the bar that armed the level: that 1m bar belongs to the breakout, so its own range must not count
		// as the first touch and use up the level it just created
		private bool justArmed;

		/// <summary>New CLOSED 15m bar: rotation of the previous rotationBars bars, breakout on this bar, level = POC of the box.</summary>
		private void step15()
		{
			DateTime tb; double ob, hb, lb, cb, vb;
			HtfClosedBar(2, 0, out tb, out ob, out hb, out lb, out cb, out vb);
			if (tb == m_last15) return;
			m_last15 = tb;
			int n = Cfg.rotationBars;
			if (BarsOfData(2).CurrentBar <= n + Cfg.atr15Length + 2) return;
			double atr15 = atrClosed(2, Cfg.atr15Length, 1);   // ATR of the bars BEFORE the breakout bar
			double boxH = double.MinValue, boxL = double.MaxValue; DateTime boxStart = DateTime.MinValue, boxEnd = DateTime.MinValue;
			for (int k = 1; k <= n; k++)
			{
				DateTime tk; double ok, hk, lk, ck, vk;
				HtfClosedBar(2, k, out tk, out ok, out hk, out lk, out ck, out vk);
				boxH = Math.Max(boxH, hk); boxL = Math.Min(boxL, lk);
				if (k == 1) boxEnd = tk;
			}
			{ DateTime tk; double ok, hk, lk, ck, vk; HtfClosedBar(2, n + 1, out tk, out ok, out hk, out lk, out ck, out vk); boxStart = tk; }
			if (!(atr15 > 0) || boxH - boxL > Cfg.boxAtr * atr15) return;
			int side = cb > boxH ? +1 : cb < boxL ? -1 : 0;
			if (side == 0 || hb - lb < Cfg.breakoutAtr * atr15) return;
			// 1m volume-at-price over the box only: bars ending in (boxStart, boxEnd] (exclusive t0, end-stamped bars, Probe P2)
			VpExtra e = RangeProfileAfter(1, boxStart, boxEnd);
			if (e == null || !(e.poc > 0)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-13 {tb:yyyy-MM-dd HH:mm} no range profile"); return; }
			RangeStats st = RangeStatsOf(e);
			if (st == null || !(st.coverage >= 0.95)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-13 {tb:HH:mm} box coverage {(st == null ? double.NaN : st.coverage):F2} < 0.95: skip"); return; }
			double atrD = atrDaily();
			if (!(atrD > 0)) return;
			double tick = tickSize();
			double risk = Math.Max(Cfg.stopFraction * atrD, Cfg.costFloorTicks * tick);   // catalogue 2.1: stop = max(structural, cost floor)
			if (overRiskBudget(risk))   // widened stop breaches the per-trade risk budget: skipped and logged, not resized
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"PS-13 {tb:yyyy-MM-dd HH:mm} skip: stop {risk / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}");
				return;
			}
			m_level = e.poc; m_side = side; m_levelDay = GlobexDate(Bars.Time[0]); m_levelSessions = 0; justArmed = true;
			m_stop = m_level - side * risk; m_target = m_level + side * Cfg.targetR * risk;
			var ctx = new Dictionary<string, string> { { "boxH", boxH.ToString() }, { "boxL", boxL.ToString() }, { "atr15", atr15.ToString("F2") }, { "atrD", atrD.ToString("F2") } };
			if (journalOn()) { journalArm(side > 0 ? "PS13LE" : "PS13SE", m_level, m_stop, m_target, ctx); logSignal("PS-13", side, m_level, m_stop, ctx, true); }
		}

		private void stepDailyAtr(DateTime day, bool roll)
		{
			if (day != m_aggDay)
			{
				if (m_aggDay != DateTime.MinValue)
				{
					if (!m_dRoll && !double.IsNaN(m_prevC)) m_tr.Add(Math.Max(m_dH - m_dL, Math.Max(Math.Abs(m_dH - m_prevC), Math.Abs(m_dL - m_prevC))));
					else m_tr.Add(m_dH - m_dL);                    // roll day: the gap to the old contract is not range
					if (m_tr.Count > Cfg.atrDailyLength) m_tr.RemoveAt(0);
					m_prevC = m_dC;
					if (!double.IsNaN(m_level)) m_levelSessions++;
				}
				m_aggDay = day; m_dH = Bars.High[0]; m_dL = Bars.Low[0]; m_dRoll = false;
			}
			m_dH = Math.Max(m_dH, Bars.High[0]); m_dL = Math.Min(m_dL, Bars.Low[0]); m_dC = Bars.Close[0];
			if (roll) { m_dRoll = true; m_prevC = double.NaN; }
		}
		private double atrDaily()
		{
			if (m_tr.Count < Cfg.atrDailyLength) return double.NaN;
			double s = 0; foreach (double x in m_tr) s += x; return s / m_tr.Count;
		}

		private double atrClosed(int ds, int len, int from)
		{
			double sum = 0;
			for (int k = from; k < from + len; k++)
			{
				DateTime t; double o, h, l, c, v, pc;
				HtfClosedBar(ds, k + 1, out t, out o, out h, out l, out pc, out v);
				HtfClosedBar(ds, k, out t, out o, out h, out l, out c, out v);
				sum += Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc)));
			}
			return sum / len;
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

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				m_leLmt.Send(m_restPx, m_restQty);
			}
			else
			{
				m_seLmt.Send(m_restPx, m_restQty);
			}
			return true;
		}

	}
}
