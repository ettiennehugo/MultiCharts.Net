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
				if (xmas.DayOfWeek == DayOfWeek.Saturday && d == xmas.AddDays(-1))
				{
					// M8: the 2021 archive is a full Globex close, not the 12:15 CT halt. Only this date (Ettienne 2026-11-10).
					if (d.Year == 2021) return closed("Christmas (observed Fri)");
					return halt("Christmas (observed Fri)", EarlyCloseEt);
				}
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

			/// <summary>
			/// M8 product-group rows for 2016-2026 (128 dates). Equity production days stay on <see cref="Get"/>;
			/// Rates, Fx, Energy and Metals use this table. Times in the tokens are CT; the Day struct stores ET (CT+1h),
			/// the same convention as HolidayHaltEt. Confidence: A archived CME, S secondary, I inferred.
			/// The per-row source_url column was not in the file delivered to this workspace. The status, time and
			/// confidence letters are Analyst's table (SHA-256 86ba5eddbec800186918319f927e656d51e378e90a975e39d612b3f2ce2522aa).
			/// Class sources: A = CME holiday-calendar archives and notices (Wayback copies of cmegroup.com 2019/2021 zips,
			/// 2022 MLK xls, Thanksgiving 2023 PDF, SER-8289, SER-9499R, the 2025 day-of-mourning PDF, Good Friday 2026 settlement PDF).
			/// S = IQFeed, Ironbeam, Paragon, helloweekends, marketcalendars. I = the 2022+ pattern on dates CME had not published.
			/// 2026-11-26, 2026-11-27 and 2026-12-24 are provisional.
			/// </summary>
			public static class Product
			{
				public enum EGroup { Equity = 0, Rates = 1, Fx = 2, Energy = 3, Metals = 4 }
				public const int FirstYear = 2016, LastYear = 2026;
				public const string TableSha256 = "86ba5eddbec800186918319f927e656d51e378e90a975e39d612b3f2ce2522aa";
				public static readonly string Table = "16-01-01 NY A C C C C C;16-01-18 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;16-02-15 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;16-03-25 GF A C C C C C;16-05-30 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;16-07-04 J4 A H12:00 H12:00 H12:00 H12:00 H12:00;16-09-05 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;16-11-24 TG A H12:00 H12:00 H12:00 H12:00 H12:00;16-11-25 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;16-12-26 XMo A C C C C C;17-01-02 NYo A C C C C C;17-01-16 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;17-02-20 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;17-04-14 GF A C C C C C;17-05-29 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;17-07-03 J3 A E12:15 N N N N;17-07-04 J4 A H12:00 H12:00 H12:00 H12:00 H12:00;17-09-04 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;17-11-23 TG A H12:00 H12:00 H12:00 H12:00 H12:00;17-11-24 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;17-12-25 XM A C C C C C;18-01-01 NY A C C C C C;18-01-15 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;18-02-19 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;18-03-30 GF A C C C C C;18-05-28 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;18-07-03 J3 A E12:15 N N N N;18-07-04 J4 A H12:00 H12:00 H12:00 H12:00 H12:00;18-09-03 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;18-11-22 TG A H12:00 H12:00 H12:00 H12:00 H12:00;18-11-23 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;18-12-05 BUSH A A08:30 C N N N;18-12-24 XE A E12:15 E12:15 E12:15 E12:45 E12:45;18-12-25 XM A C C C C C;19-01-01 NY A C C C C C;19-01-21 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;19-02-18 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;19-04-19 GF A C C C C C;19-05-27 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;19-07-03 J3 A E12:15 N N N N;19-07-04 J4 A H12:00 H12:00 H12:00 H12:00 H12:00;19-09-02 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;19-11-28 TG A H12:00 H12:00 H12:00 H12:00 H12:00;19-11-29 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;19-12-24 XE A E12:15 E12:15 E12:15 E12:45 E12:45;19-12-25 XM A C C C C C;20-01-01 NY A C C C C C;20-01-20 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;20-02-17 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;20-04-10 GF A C C C C C;20-05-25 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;20-07-03 J4o A F12:00 F12:00 F12:00 F12:00 F12:00;20-09-07 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;20-11-26 TG A H12:00 H12:00 H12:00 H12:00 H12:00;20-11-27 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;20-12-24 XE A E12:15 E12:15 E12:15 E12:45 E12:45;20-12-25 XM A C C C C C;21-01-01 NY A C C C C C;21-01-18 MLK A H12:00 H12:00 H12:00 H12:00 H12:00;21-02-15 PRES A H12:00 H12:00 H12:00 H12:00 H12:00;21-04-02 GF A E08:15 E10:15 E10:15 C C;21-05-31 MEM A H12:00 H12:00 H12:00 H12:00 H12:00;21-07-05 J4o A H12:00 H12:00 H12:00 H12:00 H12:00;21-09-06 LAB A H12:00 H12:00 H12:00 H12:00 H12:00;21-11-25 TG A H12:00 H12:00 H12:00 H12:00 H12:00;21-11-26 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;21-12-24 XMo A C C C C C;22-01-17 MLK A H12:00 H12:00 X H13:30 H13:30;22-02-21 PRES A H12:00 H12:00 X H13:30 H13:30;22-04-15 GF A C C C C C;22-05-30 MEM A H12:00 H12:00 X H13:30 H13:30;22-06-20 JTo A H12:00 H12:00 X H13:30 H13:30;22-07-04 J4 A H12:00 H12:00 X H13:30 H13:30;22-09-05 LAB A H12:00 H12:00 X H13:30 H13:30;22-11-24 TG A H12:00 H12:00 X H13:30 H13:30;22-11-25 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;22-12-26 XMo A C C C C C;23-01-02 NYo A C C C C C;23-01-16 MLK I H12:00 H12:00 X H13:30 H13:30;23-02-20 PRES A H12:00 H12:00 X H13:30 H13:30;23-04-07 GF S E08:15 E10:15 E10:15 C C;23-05-29 MEM A H12:00 H12:00 X H13:30 H13:30;23-06-19 JT A H12:00 H12:00 X H13:30 H13:30;23-07-03 J3 A E12:15 N N N N;23-07-04 J4 A H12:00 H12:00 X H13:30 H13:30;23-09-04 LAB A H12:00 H12:00 X H13:30 H13:30;23-11-23 TG A H12:00 H12:00 X H13:30 H13:30;23-11-24 TG+1 A E12:15 E12:15 E12:15 E12:45 E12:45;23-12-25 XM A C C C C C;24-01-01 NY A C C C C C;24-01-15 MLK S H12:00 H12:00 X H13:30 H13:30;24-02-19 PRES I H12:00 H12:00 X H13:30 H13:30;24-03-29 GF S C C C C C;24-05-27 MEM I H12:00 H12:00 X H13:30 H13:30;24-06-19 JT I H12:00 H12:00 X H13:30 H13:30;24-07-03 J3 I E12:15 N N N N;24-07-04 J4 I H12:00 H12:00 X H13:30 H13:30;24-09-02 LAB I H12:00 H12:00 X H13:30 H13:30;24-11-28 TG S H12:00 H12:00 X H13:30 H13:30;24-11-29 TG+1 S E12:15 E12:15 E13:45 E13:45 E13:45;24-12-24 XE S E12:15 E12:15 E12:45 E12:45 E12:45;24-12-25 XM S C C C C C;25-01-01 NY S C C C C C;25-01-09 CARTER A A08:30 E12:15 N N N;25-01-20 MLK I H12:00 H12:00 X H13:30 H13:30;25-02-17 PRES I H12:00 H12:00 X H13:30 H13:30;25-04-18 GF S C C C C C;25-05-26 MEM S H12:00 H12:00 X H13:30 H13:30;25-06-19 JT S H12:00 H12:00 X H13:30 H13:30;25-07-03 J3 S E12:15 N N N N;25-07-04 J4 S F12:00 F12:00 F12:00 F12:00 F12:00;25-09-01 LAB I H12:00 H12:00 X H13:30 H13:30;25-11-27 TG S H12:00 H12:00 X H13:30 H13:30;25-11-28 TG+1 S E12:15 E12:15 E13:45 E13:45 E13:45;25-12-24 XE S E12:15 E12:15 E12:45 E12:45 E12:45;25-12-25 XM S C C C C C;26-01-01 NY S C C C C C;26-01-19 MLK S H12:00 H12:00 X H13:30 H13:30;26-02-16 PRES S H12:00 H12:00 X H13:30 H13:30;26-04-03 GF S E08:15 E10:15 E10:15 C C;26-05-25 MEM S H12:00 H12:00 X H13:30 H13:30;26-06-19 JT S F12:00 F12:00 F12:00 F12:00 F12:00;26-07-03 J4o S F12:00 F12:00 F12:00 F12:00 F12:00;26-09-07 LAB S H12:00 H12:00 X H13:30 H13:30;26-11-26 TG S H12:00 H12:00 X H13:30 H13:30;26-11-27 TG+1 S E12:15 E12:15 E13:45 E13:45 E13:45;26-12-24 XE I E12:15 E12:15 E12:45 E12:45 E12:45;26-12-25 XM S C C C C C";

				static System.Collections.Generic.Dictionary<int, string[]> s_codes;
				static System.Collections.Generic.Dictionary<int, char> s_conf;
				static System.Collections.Generic.Dictionary<int, string> s_holiday;

				public static bool Covers(DateTime d) { return d.Year >= FirstYear && d.Year <= LastYear; }
				public static int Rows { get { Ensure(); return s_codes.Count; } }

				public static char Confidence(DateTime d)
				{
					Ensure();
					char c;
					return s_conf.TryGetValue(Key(d), out c) ? c : '\0';
				}

				public static bool Provisional(DateTime d)
				{
					return d == new DateTime(2026, 11, 26) || d == new DateTime(2026, 11, 27) || d == new DateTime(2026, 12, 24);
				}

				/// <summary>Raw token for the group (C, H12:00, E12:15, F12:00, A08:30, N, X), or N when the date is not a listed holiday.</summary>
				public static string Code(DateTime d, EGroup g)
				{
					Ensure();
					string[] codes;
					if (!s_codes.TryGetValue(Key(d), out codes)) return "N";
					return codes[(int)g];
				}

				public static Day Of(DateTime d, EGroup g)
				{
					Ensure();
					int key = Key(d);
					string[] codes;
					if (!s_codes.TryGetValue(key, out codes)) return new Day();
					string name = s_holiday[key];
					if (Provisional(d)) name = name + " (provisional)";
					return FromToken(codes[(int)g], name);
				}

				public static bool IsBusinessDay(DateTime d, EGroup g)
				{
					if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
					return !Of(d, g).Closed;
				}

				static int Key(DateTime d) { return d.Year * 10000 + d.Month * 100 + d.Day; }

				static Day FromToken(string token, string name)
				{
					if (token == "C") return closed(name);
					if (token == "N" || token == "X") return new Day();
					int hh = int.Parse(token.Substring(1, 2), System.Globalization.CultureInfo.InvariantCulture);
					int mm = int.Parse(token.Substring(4, 2), System.Globalization.CultureInfo.InvariantCulture);
					var et = new TimeSpan(hh + 1, mm, 0);
					if (token[0] == 'E') return new Day { HaltEt = et, RthCloseEt = et, Name = name };
					string label = token[0] == 'F' ? name + " (close, shut until Sunday)" : name;
					return new Day { NoRth = true, HaltEt = et, Name = label };
				}

				static void Ensure()
				{
					if (s_codes != null) return;
					s_codes = new System.Collections.Generic.Dictionary<int, string[]>();
					s_conf = new System.Collections.Generic.Dictionary<int, char>();
					s_holiday = new System.Collections.Generic.Dictionary<int, string>();
					foreach (string line in Table.Split(';'))
					{
						string[] p = line.Split(' ');
						string[] ymd = p[0].Split('-');
						int y = 2000 + int.Parse(ymd[0], System.Globalization.CultureInfo.InvariantCulture);
						int m = int.Parse(ymd[1], System.Globalization.CultureInfo.InvariantCulture);
						int day = int.Parse(ymd[2], System.Globalization.CultureInfo.InvariantCulture);
						int key = y * 10000 + m * 100 + day;
						s_codes[key] = new[] { p[3], p[4], p[5], p[6], p[7] };
						s_conf[key] = p[2][0];
						s_holiday[key] = HolidayName(p[1]);
					}
				}

				static string HolidayName(string code)
				{
					switch (code)
					{
						case "NY": return "New Year's Day";
						case "NYo": return "New Year's Day (observed)";
						case "MLK": return "Martin Luther King Jr. Day";
						case "PRES": return "Presidents Day";
						case "GF": return "Good Friday";
						case "MEM": return "Memorial Day";
						case "JT": return "Juneteenth";
						case "JTo": return "Juneteenth (observed)";
						case "J3": return "Independence Day eve";
						case "J4": return "Independence Day";
						case "J4o": return "Independence Day (observed)";
						case "LAB": return "Labor Day";
						case "TG": return "Thanksgiving";
						case "TG+1": return "Day after Thanksgiving";
						case "XE": return "Christmas Eve";
						case "XM": return "Christmas";
						case "XMo": return "Christmas (observed)";
						case "BUSH": return "Day of mourning (Bush)";
						case "CARTER": return "Day of mourning (Carter)";
						default: return code;
					}
				}
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
