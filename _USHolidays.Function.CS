using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Calendar for US Fedaral Holidays.
	/// </summary>
	public sealed class _USHolidays : _Holidays
	{
		//constants


		//types


		//properties


		//attribute


		//interface methods
		public _USHolidays(CStudyControl _master) : base(_master) { }
		public _USHolidays(CStudyControl _master, int _ds) : base(_master, _ds) { }

		//Refresh the set of US holidays for a given year. 
		// https://canro91.github.io/2018/10/22/YeahTomorrowIsAHoliday/
		// https://stackoverflow.com/questions/18326446/how-to-calculate-holidays-for-the-usa
		// http://www.usa.gov/citizens/holidays.shtml      
		// http://archive.opm.gov/operating_status_schedules/fedhol/2013.asp
		protected override void refreshHolidays(int year)
		{
			//omit new years if it falls on a Saturday in which case it would be moved to the previous year
			//(see code below that add's that), if it falls on a Sunday it will be moved to the Monda
			DateTime newYears = new DateTime(year, 1, 1);
			if (newYears.DayOfWeek != DayOfWeek.Saturday) m_holidays.Add(newYears);

			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 1, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Martin Luther King Jr day, third Monday in Jan
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 2, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Washington's birthday, third Monday in Feb
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 5, 1), DayOfWeek.Monday, WeekOfMonth.Last)); //Memorial day, last Monday in May
			m_holidays.Add(new DateTime(year, 6, 19)); //Juneteenth National Independance day, June 19th
			m_holidays.Add(new DateTime(year, 7, 4)); //Independance day, July 4th
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 9, 1), DayOfWeek.Monday, WeekOfMonth.First)); //Labor day, first Monday of September
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 10, 1), DayOfWeek.Monday, WeekOfMonth.Second)); //Columnbus day, second Monday of October
			m_holidays.Add(new DateTime(year, 11, 11)); //Veterans day, November 11
			m_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 11, 1), DayOfWeek.Thursday, WeekOfMonth.Fourth)); //Thanksgiving day, fourth Thursday of November
			m_holidays.Add(new DateTime(year, 12, 25)); //Christmas day, December 25

			//next year's new years holiday can actually fall in this year if it falls on a Saturday, add it to
			//this year if that's the case
			newYears = new DateTime(year + 1, 1, 1);
			if (newYears.DayOfWeek == DayOfWeek.Saturday) m_holidays.Add(newYears.AddDays(-1)); //add next year's new years on Friday of this year

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


	}
}