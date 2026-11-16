// ## 1. Identity
// Catalogue ID: A2-12
// ID mapping: A2-12 -> _A2_12_VwapTwapDivergence.Strategy.CS (class _A2_12_VwapTwapDivergence).
// Name: A2-12 VWAP minus TWAP
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_12_VwapTwapDivergence
// File: sandbox/_A2_12_VwapTwapDivergence.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-12.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: Chart is 1-minute. sVWAP is CurrentVwap1 with Vwap1Type Session. sTWAP is the equal-weight mean of 1-minute closes from 09:30, not a tick TWAP. ATR30 is Wilder ATR(14) of 30-minute true range built from the 1-minute bars. The event-study gate in the catalogue is not a second switch: this file is the strategy arm, and it trades when D is above the threshold. A 30-minute close across sVWAP is a 1-minute close below sVWAP on a minute that ends a 30-minute bucket.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-12 heading. Do not paste the entry.
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
// Chart: Data1 = 1-minute. Data2 = 30-minute.
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
// Other: ASSUMPTION: Chart is 1-minute. sVWAP is CurrentVwap1 with Vwap1Type Session. sTWAP is the equal-weight mean of 1-minute closes from 09:30, not a tick TWAP. ATR30 is Wilder ATR(14) of 30-minute true range built from the 1-minute bars. The event-study gate in the catalogue is not a second switch: this file is the strategy arm, and it trades when D is above the threshold. A 30-minute close across sVWAP is a 1-minute close below sVWAP on a minute that ends a 30-minute bucket.
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
// logSignal tag: A2-12
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Chart is 1-minute. sVWAP is CurrentVwap1 with Vwap1Type Session. sTWAP is the equal-weight mean of 1-minute closes from 09:30, not a tick TWAP. ATR30 is Wilder ATR(14) of 30-minute true range built from the 1-minute bars. The event-study gate in the catalogue is not a second switch: this file is the strategy arm, and it trades when D is above the threshold. A 30-minute close across sVWAP is a 1-minute close below sVWAP on a minute that ends a 30-minute bucket.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_12_VwapTwapDivergence : _VolumeProfileFuturesBase
	{
		public enum EDeltaMin { D025, D050 }
		[Input] public EDeltaMin DeltaMin { get; set; }

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private double m_twapNum; private int m_twapN, m_atrCount;
		private double m_atr, m_prevClose, m_stop, m_bHigh, m_bLow, m_bClose;
		private DateTime m_day = DateTime.MinValue, m_bucket = DateTime.MinValue;
		private bool m_done;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_12_VwapTwapDivergence(object _ctx) : base(_ctx) { DeltaMin = EDeltaMin.D025; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			FlatBy = new TimeSpan(15, 55, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A212LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A212LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A212LS", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EDeltaMin), DeltaMin)) throw new ArgumentException("_A2_12_VwapTwapDivergence: DeltaMin is not a defined EDeltaMin");
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			base.StartCalc();
			m_atr = m_prevClose = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime et = ToEastern(Bars.Time[0]);
			TimeSpan tod = et.TimeOfDay;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_twapNum = 0; m_twapN = 0; }
			if (IsRth()) { m_twapNum += Bars.Close[0]; m_twapN++; }
			stepAtr(et);
			double vwap = CurrentVwap1;
			int pos = StrategyInfo.MarketPosition;
			bool bucketEnd = et.Minute % 30 == 0;
			bool sendExits()
			{
				if (mustFlatten() || (bucketEnd && !double.IsNaN(vwap) && Bars.Close[0] < vwap)) { m_lx.Send(); return true; }
				m_lxStp.Send(m_stop);
				return false;
			}
			if (pos > 0 && sendExits()) return;
			if (m_done || pos != 0 || tod != new TimeSpan(11, 0, 0) || rollOn(1)) return;
			if (m_atrCount < 14 || !(m_atr > 0) || m_twapN < 1 || double.IsNaN(vwap)) { m_done = true; return; }
			double d = (vwap - m_twapNum / m_twapN) / m_atr;
			double thr = DeltaMin == EDeltaMin.D050 ? 0.5 : 0.25;
			if (!(d > thr) || !(Bars.Close[0] > vwap)) { m_done = true; return; }
			double tick = tickSize();
			double stop = Bars.Close[0] - Math.Max(m_atr, 14 * tick);
			double risk = Bars.Close[0] - stop;
			if (risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ if (journalOn()) trace(ETraceLevel.Journal, "A2-12 " + day.ToString("yyyy-MM-dd") + " skip: risk budget"); m_done = true; return; }
			m_stop = stop; m_done = true;
			m_le.Send(); m_lxStp.Send(m_stop);
			if (journalOn()) journalArm("A212LE", Bars.Close[0], m_stop, double.NaN, null);
		}

		private void stepAtr(DateTime et)
		{
			int slot = (et.Hour * 60 + et.Minute) / 30;
			DateTime b = et.Date.AddMinutes(slot * 30);
			if (m_bucket == DateTime.MinValue) { m_bucket = b; m_bHigh = Bars.High[0]; m_bLow = Bars.Low[0]; m_bClose = Bars.Close[0]; return; }
			if (b == m_bucket) { m_bHigh = Math.Max(m_bHigh, Bars.High[0]); m_bLow = Math.Min(m_bLow, Bars.Low[0]); m_bClose = Bars.Close[0]; return; }
			double tr = m_bHigh - m_bLow;
			if (m_prevClose > 0) tr = Math.Max(tr, Math.Max(Math.Abs(m_bHigh - m_prevClose), Math.Abs(m_bLow - m_prevClose)));
			m_atrCount++;
			m_atr = m_atrCount == 1 || double.IsNaN(m_atr) ? tr : (m_atr * 13.0 + tr) / 14.0;
			m_prevClose = m_bClose; m_bucket = b; m_bHigh = Bars.High[0]; m_bLow = Bars.Low[0]; m_bClose = Bars.Close[0];
		}
	}
}
