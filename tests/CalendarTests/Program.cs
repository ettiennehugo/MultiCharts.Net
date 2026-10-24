using System;
using System.Collections.Generic;
using Nyse = Extracted.Nyse.Rules;
using Cme = Extracted.Cme.Rules;

// Spec VPF-6 unit tests: known NYSE / CME Globex equity-index dates 2017-2026 (sources in _CmeHolidays.Function.CS).
static class Program
{
	static int s_fail, s_pass;

	static void Check(bool ok, string what)
	{
		if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); }
	}

	static DateTime D(int y, int m, int d) { return new DateTime(y, m, d); }
	static readonly TimeSpan H1300 = new TimeSpan(13, 0, 0), H1315 = new TimeSpan(13, 15, 0), H0915 = new TimeSpan(9, 15, 0), H0930 = new TimeSpan(9, 30, 0);

	static void Closed(DateTime d) { var r = Cme.Get(d); Check(r.Closed && r.NoRth, "CME closed " + d.ToString("yyyy-MM-dd") + " got " + Describe(r)); }
	static void Halt(DateTime d, TimeSpan at) { var r = Cme.Get(d); Check(!r.Closed && r.NoRth && r.HaltEt == at && r.RthCloseEt == null, "CME halt " + at + " " + d.ToString("yyyy-MM-dd") + " got " + Describe(r)); }
	static void Early(DateTime d) { var r = Cme.Get(d); Check(!r.Closed && !r.NoRth && r.HaltEt == H1315 && r.RthCloseEt == H1315, "CME early close " + d.ToString("yyyy-MM-dd") + " got " + Describe(r)); }
	static void Normal(DateTime d) { var r = Cme.Get(d); Check(!r.Closed && !r.NoRth && r.HaltEt == null && r.RthCloseEt == null, "CME normal " + d.ToString("yyyy-MM-dd") + " got " + Describe(r)); }
	static string Describe(Cme.Day r) { return "closed=" + r.Closed + " noRth=" + r.NoRth + " halt=" + r.HaltEt + " rthClose=" + r.RthCloseEt + " (" + r.Name + ")"; }

	static void NyseHoliday(DateTime d, bool expected)
	{
		Check(Nyse.Holidays(d.Year).Contains(d) == expected, "NYSE " + d.ToString("yyyy-MM-dd ddd") + " holiday expected " + expected);
	}

	// verbatim port of the old _NyseHolidays.refreshHolidays / _Holidays.getNthDayOfNthWeek
	static DateTime OldNth(DateTime date, DayOfWeek dayOfWeek, int weekOfMonth)
	{
		DateTime firstOfMonth = new DateTime(date.Year, date.Month, 1);
		DateTime result = firstOfMonth.AddDays(6 - (double)firstOfMonth.AddDays(-((int)dayOfWeek + 1)).DayOfWeek);
		result = result.AddDays((weekOfMonth - 1) * 7);
		if (result >= firstOfMonth.AddMonths(1)) result = result.AddDays(-7);
		return result;
	}

	static HashSet<DateTime> OldNyse(int year)
	{
		var h = new List<DateTime>();
		DateTime newYears = new DateTime(year, 1, 1);
		if (newYears.DayOfWeek != DayOfWeek.Saturday) h.Add(newYears);
		h.Add(OldNth(new DateTime(year, 1, 1), DayOfWeek.Monday, 3));
		h.Add(OldNth(new DateTime(year, 2, 1), DayOfWeek.Monday, 3));
		h.Add(Nyse.Easter(year).AddDays(-2));
		h.Add(OldNth(new DateTime(year, 5, 1), DayOfWeek.Monday, 5));
		if (year >= 2022) h.Add(new DateTime(year, 6, 19));
		h.Add(new DateTime(year, 7, 4));
		h.Add(OldNth(new DateTime(year, 9, 1), DayOfWeek.Monday, 1));
		h.Add(OldNth(new DateTime(year, 11, 1), DayOfWeek.Thursday, 4));
		h.Add(new DateTime(year, 12, 25));
		for (int i = 0; i < h.Count; i++)
		{
			if (h[i].DayOfWeek == DayOfWeek.Saturday) h[i] = h[i].AddDays(-1);
			else if (h[i].DayOfWeek == DayOfWeek.Sunday) h[i] = h[i].AddDays(1);
		}
		return new HashSet<DateTime>(h);
	}

	static int Main()
	{
		// --- Sunday / Saturday observance (Rev 2.1 known case: Sun 25 Dec 2022 -> CME closed Mon 26 Dec) ---
		Closed(D(2022, 12, 26)); Normal(D(2022, 12, 23)); Closed(D(2023, 1, 2)); Closed(D(2017, 1, 2));
		NyseHoliday(D(2022, 12, 26), true); NyseHoliday(D(2023, 1, 2), true); NyseHoliday(D(2017, 1, 2), true);
		// New Year on Saturday (2022): NYSE and CME open on Fri 31 Dec 2021
		NyseHoliday(D(2021, 12, 31), false); Normal(D(2021, 12, 31));
		// Christmas on Saturday (2021): NYSE closed Fri 24 Dec; CME no RTH, 13:15 ET halt
		NyseHoliday(D(2021, 12, 24), true); Halt(D(2021, 12, 24), H1315);
		// July 4 on Saturday (2020, 2026) -> Friday; on Sunday (2021) -> Monday
		NyseHoliday(D(2020, 7, 3), true); Halt(D(2020, 7, 3), H1300);
		NyseHoliday(D(2026, 7, 3), true); Halt(D(2026, 7, 3), H1300);
		NyseHoliday(D(2021, 7, 5), true); Halt(D(2021, 7, 5), H1300);
		// Juneteenth: not before 2022; Sunday 2022 -> Mon 20 Jun
		NyseHoliday(D(2021, 6, 18), false); Normal(D(2021, 6, 18));
		NyseHoliday(D(2022, 6, 20), true); Halt(D(2022, 6, 20), H1300); Halt(D(2026, 6, 19), H1300);

		// --- full closures ---
		Closed(D(2026, 1, 1)); Closed(D(2025, 12, 25)); Closed(D(2026, 12, 25)); Closed(D(2024, 12, 25));
		// Good Friday: closed, except abbreviated sessions (NFP) in 2021, 2023, 2026
		Closed(D(2024, 3, 29)); Closed(D(2025, 4, 18)); Closed(D(2022, 4, 15)); Closed(D(2019, 4, 19)); Closed(D(2017, 4, 14));
		Halt(D(2021, 4, 2), H0915); Halt(D(2023, 4, 7), H0915); Halt(D(2026, 4, 3), H0915);
		NyseHoliday(D(2023, 4, 7), true); NyseHoliday(D(2026, 4, 3), true);

		// --- holiday halts 13:00 ET ---
		Halt(D(2026, 1, 19), H1300); Halt(D(2026, 2, 16), H1300); Halt(D(2026, 5, 25), H1300); Halt(D(2026, 9, 7), H1300);
		Halt(D(2026, 11, 26), H1300); Halt(D(2024, 7, 4), H1300); Halt(D(2025, 11, 27), H1300); Halt(D(2017, 1, 16), H1300);

		// --- early closes 13:15 ET ---
		Early(D(2026, 11, 27)); Early(D(2025, 11, 28)); Early(D(2026, 12, 24)); Early(D(2024, 12, 24)); Early(D(2025, 12, 24));
		Early(D(2025, 7, 3)); Early(D(2024, 7, 3)); Early(D(2023, 7, 3)); Early(D(2019, 12, 24));
		Normal(D(2023, 12, 22)); // Dec 24 2023 is a Sunday: no early close on Friday

		// --- days of mourning ---
		Halt(D(2025, 1, 9), H0930); Halt(D(2018, 12, 5), H0930);
		NyseHoliday(D(2025, 1, 9), true); NyseHoliday(D(2018, 12, 5), true);

		// --- weekends and ordinary days ---
		Normal(D(2026, 10, 19)); Normal(D(2026, 10, 17)); Normal(D(2026, 3, 9));

		// --- invariants over 2017-2026 ---
		for (int y = 2017; y <= 2026; y++)
		{
			List<DateTime> nyse = Nyse.Holidays(y);
			foreach (DateTime h in nyse)
			{
				var r = Cme.Get(h);
				Check(h.DayOfWeek != DayOfWeek.Saturday && h.DayOfWeek != DayOfWeek.Sunday, "NYSE holiday on a weekend " + h.ToString("yyyy-MM-dd"));
				Check(r.NoRth, "NYSE holiday " + h.ToString("yyyy-MM-dd") + " must be a CME no-RTH day, got " + Describe(r));
			}
			for (DateTime d = D(y, 1, 1); d.Year == y; d = d.AddDays(1))
			{
				var r = Cme.Get(d);
				if (r.NoRth) Check(nyse.Contains(d), "CME no-RTH day " + d.ToString("yyyy-MM-dd") + " is not an NYSE holiday");
				if (r.Closed) Check(Cme.ClosedDates(y).Contains(d), "ClosedDates misses " + d.ToString("yyyy-MM-dd"));
			}
			Check(nyse.Count == new HashSet<DateTime>(nyse).Count, "NYSE duplicate dates in " + y);
		}

		// --- regression: the refactored NYSE rules equal the pre-VPF-6 _NyseHolidays.refreshHolidays (plus the unscheduled
		// one-off closures: mourning days and the historical 2001/2004/2007/2012 closures, R2.1-d amendment 2026-10-23) ---
		for (int y = 2000; y <= 2035; y++)
		{
			var now = new HashSet<DateTime>(Nyse.Holidays(y));
			foreach (var x in new[] { D(2001, 9, 11), D(2001, 9, 12), D(2001, 9, 13), D(2001, 9, 14), D(2004, 6, 11), D(2007, 1, 2), D(2012, 10, 29), D(2012, 10, 30), D(2018, 12, 5), D(2025, 1, 9) })
				Check(now.Remove(x) || x.Year != y, "special closure " + x.ToString("yyyy-MM-dd") + " in NYSE rules");
			Check(now.SetEquals(OldNyse(y)), "NYSE rules changed vs old implementation in " + y);
		}

		Console.WriteLine("calendar tests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}
}
