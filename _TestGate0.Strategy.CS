using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	// ================================================================================================================
	// _TestGate0 — Gate-0 baseline measurement (catalogue Gate 0: level baseline via A2-33 _LevelStudy + G0-b canaries)
	// ================================================================================================================
	//
	// WHAT IT IS
	//   A measurement strategy, not a trading idea. In the level-study modes it never takes a position: it builds the
	//   Gate-0 level families each RTH session, scores every level type with A2-33's matched-control reaction edge
	//   (20 controls per level) and journals PASS / PARK per level type. In Canary mode it trades one deliberately naive "known loser" rule with
	//   1 contract; a canary whose NET profit factor is > 1.0 means the backtest plumbing (fills, costs, lookahead,
	//   session handling) has a bug — it is not an edge.
	//
	// HELPER FILES TO IMPORT FIRST (PowerLanguage .NET Editor → File → Import, then compile)
	//   Already in the repo (import if your MultiCharts install does not have them yet, in this order):
	//     _Rng.Function.CS            (SplitMix64 seeded RNG: controls, negative controls, bootstrap)
	//     _LevelStudy.Function.CS     (A2-33 level-study harness, PR #15)
	//     _BarRing.Function.CS, _ProfileMath.Function.CS, _ProfileStats.Function.CS (KDE h_cv for the HVN/LVN sensitivity arm, PR #18),
	//     _Gmm.Function.CS (shared EM core used by the GMM / sigma mixture families 12 and 13)
	//     _LevelRef.Function.CS, _RollDetector.Function.CS (roll window / roll detection; PR #19 branch)
	//     the _CustomSignal / _VolumeProfileBase / _VolumeProfileFuturesBase chain and their helper functions
	//   New with this strategy:
	//     _Gate0.Function.CS          (the pure Gate-0 engine: level families, KDE/GMM, DV sources, bootstrap, verdicts, canaries)
	//     _TestGate0.Strategy.CS      (this file)
	//
	// CHART AND DATA SETUP
	//   Instrument : @ES (CME E-mini S&P 500 continuous), NOT back-adjusted (dxFeed default; the levels and roll logic
	//                assume raw prices). Other CME index futures (NQ, RTY, YM) work; tick size is read from the symbol.
	//   Data1      : 1-minute bars (recommended: the dev window 2016–2024 only exists as 1m history) or tick bars
	//                (only ~2 years of ticks exist, mostly after the dev window). The engine re-aggregates Data1 into
	//                1m / 5m / 30m buckets itself, so tick, second or 1-minute Data1 all work; do not use bars longer
	//                than 1 minute.
	//   Data2      : not used. Leave it off the chart (nothing reads it).
	//   Session    : "CME Globex" session template (18:00–17:00 ET). RTH (09:30–16:00 ET) is derived internally.
	//   Time zone  : any; bar times are converted to US Eastern by _VolumeProfileFuturesBase.
	//   Bar Magnifier (Format Signals → Properties → Backtesting):
	//     - Level-study modes: not needed (no orders).
	//     - Canary mode on 1-minute Data1: turn Bar Magnifier ON at 1 tick where tick history exists (2024+); before
	//       that MultiCharts falls back to its intrabar O-H-L-C path assumption. Canaries that rest stop/limit
	//       orders (A2-24 i–iii, DV-10, DV-01, PS-03, A2-53 reversal) need it for honest stop-vs-target ordering.
	//     - Limit fills: choose "fill limit order when the trade price goes beyond the limit price" (1 tick) if your
	//       MultiCharts version offers it (DV-01 / DV-10 require a 1-tick trade-through; G0-a cost model).
	//   Max bars back : 1 is enough (all state is internal); leave the default.
	//
	// DATE RANGE (development window only)
	//   Load 2016-01-01 (or the first 1m bar available) through 2024-12-31. The engine hard-stops at 2025-01-01:
	//   nothing is registered, scored or traded on a trading day >= 2025-01-01 even if more data is loaded, so the
	//   2025+ holdout stays unseen. Allow ~5 months of warm-up: ATR20d needs 20 RTH sessions, HV100 (A2-53) ~100.
	//
	// INPUTS (two only; nothing was added to _CustomSignal)
	//   Mode    : Strategy | EventStudy | ControlRT | ControlFL | Canary   (EGate0Mode)
	//   Variant : int; meaning depends on Mode (tables below)
	//   Everything else is a pre-registered constant in _Gate0.Function.CS (seed 33, VA 70%, 500 bootstrap draws in blocks
	//   of 5, A2-33: 20 controls per level, reaction window 12 x 5m bars after the first touch, sigma = ATR14(5m) frozen at
	//   the RTH open, max level age 15, composite length CompositeSessions = 5, KDE bandwidth 6 ticks / prominence 10%).
	//   Also set the inherited trace level input to Journal (ETraceLevel.Journal) or nothing is printed.
	//
	// MODE
	//   Strategy   : NOT RUNNABLE - StartCalc throws "use EventStudy/Control*/Canary". Kept only so the Mode list matches
	//                the Gate-0 plan. The default Mode is EventStudy.
	//   EventStudy : the real level families of Variant through _LevelStudy (A2-33). Each level registered at the RTH open
	//                gets 20 matched controls at 0.9-1.1x its distance from the RTH open price, random side (_Rng seeded).
	//   ControlRT  : negative control, "random time": every level is replaced by the 1m close at a random bar of the
	//                prior RTH session (same family label prefixed RT:). Expected verdict: PARK. A PASS = bug.
	//   ControlFL  : negative control, "fake level": every level is shifted by ±{0.5, 1, 2} x ATR14(5m), chosen by
	//                _Rng (labels prefixed FL:). Expected verdict: PARK. A PASS = bug.
	//   Canary     : G0-b canary number Variant, 1 contract, MultiCharts commission + slippage. Net PF > 1.0 = bug.
	//
	// VARIANT in Strategy / EventStudy / ControlRT / ControlFL (level family; types journaled in brackets)
	//    0 All families at once - OVERVIEW ONLY, NOT A VERDICT RUN: controls drop to 5 per level for memory and every
	//      result line is prefixed "NOT A VERDICT RUN". Run Variant 1..13 one by one for G0-d.
	//    1 Prior profile          [POC, VAH, VAL] of the prior RTH sessions, age 1..15
	//    2 Naked POCs             [NPOC] prior RTH POCs not traded since they formed, age 2..15
	//    3 Initial balance        [IBH, IBL] prior session 09:30-10:30 high / low
	//    4 Overnight              [ONH, ONL] this morning's Globex overnight high / low (18:00 -> 09:30)
	//    5 AVWAP anchors          [AV_PH, AV_PL] from the prior RTH high / low bar, [AV_WK, AV_MO] week / month,
	//                             [AV_PC] prior RTH close; all valued at the RTH open. AV_OPEN (prior RTH open) is
	//                             RETIRED (= the prior session VWAP, type VWAP in Variant 6); Holm m = 36
	//    6 VWAP                   [VWAP, VWAP+1, VWAP-1] prior RTH session VWAP and +-1 sigma at its close
	//    7 Weekly profile         [WPOC, WVAH, WVAL] prior week (the 09:30-11:30 range profile was dropped)
	//    8 Composite value area   [CVAH, CVAL] composite of the last CompositeSessions = 5 RTH sessions (pre-registered)
	//    9 Single prints          [SP_HI, SP_LO] PRIMARY: interior runs of >= 4 rows touched by ONE 30m period (TPO count 1,
	//                             built from 1m bars) with a non-thin row on both sides (tails are never single prints);
	//                             [SPV_HI, SPV_LO] sensitivity arm: the same interior rule with volume <= 5% of the POC row
	//   10 DV level sources       [DV_ROT_POC_R6, DV_ROT_POC_R12] rotation of >= 6 / 12 RTH 30m bars with H-L <= 1.5 x
	//                             ATR30(14), then a breakout bar closing outside with range >= 1.5 x ATR30, then follow-through
	//                             >= 1 box width before a retest of the POC; level = rotation POC, valid 5 sessions.
	//                             [DV_TREND_HVN] trend leg (30m close beyond the prior 20-bar high/low, leg from the 21-bar
	//                             extreme, range >= 3 x ATR30): KDE HVN of the leg profile with row volume >= 2 x the leg's
	//                             median row; armed after 2 closed 30m bars fully beyond it, expires after 10 sessions.
	//   11 HVN / LVN              [HVN, LVN] PRIMARY: KDE (bandwidth 6 ticks fixed, prominence >= 10%) of the prior RTH
	//                             profile; [HVN_HCV, LVN_HCV] SENSITIVITY arm: same with bandwidth = the prior session's h_cv
	//                             (_ProfileStats.KdeSweep, PR #18); quality = prominence (A2-33 terciles)
	//   12 Mixture (A2-27)        [GMM_MU, GMM_EDGE] volume-weighted GMM (K <= 4 by BIC) of the prior RTH profile: means and
	//                             mu +- sigma edges; quality = weight pi
	//   13 Sigma mixture (A2-32)  [SIG_MU, SIG_EDGE] GMM of the last 20 sessions in z = (p - sVWAP) / sigma_session
	//                             (Parkinson on 5m RTH bars, >= 60 bars), translated with the prior VWAP and EWMA(10) sigma
	//   Roll honesty (A2-39, Reset): all multi-session sources restart at the roll; events on the session BEFORE a roll,
	//   the roll session and the session after it are dropped before scoring.
	//
	// VARIANT in Canary mode (G0-b; all flat by 15:55 ET, RTH only, roll session and the session after skipped live; the
	// session before a roll is dropped from the "ex-roll" totals afterwards)
	//    0 VW-08      long on a 1m close above session (RTH) VWAP, short below, reverse on the opposite close
	//    1 A2-24 (i)  fade the first session-VWAP touch (after 09:35), stop / target 1R = ATR14(5m)
	//    2 A2-24 (ii) fade a 1m close beyond VWAP +-2 sigma, target VWAP frozen at entry, stop 1:1
	//    3 A2-24(iii) fade a 1m close >= 2 x ATR14(5m) from VWAP, target VWAP frozen, stop 1:1
	//    4 DV-10      (A2-24 arm iv) weekly VWAP +-1 sigma fade when both bands moved < 0.5 x ATR30m over 8 x 30m bars;
	//                 limit at the band, TP weekly VWAP, SL 0.20 x ATR20d
	//    5 A2-53      default cell (= 108): RTH gap >= 0.5%, vol filter on (prior inside day INCLUSIVE H<=H[-1] and
	//                 L>=L[-1], or HV6 < HV100); after the j=1st 30m bar, if the prior close was not retraced, fade at
	//                 market; stop-and-reverse at the session extreme +-1 tick (one reversal)
	//    6 DV-01      prior RTH POC, after a move-away >= 1 x ATR14(5m) (a gap counts); limit at the POC on the first
	//                 touch only; SL = TP = 0.15 x ATR20d
	//    7 PS-03      naive 80% rule: open outside the prior value area, enter on the first 30m close back inside,
	//                 target the prior POC, stop 0.25 x VA width beyond the entry edge
	//  100..123 A2-53 sweep: idx = Variant - 100; gap g = {0.3, 0.5, 0.8}%[idx / 8]; j = 1 + (idx / 4) % 2;
	//                 bar = 30m if (idx / 2) % 2 == 0 else 15m; vol filter ON if idx % 2 == 0 else OFF.
	//                 e.g. 100 = 0.3% j1 30m on, 101 = same filter off, 102 = 0.3% j1 15m on, 104 = 0.3% j2 30m on,
	//                 108 = 0.5% j1 30m on (default), 123 = 0.8% j2 15m off.
	//
	// COMMISSION AND SLIPPAGE FOR THE CANARIES (Format Signals → Properties)
	//   Commission: $2.25 per contract per side ($4.50 round trip, IBKR ES incl. fees).
	//   Slippage  : 0.5 tick per contract per side ($6.25 on ES, $2.50 NQ). Trade size: fixed 1 contract.
	//   Round trip: ES $4.50 + $12.50 = $17.00 (catalogue §2.1: IBKR round trip + 1 tick = $16.98). Before 2026-10-23 this
	//               read 1 tick per side ($29.50 round trip): Gate 0 canary runs made with that setting were over-costed.
	//   The canary verdict is the Strategy Performance Report NET profit factor over 2016–2024 (or your dev window).
	//
	// WHERE THE RESULTS SHOW UP AND HOW TO READ THEM
	//   At StartCalc (level-study modes) a "G0 PRE-REGISTERED:" line records the DV rotation types and HVN/LVN bandwidth arms.
	//   With the trace level at Journal, the PowerLanguage Output window / the _CustomSignal log file receive, once at
	//   the first bar of 2025 (or the last bar on the chart, whichever comes first):
	//     A2-33 <Summary>   the _LevelStudy result per type (touch edge, reaction edge, Holm, half-life, terciles, KM)
	//     G0 type(T) levels(n) touched(t) pairs(p) edgeR(e) t() p() holm() ci95[lo,hi] sessions(s) halfLife(h)
	//        tTercile(lo/mid/hi) tByAge[age:t/n ...] A2-33(rule) => PASS | PARK (reason)
	//   R        = max over the 12 5m bars after the first touch of |close - level| / sigma, sigma = ATR14(5m) at the RTH open.
	//   edgeR    = mean over touched levels of R(level) - mean R(its touched controls). t / p from _LevelStudy; holm = Holm
	//              step-down across ALL Gate-0 level types (m fixed at the full type count, so per-family runs are conservative).
	//   tByAge   = t per age 1..15; an "x" marks |t| < 2 (that age is not traded). t > 3 with decay -> keep up to the half-life.
	//   tTercile = t in the low / mid / top quality tercile (HVN/LVN prominence, GMM weight; other types have no quality).
	//   ci95     = 2.5 / 97.5 percentiles of edgeR over 500 moving-block bootstrap draws (blocks of 5 consecutive sessions).
	//   verdict  (§2.3, n = pairs): n < 30 PARK (no evaluation); 30 <= n < 150 PARK (inconclusive); n >= 150: PASS on
	//              A2-33 "keep..." with |t| >= 2, PASS on "trade top-quality tercile only", else PARK (kill). Variant 0
	//              lines are prefixed "NOT A VERDICT RUN".
	//   Canary mode journals each fill and closed trade, then totals: all, ex-roll window (session before / of / after a
	//   roll), 2016-2024 and 2019-2024 (A2-24's own window). Read the NET PF in the Strategy Performance Report too:
	//   > 1.0 = BUG to investigate, <= 1.0 = plumbing behaves.
	// ================================================================================================================
	public class _TestGate0 : _VolumeProfileFuturesBase
	{
		[Input] public EGate0Mode Mode { get; set; }
		[Input] public int Variant { get; set; }

		private Gate0Engine m_g0;
		private bool m_done, m_configured;
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_leLmt, m_seLmt, m_leStp, m_seStp, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;

		public _TestGate0(object _ctx) : base(_ctx) { Mode = EGate0Mode.EventStudy; Variant = 1; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;   // A2-39 needs the roll; not an [Input]
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "G0LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "G0SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "G0LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "G0SX", EOrderAction.BuyToCover));
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "G0LEl", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "G0SEl", EOrderAction.SellShort));
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "G0LEr", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "G0SEr", EOrderAction.SellShort));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "G0LXs", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "G0SXs", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "G0LXt", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "G0SXt", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (Mode == EGate0Mode.Strategy)
				throw new ArgumentException("_TestGate0: Mode Strategy does not run Gate 0 - use EventStudy/Control*/Canary");
			m_g0 = new Gate0Engine { mode = Mode, variant = Variant, tick = tickSize(), journal = engineSink(ETraceLevel.Journal) };
			m_tradeKeys = new HashSet<string>(); m_gross = m_net = 0; m_trades.Clear();
			string err = m_g0.Configure();
			if (!string.IsNullOrEmpty(err)) throw new ArgumentException("_TestGate0: " + err);
			m_done = false; m_configured = true;
		}

		protected override void StopCalc()
		{
			m_g0 = null; m_configured = false;
			base.StopCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (!m_configured || Bars.Status != EBarState.Close) return;
			DateTime t = Bars.Time[0];
			var b = new Gate0Bar(ToEastern(t), GlobexDate(t), Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0], rollOn(1));
			int mp = Math.Sign(StrategyInfo.MarketPosition);
			if (!m_done) m_g0.Bar(b, mp);
			if (!m_done && (b.day >= Gate0Engine.DevEnd || Bars.LastBarOnChart)) finish();
			if (Mode == EGate0Mode.Canary) { route(mp); journalTrades(); }
		}

		private HashSet<string> m_tradeKeys; private double m_gross, m_net;
		private readonly List<KeyValuePair<DateTime, double[]>> m_trades = new List<KeyValuePair<DateTime, double[]>>();   // entry Globex day -> {gross, net}

		/// <summary>Canary: one journal line per closed trade with GROSS (price move x big point value, no costs) and NET (MC trade
		/// profit, after the commission/slippage set in Strategy Properties), plus running totals.</summary>
		private void journalTrades()
		{
			int n = Math.Min(Positions.Count, 3);
			for (int i = n - 1; i >= 0; i--)
			{
				IMarketPosition pos = Positions[i];
				if (pos == null || pos.ClosedTrades == null) continue;
				foreach (var t in pos.ClosedTrades)
				{
					if (t == null || t.EntryOrder == null || t.ExitOrder == null) continue;
					string key = t.EntryOrder.Name + "|" + t.EntryOrder.BarNumber + "|" + t.ExitOrder.Name + "|" + t.ExitOrder.BarNumber + "|" + t.ExitOrder.Contracts;
					if (!m_tradeKeys.Add(key)) continue;
					double gross = (t.ExitOrder.Price - t.EntryOrder.Price) * (t.IsLong ? 1 : -1) * t.ExitOrder.Contracts * Bars.Info.BigPointValue;
					m_gross += gross; m_net += t.Profit;
					m_trades.Add(new KeyValuePair<DateTime, double[]>(GlobexDate(t.EntryOrder.Time), new[] { gross, t.Profit }));
					if (m_done) printTotals();
					if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, string.Format(System.Globalization.CultureInfo.InvariantCulture,
						"G0-b trade {0} {1} {2:yyyy-MM-dd HH:mm} {3} -> {4:yyyy-MM-dd HH:mm} {5} gross({6:F2}) net({7:F2}) cumGross({8:F2}) cumNet({9:F2})",
						m_g0.CanaryName, t.IsLong ? "L" : "S", t.EntryOrder.Time, t.EntryOrder.Price, t.ExitOrder.Time, t.ExitOrder.Price, gross, t.Profit, m_gross, m_net));
				}
			}
		}

		private void finish()
		{
			m_done = true;
			if (Mode == EGate0Mode.Canary)
			{
				printTotals();
				return;
			}
			List<Gate0Result> r = m_g0.Finish();
			if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, $"G0 done mode({Mode}) family({(EGate0Family)Variant}) types({r.Count}) rollDays({m_g0.rollDays.Count}) sessions({m_g0.sessionDays.Count})");
		}

		/// <summary>Canary totals: all trades, ex-roll window (session before / of / after a roll dropped), 2016-2024 and 2019-2024.</summary>
		private void printTotals()
		{
			if (!traceOn(ETraceLevel.Journal)) return;
			trace(ETraceLevel.Journal, $"G0-b canary({m_g0.CanaryName}) entries({m_g0.Trades}) rollSkips({m_g0.RollSkips}) cumGross({m_gross:F2}) cumNet({m_net:F2}) - read NET profit factor in the Strategy Performance Report; > 1.0 = BUG");
			split("all", d => true);
			split("ex-roll", d => !m_g0.InRollWindow(d));
			split("2016-2024", d => d >= new DateTime(2016, 1, 1) && d < Gate0Engine.DevEnd && !m_g0.InRollWindow(d));
			split("2019-2024", d => d >= Gate0Engine.CanaryA224From && d < Gate0Engine.DevEnd && !m_g0.InRollWindow(d));
		}

		private void split(string name, Func<DateTime, bool> keep)
		{
			int n = 0, w = 0; double g = 0, net = 0, win = 0, loss = 0;
			foreach (var kv in m_trades)
			{
				if (!keep(kv.Key)) continue;
				n++; g += kv.Value[0]; net += kv.Value[1];
				if (kv.Value[1] > 0) { w++; win += kv.Value[1]; } else loss -= kv.Value[1];
			}
			string pf = loss > 0 ? (win / loss).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) : "n/a";
			trace(ETraceLevel.Journal, $"G0-b totals[{name}] trades({n}) wins({w}) gross({g:F2}) net({net:F2}) netPF({pf})");
		}

		/// <summary>Translate the engine intent into next-bar orders (re-sent every bar, MultiCharts cancels unsent ones).</summary>
		private void route(int mp)
		{
			Gate0Intent it = m_g0.intent;
			if (m_done && mp == 0) return;
			if (it.flatten || (m_done && mp != 0)) { if (mp > 0) m_lx.Send(); else if (mp < 0) m_sx.Send(); return; }
			if (mp == 0 || (it.entry == EGate0Entry.Market && it.side != 0 && it.side != mp))
			{
				if (it.side != 0 && it.side != mp)
				{
					if (it.entry == EGate0Entry.Market) { if (it.side > 0) m_le.Send(1); else m_se.Send(1); }
					else if (it.entry == EGate0Entry.Limit && it.entryPrice > 0) { if (it.side > 0) m_leLmt.Send(it.entryPrice, 1); else m_seLmt.Send(it.entryPrice, 1); }
				}
				if (mp == 0) return;
			}
			if (mp > 0)
			{
				if (it.stop > 0) m_lxStp.Send(it.stop);
				if (it.target > 0) m_lxLmt.Send(it.target);
				if (it.reverseStop > 0) m_seStp.Send(it.reverseStop, 1);
			}
			else
			{
				if (it.stop > 0) m_sxStp.Send(it.stop);
				if (it.target > 0) m_sxLmt.Send(it.target);
				if (it.reverseStop > 0) m_leStp.Send(it.reverseStop, 1);
			}
		}
	}
}
