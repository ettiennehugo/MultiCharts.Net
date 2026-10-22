using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 VP-2 — weekly profile stream ("VPF-3 extension"): completed and developing weekly profiles plus N-week
	// composites. The host (_VolumeProfileBase) feeds each completed session histogram once at the session change; nothing
	// happens per bar. Week key = Monday-start week of the trading day (same key as _AvwapManager's Week reset).
	// Roll policy Reset (A2-39a): the week containing a roll is split; the pre-roll part is crossesRoll (not usable), the post-
	// roll part starts a new partial week; a composite never spans a roll. CashTerms / BackAdjusted are reserved, not built (Q3).
	// Review M2: each session record carries isRollWindow (roll session +-1). Pure C# (tests/VpHelperTests).
	// ============================================================================================

	// <weekly-profiles>
	public enum EWeeklySource { RthSessions = 0, GlobexSessions = 1 }
	public enum EWeeklyRollPolicy { Reset = 0, CashTermsReserved = 1, BackAdjustedReserved = 2 }

	public sealed class WeeklyProfileConfig
	{
		public bool enabled = false;
		public int dataStream = 1;
		public EWeeklySource source = EWeeklySource.RthSessions;
		public int keepWeeks = 8;
		public int minSessions = 3;
		public bool includeDeveloping = false;
		public EWeeklyRollPolicy rollPolicy = EWeeklyRollPolicy.Reset;
		public int distribution = 2;   // session histogram from closed bars: ProfileMath.Accumulator mode (2 = uniform, A2 convention)

		public string Validate()
		{
			var e = new List<string>();
			if (dataStream < 1) e.Add("dataStream=" + dataStream);
			if (keepWeeks < 1) e.Add("keepWeeks=" + keepWeeks);
			if (minSessions < 1) e.Add("minSessions=" + minSessions);
			if (rollPolicy != EWeeklyRollPolicy.Reset) e.Add("rollPolicy=" + rollPolicy + " (only Reset is built)");
			if (distribution < 1 || distribution > 3) e.Add("distribution=" + distribution);
			return e.Count == 0 ? null : "WeeklyProfileConfig invalid: " + string.Join(", ", e);
		}
		public string Canon() { return "wk:" + dataStream + ":" + (int)source + ":" + keepWeeks + ":" + minSessions + ":" + (includeDeveloping ? 1 : 0) + ":" + (int)rollPolicy + ":" + distribution; }
	}

	/// <summary>Published per-session record (review M2: isRollWindow).</summary>
	public sealed class WeekSession
	{
		public DateTime day; public bool roll, partial, isRollWindow; public double volume; public int bars;
	}

	public sealed class WeekProfile
	{
		public DateTime key;                       // Monday of the week
		public double tick;
		public readonly SortedDictionary<long, double[]> rows = new SortedDictionary<long, double[]>();   // row -> {total, ask, bid}
		public readonly List<WeekSession> sessions = new List<WeekSession>();
		public bool isPartial, crossesRoll, complete, bidAskValid = true;
		public double high = double.MinValue, low = double.MaxValue, open = double.NaN, close = double.NaN;
		public int Sessions { get { return sessions.Count; } }
		public bool IsRollWindow { get { foreach (WeekSession s in sessions) if (s.isRollWindow) return true; return false; } }

		/// <summary>Dense ascending 1-tick grid (zero rows included). ask / bid are null when any session lacked them.</summary>
		public bool Dense(out double[] prices, out double[] total, out double[] ask, out double[] bid)
		{
			prices = total = ask = bid = null;
			if (rows.Count == 0) return false;
			long lo = long.MaxValue, hi = long.MinValue;
			foreach (long k in rows.Keys) { if (k < lo) lo = k; if (k > hi) hi = k; }
			int n = (int)Math.Min(200000, hi - lo + 1);
			prices = new double[n]; total = new double[n];
			if (bidAskValid) { ask = new double[n]; bid = new double[n]; }
			for (int i = 0; i < n; i++) prices[i] = (lo + i) * tick;
			foreach (var kv in rows)
			{
				long i = kv.Key - lo; if (i < 0 || i >= n) continue;
				total[i] += kv.Value[0];
				if (bidAskValid) { ask[i] += kv.Value[1]; bid[i] += kv.Value[2]; }
			}
			return true;
		}

		internal void merge(WeekProfile o)
		{
			foreach (var kv in o.rows) addRow(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2]);
			if (!o.bidAskValid) bidAskValid = false;
			if (o.high > high) high = o.high;
			if (o.low < low) low = o.low;
			if (double.IsNaN(open)) open = o.open;
			close = o.close;
		}

		internal void addRow(long k, double total, double ask, double bid)
		{
			double[] v;
			if (!rows.TryGetValue(k, out v)) { v = new double[3]; rows[k] = v; }
			v[0] += total; v[1] += ask; v[2] += bid;
		}

		public string Tag { get { return "WK:" + key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); } }
	}

	public sealed class _WeeklyProfiles
	{
		public readonly WeeklyProfileConfig config;
		public readonly double tick;
		public Action<string> journal;
		private readonly List<WeekProfile> m_done = new List<WeekProfile>();   // oldest first, at most keepWeeks
		private WeekProfile m_cur;
		private DateTime m_lastDay = DateTime.MinValue;
		private readonly Dictionary<long, WeekProfile> m_compCache = new Dictionary<long, WeekProfile>();

		public _WeeklyProfiles(WeeklyProfileConfig cfg, double tick) { config = cfg ?? new WeeklyProfileConfig(); this.tick = tick > 0 ? tick : 0.01; }

		/// <summary>Monday-start week of the trading day (not the ISO week number, so 2026-12-28..2027-01-01 is one week).</summary>
		public static DateTime WeekKeyOf(DateTime tradingDay) { DateTime d = tradingDay.Date; return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

		public int CompletedWeeks { get { return m_done.Count; } }

		/// <summary>
		/// Fold one completed session. rows: row -> {total, ask, bid} (ask / bid NaN when unknown). roll: the session is the first
		/// session of a new contract. partial: the session itself is partial. Sessions must arrive in trading-day order.
		/// </summary>
		public void AddSession(DateTime day, IDictionary<long, double[]> rows, bool roll, bool partial, double open, double high, double low, double close, int bars, bool isRollWindow = false)
		{
			day = day.Date;
			if (day <= m_lastDay) return;
			m_lastDay = day;
			DateTime key = WeekKeyOf(day);
			if (m_cur != null && m_cur.key != key) completeCurrent();
			if (roll && m_cur != null && m_cur.sessions.Count > 0)
			{
				m_cur.crossesRoll = true;   // pre-roll part: not usable
				if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "WK roll split {0:yyyy-MM-dd} at {1:yyyy-MM-dd}", m_cur.key, day));
				completeCurrent();
				m_cur = new WeekProfile { key = key, tick = tick, isPartial = true };
			}
			if (m_cur == null) m_cur = new WeekProfile { key = key, tick = tick };
			var s = new WeekSession { day = day, roll = roll, partial = partial, isRollWindow = isRollWindow, bars = bars };
			m_cur.sessions.Add(s);
			if (partial) m_cur.isPartial = true;
			if (rows != null)
				foreach (var kv in rows)
				{
					double a = kv.Value.Length > 1 ? kv.Value[1] : double.NaN, b = kv.Value.Length > 2 ? kv.Value[2] : double.NaN;
					if (double.IsNaN(a) || double.IsNaN(b)) { m_cur.bidAskValid = false; a = b = 0; }
					m_cur.addRow(kv.Key, kv.Value[0], a, b);
					s.volume += kv.Value[0];
				}
			if (high > m_cur.high) m_cur.high = high;
			if (low > 0 && low < m_cur.low) m_cur.low = low;
			if (double.IsNaN(m_cur.open)) m_cur.open = open;
			m_cur.close = close;
			m_compCache.Clear();
		}

		/// <summary>Review M2: re-stamp isRollWindow on stored session records (the "day after a roll" is known only later).</summary>
		public void RefreshRollWindow(Func<DateTime, bool> isRollWindow)
		{
			if (isRollWindow == null) return;
			foreach (WeekProfile w in m_done) foreach (WeekSession s in w.sessions) s.isRollWindow = isRollWindow(s.day);
			if (m_cur != null) foreach (WeekSession s in m_cur.sessions) s.isRollWindow = isRollWindow(s.day);
		}

		/// <summary>Force the developing week to complete (e.g. at the last bar). Normally a new week key completes it.</summary>
		public void Flush() { if (m_cur != null) completeCurrent(); }

		private void completeCurrent()
		{
			WeekProfile w = m_cur; m_cur = null;
			if (w == null || w.sessions.Count == 0) return;
			w.complete = true;
			if (w.sessions.Count < config.minSessions) w.isPartial = true;
			m_done.Add(w);
			while (m_done.Count > config.keepWeeks) m_done.RemoveAt(0);
			m_compCache.Clear();
			if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "WK complete {0:yyyy-MM-dd} sessions({1}) roll({2}) partial({3})",
				w.key, w.sessions.Count, w.crossesRoll ? 1 : 0, w.isPartial ? 1 : 0));
		}

		/// <summary>0 = the developing week (completed sessions of the current week; the host merges the developing session when includeDeveloping), 1 = last completed.</summary>
		public WeekProfile Week(int weeksAgo)
		{
			if (weeksAgo < 0) return null;
			if (weeksAgo == 0) return m_cur;
			int i = m_done.Count - weeksAgo;
			return i >= 0 ? m_done[i] : null;
		}

		public DateTime WeekKey(int weeksAgo) { WeekProfile w = Week(weeksAgo); return w != null ? w.key : DateTime.MinValue; }

		/// <summary>Row-wise merge of the last n COMPLETED weeks; null when fewer than n weeks exist or any of them crosses a roll
		/// (i.e. fewer than n weeks after the last roll). Partial post-roll weeks are allowed and make the composite partial.</summary>
		public WeekProfile Composite(int n)
		{
			if (n < 1 || m_done.Count < n) return null;
			long ck = n * 100000L + m_done.Count;
			WeekProfile c;
			if (m_compCache.TryGetValue(ck, out c)) return c;
			c = new WeekProfile { key = m_done[m_done.Count - n].key, tick = tick, complete = true };
			for (int i = m_done.Count - n; i < m_done.Count; i++)
			{
				WeekProfile w = m_done[i];
				// a roll is always the first session of a week profile (Reset splits mid-week); one after the oldest week means the span crosses it
				if (w.crossesRoll || (i > m_done.Count - n && w.sessions.Count > 0 && w.sessions[0].roll)) { m_compCache[ck] = null; return null; }
				c.merge(w);
				c.sessions.AddRange(w.sessions);
				if (w.isPartial) c.isPartial = true;
			}
			m_compCache[ck] = c;
			return c;
		}
	}
	// </weekly-profiles>
}
