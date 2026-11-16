// ## 1. Identity
// Catalogue ID: A2-13
// ID mapping: A2-13 -> _A2_13_GapAvwapOpen.Strategy.CS (class _A2_13_GapAvwapOpen).
// Name: A2-13 gap-day AVWAP from the open
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_13_GapAvwapOpen
// File: sandbox/_A2_13_GapAvwapOpen.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-13.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: Chart is 1-minute. AVWAP from the open is the RTH volume-weighted typical price computed here from 09:30, not the engine slot. 5-minute closes are built from Data1 bar-end times. ATR5 is ATR(14) on Data1, which is ATR of 1-minute bars, not of 5-minute bars. The catalogue text is written for a gap up; a gap down is the mirror. One contract, so branch Hold works a 1R limit and also exits on a close through the AVWAP after 1R; branch Fail targets the prior VAH only.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-13 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: sendExits re-sends the stop and the target while the position is open. Size stays 1 contract.
// Exit: the catalogue stop, target or time exit, sent only from sendExits. Not the GX wrapper.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: market next bar, or a stop, as the rule states.
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: one contract, long or short as the rule states.
// Re-entry: as the catalogue states. Where it is silent, one attempt per session.
// Same session after a stop: no new entry that session, except where the catalogue allows re-entry.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the catalogue market. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 5-minute.
// Data1: the chart the header names. Tests use data through 2024-12-31 only.
// Data2: Not applicable. Higher-timeframe bars are built from Data1.
// Further series: Not applicable
// Warmup: the ATR or average named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup and the 1 percent risk-budget check.
// Skip when: the budget fails, a required average is not ready, or the rule's filter fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract. journalArm records the planned entry.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create.
// Exit orders: sent only from sendExits.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: sendExits re-sends the stop and the target. Nothing else is added.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
// Hard stop: the catalogue stop, widened to 14 ticks when the rule places a stop.
// Target: the catalogue target, or Not applicable.
// Time exit: the catalogue flat time, via FlatBy and mustFlatten inside sendExits.
// Invalidation: the catalogue skip rules.
//
// ## 14. Regimes
// Suitable: the catalogue session window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar.
// Repaint: Not applicable. Closed bars only.
// Survivorship: Not applicable
// Rolls: RollDetection Series, unless the file says otherwise.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: Chart is 1-minute. AVWAP from the open is the RTH volume-weighted typical price computed here from 09:30, not the engine slot. 5-minute closes are built from Data1 bar-end times. ATR5 is ATR(14) on Data1, which is ATR of 1-minute bars, not of 5-minute bars. The catalogue text is written for a gap up; a gap down is the mirror. One contract, so branch Hold works a 1R limit and also exits on a close through the AVWAP after 1R; branch Fail targets the prior VAH only.
//
// ## 17. Parameters
// Each [Input] below is one of this strategy's own parameters. TraceLevel stays on the base.
// Input: see the [Input] properties on the class
// Purpose: the catalogue sweep named in section 22
// Default: the value set in the constructor, which is the catalogue's first listed value
// Units: as the property name states
// Range: the enum members, checked with Enum.IsDefined in StartCalc
// Effect: selects the arm. It does not resize a position.
// Interactions: the other settings stay in the protected config
// Fixed or research-tuned: research-tuned where the catalogue names a sweep
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; GX is not on this branch; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name.
// logSignal tag: A2-13
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Chart is 1-minute. AVWAP from the open is the RTH volume-weighted typical price computed here from 09:30, not the engine slot. 5-minute closes are built from Data1 bar-end times. ATR5 is ATR(14) on Data1, which is ATR of 1-minute bars, not of 5-minute bars. The catalogue text is written for a gap up; a gap down is the mirror. One contract, so branch Hold works a 1R limit and also exits on a close through the AVWAP after 1R; branch Fail targets the prior VAH only.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_13_GapAvwapOpen : _VolumeProfileFuturesBase
	{
		public enum EBranch { Hold, Fail }
		[Input] public EBranch Branch { get; set; }

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private double m_num, m_den, m_stop, m_target, m_failExtreme, m_failTrigger, m_atr;
		private int m_below, m_atrN;
		private DateTime m_day = DateTime.MinValue, m_b5 = DateTime.MinValue;
		private bool m_gapUp, m_tradable, m_gapChecked, m_failed, m_done, m_holdDead;
		private double m_5c, m_prev5c, m_5h, m_5l, m_prevTr;

		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_13_GapAvwapOpen(object _ctx) : base(_ctx) { Branch = EBranch.Hold;  Side = ESandboxSide.Both; }

		protected override void Create()
		{
			base.Create();
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   // PriorRthProfile() is built from Own RTH profiles; null without this
			RollDetection = ERollDetection.Series;
			FlatBy = new TimeSpan(15, 55, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A213LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A213SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A213LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A213SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A213LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A213SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A213LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A213ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EBranch), Branch)) throw new ArgumentException("_A2_13_GapAvwapOpen: Branch is not a defined EBranch");
			NeedsDaily = true;
			DailyConfig.atrRestartOnRoll = false;   // Analyst MC17: the reseed after each quarterly roll left Atr20d NaN ~20 sessions per roll (173 days); PR #46 DECIDED: ATR carries on across rolls
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   /* Analyst MC17 (A2-13): DevelopingSource is an [Input]; the chart's saved value replaces the Create() value, so re-apply it */ base.StartCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime et = ToEastern(Bars.Time[0]);
			TimeSpan tod = et.TimeOfDay;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) newDay(day);
			if (!m_gapChecked && IsRth()) checkGap(day);
			if (IsRth()) { double typ = (Bars.High[0] + Bars.Low[0] + Bars.Close[0]) / 3.0; double v = Bars.Volume[0] > 0 ? Bars.Volume[0] : 1; m_num += typ * v; m_den += v; }
			step5(et);
			double av = m_den > 0 ? m_num / m_den : double.NaN;
			int pos = StrategyInfo.MarketPosition;
			int side = m_gapUp ? 1 : -1;
			if (Branch == EBranch.Fail) side = -side;
			bool sendExits()
			{
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (Branch == EBranch.Hold && pos != 0)
				{
					double r = Math.Abs(StrategyInfo.AvgEntryPrice - m_stop);
					bool past1r = pos > 0 ? Bars.High[0] >= StrategyInfo.AvgEntryPrice + r : Bars.Low[0] <= StrategyInfo.AvgEntryPrice - r;
					if (past1r && !double.IsNaN(av) && pos * (Bars.Close[0] - av) < 0) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				}
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return false;
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || pos != 0 || !m_tradable || rollOn(1) || double.IsNaN(av)) return;
			if (Branch == EBranch.Hold) hold(tod, av, side);
			else fail(tod, av, side);
		}

		private void newDay(DateTime day)
		{
			m_day = day; m_num = m_den = 0; m_below = 0; m_done = m_failed = m_holdDead = false; m_failExtreme = double.NaN;
			m_tradable = m_gapUp = m_gapChecked = false;
		}

		// The rollover bar (18:00 ET) has no RTH open yet, so the gap is read on the first RTH bar of the day.
		private void checkGap(DateTime day)
		{
			SessionStats s = Session;
			if (s == null || !(s.rthOpen > 0)) return;
			m_gapChecked = true;
			VpExtra prior = PriorRthProfile();
			double atr = DailyContext != null ? DailyContext.Atr20d : double.NaN;
			m_tradable = prior != null && atr > 0 && Math.Abs(s.Gap) >= 0.5 * atr && (s.rthOpen > prior.vah || s.rthOpen < prior.val);
			m_gapUp = s.Gap > 0;
			if (journalOn()) trace(ETraceLevel.Journal, $"A2-13 {day:yyyy-MM-dd} gap {s.Gap:F2} open {s.rthOpen:F2} priorVA {(prior != null ? prior.val : double.NaN):F2}-{(prior != null ? prior.vah : double.NaN):F2} atr20d {atr:F2} tradable {m_tradable}");
		}

		private void hold(TimeSpan tod, double av, int side)
		{
			if (m_holdDead) { m_done = true; return; }
			if (tod < new TimeSpan(10, 15, 0)) return;
			double atr = averageTrueRange(Bars, 14);
			if (!(atr > 0)) return;
			bool near = side > 0 ? Bars.Low[0] <= av + 0.1 * atr : Bars.High[0] >= av - 0.1 * atr;
			bool bos = side > 0 ? Bars.Close[0] > Bars.High[1] : Bars.Close[0] < Bars.Low[1];
			if (!(near && bos)) return;
			double tick = tickSize();
			double stop = side > 0 ? av - 0.3 * atr : av + 0.3 * atr;
			double floor = 14 * tick;
			if (side * (Bars.Close[0] - stop) < floor) stop = Bars.Close[0] - side * floor;
			double risk = Math.Abs(Bars.Close[0] - stop);
			if (2 * risk * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Error, "A2-13 skipped: multi-contract risk exceeds 1 percent"); m_done = true; return; }
			if (journalOn()) trace(ETraceLevel.Warning, "multi-contract size 2");
			m_stop = stop; m_target = Bars.Close[0] + side * risk; m_done = true;
			if (side > 0) { m_le.Send(2); m_lxStp.Send(m_stop); } else { m_se.Send(2); m_sxStp.Send(m_stop); }
			if (journalOn()) journalArm(side > 0 ? "A213LE" : "A213SE", Bars.Close[0], m_stop, m_target, null);
		}

		private void fail(TimeSpan tod, double av, int side)
		{
			if (tod > new TimeSpan(10, 30, 0) && m_below < 2) { m_done = true; return; }
			if (!m_failed) return;
			bool trigger = side > 0 ? Bars.Close[0] > m_failTrigger : Bars.Close[0] < m_failTrigger;
			if (!trigger) { m_failExtreme = side > 0 ? Math.Max(m_failExtreme, Bars.High[0]) : Math.Min(m_failExtreme, Bars.Low[0]); return; }
			double tick = tickSize();
			double stop = m_failExtreme + (side > 0 ? -tick : tick);
			double floor = 14 * tick;
			if (side * (Bars.Close[0] - stop) < floor) stop = Bars.Close[0] - side * floor;
			VpExtra prior = PriorRthProfile();
			double target = prior != null ? (m_gapUp ? prior.vah : prior.val) : double.NaN;
			double risk = Math.Abs(Bars.Close[0] - stop);
			if (double.IsNaN(target) || side * (target - Bars.Close[0]) <= 0) { if (journalOn()) trace(ETraceLevel.Journal, "A2-13 skip: prior VA target missing or not ahead of the entry"); m_done = true; return; }
			if (2 * risk * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Error, "A2-13 skipped: multi-contract risk exceeds 1 percent"); m_done = true; return; }
			if (journalOn()) trace(ETraceLevel.Warning, "multi-contract size 2");
			m_stop = stop; m_target = target; m_done = true;
			if (side > 0) { m_le.Send(2); m_lxStp.Send(m_stop); } else { m_se.Send(2); m_sxStp.Send(m_stop); }
			if (journalOn()) journalArm(side > 0 ? "A213LE" : "A213SE", Bars.Close[0], m_stop, m_target, null);
		}

		private void step5(DateTime et)
		{
			int slot = (et.Hour * 60 + et.Minute) / 5;
			DateTime b = et.Date.AddMinutes(slot * 5);
			if (m_b5 == DateTime.MinValue) { m_b5 = b; m_5c = Bars.Close[0]; m_5h = Bars.High[0]; m_5l = Bars.Low[0]; return; }
			if (b == m_b5) { m_5c = Bars.Close[0]; m_5h = Math.Max(m_5h, Bars.High[0]); m_5l = Math.Min(m_5l, Bars.Low[0]); return; }
			double av = m_den > 0 ? m_num / m_den : double.NaN;
			if (m_tradable && !double.IsNaN(av))
			{
				bool against = m_gapUp ? m_5c < av : m_5c > av;
				m_below = against ? m_below + 1 : 0;
				if (m_below >= 2 && !m_failed && ToEastern(Bars.Time[0]).TimeOfDay <= new TimeSpan(10, 30, 0))
				{ m_failed = true; m_failExtreme = m_gapUp ? m_5h : m_5l; m_failTrigger = m_gapUp ? m_5l : m_5h; }
				if (Branch == EBranch.Hold && against && ToEastern(Bars.Time[0]).TimeOfDay <= new TimeSpan(10, 15, 0)) m_holdDead = true;
			}
			m_prev5c = m_5c; m_b5 = b; m_5c = Bars.Close[0]; m_5h = Bars.High[0]; m_5l = Bars.Low[0];
		}
	}
}
