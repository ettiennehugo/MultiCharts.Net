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
// Research assumption: ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of today's developing RTH histogram, sampled on 5-minute boundaries. The 90th percentile uses an in-file list of the last 60 same-slot slopes. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
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
// Data2: named in section 22, or Not applicable.
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
// Other: ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of today's developing RTH histogram, sampled on 5-minute boundaries. The 90th percentile uses an in-file list of the last 60 same-slot slopes. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
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
// ASSUMPTION: The balance is the prior two RTH sessions stored in this file, not a composite call. H_norm is the Miller-Madow normalised entropy of today's developing RTH histogram, sampled on 5-minute boundaries. The 90th percentile uses an in-file list of the last 60 same-slot slopes. The stop and the 1.0 range target and the 15:55 flat would map to GX. The exit on a 5-minute close back inside the balance is not a GX trail mode, so the whole exit stays in sendExits.
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
		private readonly List<double> m_slopes = new List<double>();
		private double m_hi, m_lo, m_prevH, m_stop, m_target, m_hNow;
		private DateTime m_day = DateTime.MinValue;
		private int m_side;
		private bool m_alert, m_done;
		private DateTime m_alertAt = DateTime.MinValue;

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
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd);
			base.StartCalc();
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (day != m_day) { remember(); m_day = day; m_alert = m_done = false; m_side = 0; m_prevH = double.NaN; }
			int pos = StrategyInfo.MarketPosition;
			bool inside = !double.IsNaN(m_hi) && Bars.Close[0] < m_hi && Bars.Close[0] > m_lo;
			bool sendExits()
			{
				if (mustFlatten() || inside) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return false;
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || pos != 0 || !IsRth() || rollOn(1) || double.IsNaN(m_hi)) return;
			if (tod.Minutes % 5 == 0) sample(tod);
			if (!m_alert || (Bars.Time[0] - m_alertAt).TotalMinutes > 60) return;
			bool outUp = Bars.Close[0] > m_hi && Bars.Close[1] > m_hi;
			bool outDn = Bars.Close[0] < m_lo && Bars.Close[1] < m_lo;
			if (!outUp && !outDn) return;
			m_side = outUp ? 1 : -1;
			double entry = Bars.Close[0];
			double range = m_hi - m_lo;
			double tick = tickSize();
			double stop = m_side > 0 ? m_hi - 0.25 * range : m_lo + 0.25 * range;
			double floor = 14 * tick;
			if (m_side * (entry - stop) < floor) stop = entry - m_side * floor;
			double risk = Math.Abs(entry - stop);
			if (!(risk > 0) || risk * Bars.Info.BigPointValue > equityAvailable(0.01)) { m_done = true; return; }
			m_stop = stop; m_target = m_side > 0 ? m_hi + range : m_lo - range; m_done = true;
			if (m_side > 0) m_le.Send(); else m_se.Send();
			if (journalOn()) journalArm(m_side > 0 ? "A230LE" : "A230SE", entry, m_stop, m_target, null);
		}

		private void sample(TimeSpan tod)
		{
			List<RingBar> bars = SessionBars(m_day, true);
			if (bars == null || bars.Count < 10) return;
			double[] p, v;
			if (!_ProfileStats.Histogram(bars, null, tickSize(), 2, out p, out v)) return;
			EntropyResult e = _ProfileStats.Entropy(v, EEntropyRows.DenseRange, bars.Count);
			if (double.IsNaN(e.Hnorm)) return;
			if (!double.IsNaN(m_prevH))
			{
				double slope = e.Hnorm - m_prevH;
				if (m_slopes.Count >= 60)
				{
					var copy = new List<double>(m_slopes); copy.Sort();
					double p90 = copy[(int)(0.9 * (copy.Count - 1))];
					if (slope > p90 && tod >= new TimeSpan(10, 30, 0)) { m_alert = true; m_alertAt = Bars.Time[0]; }
				}
				m_slopes.Add(slope); if (m_slopes.Count > 60) m_slopes.RemoveAt(0);
			}
			m_prevH = e.Hnorm;
		}

		private void remember()
		{
			SessionStats s = Session;
			if (s == null || !(s.rthHigh > 0)) return;
			if (double.IsNaN(m_hi)) { m_hi = s.rthHigh; m_lo = s.rthLow; return; }
			m_hi = Math.Max(m_hi, s.priorRthHigh > 0 ? s.priorRthHigh : m_hi);
			m_lo = Math.Min(m_lo, s.priorRthLow > 0 ? s.priorRthLow : m_lo);
			m_hi = Math.Max(m_hi, s.rthHigh); m_lo = Math.Min(m_lo, s.rthLow);
		}
	}
}
