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
		{   // Coder 2026-11-23 (Analyst MES 2024-06): a Series window with no splice is reported once, after the window
			var dm = new _RollDetector { series = true };
			Run(dm, s, 0.25);   // window Mon 2025-03-17 .. Fri 03-21; the 03-12 gap (10 ticks) is before it
			Check(dm.MissedExpiry == new DateTime(2025, 3, 21) && dm.MissedGapDay == new DateTime(2025, 3, 12) && Math.Abs(dm.MissedGap - 2.5) < 1e-9,
				$"missed Series window reported: expiry {dm.MissedExpiry:yyyy-MM-dd} largest gap {dm.MissedGap} on {dm.MissedGapDay:yyyy-MM-dd}");
			var s3 = Series(60, (d, m) => d == new DateTime(2025, 3, 18) ? 2.5 : 0.75, d => "ES", start);
			var dk = new _RollDetector { series = true };
			Run(dk, s3, 0.25);
			Check(dk.MissedExpiry == DateTime.MinValue, "a Series window with its splice is not reported as missed");
		}
		FixI();
		CalibFit();
		TableJournal();
		Evidence();
		SpliceLocator();
		SpliceBody();
		ResetAtSplice();

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
		// typed-enums follow-up: the caller decides intraday from EResolution (_CustomSignal.isIntradayResolution); the label is display only
		Check(_RollDetector.SeriesKeyWarning(true, true, "Minute", true) != null, "R1 warn: Series + intraday (Minute) + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, true, "Tick", true) != null, "R1 warn: Series + intraday (Tick) + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, true, "Second", true) != null, "R1 warn: Series + intraday (Second) + calendar key");
		Check(_RollDetector.SeriesKeyWarning(true, false, "Day", true) == null, "R1 no warn: daily bars (not intraday)");
		Check(_RollDetector.SeriesKeyWarning(true, false, "Minute", true) == null, "R1 no warn: the intraday flag decides, not the label");
		Check(_RollDetector.SeriesKeyWarning(true, true, "Minute", false) == null, "R1 no warn: Globex key");
		Check(_RollDetector.SeriesKeyWarning(false, true, "Minute", true) == null, "R1 no warn: Table only");
		Check((_RollDetector.SeriesKeyWarning(true, true, "Minute", true) ?? "").Contains("Table") && (_RollDetector.SeriesKeyWarning(true, true, "Tick", true) ?? "").Contains("(Tick)"), "R1 warning points to Table and names the resolution label");
		// PR #46 review (M1): fail if _A2_63_RegimeGate stops running Table mode on Globex days (no suite compiles _A2_63_RegimeGate/_CustomSignal, so a
		// source check). Needs _A2_63_RegimeGate.Strategy.CS on disk (searched upwards from the test directory).
		{
			string dir = AppContext.BaseDirectory, path = null;
			for (var di = new DirectoryInfo(dir); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_A2_63_RegimeGate.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			var assigns = Regex.Matches(src, @"\bRollDetection\s*=(?!=)\s*([^;]+);").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
			Check(path != null && assigns.Count == 1 && assigns[0] == "ERollDetection.Table", "_A2_63_RegimeGate sets RollDetection = ERollDetection.Table exactly once [" + string.Join(" | ", assigns) + "]");
			Check(Regex.IsMatch(src, @"override\s+DateTime\s+tradingDayKey\s*\([^)]*\)\s*\{\s*return\s+_CmeSessionClock\.GlobexTradingDayEt\("), "_A2_63_RegimeGate tradingDayKey = _CmeSessionClock.GlobexTradingDayEt");
			Check(Regex.IsMatch(src, @"HostConfig\.rollDetection\s*=\s*rollText") && Regex.IsMatch(src, @"RollDetectionText\(RollDetection\)") && !src.Contains("RollDetection.ToString()")
				&& Regex.IsMatch(src, @"HostConfig\.rollDatesDigest\s*=\s*_RollDetector\.DatesDigest\(RollDates\)")
				&& Regex.IsMatch(src, @"TradingDayText\(ETradingDayKey\.Globex18ET\)")
				&& src.Contains("ES only (N2)") && src.Contains("RequireGate0Rth"),
				"_A2_63_RegimeGate hashes explicit roll and day-key spellings, accepts ES only, and checks the frozen window");
			Check(Regex.IsMatch(src, @"HostConfig\.atrRestartOnRoll\s*=\s*false\s*;") && Regex.IsMatch(src, @"DailyConfig\.atrRestartOnRoll\s*=\s*HostConfig\.atrRestartOnRoll\s*;"), "_A2_63_RegimeGate carries ATR20d across rolls (atrRestartOnRoll = false, hashed) - rulings (h)");
		}
		// PR #51 review item 2: host-side Globex key in _CustomSignal (no suite compiles it, so a source check like _A2_63_RegimeGate's).
		// Kills: key forced off, Future-category gate removed, intraday gate removed, warning not suppressed, day not re-keyed.
		{
			string dir = AppContext.BaseDirectory, path = null;
			for (var di = new DirectoryInfo(dir); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			var assigns = Regex.Matches(src, @"\bm_rollGlobexKey\s*=(?!=)\s*([^;]+);").Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim()).ToList();
			Check(path != null && assigns.Count == 2 && assigns.Contains("false")
				&& assigns.Contains("(m_rollDet.series || m_rollDet.table) && calendarKeyed && intraday && isFuturesCategory(Bars.Info.Category)"),
				"_CustomSignal Globex key = series or table && calendarKeyed && intraday && futures (Future or FutureRolover; reset false only) [" + string.Join(" | ", assigns) + "]");
			Check(Regex.IsMatch(src, @"DateTime\s+day\s*=\s*m_rollGlobexKey\s*\?\s*_CmeSessionClock\.GlobexTradingDayEt\("), "_CustomSignal detector day = GlobexTradingDayEt when keyed");
			Check(Regex.IsMatch(src, @"SeriesKeyWarning\(m_rollDet\.series,\s*intraday,\s*res\.ToString\(\),\s*calendarKeyed\s*&&\s*!m_rollGlobexKey\)")
				&& Regex.IsMatch(src, @"EResolution\s+res\s*=\s*Bars\.Info\.Resolution\.Type;\s*bool\s+intraday\s*=\s*isIntradayResolution\(res\);"), "_CustomSignal warns only when calendar-keyed and not Globex-keyed (typed intraday)");
			// typed-enums follow-up: intraday = every EResolution member except Day/Week/Month/Quarter/Year (typed switch, no names)
			int ii = src.IndexOf("protected static bool isIntradayResolution(EResolution t)");
			string ib = ii < 0 ? "" : src.Substring(ii, Math.Max(0, src.IndexOf("default: return true;", ii) - ii) + "default: return true;".Length);
			Check(Regex.IsMatch(ib, @"switch\s*\(t\)\s*\{\s*case EResolution\.Day:\s*case EResolution\.Week:\s*case EResolution\.Month:\s*case EResolution\.Quarter:\s*case EResolution\.Year:\s*return false;\s*default: return true;$"),
				"_CustomSignal.isIntradayResolution: typed switch, Day/Week/Month/Quarter/Year -> false, everything else intraday");
			// Analyst 1 N1: the splice line is journaled at ETraceLevel.Journal (kills Journal -> Info)
			Check(Regex.IsMatch(src, @"if\s*\(\s*traceOn\(ETraceLevel\.Journal\)\s*\)\s*trace\(ETraceLevel\.Journal,\s*\$""VPF-4 roll splice detected"), "_CustomSignal roll splice line at ETraceLevel.Journal");
			Check(src.Contains("m_rollDet.gapTicks = RollGapTicks") && src.Contains("applyRollGapCalibration = false")
				&& src.Contains("CFG ROLL gap calibrated("), "M9 host keeps the literal gap assignment, the apply switch off, and the one Info line");
		}
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
		// Analyst 2 nits (PR #55, RollTests nits; PR #57): the Thursday halt-reopen gap sizes, 2022+ offset 0 on the live fixture, and a negative replay
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
		// the gap floor itself (Analyst 2, PR #57): on every live splice day, which is inside the roll window, a 7-tick gap
		// (gapTicks - 1) is rejected and an 8-tick gap (gapTicks) is accepted, each on a fresh detector
		int inWin = 0, below = 0, atFloor = 0; DateTime exw;
		foreach (var m in lv)
		{
			DateTime d = DateTime.Parse(m.Groups[1].Value); double pc = double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
			if (_RollDetector.SeriesWindow(d, out exw)) inWin++;
			var d7 = new _RollDetector { series = true }; d7.Step(1, d.AddDays(-3), pc, pc, "@ES", 0.25);
			if (d7.Step(2, d, pc + 7 * 0.25, pc, "@ES", 0.25)) below++;
			var d8 = new _RollDetector { series = true }; d8.Step(1, d.AddDays(-3), pc, pc, "@ES", 0.25);
			if (d8.Step(2, d, pc - 8 * 0.25, pc, "@ES", 0.25)) atFloor++;
		}
		Check(new _RollDetector().gapTicks == 8 && inWin == lv.Count && below == 0 && atFloor == lv.Count,
			$"gap floor: every splice day in the window ({inWin}/{lv.Count}); 7 ticks rejected ({below} accepted), 8 ticks accepted ({atFloor})");
		Check(smallAccepted == 0 && extraAccepted == 0 && kept3.SequenceEqual(liveExpect), $"negative replay: 1-tick and extra same-contract gaps rejected, the 30 splices kept (small {smallAccepted}, extra {extraAccepted}, kept {kept3.Count})");
	}

	// M9: c from the median Wilder ATR(20) on series b (the same Intraday path as fix-i (b)). Not applied to the detector.
	static void CalibFit()
	{
		Check(!new RollGapCalibrator().Apply && RollGapCalibrator.InUseTicks == 8 && RollGapCalibrator.AtrLength == 20, "M9 calibrator defaults: apply off, in-use 8, ATR length 20");
		Check(RollGapCalibrator.Ticks(2.0, 0.25) == (int)Math.Round(RollGapCalibrator.C * 2.0 / 0.25, MidpointRounding.AwayFromZero), "M9 Ticks is round-half-away-from-zero(c × atr / tick)");
		Check(RollGapCalibrator.Ticks(double.NaN, 0.25) < 0 && RollGapCalibrator.Ticks(1, 0) < 0, "M9 Ticks refuses a missing ATR or a non-positive tick");
		Check((int)Math.Round(1.5, MidpointRounding.AwayFromZero) == 2 && (int)Math.Round(2.5, MidpointRounding.AwayFromZero) == 3, "M9 rounding half away from zero (1.5 → 2, 2.5 → 3)");

		var spliceDays = new HashSet<DateTime>();
		for (int y = 2016; y <= 2024; y++) for (int m = 3; m <= 12; m += 3)
		{
			DateTime e = _RollDetector.ThirdFriday(y, m); if (e < new DateTime(2016, 11, 1)) continue;
			spliceDays.Add(MondayOf(RuleRoll(e)));
		}
		var b = Intraday(new DateTime(2016, 10, 30, 18, 0, 0), new DateTime(2024, 12, 31), 4.0, 1.5, d => spliceDays.Contains(d) ? 12 : 0, true);
		var cal = new RollGapCalibrator();
		var atrs = new List<double>(); int seen = 0;
		foreach (var bar in b)
		{
			cal.AddBar(bar.day, Math.Max(bar.o, bar.c), Math.Min(bar.o, bar.c), bar.c);
			if (cal.TrCount >= RollGapCalibrator.AtrLength && cal.TrCount != seen) { atrs.Add(cal.Atr); seen = cal.TrCount; }
		}
		cal.Flush();
		if (cal.TrCount >= RollGapCalibrator.AtrLength && cal.TrCount != seen) atrs.Add(cal.Atr);
		atrs.Sort();
		int n = atrs.Count;
		double median = n == 0 ? double.NaN : atrs[n / 2];
		double fitted = 8 * 0.25 / median;
		int n8 = 0, n7 = 0, n9 = 0, n10 = 0, nOther = 0;
		foreach (double a in atrs)
		{
			int t = (int)Math.Round(fitted * a / 0.25, MidpointRounding.AwayFromZero);
			if (t == 8) n8++; else if (t == 7) n7++; else if (t == 9) n9++; else if (t == 10) n10++; else nOther++;
		}
		int medTicks = RollGapCalibrator.Ticks(median, 0.25);
		Check(n == 2112 && n8 == 1737 && n7 == 2 && n9 == 305 && n10 == 68 && nOther == 0, "M9 series b: 2112 ATR days, 1737 round to 8 (7/9/10 are 2/305/68)");
		Check(fitted == RollGapCalibrator.C && medTicks == 8, "M9 C is 8 × 0.25 / the upper central ATR, and that day rounds to 8");
		var det = new _RollDetector { series = true };
		Check(det.gapTicks == 8 && !cal.Apply, "M9 series b does not change the detector gap (still 8, apply off)");
	}

	// ---- T-R1 / T-R1b (m-instr §7.1 items 2-5, Confirmed by Ettienne 2026-11-22): Table roll journal ----
	struct DB { public int bar; public DateTime day; public double o, h, l, c; }

	/// <summary>One bar per Globex trading day (weekdays from..to): open = prior close + gap(day), high/low = open ± 30, close = open.</summary>
	static List<DB> Daily(DateTime from, DateTime to, Func<DateTime, double> gap)
	{
		var l = new List<DB>(); double px = 5200; int bar = 1;
		for (DateTime d = from; d <= to; d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			double o = px + (l.Count > 0 ? gap(d) : 0);
			l.Add(new DB { bar = bar++, day = d, o = o, h = o + 30, l = o - 30, c = o });
			px = o;
		}
		return l;
	}

	/// <summary>The _CustomSignal.stepRollDetector order: calibrator first (commits the prior day), then Step, then the Table
	/// journal (TableSpliceLine on a Table roll bar, OffTableWarning on any other day start). Each bar is stepped twice, as the host can.</summary>
	static List<int> HostTable(List<DB> bars, HashSet<DateTime> dates, double k, List<string> journal, List<string> warnings, List<string> splices = null, double minAtr = RollSpliceLocator.DefaultMinAtr)
	{
		var det = new _RollDetector { table = true, dates = dates };
		var cal = new RollGapCalibrator();
		var loc = new RollSpliceLocator { minAtr = minAtr };
		var rolls = new List<int>(); int lastJournal = -1, lastSplice = -1, spliceBar = -1;
		for (int i = 0; i < bars.Count; i++)
			for (int rep = 0; rep < 2; rep++)
			{
				DB b = bars[i];
				cal.AddBar(b.day, b.h, b.l, b.c);
				bool stepped = det.Step(b.bar, b.day, b.o, i > 0 ? bars[i - 1].c : double.NaN, "@MES", 0.25);
				if (stepped) rolls.Add(b.bar);
				if (b.bar != lastSplice)
				{
					lastSplice = b.bar;
					if (stepped && det.DayInTable) loc.Arm(det.DayStartDay);
					if (loc.Step(b.bar, b.day, b.day.AddDays(-1).AddHours(17), b.o, i > 0 ? bars[i - 1].c : double.NaN, cal.Atr, det.DayStart)) { spliceBar = b.bar; if (splices != null) splices.Add(_RollDetector.SpliceLine(loc, 0.25)); }
				}
				if (det.DayStart && b.bar != lastJournal)
				{
					lastJournal = b.bar;
					if (det.DayInTable) journal.Add(_RollDetector.TableRollLine(det.DayStartDay, det.DayGap, cal.Atr, minAtr));
					else if (spliceBar != b.bar) { string w = _RollDetector.OffTableWarning(det.DayStartDay, det.DayGap, cal.Atr, k); if (w != null) warnings.Add(w); }
				}
			}
		return rolls;
	}

	static int BarOf(List<DB> bars, DateTime d) { foreach (var b in bars) if (b.day == d) return b.bar; return -1; }

	static void TableJournal()
	{
		var d0613 = new DateTime(2024, 6, 13); var d0617 = new DateTime(2024, 6, 17);
		// the 2024 June table date is the Monday before the 06-21 expiry (EventRules.RollDate: expiry - 4 from 2022-04)
		Check(_RollDetector.ThirdFriday(2024, 6) == new DateTime(2024, 6, 21) && _RollDetector.ThirdFriday(2024, 6).AddDays(-4) == d0617, "T-R1 ESM24 / MESM24 table date 2024-06-17");
		var table = new HashSet<DateTime> { new DateTime(2024, 3, 11), d0617, new DateTime(2024, 9, 16) };

		// 1. MES June 2024 (Analyst MES run): the data splices on trading day 2024-06-13 (Thursday, the 06-12 18:00 ET open; +70 pts,
		// about 1.2 x ATR(20)). The roll resolves to the table date 06-17: one roll there (confirmed(0): an ordinary 0.5 pt gap),
		// none on 06-13, which logs the off-table Warning instead.
		{
			var bars = Daily(new DateTime(2024, 3, 18), new DateTime(2024, 6, 28), d => d == d0613 ? 70 : 0.5);
			var j = new List<string>(); var w = new List<string>(); var sp = new List<string>();
			var rolls = HostTable(bars, table, 1.0, j, w, sp);
			Check(rolls.Count == 1 && rolls[0] == BarOf(bars, d0617), "T-R1 MES June 2024: the 06-13 data splice resolves to the table date 06-17 (one roll, on 06-17) [" + string.Join(",", rolls) + "]");
			Check(j.Count == 1 && j[0].StartsWith("ROLL table roll(2024-06-17) reopenGapPts(0.50) atr20(") && j[0].Contains(") minAtr(0.4) offset(search"), "T-R1 MES 06-17 table roll line (" + string.Join(" | ", j) + ")");
			Check(sp.Count == 1 && sp[0].StartsWith("ROLL table splice(2024-06-17) at(2024-06-18 17:00) via(fallback) session(+2) gapBar(2024-06-16 17:00) gapPts(+0.50)") && sp[0].EndsWith("found(0)"),
				"ruling 11:30: a splice BEFORE the table date is outside the search; no bar qualifies, so the fallback applies at the end of the next session (" + string.Join(" | ", sp) + ")");
			Check(w.Count == 1 && w[0].StartsWith("ROLL off-table gap(2024-06-13) gapPts(70.00) atr20(") && w[0].EndsWith(" k(1.0)"), "T-R1 MES 06-13 off-table Warning (" + string.Join(" | ", w) + ")");
			var m = Regex.Match(j.Count > 0 ? j[0] : "", @"atr20\(([0-9.]+)\) gapAtr\(([0-9.]+)\)");
			Check(m.Success && double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) is double a20 && a20 > 60 && a20 < 65 && m.Groups[2].Value == "0.008", "T-R1 table line prints A (ATR(20) 60-65: the 06-13 splice's range is in it) and g / A (" + (j.Count > 0 ? j[0] : "") + ")");
			// the Table splice set is unchanged by the journal: the same detector with no host journal gives the same rolls
			var plain = new _RollDetector { table = true, dates = table }; var pr = new List<int>();
			for (int i = 0; i < bars.Count; i++) if (plain.Step(bars[i].bar, bars[i].day, bars[i].o, i > 0 ? bars[i - 1].c : double.NaN, "@MES", 0.25)) pr.Add(bars[i].bar);
			Check(pr.SequenceEqual(rolls), "T-R1 Table splice set unchanged by the confirmation journal");
		}
		// 2. the splice on the table date: confirmed(1), no Warning
		{
			var bars = Daily(new DateTime(2024, 3, 18), new DateTime(2024, 6, 28), d => d == d0617 ? 70 : 0.5);
			var j = new List<string>(); var w = new List<string>(); var sp = new List<string>();
			var rolls = HostTable(bars, table, 1.0, j, w, sp);
			Check(rolls.Count == 1 && rolls[0] == BarOf(bars, d0617) && j.Count == 1 && j[0].StartsWith("ROLL table roll(2024-06-17) reopenGapPts(70.00)") && w.Count == 0
				&& sp.Count == 1 && sp[0].StartsWith("ROLL table splice(2024-06-17) at(2024-06-16 17:00) via(reopen) session(+0) gapBar(2024-06-16 17:00) gapPts(+70.00) gapTicks(+280)") && sp[0].EndsWith("found(1)"),
				"ruling 11:30: a reopen jump on the table date is the splice (found(1)), no Warning (" + string.Join(" | ", j) + " | " + string.Join(" | ", sp) + ")");
			var j2 = new List<string>(); var w2 = new List<string>(); var sp2 = new List<string>();
			HostTable(bars, table, 1.0, j2, w2, sp2, 1.5);
			Check(sp2.Count == 1 && sp2[0].Contains("via(fallback)") && sp2[0].Contains("minAtr(1.5)") && sp2[0].EndsWith("found(0)"), "minAtr is applied (1.5: 70 pts < 1.5 x ATR -> fallback)");
		}
		// 3. ATR warm-up: fewer than 20 true ranges before 06-13 / 06-17. The table roll still fires, prints NaN and confirmed(-);
		// the off-table check is skipped, so the 06-13 splice warns nothing.
		{
			var bars = Daily(new DateTime(2024, 5, 27), new DateTime(2024, 6, 28), d => d == d0613 ? 70 : 0.5);
			var j = new List<string>(); var w = new List<string>();
			var rolls = HostTable(bars, table, 1.0, j, w);
			Check(rolls.Count == 1 && rolls[0] == BarOf(bars, d0617), "T-R1 warm-up: the table roll still fires");
			Check(j.Count == 1 && j[0].StartsWith("ROLL table roll(2024-06-17) reopenGapPts(0.50) atr20(NaN) gapAtr(NaN) minAtr(0.4) offset("), "T-R1 warm-up line (" + string.Join(" | ", j) + ")");
			Check(w.Count == 0, "T-R1 warm-up: no off-table Warning");
		}
		// 4. thresholds and formats
		Check(!_RollDetector.GapConfirmed(60, 60, 1.0) && _RollDetector.GapConfirmed(60.01, 60, 1.0) && !_RollDetector.GapConfirmed(100, double.NaN, 1.0) && !_RollDetector.GapConfirmed(100, 0, 1.0),
			"T-R1 confirmed = g > k x A (strict), never while A is NaN or 0");
		Check(_RollDetector.OffTableWarning(new DateTime(2024, 6, 13), 60, 60, 1.0) == null
			&& _RollDetector.OffTableWarning(new DateTime(2024, 6, 13), 90, 60, 1.0) == "ROLL off-table gap(2024-06-13) gapPts(90.00) atr20(60.00) gapAtr(1.500) k(1.0)",
			"T-R1 off-table Warning text and threshold");
		Check(_RollDetector.TableRollLine(new DateTime(2024, 6, 17), 0.5, 50, 0.4) == "ROLL table roll(2024-06-17) reopenGapPts(0.50) atr20(50.00) gapAtr(0.010) minAtr(0.4) offset(search: first bar to the end of the next session with |open - prior close| >= minAtr x atr20; else fallback)", "table roll line format");
		// 5. DayStart facts reset on a non-start bar; Series-only keeps DayInTable false
		{
			var det = new _RollDetector { series = true, dates = table };
			det.Step(1, new DateTime(2024, 6, 14), 100, 100, "@MES", 0.25); det.Step(2, d0617, 101, 100, "@MES", 0.25);
			bool start = det.DayStart && !det.DayInTable && det.DayGap == 1;
			det.Step(3, d0617, 101, 101, "@MES", 0.25);
			Check(start && !det.DayStart, "T-R1 DayStart only on a day's first bar; DayInTable needs Table");
		}

		// 6. host source pins (no suite compiles _CustomSignal against MC): k = 1.0, calibrator fed in Table mode, the journal and
		// Warning levels, T-R1b empty-table throw at the first closed bar, and the fixed-year profile fill.
		{
			string path = null;
			for (var di = new DirectoryInfo(AppContext.BaseDirectory); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			Check(Regex.IsMatch(src, @"protected\s+double\s+RollGapAtrK\s*=\s*1\.0\s*;") && !Regex.IsMatch(src, @"\[Input\][^;{]*RollGapAtrK"), "T-R1 RollGapAtrK = 1.0, not an [Input]");
			Check(Regex.IsMatch(src, @"if\s*\(m_rollDet\.series\s*\|\|\s*m_rollDet\.table\)\s*\{\s*m_gapCal\.AddBar\("), "T-R1 the ATR(20) calibrator is fed in Table mode too");
			Check(Regex.IsMatch(src, @"trace\(ETraceLevel\.Journal,\s*_RollDetector\.TableRollLine\(m_rollDet\.DayStartDay,\s*m_rollDet\.DayGap,\s*atrA,\s*RollSpliceMinAtr\)\)")
				&& Regex.IsMatch(src, @"_RollDetector\.OffTableWarning\(m_rollDet\.DayStartDay,\s*m_rollDet\.DayGap,\s*atrA,\s*RollGapAtrK\)") && Regex.IsMatch(src, @"trace\(ETraceLevel\.Warning,\s*off\s*\+"),
				"T-R1 table line at Journal, off-table line at Warning, both with RollGapAtrK");
			Check(Regex.IsMatch(src, @"configureRollDetector\(\);\s*requireRollTable\(\);")
				&& Regex.IsMatch(src, @"if\s*\(!m_rollDet\.table\s*\|\|\s*RollDates\.Count\s*>\s*0\s*\|\|\s*!isFuturesCategory\(Bars\.Info\.Category\)\)\s*return;")
				&& src.Contains("InstrumentReason.UnknownField(\"roll\")") && Regex.IsMatch(src, @"throw\s+new\s+InvalidOperationException\(text\)"),
				"T-R1b Table / SeriesOrTable with an empty RollDates on futures: Error + unknown-field:roll at the first closed bar");
			Check(Regex.IsMatch(src, @"protected\s+int\s+RollTableFromYear\s*=\s*2000,\s*RollTableToYear\s*=\s*2035\s*;") && Regex.IsMatch(src, @"protected\s+bool\s+RollTableFromProfile\s*=\s*false\s*;")
				&& Regex.IsMatch(src, @"InstrumentRules\.CollectCoveredRollDates\(rp,\s*RollTableFromYear,\s*RollTableToYear,\s*""ROLL"""),
				"T-R2 fixed contract years 2000-2035 (not [Input]s); the profile fill is opt-in per study (no auto-fill)");
		}
	}
	// ---- T-R4 evidence collector (owner ruling 2026-11-23): per table date, +-5 sessions of start / max-bar gaps and a verdict ----
	// sessions: weekdays from `from`, 4 bars each (first bar = the Globex reopen). startGap(day) is added to the first bar's open,
	// barGap(day) to the third bar's open. A = 50 points; tick 0.25.
	static List<string> Ev(RollEvidence ev, DateTime from, int days, Func<DateTime, double> startGap, Func<DateTime, double> barGap, bool finish)
	{
		var outp = new List<string>(); double px = 5000, prev = double.NaN; DateTime d = from;
		for (int k = 0; k < days; d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			k++;
			for (int m = 0; m < 4; m++)
			{
				double o = px + (m == 0 ? startGap(d) : m == 2 ? barGap(d) : 0) + (m == 1 ? 0.25 : 0);
				ev.AddBar(d, d.AddDays(-1).AddHours(17).AddMinutes(m == 0 ? 0 : 900 + m), o, prev, 50, outp);
				px = o + (m % 2 == 0 ? 0.5 : -0.5); prev = px;
			}
		}
		if (finish) ev.Finish(outp);
		return outp;
	}

	static void Evidence()
	{
		var t0617 = new DateTime(2024, 6, 17); var t0613 = new DateTime(2024, 6, 13);
		var dates = new HashSet<DateTime> { t0617 };
		Func<DateTime, double> small = d => (d.Day % 3) * 0.25;   // ordinary reopen gaps 0..2 ticks
		Func<DateTime, double> none = d => 0;
		DateTime from = new DateTime(2024, 5, 27);
		// 1. splice on the table date's reopen: aligned
		{
			var ev = new RollEvidence { dates = dates, tick = 0.25 };
			var o = Ev(ev, from, 30, d => d == t0617 ? 20.0 : small(d), none, true);
			var days = o.FindAll(l => l.StartsWith("ROLL T-R4 table(2024-06-17) day(", StringComparison.Ordinal));
			string v = o.Find(l => l.StartsWith("ROLL T-R4 verdict table(2024-06-17) ", StringComparison.Ordinal));
			Check(days.Count == 11 && days[0].Contains("day(2024-06-10) rel(-5)") && days[5].Contains("day(2024-06-17) rel(0)") && days[10].Contains("day(2024-06-24) rel(+5)"),
				"T-R4 one line per session rel(-5)..rel(+5) (sessions, not calendar days): " + (days.Count > 0 ? days[0] : "none"));
			Check(v != null && v.Contains(" aligned | largest(2024-06-17 start ") && v.Contains("pts +20.00 ticks +80 atr 0.400") && v.Contains("confirmed(0)") && v.Contains("sessions(5 before 5 after)"),
				"T-R4 aligned: largest on the table date at the reopen, ticks / atr / T-R1 flag: " + v);
			Check(days[5].Contains("largest(1)") && days[4].Contains("largest(0)"), "T-R4 the largest day is flagged");
			Check(ev.Aligned == 1 && ev.OffDate == 0 && ev.NoVisible == 0 && ev.Incomplete == 0 && o[o.Count - 1].StartsWith("ROLL T-R4 summary aligned(1) dataSpliceOtherDay(0) noVisibleSplice(0) incomplete(0)", StringComparison.Ordinal), "T-R4 summary counts");
			Check(o.TrueForAll(l => l.IndexOf(',') < 0), "T-R4 lines are comma-free (never quoted in the MCSignal CSV)");
		}
		// 2. MES June 2024 case: the data splices on 06-13, the table says 06-17
		{
			var ev = new RollEvidence { dates = dates, tick = 0.25 };
			var o = Ev(ev, from, 30, d => d == t0613 ? -15.0 : small(d), none, true);
			string v = o.Find(l => l.StartsWith("ROLL T-R4 verdict table(2024-06-17) ", StringComparison.Ordinal));
			Check(v != null && v.Contains(" data splice on 2024-06-13 | largest(2024-06-13 start ") && v.Contains("pts -15.00 ticks -60 atr 0.300") && ev.OffDate == 1,
				"T-R4 data splice on another day of the window (MES 06-13 vs table 06-17): " + v);
		}
		// 3. a splice inside the table date's session (not at the reopen): aligned, where = bar time
		{
			var ev = new RollEvidence { dates = dates, tick = 0.25 };
			var o = Ev(ev, from, 30, small, d => d == t0617 ? 60.0 : 0, true);
			string v = o.Find(l => l.StartsWith("ROLL T-R4 verdict table(2024-06-17) ", StringComparison.Ordinal));
			Check(v != null && v.Contains(" aligned | largest(2024-06-17 bar 2024-06-17 08:02 ") && v.Contains("atr 1.200"), "T-R4 an in-session splice counts, with its bar time: " + v);
		}
		// 4. no day stands out: no visible splice
		{
			var ev = new RollEvidence { dates = dates, tick = 0.25 };
			var o = Ev(ev, from, 30, d => 1.0 + (d.Day % 2) * 0.25, none, true);
			string v = o.Find(l => l.StartsWith("ROLL T-R4 verdict table(2024-06-17) ", StringComparison.Ordinal));
			Check(v != null && v.Contains(" no visible splice | ") && ev.NoVisible == 1, "T-R4 no visible splice when the largest is < 2 x the median: " + v);
		}
		// 5. the chart ends 2 sessions after the date: reported at Finish as incomplete, not counted as a verdict
		{
			var ev = new RollEvidence { dates = dates, tick = 0.25 };
			var o = Ev(ev, from, 18, d => d == t0617 ? 20.0 : small(d), none, false);
			Check(!o.Exists(l => l.StartsWith("ROLL T-R4 verdict", StringComparison.Ordinal)), "T-R4 no verdict before 5 sessions after the date");
			ev.Finish(o);
			string v = o.Find(l => l.StartsWith("ROLL T-R4 verdict table(2024-06-17) ", StringComparison.Ordinal));
			Check(v != null && v.Contains("aligned (incomplete window)") && v.Contains("sessions(5 before 2 after)") && ev.Incomplete == 1 && ev.Aligned == 0, "T-R4 incomplete window at the end of data: " + v);
		}
		// 6. helpers: Gap format, warm-up NaN, Median, Verdict on an empty window
		{
			var ev = new RollEvidence { tick = 0.25 };
			Check(ev.Gap(-3.0, 60) == "pts -3.00 ticks -12 atr 0.050" && ev.Gap(2.5, double.NaN) == "pts +2.50 ticks +10 atr NaN" && ev.Gap(double.NaN, 50) == "pts NaN ticks NaN atr NaN", "T-R4 Gap format (signed pts / ticks; atr NaN in warm-up)");
			Check(RollEvidence.Median(new List<double> { 3, 1, 2 }) == 2 && RollEvidence.Median(new List<double> { 4, 1, 2, 3 }) == 2.5 && double.IsNaN(RollEvidence.Median(new List<double>())), "T-R4 Median");
			int li; double r;
			Check(RollEvidence.Verdict(new List<RollEvidence.Day>(), null, out li, out r) == "no visible splice" && li == -1, "T-R4 empty window: no visible splice");
			Check(RollEvidence.Sessions == 5 && RollEvidence.StandoutRatio == 2.0, "T-R4 window +-5 sessions, standout 2.0 x median");
		}
		// 7. host pins: default off, not an [Input]; only _RollEvidenceProbe turns it on; fed once per bar with the T-R1 day key and A
		{
			string path = null;
			for (var di = new DirectoryInfo(AppContext.BaseDirectory); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string root = path == null ? null : Path.GetDirectoryName(path);
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			Check(Regex.IsMatch(src, @"protected\s+bool\s+RollEvidenceJournal\s*=\s*false\s*;") && !Regex.IsMatch(src, @"\[Input\][^;{]*RollEvidenceJournal"), "T-R4 RollEvidenceJournal defaults false, not an [Input]");
			Check(Regex.IsMatch(src, @"if\s*\(RollEvidenceJournal\s*&&\s*m_rollDet\.table\s*&&\s*bar\s*!=\s*m_rollEvBar\s*&&\s*!m_rollEvDone\)\s*stepRollEvidence\(bar,\s*day,\s*prev\);")
				&& Regex.IsMatch(src, @"m_rollEv\.AddBar\(day,\s*Bars\.Time\[0\],\s*Bars\.Open\[0\],\s*prev,\s*m_gapCal\.Atr,\s*m_rollEvOut\);"), "T-R4 host feed: once per bar, the detector's day key and the T-R1 A");
			var setters = new List<string>();
			if (root != null)
				foreach (string f in Directory.GetFiles(root, "*.CS", SearchOption.AllDirectories))
				{
					if (f.Contains(Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar)) continue;
					if (Regex.IsMatch(Regex.Replace(File.ReadAllText(f), @"//[^\n]*", ""), @"RollEvidenceJournal\s*=\s*true")) setters.Add(Path.GetFileName(f));
				}
			Check(setters.Count == 1 && setters[0] == "_RollEvidenceProbe.Strategy.CS", "T-R4 only _RollEvidenceProbe sets RollEvidenceJournal = true: " + string.Join(" ", setters));
			string probe = root == null ? "" : File.ReadAllText(Path.Combine(root, "_RollEvidenceProbe.Strategy.CS"));
			string proj = root == null ? "" : File.ReadAllText(Path.Combine(root, "PLStudies.csproj"));
			Check(probe.Contains("[IOGMode(IOGMode.Disabled)]") && !Regex.IsMatch(Regex.Replace(probe, @"//[^\n]*", ""), @"OrderCreator|\.Send\(") && proj.Contains("<Compile Include=\"_RollEvidenceProbe.Strategy.CS\" />"),
				"T-R4 probe: IOG disabled, no orders, in PLStudies.csproj");
		}
	}
	// ---- Owner ruling 2026-11-23 11:30 CT: the roll price offset goes on the first bar from the table reopen with |g| >= 0.4 x A ----
	// Sessions of 6 bars (CT): 17:00 reopen, 22:51, 03:32, 07:30, 08:30, 14:30. Flat bars (H/L = O +- 15), so A = ATR(20) = 30.
	static readonly int[] s_mins = { 0, 351, 632, 870, 930, 1290 };
	struct IB { public int bar; public DateTime day, time; public double o, h, l, c; public bool start; }
	static List<IB> Intraday(DateTime from, DateTime to, Func<DateTime, int, double> gap)
	{
		var l = new List<IB>(); double px = 5200; int bar = 1;
		for (DateTime d = from; d <= to; d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			DateTime reopen = d.AddDays(d.DayOfWeek == DayOfWeek.Monday ? -3 : -1).AddHours(17);
			for (int m = 0; m < s_mins.Length; m++)
			{
				double o = px + (l.Count > 0 ? gap(d, m) : 0);
				l.Add(new IB { bar = bar++, day = d, time = reopen.AddMinutes(s_mins[m] + (m > 0 && d.DayOfWeek == DayOfWeek.Monday ? 2880 : 0)), o = o, h = o + 15, l = o - 15, c = o, start = m == 0 });
				px = o;
			}
		}
		return l;
	}
	/// <summary>The host order (stepRollDetector): calibrator, detector, Arm on the table reopen, locator; each bar stepped twice.</summary>
	static List<RollSpliceLocator> RunSplice(List<IB> bars, HashSet<DateTime> dates, List<string> lines, List<int> spliceBars)
	{
		var det = new _RollDetector { table = true, dates = dates }; var cal = new RollGapCalibrator(); var loc = new RollSpliceLocator();
		var res = new List<RollSpliceLocator>();
		for (int i = 0; i < bars.Count; i++)
			for (int rep = 0; rep < 2; rep++)
			{
				IB b = bars[i]; double prev = i > 0 ? bars[i - 1].c : double.NaN;
				cal.AddBar(b.day, b.h, b.l, b.c);
				bool stepped = det.Step(b.bar, b.day, b.o, prev, "@ES", 0.25);
				if (stepped && det.DayInTable) loc.Arm(det.DayStartDay);
				if (loc.Step(b.bar, b.day, b.time, b.o, prev, cal.Atr, det.DayStart)) { lines.Add(_RollDetector.SpliceLine(loc, 0.25)); spliceBars.Add(b.bar); }
			}
		return res;
	}
	static string One(List<string> l) { return l.Count == 1 ? l[0] : "(" + l.Count + ") " + string.Join(" | ", l); }

	static void SpliceLocator()
	{
		var d0617 = new DateTime(2024, 6, 17); var d0618 = new DateTime(2024, 6, 18); var d1216 = new DateTime(2024, 12, 16);
		var table = new HashSet<DateTime> { d0617, new DateTime(2024, 9, 16), d1216 };
		DateTime from = new DateTime(2024, 4, 15), to = new DateTime(2024, 6, 28);
		Check(RollSpliceLocator.DefaultMinAtr == 0.4, "ruling 11:30: minAtr 0.4 (named constant; host RollSpliceMinAtr, not an [Input])");
		// 1. aligned: the jump is the table-date reopen (6 of 8 dates on ES / MES / NQ / MNQ, T-R4 on fabf1bd)
		{
			var l = new List<string>(); var b = new List<int>();
			var bars = Intraday(from, to, (d, m) => d == d0617 && m == 0 ? 25 : 0);
			RunSplice(bars, table, l, b);
			Check(One(l) == "ROLL table splice(2024-06-17) at(2024-06-14 17:00) via(reopen) session(+0) gapBar(2024-06-14 17:00) gapPts(+25.00) gapTicks(+100) atr20(30.00) gapAtr(0.833) minAtr(0.4) found(1)",
				"splice: aligned reopen jump, on the table roll bar itself: " + One(l));
		}
		// 2. June 2024 pattern: the data switches in the NEXT session (ES 06-17 22:51 CT = trading day 06-18; MES / NQ / MNQ 06-18 03:32)
		{
			var l = new List<string>(); var b = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0618 && m == 1 ? -20 : (m == 0 ? 0.25 : 0)), table, l, b);
			Check(One(l) == "ROLL table splice(2024-06-17) at(2024-06-17 22:51) via(intrabar) session(+1) gapBar(2024-06-17 22:51) gapPts(-20.00) gapTicks(-80) atr20(30.00) gapAtr(0.667) minAtr(0.4) found(1)",
				"splice: ES June 2024, late splice on the 22:51 bar of the next session: " + One(l));
			var l2 = new List<string>(); var b2 = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0618 && m == 2 ? 15 : (m == 0 ? 0.25 : 0)), table, l2, b2);
			Check(One(l2).StartsWith("ROLL table splice(2024-06-17) at(2024-06-18 03:32) via(intrabar) session(+1) gapBar(2024-06-18 03:32) gapPts(+15.00) gapTicks(+60) atr20(30.00) gapAtr(0.500)"),
				"splice: MES / NQ / MNQ June 2024, late splice at 06-18 03:32: " + One(l2));
		}
		// 3. December 2024 pattern: tiny reopen gap, the switch is intrabar at 03:32 CT on the table date
		{
			var l = new List<string>(); var b = new List<int>();
			var bars = Intraday(new DateTime(2024, 10, 1), new DateTime(2024, 12, 31), (d, m) => d == d1216 && m == 2 ? 18 : (d == d1216 && m == 0 ? 0.5 : 0));
			RunSplice(bars, table, l, b);
			Check(One(l) == "ROLL table splice(2024-12-16) at(2024-12-16 03:32) via(intrabar) session(+0) gapBar(2024-12-16 03:32) gapPts(+18.00) gapTicks(+72) atr20(30.00) gapAtr(0.600) minAtr(0.4) found(1)",
				"splice: December 2024, intrabar 03:32 jump on the table date: " + One(l));
		}
		// 4. no jump: fallback on the first bar after the next session, offset = the largest gap of the window (first one on a tie)
		{
			var l = new List<string>(); var b = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0618 && m == 3 ? 2.0 : (d == d0617 && m == 4 ? -1.0 : 0)), table, l, b);
			Check(One(l) == "ROLL table splice(2024-06-17) at(2024-06-18 17:00) via(fallback) session(+2) gapBar(2024-06-18 07:30) gapPts(+2.00) gapTicks(+8) atr20(30.05) gapAtr(0.067) minAtr(0.4) found(0)",
				"splice: no qualifying bar -> fallback at the end of the next session with the window's largest gap: " + One(l));
		}
		// 5. boundary: A = 30, so 0.39 x A = 11.7 does not qualify and 0.41 x A = 12.3 does (same bar)
		{
			var l = new List<string>(); var b = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0617 && m == 2 ? 11.7 : 0), table, l, b);
			Check(One(l).Contains("via(fallback)") && One(l).Contains("gapBar(2024-06-17 03:32) gapPts(+11.70)") && One(l).Contains("gapAtr(0.390)"), "splice: 0.39 x A does not qualify (fallback, but that bar is the largest gap): " + One(l));
			var l2 = new List<string>(); var b2 = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0617 && m == 2 ? 12.3 : 0), table, l2, b2);
			Check(One(l2).Contains("at(2024-06-17 03:32) via(intrabar) session(+0)") && One(l2).Contains("gapAtr(0.410)") && One(l2).EndsWith("found(1)"), "splice: 0.41 x A qualifies: " + One(l2));
			Check(RollSpliceLocator.Qualifies(12.0, 30, 0.4) && !RollSpliceLocator.Qualifies(11.99, 30, 0.4) && RollSpliceLocator.Qualifies(-12.0, 30, 0.4) && !RollSpliceLocator.Qualifies(50, double.NaN, 0.4) && !RollSpliceLocator.Qualifies(50, 0, 0.4),
				"splice: |g| >= 0.4 x A inclusive, either sign; never while A is NaN or 0");
		}
		// 6. news-sized gap BEFORE the real splice (documented risk): a 0.433 x A gap at 07:30 CT on the table date is taken as the
		// splice; the real switch at 06-18 03:32 then gets no offset (one offset per roll: nothing is applied twice).
		{
			var l = new List<string>(); var b = new List<int>();
			RunSplice(Intraday(from, to, (d, m) => d == d0617 && m == 3 ? 13 : (d == d0618 && m == 2 ? 25 : 0)), table, l, b);
			Check(One(l).StartsWith("ROLL table splice(2024-06-17) at(2024-06-17 07:30) via(intrabar) session(+0) gapBar(2024-06-17 07:30) gapPts(+13.00)") && l.Count == 1,
				"splice: a pre-splice news bar >= 0.4 x A triggers early and the later real jump is not offset again (documented): " + One(l));
		}
		// 7. causality and idempotence: nothing before Arm; a repeated bar returns false; warm-up (A NaN) never qualifies
		{
			var loc = new RollSpliceLocator();
			Check(!loc.Step(1, d0617, d0617, 100, 50, 30, true), "splice: no search before Arm");
			loc.Arm(d0617);
			Check(loc.Step(2, d0617, d0617, 100, 50, 30, true) && !loc.Step(2, d0617, d0617, 100, 50, 30, true) && !loc.Armed, "splice: one result per bar, then disarmed");
			loc.Arm(d0617);
			Check(!loc.Step(3, d0617, d0617, 100, 50, double.NaN, true) && !loc.Step(4, d0618, d0618, 200, 100, double.NaN, true) && loc.Step(5, new DateTime(2024, 6, 19), d0618, 0, 0, 30, true)
				&& loc.Via == RollSpliceLocator.EVia.Fallback && loc.Gap == 100 && !loc.Found, "splice: warm-up ends in the fallback with the largest gap (100)");
		}
		// 8. host pins: the price offset follows rollSpliceOn (M-DAILY history + in-session aggregate before the merge; TOD splice bar);
		// rollOn (the roll event) is unchanged; Table only; the located splice is not also an off-table Warning.
		{
			string path = null;
			for (var di = new DirectoryInfo(AppContext.BaseDirectory); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			Check(Regex.IsMatch(src, @"protected\s+double\s+RollSpliceMinAtr\s*=\s*RollSpliceLocator\.DefaultMinAtr\s*;") && !Regex.IsMatch(src, @"\[Input\][^;{]*RollSpliceMinAtr"), "host: RollSpliceMinAtr = 0.4 constant, not an [Input]");
			Check(Regex.IsMatch(src, @"if\s*\(m_rollDet\.table\s*&&\s*!m_rollDet\.series\)\s*\{\s*if\s*\(stepped\s*&&\s*m_rollDet\.DayInTable\)\s*m_spliceLoc\.Arm\(m_rollDet\.DayStartDay,\s*Bars\.Time\[0\]\);")
				&& Regex.IsMatch(src, @"m_spliceLoc\.Step\(bar,\s*day,\s*Bars\.Time\[0\],\s*Bars\.Open\[0\],\s*prev,\s*m_gapCal\.Atr,\s*m_rollDet\.DayStart,\s*m_spliceLoc\.bodyCheck\s*\?\s*Bars\.Close\[0\]\s*:\s*double\.NaN\)")
				&& Regex.IsMatch(src, @"else\s+if\s*\(stepped\)\s*setSplice\(bar,\s*double\.NaN,\s*false\);\s*\}"), "host: Table arms on the reopen and steps with the carried A; other modes put the offset on the roll bar");
			Check(Regex.IsMatch(src, @"if\s*\(splice\s*&&\s*m_daily\.Days\s*>\s*0\)\s*m_daily\.OnRoll\(key,\s*double\.IsNaN\(spliceOff\)\s*\?\s*Bars\.Open\[0\]\s*-\s*m_daily\.PrevClose\s*:\s*spliceOff\);")
				&& !Regex.IsMatch(src, @"if\s*\(roll\s*&&\s*m_daily\.Days\s*>\s*0\)\s*m_daily\.OnRoll"), "host: M-DAILY offset on the splice bar only (never on the table reopen as well)");
			int shift = src.IndexOf("m_dailyAggO += spliceOff; m_dailyAggH += spliceOff; m_dailyAggL += spliceOff;", StringComparison.Ordinal), merge = src.IndexOf("m_dailyAggH = Math.Max(m_dailyAggH, bh)", StringComparison.Ordinal);   // 18:46 CT: bh = the sanitised mixed-bar high
			Check(shift > 0 && merge > shift && src.IndexOf("m_dailyTodayOpen += spliceOff;", StringComparison.Ordinal) > 0, "host: an in-session splice shifts today's open and running O/H/L before the splice bar is merged");
			Check(Regex.IsMatch(src, @"bool\s+splice\s*=\s*rollSpliceOn\(\);\s*double\s+tr\s*=") && Regex.IsMatch(src, @"else\s+if\s*\(traceOn\(ETraceLevel\.Warning\)\s*&&\s*m_spliceBar\s*!=\s*bar\)"), "host: TOD drops the splice bar's range; a located splice is not warned as off-table");
		}
	}

	// ---- Owner ruling 2026-11-23 18:46 CT: Data1 slower than 1 minute (Minute size > 1, Hour) may hold the switch INSIDE a bar
	// (ES 2024-12-16 03:32 CT inside the 15-minute 03:30-03:45 bar: open gap +0.25, net jump +70.5). After the open-gap test fails the
	// locator tests close - prevClose against the same 0.4 x A and journals via(intrabar-body). 1-minute and faster, and
	// Daily / Weekly / Monthly, never run the body test (bodyCheck = false: byte-identical to before).
	static List<IB> IntradayMin(DateTime from, DateTime to, int minutes, DateTime mixedEnd, double openGap, double body)
	{
		var l = new List<IB>(); double px = 6050; int bar = 1;
		for (DateTime d = from; d <= to; d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			DateTime reopen = d.AddDays(-1).AddHours(17);
			for (DateTime t = reopen.AddMinutes(minutes); t <= d.AddHours(16); t = t.AddMinutes(minutes))
			{
				IB b = new IB { bar = bar++, day = d, time = t, start = t == reopen.AddMinutes(minutes) };
				if (t == mixedEnd) { b.o = px + openGap; b.c = px + openGap + body; b.h = Math.Max(b.o, b.c) + 2; b.l = Math.Min(b.o, b.c) - 2; }
				else { b.o = px; b.c = px; b.h = px + 15; b.l = px - 15; }
				l.Add(b); px = b.c;
			}
		}
		return l;
	}
	sealed class BodyRun { public List<string> lines = new List<string>(), resets = new List<string>(); public int splice = -1, reopenBar = -1; public RollSpliceLocator.EVia via; public double gap = double.NaN, openGap = double.NaN, offset = double.NaN; public bool intrabar, resetsAgain, found; public int session = -1; }
	/// <summary>The host order (stepRollDetector) with the 18:46 body test: bodyCheck as the host's spliceBodyCheck(resolution) sets it.</summary>
	static BodyRun RunBody(List<IB> bars, HashSet<DateTime> dates, bool bodyCheck)
	{
		var r = new BodyRun();
		var det = new _RollDetector { table = true, dates = dates }; var cal = new RollGapCalibrator(); var loc = new RollSpliceLocator { bodyCheck = bodyCheck };
		for (int i = 0; i < bars.Count; i++)
			for (int rep = 0; rep < 2; rep++)
			{
				IB b = bars[i]; double prev = i > 0 ? bars[i - 1].c : double.NaN;
				cal.AddBar(b.day, b.h, b.l, b.c);
				bool stepped = det.Step(b.bar, b.day, b.o, prev, "@ES", 0.25);
				if (stepped && det.DayInTable) { loc.Arm(det.DayStartDay, b.time); r.reopenBar = b.bar; }
				if (loc.Step(b.bar, b.day, b.time, b.o, prev, cal.Atr, det.DayStart, loc.bodyCheck ? b.c : double.NaN))
				{
					r.lines.Add(_RollDetector.SpliceLine(loc, 0.25)); r.splice = b.bar;
					r.via = loc.Via; r.gap = loc.Gap; r.openGap = loc.OpenGap; r.resetsAgain = loc.ResetsAgain; r.found = loc.Found; r.session = loc.Session;
					bool body = loc.Via == RollSpliceLocator.EVia.IntrabarBody;   // host: setSplice(bar, Via == Reopen ? NaN : Gap, Via == Intrabar || body, body)
					r.offset = loc.Via == RollSpliceLocator.EVia.Reopen ? double.NaN : loc.Gap; r.intrabar = loc.Via == RollSpliceLocator.EVia.Intrabar || body;
					if (loc.ResetsAgain && b.bar != det.LastRollBar) r.resets.Add(_RollDetector.ResetAtSpliceLine(loc));
				}
			}
		return r;
	}

	static void SpliceBody()
	{
		var d1216 = new DateTime(2024, 12, 16); var table = new HashSet<DateTime> { new DateTime(2024, 9, 16), d1216 };
		DateTime from = new DateTime(2024, 11, 1), to = new DateTime(2024, 12, 20), sw15 = new DateTime(2024, 12, 16, 3, 45, 0);
		Check(new RollSpliceLocator().bodyCheck == false, "18:46: bodyCheck defaults off (1-minute, tick, second, daily+ and every caller that does not opt in)");
		// 1. 15-minute Data1: the 03:30-03:45 bar holds the 03:32 switch (open gap +0.25, net +70.5)
		{
			var bars = IntradayMin(from, to, 15, sw15, 0.25, 70.25);
			BodyRun r = RunBody(bars, table, true);
			Check(One(r.lines) == "ROLL table splice(2024-12-16) at(2024-12-16 03:45) via(intrabar-body) session(+0) gapBar(2024-12-16 03:45) gapPts(+70.50) gapTicks(+282) atr20(30.00) gapAtr(2.350) minAtr(0.4) found(1) test(close - prevClose) openGapPts(+0.25) mixedBar(1)",
				"18:46 15m: mid-bar switch 2024-12-16 03:30-03:45 located on the mixed bar, via(intrabar-body), session(+0): " + One(r.lines));
			Check(r.via == RollSpliceLocator.EVia.IntrabarBody && r.resetsAgain && r.found && r.session == 0 && r.gap == 70.5 && r.openGap == 0.25 && r.splice != r.reopenBar && bars[r.splice - 1].time == sw15,
				"18:46 15m: ResetsAgain, Gap = close - prevClose = +70.50, OpenGap +0.25, splice bar = the 03:45 bar (not the reopen, not a fallback)");
			Check(One(r.resets) == "ROLL table reset-at-splice(2024-12-16) at(2024-12-16 03:45) via(intrabar-body) session(+0) rollEvent(2024-12-15 17:15) engines(AVWAP VWAP PRICEACTION PROFILE)",
				"18:46 15m: reset-at-splice fires on the mixed bar: " + One(r.resets));
			Check(r.offset == 70.5 && r.intrabar, "18:46 15m: host offset (M-DAILY OnRoll + in-session shift) = +70.50, treated as intrabar");
			Check(!r.lines.Any(x => x.Contains("fallback")) && !r.lines.Any(x => x.Contains("2024-12-17")), "18:46 15m: no 2-session-late fallback (was at(2024-12-17 17:15) via(fallback) session(+2))");
			// M-DAILY replica of the host merge (stepDaily): earlier bars shifted by +off, the mixed bar merged as {open + off, close}
			double off = r.offset, hi = double.MinValue, lo = double.MaxValue, rawHi = double.MinValue, rawLo = double.MaxValue;
			foreach (IB b in bars.Where(x => x.day == d1216))
			{
				bool before = b.bar < r.splice, mixed = b.bar == r.splice;
				double bh = b.h, bl = b.l;
				if (before) { bh += off; bl += off; }
				if (mixed) { double bo = b.o + off; bh = Math.Max(bo, b.c); bl = Math.Min(bo, b.c); }
				hi = Math.Max(hi, bh); lo = Math.Min(lo, bl); rawHi = Math.Max(rawHi, b.h); rawLo = Math.Min(rawLo, b.l);
			}
			Check(hi - lo == 30 && rawHi - rawLo > 100, "18:46 M-DAILY: the splice day's range in new-contract terms is the clean 30 pts (raw mixed day " + (rawHi - rawLo) + ")");
		}
		// 2. 5-minute Data1: switch inside the 03:30-03:35 bar
		{
			var bars = IntradayMin(from, to, 5, new DateTime(2024, 12, 16, 3, 35, 0), 0.25, 70.25);
			BodyRun r = RunBody(bars, table, true);
			Check(One(r.lines).StartsWith("ROLL table splice(2024-12-16) at(2024-12-16 03:35) via(intrabar-body) session(+0) gapBar(2024-12-16 03:35) gapPts(+70.50)") && r.resets.Count == 1 && r.offset == 70.5,
				"18:46 5m: mid-bar switch located on the 03:35 bar, reset-at-splice, offset +70.50: " + One(r.lines));
		}
		// 3. bodyCheck off (1-minute rule, also Daily+): the same mixed bar does NOT trigger; the locator falls back as before
		{
			var bars = IntradayMin(from, to, 15, sw15, 0.25, 70.25);
			BodyRun r = RunBody(bars, table, false);
			Check(One(r.lines).StartsWith("ROLL table splice(2024-12-16) at(2024-12-17 17:15) via(fallback) session(+2)") && r.via == RollSpliceLocator.EVia.Fallback && !r.found && double.IsNaN(r.openGap) && r.resets.Count == 1,
				"18:46 bodyCheck off: close - prevClose >= 0.4A with a small open gap does not trigger (pre-18:46 behaviour): " + One(r.lines));
		}
		// 4. direct locator: 1-minute (bodyCheck off) small open gap, big close - prevClose -> no trigger; Daily the same
		{
			var one = new RollSpliceLocator(); one.Arm(d1216, new DateTime(2024, 12, 15, 17, 0, 0));
			bool hit = one.Step(1, d1216, new DateTime(2024, 12, 16, 3, 33, 0), 6050.25, 6050, 30, false, 6080);
			Check(!hit && !one.Found && one.Via == RollSpliceLocator.EVia.None, "18:46 1m: |close - prevClose| = 1.0A with |open - prevClose| = 0.25 does not trigger");
			var day = new RollSpliceLocator(); day.Arm(d1216, new DateTime(2024, 12, 13, 16, 0, 0));
			Check(!day.Step(1, d1216, new DateTime(2024, 12, 16, 16, 0, 0), 6050.25, 6050, 30, false, 6120) && !day.Found, "18:46 Daily: no body test (bodyCheck off on Day / Week / Month)");
		}
		// 5. bodyCheck on: the open gap is tested first and wins unchanged; a body under 0.4A does not trigger; at 0.4A it does
		{
			var a = new RollSpliceLocator { bodyCheck = true }; a.Arm(d1216, new DateTime(2024, 12, 15, 17, 0, 0));
			Check(a.Step(1, d1216, new DateTime(2024, 12, 16, 3, 45, 0), 6070, 6050, 30, false, 6000) && a.Via == RollSpliceLocator.EVia.Intrabar && a.Gap == 20 && double.IsNaN(a.OpenGap)
				&& !_RollDetector.SpliceLine(a, 0.25).Contains("test("), "18:46: open gap >= 0.4A wins first (via(intrabar), gap = open - prevClose, no body fields)");
			var b = new RollSpliceLocator { bodyCheck = true }; b.Arm(d1216, new DateTime(2024, 12, 15, 17, 0, 0));
			Check(!b.Step(1, d1216, new DateTime(2024, 12, 16, 3, 45, 0), 6050.25, 6050, 30, false, 6061.75) && b.Step(2, d1216, new DateTime(2024, 12, 16, 4, 0, 0), 6061.75, 6061.75, 30, false, 6073.75)
				&& b.Via == RollSpliceLocator.EVia.IntrabarBody && b.Gap == 12, "18:46: body 11.75 < 12 = 0.4A does not trigger; 12.00 does");
		}
		// 6. host pins: typed resolution switch (no enum strings), the body test only when bodyCheck, setSplice intrabar, M-DAILY / ring
		{
			string path = null;
			for (var di = new DirectoryInfo(AppContext.BaseDirectory); di != null && path == null; di = di.Parent) { string fp = Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"); if (File.Exists(fp)) path = fp; }
			string src = path != null ? Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "") : "";
			Check(Regex.IsMatch(src, @"protected\s+static\s+bool\s+spliceBodyCheck\(EResolution\s+type,\s*long\s+size\)\s*\{\s*switch\s*\(type\)\s*\{\s*case\s+EResolution\.Minute:\s*return\s+size\s*>\s*1;\s*case\s+EResolution\.Hour:\s*return\s+true;\s*default:\s*return\s+false;\s*\}\s*\}")
				&& Regex.IsMatch(src, @"m_spliceLoc\.bodyCheck\s*=\s*spliceBodyCheck\(Bars\.Info\.Resolution\.Type,\s*\(long\)Bars\.Info\.Resolution\.Size\);"),
				"host: bodyCheck = typed EResolution switch (Minute size > 1, Hour; never Tick / Second / 1-minute / Day / Week / Month)");
			Check(Regex.IsMatch(src, @"bool\s+body\s*=\s*m_spliceLoc\.Via\s*==\s*RollSpliceLocator\.EVia\.IntrabarBody;\s*setSplice\(bar,\s*m_spliceLoc\.Via\s*==\s*RollSpliceLocator\.EVia\.Reopen\s*\?\s*double\.NaN\s*:\s*m_spliceLoc\.Gap,\s*m_spliceLoc\.Via\s*==\s*RollSpliceLocator\.EVia\.Intrabar\s*\|\|\s*body,\s*body\);"),
				"host: an intrabar-body splice is set as intrabar with offset Gap = close - prevClose");
			Check(src.Contains("if (splice && m_spliceBody && !double.IsNaN(spliceOff)) { bo += spliceOff; bh = Math.Max(bo, Bars.Close[0]); bl = Math.Min(bo, Bars.Close[0]); }"),
				"host: M-DAILY merges the mixed bar as {open + offset, close} only (no cross-contract range)");
			Check(Regex.IsMatch(src, @"rollMixed\s*=\s*ago\s*==\s*0\s*&&\s*rollResetOn\(ds\)\s*&&\s*rollMixedOn\(ds\)"), "host: ringBarOf flags the mixed splice bar (DVP excludes it)");
		}
	}

	// ---- Owner ruling 2026-11-23 13:31 CT: a LATE splice (not the table reopen bar) fires the AVWAP / VWAP / PRICEACTION / profile
	// resets again on the switch bar. The host model below is stepRollDetector + rollResetOn: a reset on the roll bar (stepped) and on
	// a located splice with ResetsAgain; one reset per bar however often the bar is stepped.
	sealed class ResetRun { public List<int> resets = new List<int>(); public List<int> events = new List<int>(); public List<string> lines = new List<string>(); public int splice = -1; }
	static ResetRun RunResets(List<IB> bars, HashSet<DateTime> dates, Func<IB, bool> onBar = null)
	{
		var r = new ResetRun();
		var det = new _RollDetector { table = true, dates = dates }; var cal = new RollGapCalibrator(); var loc = new RollSpliceLocator();
		for (int i = 0; i < bars.Count; i++)
		{
			IB b = bars[i]; double prev = i > 0 ? bars[i - 1].c : double.NaN; bool reset = false, late = false;
			for (int rep = 0; rep < 2; rep++)
			{
				cal.AddBar(b.day, b.h, b.l, b.c);
				bool stepped = det.Step(b.bar, b.day, b.o, prev, "@ES", 0.25);
				if (stepped && det.DayInTable) loc.Arm(det.DayStartDay, b.time);
				if (stepped && !r.events.Contains(b.bar)) r.events.Add(b.bar);
				if (loc.Step(b.bar, b.day, b.time, b.o, prev, cal.Atr, det.DayStart))
				{
					r.splice = b.bar;
					if (loc.ResetsAgain && b.bar != det.LastRollBar) { late = true; r.lines.Add(_RollDetector.ResetAtSpliceLine(loc)); }
				}
				bool ev = det.LastRollBar == b.bar;
				reset |= ev || late;   // rollResetOn: both sides, one answer per bar
			}
			if (reset) r.resets.Add(b.bar);
		}
		return r;
	}
	/// <summary>Toy engine (VWAP-like): sum(close) / n since the last reset. After a re-reset it must equal the mean of post-switch bars.</summary>
	static double MeanSinceLastReset(List<IB> bars, List<int> resets, int uptoBar)
	{
		double sum = 0; int n = 0;
		foreach (IB b in bars) { if (b.bar > uptoBar) break; if (resets.Contains(b.bar)) { sum = 0; n = 0; } sum += b.c; n++; }
		return n > 0 ? sum / n : double.NaN;
	}
	static double MeanFrom(List<IB> bars, int fromBar, int uptoBar) { double sum = 0; int n = 0; foreach (IB b in bars) if (b.bar >= fromBar && b.bar <= uptoBar) { sum += b.c; n++; } return sum / n; }
	static int BarAt(List<IB> bars, DateTime t) { foreach (IB b in bars) if (b.time == t) return b.bar; return -1; }

	static void ResetAtSplice()
	{
		var d0617 = new DateTime(2024, 6, 17); var d0618 = new DateTime(2024, 6, 18); var d1216 = new DateTime(2024, 12, 16);
		var table = new HashSet<DateTime> { d0617, new DateTime(2024, 9, 16), d1216 };
		DateTime from = new DateTime(2024, 4, 15), to = new DateTime(2024, 6, 28);
		// 1. June 2024 late pattern (ES: switch on 06-17 22:51 CT, trading day 06-18): reset at the table reopen AND at the switch bar
		{
			var bars = Intraday(from, to, (d, m) => d == d0618 && m == 1 ? -20 : (m == 0 ? 0.25 : 0));
			var r = RunResets(bars, table);
			int reopen = BarAt(bars, new DateTime(2024, 6, 14, 17, 0, 0)), sw = BarAt(bars, new DateTime(2024, 6, 17, 22, 51, 0));
			Check(r.events.Count == 1 && r.events[0] == reopen, "reset-at-splice June: the roll EVENT stays on the table reopen only");
			Check(r.resets.Count == 2 && r.resets[0] == reopen && r.resets[1] == sw, "reset-at-splice June: resets at the reopen and again at the 22:51 switch bar: " + string.Join(",", r.resets));
			Check(One(r.lines) == "ROLL table reset-at-splice(2024-06-17) at(2024-06-17 22:51) via(intrabar) session(+1) rollEvent(2024-06-14 17:00) engines(AVWAP VWAP PRICEACTION PROFILE)",
				"reset-at-splice June: journal line: " + One(r.lines));
			int upto = BarAt(bars, new DateTime(2024, 6, 18, 14, 30, 0));
			double got = MeanSinceLastReset(bars, r.resets, upto), post = MeanFrom(bars, sw, upto), old = MeanSinceLastReset(bars, r.events, upto);
			Check(Math.Abs(got - post) < 1e-9 && Math.Abs(old - post) > 1, "reset-at-splice June: engine state after the switch is built only from post-switch bars (11d67e1 state carried the one-spread step: " + old.ToString("0.00") + " vs " + post.ToString("0.00") + ")");
			// MES / NQ / MNQ variant: switch at 06-18 03:32
			var bars2 = Intraday(from, to, (d, m) => d == d0618 && m == 2 ? 15 : (m == 0 ? 0.25 : 0));
			var r2 = RunResets(bars2, table);
			Check(r2.resets.Count == 2 && r2.resets[1] == BarAt(bars2, new DateTime(2024, 6, 18, 3, 32, 0)) && One(r2.lines).Contains("at(2024-06-18 03:32) via(intrabar) session(+1)"), "reset-at-splice June (MES/NQ/MNQ): second reset at 06-18 03:32");
		}
		// 2. December 2024 intrabar pattern: switch at 03:32 CT on the table date
		{
			var bars = Intraday(new DateTime(2024, 10, 1), new DateTime(2024, 12, 31), (d, m) => d == d1216 && m == 2 ? 18 : (d == d1216 && m == 0 ? 0.5 : 0));
			var r = RunResets(bars, table);
			int reopen = BarAt(bars, new DateTime(2024, 12, 13, 17, 0, 0)), sw = BarAt(bars, new DateTime(2024, 12, 16, 3, 32, 0));
			Check(r.resets.Count == 2 && r.resets[0] == reopen && r.resets[1] == sw && r.events.Count == 1 && r.events[0] == reopen, "reset-at-splice December: reset at the reopen and at the 03:32 intrabar switch; one roll event");
			Check(One(r.lines) == "ROLL table reset-at-splice(2024-12-16) at(2024-12-16 03:32) via(intrabar) session(+0) rollEvent(2024-12-13 17:00) engines(AVWAP VWAP PRICEACTION PROFILE)", "reset-at-splice December: journal: " + One(r.lines));
			int upto = BarAt(bars, new DateTime(2024, 12, 16, 14, 30, 0));
			Check(Math.Abs(MeanSinceLastReset(bars, r.resets, upto) - MeanFrom(bars, sw, upto)) < 1e-9, "reset-at-splice December: post-switch state only");
		}
		// 3. aligned reopen splice: exactly one reset, no reset-at-splice line (behaviour unchanged vs 11d67e1)
		{
			var bars = Intraday(from, to, (d, m) => d == d0617 && m == 0 ? 25 : 0);
			var r = RunResets(bars, table);
			Check(r.resets.Count == 1 && r.resets[0] == BarAt(bars, new DateTime(2024, 6, 14, 17, 0, 0)) && r.splice == r.resets[0] && r.lines.Count == 0, "reset-at-splice aligned: exactly one reset (the reopen), no second line");
		}
		// 4. fallback: re-reset on the fallback bar (first bar after the next session)
		{
			var bars = Intraday(from, to, (d, m) => d == d0618 && m == 3 ? 2.0 : (d == d0617 && m == 4 ? -1.0 : 0));
			var r = RunResets(bars, table);
			int fb = BarAt(bars, new DateTime(2024, 6, 18, 17, 0, 0));
			Check(r.resets.Count == 2 && r.resets[1] == fb && One(r.lines).StartsWith("ROLL table reset-at-splice(2024-06-17) at(2024-06-18 17:00) via(fallback) session(+2)"), "reset-at-splice fallback: re-reset at the fallback bar: " + One(r.lines));
		}
		// 5. ResetsAgain: Reopen session 0 = aligned (no); Reopen session 1, Intrabar, Fallback = late (yes); nothing located = no
		{
			var loc = new RollSpliceLocator();
			Check(!loc.ResetsAgain, "ResetsAgain: false before any result");
			loc.Arm(d0617); loc.Step(1, d0617, d0617, 100, 50, 30, true);
			Check(loc.Via == RollSpliceLocator.EVia.Reopen && loc.Session == 0 && !loc.ResetsAgain, "ResetsAgain: aligned reopen -> false");
			loc.Arm(d0617); loc.Step(2, d0617, d0617, 50, 50, 30, true); loc.Step(3, d0618, d0618, 100, 50, 30, true);
			Check(loc.Via == RollSpliceLocator.EVia.Reopen && loc.Session == 1 && loc.ResetsAgain, "ResetsAgain: next-session reopen -> true");
		}
		// 6. Data n (same root): the late-splice pending fires once on the first Data2 close >= the switch time; a bar that holds both
		// the roll pending and the splice pending resets once
		{
			var ev = new _RollPending(); var sp = new _RollPending();
			ev.Arm(new DateTime(2024, 6, 14, 17, 0, 0)); sp.Arm(new DateTime(2024, 6, 17, 22, 51, 0));
			Func<int, DateTime, bool> resetOn = (bar, t) => { bool a = ev.Fire(bar, t), b = sp.Fire(bar, t); return a || b; };
			Check(resetOn(1, new DateTime(2024, 6, 14, 17, 5, 0)) && !resetOn(2, new DateTime(2024, 6, 17, 22, 50, 0)) && resetOn(3, new DateTime(2024, 6, 17, 22, 55, 0)) && !resetOn(4, new DateTime(2024, 6, 17, 23, 0, 0)),
				"reset-at-splice Data2 5m: resets at the first close after the reopen and after the switch, once each");
			ev.Reset(); sp.Reset(); ev.Arm(new DateTime(2024, 6, 14, 17, 0, 0)); sp.Arm(new DateTime(2024, 6, 17, 22, 51, 0));
			int fires = 0; if (resetOn(7, new DateTime(2024, 6, 18, 16, 0, 0))) fires++; if (resetOn(7, new DateTime(2024, 6, 18, 16, 0, 0))) fires++; if (resetOn(8, new DateTime(2024, 6, 19, 16, 0, 0))) fires++;
			Check(fires == 2 && !ev.Armed && !sp.Armed, "reset-at-splice Data2 daily: both pendings land on one bar -> one reset (repeat call on the same bar is still that reset)");
		}
		// 7. host pins
		{
			string root = null;
			for (var di = new DirectoryInfo(AppContext.BaseDirectory); di != null && root == null; di = di.Parent) if (File.Exists(Path.Combine(di.FullName, "_CustomSignal.Strategy.CS"))) root = di.FullName;
			Func<string, string> rd = f => root == null ? "" : Regex.Replace(File.ReadAllText(Path.Combine(root, f)), @"//[^\n]*", "");
			string cs = rd("_CustomSignal.Strategy.CS"), vp = rd("_VolumeProfileBase.Strategy.CS"), vpf = rd("_VolumeProfileFuturesBase.Strategy.CS");
			Check(Regex.IsMatch(cs, @"protected\s+bool\s+rollResetOn\(int\s+ds\)\s*\{\s*bool\s+ev\s*=\s*rollOn\(ds\),\s*late\s*=\s*rollSpliceResetOn\(ds\);\s*return\s+ev\s*\|\|\s*late;"), "host: rollResetOn = rollOn | late splice, both evaluated");
			Check(Regex.IsMatch(cs, @"if\s*\(m_spliceLoc\.ResetsAgain\s*&&\s*bar\s*!=\s*m_rollDet\.LastRollBar\)") && cs.Contains("_RollDetector.ResetAtSpliceLine(m_spliceLoc)"), "host: late splice sets the re-reset and journals reset-at-splice");
			Check(Regex.IsMatch(cs, @"if\s*\(rollResetOn\(ds\)\)\s*pa\.OnRoll") && Regex.IsMatch(cs, @"rollResetOn\(pa\.config\.dataStream\)\)") && Regex.IsMatch(cs, @"rollBar\s*=\s*ago\s*==\s*0\s*&&\s*rollResetOn\(ds\)")
				&& Regex.IsMatch(cs, @"bool\s+roll\s*=\s*rollResetOn\(AvwapLimits\.dataStream\);") && Regex.IsMatch(cs, @"rollResetOn\(PriceActionConfig\.dataStream\)\)\s*m_pa\.OnRoll"), "host: PA hosts, AVWAP, PA and the ring roll flag use rollResetOn");
			Check(Regex.IsMatch(vp, @"internal\s+bool\s+vwapRollOn\(int\s+stream\)\s*\{\s*return\s+rollResetOn\(stream\);") && Regex.IsMatch(vp, @"bool\s+roll\s*=\s*rollResetOn\(WeeklyProfiles\.dataStream\);")
				&& Regex.IsMatch(vpf, @"IList<int>\s+rolls\s*=\s*ResetBars;"), "host: VWAP, weekly profiles and profileCrossesRoll use the reset (roll + late splice) bars");
			Check(Regex.IsMatch(cs, @"stepRollWindow|_RollWindow") ? !Regex.IsMatch(cs, @"RollWindow[^;\n]*rollResetOn") : true, "host: the roll window does not follow the re-reset");
		}
	}
}
