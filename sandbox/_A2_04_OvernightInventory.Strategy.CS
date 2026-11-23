// ## 1. Identity
// Catalogue ID: A2-04
// ID mapping: A2-04 -> _A2_04_OvernightInventory.Strategy.CS (class _A2_04_OvernightInventory).
// Name: A2-04 overnight inventory vs overnight VWAP
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_04_OvernightInventory
// File: sandbox/_A2_04_OvernightInventory.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-04.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: Data1 is 1-minute (owner rule); the 09:35 and 09:40 5-minute bars are built from it. oVWAP and sVWAP are volume-weighted typical prices computed here (ETH 18:00-09:29, RTH from 09:30), not the engine VWAP slot, because the engine session may include both. ATR30 is Wilder ATR(14) of the closed Data2 30-minute bars. The opening-5-minute low holds when the 09:40 bar low is at or above the 09:35 bar low. One contract, so the 1R limit and the sVWAP trail are both working and the first exit fills; there is no half-off.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-04 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: two contracts (coding-plan decision 10; catalogue "T1 1 R; runner trails at sVWAP"). sendExits re-sends the stop on both lots and the 1 R T1 limit on one lot while both are open; the runner exits on the stop, a 5-minute close (Data1 bar ending on a 5-minute boundary; Data2 is 30-minute) below sVWAP from 10:00, or the 11:30 flat.
// Exit: the catalogue stop, target or time exit, sent only from sendExits. Not the GX wrapper.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: market next bar, or a stop, as the rule states.
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: two contracts, long only as the rule states.
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
// Data2: 30-minute, read for ATR30 (closed bars, M-MTF rule). The 5-minute bars are built from Data1.
// Further series: Not applicable
// Warmup: the ATR or average named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup and the 1 percent risk-budget check.
// Skip when: the budget fails, a required average is not ready, or the rule's filter fails.
// Entry cutoff: no entry order is sent when its fill bar would end within NoEntryMinutesBeforeFlat (default 25) minutes of the date-aware flat-by cutoff, or after it (catalogue 2.1, early closes included); evening-session entries are allowed.
//
// ## 10. Position sizing and risk
// Size: 2 contracts (coding-plan decision 10); the 1 percent budget is on both. journalArm records the planned entry.
// Risk limit: skip when 2 contracts times the widened stop times BigPointValue exceed 1 percent of equity (an Error is journalled).
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
// Other: ASSUMPTION: Data1 is 1-minute (owner rule); the 09:35 and 09:40 5-minute bars are built from it. oVWAP and sVWAP are volume-weighted typical prices computed here (ETH 18:00-09:29, RTH from 09:30), not the engine VWAP slot, because the engine session may include both. ATR30 is Wilder ATR(14) of the closed Data2 30-minute bars. The opening-5-minute low holds when the 09:40 bar low is at or above the 09:35 bar low. One contract, so the 1R limit and the sVWAP trail are both working and the first exit fills; there is no half-off.
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
// Input: NoEntryMinutesBeforeFlat
// Purpose: no new entry in the last N minutes before the date-aware flat-by cutoff (catalogue 2.1: no new entries after 15:30; flat by 15:55).
// Default: 25 minutes (owner 2026-11-22 22:50 CT).
// Units: minutes
// Range: 0 or more. 0 disables the window (the flat-by cutoff itself still blocks). A negative value acts as 0.
// Effect: an entry order is not sent when its fill bar (the next bar) would end at or after the cutoff minus N minutes. An early close moves it (ES 13:10 ET flat: none from 12:45 ET). Evening-session entries are not affected.
// Interactions: the flat-by cutoff (FlatBy, early-close aware).
// Fixed or research-tuned: fixed
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
// logSignal tag: A2-04
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Data1 is 1-minute (owner rule); the 09:35 and 09:40 5-minute bars are built from it. oVWAP and sVWAP are volume-weighted typical prices computed here (ETH 18:00-09:29, RTH from 09:30), not the engine VWAP slot, because the engine session may include both. ATR30 is Wilder ATR(14) of the closed Data2 30-minute bars. The opening-5-minute low holds when the 09:40 bar low is at or above the 09:35 bar low. One contract, so the 1R limit and the sVWAP trail are both working and the first exit fills; there is no half-off.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_04_OvernightInventory : _VolumeProfileFuturesBase
	{
		public enum EInventoryAtr { A05, A075 }
		[Input] public EInventoryAtr InventoryAtr { get; set; }

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp, m_lxLmt;
		private double m_onNum, m_onDen, m_rthNum, m_rthDen, m_oVwap, m_onClose, m_atr, m_prevBucketClose;
		private double m_stop, m_target, m_open5Low;
		private int m_atrCount;
		private DateTime m_day = DateTime.MinValue;
		// Coder 2026-11-22: Data2 (the native timeframe) read through the M-MTF closed-bar rule. newNativeBar() is true once per new
		// closed Data2 bar; nativeBar(ago) is the ago-th closed Data2 bar; nativeAtr(n) is the mean true range of the last n closed bars.
		private DateTime m_nativeTime = DateTime.MinValue;
		private bool nativeReady(int ago) { return IsExist(2) && BarsOfData(2).CurrentBar > ago + 2; }
		private bool newNativeBar()
		{
			if (!nativeReady(0)) return false;
			DateTime t; double o, h, l, c, v; HtfClosedBar(2, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_nativeTime) return false;
			m_nativeTime = t; return true;
		}
		private void nativeBar(int ago, out double o, out double h, out double l, out double c, out double v) { DateTime t; HtfClosedBar(2, ago, out t, out o, out h, out l, out c, out v); }
		private double nativeAtr(int n)
		{
			if (!nativeReady(n)) return double.NaN;
			double sum = 0, o, h, l, c, v, po, ph, pl, pc, pv;
			for (int k = 0; k < n; k++)
			{
				nativeBar(k, out o, out h, out l, out c, out v); nativeBar(k + 1, out po, out ph, out pl, out pc, out pv);
				sum += Math.Max(h, pc) - Math.Min(l, pc);
			}
			return sum / n;
		}
		private bool m_done, m_latched;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _A2_04_OvernightInventory(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; InventoryAtr = EInventoryAtr.A05; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			FlatBy = new TimeSpan(11, 30, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A204LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A204LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A204LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "A204LT", EOrderAction.Sell, OrderExit.FromAll));   // T1: one of the two lots
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EInventoryAtr), InventoryAtr)) throw new ArgumentException("_A2_04_OvernightInventory: InventoryAtr is not a defined EInventoryAtr");
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
			m_day = DateTime.MinValue; m_atr = double.NaN; m_atrCount = 0; m_prevBucketClose = double.NaN; m_nativeTime = DateTime.MinValue;
		}

		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime et = ToEastern(Bars.Time[0]);
			TimeSpan tod = et.TimeOfDay;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = m_latched = false; m_onNum = m_onDen = m_rthNum = m_rthDen = 0; m_oVwap = m_onClose = double.NaN; m_open5Low = double.NaN; }
			if (rollOn(1)) m_done = true;
			bool eth = tod >= new TimeSpan(18, 0, 0) || tod < new TimeSpan(9, 30, 0);
			double typ = (Bars.High[0] + Bars.Low[0] + Bars.Close[0]) / 3.0;
			double vol = Bars.Volume[0] > 0 ? Bars.Volume[0] : 1;
			if (eth) { m_onNum += typ * vol; m_onDen += vol; m_onClose = Bars.Close[0]; }
			else { m_rthNum += typ * vol; m_rthDen += vol; }
			stepAtr(et);
			int pos = StrategyInfo.MarketPosition;
			bool sendExits()
			{
				if ((mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero)) || (tod >= new TimeSpan(10, 0, 0) && tod.Minutes % 5 == 0 && m_rthDen > 0 && Bars.Close[0] < m_rthNum / m_rthDen)) { m_lx.Send(); return true; }
				// Coder 2026-11-22 (catalogue "runner trails at sVWAP from 10:00 (exit on a 5m close below)"): the trail tests 5-minute closes,
				// the 1-minute bar ending on a 5-minute boundary (Data2 is 30-minute, so 5m closes come from Data1); it tested every 1-minute close.
				// Coder 2026-11-22 (Analyst native retest of #152; catalogue "T1 1 R; runner trails at sVWAP"): T1 takes one of the two lots at
				// 1 R; the runner stays on the stop, the sVWAP trail and the 11:30 flat. The target exited both lots.
				m_lxStp.Send(m_stop); if (Math.Abs(StrategyInfo.MarketPosition) >= 2) m_lxLmt.Send(m_target, 1);
				return false;
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || pos != 0 || tod != new TimeSpan(9, 40, 0)) return;
			if (m_atrCount < 14 || !(m_atr > 0) || !(m_onDen > 0)) { m_done = true; return; }
			SessionStats s = Session;
			if (s == null || !(s.rthOpen > 0) || !(s.ethHigh > 0)) { m_done = true; return; }
			double thr = InventoryAtr == EInventoryAtr.A075 ? 0.75 : 0.5;
			double ov = m_onNum / m_onDen;
			bool inventory = m_onClose - ov > thr * m_atr;
			bool openDrive = s.rthOpen >= s.ethHigh - 0.25 * m_atr;
			// Coder 2026-11-22: Data1 is 1-minute, so the 09:35 and 09:40 5-minute bars are built from it (1-minute bars 09:31-09:35 and
			// 09:36-09:40). Close[1] / Low[1] were the 09:39 minute, not the 09:35 bar.
			double lowB = Math.Min(Math.Min(Math.Min(Bars.Low[0], Bars.Low[1]), Math.Min(Bars.Low[2], Bars.Low[3])), Bars.Low[4]);
			double prevLow = Math.Min(Math.Min(Math.Min(Bars.Low[5], Bars.Low[6]), Math.Min(Bars.Low[7], Bars.Low[8])), Bars.Low[9]);
			double prevClose = Bars.Close[5];
			bool twoAbove = Bars.Close[0] > ov && prevClose > ov && lowB >= prevLow;
			if (!(inventory && openDrive && twoAbove)) { m_done = true; return; }
			double tick = tickSize();
			double structural = Math.Min(lowB, prevLow) - tick;
			double cap = Bars.Close[0] - 0.6 * m_atr;
			double stop = Math.Max(structural, cap);
			double floor = 14 * tick;
			if (Bars.Close[0] - stop < floor) stop = Bars.Close[0] - floor;
			double risk = Bars.Close[0] - stop;
			if (!(risk > 0) || 2 * risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ if (journalOn()) trace(ETraceLevel.Error, "A2-04 skipped: multi-contract risk exceeds 1 percent"); m_done = true; return; }
			if (journalOn()) trace(ETraceLevel.Warning, "multi-contract size 2");
			m_stop = stop; m_target = Bars.Close[0] + risk; m_done = true;
			(entryOpen() ? m_le : null)?.Send(2); m_lxStp.Send(m_stop);
			if (journalOn()) journalArm("A204LE", Bars.Close[0], m_stop, m_target, null);
		}

		private void stepAtr(DateTime et)
		{
			// Coder 2026-11-22: the 30-minute true range is read from the closed Data2 30-minute bars (it was rebuilt from Data1 clock buckets).
			if (!newNativeBar()) return;
			double o, h, l, c, v; nativeBar(0, out o, out h, out l, out c, out v);
			double tr = h - l;
			if (m_prevBucketClose > 0) tr = Math.Max(tr, Math.Max(Math.Abs(h - m_prevBucketClose), Math.Abs(l - m_prevBucketClose)));
			m_atrCount++;
			m_atr = m_atrCount == 1 || double.IsNaN(m_atr) ? tr : (m_atr * 13.0 + tr) / 14.0;
			m_prevBucketClose = c;
		}
	}
}
