using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPF-5, Rev 2.5 (analyst-base-class-spec.md): order-flow helpers with a per-bar ranked fallback chain and a quality
	/// tag on every value. MultiCharts .NET does not expose the trade aggressor side (confirmed by the user, also with dxFeed),
	/// so every buy/sell split below except the reserved Aggressor rank is an APPROXIMATION:
	///   1 Aggressor   (Exact)   reserved: native aggressor flag per trade. Not exposed by MC .NET today; never produced.
	///   2 Quote       (High)    Lee-Ready on 1-tick Trade + Bid + Ask streams (DataLoader): price >= ask BUY, price &lt;= bid SELL,
	///                           inside the spread -> tick rule. A trade whose bid or ask is older than MaxQuoteAgeMs (default
	///                           1000 ms), missing or crossed is classified by the tick rule and the bar drops to TickRule.
	///   3 TickRule    (Medium)  trade ticks with sizes: uptick BUY, downtick SELL, zero tick inherits the previous side. The side
	///                           carries across bars and sessions; the very first trade ever is BUY (as MC, UM p35).
	///   4 BarUpDown   (Low)     Bars.UpTicks / DownTicks only, Build Volume On = Trade Volume (set by the caller), and the
	///                           per-bar T7 check Up + Down == Volume. Bar delta only, no ladder.
	///   5 Unavailable (Unknown) Tick Count mode, failed T7, IOG history without a 1-tick Bar Magnifier, or no usable data.
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
		public enum FlowSource { Aggressor = 0, Quote = 1, TickRule = 2, BarUpDown = 3, Unavailable = 4 }
		/// <summary>Quality tag (lower = better). A combined value takes the worst (highest) tag of its parts.</summary>
		public enum FlowQuality { Exact = 0, High = 1, Medium = 2, Low = 3, Unknown = 4 }
		public enum Side { Unknown = 0, Buy = 1, Sell = -1 }
		/// <summary>Cumulative delta reset: Globex session open or the first RTH bar (caller supplies the keys).</summary>
		public enum ResetMode { Session = 0, Rth = 1 }
		/// <summary>Chart Build Volume On setting (Format Instruments). Not readable from code (unverified), so the caller states it.</summary>
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
			public static FlowValue Unavailable(bool developing = false)
			{
				return new FlowValue { Value = double.NaN, Source = FlowSource.Unavailable, Quality = FlowQuality.Unknown, Developing = developing };
			}
			public bool IsAvailable { get { return Source != FlowSource.Unavailable && !double.IsNaN(Value); } }
			public override string ToString() { return Value + " " + Source + "/" + Quality + (Developing ? " dev" : ""); }
		}

		public sealed class FlowConfig
		{
			public double MaxQuoteAgeMs = 1000;
			public ResetMode ResetMode = ResetMode.Session;
			/// <summary>false (default): a session mixing Quote and TickRule bars reports the worst of them (session to date, no lookahead).</summary>
			public bool AllowMixed = false;
			public bool QuotesExpected = true;        // Bid/Ask tick streams are being fed
			public VolumeBasis BuildVolumeOn = VolumeBasis.Unknown;   // BarUpDown needs TradeVolume
			public bool ExcludeExtremesTickRule = true;   // TickRule footprints are wrong at the bar high/low
			public int MaxBars = 5000;                     // closed bars kept
		}

		/// <summary>Data the caller has for the bar being closed (Data1 values).</summary>
		public struct BarInput
		{
			public int BarNumber;            // 1-based, increasing
			public DateTime CloseTime;       // bar time stamp (MC bars are close-stamped); events with time &lt;= CloseTime belong here
			public DateTime SessionKey;      // e.g. Globex date
			public DateTime ResetKey;        // CumDelta resets when this changes (Session or RTH key, caller's choice)
			public Regime Regime;
			public double High, Low, Close, Volume, UpTicks, DownTicks;
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
			public bool FirstAfterConnect;                      // first built bar: ticks before the feed started are missing
			public double TradeHigh = double.NaN, TradeLow = double.NaN, BarHigh, BarLow, BarClose;
			internal readonly SortedDictionary<long, double[]> Ladder = new SortedDictionary<long, double[]>();   // row -> {bid, ask}
			public double Tick;
			public bool HasLadder { get { return Ladder.Count > 0 && (Source == FlowSource.Quote || Source == FlowSource.TickRule || Source == FlowSource.Aggressor); } }
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
				while (m_trades.HasNext(b.CloseTime))
				{
					Ev t = m_trades.Pop();
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

				if (b.Regime == Regime.IogHistoryNoMagnifier || Cfg.BuildVolumeOn == VolumeBasis.TickCount)
					clearToUnavailable(fp);
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

				// AllowMixed = false: Quote/TickRule bars of one session report the worst tag seen so far in that session
				if (!Cfg.AllowMixed && (fp.Source == FlowSource.Quote || fp.Source == FlowSource.TickRule))
				{
					if (fp.SessionKey != m_mixSession) { m_mixSession = fp.SessionKey; m_mixWorst = FlowQuality.Exact; }
					if (fp.Quality > m_mixWorst) m_mixWorst = fp.Quality;
					fp.Quality = m_mixWorst;
				}
				m_bars.Add(fp);
				m_inputs[b.BarNumber] = b;
				if (m_bars.Count > Math.Max(10, Cfg.MaxBars)) { m_inputs.Remove(m_bars[0].BarNumber); m_bars.RemoveAt(0); }
				return fp;
			}

			private static void clearToUnavailable(FootprintBar fp)
			{
				fp.Ladder.Clear(); fp.Buy = fp.Sell = 0; fp.Trades = 0;
				fp.Source = FlowSource.Unavailable; fp.Quality = FlowQuality.Unknown;
			}

			/// <summary>T7 gate: Trade Volume basis and Up + Down == Volume on this bar.</summary>
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
				for (int i = m_bars.Count - 1 - ago; i >= 0 && m_bars[i].ResetKey == last.ResetKey; i--)
				{
					FootprintBar fp = m_bars[i];
					if (fp.Source == FlowSource.Unavailable) return FlowValue.Unavailable();
					sum += fp.Delta;
					if (fp.Source > src) src = fp.Source;
					if (fp.Quality > q) q = fp.Quality;
				}
				return new FlowValue { Value = sum, Source = src, Quality = q };
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
		// </order-flow>
	}
}
