// ## 1. Identity
// Catalogue ID: A2-11
// ID mapping: A2-11 -> _A2_11_PriorDayVwap.Strategy.CS (class _A2_11_PriorDayVwap).
// Name: A2-11 prior-day VWAP acceptance
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_11_PriorDayVwap
// File: sandbox/_A2_11_PriorDayVwap.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-11.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: Chart is 1-minute. pdVWAP is CurrentVwap1 (Vwap1Type Session) latched when Session.rthComplete first becomes true. 15-minute and 5-minute closes are built from Data1 bar-end times, not from extra series. A higher low is the first 5-minute bar whose low is above both pdVWAP and the prior 5-minute low. The 1-minute break of structure is a 1-minute close above that 5-minute bar's high. The target is the nearer of prior RTH high and prior VAH, and the trade is skipped when that distance is under 1R.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-11 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: exitBar re-sends the stop and the target while the position is open. Size stays 1 contract.
// Exit: the catalogue stop and target, sent through exitPlan and exitBar.
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
// Chart: Data1 = 1-minute. Data2 = 15-minute.
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
// Exit orders: exitPlan and exitBar.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: exitBar re-sends the stop and the target. Nothing else is added.
//
// ## 13. Exits
// Exit orders go through exitPlan and exitBar.
// Hard stop: the catalogue stop, widened to 14 ticks when the rule places a stop.
// Target: the catalogue target, or Not applicable.
// Time exit: the catalogue flat time, via the GX FlatBy clock.
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
// Other: ASSUMPTION: Chart is 1-minute. pdVWAP is CurrentVwap1 (Vwap1Type Session) latched when Session.rthComplete first becomes true. 15-minute and 5-minute closes are built from Data1 bar-end times, not from extra series. A higher low is the first 5-minute bar whose low is above both pdVWAP and the prior 5-minute low. The 1-minute break of structure is a 1-minute close above that 5-minute bar's high. The target is the nearer of prior RTH high and prior VAH, and the trade is skipped when that distance is under 1R.
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
// logSignal tag: A2-11
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Chart is 1-minute. pdVWAP is CurrentVwap1 (Vwap1Type Session) latched when Session.rthComplete first becomes true. 15-minute and 5-minute closes are built from Data1 bar-end times, not from extra series. A higher low is the first 5-minute bar whose low is above both pdVWAP and the prior 5-minute low. The 1-minute break of structure is a 1-minute close above that 5-minute bar's high. The target is the nearer of prior RTH high and prior VAH, and the trade is skipped when that distance is under 1R.
// IOG is disabled. Exits go through exitPlan and exitBar.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_11_PriorDayVwap : _VolumeProfileFuturesBase
	{
		public enum EAcceptBars { Two, Three }
		[Input] public EAcceptBars AcceptBars { get; set; }

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp, m_lxLmt;
		private double m_pd, m_stop, m_target, m_hl, m_hlHigh, m_prev5Low, m_prev5High;
		private int m_above15, m_fiveCount;
		private DateTime m_day = DateTime.MinValue, m_b15 = DateTime.MinValue, m_b5 = DateTime.MinValue;
		private bool m_latched, m_accepted, m_haveHl, m_done, m_wasComplete;
		private double m_15c, m_5h, m_5l, m_5c;

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_11_PriorDayVwap(object _ctx) : base(_ctx) { AcceptBars = EAcceptBars.Two;  Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }

		protected override void Create()
		{
			base.Create();
			DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth;   // PriorRthProfile() is built from Own RTH profiles; null without this
			GxOrderPrefix = "A211"; createGxOrders();
			RollDetection = ERollDetection.Series;
			FlatBy = new TimeSpan(15, 55, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A211LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A211LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A211LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A211LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EAcceptBars), AcceptBars)) throw new ArgumentException("_A2_11_PriorDayVwap: AcceptBars is not a defined EAcceptBars");
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			base.StartCalc();
			m_pd = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime et = ToEastern(Bars.Time[0]);
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_above15 = 0; m_accepted = m_haveHl = m_done = false; m_filledSetup = false; m_restOn = false; m_hl = m_hlHigh = double.NaN; }
			SessionStats s = Session;
			if (s != null && s.rthComplete && !m_wasComplete && !double.IsNaN(CurrentVwap1)) m_pd = CurrentVwap1;
			m_wasComplete = s != null && s.rthComplete;
			int pos = StrategyInfo.MarketPosition;
			exitBar();
			if (pos > 0) return;
			if (pos != 0 || m_done || rollOn(1) || !(m_pd > 0) || s == null || !(s.rthOpen > 0) || s.rthOpen >= m_pd) return;
			if (!IsRth()) return;
			bucket(et, 15, ref m_b15, ref m_15c, on15);
			bucket(et, 5, ref m_b5, ref m_5c, on5);
			if (!m_haveHl) return;
			if (!(Bars.Close[0] > m_hlHigh)) return;
			double tick = tickSize();
			double stop = m_hl - tick;
			double floor = 14 * tick;
			if (Bars.Close[0] - stop < floor) stop = Bars.Close[0] - floor;
			double risk = Bars.Close[0] - stop;
			VpExtra prior = PriorRthProfile();
			double vah = prior != null ? prior.vah : double.NaN;
			double tHigh = s.priorRthHigh, tVah = vah;
			double target = double.NaN;
			if (tHigh > Bars.Close[0] && tVah > Bars.Close[0]) target = Math.Min(tHigh, tVah);
			else if (tHigh > Bars.Close[0]) target = tHigh;
			else if (tVah > Bars.Close[0]) target = tVah;
			if (!(risk > 0) || double.IsNaN(target) || target - Bars.Close[0] < risk || risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ if (journalOn()) trace(ETraceLevel.Journal, "A2-11 " + day.ToString("yyyy-MM-dd") + " skip: target or risk"); m_done = true; return; }
			m_stop = stop; m_target = target; m_done = true;
			takeGx(true, Bars.Close[0], m_stop, m_target, "A211LE");
			m_le.Send();
			if (journalOn()) journalArm("A211LE", Bars.Close[0], m_stop, m_target, null);
		}

		private void takeGx(bool longSide, double entry, double stop, double target, string name)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			GxConfig.TargetMode = ETargetMode.SingleTarget;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = Math.Abs(target - entry);
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(longSide, entry, stop, 1, mkt, name);
		}

		private void bucket(DateTime et, int minutes, ref DateTime cur, ref double close, Action onClose)
		{
			int slot = (et.Hour * 60 + et.Minute) / minutes;
			DateTime b = et.Date.AddMinutes(slot * minutes);
			if (cur == DateTime.MinValue) { cur = b; close = Bars.Close[0]; if (minutes == 5) { m_5h = Bars.High[0]; m_5l = Bars.Low[0]; } return; }
			if (b == cur) { close = Bars.Close[0]; if (minutes == 5) { m_5h = Math.Max(m_5h, Bars.High[0]); m_5l = Math.Min(m_5l, Bars.Low[0]); } return; }
			onClose();
			cur = b; close = Bars.Close[0]; if (minutes == 5) { m_5h = Bars.High[0]; m_5l = Bars.Low[0]; }
		}

		private void on15()
		{
			if (m_accepted) return;
			m_above15 = m_15c > m_pd ? m_above15 + 1 : 0;
			int need = AcceptBars == EAcceptBars.Three ? 3 : 2;
			if (m_above15 >= need) m_accepted = true;
		}

		private void on5()
		{
			m_fiveCount++;
			if (m_accepted && !m_haveHl && m_fiveCount > 1 && m_5l > m_pd && m_5l > m_prev5Low)
			{ m_haveHl = true; m_hl = m_5l; m_hlHigh = m_5h; }
			m_prev5Low = m_5l; m_prev5High = m_5h;
		}

		private bool hostGate(bool buy)
		{
			if (Gate == ESandboxGate.Off) return true;
			VpExtra prior = null;
			try { prior = PriorRthProfile(); } catch (Exception) { }
			double poc = prior != null ? prior.poc : double.NaN;
			double hi = Session != null ? Session.rthHigh : Bars.High[0];
			double lo = Session != null ? Session.rthLow : Bars.Low[0];
			double atr = averageTrueRange(Bars, 14);
			double sma = double.NaN;
			try { sma = DailyContext.Sma200; } catch (Exception) { }
			double prev = Session != null ? Session.priorRthClose : double.NaN;
			int fam = 0, ranges = 0;
			if (Gate == ESandboxGate.ThreeFamily) dvpFamiliesAt(1, Bars.Close[0], 0.3, out fam, out ranges);
			bool ok = _SandboxRules.GateAllows(Gate, buy, Bars.Close[0], poc, hi, lo, atr, sma, prev, fam);
			if (!ok && journalOn()) trace(ETraceLevel.Journal, "gate " + Gate + " blocked");
			return ok;
		}
		private void honorExit(double entry, double risk)
		{
			double t1; int pct; bool be;
			if (!_SandboxRules.ScaleExit(ExitMode, out t1, out pct, out be)) return;
			int qty = _SandboxRules.ScaleQty(ExitMode, false);
			if (qty > 1 && journalOn()) trace(ETraceLevel.Warning, "multi-contract size " + qty);
			if (qty > 1 && risk > 0 && qty * risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{
				if (journalOn()) trace(ETraceLevel.Error, "skipped: multi-contract risk exceeds 1 percent");
				m_done = true; m_restOn = false; return;
			}
			GxConfig.TargetMode = pct > 0 ? ETargetMode.ScaleOutThenRunner : ETargetMode.SingleTarget;
			GxConfig.T1Unit = ETriggerUnit.RMultiple;
			GxConfig.T1Value = t1;
			GxConfig.T1ScalePct = pct;
			GxConfig.FinalTargetUnit = ETriggerUnit.RMultiple;
			GxConfig.FinalTargetValue = t1;
			if (be) GxConfig.BreakEven = EBreakEven.AfterT1;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
		}

	}
}