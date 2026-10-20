using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Calendar for NYSE Ecxchange Holidays.
	/// The date rules live in the nested, MultiCharts-free Rules class so they can be unit tested outside MultiCharts
	/// (spec VPF-6, tests/CalendarTests).
	/// </summary>
	public sealed class _NyseHolidays : _Holidays
	{
		//constants


		//types
		// <calendar-rules nyse>
		/// <summary>
		/// NYSE full-day closures (spec VPF-6). Observed-date rules (NYSE rule 7.2): a Sunday holiday is observed on the
		/// Monday; a Saturday holiday on the Friday, EXCEPT New Year's Day (NYSE stays open on Fri Dec 31, e.g. 2021).
		/// Juneteenth from 2022. Special closures (national days of mourning) are listed per date with their source.
		/// </summary>
		public static class Rules
		{
			// Unscheduled full-day NYSE closures inside the backtest range (2017-2026).
			private static readonly DateTime[] s_special =
			{
				new DateTime(2018, 12, 5),   // National Day of Mourning, President G.H.W. Bush (NYSE closed)
				new DateTime(2025, 1, 9),    // National Day of Mourning, President J. Carter (NYSE closed)
			};

			public static List<DateTime> Holidays(int year)
			{
				var list = new List<DateTime>();
				DateTime newYears = new DateTime(year, 1, 1);
				if (newYears.DayOfWeek != DayOfWeek.Saturday) list.Add(Observed(newYears));   // Saturday: not observed

				list.Add(NthWeekday(year, 1, DayOfWeek.Monday, 3));    // Martin Luther King Jr. Day
				list.Add(NthWeekday(year, 2, DayOfWeek.Monday, 3));    // Washington's Birthday
				list.Add(Easter(year).AddDays(-2));                    // Good Friday
				list.Add(LastWeekday(year, 5, DayOfWeek.Monday));      // Memorial Day
				if (year >= 2022) list.Add(Observed(new DateTime(year, 6, 19)));   // Juneteenth (NYSE from 2022)
				list.Add(Observed(new DateTime(year, 7, 4)));          // Independence Day
				list.Add(NthWeekday(year, 9, DayOfWeek.Monday, 1));    // Labor Day
				list.Add(NthWeekday(year, 11, DayOfWeek.Thursday, 4)); // Thanksgiving
				list.Add(Observed(new DateTime(year, 12, 25)));        // Christmas
				foreach (DateTime d in s_special)
					if (d.Year == year) list.Add(d);
				return list;
			}

			/// <summary>Saturday → Friday, Sunday → Monday.</summary>
			public static DateTime Observed(DateTime holiday)
			{
				if (holiday.DayOfWeek == DayOfWeek.Saturday) return holiday.AddDays(-1);
				if (holiday.DayOfWeek == DayOfWeek.Sunday) return holiday.AddDays(1);
				return holiday;
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
		// </calendar-rules nyse>

		//properties


		//attribute


		//interface methods
		public _NyseHolidays(CStudyControl _master) : base(_master) { }
		public _NyseHolidays(CStudyControl _master, int _ds) : base(_master, _ds) { }

		//Refresh the set of US holidays for a given year.
		// https://canro91.github.io/2018/10/22/YeahTomorrowIsAHoliday/
		// https://stackoverflow.com/questions/18326446/how-to-calculate-holidays-for-the-usa
		// http://www.usa.gov/citizens/holidays.shtml
		// http://archive.opm.gov/operating_status_schedules/fedhol/2013.asp
		protected override void refreshHolidays(int year)
		{
			m_holidays.AddRange(Rules.Holidays(year));
		}
	}
}
