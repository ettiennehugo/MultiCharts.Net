// ## 1. Identity
// Catalogue ID: A2-28
// ID mapping: A2-28 -> _A2_28_MinorExcess.Strategy.CS (class _A2_28_MinorExcess).
// Name: A2-28 minor-component excess rejection
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_28_MinorExcess
// File: sandbox/_A2_28_MinorExcess.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-28.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: Prior-session mixture is ProfileMixture K<=2 on SessionBars of the previous RTH day. The minor component is the smaller weight. Two contracts (coding-plan decision 10): GX takes 50 percent at the void minimum (the lowest-volume price between the two means) when it lies before the major mean, and the final target is the major mean, in points from the signal close. The 5-minute confirmation reads closed Data2 5-minute bars (long confirms back above mu_minor + sd_minor, short back below mu_minor - sd_minor). The 1.2 ATR cap is applied to the structural stop before exitPlan. ATR5 is averageTrueRange(Bars, 14) on the 1-minute chart. The trigger is a later 1-minute close beyond the high (long) or low (short) of the second 5-minute bar that closed back through the edge.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-28 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: two contracts (coding-plan decision 10): T1 (50 percent) at the void minimum, the other lot at mu_major.
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
// Data2: the same instrument on 5-minute bars (owner rule: Data2 = native timeframe). CalcBar reads closed Data2 bars only, through HtfClosedBar.
// Further series: Not applicable
// Warmup: the statistic named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup, and the 1 percent budget when an order is sent.
// Skip when: the statistic is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 2 contracts where this file trades; the 1 percent budget is on both. journalArm is written by exitPlan when GX is used.
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
// Exit orders: GX exitPlan on the entry bar and exitBar on each later closed bar. sendExits is not used.
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
// Other: ASSUMPTION: Prior-session mixture is ProfileMixture K<=2 on SessionBars of the previous RTH day. The minor component is the smaller weight. Two contracts (coding-plan decision 10): GX takes 50 percent at the void minimum (the lowest-volume price between the two means) when it lies before the major mean, and the final target is the major mean, in points from the signal close. The 5-minute confirmation reads closed Data2 5-minute bars (long confirms back above mu_minor + sd_minor, short back below mu_minor - sd_minor). The 1.2 ATR cap is applied to the structural stop before exitPlan. ATR5 is averageTrueRange(Bars, 14) on the 1-minute chart. The trigger is a later 1-minute close beyond the high (long) or low (short) of the second 5-minute bar that closed back through the edge.
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
// logSignal tag: A2-28
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Prior-session mixture is ProfileMixture K<=2 on SessionBars of the previous RTH day. The minor component is the smaller weight. Two contracts (coding-plan decision 10): GX takes 50 percent at the void minimum (the lowest-volume price between the two means) when it lies before the major mean, and the final target is the major mean, in points from the signal close. The 5-minute confirmation reads closed Data2 5-minute bars (long confirms back above mu_minor + sd_minor, short back below mu_minor - sd_minor). The 1.2 ATR cap is applied to the structural stop before exitPlan. ATR5 is averageTrueRange(Bars, 14) on the 1-minute chart. The trigger is a later 1-minute close beyond the high (long) or low (short) of the second 5-minute bar that closed back through the edge.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_28_MinorExcess : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se;
		private DateTime m_day = DateTime.MinValue;
		private double m_muMinor, m_sdMinor, m_muMajor, m_edge, m_extreme;
		private int m_side, m_back;
		private bool m_done, m_inMinor;
		private double m_trig = double.NaN, m_void = double.NaN;   // the 1-minute trigger level; the density minimum between the components (T1)

		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;

		public _A2_28_MinorExcess(object _ctx) : base(_ctx) { Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			ProfileStats.enabled = true;
			GxOrderPrefix = "A228"; createGxOrders();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A228LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "A228SE", EOrderAction.SellShort));
		}

		protected override void StartCalc() {
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 m_nativeTime = DateTime.MinValue; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); m_muMinor = double.NaN; m_void = double.NaN; }


		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (sandboxBook()) return;
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			exitBar();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = m_inMinor = false; m_side = m_back = 0; m_extreme = double.NaN; m_trig = double.NaN; loadPrior(day); }
			if (StrategyInfo.MarketPosition != 0 || m_done || double.IsNaN(m_muMinor) || !IsRth()) return;
			// Coder 2026-11-22: long (minor below major, m_side = 1) trades down into the minor (low <= mu + sd) and confirms back above
			// mu + sd; short mirrors at mu - sd (catalogue short case). The signs were swapped, so each side tested the other side's edge.
			double edge = m_muMinor + m_side * m_sdMinor;
			bool inside = m_side > 0 ? Bars.Low[0] <= edge : Bars.High[0] >= edge;
			if (inside) { m_inMinor = true; m_extreme = double.IsNaN(m_extreme) ? (m_side > 0 ? Bars.Low[0] : Bars.High[0]) : (m_side > 0 ? Math.Min(m_extreme, Bars.Low[0]) : Math.Max(m_extreme, Bars.High[0])); }
			step5(edge);
		}

		private void step5(double edge)
		{
			// Coder 2026-11-22: the 5-minute confirmation reads the closed Data2 5-minute bars (owner rule: Data2 = native timeframe).
			// The second 5-minute close back through the edge sets the trigger at that bar's high (long) or low (short), and every later
			// 1-minute close is tested against it (the catalogue's 1m trigger).
			if (newNativeBar() && nativeReady(1))
			{
				double o, h, l, c, v; nativeBar(0, out o, out h, out l, out c, out v);
				bool back = m_side > 0 ? c > edge : c < edge;
				m_back = m_inMinor && back ? m_back + 1 : 0;
				if (m_back == 2) m_trig = m_side > 0 ? h : l;
				else if (m_back == 0) m_trig = double.NaN;
				return;   // the bar that closes the 5-minute bar is not its own trigger
			}
			if (!double.IsNaN(m_trig) && (m_side > 0 ? Bars.Close[0] > m_trig : Bars.Close[0] < m_trig)) { m_trig = double.NaN; enter(); }
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

		private void enter()
		{
			double tick = tickSize();
			double atr = averageTrueRange(Bars, 14);
			if (!(atr > 0) || double.IsNaN(m_extreme)) { m_done = true; return; }
			double entry = Bars.Close[0];
			double structural = m_side > 0 ? m_extreme - tick : m_extreme + tick;
			double cap = entry - m_side * 1.2 * atr;
			if (m_side * (entry - structural) > m_side * (entry - cap)) structural = cap;
			double floor = 14 * tick;
			if (m_side * (entry - structural) < floor) structural = entry - m_side * floor;
			if (!budgetOk(entry, structural)) { if (journalOn()) trace(ETraceLevel.Journal, "A2-28 skip: risk budget"); m_done = true; return; }
			string name = m_side > 0 ? "A228LE" : "A228SE";
			armGx(m_side > 0, entry, structural, m_muMajor, _SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), name);
			if (m_side > 0) { if (hostGate(true)) m_le.Send(2); } else { if (hostGate(false)) m_se.Send(2); }
		}

		private void loadPrior(DateTime day)
		{
			m_muMinor = double.NaN; m_side = 0; m_void = double.NaN;
			DateTime prev = DateTime.MinValue;
			int maxAgo = Math.Min(2000, Bars.CurrentBar);
			for (int ago = 0; ago < maxAgo; ago++)
			{
				RingBar rb = ringBarOf(1, ago);
				if (rb.tradingDay < day && rb.tradingDay != DateTime.MinValue) { prev = rb.tradingDay; break; }
			}
			if (prev == DateTime.MinValue) return;
			List<RingBar> bars = SessionBars(prev, true);
			if (bars == null || bars.Count < 30) return;
			double[] p, v;
			if (!_ProfileStats.Histogram(bars, null, tickSize(), 2, out p, out v)) return;
			ProfileMixtureResult mix = _ProfileStats.ProfileMixture(p, v, 2, _ProfileStats.MixtureSeed(prev), EGmmNeff.Bars, bars.Count, tickSize());
			if (mix == null || mix.K != 2) return;
			int minor = mix.pi[0] <= mix.pi[1] ? 0 : 1;
			int major = 1 - minor;
			if (mix.pi[minor] > 0.30) return;
			m_muMinor = mix.mu[minor]; m_sdMinor = mix.sd[minor]; m_muMajor = mix.mu[major];
			// Coder 2026-11-22 (Analyst native retest of #152: both lots always exited together, 0 partials): T1 is the mixture's density
			// minimum between the two components (ProfileMixture voidPrice, the GMM void). The raw tick histogram's lowest-volume row (ties
			// to the lowest price) sat at a thin row at or behind the entry, so T1 was never ahead of the entry and GX fell back to
			// SingleTarget on every trade.
			if (mix.voidPrice.Length > 0 && !double.IsNaN(mix.voidPrice[0])) m_void = mix.voidPrice[0];
			m_side = mix.mu[minor] < mix.mu[major] ? 1 : -1;
		}

		private bool budgetOk(double entry, double stop)
		{   // Coder 2026-11-22: the entry is 2 contracts (coding-plan decision 10), so the 1 percent budget is on 2 contracts (it tested 1)
			double risk = Math.Abs(entry - stop);
			if (journalOn()) trace(ETraceLevel.Warning, "A2-28 multi-contract size 2");
			bool ok = risk > 0 && 2 * risk * Bars.Info.BigPointValue <= equityAvailable(0.01);
			if (!ok && journalOn()) trace(ETraceLevel.Error, "A2-28 skipped: multi-contract risk exceeds 1 percent");
			return ok;
		}

		private void armGx(bool longSide, double entry, double stop, double target, int flatHHMM, string name)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			// Coder 2026-11-22 (catalogue: "Target: mu_major (T1 50% at the void minimum)"): 2 lots, T1 1 lot at the void minimum when it
			// lies between the entry and mu_major, the other lot at mu_major.
			double t1 = longSide ? m_void - entry : entry - m_void, fin = Math.Abs(target - entry);
			bool scale = !double.IsNaN(m_void) && t1 > 0 && t1 < fin;
			if (!scale && journalOn()) trace(ETraceLevel.Journal, "A2-28 void " + m_void + " not between the entry and mu_major: both lots at mu_major");
			GxConfig.TargetMode = scale ? ETargetMode.ScaleOutThenRunner : ETargetMode.SingleTarget;
			GxConfig.T1Unit = ETriggerUnit.Points; GxConfig.T1Value = scale ? t1 : 0; GxConfig.T1ScalePct = 50;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = fin;
			GxConfig.BreakEven = EBreakEven.Off;
			GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = ESessionExit.FlatBy;
			GxConfig.FlatByHHMM = flatHHMM;
			GxConfig.TimeStopBars = 0;
			GxConfig.TimeStopMinutes = 0;
			GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket();
			mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop;
			mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1; mkt.ResolutionLabel = "1 min";
			exitPlan(longSide, entry, stop, 2, mkt, name);   // the plan covers both contracts sent with Send(2)
		}

		// Coder 2026-11-22 (Analyst native retest of #152: A2-11 / A2-32 entries filled after the 15:55 ET flat-by cutoff): a study whose
		// position is flat by the session cutoff sends no entry on or after the cutoff bar (session end minus 5 minutes, early-close
		// aware) until 18:00 ET. Entries that GX holds overnight (SessionExit HoldOvernight) are not blocked.
		private DateTime m_cutoffLogDay = DateTime.MinValue;
		private bool entryCutoff()
		{
			if (GxConfig.SessionExit == ESessionExit.HoldOvernight || !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)) return false;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_cutoffLogDay) { m_cutoffLogDay = day; if (journalOn()) trace(ETraceLevel.Journal, "entry blocked: at or after the flat-by cutoff"); }
			return true;
		}
		private bool hostGate(bool buy)
		{
			if (entryCutoff()) return false;   // Coder 2026-11-22: no entry on or after the flat-by cutoff bar (it would fill after the cutoff)
			if (Gate == ESandboxGate.Off) return true;
			double poc = double.NaN, sma = double.NaN, prev = double.NaN, atr = double.NaN, hi = Bars.High[0], lo = Bars.Low[0];
			int fam = 0, ranges = 0;
			string notReady;
			try
			{
				SessionStats s = Session;
				if (s != null) { hi = s.rthHigh; lo = s.rthLow; prev = s.priorRthClose; }
				atr = averageTrueRange(Bars, 14);
				if (Gate == ESandboxGate.ValueMigration) { VpExtra prior = PriorRthProfile(); if (prior != null) poc = prior.poc; }
				if (_SandboxRules.GateReadsDaily(Gate) && NeedsDaily) { _DailyContext d = DailyContext; if (d != null) sma = d.Sma200; }
				if (Gate == ESandboxGate.ThreeFamily && DvpConfig.enabled) dvpFamiliesAt(1, Bars.Close[0], 0.3, out fam, out ranges);
				notReady = _SandboxRules.GateNotReady(Gate, poc, sma, prev, atr, DvpConfig.enabled);
			}
			catch (Exception ex) { notReady = ex.GetType().Name; }
			if (notReady != null) { gateWarn(notReady); return false; }
			bool ok = _SandboxRules.GateAllows(Gate, buy, Bars.Close[0], poc, hi, lo, atr, sma, prev, fam);
			gateLog(ok, buy);
			return ok;
		}

		// Gate dependencies (owner-approved follow-up to batch 1). Gate is an input, so what it reads is turned on in StartCalc,
		// before base.StartCalc builds the engines. Gate = Off changes nothing; switching back to Off restores what was forced.
		private bool m_gateOwnForced, m_gateDailyForced, m_gateDvpForced;
		private EDevelopingSource m_gateSrcWas; private EOwnProfileWindow m_gateWinWas;
		private DateTime m_gateWarnDay = DateTime.MinValue;
		private void applyGateNeeds()
		{
			if (m_gateOwnForced) { DevelopingSource = m_gateSrcWas; OwnProfileWindow = m_gateWinWas; m_gateOwnForced = false; }
			if (m_gateDailyForced) { NeedsDaily = false; m_gateDailyForced = false; }
			if (m_gateDvpForced) { DvpConfig.enabled = false; m_gateDvpForced = false; }
			m_gateWarnDay = m_gateLogDay = DateTime.MinValue;
			bool own, daily, dvp;
			_SandboxRules.GateNeeds(Gate, false, out own, out daily, out dvp);
			if (own && !(DevelopingSource == EDevelopingSource.Own && OwnProfileWindow == EOwnProfileWindow.Rth))
			{ m_gateSrcWas = DevelopingSource; m_gateWinWas = OwnProfileWindow; DevelopingSource = EDevelopingSource.Own; OwnProfileWindow = EOwnProfileWindow.Rth; m_gateOwnForced = true; }
			if (daily && !NeedsDaily) { NeedsDaily = true; m_gateDailyForced = true; }
			if (dvp && !DvpConfig.enabled) { DvpConfig.enabled = true; m_gateDvpForced = true; }
			if (Gate != ESandboxGate.Off && traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, "gate " + Gate + " needs ownRth(" + own + ") daily(" + daily + ") dvp(" + dvp + ") forced(" + m_gateOwnForced + "," + m_gateDailyForced + "," + m_gateDvpForced + ")");
		}

		// The decision is journalled when it changes within a Globex day, not on every bar.
		private DateTime m_gateLogDay = DateTime.MinValue; private int m_gateLogState;
		private void gateLog(bool ok, bool buy)
		{
			int st = (ok ? 1 : 2) + (buy ? 0 : 2);
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day == m_gateLogDay && st == m_gateLogState) return;
			m_gateLogDay = day; m_gateLogState = st;
			if (journalOn()) trace(ETraceLevel.Journal, "gate " + Gate + (ok ? " allowed " : " blocked ") + (buy ? "buy" : "sell"));
		}

		// A gate whose data is not ready blocks; the reason is a Warning once per Globex day.
		private void gateWarn(string why)
		{
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day == m_gateWarnDay) return;
			m_gateWarnDay = day;
			if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "gate " + Gate + " not ready (" + why + "): blocked");
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


		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
			}
			else
			{
			}
			return true;
		}

	}
}