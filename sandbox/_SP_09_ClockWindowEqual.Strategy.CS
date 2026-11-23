// ## 1. Identity
// Catalogue ID: SP-09
// ID mapping: SP-09 -> _SP_09_ClockWindowEqual.Strategy.CS (class _SP_09_ClockWindowEqual).
// Name: SP-09 fair-value gap retest
// Header version: 1
// Rules version: catalogue specification under the SP-09 heading, rewritten 2026-11-15.
// Study class: _SP_09_ClockWindowEqual
// File: sandbox/_SP_09_ClockWindowEqual.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the SP-09 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: rebuilt 2026-11-22 to the catalogue's 1-minute rules; shorts as written, longs mirror (Side input). Window: the sweep bar's Eastern close is in [H:50, H+1:10), H 9..14, RTH. A live bearish 5-minute gap (minTicks 1, 0.15 x ATR14, RTH, 5 sessions, filled on a close) was created before the sweep bar and the sweep bar overlaps it. The sweep bar trades 1 tick above a confirmed isEqual 1-minute swing high (Fractal 2, 0.10 x ATR20, 5 pivots) that no bar since its confirmation traded through. Within 3 bars (sweep bar = 0) a new bearish 1-minute gap forms whose third bar closes at least 0.5 x ATR14 below the swept high. Sell limit at that gap's low edge (High3), working to the window end plus 2 bars; no new order after 15:30. Stop: ITH (sweep bar through displacement bar) plus 1 tick, widened to the cost floor. Target: the nearest 5-minute isEqual low pair's higher low minus 1 tick, at least 0.5R below the entry, else skip. One attempt per window per direction; a tier-1 event blackout (2 min before / 10 min after) vetoes and cancels orders. Bracket only; flat by the session cutoff. The optional session-extreme pairing is dropped.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the SP-09 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: see CalcBar. Closed bar only.
// Management: one contract when this file trades. Size is never cut to fit the stop.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: the order type named in the catalogue (market next bar, stop, or limit).
// Can fill: the next bar, or later for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one, unless the catalogue says a reverse, which this file does not open inside GX.
// Re-entry: one attempt per clock window per direction (several windows a day). TradesToday guard via the sandbox book.
// Same session after a stop: a later window may trade; the same window and direction may not.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 5-minute.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: 5-minute, same symbol: the respected 5-minute gap (host FVG detector) and the 5-minute equal-pair target (ExtraPriceAction draw5).
// Further series: Not applicable
// Warmup: the average or profile the rule names.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue trigger and the 1 percent budget when an order is sent.
// Skip when: rollOn on a futures profile idea, the profile or average is not ready, or the budget fails.
// Entry cutoff: no entry order is sent when its fill bar would end within NoEntryMinutesBeforeFlat (default 25) minutes of the date-aware flat-by cutoff, or after it (catalogue 2.1, early closes included); evening-session entries are allowed.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is InstrumentProfile.costFloorTicks times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: named in Create when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. A stop or limit rests. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when section 13 says GX, otherwise sendExits.
//
// ## 13. Exits
// Exit orders: GX. Stop at ITH + 1 tick (ITL - 1 tick long), widened to the cost floor. Target is the 5-minute equal pair minus/plus 1 tick. Flat at the session cutoff.
// Hard stop: the catalogue stop, widened to the instrument cost floor.
// Target: the catalogue target when it is a price known at the signal.
// Time exit: the catalogue clock, taken from the session API when the rule says the session end.
// Invalidation: any extra exit named in the assumption.
//
// ## 14. Regimes
// Suitable: the session the code consults.
// Unsuitable: outside that session, and a roll day when the rule skips rolls.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: PriceAction on Data1 (Fractal 2, equal 0.10), ExtraPriceAction draw5 on Data2, FVG on Data2, a local 1-minute FVG detector, EventCalendar (T1 blackout 2/10).
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: rebuilt 2026-11-22 to the catalogue's 1-minute rules; shorts as written, longs mirror (Side input). Window: the sweep bar's Eastern close is in [H:50, H+1:10), H 9..14, RTH. A live bearish 5-minute gap (minTicks 1, 0.15 x ATR14, RTH, 5 sessions, filled on a close) was created before the sweep bar and the sweep bar overlaps it. The sweep bar trades 1 tick above a confirmed isEqual 1-minute swing high (Fractal 2, 0.10 x ATR20, 5 pivots) that no bar since its confirmation traded through. Within 3 bars (sweep bar = 0) a new bearish 1-minute gap forms whose third bar closes at least 0.5 x ATR14 below the swept high. Sell limit at that gap's low edge (High3), working to the window end plus 2 bars; no new order after 15:30. Stop: ITH (sweep bar through displacement bar) plus 1 tick, widened to the cost floor. Target: the nearest 5-minute isEqual low pair's higher low minus 1 tick, at least 0.5R below the entry, else skip. One attempt per window per direction; a tier-1 event blackout (2 min before / 10 min after) vetoes and cancels orders. Bracket only; flat by the session cutoff. The optional session-extreme pairing is dropped.
//
// ## 17. Parameters
// Input: Side
// Purpose: which direction trades. Both (default), Long or Short.
// Default: Both
// Units: Not applicable
// Range: Both, Long, Short
// Effect: Short is the catalogue rule as written; Long is its mirror.
// Interactions: Gate and ExitMode are the shared sandbox inputs (defaults Off).
// Fixed or research-tuned: the catalogue core, not a sweep. The sweep stays in the catalogue.
//
// Input: NoEntryMinutesBeforeFlat
// Purpose: no new entry in the last N minutes before the date-aware flat-by cutoff (catalogue 2.1: no new entries after 15:30; flat by 15:55).
// Default: 25 minutes (owner 2026-11-22 22:50 CT).
// Units: minutes
// Range: 0 or more. 0 disables the window (the flat-by cutoff itself still blocks). A negative value acts as 0.
// Effect: an entry order is not sent when its fill bar (the next bar) would end at or after the cutoff minus N minutes. An early close moves it (ES 13:10 ET flat: none from 12:45 ET). Evening-session entries are not affected.
// Interactions: the flat-by cutoff (FlatBy, early-close aware); entries GX holds overnight (SessionExit HoldOvernight) are not blocked.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name when an entry is sent.
// logSignal tag: SP-09
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: rebuilt 2026-11-22 to the catalogue's 1-minute rules; shorts as written, longs mirror (Side input). Window: the sweep bar's Eastern close is in [H:50, H+1:10), H 9..14, RTH. A live bearish 5-minute gap (minTicks 1, 0.15 x ATR14, RTH, 5 sessions, filled on a close) was created before the sweep bar and the sweep bar overlaps it. The sweep bar trades 1 tick above a confirmed isEqual 1-minute swing high (Fractal 2, 0.10 x ATR20, 5 pivots) that no bar since its confirmation traded through. Within 3 bars (sweep bar = 0) a new bearish 1-minute gap forms whose third bar closes at least 0.5 x ATR14 below the swept high. Sell limit at that gap's low edge (High3), working to the window end plus 2 bars; no new order after 15:30. Stop: ITH (sweep bar through displacement bar) plus 1 tick, widened to the cost floor. Target: the nearest 5-minute isEqual low pair's higher low minus 1 tick, at least 0.5R below the entry, else skip. One attempt per window per direction; a tier-1 event blackout (2 min before / 10 min after) vetoes and cancels orders. Bracket only; flat by the session cutoff. The optional session-extreme pairing is dropped.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _SP_09_ClockWindowEqual : _VolumeProfileFuturesBase
	{
		// Coder 2026-11-22 (owner: rebuild SP-09 to the catalogue's 1-minute rules). Shorts as written in the catalogue; longs mirror.
		private IOrderPriced m_se, m_le; private bool m_done; private DateTime m_day = DateTime.MinValue;
		[Input] public ESandboxGate Gate { get; set; }
		[Input] public ESandboxExit ExitMode { get; set; }
		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		private int m_anchorId = -1; private DateTime m_orderUntil = DateTime.MinValue;
		private _FvgDetector m_fvg1;   // the trigger-TF (1-minute) displacement gaps: minTicks 1, minAtrFrac 0, RTH only
		// Per side (0 = short, 1 = long): the open sweep, its window key, the swept equal pivot, the ITH / ITL and the sweep bar.
		private readonly bool[] m_swOn = new bool[2]; private readonly int[] m_swBar = new int[2], m_swKey = new int[2];
		private readonly double[] m_swPx = new double[2], m_ext = new double[2]; private readonly long[] m_swSeq = new long[2];
		private readonly int[] m_attemptKey = { -1, -1 };   // one attempt per window per direction
		private readonly long[] m_lastSweptSeq = { -1, -1 };
		// Daily skip counts (one Journal line per day, like A2-45): sweeps seen, no 5m gap, no displacement, no 5m draw, budget, blackout, sent.
		private int m_cSweep, m_cNoGap, m_cNoDisp, m_cNoDraw, m_cBudget, m_cBlack, m_cSent; private DateTime m_chkDay = DateTime.MinValue;

		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _SP_09_ClockWindowEqual(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; Gate = ESandboxGate.Off; ExitMode = ESandboxExit.Off; Side = ESandboxSide.Both; }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series; GxOrderPrefix = "SP09"; createGxOrders();
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "SP09SE", EOrderAction.SellShort));
			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "SP09LE", EOrderAction.Buy));
			// Trigger-TF (Data1, 1-minute) swings: Fractal strength 2, isEqual at 0.10 x ATR(20), last 5 pivots.
			PriceActionConfig.enabled = true; PriceActionConfig.dataStream = 1; PriceActionConfig.pivotMode = EPivotMode.Fractal;
			PriceActionConfig.strength = 2; PriceActionConfig.equalFactor = 0.10; PriceActionConfig.lookbackPivots = 5; PriceActionConfig.atrLength = 20; PriceActionConfig.candles = false;
			// Draw-TF (Data2, 5-minute) swings for the equal-low / equal-high target pair.
			ExtraPriceAction.Add(new PriceActionConfig { enabled = true, dataStream = 2, role = "draw5", pivotMode = EPivotMode.Fractal, strength = 2, equalFactor = 0.10, lookbackPivots = 5, atrLength = 20, candles = false });
		}

		protected override void StartCalc()
		{
			applyGateNeeds();   // before base.StartCalc: the gate input decides which engines start
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			// The 5-minute respected gap (host detector on Data2): minTicks 1, minAtrFrac 0.15 of ATR(14), RTH only, no cross-session, 5 sessions, filled on a close.
			Fvgs.enabled = true; Fvgs.dataStream = 2; Fvgs.minTicks = 1; Fvgs.minAtrFrac = 0.15; Fvgs.atrLen = 14;
			Fvgs.rthOnly = true; Fvgs.crossSessions = false; Fvgs.maxAgeSessions = 5; Fvgs.fillOn = EFvgFillOn.Close;
			// M-EVENT tier-1 blackout vetoes new SP-09 orders (no flatten: the bracket and the flat-by manage an open trade).
			EventConfig.enabled = true; EventConfig.blackoutTier = EEventTier.T1; EventConfig.blackoutPreMin = 2; EventConfig.blackoutPostMin = 10;
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); requireRing(1, 2000, "SP-09 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */
			m_fvg1 = new _FvgDetector(new FvgConfig { enabled = true, dataStream = 1, minTicks = 1, minAtrFrac = 0, atrLen = 14, rthOnly = true, crossSessions = false, maxAgeSessions = 1, fillOn = EFvgFillOn.Close }, tickSize(), TimeSpan.FromMinutes(1));
			m_done = false; m_day = m_chkDay = DateTime.MinValue; m_restOn = false; m_anchorId = -1; m_orderUntil = DateTime.MinValue;
			for (int s = 0; s < 2; s++) { m_swOn[s] = false; m_attemptKey[s] = -1; m_lastSweptSeq[s] = -1; }
			m_cSweep = m_cNoGap = m_cNoDisp = m_cNoDraw = m_cBudget = m_cBlack = m_cSent = 0;
		}

		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return; exitBar();
			if (m_fvg1 != null) m_fvg1.Add(_SandboxRules.RingAgo(Ring(1), 0));   // every closed Data1 bar, before any return
			if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day)
			{
				flushChecks();
				m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_anchorId = -1;
				for (int s = 0; s < 2; s++) { m_swOn[s] = false; m_attemptKey[s] = -1; }
			}
			DateTime et = ToEastern(Bars.Time[0]);
			// The limit works until the window end plus 2 bars; a tier-1 blackout cancels it.
			if (m_restOn && et >= m_orderUntil) { m_restOn = false; if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} limit expired (window end + 2 bars)"); }
			if (m_restOn && EventBlackout) { m_restOn = false; m_cBlack++; if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} limit cancelled: tier-1 blackout"); }
			if (sandboxBook()) return;
			if (StrategyInfo.MarketPosition != 0) return;
			if (m_done || !IsRth() || rollOn(1)) { clearSweeps(); return; }
			_PriceActionEngine pa1 = PriceActionEngine, pa5 = PriceActionFor(2, "draw5"); _FvgDetector f5 = FvgDetector;
			if (pa1 == null || pa5 == null || f5 == null || m_fvg1 == null) return;
			int key = windowKey(et);
			for (int s = 0; s < 2; s++)
			{
				bool buy = s == 1;
				if (!_SandboxRules.Allows(Side, buy)) { m_swOn[s] = false; continue; }
				if (!m_swOn[s] && key >= 0 && m_attemptKey[s] != key) detectSweep(s, key, pa1, f5, et);
				if (m_swOn[s]) stepDisplacement(s, pa5, f5, et);
			}
		}

		/// <summary>Window key H (9..14) when the bar's ET close time is in [H:50, H+1:10); -1 outside. RTH only; no window after 15:10.</summary>
		private static int windowKey(DateTime et)
		{
			int h = et.Hour, m = et.Minute, key = m >= 50 ? h : (m < 10 ? h - 1 : -1);
			return key >= 9 && key <= 14 ? key : -1;
		}

		private void clearSweeps() { m_swOn[0] = m_swOn[1] = false; }

		/// <summary>Sweep: this closed 1m bar trades at least 1 tick beyond a confirmed isEqual 1m swing (no earlier bar since its
		/// confirmation did), and its range overlaps a live 5m gap of the trade's direction created before this bar.</summary>
		private void detectSweep(int s, int key, _PriceActionEngine pa1, _FvgDetector f5, DateTime et)
		{
			bool buy = s == 1; double tick = tickSize(); DateTime now = Bars.Time[0];
			for (int ago = 0; ago < 5; ago++)
			{
				_Pivot p = buy ? pa1.Low(0, ago) : pa1.High(0, ago);
				if (p == null) break;
				if (!p.isEqual || p.confirmTime >= now || p.seq == m_lastSweptSeq[s]) continue;
				bool through = buy ? Bars.Low[0] <= p.price - tick : Bars.High[0] >= p.price + tick;
				if (!through || sweptBefore(buy, p)) continue;
				m_lastSweptSeq[s] = p.seq; m_cSweep++;
				Fvg g = respectedGap(buy, f5, now);
				if (g == null)
				{
					m_cNoGap++;
					if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {(buy ? "long" : "short")} sweep of equal {(buy ? "low" : "high")} {p.price} skipped: no live 5m gap at the sweep");
					return;
				}
				m_swOn[s] = true; m_swBar[s] = Bars.CurrentBar; m_swKey[s] = key; m_swPx[s] = p.price; m_swSeq[s] = p.seq;
				m_ext[s] = buy ? Bars.Low[0] : Bars.High[0];
				if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {(buy ? "long" : "short")} sweep of equal {(buy ? "low" : "high")} {p.price} in 5m gap {g.lo}-{g.hi} ({g.createdTime:HH:mm})");
				return;
			}
		}

		/// <summary>True when an earlier Data1 bar after the pivot's confirmation already traded 1 tick through it (not a fresh sweep).</summary>
		private bool sweptBefore(bool buy, _Pivot p)
		{
			double tick = tickSize(); _BarRing r = Ring(1);
			for (int ago = 1; r != null && ago < r.Count && ago < 2000; ago++)
			{
				RingBar b = _SandboxRules.RingAgo(r, ago);
				if (b.bar < 0 || b.time <= p.confirmTime) break;
				if (buy ? b.l <= p.price - tick : b.h >= p.price + tick) return true;
			}
			return false;
		}

		/// <summary>A live (unfilled) 5m gap of the trade's direction (bearish for a short), created strictly before the sweep bar,
		/// whose zone the sweep bar's range overlaps. Null when none.</summary>
		private Fvg respectedGap(bool buy, _FvgDetector f5, DateTime now)
		{
			foreach (Fvg g in f5.Live)
			{
				if (!g.IsLive || g.dir != (buy ? 1 : -1) || !(g.createdTime < now)) continue;
				if (Bars.High[0] >= g.lo && Bars.Low[0] <= g.hi) return g;
			}
			return null;
		}

		/// <summary>Displacement: a new 1m gap in the trade's direction whose third bar closes at least 0.5 x ATR(14) beyond the swept
		/// level, within 3 bars of the sweep bar. Then the limit at the gap's near edge (short: lo = High3; long: hi = Low3).</summary>
		private void stepDisplacement(int s, _PriceActionEngine pa5, _FvgDetector f5, DateTime et)
		{
			bool buy = s == 1; double tick = tickSize();
			int k = Bars.CurrentBar - m_swBar[s];
			m_ext[s] = buy ? Math.Min(m_ext[s], Bars.Low[0]) : Math.Max(m_ext[s], Bars.High[0]);   // ITH / ITL: sweep bar through this bar
			Fvg d = m_fvg1.NewThisBar;
			double atr = averageTrueRange(Bars, 14);
			bool disp = d != null && d.dir == (buy ? 1 : -1) && atr > 0 && (buy ? Bars.Close[0] >= m_swPx[s] + 0.5 * atr : Bars.Close[0] <= m_swPx[s] - 0.5 * atr);
			if (!disp)
			{
				if (k >= 3) { m_swOn[s] = false; m_cNoDisp++; if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {(buy ? "long" : "short")} no displacement within 3 bars of the sweep"); }
				return;
			}
			m_swOn[s] = false;
			int key = m_swKey[s];
			m_attemptKey[s] = key;   // the window's one attempt for this direction is used whether or not an order goes out
			DateTime until = et.Date.AddHours(key + 1).AddMinutes(12);   // window end (H+1:10) plus 2 one-minute bars
			string side = buy ? "long" : "short";
			if (et >= until || et.Hour > 15 || (et.Hour == 15 && et.Minute > 30)) { if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} skipped: past the window order life or 15:30"); return; }
			if (EventBlackout) { m_cBlack++; if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} skipped: tier-1 blackout"); return; }
			if (m_restOn) { if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} skipped: another SP-09 limit is working"); return; }
			double entry = buy ? d.hi : d.lo;
			double stop = widen(buy, entry, buy ? m_ext[s] - tick : m_ext[s] + tick);
			double risk = Math.Abs(entry - stop);
			double target = drawTarget(buy, pa5, entry, risk);
			if (double.IsNaN(target)) { m_cNoDraw++; if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} skipped: no 5m equal {(buy ? "high" : "low")} pair at least 0.5R beyond {entry}"); return; }
			if (!budget(entry, stop)) { m_cBudget++; if (journalOn()) trace(ETraceLevel.Error, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} skipped: risk exceeds 1 percent ({risk} points)"); return; }
			arm(buy, entry, stop, target, buy ? "SP09LE" : "SP09SE", 0);
			honorExit(entry, risk);
			if (m_done) return;
			m_restPx = entry; m_restBuy = buy; m_restOn = true; m_orderUntil = until; m_anchorId = key * 2 + s;
			m_restQty = _SandboxRules.ScaleQty(ExitMode, false);
			m_cSent++;
			if (journalOn()) trace(ETraceLevel.Journal, $"SP-09 {et:yyyy-MM-dd HH:mm} {side} limit {entry} stop {stop} target {target} swept {m_swPx[s]} ext {m_ext[s]} gap {d.lo}-{d.hi} until {until:HH:mm}");
			if (buy) { if (hostGate(true)) m_le.Send(entry, m_restQty); }
			else { if (hostGate(false)) m_se.Send(entry, m_restQty); }
		}

		/// <summary>5m draw: a confirmed isEqual swing low (short) and the earlier low it matched; the pair's nearer (higher) low minus
		/// 1 tick. Longs mirror on equal highs (lower high plus 1 tick). The nearest pair at least 0.5R beyond the entry; NaN when none.</summary>
		private double drawTarget(bool buy, _PriceActionEngine pa5, double entry, double risk)
		{
			double tick = tickSize(), best = double.NaN; DateTime now = Bars.Time[0];
			int n = pa5.PivotCount(0, buy);
			for (int ago = 0; ago < n; ago++)
			{
				_Pivot p = buy ? pa5.High(0, ago) : pa5.Low(0, ago);
				if (p == null) break;
				if (!p.isEqual || p.equalToSeq < 0 || p.confirmTime > now) continue;
				_Pivot q = null;
				for (int j = ago + 1; j < n; j++) { _Pivot c = buy ? pa5.High(0, j) : pa5.Low(0, j); if (c == null) break; if (c.seq == p.equalToSeq) { q = c; break; } }
				if (q == null) continue;
				double px = buy ? Math.Min(p.price, q.price) + tick : Math.Max(p.price, q.price) - tick;
				if (buy ? px < entry + 0.5 * risk : px > entry - 0.5 * risk) continue;
				if (double.IsNaN(best) || (buy ? px < best : px > best)) best = px;
			}
			return best;
		}

		/// <summary>One Journal line per Globex day with the setup and skip counts (only when a sweep was seen).</summary>
		private void flushChecks()
		{
			if (m_day != DateTime.MinValue && m_cSweep > 0 && journalOn())
				trace(ETraceLevel.Journal, $"SP-09 {m_day:yyyy-MM-dd} checks sweeps({m_cSweep}) noGap5m({m_cNoGap}) noDisplacement({m_cNoDisp}) noDraw({m_cNoDraw}) budget({m_cBudget}) blackout({m_cBlack}) sent({m_cSent})");
			m_cSweep = m_cNoGap = m_cNoDisp = m_cNoDraw = m_cBudget = m_cBlack = m_cSent = 0;
		}

		private double widen(bool buy, double entry, double stop)
		{
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			double floor = ticks * tickSize();
			if (buy && entry - stop < floor) return entry - floor;
			if (!buy && stop - entry < floor) return entry + floor;
			return stop;
		}
		private int m_timeStop, m_flatOverride;
		private bool budget(double a, double b) { double r = Math.Abs(a - b); return r > 0 && r * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private void arm(bool buy, double entry, double stop, double target, string name, int holdDays)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = ETrailMode.None;
			GxConfig.SessionExit = holdDays > 0 ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.MaxHoldDays = holdDays;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(m_flatOverride, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd);
			GxConfig.TimeStopMinutes = m_timeStop; GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
		}

		// Coder 2026-11-22 (Analyst native retest of #152: A2-11 / A2-32 entries filled after the 15:55 ET flat-by cutoff): a study whose
		// position is flat by the session cutoff sends no entry on or after the cutoff bar (session end minus 5 minutes, early-close
		// aware) until 18:00 ET. Entries that GX holds overnight (SessionExit HoldOvernight) are not blocked.
		private DateTime m_cutoffLogDay = DateTime.MinValue;
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryCutoff()
		{
			if (GxConfig.SessionExit == ESessionExit.HoldOvernight || !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat)) return false;   // #146 date-aware cutoff
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_cutoffLogDay) { m_cutoffLogDay = day; if (journalOn()) trace(ETraceLevel.Journal, "entry blocked: " + gxEntryCutoffNote(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat)); }
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
			int anchor = m_anchorId;   // window key x side: one attempt per window per direction
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_anchorId = -1; return true; }   // that window/side is used; later windows stay open
			if (step != ERest.Resend) return false;
			if (EventBlackout) { m_restOn = false; return false; }
			if (m_restBuy) { if (hostGate(true)) m_le.Send(m_restPx, m_restQty); }
			else { if (hostGate(false)) m_se.Send(m_restPx, m_restQty); }
			return true;
		}

	}
}