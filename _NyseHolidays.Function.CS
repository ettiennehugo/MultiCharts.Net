using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Calendar for NYSE Ecxchange Holidays.
	/// </summary>
	public sealed class _NyseHolidays : _Holidays
	{
		//constants


		//types


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
			//omit new years if it falls on a Saturday: NYSE rule 7.2 does NOT observe it on the preceding Friday Dec 31
			//(e.g. NYSE was open on Fri 2021-12-31), if it falls on a Sunday it will be moved to the Monday
			DateTime newYears = new DateTime(year, 1, 1);
			if (newYears.DayOfWeek != DayOfWeek.Saturday) m_holidays.Add(newYears);

			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 1, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Martin Luther King Jr day, third Monday in Jan
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 2, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Washington's birthday, third Monday in Feb
			m_holidays.Add(easter(year).AddDays(-2));		//Good Friday
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 5, 1), DayOfWeek.Monday, WeekOfMonth.Last)); //Memorial day, last Monday in May
			if (year >= 2022) m_holidays.Add(new DateTime(year, 6, 19)); //Juneteenth National Independance day, June 19th (NYSE observes it from 2022)
			m_holidays.Add(new DateTime(year, 7, 4)); //Independance day, July 4th
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 9, 1), DayOfWeek.Monday, WeekOfMonth.First)); //Labor day, first Monday of September
			// No Columbus day
			// No Veterans day
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 11, 1), DayOfWeek.Thursday, WeekOfMonth.Fourth)); //Thanksgiving day, fourth Thursday of November
			m_holidays.Add(new DateTime(year, 12, 25)); //Christmas day, December 25

			//Saturday holidays move to Friday and Sunday to Monday
			for (int i = 0; i < m_holidays.Count; i++)
			{
				DateTime holiday = m_holidays[i];
				if (holiday.DayOfWeek == DayOfWeek.Saturday)
					m_holidays[i] = holiday.AddDays(-1);
				else if (holiday.DayOfWeek == DayOfWeek.Sunday)
					m_holidays[i] = holiday.AddDays(1);
			}
		}

		//methods
		private static DateTime easter(int year)   // Anonymous Gregorian algorithm
		{
			int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
			int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
			int m = (a + 11 * h + 22 * l) / 451;
			int month = (h + l - 7 * m + 114) / 31, day = ((h + l - 7 * m + 114) % 31) + 1;
			return new DateTime(year, month, day);
		}
	}
}