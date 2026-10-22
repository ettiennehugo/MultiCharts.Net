using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 §0.2 — closed-bar ring. The host (_CustomSignal) fills one ring per stream, once per closed bar, only when an
	// enabled helper asks for that stream. Helpers read past windows from the ring instead of indexing Bars.X[n] (MaxBarsBack
	// per-series behaviour is unverified, MFS §3.5 / Probe P2). Pure C# (tests/VpHelperTests).
	// ============================================================================================

	// <bar-ring>
	/// <summary>One closed bar. time = MC bar stamp (end of bar); up/down = Bars.UpTicks/DownTicks (BarUpDown tier only).</summary>
	public struct RingBar
	{
		public int bar; public DateTime time; public DateTime tradingDay;
		public double o, h, l, c, v, up, down;
		public bool rth; public bool rollBar;
	}

	public sealed class BarRingConfig
	{
		/// <summary>0 = auto: the sum of every enabled consumer's need, capped at MaxAutoBars.</summary>
		public int capacityBars = 0;
		public const int MaxAutoBars = 40000;   // ~29 Globex sessions of 1m

		public string Canon() { return "ring:" + capacityBars; }
	}

	public sealed class _BarRing
	{
		private readonly RingBar[] m_buf;
		private int m_head;    // next write slot
		private int m_count;
		private long m_added;

		public _BarRing(int capacity) { m_buf = new RingBar[Math.Max(1, capacity)]; }

		public int Capacity { get { return m_buf.Length; } }
		public int Count { get { return m_count; } }
		/// <summary>Bars ever added (Count + dropped).</summary>
		public long Added { get { return m_added; } }
		public bool Dropped { get { return m_added > m_count; } }
		public int LastBarNumber { get { return m_count > 0 ? this[0].bar : -1; } }

		/// <summary>Append a closed bar. Times must be non-decreasing; a bar number already stored as the newest is ignored (recalc idempotence).</summary>
		public bool Add(RingBar b)
		{
			if (m_count > 0)
			{
				RingBar last = this[0];
				if (b.bar == last.bar) return false;
				if (b.time < last.time) return false;
			}
			m_buf[m_head] = b;
			m_head = (m_head + 1) % m_buf.Length;
			if (m_count < m_buf.Length) m_count++;
			m_added++;
			return true;
		}

		public void Clear() { m_head = 0; m_count = 0; m_added = 0; }

		/// <summary>ago = 0 is the newest bar.</summary>
		public RingBar this[int ago]
		{
			get
			{
				if (ago < 0 || ago >= m_count) throw new ArgumentOutOfRangeException("ago");
				int i = m_head - 1 - ago; if (i < 0) i += m_buf.Length;
				return m_buf[i];
			}
		}

		/// <summary>Chronological access: idx 0 = the oldest bar still held, Count-1 = the newest.</summary>
		public RingBar At(int idx) { return this[m_count - 1 - idx]; }

		/// <summary>Chronological index of the newest bar with time &lt;= t, or -1.</summary>
		public int IndexAtOrBefore(DateTime t)
		{
			int lo = 0, hi = m_count - 1, res = -1;
			while (lo <= hi)
			{
				int mid = (lo + hi) >> 1;
				if (At(mid).time <= t) { res = mid; lo = mid + 1; } else hi = mid - 1;
			}
			return res;
		}

		/// <summary>Chronological index of the oldest bar with time &gt;= t (or &gt; t when exclusive), or Count when none.</summary>
		public int FirstIndexAtOrAfter(DateTime t, bool exclusive = false)
		{
			int lo = 0, hi = m_count - 1, res = m_count;
			while (lo <= hi)
			{
				int mid = (lo + hi) >> 1;
				DateTime x = At(mid).time;
				if (exclusive ? x > t : x >= t) { res = mid; hi = mid - 1; } else lo = mid + 1;
			}
			return res;
		}

		/// <summary>True when the ring still holds every bar from t0 on (nothing at or after t0 was dropped).</summary>
		public bool Covers(DateTime t0)
		{
			if (m_count == 0) return false;
			if (!Dropped) return true;
			return At(0).time <= t0;
		}

		/// <summary>Bars with t0 (&lt;= or &lt;) time &lt;= t1, oldest first. Allocates: for occasional (per-session) use only.</summary>
		public List<RingBar> Window(DateTime t0, DateTime t1, bool t0Exclusive = false)
		{
			var res = new List<RingBar>();
			int i1 = IndexAtOrBefore(t1), i0 = FirstIndexAtOrAfter(t0, t0Exclusive);
			for (int i = i0; i <= i1; i++) res.Add(At(i));
			return res;
		}

		/// <summary>Bars of one trading day, oldest first.</summary>
		public List<RingBar> Session(DateTime tradingDay, bool rthOnly = false)
		{
			var res = new List<RingBar>();
			for (int i = 0; i < m_count; i++) { RingBar b = At(i); if (b.tradingDay == tradingDay && (!rthOnly || b.rth)) res.Add(b); }
			return res;
		}
	}
	// </bar-ring>
}
