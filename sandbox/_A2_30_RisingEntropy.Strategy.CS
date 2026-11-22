// ## 1. Identity
// Catalogue ID: A2-30
// ID mapping: A2-30 -> _A2_30_RisingEntropy.Strategy.CS (class _A2_30_RisingEntropy).
// Name: A2-30 rising-entropy balance break
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_30_RisingEntropy
// File: sandbox/_A2_30_RisingEntropy.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-30.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of the histogram of the last 30 minutes of today's RTH bars, sampled on 5-minute boundaries; H_slope is the change from the sample 15 minutes earlier. The 90th percentile is over the last 60 sessions' slopes at the same clock slot. The breakout is 2 consecutive closed Data2 5-minute closes outside the balance within 60 minutes of the alert, entered at the next 5-minute open. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-30 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: one contract.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades.
// Can fill: the next bar. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one.
// Re-entry: one attempt per session where the file trades.
// Same session after a stop: no new entry that session.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: ES or NQ as the catalogue says. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 5-minute.
// Data1: 1-minute unless section 22 says daily. Tests use data through 2024-12-31 only.
// Data2: the same instrument on 5-minute bars (owner rule: Data2 = native timeframe). CalcBar reads closed Data2 bars for the breakout and the trail, through HtfClosedBar.
// Further series: Not applicable
// Warmup: the statistic named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup, and the 1 percent budget when an order is sent.
// Skip when: the statistic is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract where this file trades. journalArm is written by exitPlan when GX is used.
// Risk limit: skip when 1 contract times the stop distance times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create, when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar, or sendExits, as section 13 says.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. ASSUMPTION/NOTE: a 5-minute close back inside the balance is not a GX trail mode (Chandelier, ATR, bar extreme, EMA, or roll level).
// Hard stop: the catalogue stop when this file trades.
// Target: the catalogue target when GX or sendExits names one.
// Time exit: the catalogue flat time.
// Invalidation: the catalogue skip.
//
// ## 14. Regimes
// Suitable: the catalogue window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series where this file trades.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of the histogram of the last 30 minutes of today's RTH bars, sampled on 5-minute boundaries; H_slope is the change from the sample 15 minutes earlier. The 90th percentile is over the last 60 sessions' slopes at the same clock slot. The breakout is 2 consecutive closed Data2 5-minute closes outside the balance within 60 minutes of the alert, entered at the next 5-minute open. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name, when an entry is sent.
// logSignal tag: A2-30
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of the histogram of the last 30 minutes of today's RTH bars, sampled on 5-minute boundaries; H_slope is the change from the sample 15 minutes earlier. The 90th percentile is over the last 60 sessions' slopes at the same clock slot. The breakout is 2 consecutive closed Data2 5-minute closes outside the balance within 60 minutes of the alert, entered at the next 5-minute open. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_30_RisingEntropy : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private readonly Dictionary<int, List<double>> m_slopes = new Dictionary<int, List<double>>();   // H_slope history per clock slot (60 sessions)
		private readonly Dictionary<int, double> m_hDay = new Dictionary<int, double>();   // today's H_norm per clock slot
		private int m_outUp, m_outDn;
		private double m_hi = double.NaN, m_lo = double.NaN, m_stop, m_target;
		private double m_p1Hi = double.NaN, m_p1Lo = double.NaN, m_p1Close = double.NaN, m_p2Hi = double.NaN, m_p2Lo = double.NaN;
		private DateTime m_day = DateTime.MinValue;
		private int m_side;
		private bool m_alert, m_done;
		private DateTime m_alertAt = DateTime.MinValue;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_30_RisingEntropy(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			ProfileStats.enabled = true;
			FlatBy = new TimeSpan(15, 55, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A230LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A230SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A230LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A230SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A230LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A230SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A230LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A230ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			/* Coder 2026-11-22: a recalc reuses this object, so the history from the previous pass is cleared (no lookahead from a reused list) */ m_slopes.Clear(); m_hDay.Clear(); m_nativeTime = DateTime.MinValue; m_hi = m_lo = double.NaN; m_p1Hi = m_p1Lo = m_p1Close = m_p2Hi = m_p2Lo = double.NaN; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
		}
		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd); }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime day = GlobexDate(Bars.Time[0]);
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (day != m_day) { if (IsNoRthDay(day)) m_hi = m_lo = double.NaN; else remember(); m_day = day;   /* Analyst MC17: a no-RTH Globex date (holiday halt, abbreviated Good Friday) is not a trading day; the next RTH date logs the balance */ m_alert = m_done = false; m_filledSetup = false; m_restOn = false; m_side = 0; m_hDay.Clear(); m_outUp = m_outDn = 0; }
			int pos = StrategyInfo.MarketPosition;
			// Coder 2026-11-22 (catalogue Rev 6): the breakout and the trail use closed Data2 5-minute closes ("2 consecutive 5m closes
			// outside the balance range; enter at the next 5m open"; "trail on a 5m close back inside"); they used 1-minute closes.
			bool new5 = newNativeBar(); double c5 = double.NaN;
			if (new5) { double o, h, l, v; nativeBar(0, out o, out h, out l, out c5, out v); }
			bool inside = new5 && !double.IsNaN(m_hi) && c5 < m_hi && c5 > m_lo;
			if (IsRth() && tod.Minutes % 5 == 0) sample(tod);   // sampled every RTH 5 minutes, flat or not, so the same-time history is complete
			bool sendExits()
			{
				if ((mustFlatten() || _SandboxRules.EarlyFlatDue(ToEastern(Bars.Time[0]), Bars.Info.Name, TimeSpan.Zero)) || inside) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return false;
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || pos != 0 || !IsRth() || rollOn(1) || double.IsNaN(m_hi) || !new5) return;
			if (!m_alert || (Bars.Time[0] - m_alertAt).TotalMinutes > 60) { m_outUp = m_outDn = 0; return; }
			m_outUp = c5 > m_hi ? m_outUp + 1 : 0;
			m_outDn = c5 < m_lo ? m_outDn + 1 : 0;
			bool outUp = m_outUp >= 2;
			bool outDn = m_outDn >= 2;
			if (!outUp && !outDn) return;
			m_side = outUp ? 1 : -1;
			double entry = c5;
			double range = m_hi - m_lo;
			double tick = tickSize();
			double stop = m_side > 0 ? m_hi - 0.25 * range : m_lo + 0.25 * range;
			double floor = 14 * tick;
			if (m_side * (entry - stop) < floor) stop = entry - m_side * floor;
			double risk = Math.Abs(entry - stop);
			if (!(risk > 0) || risk * Bars.Info.BigPointValue > equityAvailable(0.01)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-30 {m_day:yyyy-MM-dd} skip: stop {risk / tick:F0} ticks breaches risk budget"); m_done = true; return; }
			m_stop = stop; m_target = m_side > 0 ? m_hi + range : m_lo - range; m_done = true;
			if (m_side > 0) { (entryOpen() ? m_le : null)?.Send(); m_lxStp.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(); m_sxStp.Send(m_stop); }
			if (journalOn()) journalArm(m_side > 0 ? "A230LE" : "A230SE", entry, m_stop, m_target, null);
		}
		// Coder 2026-11-22 (catalogue: "rolling 30-minute H_norm of today's profile, sampled every 5 minutes; H_slope = 15-minute change.
		// Alert when H_slope > its 90th percentile (trailing 60 sessions, same time of day)"): the profile is the last 30 minutes of
		// today's RTH bars, the slope is against the sample 15 minutes earlier, and the percentile is over that clock slot's last 60 sessions.
		private void sample(TimeSpan tod)
		{
			List<RingBar> bars = SessionBars(m_day, true);
			if (bars == null) return;
			DateTime from = Bars.Time[0].AddMinutes(-30);
			List<RingBar> win = bars.FindAll(b => b.time > from);
			if (win.Count < 10) return;
			double[] p, v;
			if (!_ProfileStats.Histogram(win, null, tickSize(), 2, out p, out v)) return;
			EntropyResult e = _ProfileStats.Entropy(v, EEntropyRows.DenseRange, win.Count);
			if (double.IsNaN(e.Hnorm)) return;
			int slot = (int)tod.TotalMinutes;
			m_hDay[slot] = e.Hnorm;
			double prev;
			if (!m_hDay.TryGetValue(slot - 15, out prev)) return;
			double slope = e.Hnorm - prev;
			List<double> hist;
			if (!m_slopes.TryGetValue(slot, out hist)) { hist = new List<double>(); m_slopes[slot] = hist; }
			if (hist.Count >= 60)
			{
				var copy = new List<double>(hist); copy.Sort();
				double p90 = copy[(int)(0.9 * (copy.Count - 1))];
				if (slope > p90 && tod >= new TimeSpan(10, 30, 0)) { m_alert = true; m_alertAt = Bars.Time[0]; }
			}
			hist.Add(slope); if (hist.Count > 60) hist.RemoveAt(0);
		}

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

		private void remember()
		{
			m_hi = m_lo = double.NaN;
			SessionStats s = Session;
			if (s == null || !(s.priorRthHigh > 0) || !(s.priorRthLow > 0)) return;
			bool same = s.priorRthHigh == m_p1Hi && s.priorRthLow == m_p1Lo && s.priorRthClose == m_p1Close;
			if (!same) { m_p2Hi = m_p1Hi; m_p2Lo = m_p1Lo; m_p1Hi = s.priorRthHigh; m_p1Lo = s.priorRthLow; m_p1Close = s.priorRthClose; }
			if (double.IsNaN(m_p2Hi)) return;
			m_hi = Math.Max(m_p1Hi, m_p2Hi); m_lo = Math.Min(m_p1Lo, m_p2Lo);
			// Analyst MC17: 515 lines over 502 sessions came from Globex dates with no RTH (holidays) re-logging the carried balance;
			// log only when a new RTH session has shifted the balance, so there is at most one line per trading day.
			if (!same && journalOn()) trace(ETraceLevel.Journal, $"A2-30 {GlobexDate(Bars.Time[0]):yyyy-MM-dd} balance {m_lo:F2}-{m_hi:F2} (prior two RTH sessions)");
		}
	}
}
