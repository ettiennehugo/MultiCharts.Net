using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PowerLanguage.Function;

// Off-MC tests for _RollDetector / _RollPending (Phase 3 S4).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	struct B { public int bar; public DateTime day, time; public double o, c; public string name; }

	// Reference: the pre-S4 _VolumeProfileFuturesBase.detectRoll body, verbatim but for the MC accessors.
	sealed class Ref
	{
		public bool series, table; public int gap = 8; public HashSet<DateTime> dates = new HashSet<DateTime>();
		public List<int> rolls = new List<int>(); DateTime lastDay = DateTime.MinValue; int lastBar = -1; string lastName;
		public void Step(B[] s, int i, double tick)
		{
			int bar = s[i].bar; if (bar == lastBar) return; lastBar = bar;
			string name = s[i].name; DateTime gx = s[i].day;
			bool newDay = gx != lastDay; bool roll = lastName != null && name != lastName;
			if (newDay && lastDay != DateTime.MinValue && bar > 1)
			{
				if (table && dates.Contains(gx.Date)) roll = true;
				if (series && gx.Month % 3 == 0 && gx.Day <= 21 && gap > 0 && Math.Abs(s[i].o - s[i - 1].c) >= gap * tick - tick * 1e-6) roll = true;
			}
			if (roll && (rolls.Count == 0 || rolls[rolls.Count - 1] != bar)) rolls.Add(bar);
			lastName = name; lastDay = gx;
		}
	}

	static B[] Series(int days, Func<DateTime, int, double> gapAt, Func<DateTime, string> nameOf, DateTime start)
	{
		var l = new List<B>(); double px = 5000; int bar = 1; DateTime d = start;
		for (int k = 0; k < days; k++, d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			for (int m = 0; m < 4; m++)
			{
				double o = px + (m == 0 ? gapAt(d, m) : 0);
				px = o + (bar % 3 - 1) * 0.25;
				l.Add(new B { bar = bar++, day = d, time = d.AddHours(9).AddMinutes(30 + 60 * m), o = o, c = px, name = nameOf(d) });
			}
		}
		return l.ToArray();
	}

	static List<int> Run(_RollDetector det, B[] s, double tick, bool twice = false)
	{
		var got = new List<int>();
		for (int i = 0; i < s.Length; i++)
		{
			if (det.Step(s[i].bar, s[i].day, s[i].o, i > 0 ? s[i - 1].c : double.NaN, s[i].name, tick)) got.Add(s[i].bar);
			if (twice && det.Step(s[i].bar, s[i].day, s[i].o, i > 0 ? s[i - 1].c : double.NaN, s[i].name, tick)) got.Add(-s[i].bar);
		}
		return got;
	}

	static void Main()
	{
		var start = new DateTime(2025, 2, 24);
		// gaps: 3 ticks most days, 10 ticks on 2025-03-12 (in the window), 12 ticks on 2025-04-08 (out of the window), 9 on 2025-03-24 (day 24: out)
		Func<DateTime, int, double> gap = (d, m) => d == new DateTime(2025, 3, 12) ? 2.5 : d == new DateTime(2025, 4, 8) ? 3.0 : d == new DateTime(2025, 3, 24) ? 2.25 : 0.75;
		var s = Series(60, gap, d => "ES", start);

		// 1. ported detector == pre-S4 futures-base detector (Table only since fix-i: Series is intentionally different now)
		{
			var r = new Ref { series = false, table = true }; r.dates.Add(new DateTime(2025, 3, 17));
			for (int i = 0; i < s.Length; i++) r.Step(s, i, 0.25);
			var det = new _RollDetector { series = false, table = true, dates = r.dates };
			var got = Run(det, s, 0.25, true);
			Check(got.SequenceEqual(r.rolls) && det.RollBars.SequenceEqual(r.rolls), $"S4 port identical to VPF-4 (table) rolls [{string.Join(",", got)}]");
		}
		// fix-i: Series window is Mon 2025-03-17 .. Fri 03-21 (rule Monday 03-17, third Friday 03-21): the 03-12 gap is out now
		Check(Run(new _RollDetector { series = true }, s, 0.25).Count == 0, "fix-i series: a gap before the roll-date week (Mar 12) is not a splice");
		{
			var s2 = Series(60, (d, m) => d == new DateTime(2025, 3, 18) ? 2.5 : d == new DateTime(2025, 3, 20) ? 3.0 : 0.75, d => "ES", start);
			var sr = Run(new _RollDetector { series = true }, s2, 0.25);
			Check(sr.Count == 1 && sr[0] == s2.First(x => x.day == new DateTime(2025, 3, 18)).bar, "fix-i series: first in-window gap >= 8 ticks only (Mar 18, not the Mar 20 one: one per contract)");
			Check(Run(new _RollDetector { series = true, gapTicks = 13 }, s2, 0.25).Count == 0, "S4 series: gapTicks threshold respected");
		}
		FixI();

		// 2. table mode
		var dt = new _RollDetector { table = true, dates = new HashSet<DateTime> { new DateTime(2025, 3, 17), new DateTime(2025, 6, 1) } };
		var tr = Run(dt, s, 0.25);
		Check(tr.Count == 1 && tr[0] == s.First(x => x.day == new DateTime(2025, 3, 17)).bar, "S4 table: first bar of the listed trading day");

		// 3. name change (any mode that is not Off; mid-day too)
		var sn = Series(10, (d, m) => 0, d => d < new DateTime(2025, 3, 3) ? "ESH25" : "ESM25", start);
		var nr = Run(new _RollDetector { table = true }, sn, 0.25);
		Check(nr.Count == 1 && nr[0] == sn.First(x => x.name == "ESM25").bar, "S4 name change flags the first bar of the new contract");

		// 5. Off gives no rolls (the host never calls Step when Off; a detector with neither mode still catches only names)
		Check(Run(new _RollDetector(), s, 0.25).Count == 0, "S4 Off (no series / table): no rolls on a same-name series");

		// 4. Data2 pending flag: Data1 roll at 10:30; Data2 60m bars at 10:00 / 11:00 -> fires at 11:00, once, idempotent per bar
		var p = new _RollPending();
		Check(!p.Fire(1, new DateTime(2025, 3, 12, 10, 0, 0)), "S4 pending: nothing armed -> false");
		p.Arm(new DateTime(2025, 3, 12, 10, 30, 0));
		Check(!p.Fire(2, new DateTime(2025, 3, 12, 10, 0, 0)), "S4 pending: Data2 bar before the Data1 roll -> false");
		Check(p.Fire(3, new DateTime(2025, 3, 12, 11, 0, 0)) && p.Fire(3, new DateTime(2025, 3, 12, 11, 0, 0)), "S4 pending fires on the first Data2 close >= the roll (repeat calls same bar true)");
		Check(!p.Fire(4, new DateTime(2025, 3, 12, 12, 0, 0)) && !p.Armed, "S4 pending: cleared after firing");
		p.Arm(new DateTime(2025, 6, 13, 9, 30, 0));
		Check(p.Fire(9, new DateTime(2025, 6, 13, 9, 30, 0)), "S4 pending: same-timestamp close fires");

		// educator review R1: Series on an intraday calendar-keyed chart warns; daily bars, Globex keys and Table do not
		Check(_RollDetector.SeriesKeyWarning(true, "Minute", true) != null, "R1 warn: Series + Minute + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, "Tick", true) != null, "R1 warn: Series + Tick + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, "Second", true) != null, "R1 warn: Series + Second + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, "Day", true) == null, "R1 no warn: daily bars");
		Check(_RollDetector.SeriesKeyWarning(true, "Week", true) == null, "R1 no warn: weekly bars");
		Check(_RollDetector.SeriesKeyWarning(true, "Minute", false) == null, "R1 no warn: Globex key");
		Check(_RollDetector.SeriesKeyWarning(false, "Minute", true) == null, "R1 no warn: Table only");
		Check((_RollDetector.SeriesKeyWarning(true, "Minute", true) ?? "").Contains("Table"), "R1 warning points to Table");
		// PR #46 review (M1): fail if _Test6 stops running Table mode on Globex days (no suite compiles _Test6/_CustomSignal, so a
		// source check). Needs _Test6.Strategy.CS on disk (searched upwards from the test directory).
		{
			string dir = AppContext.BaseDirectory, path = null;
			for (var di = new DirectoryInfo(dir); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_Test6.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			var assigns = Regex.Matches(src, @"\bRollDetection\s*=(?!=)\s*([^;]+);").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
			Check(path != null && assigns.Count == 1 && assigns[0] == "ERollDetection.Table", "_Test6 sets RollDetection = ERollDetection.Table exactly once [" + string.Join(" | ", assigns) + "]");
			Check(Regex.IsMatch(src, @"override\s+DateTime\s+tradingDayKey\s*\([^)]*\)\s*\{\s*return\s+_CmeSessionClock\.GlobexTradingDayEt\("), "_Test6 tradingDayKey = _CmeSessionClock.GlobexTradingDayEt");
			Check(Regex.IsMatch(src, @"HostConfig\.rollDetection\s*=\s*RollDetection\.ToString\(\)") && Regex.IsMatch(src, @"HostConfig\.rollDatesDigest\s*=\s*_RollDetector\.DatesDigest\(RollDates\)"), "_Test6 hashes the roll mode and date digest it runs");
			Check(Regex.IsMatch(src, @"HostConfig\.atrRestartOnRoll\s*=\s*false\s*;") && Regex.IsMatch(src, @"DailyConfig\.atrRestartOnRoll\s*=\s*HostConfig\.atrRestartOnRoll\s*;"), "_Test6 carries ATR20d across rolls (atrRestartOnRoll = false, hashed) - rulings (h)");
		}
		// PR #51 review item 2: host-side Globex key in _CustomSignal (no suite compiles it, so a source check like _Test6's).
		// Kills: key forced off, Future-category gate removed, intraday gate removed, warning not suppressed, day not re-keyed.
		{
			string dir = AppContext.BaseDirectory, path = null;
			for (var di = new DirectoryInfo(dir); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			var assigns = Regex.Matches(src, @"\bm_rollGlobexKey\s*=(?!=)\s*([^;]+);").Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim()).ToList();
			Check(path != null && assigns.Count == 2 && assigns.Contains("false")
				&& assigns.Contains("m_rollDet.series && calendarKeyed && _RollDetector.IsIntraday(res) && Bars.Info.Category.ToString() == \"Future\""),
				"_CustomSignal Globex key = series && calendarKeyed && intraday && Future (reset false only) [" + string.Join(" | ", assigns) + "]");
			Check(Regex.IsMatch(src, @"DateTime\s+day\s*=\s*m_rollGlobexKey\s*\?\s*_CmeSessionClock\.GlobexTradingDayEt\("), "_CustomSignal detector day = GlobexTradingDayEt when keyed");
			Check(Regex.IsMatch(src, @"SeriesKeyWarning\(m_rollDet\.series,\s*res,\s*calendarKeyed\s*&&\s*!m_rollGlobexKey\)"), "_CustomSignal warns only when calendar-keyed and not Globex-keyed");
			// Analyst 1 N1: the splice line is journaled at ETraceLevel.Journal (kills Journal -> Info)
			Check(Regex.IsMatch(src, @"if\s*\(\s*traceOn\(ETraceLevel\.Journal\)\s*\)\s*trace\(ETraceLevel\.Journal,\s*\$""VPF-4 roll splice detected"), "_CustomSignal roll splice line at ETraceLevel.Journal");
		}
		Check(_RollDetector.IsIntraday("Minute") && _RollDetector.IsIntraday("Tick") && !_RollDetector.IsIntraday("Day") && !_RollDetector.IsIntraday("Week"), "IsIntraday: Minute/Tick yes, Day/Week no");
		// diagnostics: gap / prior close / open of the Series bar
		{
			var d = new _RollDetector { series = true };
			d.Step(1, new DateTime(2025, 3, 14), 100, 100, "@ES", 0.25); d.Step(2, new DateTime(2025, 3, 17), 112.5, 100, "@ES", 0.25);
			Check(d.LastGap == 12.5 && d.LastOpen == 112.5 && d.LastPrevClose == 100, "Series roll diagnostics: gap 12.5 pts, open, prior close");
			var t = new _RollDetector { table = true, dates = new HashSet<DateTime> { new DateTime(2025, 3, 18) } };
			t.Step(1, new DateTime(2025, 3, 17), 100, 100, "@ES", 0.25); t.Step(2, new DateTime(2025, 3, 18), 100, 100, "@ES", 0.25);
			Check(t.RollBars.Count == 1 && double.IsNaN(t.LastGap), "Table roll diagnostics: gap NaN");
			// Analyst 1 N4: a Table roll after a Series roll on the SAME detector clears the diagnostics (kills "not cleared")
			var st = new _RollDetector { series = true, table = true, dates = new HashSet<DateTime> { new DateTime(2025, 6, 2) } };
			st.Step(1, new DateTime(2025, 3, 14), 100, 100, "@ES", 0.25); st.Step(2, new DateTime(2025, 3, 17), 112.5, 100, "@ES", 0.25);
			bool seriesSet = st.LastGap == 12.5;
			st.Step(3, new DateTime(2025, 6, 2), 112.5, 112.5, "@ES", 0.25);
			Check(seriesSet && st.RollBars.Count == 2 && double.IsNaN(st.LastGap) && double.IsNaN(st.LastOpen) && double.IsNaN(st.LastPrevClose), "SeriesOrTable: Table roll after a Series roll clears diagnostics to NaN");
			// Analyst 1 N3: a DOWN gap is a splice too and LastGap is its magnitude (kills the signed-gap mutant)
			var dn = new _RollDetector { series = true };
			dn.Step(1, new DateTime(2025, 3, 14), 100, 100, "@ES", 0.25);
			Check(dn.Step(2, new DateTime(2025, 3, 17), 87.5, 100, "@ES", 0.25) && dn.LastGap == 12.5 && dn.LastOpen == 87.5, "Series: down-gap roll detected, LastGap = |gap| 12.5");
		}
		// Analyst 1 N2: the window ends on the FRIDAY of the rule week (kills the Thursday-end mutant), pre- and post-2022
		{
			DateTime ex;
			Check(_RollDetector.SeriesWindow(new DateTime(2025, 3, 21), out ex) && ex == new DateTime(2025, 3, 21) && !_RollDetector.SeriesWindow(new DateTime(2025, 3, 24), out ex), "SeriesWindow 2025-03: Friday 03-21 in, Monday after out");
			Check(_RollDetector.SeriesWindow(new DateTime(2019, 9, 13), out ex) && ex == new DateTime(2019, 9, 20) && !_RollDetector.SeriesWindow(new DateTime(2019, 9, 16), out ex), "SeriesWindow 2019-09: rule-week Friday 09-13 in, expiry week out");
			var fr = new _RollDetector { series = true };
			fr.Step(1, new DateTime(2019, 9, 12), 100, 100, "@ES", 0.25);
			Check(fr.Step(2, new DateTime(2019, 9, 13), 112.5, 100, "@ES", 0.25), "Series: a splice on the rule-week Friday (2019-09-13) is kept");
		}
		// DatesDigest: order / duplicate insensitive, sensitive to a date
		{
			var a = new[] { new DateTime(2022, 6, 13), new DateTime(2022, 3, 10) }; var b = new[] { new DateTime(2022, 3, 10), new DateTime(2022, 6, 13), new DateTime(2022, 6, 13) };
			Check(_RollDetector.DatesDigest(a) == _RollDetector.DatesDigest(b) && _RollDetector.DatesDigest(a) != _RollDetector.DatesDigest(new[] { new DateTime(2022, 6, 9), new DateTime(2022, 3, 10) }), "DatesDigest order/dup-insensitive, date-sensitive");
		}
		Console.WriteLine($"RollTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	// ---- fix-i: Series over-detection on intraday @ES (72 EVROLL lines for ~33 rolls, specs/evroll-mode2-run-2026-10-30.csv) ----

	static DateTime GlobexDay(DateTime et) { return et.Hour >= 18 ? et.Date.AddDays(1) : et.Date; }   // = _CmeSessionClock.GlobexTradingDayEt

	// Hourly ET bars of a Globex week: Sun 18:00 .. Fri 16:00, 17:00-18:00 halt Mon-Thu. Every Sunday open gaps weekendGap points
	// (an ordinary weekend gap, > the 8-tick = 2 pt threshold), every weekday reopen haltGap; the splice (spread points) is at
	// the Sunday open of the Globex day spliceDay. key: Globex day (fixed host) or calendar date (pre-fix host).
	static B[] Intraday(DateTime from, DateTime to, double weekendGap, double haltGap, Func<DateTime, double> spliceAt, bool globexKey)
	{
		var l = new List<B>(); double px = 2000; int bar = 1; double prevC = px;
		for (DateTime t = from; t < to; t = t.AddHours(1))
		{
			var dow = t.DayOfWeek;
			bool open = !(dow == DayOfWeek.Saturday || (dow == DayOfWeek.Friday && t.Hour >= 17) || (dow == DayOfWeek.Sunday && t.Hour < 18) || t.Hour == 17);
			if (!open) continue;
			double o = prevC;
			if (l.Count > 0)
			{
				DateTime pt = l[l.Count - 1].time;
				if ((t - pt).TotalHours > 24) o += (bar % 2 == 0 ? weekendGap : -weekendGap) + spliceAt(GlobexDay(t));
				else if ((t - pt).TotalHours > 1) o += haltGap;
			}
			double c = o + (bar % 3 - 1) * 0.25;
			l.Add(new B { bar = bar++, day = globexKey ? GlobexDay(t) : t.Date, time = t, o = o, c = c, name = "@ES" });
			prevC = c;
		}
		return l.ToArray();
	}

	static DateTime RuleRoll(DateTime expiry) { return expiry >= new DateTime(2022, 4, 1) ? expiry.AddDays(-4) : expiry.AddDays(-8); }
	static DateTime MondayOf(DateTime d) { return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

	static void FixI()
	{
		// (a) one quarter, Feb-Apr 2025: 3.5 pt weekend gaps every week + 1 pt weekday halt gaps; real splice (+25 pt) at the
		//     Sunday 03-16 18:00 ET open (Globex Monday 03-17 = the 2022+ rule date).
		DateTime spl = new DateTime(2025, 3, 17);
		var a = Intraday(new DateTime(2025, 2, 2, 18, 0, 0), new DateTime(2025, 4, 26), 3.5, 1.0, d => d == spl ? 25 : 0, true);
		var ra = Run(new _RollDetector { series = true }, a, 0.25);
		Check(ra.Count == 1 && a.First(x => x.bar == ra[0]).day == spl, $"fix-i (a) weekend gaps + one splice -> exactly 1 detection on {spl:yyyy-MM-dd} [{string.Join(",", ra.Select(q => a.First(x => x.bar == q).day.ToString("MM-dd")))}]");
		var ac = Intraday(new DateTime(2025, 2, 2, 18, 0, 0), new DateTime(2025, 4, 26), 3.5, 1.0, d => d == spl ? 25 : 0, false);
		var refA = new Ref { series = true }; for (int i = 0; i < ac.Length; i++) refA.Step(ac, i, 0.25);
		Check(refA.rolls.Count == 3, $"fix-i (a) pre-fix detector on the calendar key reproduces the bug: every Sunday open of Mar 1-21 ({refA.rolls.Count})");

		// (b)+(c) 2016-11 .. 2024-12: weekend gaps every week, the vendor splice at the Sunday open of the Monday of the rule
		//     date's week (2022+: the rule Monday itself). Per contract <= 1 (= 1), on that Monday; ESM22 2022-06-13.
		var spliceDays = new HashSet<DateTime>(); var expiries = new List<DateTime>();
		for (int y = 2016; y <= 2024; y++) for (int m = 3; m <= 12; m += 3)
		{
			DateTime e = _RollDetector.ThirdFriday(y, m); if (e < new DateTime(2016, 11, 1)) continue;
			expiries.Add(e); spliceDays.Add(MondayOf(RuleRoll(e)));
		}
		var b = Intraday(new DateTime(2016, 10, 30, 18, 0, 0), new DateTime(2024, 12, 31), 4.0, 1.5, d => spliceDays.Contains(d) ? 12 : 0, true);
		var rb = Run(new _RollDetector { series = true }, b, 0.25).Select(x => b.First(y => y.bar == x).day).ToList();
		bool perQ = expiries.All(e => rb.Count(d => d <= e && d > e.AddMonths(-3)) <= 1);
		Check(perQ, "fix-i (b) per-quarter Series count never above 1");
		Check(rb.Count == expiries.Count && rb.All(spliceDays.Contains), $"fix-i (b) one detection per contract on the splice Monday ({rb.Count}/{expiries.Count})");
		Check(rb.Where(d => d >= new DateTime(2022, 4, 1)).All(d => expiries.Any(e => RuleRoll(e) == d)), "fix-i (c) 2022+ detections at offset 0 (= the rule Monday)");
		Check(rb.Contains(new DateTime(2022, 6, 13)) && !rb.Any(d => d.Year == 2022 && d.Month == 6 && d.Day != 13), "fix-i (c) ESM22 splice 2022-06-13");
		Check(_RollDetector.RollRuleMondayFrom == new DateTime(2022, 4, 1), "fix-i RollRuleMondayFrom == EventRules cut-over 2022-04-01");

		// replay of the MC evidence: every EVROLL splice day of the run fed as a qualifying gap (with a non-gap day before it)
		string dir = AppContext.BaseDirectory, path = null;
		for (var di = new DirectoryInfo(dir); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "tests", "RollTests", "evroll-mode2-run-2026-10-30.csv"); if (File.Exists(fp)) path = fp; }
		var ev = path == null ? new List<DateTime>() : File.ReadAllLines(path).Select(x => Regex.Match(x, @"EVROLL splice\((\d{4}-\d\d-\d\d)\)")).Where(m => m.Success).Select(m => DateTime.Parse(m.Groups[1].Value)).ToList();
		var det = new _RollDetector { series = true }; int n = 0, k = 1; var kept = new List<DateTime>();
		foreach (var d in ev)
		{
			det.Step(k++, d.AddDays(-1), 100, 100, "@ES", 0.25);
			if (det.Step(k++, d, 110, 100, "@ES", 0.25)) { n++; kept.Add(d); }
		}
		var byQ = kept.GroupBy(d => _RollDetector.ThirdFriday(d.Year, ((d.Month - 1) / 3 + 1) * 3)).ToList();
		Console.WriteLine($"fix-i replay: {ev.Count} EVROLL -> {n} splices; 2022+: {string.Join(" ", kept.Where(d => d.Year >= 2022).Select(d => d.ToString("yyyy-MM-dd")))}");
		Check(ev.Count == 72 && byQ.All(g => g.Count() == 1), $"fix-i replay of the MC run: 72 -> {n} (<= 1 per contract)");
		// PR #51 B2 (this fixture): the pre-PR #51 run only, which had no EVROLL line on the Thursday 18:00 halt reopen of the
		// pre-2022 rule date, so this replay of it keeps 28 splices. ESM17 (2017-06-09) and ESZ19 (2019-12-13) are missing here
		// because they are absent from this input, not because Series cannot detect them: the live run (-c fixture below) has both.
		var expect = "2016-12-05 2017-03-06 2017-09-04 2017-12-04 2018-03-05 2018-06-04 2018-12-10 2019-03-04 2019-06-10 2020-03-09 2020-06-08 2020-09-07 2020-12-07 2021-03-08 2021-06-07 2021-12-06 2022-03-07 2022-06-13 2022-09-12 2022-12-12 2023-03-13 2023-06-12 2023-09-11 2023-12-11 2024-03-11 2024-06-17 2024-09-16 2024-12-16".Split(' ').Select(DateTime.Parse).ToList();
		Check(kept.SequenceEqual(expect), $"fix-i B2 replay of the pre-PR #51 fixture keeps exactly its 28 splices (M17/Z19 not in this input) [{string.Join(" ", kept.Except(expect).Select(d => "+" + d.ToString("yyyy-MM-dd")).Concat(expect.Except(kept).Select(d => "-" + d.ToString("yyyy-MM-dd"))))}]");
		Check(kept.Where(d => d >= new DateTime(2022, 4, 1)).All(d => d == RuleRoll(_RollDetector.ThirdFriday(d.Year, ((d.Month - 1) / 3 + 1) * 3))) && kept.Contains(new DateTime(2022, 6, 13)), "fix-i replay: 2022+ kept splices all at offset 0, ESM22 2022-06-13");

		// Live Mode 2 run after PR #51 (TraceLevel Journal, 2026-10-30-c): 30 detected rolls. Every contract Dec 2016 .. Dec 2024
		// (33) is either pinned below or one of the 3 Series cannot detect in this data: U18/U19/U21 (no >= 8-tick open gap in the
		// rule week; the only candidates were in expiry week, which B1's window excludes: no roll beats a wrong one).
		// ESM17 2017-06-09 and ESZ19 2019-12-13 are Thursday 18:00 halt reopens on the pre-2022 Thursday rule date (Globex Friday).
		var live = new List<string>();
		for (var di = new DirectoryInfo(dir); di != null && live.Count == 0; di = di.Parent) { string fp = Path.Combine(di.FullName, "tests", "RollTests", "evroll-mode2-run-2026-10-30-c.csv"); if (File.Exists(fp)) live = File.ReadAllLines(fp).ToList(); }
		var rx = new Regex(@"EVROLL splice\((\d{4}-\d\d-\d\d)\).* n\((\d+)\) gapPts\(([\d.]+)\).* prevClose\(([\d.]+)\) open\(([\d.]+)\)");
		var lv = live.Select(x => rx.Match(x)).Where(m => m.Success).ToList();
		var liveExpect = "2016-12-05 2017-03-06 2017-06-09 2017-09-04 2017-12-04 2018-03-05 2018-06-04 2018-12-10 2019-03-04 2019-06-10 2019-12-13 2020-03-09 2020-06-08 2020-09-07 2020-12-07 2021-03-08 2021-06-07 2021-12-06 2022-03-07 2022-06-13 2022-09-12 2022-12-12 2023-03-13 2023-06-12 2023-09-11 2023-12-11 2024-03-11 2024-06-17 2024-09-16 2024-12-16".Split(' ').Select(DateTime.Parse).ToList();
		var noSplice = new[] { "2018-09-21", "2019-09-20", "2021-09-17" }.Select(DateTime.Parse).ToList();
		var exp2 = new List<DateTime>(); for (int y = 2016; y <= 2024; y++) for (int m = 3; m <= 12; m += 3) { var e = _RollDetector.ThirdFriday(y, m); if (e >= new DateTime(2016, 12, 1)) exp2.Add(e); }
		Check(exp2.Count == 33 && exp2.All(e => noSplice.Contains(e) ^ liveExpect.Any(d => _RollDetector.ThirdFriday(d.Year, ((d.Month - 1) / 3 + 1) * 3) == e)), "live B2: 33 contracts = 30 pinned + 3 listed undetectable (U18 U19 U21)");
		Check(live.Count(x => x.Contains("EVROLL")) == 30 && live.Count(x => x.Contains("VPF-4")) == 30, $"live fixture: 30 EVROLL / 30 VPF-4 lines ({live.Count(x => x.Contains("EVROLL"))}/{live.Count(x => x.Contains("VPF-4"))})");
		Check(!live.Any(x => x.Contains("calendar-keyed")), "live fixture: no calendar-key warning");
		var logged = lv.Select(m => DateTime.Parse(m.Groups[1].Value)).ToList();
		Check(logged.SequenceEqual(liveExpect) && lv.Select(m => int.Parse(m.Groups[2].Value)).SequenceEqual(Enumerable.Range(1, 30)), "live fixture: the 30 logged splices are exactly the pinned set, n(1..30)");
		// replay through the real detector with the logged prevClose/open (a non-gap bar on the previous day first)
		var det2 = new _RollDetector { series = true }; int k2 = 1; var kept2 = new List<DateTime>();
		foreach (var m in lv)
		{
			DateTime d = DateTime.Parse(m.Groups[1].Value); double pc = double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture), op = double.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
			det2.Step(k2++, d.AddDays(-1), pc, pc, "@ES", 0.25);
			if (det2.Step(k2++, d, op, pc, "@ES", 0.25)) kept2.Add(d);
		}
		Check(kept2.SequenceEqual(liveExpect), $"live replay keeps exactly the pinned 30 splices [{string.Join(" ", kept2.Except(liveExpect).Select(d => "+" + d.ToString("yyyy-MM-dd")).Concat(liveExpect.Except(kept2).Select(d => "-" + d.ToString("yyyy-MM-dd"))))}]");
		Check(kept2.Contains(new DateTime(2017, 6, 9)) && kept2.Contains(new DateTime(2019, 12, 13)), "live replay: ESM17 2017-06-09 and ESZ19 2019-12-13 (Thursday halt reopens) detected");
		// weak-gap visibility: 2024-12-16 wins its window on 2.25 pts (gapX 1.13) vs ~60 for the other 2024 rolls
		var gp = lv.ToDictionary(m => DateTime.Parse(m.Groups[1].Value), m => double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
		Check(gp[new DateTime(2024, 12, 16)] == 2.25 && new[] { "2024-03-11", "2024-06-17", "2024-09-16" }.All(x => gp[DateTime.Parse(x)] >= 60), "live: weak 2.25 pt gap 2024-12-16 still wins its window (other 2024 rolls >= 60 pts)");
		Check(new[] { "2017-03-06", "2018-06-04", "2021-06-07", "2021-12-06" }.All(x => gp[DateTime.Parse(x)] <= 3.5), "live: 2017-03/2018-06/2021-06/2021-12 gaps all <= 3.5 pts");
		// Analyst 2 nits (PR #55): the Thursday halt-reopen gap sizes, 2022+ offset 0 on the live fixture, and a negative replay
		Check(gp[new DateTime(2017, 6, 9)] == 6.00 && gp[new DateTime(2019, 12, 13)] == 7.75, "live: ESM17 gap 6.00 pts, ESZ19 gap 7.75 pts");
		var rxOff = new Regex(@"EVROLL splice\((\d{4}-\d\d-\d\d)\) rule\((\d{4}-\d\d-\d\d)\) offsetSessions\((-?\d+)\)");
		var offs = live.Select(x => rxOff.Match(x)).Where(m => m.Success && DateTime.Parse(m.Groups[1].Value) >= new DateTime(2022, 4, 1)).ToList();
		Check(offs.Count == 11 && offs.All(m => m.Groups[3].Value == "0" && m.Groups[1].Value == m.Groups[2].Value
			&& DateTime.Parse(m.Groups[1].Value) == RuleRoll(_RollDetector.ThirdFriday(DateTime.Parse(m.Groups[1].Value).Year, ((DateTime.Parse(m.Groups[1].Value).Month - 1) / 3 + 1) * 3))),
			$"live: all 11 2022+ splices at offsetSessions(0) on the rule Monday ({offs.Count})");
		// negative replay: the live splices plus, per contract, a wrong gap (too small: 1 tick) and an extra full gap later in the
		// same rule week; neither may add a splice
		var det3 = new _RollDetector { series = true }; int k3 = 1; var kept3 = new List<DateTime>(); int extraAccepted = 0, smallAccepted = 0;
		foreach (var m in lv)
		{
			DateTime d = DateTime.Parse(m.Groups[1].Value); double pc = double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture), op = double.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
			det3.Step(k3++, d.AddDays(-2), pc, pc, "@ES", 0.25);
			if (det3.Step(k3++, d.AddDays(-1), pc + 0.25, pc, "@ES", 0.25)) smallAccepted++;   // wrong gap: 1 tick, below the 8-tick floor
			if (det3.Step(k3++, d, op, pc, "@ES", 0.25)) kept3.Add(d);
			if (det3.Step(k3++, d.AddDays(1), op + 50, op, "@ES", 0.25)) extraAccepted++;      // extra 200-tick gap the next day: same contract
		}
		Check(smallAccepted == 0 && extraAccepted == 0 && kept3.SequenceEqual(liveExpect), $"negative replay: 1-tick and extra same-contract gaps rejected, the 30 splices kept (small {smallAccepted}, extra {extraAccepted}, kept {kept3.Count})");
	}
}
