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
	//   Gate-0 level families each RTH session, scores every family against matched random-price placebo levels and
	//   journals PASS / PARK per level type. In Canary mode it trades one deliberately naive "known loser" rule with
	//   1 contract; a canary whose NET profit factor is > 1.0 means the backtest plumbing (fills, costs, lookahead,
	//   session handling) has a bug — it is not an edge.
	//
	// HELPER FILES TO IMPORT FIRST (PowerLanguage .NET Editor → File → Import, then compile)
	//   Already in the repo (import if your MultiCharts install does not have them yet, in this order):
	//     _Rng.Function.CS            (SplitMix64 seeded RNG: placebos + bootstrap)
	//     _LevelStudy.Function.CS     (A2-33 level-study harness, PR #15)
	//     _LevelRef.Function.CS, _RollDetector.Function.CS (roll window / roll detection; PR #19 branch)
	//     the _CustomSignal / _VolumeProfileBase / _VolumeProfileFuturesBase chain and their helper functions
	//   New with this strategy:
	//     _Gate0.Function.CS          (the pure Gate-0 engine: level families, placebos, bootstrap, verdicts, canaries)
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
	//   Everything else is a constant in _Gate0.Function.CS (seed 33, VA 70%, 500 bootstrap draws in blocks of 5,
	//   A2-33 controls 20 per level (5 when Variant = 0), reaction window 60 x 1m bars, max level age 15).
	//   Also set the inherited trace level input to Journal (ETraceLevel.Journal) or nothing is printed.
	//
	// MODE
	//   Strategy   : same as EventStudy (the default; no positions). Kept so the Mode list matches the Gate-0 plan.
	//   EventStudy : the real level families of Variant, each with one matched _Rng placebo, through _LevelStudy.
	//   ControlRT  : negative control, "random time": every level is replaced by the 1m close at a random bar of the
	//                prior RTH session (same family label prefixed RT:). Expected verdict: PARK. A PASS = bug.
	//   ControlFL  : negative control, "fake level": every level is shifted by ±{0.5, 1, 2} x ATR14(5m), chosen by
	//                _Rng (labels prefixed FL:). Expected verdict: PARK. A PASS = bug.
	//   Canary     : G0-b canary number Variant, 1 contract, MultiCharts commission + slippage. Net PF > 1.0 = bug.
	//
	// VARIANT in Strategy / EventStudy / ControlRT / ControlFL (level family; types journaled in brackets)
	//    0 All families at once (heavy: run families one by one for the record; controls drop to 5 per level)
	//    1 Prior profile          [POC, VAH, VAL] of the prior RTH sessions, age 1..5
	//    2 Naked POCs             [NPOC] prior RTH POCs not traded since they formed, age 2..15
	//    3 Initial balance        [IBH, IBL] prior session 09:30–10:30 high / low
	//    4 Overnight              [ONH, ONL] this morning's Globex overnight high / low (18:00 → 09:30)
	//    5 AVWAP anchors          [AV_PH, AV_PL] AVWAP from the prior RTH high / low bar, [AV_WK, AV_MO] week / month
	//                             anchored AVWAP, all valued at the RTH open
	//    6 VWAP                   [VWAP, VWAP+1, VWAP-1] prior RTH session VWAP and ±1 sigma at its close
	//    7 Range + weekly profile [RPOC, RVAH, RVAL] prior 09:30–11:30 range profile; [WPOC, WVAH, WVAL] prior week
	//    8 Composite value area   [CVAH, CVAL] 5-session RTH composite
	//    9 Single-print shelves   [SP_HI, SP_LO] edges of thin runs (>= 4 rows at <= 5% of POC volume) in the prior RTH profile
	//   10 DV level sources       [DV_CLUSTER] prior RTH 30m blocks with range <= 0.7 x mean 30m range and POC row
	//                             >= 2 x median row volume (accumulation cluster)
	//   Roll honesty (A2-39, Reset): all multi-session sources (prior / naked / composite / weekly / week+month
	//   AVWAP) restart at the roll; events on a roll session ±1 are dropped before scoring (review M2).
	//
	// VARIANT in Canary mode (G0-b; all flat by 15:55 ET, RTH only, roll session and the session after skipped)
	//    0 VW-08      long on a 1m close above session (RTH) VWAP, short below, reverse on the opposite close
	//    1 A2-24 (i)  fade the first session-VWAP touch (after 09:35), stop / target 1R = ATR14(5m)
	//    2 A2-24 (ii) fade a 1m close beyond VWAP ±2 sigma, target VWAP frozen at entry, stop 1:1
	//    3 A2-24(iii) fade a 1m close >= 2 x ATR14(5m) from VWAP, target VWAP frozen, stop 1:1
	//    4 DV-10      (A2-24 arm iv) weekly VWAP ±1 sigma fade when both bands moved < 0.5 x ATR30m over 8 x 30m bars;
	//                 limit at the band, TP weekly VWAP, SL 0.20 x ATR20d
	//    5 A2-53      RTH gap >= 0.5%, prior inside day or HV6 < HV100; after the first 30m bar, if the prior close was
	//                 not retraced, fade at market; stop-and-reverse at the session extreme ±1 tick (one reversal)
	//    6 DV-01      prior RTH POC, after a move-away >= 1 x ATR14(5m) (a gap counts); limit at the POC on the first
	//                 touch only; SL = TP = 0.15 x ATR20d
	//    7 PS-03      naive 80% rule: open outside the prior value area, enter on the first 30m close back inside,
	//                 target the prior POC, stop 0.25 x VA width beyond the entry edge
	//
	// COMMISSION AND SLIPPAGE FOR THE CANARIES (Format Signals → Properties)
	//   Commission: $2.25 per contract per side ($4.50 round trip, IBKR ES incl. fees).
	//   Slippage  : 1 tick per contract per side ($12.50 on ES, $5.00 NQ). Trade size: fixed 1 contract.
	//   The canary verdict is the Strategy Performance Report NET profit factor over 2016–2024 (or your dev window).
	//
	// WHERE THE RESULTS SHOW UP AND HOW TO READ THEM
	//   With the trace level at Journal, the PowerLanguage Output window / the _CustomSignal log file receive, once at
	//   the first bar of 2025 (or the last bar on the chart, whichever comes first):
	//     A2-33 <Summary>   the _LevelStudy verdict per type (touch edge, reaction edge, age, KM) for reference
	//     G0 type(T) levels(n) touches(t) holds(h) holdRate(h/t) placebo touches() holds() rate() adj(±x) ci95[lo,hi]
	//        sessions(s) => PASS | PARK (reason)
	//   touches  = real levels of type T hit by a 1m RTH bar in that session; holds = touched levels whose bounce
	//              beat their penetration over the next 60 bars (A2-33 asymmetry > 0).
	//   adj      = holdRate(level) − holdRate(placebo). Placebo = one level per real level at the same distance x
	//              U(0.5, 1.5) from the last close, random side (_Rng seeded by type, session, level number).
	//   ci95     = 2.5 / 97.5 percentiles of adj over 500 moving-block bootstrap draws (blocks of 5 consecutive sessions).
	//   verdict  (§2.3 kill criteria, n = touches):  n < 30 PARK (no evaluation);  30 <= n < 150 PARK (inconclusive);
	//              n >= 150: PARK (kill) if adj <= 0 or ci95 includes 0, else PASS.
	//   Canary mode journals each fill ("G0-b <canary> fill ...") and the trade count at the end; read the NET PF in the
	//   Strategy Performance Report: > 1.0 = BUG to investigate, <= 1.0 = plumbing behaves.
	// ================================================================================================================
	public class _TestGate0 : _VolumeProfileFuturesBase
	{
		[Input] public EGate0Mode Mode { get; set; }
		[Input] public int Variant { get; set; }

		private Gate0Engine m_g0;
		private bool m_done, m_configured;
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_leLmt, m_seLmt, m_leStp, m_seStp, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;

		public _TestGate0(object _ctx) : base(_ctx) { Mode = EGate0Mode.Strategy; Variant = 1; }

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
			m_g0 = new Gate0Engine { mode = Mode, variant = Variant, tick = tickSize(), journal = engineSink(ETraceLevel.Journal) };
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
			if (Mode == EGate0Mode.Canary) route(mp);
		}

		private void finish()
		{
			m_done = true;
			if (Mode == EGate0Mode.Canary)
			{
				if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, $"G0-b canary({(EGate0Canary)Variant}) entries({m_g0.Trades}) — read NET profit factor in the Strategy Performance Report; > 1.0 = BUG");
				return;
			}
			List<Gate0Result> r = m_g0.Finish();
			if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, $"G0 done mode({Mode}) family({(EGate0Family)Variant}) types({r.Count}) rollDays({m_g0.rollDays.Count}) sessions({m_g0.sessionDays.Count})");
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
