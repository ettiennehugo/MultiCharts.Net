using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPF-5, Rev 2.5 (analyst-base-class-spec.md): order-flow helpers with a per-bar ranked fallback chain and a quality
	/// tag on every value. MultiCharts .NET does not expose the trade aggressor side (confirmed by the user, also with dxFeed),
	/// so every buy/sell split below except the reserved Aggressor rank is an APPROXIMATION:
	///   1 Aggressor   (Exact)   reserved: native aggressor flag per trade. Not exposed by MC .NET today; never produced.
	///   2 McBidAsk    (High)    Spec S1 (rev 2.7): MC's own Ask/Bid-traded split from Volume Delta levels. QUOTE-CLASSIFIED, NOT
	///                           AGGRESSOR (MC rule: inside-spread trade -> last recorded side). Off by default (FlowConfig.McBidAsk).
	///   3 Quote       (High)    Lee-Ready on 1-tick Trade + Bid + Ask streams (DataLoader): price >= ask BUY, price &lt;= bid SELL,
	///                           inside the spread -> tick rule. A trade whose bid or ask is older than MaxQuoteAgeMs (default
	///                           1000 ms), missing or crossed is classified by the tick rule and the bar drops to TickRule.
	///   4 TickRule    (Medium)  trade ticks with sizes: uptick BUY, downtick SELL, zero tick inherits the previous side. The side
	///                           carries across bars and sessions; the very first trade ever is BUY (as MC, UM p35).
	///   5 BarUpDown   (Low)     Bars.UpTicks / DownTicks only, Build Volume On = Trade Volume (set by the caller), and the
	///                           per-bar T7 check Up + Down == Volume. Bar delta only, no ladder.
	///   6 Unavailable (Unknown) Tick Count mode, failed T7, IOG history without a 1-tick Bar Magnifier, or no usable data.
	/// Closed bars only: a bar is built when the caller closes it, from events time-stamped at or before its close; later events
	/// stay queued. A cumulative value carries the lowest quality of its bars. MultiCharts-free (no MC types): the
	/// &lt;order-flow&gt; block is extracted by tests/OrderFlowTests (acceptance tests T1-T8). Helper-file convention per the CS-0
	/// probe (plain class in PowerLanguage.Function, class name = file name).
	/// </summary>
	public sealed class _OrderFlow
	{
		private _OrderFlow() { }

		// <order-flow>
		/// <summary>Rank of the source that produced a bar (lower = better).</summary>
		public enum FlowSource { Aggressor = 0, McBidAsk = 1, Quote = 2, TickRule = 3, BarUpDown = 4, Unavailable = 5 }
		/// <summary>Spec S1: MultiCharts' own Ask/Bid-traded split (Volume Delta levels). Off until probes P-BA1..4 pass.</summary>
		public enum EMcBidAsk { Off = 0, Prefer = 1, CrossCheckOnly = 2 }
		/// <summary>Tick-chunk spec §5.2: a day whose window ends before the last closed bar: Skip = not requested (its ticks could no
		/// longer be used), Load = requested anyway.</summary>
		public enum ETickChunkLate { Skip = 0, Load = 1 }
		/// <summary>Method tag of the McBidAsk tier: quote-classified by MC, NOT aggressor.</summary>
		public const string McBidAskMethod = "McBidAsk(quote-classified, not aggressor; MC rule: inside-spread trade -> last recorded side, UM p35/p159)";
		/// <summary>Quality tag (lower = better). A combined value takes the worst (highest) tag of its parts.</summary>
		public enum FlowQuality { Exact = 0, High = 1, Medium = 2, Low = 3, Unknown = 4 }
		public enum Side { Unknown = 0, Buy = 1, Sell = -1 }
		/// <summary>Cumulative delta reset: Globex session open or the first RTH bar (caller supplies the keys).</summary>
		public enum ResetMode { Session = 0, Rth = 1 }
		/// <summary>Chart Build Volume On setting (Format Instrument > Settings), mapped from the typed Bars.Info.BVO (MC enum BuildVolumeOn:
		/// Volume (249) = TradeVolume, TickCount (324) = TickCount, anything else = Unknown; _McApiDump4 on @ES, 2026-11-01).</summary>
		public enum VolumeBasis { Unknown = 0, TradeVolume = 1, TickCount = 2 }
		/// <summary>Per-bar data regime, set by the caller.</summary>
		public enum Regime
		{
			TickStream = 0,             // DataLoader 1-tick Trade stream (and Bid/Ask streams if QuotesExpected)
			IogRealTime = 1,            // IOG tick calls in real time (trade ticks, no quotes: StatusLine bid/ask is not used)
			IogMagnifierOneTick = 2,    // IOG history with Bar Magnifier = 1 tick (stated by the caller)
			IogHistoryNoMagnifier = 3,  // IOG history on synthetic OHLC points: no flow
			BarsOnly = 4                // no tick source: BarUpDown or Unavailable
		}

		public struct FlowValue
		{
			public double Value;
			public FlowSource Source;
			public FlowQuality Quality;
			public bool Developing;     // not final yet (e.g. absorption before the next bar closes)
			/// <summary>CumDelta only: the reset period started before the oldest kept bar (FlowConfig.MaxBars cap), so Value sums
			/// only the kept bars of the period and is not the full cumulative delta.</summary>
			public bool Truncated;
			public static FlowValue Unavailable(bool developing = false)
			{
				return new FlowValue { Value = double.NaN, Source = FlowSource.Unavailable, Quality = FlowQuality.Unknown, Developing = developing };
			}
			public bool IsAvailable { get { return Source != FlowSource.Unavailable && !double.IsNaN(Value); } }
			public override string ToString() { return Value + " " + Source + "/" + Quality + (Developing ? " dev" : "") + (Truncated ? " trunc" : ""); }
		}

		public sealed class FlowConfig
		{
			public double MaxQuoteAgeMs = 1000;
			public ResetMode ResetMode = ResetMode.Session;
			/// <summary>false (default): a session mixing Quote and TickRule bars reports the worst of them (session to date, no lookahead).</summary>
			public bool AllowMixed = false;
			public bool QuotesExpected = true;        // Bid/Ask tick streams are being fed
			public VolumeBasis BuildVolumeOn = VolumeBasis.Unknown;   // set each StartCalc from the typed Bars.Info.BVO (VPFB); BarUpDown needs TradeVolume; TickCount: McBidAsk Unavailable reason(volumeBasis), Info once
			public bool ExcludeExtremesTickRule = true;   // TickRule footprints are wrong at the bar high/low
			public int MaxBars = 5000;                     // closed bars kept (at least 10); older bars are dropped and CumDelta reports Truncated
			/// <summary>Spec S1: MC Volume Delta levels. Off (default): never requested. Prefer: a matching level set becomes the
			/// ladder. CrossCheckOnly: levels are matched and their delta recorded (T9) but Quote/TickRule stays the source.</summary>
			public EMcBidAsk McBidAsk = EMcBidAsk.Off;
			/// <summary>|sum(ask + bid) - bar volume| / bar volume must be within this for a level set to be used.</summary>
			public double mcVolTolerance = 0.005;
			/// <summary>Recalc spec §4: after the McBidAsk history completes, recalculate the study once (from the calc thread) so the
			/// bars that closed before it arrived get the levels from the cache. false = never recalculate.</summary>
			public bool McRecalcOnLoad = true;
			/// <summary>Recalculations per cache signature (loop guard). A manual Recalculate after that gives a cache hit and no recalc.</summary>
			public int McRecalcMax = 1;
			/// <summary>ExecControl.RecalcLastBarAfter interval (ms) while idle work is pending (history loading, queued lines).</summary>
			public int McIdlePollMs = 1000;
			/// <summary>Stop the idle poll after this many seconds.</summary>
			public int McIdleTimeoutSec = 900;
			/// <summary>Fallback (spec §4.3 step 6), off: call ExecControl.Recalculate() from the DataLoader callback when the history
			/// completes. Stays off: the probe (2026-11-01) showed OnRecalcLastBarAfterEvent fires on a historical chart, on the calc
			/// thread (P-RC2/P-RC3), so the event path is the primary one.</summary>
			public bool McRecalcFromCallback = false;
			/// <summary>Process-wide loop guard (review B1): at most McRecalcGuardMaxPerKey recalcs per guard key (strategy type +
			/// request signature) within this many seconds, across strategy objects. A backstop: the probe (P-RC1) showed MC keeps the
			/// same object and its fields across Recalculate, so the instance gate normally decides. 0 or less does NOT disable the guard: the window is clamped to at least
			/// 1 second (Math.Max(1, ...) in mcGuardWindow). To turn recalc-on-load off use McRecalcOnLoad = false.</summary>
			public int McRecalcGuardWindowSec = 600;
			/// <summary>Review 2 item 1: recalcs allowed per guard key within McRecalcGuardWindowSec, process-wide. No per-chart member
			/// is confirmed in the typed API, so two identical charts (same strategy type, symbol, feed, resolution, first bar) share a
			/// key; 3 lets each of them recalc once (the instance gate still allows McRecalcMax per object) while a loop (e.g. a host
			/// that rebuilds the object) stops after 3 recalcs per window. 0 or less blocks every recalc.</summary>
			public int McRecalcGuardMaxPerKey = 3;
			/// <summary>Tick-chunk spec §5: one DataLoader request per Globex day and stream. false = the legacy single request (A/B only).</summary>
			public bool TickChunkByDay = true;
			/// <summary>Days requested at the same time (1..4; all streams of a day together).</summary>
			public int TickChunkMaxInFlightDays = 1;
			/// <summary>A failed day is re-requested once as this many equal sub-windows; 0 = no retry.</summary>
			public int TickChunkSplitOnFail = 4;
			public ETickChunkLate TickChunkLate = ETickChunkLate.Skip;
			/// <summary>Bounded wait (ms) at the first bar of each Globex day for that day's chunks; 0 = off. With a wait, late days are
			/// loaded (not skipped). DataLoader callbacks arrive on their own thread while CalcBar sleeps (probe P-RC7).</summary>
			public int TickChunkWaitMs = 0;
			/// <summary>Chunk boundary, US Eastern. The host replaces this with the template's listed session end converted to ET
			/// (M6b). 17:00 remains only until that replacement, for a profile-off chart.</summary>
			public TimeSpan TickChunkBoundaryEt = new TimeSpan(17, 0, 0);
			/// <summary>An in-flight chunk with no callback for this many seconds is failed (and split like any failure); 0 = no timeout.</summary>
			public int TickChunkTimeoutSec = 300;
		}

		/// <summary>Data the caller has for the bar being closed (Data1 values).</summary>
		public struct BarInput
		{
			public int BarNumber;            // 1-based, increasing
			public DateTime CloseTime;       // bar time stamp (MC bars are close-stamped); events with time &lt;= CloseTime belong here
			public DateTime SessionKey;      // e.g. Globex date
			public DateTime ResetKey;        // CumDelta resets when this changes (Session or RTH key, caller's choice)
			public Regime Regime;
			/// <summary>Volume = the bar's TOTAL (the caller passes Bars.Ticks[0]: on intraday charts Bars.Volume is the up volume only);
			/// UpTicks/DownTicks = Bars.UpTicks[0]/Bars.DownTicks[0]. All three are contracts under Build Volume On = Trade Volume and
			/// tick counts under Tick Count.</summary>
			public double High, Low, Close, Volume, UpTicks, DownTicks;
			/// <summary>Spec §7.3: the bar's open (previous bar's time; first bar: CloseTime − bar duration). Trades at or before
			/// the previous CloseBar's CloseTime are backlog and not added; with TimeBased, also those at or before OpenTime.
			/// Default (MinValue) = no bound.</summary>
			public DateTime OpenTime;
			/// <summary>The chart resolution is exact time-based (Second/Minute/Hour/Day: the OpenTime cut applies). Week/Month/Quarter/
			/// Year (calendar-aligned) and tick/volume/range/custom bars: false (previous-close cut only).</summary>
			public bool TimeBased;
		}

		public struct Imbalance
		{
			public double Price;
			public Side Side;
			public double Ratio;             // +Infinity when the diagonal level traded 0
			public FlowQuality Quality;
		}

		public sealed class StackedResult
		{
			public readonly List<double[]> Runs = new List<double[]>();   // {lowPrice, highPrice, count, side (+1 buy / -1 sell)}, buy runs first, low -> high
			public FlowQuality Quality = FlowQuality.Unknown;
			public int Longest { get { int m = 0; foreach (var r in Runs) m = Math.Max(m, (int)r[2]); return m; } }
			public bool Found { get { return Runs.Count > 0; } }
		}

		/// <summary>Footprint of one closed bar. Bid = approximated sell volume, Ask = approximated buy volume (footprint naming).</summary>
		public sealed class FootprintBar
		{
			public int BarNumber;
			public DateTime CloseTime, SessionKey, ResetKey;
			public FlowSource Source = FlowSource.Unavailable;
			public FlowQuality Quality = FlowQuality.Unknown;   // after the AllowMixed session rule
			public double Buy, Sell, Volume;
			public int Trades, StaleQuoteTrades;
			public double McDelta = double.NaN;                 // S1: MC level-set delta (ask - bid) when matched (Prefer or CrossCheckOnly)
			public double McAsk = double.NaN, McBid = double.NaN;   // diag: ask/bid sums of the level set stamped at this bar (also on volMismatch)
			public double ChainDelta = double.NaN;              // S1: delta of the Quote/TickRule/BarUpDown chain before an MC set replaced it (T9 cross-check)
			public string McMiss;                               // S1: null, "missing" or "volMismatch" when McBidAsk is on and no set was used
			public bool FirstAfterConnect;                      // first built bar: ticks before the feed started are missing
			public int BacklogTrades;                           // spec §7.3: trades stamped before this bar's open, popped and not added
			public double TradeHigh = double.NaN, TradeLow = double.NaN, BarHigh, BarLow, BarClose;
			internal readonly SortedDictionary<long, double[]> Ladder = new SortedDictionary<long, double[]>();   // row -> {bid, ask}
			public double Tick;
			public bool HasLadder { get { return Ladder.Count > 0 && (Source == FlowSource.McBidAsk || Source == FlowSource.Quote || Source == FlowSource.TickRule || Source == FlowSource.Aggressor); } }
			public double Delta { get { return Buy - Sell; } }
			public long Row(double price) { return (long)Math.Round(price / Tick); }
			public double Bid(double price) { double[] v; return Ladder.TryGetValue(Row(price), out v) ? v[0] : 0; }
			public double Ask(double price) { double[] v; return Ladder.TryGetValue(Row(price), out v) ? v[1] : 0; }
			/// <summary>Ladder low -> high: {price, bid, ask}, every row from the trade low to the trade high (untraded rows are 0).</summary>
			public List<double[]> Rows()
			{
				var res = new List<double[]>();
				if (!HasLadder) return res;
				for (long r = Row(TradeLow); r <= Row(TradeHigh); r++) { double[] v; Ladder.TryGetValue(r, out v); res.Add(new[] { r * Tick, v != null ? v[0] : 0, v != null ? v[1] : 0 }); }
				return res;
			}
			internal void add(double price, double size, Side side)
			{
				if (double.IsNaN(TradeHigh) || price > TradeHigh) TradeHigh = price;
				if (double.IsNaN(TradeLow) || price < TradeLow) TradeLow = price;
				Trades++;
				Volume += size;
				long r = Row(price);
				double[] v;
				if (!Ladder.TryGetValue(r, out v)) { v = new double[2]; Ladder[r] = v; }
				if (side == Side.Buy) { v[1] += size; Buy += size; } else { v[0] += size; Sell += size; }
			}
		}

		/// <summary>Tick rule with zero-tick inheritance. The first trade ever is BUY. Observe() records a side decided elsewhere (Lee-Ready).</summary>
		public sealed class TickRule
		{
			private double m_last = double.NaN;
			private Side m_side = Side.Buy;
			private readonly double m_eps;
			public TickRule(double tick) { m_eps = tick > 0 ? tick * 0.5 : 1e-9; }
			public Side Peek(double price)
			{
				if (double.IsNaN(m_last)) return Side.Buy;
				if (price > m_last + m_eps) return Side.Buy;
				if (price < m_last - m_eps) return Side.Sell;
				return m_side;
			}
			public void Observe(double price, Side side) { m_last = price; m_side = side; }
			public Side Classify(double price) { Side s = Peek(price); Observe(price, s); return s; }
		}

		private struct Ev { public DateTime Time; public double Price, Size; }

		/// <summary>Event queue of one stream (time-ordered as delivered); consumed from the head.</summary>
		private sealed class Queue
		{
			private readonly List<Ev> m_items = new List<Ev>();
			private int m_head;
			public bool HasNext(DateTime upTo) { return m_head < m_items.Count && m_items[m_head].Time <= upTo; }
			public DateTime NextTime { get { return m_head < m_items.Count ? m_items[m_head].Time : DateTime.MaxValue; } }
			public Ev Pop() { Ev e = m_items[m_head++]; if (m_head > 4096 && m_head * 2 > m_items.Count) { m_items.RemoveRange(0, m_head); m_head = 0; } return e; }
			public void Push(Ev e) { m_items.Add(e); }
			public int Count { get { return m_items.Count - m_head; } }
		}

		/// <summary>
		/// The per-bar engine. Feed trades and quotes in any interleaving (each stream in time order), then CloseBar for each
		/// Data1 bar in order. Not thread-safe: the caller locks when DataLoader callbacks arrive on another thread.
		/// </summary>
		public sealed class FlowEngine
		{
			public readonly FlowConfig Cfg;
			public readonly double Tick;
			private readonly Queue m_trades = new Queue(), m_bids = new Queue(), m_asks = new Queue();
			private readonly TickRule m_rule;
			private double m_bid = double.NaN, m_ask = double.NaN;
			private DateTime m_bidTime = DateTime.MinValue, m_askTime = DateTime.MinValue;
			private readonly List<FootprintBar> m_bars = new List<FootprintBar>();
			private readonly Dictionary<int, BarInput> m_inputs = new Dictionary<int, BarInput>();
			private DateTime m_mixSession = DateTime.MinValue;
			private FlowQuality m_mixWorst = FlowQuality.Exact;
			// MaxBars cap: bars dropped so far and the reset key of the last dropped bar (CumDelta Truncated)
			private int m_dropped;
			private DateTime m_droppedResetKey;
			// first quality downgrade per session (journal)
			// spec §7.3: CloseTime of the previous CloseBar; trades at or before it are backlog
			private DateTime m_lastClose = DateTime.MinValue;
			private bool m_backlogLogged;
			private bool m_volumeBasisNoted;
			/// <summary>Spec §7.3: backlog trades dropped so far (all bars).</summary>
			public long BacklogDropped;
			private DateTime m_qSession = DateTime.MinValue;
			private FlowQuality m_qWorst = FlowQuality.Exact;
			private bool m_qStarted, m_qLogged;
			/// <summary>Journal sink (null = off): one line on the first quality downgrade of each session.</summary>
			public Action<string> journal;
			/// <summary>Warning sink (null = off): one line the first time the MaxBars cap drops a bar.</summary>
			public Action<string> warnOnce;
			public Action<string> infoOnce;   // Info-level one-time notes (calc-thread queue in the strategy)
			/// <summary>Closed bars dropped by the MaxBars cap so far.</summary>
			public int DroppedBars { get { return m_dropped; } }
			/// <summary>The bar cap actually applied: max(10, Cfg.MaxBars).</summary>
			public int BarCap { get { return Math.Max(10, Cfg.MaxBars); } }

			private readonly SortedDictionary<DateTime, double[][]> m_mc = new SortedDictionary<DateTime, double[][]>();
			/// <summary>S1 counters: bars that used an MC set, bars with no set, bars whose set missed the volume tolerance.</summary>
			public int McUsed, McMissing, McVolMismatch;

			/// <summary>Spec S1: queue one MC level set (price, ask-traded, bid-traded per level) for the bar stamped barTime.</summary>
			public void AddMcLevels(DateTime barTime, double[] price, double[] ask, double[] bid)
			{
				if (price == null || ask == null || bid == null || price.Length != ask.Length || price.Length != bid.Length) return;
				m_mc[barTime] = new[] { (double[])price.Clone(), (double[])ask.Clone(), (double[])bid.Clone() };
			}
			public int PendingMcSets { get { return m_mc.Count; } }

			// drop sets older than the bar; return the set stamped exactly at the bar close (or null)
			private double[][] takeMc(DateTime closeTime)
			{
				double[][] hit = null;
				var drop = new List<DateTime>();
				foreach (var kv in m_mc) { if (kv.Key > closeTime) break; drop.Add(kv.Key); if (kv.Key == closeTime) hit = kv.Value; }
				foreach (var k in drop) m_mc.Remove(k);
				return hit;
			}

			private void applyMc(FootprintBar fp, BarInput b)
			{
				fp.ChainDelta = fp.Source == FlowSource.Quote || fp.Source == FlowSource.TickRule || fp.Source == FlowSource.BarUpDown ? fp.Buy - fp.Sell : double.NaN;
				double[][] set = takeMc(b.CloseTime);
				if (set == null) { fp.McMiss = "missing"; McMissing++; return; }
				double ask = 0, bid = 0;
				for (int i = 0; i < set[0].Length; i++) { ask += set[1][i]; bid += set[2][i]; }
				fp.McAsk = ask; fp.McBid = bid;
				double vol = b.Volume;   // the bar total (Bars.Ticks[0]); Bars.Volume (up volume intraday) made every mixed bar a volMismatch
				if (!(vol > 0) || Math.Abs(ask + bid - vol) / vol > Cfg.mcVolTolerance) { fp.McMiss = "volMismatch"; McVolMismatch++; return; }
				fp.McDelta = ask - bid;
				if (Cfg.McBidAsk != EMcBidAsk.Prefer) return;
				McUsed++;
				fp.Ladder.Clear(); fp.Buy = fp.Sell = 0; fp.Volume = 0;
				fp.TradeHigh = fp.TradeLow = double.NaN;
				for (int i = 0; i < set[0].Length; i++)
				{
					double p = set[0][i];
					if (set[1][i] <= 0 && set[2][i] <= 0) continue;
					if (double.IsNaN(fp.TradeHigh) || p > fp.TradeHigh) fp.TradeHigh = p;
					if (double.IsNaN(fp.TradeLow) || p < fp.TradeLow) fp.TradeLow = p;
					long r = fp.Row(p);
					double[] v;
					if (!fp.Ladder.TryGetValue(r, out v)) { v = new double[2]; fp.Ladder[r] = v; }
					v[0] += set[2][i]; v[1] += set[1][i];
				}
				fp.Buy = ask; fp.Sell = bid; fp.Volume = ask + bid;
				fp.Source = FlowSource.McBidAsk; fp.Quality = FlowQuality.High;
			}

			public FlowEngine(double tick, FlowConfig cfg) { Tick = tick > 0 ? tick : 0.01; Cfg = cfg ?? new FlowConfig(); m_rule = new TickRule(Tick); }

			public void AddTrade(DateTime time, double price, double size) { m_trades.Push(new Ev { Time = time, Price = price, Size = size }); }
			public void AddQuote(DateTime time, bool isBid, double price) { (isBid ? m_bids : m_asks).Push(new Ev { Time = time, Price = price }); }
			public int PendingTrades { get { return m_trades.Count; } }
			public int Count { get { return m_bars.Count; } }

			/// <summary>The closed bar `ago` bars back from the newest closed bar (0 = newest), or null.</summary>
			public FootprintBar Closed(int ago) { int i = m_bars.Count - 1 - ago; return ago >= 0 && i >= 0 ? m_bars[i] : null; }
			public FootprintBar ByNumber(int bar)
			{
				for (int i = m_bars.Count - 1; i >= 0; i--) { if (m_bars[i].BarNumber == bar) return m_bars[i]; if (m_bars[i].BarNumber < bar) break; }
				return null;
			}

			private void applyQuotes(DateTime upTo)
			{
				while (m_bids.HasNext(upTo)) { Ev q = m_bids.Pop(); m_bid = q.Price; m_bidTime = q.Time; }
				while (m_asks.HasNext(upTo)) { Ev q = m_asks.Pop(); m_ask = q.Price; m_askTime = q.Time; }
			}

			/// <summary>Build the footprint of a closed Data1 bar from the events stamped at or before its close.</summary>
			public FootprintBar CloseBar(BarInput b)
			{
				var fp = new FootprintBar { BarNumber = b.BarNumber, CloseTime = b.CloseTime, SessionKey = b.SessionKey, ResetKey = b.ResetKey, Tick = Tick,
					BarHigh = b.High, BarLow = b.Low, BarClose = b.Close, FirstAfterConnect = m_bars.Count == 0 };
				bool tickRegime = b.Regime == Regime.TickStream || b.Regime == Regime.IogRealTime || b.Regime == Regime.IogMagnifierOneTick;
				bool quotes = b.Regime == Regime.TickStream && Cfg.QuotesExpected;
				// always consume this bar's events, whatever the regime, so later bars never see them
				DateTime backlogCut = b.TimeBased && b.OpenTime > m_lastClose ? b.OpenTime : m_lastClose;
				while (m_trades.HasNext(b.CloseTime))
				{
					Ev t = m_trades.Pop();
					if (t.Time <= backlogCut)
					{
						// spec §7.3: delivered after its bar closed (e.g. as BarsOnly) - not this bar's trade. Only the tick rule's last price moves.
						fp.BacklogTrades++;
						m_rule.Classify(t.Price);
						continue;
					}
					Side s;
					if (quotes)
					{
						applyQuotes(t.Time);
						bool fresh = !double.IsNaN(m_bid) && !double.IsNaN(m_ask) && m_bid < m_ask
							&& (t.Time - m_bidTime).TotalMilliseconds <= Cfg.MaxQuoteAgeMs && (t.Time - m_askTime).TotalMilliseconds <= Cfg.MaxQuoteAgeMs;
						if (!fresh) { s = m_rule.Peek(t.Price); fp.StaleQuoteTrades++; }
						else if (t.Price >= m_ask - Tick * 1e-6) s = Side.Buy;
						else if (t.Price <= m_bid + Tick * 1e-6) s = Side.Sell;
						else s = m_rule.Peek(t.Price);   // inside the spread: tick rule
						m_rule.Observe(t.Price, s);
					}
					else s = m_rule.Classify(t.Price);
					if (tickRegime && t.Size > 0) fp.add(t.Price, t.Size, s);
					else if (tickRegime) { m_rule.Observe(t.Price, s); }
				}
				if (quotes) applyQuotes(b.CloseTime);
				if (b.CloseTime > m_lastClose) m_lastClose = b.CloseTime;
				if (fp.BacklogTrades > 0)
				{
					BacklogDropped += fp.BacklogTrades;
					if (!m_backlogLogged && journal != null)
					{
						m_backlogLogged = true;
						journal("OF tick backlog dropped bar(" + b.BarNumber + ") trades(" + fp.BacklogTrades + ") before(" + backlogCut.ToString("yyyy-MM-dd HH:mm:ss") + ") (once; total in BacklogDropped)");
					}
				}

				if (b.Regime == Regime.IogHistoryNoMagnifier || Cfg.BuildVolumeOn == VolumeBasis.TickCount)
				{
					clearToUnavailable(fp);
					if (Cfg.BuildVolumeOn == VolumeBasis.TickCount && Cfg.McBidAsk != EMcBidAsk.Off)
					{
						fp.McMiss = "volumeBasis";   // the level sets are contracts, the bar total is a tick count: never compared
						if (!m_volumeBasisNoted && infoOnce != null) { m_volumeBasisNoted = true; infoOnce(VolumeBasisInfo); }
					}
				}
				else if (tickRegime && fp.Trades > 0)
				{
					fp.Source = quotes && fp.StaleQuoteTrades == 0 ? FlowSource.Quote : FlowSource.TickRule;
					fp.Quality = fp.Source == FlowSource.Quote ? FlowQuality.High : FlowQuality.Medium;
				}
				else if (tickRegime && b.Volume <= 0)
				{
					fp.Source = quotes ? FlowSource.Quote : FlowSource.TickRule;   // an empty bar: nothing traded, delta 0
					fp.Quality = quotes ? FlowQuality.High : FlowQuality.Medium;
				}
				else if (BarUpDownQualifies(Cfg.BuildVolumeOn, b.Volume, b.UpTicks, b.DownTicks))
				{
					fp.Ladder.Clear(); fp.Trades = 0;
					fp.Source = FlowSource.BarUpDown; fp.Quality = FlowQuality.Low;
					fp.Buy = b.UpTicks; fp.Sell = b.DownTicks; fp.Volume = b.Volume;
				}
				else clearToUnavailable(fp);
				if (Cfg.McBidAsk != EMcBidAsk.Off && b.Regime != Regime.IogHistoryNoMagnifier && Cfg.BuildVolumeOn != VolumeBasis.TickCount) applyMc(fp, b);

				// AllowMixed = false: Quote/TickRule bars of one session report the worst tag seen so far in that session
				if (!Cfg.AllowMixed && (fp.Source == FlowSource.McBidAsk || fp.Source == FlowSource.Quote || fp.Source == FlowSource.TickRule))
				{
					if (fp.SessionKey != m_mixSession) { m_mixSession = fp.SessionKey; m_mixWorst = FlowQuality.Exact; }
					if (fp.Quality > m_mixWorst) m_mixWorst = fp.Quality;
					fp.Quality = m_mixWorst;
				}
				noteQuality(fp);
				m_bars.Add(fp);
				m_inputs[b.BarNumber] = b;
				if (m_bars.Count > BarCap)
				{
					m_droppedResetKey = m_bars[0].ResetKey;
					m_inputs.Remove(m_bars[0].BarNumber); m_bars.RemoveAt(0);
					if (m_dropped++ == 0 && warnOnce != null)
						warnOnce("OF MaxBars cap " + BarCap + " hit at bar " + fp.BarNumber + ": oldest footprints dropped; CumDelta is Truncated only if a reset period exceeds the cap");
				}
				return fp;
			}

			// journal the first bar of a session whose (final) quality is worse than every earlier bar of that session
			private void noteQuality(FootprintBar fp)
			{
				if (fp.SessionKey != m_qSession || !m_qStarted) { m_qSession = fp.SessionKey; m_qWorst = fp.Quality; m_qStarted = true; m_qLogged = false; return; }
				if (fp.Quality <= m_qWorst) return;
				FlowQuality from = m_qWorst;
				m_qWorst = fp.Quality;
				if (m_qLogged) return;
				m_qLogged = true;
				if (journal != null)
					journal("OF quality downgrade session(" + fp.SessionKey.ToString("yyyy-MM-dd") + ") bar(" + fp.BarNumber + ") " + from + "->" + fp.Quality
						+ " source(" + fp.Source + ")" + (fp.StaleQuoteTrades > 0 ? " staleQuoteTrades(" + fp.StaleQuoteTrades + ")" : "") + (fp.McMiss != null ? " mcMiss(" + fp.McMiss + ")" : ""));
			}

			private static void clearToUnavailable(FootprintBar fp)
			{
				fp.Ladder.Clear(); fp.Buy = fp.Sell = 0; fp.Trades = 0;
				fp.Source = FlowSource.Unavailable; fp.Quality = FlowQuality.Unknown;
			}

			/// <summary>Info (once per engine) when McBidAsk is on and FlowCfg.BuildVolumeOn = TickCount. Tick Count is a valid setting on
			/// non-futures charts (Ettienne 2026-11-01): no Warning about the setting, only the McBidAsk tier is Unavailable. Futures +
			/// Tick Count never gets here: _VolumeProfileBase.StartCalc logs FlowEngine.VolumeBasisError (whatever TraceLevel) and throws.</summary>
			public const string VolumeBasisInfo = "OF McBidAsk Unavailable reason(volumeBasis): Build Volume On = Tick Count; footprint is contracts, bar total is tick count";

			/// <summary>The StartCalc error for a futures data stream built on Tick Count (Ettienne 2026-11-01), or null when it may run.
			/// isFuture is the typed Category == ESymbolCategory.Future or ESymbolCategory.FutureRolover of that stream; basis is its typed
			/// Info.BVO mapped by _VolumeProfileBase.volumeBasisOf; stream names it, e.g. "Data2 @ES". Checked for Data1 and for every
			/// Data2..N that feeds a profile or a VWAP (_VolumeProfileBase.volumeFedStreams). Non-futures on Tick Count and an Unknown
			/// basis pass (null).</summary>
			public static string VolumeBasisError(bool isFuture, VolumeBasis basis, string stream)
			{
				if (!isFuture || basis != VolumeBasis.TickCount) return null;
				return "Futures data stream " + stream + ": Build Volume On must be Trade Volume (Format Instrument > Settings), but it is set to Tick Count. "
					+ "Tick Count counts trades, not contracts, so volume profiles, VWAP and order flow would be wrong.";
			}

			/// <summary>T7 gate: Trade Volume basis and Up + Down == Volume (the bar total, Bars.Ticks[0]) on this bar. Unchanged-price
			/// volume that MC does not book as up or down makes Up + Down differ from the total, and the bar does not qualify.</summary>
			public static bool BarUpDownQualifies(VolumeBasis basis, double volume, double up, double down)
			{
				if (basis != VolumeBasis.TradeVolume || volume <= 0 || up < 0 || down < 0) return false;
				return Math.Abs(up + down - volume) <= 1e-9 * Math.Max(1, volume);
			}

			public FlowValue BarDelta(int ago)
			{
				FootprintBar fp = Closed(ago);
				if (fp == null || fp.Source == FlowSource.Unavailable) return FlowValue.Unavailable();
				return new FlowValue { Value = fp.Delta, Source = fp.Source, Quality = fp.Quality };
			}

			/// <summary>Cumulative delta from the first bar of `ago`'s reset key through `ago`. Any Unavailable bar makes it Unavailable.</summary>
			public FlowValue CumDelta(int ago)
			{
				FootprintBar last = Closed(ago);
				if (last == null) return FlowValue.Unavailable();
				double sum = 0; FlowSource src = FlowSource.Aggressor; FlowQuality q = FlowQuality.Exact;
				int i = m_bars.Count - 1 - ago;
				for (; i >= 0 && m_bars[i].ResetKey == last.ResetKey; i--)
				{
					FootprintBar fp = m_bars[i];
					if (fp.Source == FlowSource.Unavailable) return FlowValue.Unavailable();
					sum += fp.Delta;
					if (fp.Source > src) src = fp.Source;
					if (fp.Quality > q) q = fp.Quality;
				}
				// reached the oldest kept bar while the period continues into the dropped bars: partial sum
				bool truncated = i < 0 && m_dropped > 0 && m_droppedResetKey == last.ResetKey;
				return new FlowValue { Value = sum, Source = src, Quality = q, Truncated = truncated };
			}

			public List<Imbalance> DiagonalImbalances(int ago, double ratio = 3.0, double minVol = 10)
			{
				return Imbalances(Closed(ago), ratio, minVol, Cfg.ExcludeExtremesTickRule);
			}

			public StackedResult StackedImbalances(int ago, double ratio = 3.0, double minVol = 10, int minStack = 3)
			{
				return Stacked(Closed(ago), ratio, minVol, minStack, Cfg.ExcludeExtremesTickRule);
			}

			/// <summary>
			/// Absorption at bar `ago` (Rev 2.5). side = Sell: bids absorbing within edgeTicks of the bar low, Σ Bid >= volMult x the
			/// mean level volume of the bar, close >= low + holdTicks, and the next bar does not trade below that low. Mirror for
			/// Buy at the high. Value 1 / 0. Developing until the next bar has closed. Requires quality High or better, so with
			/// TickRule / BarUpDown it is Unknown.
			/// </summary>
			public FlowValue Absorption(int ago, Side side, int edgeTicks = 2, double volMult = 2.0, int holdTicks = 1)
			{
				FootprintBar fp = Closed(ago);
				if (fp == null || side == Side.Unknown || !fp.HasLadder || fp.Quality > FlowQuality.High) return FlowValue.Unavailable();
				List<double[]> rows = fp.Rows();
				double total = 0; foreach (var r in rows) total += r[1] + r[2];
				double mean = rows.Count > 0 ? total / rows.Count : 0;
				double edge = 0, tol = Tick * 1e-6;
				foreach (var r in rows)
				{
					if (side == Side.Sell && r[0] <= fp.TradeLow + edgeTicks * Tick + tol) edge += r[1];
					if (side == Side.Buy && r[0] >= fp.TradeHigh - edgeTicks * Tick - tol) edge += r[2];
				}
				bool ok = mean > 0 && edge >= volMult * mean - tol
					&& (side == Side.Sell ? fp.BarClose >= fp.TradeLow + holdTicks * Tick - tol : fp.BarClose <= fp.TradeHigh - holdTicks * Tick + tol);
				FootprintBar next = Closed(ago - 1);
				bool developing = next == null;
				if (ok && next != null)
				{
					BarInput nb = m_inputs.ContainsKey(next.BarNumber) ? m_inputs[next.BarNumber] : default(BarInput);
					double nLow = !double.IsNaN(next.TradeLow) && next.HasLadder ? Math.Min(next.TradeLow, nb.Low) : nb.Low;
					double nHigh = !double.IsNaN(next.TradeHigh) && next.HasLadder ? Math.Max(next.TradeHigh, nb.High) : nb.High;
					if (side == Side.Sell && nLow < fp.TradeLow - tol) ok = false;
					if (side == Side.Buy && nHigh > fp.TradeHigh + tol) ok = false;
				}
				return new FlowValue { Value = ok ? 1 : 0, Source = fp.Source, Quality = fp.Quality, Developing = developing };
			}
		}

		/// <summary>
		/// Diagonal imbalances (Rev 2.5): buy at p when Ask[p] >= ratio x Bid[p - 1 tick] and Ask[p] >= minVol; sell at p when
		/// Bid[p] >= ratio x Ask[p + 1 tick] and Bid[p] >= minVol. A zero diagonal (inside the ladder) counts as +Infinity; a
		/// missing diagonal level (ladder edge) never counts. ratio is floored at 3.0 and minVol at 1. Under TickRule the
		/// quality is Low and the bar high/low levels are excluded when excludeExtremes. Empty without a ladder.
		/// </summary>
		public static List<Imbalance> Imbalances(FootprintBar fp, double ratio, double minVol, bool excludeExtremes)
		{
			var res = new List<Imbalance>();
			if (fp == null || !fp.HasLadder) return res;
			ratio = Math.Max(3.0, ratio);
			minVol = Math.Max(1.0, minVol);
			bool tr = fp.Source == FlowSource.TickRule;
			FlowQuality q = tr ? FlowQuality.Low : fp.Quality;
			long lo = fp.Row(fp.TradeLow), hi = fp.Row(fp.TradeHigh);
			for (long r = lo; r <= hi; r++)
			{
				if (tr && excludeExtremes && (r == lo || r == hi)) continue;
				double p = r * fp.Tick;
				double ask = fp.Ask(p), bid = fp.Bid(p);
				if (r > lo && ask >= minVol)
				{
					double d = fp.Bid(p - fp.Tick);
					if (ask >= ratio * d) res.Add(new Imbalance { Price = p, Side = Side.Buy, Ratio = d > 0 ? ask / d : double.PositiveInfinity, Quality = q });
				}
				if (r < hi && bid >= minVol)
				{
					double d = fp.Ask(p + fp.Tick);
					if (bid >= ratio * d) res.Add(new Imbalance { Price = p, Side = Side.Sell, Ratio = d > 0 ? bid / d : double.PositiveInfinity, Quality = q });
				}
			}
			return res;
		}

		/// <summary>Stacked imbalances: runs of >= minStack (floored at 3) consecutive same-side imbalance prices, both sides.</summary>
		public static StackedResult Stacked(FootprintBar fp, double ratio, double minVol, int minStack, bool excludeExtremes)
		{
			var res = new StackedResult();
			if (fp == null || !fp.HasLadder) return res;
			minStack = Math.Max(3, minStack);
			res.Quality = fp.Source == FlowSource.TickRule ? FlowQuality.Low : fp.Quality;
			List<Imbalance> all = Imbalances(fp, ratio, minVol, excludeExtremes);
			foreach (Side side in new[] { Side.Buy, Side.Sell })
			{
				var px = new List<long>();
				foreach (var i in all) if (i.Side == side) px.Add(fp.Row(i.Price));
				int a = 0;
				while (a < px.Count)
				{
					int b = a;
					while (b + 1 < px.Count && px[b + 1] == px[b] + 1) b++;
					if (b - a + 1 >= minStack) res.Runs.Add(new double[] { px[a] * fp.Tick, px[b] * fp.Tick, b - a + 1, (int)side });
					a = b + 1;
				}
			}
			return res;
		}
		/// <summary>Recalc spec §4.1: the McBidAsk level sets of one request (same {price, ask, bid} triple as AddMcLevels). Kept by the
		/// strategy across StartCalc/StopCalc (and so across ExecControl.Recalculate) and replayed into each new FlowEngine.
		/// Not thread-safe: the caller locks (DataLoader callbacks add, the calc thread replays).</summary>
		public sealed class McLevelCache
		{
			private SortedDictionary<DateTime, double[][]> m_sets;   // lazy
			/// <summary>Request identity (symbol, feed, resolution, chart type, mode, first bar); the caller clears the cache when it changes.</summary>
			public string Signature;
			/// <summary>The History event of the request completed.</summary>
			public bool HistoryDone;
			/// <summary>Window that was requested (chart time). Coverage is judged on it, not on First/Last: the feed may have no levels at the edges.</summary>
			public DateTime RequestFrom = DateTime.MaxValue, RequestTo = DateTime.MinValue;
			public DateTime First = DateTime.MaxValue, Last = DateTime.MinValue;
			public int Sets;
			public long Levels;

			public void Add(DateTime barTime, double[] price, double[] ask, double[] bid)
			{
				if (price == null || ask == null || bid == null || price.Length != ask.Length || price.Length != bid.Length) return;
				if (m_sets == null) m_sets = new SortedDictionary<DateTime, double[][]>();
				double[][] old;
				if (m_sets.TryGetValue(barTime, out old)) Levels -= old[0].Length; else Sets++;
				m_sets[barTime] = new[] { (double[])price.Clone(), (double[])ask.Clone(), (double[])bid.Clone() };
				Levels += price.Length;
				if (barTime < First) First = barTime;
				if (barTime > Last) Last = barTime;
			}

			/// <summary>Feed every set to the engine (AddMcLevels clones again). Returns the number of sets.</summary>
			public int ReplayInto(FlowEngine e)
			{
				if (e == null || m_sets == null) return 0;
				foreach (var kv in m_sets) e.AddMcLevels(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2]);
				return m_sets.Count;
			}

			public void Clear()
			{
				m_sets = null; Signature = null; HistoryDone = false;
				RequestFrom = DateTime.MaxValue; RequestTo = DateTime.MinValue;
				First = DateTime.MaxValue; Last = DateTime.MinValue; Sets = 0; Levels = 0;
			}

			/// <summary>History completed for a requested window that contains [from, to].</summary>
			public bool Covers(DateTime from, DateTime to) { return HistoryDone && RequestFrom <= from && RequestTo >= to; }

			/// <summary>Why the cache cannot serve sig over [from, to] (McCacheMiss.None = hit). A cache that holds ANOTHER request's
			/// signature is a Signature miss even when it holds no level set (gate-ABA: A -> B with no levels -> A must still reset the gate).</summary>
			public McCacheMiss Miss(string sig, DateTime from, DateTime to)
			{
				if (Signature != null && Signature != sig) return McCacheMiss.Signature;
				if (Sets == 0 && (Signature == null || HistoryDone)) return McCacheMiss.Empty;   // a completed request with no level set is not a hit
				if (Signature == null) return McCacheMiss.Signature;   // level sets never stamped with a request signature (was the 2nd Signature != sig, now only reachable here)
				if (!HistoryDone) return McCacheMiss.Incomplete;
				if (!Covers(from, to)) return McCacheMiss.Coverage;
				return McCacheMiss.None;
			}

			/// <summary>Miss reason for the journal (empty | signature | incomplete | coverage), or null on a hit.</summary>
			public string MissReason(string sig, DateTime from, DateTime to) { return MissName(Miss(sig, from, to)); }

			/// <summary>Journal text of a miss (null for None).</summary>
			public static string MissName(McCacheMiss m)
			{
				switch (m)
				{
					case McCacheMiss.Empty: return "empty";
					case McCacheMiss.Signature: return "signature";
					case McCacheMiss.Incomplete: return "incomplete";
					case McCacheMiss.Coverage: return "coverage";
					default: return null;
				}
			}
		}

		/// <summary>Recalc spec §4.2: why McLevelCache.Miss refused a replay (None = hit).</summary>
		public enum McCacheMiss { None = 0, Empty = 1, Signature = 2, Incomplete = 3, Coverage = 4 }

		/// <summary>Recalc spec §4.1: outcome of McRecalcGate.Decide.</summary>
		public enum RecalcDecision { Wait = 0, Recalc = 1, SkipNoMissed = 2, SkipGuard = 3, SkipAutoTrading = 4, SkipDisabled = 5, SkipProcessGuard = 6 }

		/// <summary>Review B1: process-wide recalc loop guard (pure; the caller holds one static instance under a static lock). At most
		/// max recalcs per signature within window, whatever strategy object asks. Not thread-safe: the caller locks.</summary>
		public sealed class McRecalcLoopGuard
		{
			private readonly Dictionary<string, List<DateTime>> m_bySig = new Dictionary<string, List<DateTime>>();

			/// <summary>Recalcs recorded for sig within (now − window, now].</summary>
			public int CountFor(string sig, DateTime now, TimeSpan window)
			{
				List<DateTime> l = prune(sig, now, window);
				return l == null ? 0 : l.Count;
			}

			/// <summary>Records a recalc and returns true when fewer than max are recorded for sig within the window; else false.</summary>
			public bool TryAcquire(string sig, DateTime now, int max, TimeSpan window)
			{
				if (sig == null) return false;
				if (m_bySig.Count > 64) pruneAll(now, window);
				List<DateTime> l = prune(sig, now, window);
				if ((l == null ? 0 : l.Count) >= max) return false;
				if (l == null) m_bySig[sig] = l = new List<DateTime>();
				l.Add(now);
				return true;
			}

			/// <summary>Signatures tracked (tests).</summary>
			public int Signatures { get { return m_bySig.Count; } }

			private List<DateTime> prune(string sig, DateTime now, TimeSpan window)
			{
				List<DateTime> l;
				if (sig == null || !m_bySig.TryGetValue(sig, out l)) return null;
				l.RemoveAll(t => t <= now - window || t > now);
				if (l.Count == 0) { m_bySig.Remove(sig); return null; }
				return l;
			}

			private void pruneAll(DateTime now, TimeSpan window)
			{
				var keys = new List<string>(m_bySig.Keys);
				foreach (string k in keys) prune(k, now, window);
			}
		}

		/// <summary>Review B2: the stream of a legacy tick-request callback, from its state (stateBase + stream, set before
		/// BeginLoadData); -1 for anything else (an earlier pass's request, a chunk id, a non-int state).</summary>
		public static int LegacyStream(object state, int stateBase, int streams)
		{
			if (!(state is int) || stateBase <= 0) return -1;
			int s = (int)state - stateBase;
			return s >= 0 && s < streams && s < 3 ? s : -1;
		}

		/// <summary>Review B2 (revised): a McBidAsk level callback belongs to the current request when its state is the current
		/// request id (set before BeginLoadData; 0 = none). The result object is NOT compared here (see SameResult).</summary>
		public static bool IsCurrentRequest(object state, int currentId)
		{
			return currentId > 0 && state is int && (int)state == currentId;
		}

		/// <summary>Review 2 deviation 1: whether the callback's result is the stored BeginLoadData result (true while none is
		/// stored yet). Diagnostic only: it is not verified that MC passes the same object to every callback (revisit after the
		/// probe), so a mismatch is logged, never rejected.</summary>
		public static bool SameResult(object result, object currentResult)
		{
			return currentResult == null || ReferenceEquals(result, currentResult);
		}

		/// <summary>Review 2 item 1: the process-wide guard key: the strategy's type name and the request signature (no per-chart
		/// member is confirmed in the typed API). null when there is no signature.</summary>
		public static string GuardKey(string strategyType, string sig)
		{
			return sig == null ? null : (strategyType ?? "") + "|" + sig;
		}

		/// <summary>P-RC-EResolution (Ettienne's EResolution dump, 2026-11-01): how a Regular chart's resolution is treated for the
		/// OpenTime backlog cut. Exact = Second/Minute/Hour/Day (bar length = Size x unit); Calendar = Week/Month/Quarter/Year
		/// (calendar-aligned: no fixed length, so no OpenTime cut); NonTime = everything else (tick, volume, range, Custom*, ...).</summary>
		public enum ResolutionKind { NonTime, Exact, Calendar }

		/// <summary>Exact time-based bar length = size x unit. TimeSpan.Zero when unit or size is not positive or the product
		/// would overflow (the caller then applies no OpenTime bound).</summary>
		public static TimeSpan ExactBarLength(TimeSpan unit, long size)
		{
			if (unit <= TimeSpan.Zero || size <= 0) return TimeSpan.Zero;
			if (size > TimeSpan.MaxValue.Ticks / unit.Ticks) return TimeSpan.Zero;
			return TimeSpan.FromTicks(unit.Ticks * size);
		}

		/// <summary>Recalc spec §4.1: decides the one recalculation after the McBidAsk history completes. No MultiCharts types. The guard
		/// counts recalcs per cache signature and survives passes; the missed-bar counters are per pass (BeginPass).</summary>
		public sealed class McRecalcGate
		{
			public int RecalcCount;
			public string RecalcSignature;
			public int MissedBeforeDone;
			public DateTime MissedFirst = DateTime.MaxValue, MissedLast = DateTime.MinValue;
			/// <summary>StartCalc passes in the object's lifetime.</summary>
			public int Pass;

			/// <summary>New calculation pass: Pass++ and the missed-bar counters restart. RecalcCount/RecalcSignature are kept.</summary>
			public void BeginPass() { Pass++; MissedBeforeDone = 0; MissedFirst = DateTime.MaxValue; MissedLast = DateTime.MinValue; }

			/// <summary>A bar closed with no level set before the history completed.</summary>
			public void NoteMissed(DateTime closeTime)
			{
				MissedBeforeDone++;
				if (closeTime < MissedFirst) MissedFirst = closeTime;
				if (closeTime > MissedLast) MissedLast = closeTime;
			}

			/// <summary>Recalcs already done for this signature.</summary>
			public int CountFor(string sig) { return sig == RecalcSignature ? RecalcCount : 0; }

			public RecalcDecision Decide(bool historyDone, bool cacheCoversMissed, bool autoTrading, bool enabled, int max, string sig)
			{
				if (!enabled) return RecalcDecision.SkipDisabled;
				if (!historyDone) return RecalcDecision.Wait;
				if (MissedBeforeDone == 0 || !cacheCoversMissed) return RecalcDecision.SkipNoMissed;
				if (autoTrading) return RecalcDecision.SkipAutoTrading;
				if (CountFor(sig) >= max) return RecalcDecision.SkipGuard;
				return RecalcDecision.Recalc;
			}

			/// <summary>Gate-ABA fix: the McBidAsk cache missed on SIGNATURE (a different request), so this object's recalc history no longer
			/// describes the data: drop it (RecalcSignature = null, RecalcCount = 0). A real recalc loop always comes back with the SAME
			/// signature (a cache hit or an incomplete/coverage miss), so it never reaches this; the process-wide McRecalcLoopGuard
			/// (McRecalcGuardMaxPerKey per McRecalcGuardWindowSec per key) stays the backstop. Missed-bar counters and Pass are kept.</summary>
			public void Forget() { RecalcSignature = null; RecalcCount = 0; }

			/// <summary>Journal line of a skip decision (not the process guard): reason, this object's recalcs for sig against max, and
			/// the signature the count belongs to ("none" after Forget or before any recalc), so guard (a previous recalc) and max = 0 differ.</summary>
			public string SkipLine(RecalcDecision d, string sig, int max)
			{
				return "OF McBidAsk recalc skipped reason(" + SkipReason(d) + ") recalcCount(" + CountFor(sig) + "/" + max + ") recalcSig(" + (RecalcSignature ?? "none") + ")";
			}

			/// <summary>Record a recalc for sig (call immediately before ExecControl.Recalculate()).</summary>
			public void NoteRecalc(string sig)
			{
				if (sig != RecalcSignature) { RecalcSignature = sig; RecalcCount = 0; }
				RecalcCount++;
			}

			/// <summary>Journal reason of a skip decision.</summary>
			public static string SkipReason(RecalcDecision d)
			{
				switch (d)
				{
					case RecalcDecision.SkipNoMissed: return "noMissedBars";
					case RecalcDecision.SkipGuard: return "guard";
					case RecalcDecision.SkipAutoTrading: return "autoTrading";
					case RecalcDecision.SkipDisabled: return "disabled";
					case RecalcDecision.SkipProcessGuard: return "processGuard";
					default: return d.ToString();
				}
			}
		}
		/// <summary>volMismatch diag: picks the first Max closed bars and the first Max volMismatch bars of a pass (counted apart).</summary>
		public sealed class McVolDiag
		{
			public int Max = 10;
			public int Closed, Mismatch;
			/// <summary>Count one closed bar; true when its diag line is logged.</summary>
			public bool Take(string mcMiss)
			{
				bool first = Closed < Max; Closed++;
				bool mm = mcMiss == "volMismatch" && Mismatch < Max;
				if (mm) Mismatch++;
				return first || mm;
			}
			/// <summary>OF McBidAsk diag vol bar(n) t(..) volume(Bars.Volume[0]) ticks(Bars.Ticks[0]) up(..) down(..) prevTicks(Bars.Ticks[1])
			/// mcAsk(..) mcBid(..) mcSum(..) miss(..|none). NaN = not available (no level set / first bar).</summary>
			public static string Line(int bar, DateTime t, double volume, double ticks, double up, double down, double prevTicks, double mcAsk, double mcBid, string miss)
			{
				var ci = System.Globalization.CultureInfo.InvariantCulture;
				return "OF McBidAsk diag vol bar(" + bar + ") t(" + t.ToString("yyyy-MM-dd HH:mm:ss", ci) + ") volume(" + volume.ToString("R", ci) + ") ticks(" + ticks.ToString("R", ci)
					+ ") up(" + up.ToString("R", ci) + ") down(" + down.ToString("R", ci) + ") prevTicks(" + prevTicks.ToString("R", ci) + ") mcAsk(" + mcAsk.ToString("R", ci)
					+ ") mcBid(" + mcBid.ToString("R", ci) + ") mcSum(" + (mcAsk + mcBid).ToString("R", ci) + ") miss(" + (miss ?? "none") + ")";
			}
		}

		/// <summary>Tick-chunk wait mode (TickChunkWaitMs > 0): per-session count of the closed bars' flow source, so a session that
		/// stays all-Quote (or all-TickRule, or falls to BarUpDown) leaves one line in the trace. Add returns the finished session's line
		/// when the session key changes; Flush returns the open session's line (end of pass). Each session is reported at most once per
		/// tally (deduped: a session key seen again later, e.g. a bar replayed after a flush, is not reported twice). Not thread-safe.</summary>
		public sealed class SessionSourceTally
		{
			private DateTime m_session = DateTime.MinValue;
			private int m_bars, m_aggressor, m_mc, m_quote, m_tickRule, m_barUpDown, m_unavailable;
			private HashSet<DateTime> m_reported;   // lazy

			/// <summary>Count one closed bar of session `session`; returns the previous session's line when the session changed, else null.</summary>
			public string Add(DateTime session, FlowSource source)
			{
				string line = null;
				if (session != m_session) { line = Flush(); m_session = session; }
				m_bars++;
				switch (source)
				{
					case FlowSource.Aggressor: m_aggressor++; break;
					case FlowSource.McBidAsk: m_mc++; break;
					case FlowSource.Quote: m_quote++; break;
					case FlowSource.TickRule: m_tickRule++; break;
					case FlowSource.BarUpDown: m_barUpDown++; break;
					default: m_unavailable++; break;
				}
				return line;
			}

			/// <summary>The open session's line (null when it has no bars or was already reported); the counters restart.</summary>
			public string Flush()
			{
				string line = null;
				if (m_bars > 0)
				{
					if (m_reported == null) m_reported = new HashSet<DateTime>();
					if (m_reported.Add(m_session))
						line = "OF tick source session(" + m_session.ToString("yyyy-MM-dd") + ") bars(" + m_bars + ") quote(" + m_quote + ") tickRule(" + m_tickRule
							+ ") mcBidAsk(" + m_mc + ") aggressor(" + m_aggressor + ") barUpDown(" + m_barUpDown + ") unavailable(" + m_unavailable + ")";
				}
				m_bars = m_aggressor = m_mc = m_quote = m_tickRule = m_barUpDown = m_unavailable = 0;
				return line;
			}
		}

		/// <summary>One row of a session template (bar-zone clock). The host copies Bars.Sessions.</summary>
		public struct WeekSession
		{
			public DayOfWeek StartDay, EndDay;
			public TimeSpan Start, End;
			public WeekSession(DayOfWeek startDay, TimeSpan start, DayOfWeek endDay, TimeSpan end)
			{ StartDay = startDay; Start = start; EndDay = endDay; End = end; }
		}

		/// <summary>M6b. The template's listed session end, converted from the bar zone to US Eastern, as a time of day.
		/// Listed end = the latest weekday session End after that conversion (R14: the template row, not the last bar's close).
		/// On the stale CME Globex template, 16:30 CT is 17:30 ET. Spot FX is unsupported: the Error line is written, then the throw.
		/// Same conversion as InstrumentSessions.ToExchange (spring-forward gap moved +1h; identity when the zones match).</summary>
		public static TimeSpan ChunkBoundaryEt(WeekSession[] sessions, TimeZoneInfo barZone, TimeZoneInfo eastern, DateTime anchor, bool spotFx, string name, Action<string> errorSink)
		{
			if (spotFx) Fail(errorSink, name, "spot FX is unsupported (no central traded volume or aggressor side)", "disable order flow, or run the idea on the currency future (6E)");
			if (sessions == null || sessions.Length == 0) Fail(errorSink, name, "sessions:none", "check the chart's session template");
			if (barZone == null || eastern == null) Fail(errorSink, name, "tz:bar", "set the chart time zone");
			DateTime sunday = DateTime.SpecifyKind(anchor.Date, DateTimeKind.Unspecified).AddDays(-(int)anchor.DayOfWeek);
			TimeSpan best = TimeSpan.Zero;
			bool any = false;
			for (int i = 0; i < sessions.Length; i++)
			{
				DateTime end = ToZone(sunday.AddDays((int)sessions[i].EndDay) + sessions[i].End, barZone, eastern);
				DateTime key = end.TimeOfDay == TimeSpan.Zero ? end.Date.AddDays(-1) : end.Date;
				if (key.DayOfWeek == DayOfWeek.Saturday || key.DayOfWeek == DayOfWeek.Sunday) continue;
				if (!any || end.TimeOfDay > best) { best = end.TimeOfDay; any = true; }
			}
			if (!any) Fail(errorSink, name, "sessions:none", "check the chart's session template");
			return best;
		}

		static DateTime ToZone(DateTime wall, TimeZoneInfo src, TimeZoneInfo dst)
		{
			if (src.Id == dst.Id) return wall;
			if (src.IsInvalidTime(wall)) wall = wall.AddHours(1);
			return TimeZoneInfo.ConvertTime(wall, src, dst);
		}

		static void Fail(Action<string> errorSink, string name, string reason, string action)
		{
			string msg = "M-INSTR: FLOW cannot run on " + (name ?? "") + ": " + reason + ". " + action + ".";
			if (errorSink != null) errorSink(msg);
			throw new InvalidOperationException(msg);
		}

		/// <summary>Tick-chunk spec §5.1: one Globex day's tick window, US Eastern: [GlobexDate − 1 day + boundary, GlobexDate + boundary).</summary>
		public struct DayChunk { public DateTime GlobexDate, FromEt, ToEt; }

		/// <summary>Tick-chunk spec §5.1: one chunk per Globex date in [firstGx, lastGx] that is not Saturday, Sunday or closed.
		/// Monday's chunk starts Sunday at the boundary. closedOut (optional) receives the closed weekdays.</summary>
		public static List<DayChunk> PlanGlobexDays(DateTime firstGx, DateTime lastGx, Func<DateTime, bool> closedDay, TimeSpan boundaryEt, List<DateTime> closedOut = null)
		{
			var days = new List<DayChunk>();
			for (DateTime d = firstGx.Date; d <= lastGx.Date; d = d.AddDays(1))
			{
				if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
				if (closedDay != null && closedDay(d)) { if (closedOut != null) closedOut.Add(d); continue; }
				days.Add(new DayChunk { GlobexDate = d, FromEt = d.AddDays(-1) + boundaryEt, ToEt = d + boundaryEt });
			}
			return days;
		}

		public enum ChunkState { Pending = 0, InFlight = 1, Done = 2, Failed = 3, Skipped = 4 }

		/// <summary>Tick-chunk spec §5.1: one request (day, stream, sub-window). ReqFrom/ReqTo are the window in the request (bar) zone,
		/// set by the caller on the calc thread. Payload is the caller's tick buffer until the chunk is released.</summary>
		public sealed class TickChunk
		{
			public int Id, DayIndex, Stream, Sub, Attempt;
			public DateTime Day, FromEt, ToEt, ReqFrom, ReqTo;
			public ChunkState State;
			/// <summary>Subscribe2RT: the last sub-window of the last day; it also covers every bar after ToEt.</summary>
			public bool Rt;
			/// <summary>Failed and replaced by its sub-windows.</summary>
			public bool Replaced;
			/// <summary>Its ticks were handed to the engine (or there were none to hand).</summary>
			public bool Released;
			public long Ticks, LateDropped;
			public DateTime IssuedAt;
			/// <summary>Last callback for this chunk (caller-set); the timeout counts from max(IssuedAt, LastActivity).</summary>
			public DateTime LastActivity;
			public object Payload;
			public string SkipReason;
			public bool Finished { get { return State == ChunkState.Done || State == ChunkState.Failed || State == ChunkState.Skipped; } }
		}

		/// <summary>Tick-chunk spec §5.1 (pure, no MultiCharts types): Pending → InFlight → Done | Failed | Skipped per (day, stream).
		/// Not thread-safe: the caller locks.</summary>
		public sealed class TickChunkScheduler
		{
			private readonly List<TickChunk>[] m_byStream;       // per stream, in time order (sub-windows in place of a replaced chunk)
			private readonly Dictionary<int, TickChunk> m_byId = new Dictionary<int, TickChunk>();
			public readonly int[] Streams;
			public readonly int Days, MaxInFlightDays, SplitOnFail;
			public readonly DateTime FirstDay, LastDay;
			/// <summary>Next id to hand out (ids are unique across schedulers when the caller seeds with the previous NextId).</summary>
			public int NextId;

			public TickChunkScheduler(List<DayChunk> days, int[] streams, int maxInFlightDays, int splitOnFail, int firstId)
			{
				Streams = streams;
				Days = days.Count;
				MaxInFlightDays = Math.Max(1, Math.Min(4, maxInFlightDays));
				SplitOnFail = Math.Max(0, splitOnFail);
				NextId = firstId;
				if (days.Count > 0) { FirstDay = days[0].GlobexDate; LastDay = days[days.Count - 1].GlobexDate; }
				m_byStream = new List<TickChunk>[streams.Length];
				for (int si = 0; si < streams.Length; si++)
				{
					m_byStream[si] = new List<TickChunk>(days.Count);
					for (int di = 0; di < days.Count; di++)
						m_byStream[si].Add(add(new TickChunk { DayIndex = di, Day = days[di].GlobexDate, Stream = streams[si], FromEt = days[di].FromEt, ToEt = days[di].ToEt, Rt = di == days.Count - 1 }));
				}
			}

			private TickChunk add(TickChunk c) { c.Id = NextId++; m_byId[c.Id] = c; return c; }

			public TickChunk Find(int id) { TickChunk c; return m_byId.TryGetValue(id, out c) ? c : null; }

			/// <summary>All chunks (replaced ones included), stream by stream in time order.</summary>
			public IEnumerable<TickChunk> All() { foreach (var l in m_byStream) foreach (var c in l) yield return c; }

			private IEnumerable<TickChunk> live() { foreach (var l in m_byStream) foreach (var c in l) if (!c.Replaced) yield return c; }

			/// <summary>Chunks to request now (marked InFlight, Attempt counted): the earliest days with pending chunks, up to
			/// MaxInFlightDays days in flight. With skipLate, a pending chunk of a day other than the last whose window ends at or before
			/// calcPositionEt is marked Skipped(late) and returned in skipped.</summary>
			public List<TickChunk> Next(DateTime calcPositionEt, bool skipLate, List<TickChunk> skipped)
			{
				var issue = new List<TickChunk>();
				List<TickChunk>[] pendingByDay = null;   // lazy: nothing pending → no allocation
				var inFlight = new HashSet<int>();
				foreach (var c in live())
				{
					if (c.State == ChunkState.InFlight) inFlight.Add(c.DayIndex);
					else if (c.State == ChunkState.Pending)
					{
						if (pendingByDay == null) pendingByDay = new List<TickChunk>[Days];
						if (pendingByDay[c.DayIndex] == null) pendingByDay[c.DayIndex] = new List<TickChunk>();
						pendingByDay[c.DayIndex].Add(c);
					}
				}
				if (pendingByDay == null) return issue;
				for (int di = 0; di < Days; di++)
				{
					if (pendingByDay[di] == null) continue;
					bool any = false;
					foreach (var c in pendingByDay[di])
					{
						if (skipLate && !c.Rt && c.DayIndex != Days - 1 && c.ToEt <= calcPositionEt)
						{
							c.State = ChunkState.Skipped; c.SkipReason = "late"; c.Released = true;
							if (skipped != null) skipped.Add(c);
							continue;
						}
						if (!inFlight.Contains(di) && inFlight.Count >= MaxInFlightDays) break;   // a day already in flight may still get its (sub-)chunks
						c.State = ChunkState.InFlight; c.Attempt++;
						issue.Add(c); any = true;
					}
					if (any) inFlight.Add(di);
				}
				return issue;
			}

			/// <summary>Record the outcome of an in-flight chunk. A first failure with SplitOnFail > 0 replaces it with SplitOnFail equal
			/// sub-windows (Pending; returns "split" + N); otherwise it stays Failed ("giveUp"). ok returns "done".</summary>
			public string Complete(TickChunk c, bool ok, long ticks, object payload)
			{
				if (c == null || c.State != ChunkState.InFlight) return null;
				if (ok) { c.State = ChunkState.Done; c.Ticks = ticks; c.Payload = payload; return "done"; }
				c.State = ChunkState.Failed; c.Payload = null; c.Released = true;
				if (SplitOnFail <= 0 || c.Attempt > 1 || c.Sub != 0) return "giveUp";
				c.Replaced = true;
				var list = m_byStream[Array.IndexOf(Streams, c.Stream)];
				int at = list.IndexOf(c);
				long span = (c.ToEt - c.FromEt).Ticks / SplitOnFail;
				for (int k = 0; k < SplitOnFail; k++)
				{
					DateTime f = c.FromEt.AddTicks(span * k), t = k == SplitOnFail - 1 ? c.ToEt : c.FromEt.AddTicks(span * (k + 1));
					list.Insert(at + 1 + k, add(new TickChunk { DayIndex = c.DayIndex, Day = c.Day, Stream = c.Stream, Sub = k + 1, FromEt = f, ToEt = t,
						Attempt = 1, Rt = c.Rt && k == SplitOnFail - 1 }));
				}
				return "split" + SplitOnFail;
			}

			/// <summary>Review S3: in-flight chunks with no activity (IssuedAt, LastActivity) for timeout or longer. The caller fails them
			/// with Complete(c, false, ...) (a first failure splits). timeout &lt;= 0: none.</summary>
			public List<TickChunk> TimedOut(DateTime now, TimeSpan timeout)
			{
				var r = new List<TickChunk>();
				if (timeout <= TimeSpan.Zero) return r;
				foreach (var c in live())
				{
					if (c.State != ChunkState.InFlight) continue;
					DateTime seen = c.LastActivity > c.IssuedAt ? c.LastActivity : c.IssuedAt;
					if (now - seen >= timeout) r.Add(c);
				}
				return r;
			}

			/// <summary>Done, unreleased chunks whose earlier chunks (same stream) are all finished, in time order; marked Released.</summary>
			public List<TickChunk> ReleasableInOrder()
			{
				var r = new List<TickChunk>();
				foreach (var l in m_byStream)
					foreach (var c in l)
					{
						if (c.Replaced) continue;
						if (!c.Finished) break;
						if (c.State == ChunkState.Done && !c.Released) { c.Released = true; r.Add(c); }
					}
				return r;
			}

			/// <summary>The bar closing at closeEt (US Eastern) can use the tick tier: for every stream, the chunk covering it
			/// (FromEt &lt; closeEt &lt;= ToEt; the RT chunk also covers everything after) is Done and released.</summary>
			public bool ReadyAt(DateTime closeEt)
			{
				foreach (var l in m_byStream)
				{
					TickChunk cover = null;
					foreach (var c in l)
					{
						if (c.Replaced) continue;
						if (c.FromEt >= closeEt) break;
						if (closeEt <= c.ToEt || c.Rt) { cover = c; break; }
					}
					if (cover == null || cover.State != ChunkState.Done || !cover.Released) return false;
				}
				return m_byStream.Length > 0;
			}

			/// <summary>Chunks not finished, or finished Done but not yet released.</summary>
			public int Pending()
			{
				int n = 0;
				foreach (var c in live()) if (!c.Finished || (c.State == ChunkState.Done && !c.Released)) n++;
				return n;
			}

			public void Summary(out int done, out int failed, out int skipped, out long ticks, out long lateDropped)
			{
				done = failed = skipped = 0; ticks = lateDropped = 0;
				foreach (var c in live())
				{
					if (c.State == ChunkState.Done) done++; else if (c.State == ChunkState.Failed) failed++; else if (c.State == ChunkState.Skipped) skipped++;
					ticks += c.Ticks; lateDropped += c.LateDropped;
				}
			}
		}
		// </order-flow>
	}
}
