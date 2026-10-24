using System;
using System.Collections.Generic;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPF-6: CME Globex calendar for US equity-index futures (ES/MES/NQ/MNQ, CME/CBOT equity products), keyed on the
	/// Globex trade date. isHoliday = full Globex closure. Halts and early closes are read from Rules.Get (the futures
	/// base uses Rules directly, so this function object is optional). MultiCharts holiday lists are not readable from
	/// code, so this list is maintained here (Rev 2.1).
	/// </summary>
	public sealed class _CmeHolidays : _Holidays
	{
		//types
		// <calendar-rules cme>
		/// <summary>
		/// CME Globex equity-index holiday schedule. All times US Eastern; dates are Globex trade dates (the evening session
		/// that opens at 18:00 ET belongs to the next date). Sources: CME Group holiday calendar / Globex holiday schedules
		/// (cmegroup.com/trading-hours.html), Good Friday notices, SER-9499R (Carter day of mourning). Rule summary:
		/// * Full closure: New Year's Day and Christmas (Sunday → observed Monday, e.g. Mon 26 Dec 2022, Mon 2 Jan 2023);
		///   Good Friday, except years with an abbreviated session (NFP released that day).
		/// * Saturday holidays: New Year (Sat) is not observed; Christmas (Sat) → Fri Dec 24 is a no-RTH day with the
		///   12:15 CT (13:15 ET) halt [verify 2021 notice]; July 4 / Juneteenth (Sat) → Friday holiday halt.
		/// * Holiday halt 12:00 CT (13:00 ET), no RTH: MLK, Presidents, Memorial, Juneteenth (from 2022), Independence,
		///   Labor, Thanksgiving.
		/// * Early close 12:15 CT (13:15 ET) for Globex and RTH: day after Thanksgiving, Dec 24 and Jul 3 when Mon-Thu.
		/// * Abbreviated sessions: Good Friday 2021-04-02, 2023-04-07, 2026-04-03 close 08:15 CT (09:15 ET);
		///   days of mourning 2018-12-05 and 2025-01-09 close 08:30 CT (09:30 ET), no RTH.
		/// Weekends are not described here (the session clock handles them).
		/// </summary>
		public static class Rules
		{
			public struct Day
			{
				public bool Closed;          // no Globex session for this trade date
				public bool NoRth;           // Globex open (abbreviated) but no cash/RTH session
				public TimeSpan? HaltEt;     // Globex halts for the rest of the trade date from this time (ET)
				public TimeSpan? RthCloseEt; // RTH ends early at this time (ET)
				public string Name;
			}

			public static readonly TimeSpan HolidayHaltEt = new TimeSpan(13, 0, 0);     // 12:00 CT
			public static readonly TimeSpan EarlyCloseEt = new TimeSpan(13, 15, 0);     // 12:15 CT
			public static readonly TimeSpan GoodFridayCloseEt = new TimeSpan(9, 15, 0); // 08:15 CT
			public static readonly TimeSpan MourningCloseEt = new TimeSpan(9, 30, 0);   // 08:30 CT

			// Good Fridays with an abbreviated equity session (NFP released). Source: CME Good Friday holiday schedules
			// 2021 (Cannon/CME notice), 2023 (cmegroup.com/files/good-friday.pdf), 2026 (CME clearing advisory, SIFMA note).
			private static readonly int[] s_goodFridayOpenYears = { 2021, 2023, 2026 };

			// Days of mourning: equity products closed after the overnight session (SER-9499R for 2025; 2018 same policy, verify).
			private static readonly DateTime[] s_mourning = { new DateTime(2018, 12, 5), new DateTime(2025, 1, 9) };

			// Historical unscheduled closures (R2.1-d amendment 2026-10-23), CME equity-index futures status from CME notices:
			//  2001-09-11 equity Globex suspended 08:30 CT; 09-12..09-14 equity index closed, reopened Mon 09-17 (CME press
			//             releases 2001-09-11/13, as reprinted by Mondo Visione; no cmegroup.com original located;
			//             corroborated by the CFTC 9/11 report: exchanges closed 09-12, stock-index futures closed until 09-17,
			//             https://www.cftc.gov/sites/default/files/files/opa/opa9-11reportprintversion.pdf)
			//  2004-06-11 Reagan: CME closed, Globex abbreviated session to 08:00 CT (CME release 2004-06-07, investor.cmegroup.com)
			//  2007-01-02 Ford: equity index closed, Globex equity halted 08:15 CT (CME release 2006-12-29; CFTC filing CME 06-120)
			//  2012-10-29/30 Sandy: equity futures halted 08:15 CT both days, Globex reopened 17:00 CT (CME 12-363, Chadv12-464)
			private static readonly DateTime[] s_histClosed = { new DateTime(2001, 9, 12), new DateTime(2001, 9, 13), new DateTime(2001, 9, 14) };
			private static readonly DateTime[] s_histHalt = { new DateTime(2001, 9, 11), new DateTime(2004, 6, 11), new DateTime(2007, 1, 2), new DateTime(2012, 10, 29), new DateTime(2012, 10, 30) };
			private static readonly TimeSpan[] s_histHaltEt = { new TimeSpan(9, 30, 0), new TimeSpan(9, 0, 0), new TimeSpan(9, 15, 0), new TimeSpan(9, 15, 0), new TimeSpan(9, 15, 0) };
			private static readonly string[] s_histName = { "September 11", "Day of mourning (Reagan)", "Day of mourning (Ford)", "Hurricane Sandy", "Hurricane Sandy" };

			public static Day Get(DateTime date)
			{
				DateTime d = date.Date;
				int y = d.Year;
				Day r = new Day();
				if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return r;

				// full closures
				DateTime ny = new DateTime(y, 1, 1);
				if (ny.DayOfWeek != DayOfWeek.Saturday && d == SundayToMonday(ny)) return closed("New Year's Day");
				DateTime xmas = new DateTime(y, 12, 25);
				if (xmas.DayOfWeek != DayOfWeek.Saturday && d == SundayToMonday(xmas)) return closed("Christmas");
				if (xmas.DayOfWeek == DayOfWeek.Saturday && d == xmas.AddDays(-1)) return halt("Christmas (observed Fri)", EarlyCloseEt);
				DateTime goodFriday = Easter(y).AddDays(-2);
				if (d == goodFriday)
					return Array.IndexOf(s_goodFridayOpenYears, y) >= 0 ? halt("Good Friday (abbreviated)", GoodFridayCloseEt) : closed("Good Friday");
				if (Array.IndexOf(s_mourning, d) >= 0) return halt("Day of mourning", MourningCloseEt);
				if (Array.IndexOf(s_histClosed, d) >= 0) return closed("September 11");
				int hi = Array.IndexOf(s_histHalt, d);
				if (hi >= 0) return halt(s_histName[hi], s_histHaltEt[hi]);

				// holiday halts (no RTH)
				if (d == NthWeekday(y, 1, DayOfWeek.Monday, 3)) return halt("Martin Luther King Jr. Day", HolidayHaltEt);
				if (d == NthWeekday(y, 2, DayOfWeek.Monday, 3)) return halt("Presidents Day", HolidayHaltEt);
				if (d == LastWeekday(y, 5, DayOfWeek.Monday)) return halt("Memorial Day", HolidayHaltEt);
				if (y >= 2022 && d == Observed(new DateTime(y, 6, 19))) return halt("Juneteenth", HolidayHaltEt);
				if (d == Observed(new DateTime(y, 7, 4))) return halt("Independence Day", HolidayHaltEt);
				if (d == NthWeekday(y, 9, DayOfWeek.Monday, 1)) return halt("Labor Day", HolidayHaltEt);
				DateTime thanksgiving = NthWeekday(y, 11, DayOfWeek.Thursday, 4);
				if (d == thanksgiving) return halt("Thanksgiving", HolidayHaltEt);

				// early closes (RTH still trades)
				if (d == thanksgiving.AddDays(1)) return early("Day after Thanksgiving");
				if (d.Month == 12 && d.Day == 24 && d.DayOfWeek <= DayOfWeek.Thursday) return early("Christmas Eve");
				if (d.Month == 7 && d.Day == 3 && d.DayOfWeek <= DayOfWeek.Thursday) return early("Independence Day eve");
				return r;
			}

			/// <summary>Full Globex closures (weekdays) of a year, for the _Holidays list.</summary>
			public static List<DateTime> ClosedDates(int year)
			{
				var list = new List<DateTime>();
				for (DateTime d = new DateTime(year, 1, 1); d.Year == year; d = d.AddDays(1))
					if (Get(d).Closed) list.Add(d);
				return list;
			}

			private static Day closed(string name) { return new Day { Closed = true, NoRth = true, Name = name }; }
			private static Day halt(string name, TimeSpan at) { return new Day { NoRth = true, HaltEt = at, Name = name }; }
			private static Day early(string name) { return new Day { HaltEt = EarlyCloseEt, RthCloseEt = EarlyCloseEt, Name = name }; }

			public static DateTime SundayToMonday(DateTime d) { return d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d; }

			public static DateTime Observed(DateTime d)
			{
				if (d.DayOfWeek == DayOfWeek.Saturday) return d.AddDays(-1);
				return SundayToMonday(d);
			}

			public static DateTime NthWeekday(int year, int month, DayOfWeek day, int n)
			{
				DateTime first = new DateTime(year, month, 1);
				int offset = ((int)day - (int)first.DayOfWeek + 7) % 7;
				return first.AddDays(offset + 7 * (n - 1));
			}

			public static DateTime LastWeekday(int year, int month, DayOfWeek day)
			{
				DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
				int back = ((int)last.DayOfWeek - (int)day + 7) % 7;
				return last.AddDays(-back);
			}

			public static DateTime Easter(int year)   // Anonymous Gregorian algorithm
			{
				int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
				int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
				int m = (a + 11 * h + 22 * l) / 451;
				int month = (h + l - 7 * m + 114) / 31, day = ((h + l - 7 * m + 114) % 31) + 1;
				return new DateTime(year, month, day);
			}
		}
		// </calendar-rules cme>

		//interface methods
		public _CmeHolidays(CStudyControl _master) : base(_master) { }
		public _CmeHolidays(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void refreshHolidays(int year)
		{
			m_holidays.AddRange(Rules.ClosedDates(year));
		}
	}
}
