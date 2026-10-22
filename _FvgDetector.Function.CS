using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 VP-5 — fair value gap (three-bar imbalance) detector with lifecycle (touched, fillPct, filled, expired, invalid).
	// Bars 1, 2, 3 consecutive closed bars, bar 3 newest. Bullish: Low3 &gt; High1, zone [High1, Low3], start = High1. Bearish:
	// High3 &lt; Low1, zone [High3, Low1], start = Low1. Size &gt;= max(minTicks x tick, minAtrFrac x ATR) (own Wilder ATR).
	// Created at bar 3's close, usable from the next bar (createdTime = time3). A roll invalidates every live gap.
	// Pure C# (tests/VpHelperTests); hosted lazily by _CustomSignal (Fvgs.enabled, default false).
	// ============================================================================================

	// <fvg>
	public enum EFvgFillOn { Close = 0, Trade = 1 }

	public sealed class FvgConfig
	{
		public bool enabled = false;
		public int dataStream = 1;
		public int minTicks = 1;
		public double minAtrFrac = 0;
		public int atrLen = 14;
		public bool crossSessions = false;   // false: all 3 bars on one trading day (an opening gap is never an FVG)
		public bool rthOnly = false;
		public int maxLive = 20;
		public int maxAgeSessions = 5;
		public EFvgFillOn fillOn = EFvgFillOn.Close;

		public string Validate()
		{
			var e = new List<string>();
			if (dataStream < 1) e.Add("dataStream=" + dataStream);
			if (minTicks < 0) e.Add("minTicks=" + minTicks);
			if (minAtrFrac < 0) e.Add("minAtrFrac=" + minAtrFrac);
			if (atrLen < 1) e.Add("atrLen=" + atrLen);
			if (maxLive < 1) e.Add("maxLive=" + maxLive);
			if (maxAgeSessions < 0) e.Add("maxAgeSessions=" + maxAgeSessions);
			return e.Count == 0 ? null : "FvgConfig invalid: " + string.Join(", ", e);
		}
		public string Canon()
		{
			return string.Format(CultureInfo.InvariantCulture, "fvg:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}", dataStream, minTicks, minAtrFrac, atrLen, crossSessions ? 1 : 0, rthOnly ? 1 : 0, maxLive, maxAgeSessions, (int)fillOn);
		}
	}

	public sealed class Fvg
	{
		public int id, dir;                  // dir +1 bullish, -1 bearish
		public double lo, hi, start;
		public DateTime createdTime, bar1Time, firstTouchTime;
		public DateTime createdDay;
		public bool touched, filled, expired, invalid;
		public double fillPct;
		public int ageSessions;
		public double Size { get { return hi - lo; } }
		public bool IsLive { get { return !filled && !expired && !invalid; } }
		public string Tag { get { return "FVG:" + (dir > 0 ? "U" : "D") + ":" + createdTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture); } }
	}

	public sealed class _FvgDetector
	{
		public readonly FvgConfig config;
		public readonly double tick;
		public readonly TimeSpan resolution;   // bar size; zero = time-gap rule off
		public Action<string> journal;
		private readonly List<Fvg> m_live = new List<Fvg>();
		private readonly RingBar[] m_win = new RingBar[3];
		private int m_n;
		private int m_lastBar = int.MinValue, m_nextId;
		private DateTime m_day = DateTime.MinValue;
		private double m_atr = double.NaN, m_atrSum, m_prevClose = double.NaN; private int m_atrN;

		public _FvgDetector(FvgConfig cfg, double tick, TimeSpan resolution)
		{
			config = cfg ?? new FvgConfig(); this.tick = tick > 0 ? tick : 0.01; this.resolution = resolution;
		}

		public IList<Fvg> Live { get { return m_live.AsReadOnly(); } }
		public Fvg NewThisBar { get; private set; }
		public double Atr { get { return m_atr; } }

		/// <summary>Feed one closed bar (idempotent per bar number). Lifecycle of existing gaps first, then detection on the new triple.</summary>
		public void Add(RingBar b)
		{
			if (b.bar == m_lastBar) return;
			m_lastBar = b.bar;
			NewThisBar = null;
			if (b.rollBar) invalidateAll(b.time);
			if (config.rthOnly && !b.rth) return;
			bool newDay = b.tradingDay != m_day;
			if (newDay) { m_day = b.tradingDay; if (m_n > 0) foreach (Fvg f in m_live) f.ageSessions++; }
			updateAtr(b);
			lifecycle(b);
			if (b.rollBar) { m_n = 0; }   // no triple spans the splice
			m_win[0] = m_win[1]; m_win[1] = m_win[2]; m_win[2] = b;
			if (m_n < 3) m_n++;
			if (m_n == 3) detect();
		}

		private void updateAtr(RingBar b)
		{
			double tr = double.IsNaN(m_prevClose) ? b.h - b.l : Math.Max(b.h, m_prevClose) - Math.Min(b.l, m_prevClose);
			m_prevClose = b.c;
			if (m_atrN < config.atrLen) { m_atrSum += tr; m_atrN++; if (m_atrN == config.atrLen) m_atr = m_atrSum / config.atrLen; }
			else m_atr = (m_atr * (config.atrLen - 1) + tr) / config.atrLen;
		}

		private void detect()
		{
			RingBar b1 = m_win[0], b3 = m_win[2];
			if (!config.crossSessions && (b1.tradingDay != b3.tradingDay || m_win[1].tradingDay != b3.tradingDay)) return;
			if (resolution > TimeSpan.Zero && (b3.time - b1.time).Ticks > 2.5 * resolution.Ticks) return;
			int dir = 0; double lo = 0, hi = 0, start = 0;
			if (b3.l > b1.h) { dir = 1; lo = b1.h; hi = b3.l; start = b1.h; }
			else if (b3.h < b1.l) { dir = -1; lo = b3.h; hi = b1.l; start = b1.l; }
			else return;
			double size = hi - lo, need = config.minTicks * tick;
			if (config.minAtrFrac > 0)
			{
				if (double.IsNaN(m_atr)) return;
				need = Math.Max(need, config.minAtrFrac * m_atr);
			}
			if (size < need - tick * 1e-9) return;
			var f = new Fvg { id = ++m_nextId, dir = dir, lo = lo, hi = hi, start = start, createdTime = b3.time, bar1Time = b1.time, createdDay = b3.tradingDay };
			m_live.Add(f);
			NewThisBar = f;
			while (m_live.Count > config.maxLive) m_live.RemoveAt(0);
			if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "FVG new {0} [{1}-{2}] size({3:0.##}t) at {4:yyyy-MM-dd HH:mm:ss}", dir > 0 ? "U" : "D", lo, hi, size / tick, b3.time));
		}

		private void lifecycle(RingBar b)
		{
			for (int i = m_live.Count - 1; i >= 0; i--)
			{
				Fvg f = m_live[i];
				if (f.ageSessions > config.maxAgeSessions) { f.expired = true; m_live.RemoveAt(i); log("expired", f, b.time); continue; }
				if (b.l <= f.hi && b.h >= f.lo)
				{
					if (!f.touched) { f.touched = true; f.firstTouchTime = b.time; }
					double pen = f.dir > 0 ? f.hi - b.l : b.h - f.lo;
					double pct = f.Size > 0 ? Math.Max(0, Math.Min(1, pen / f.Size)) : 1;
					if (pct > f.fillPct) f.fillPct = pct;
				}
				bool filled = config.fillOn == EFvgFillOn.Close ? (f.dir > 0 ? b.c < f.lo : b.c > f.hi) : (f.dir > 0 ? b.l < f.lo : b.h > f.hi);
				if (filled) { f.filled = true; f.fillPct = 1; m_live.RemoveAt(i); log("filled", f, b.time); }
			}
		}

		private void invalidateAll(DateTime t)
		{
			foreach (Fvg f in m_live) { f.invalid = true; log("invalid(roll)", f, t); }
			m_live.Clear();
			m_n = 0;
		}

		private void log(string what, Fvg f, DateTime t)
		{
			if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "FVG {0} {1} at {2:yyyy-MM-dd HH:mm:ss}", what, f.id, t));
		}

		/// <summary>Nearest live gap to price (distance 0 inside the zone); dir +1 / -1 filters bullish / bearish, 0 = any. Null when none.</summary>
		public Fvg Nearest(double price, int dir)
		{
			Fvg best = null; double bd = double.MaxValue;
			foreach (Fvg f in m_live)
			{
				if (dir != 0 && f.dir != dir) continue;
				double d = price < f.lo ? f.lo - price : price > f.hi ? price - f.hi : 0;
				if (d < bd) { bd = d; best = f; }
			}
			return best;
		}

		/// <summary>LevelRef for a gap: startEdge = the start edge (FvgLower for bullish High1, FvgUpper for bearish Low1), else the far edge.</summary>
		public LevelRef ToLevel(Fvg f, bool startEdge, int stream = 1)
		{
			if (f == null) return null;
			double px = startEdge ? f.start : (f.dir > 0 ? f.hi : f.lo);
			ELevelKind k = px == f.hi ? ELevelKind.FvgUpper : ELevelKind.FvgLower;
			return LevelRef.Zone(k, px, f.lo, f.hi, f.createdTime, f.bar1Time, f.createdTime, stream, f.Tag, f.Size / tick);
		}
	}
	// </fvg>
}
