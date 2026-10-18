using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Base class used to compute holiday calendars for exchanges since Multicharts in their infinite wisdom
	/// does not expose the holiday calendars already defined in the Quote Manager.
	/// 
	/// NOTES:
	/// * You must call the function at least once in your calling signal/indicator to ensure the holiday list remains updated
	///   e.g. int _ = m_holidays.Value; - this forces CalcBar to be called.
	/// * Even though we store the list of holidays the function isHoliday makes sure that the list of holidays are accessed
	///   correctly via the DateOnly part of the DateTime since the holidays are stored with only the date part set (time part
	///   is zeroed, so comparing on it against a bar DateTime would never get a match).
	/// </summary>
	public abstract class _Holidays : FunctionSimple<int>
	{
		//constants


		//types
		protected enum WeekOfMonth
		{
			First = 1,
			Second = 2,
			Third = 3,
			Fourth = 4,
			Last = 5,
		}

		//properties


		//attribute
		protected List<DateTime> m_holidays;

		//interface methods
		public _Holidays(CStudyControl _master) : base(_master) { }
		public _Holidays(CStudyControl _master, int _ds) : base(_master, _ds) { }

		public bool isHoliday(DateTime dateTime)
		{
			//holidays are stored as DateOnly with zeroed time, so we only compare using the Date
			return m_holidays.Contains(dateTime.Date);
		}

		protected override void StartCalc()
		{
			m_holidays = new List<DateTime>();
		}

		protected override int CalcBar()
		{
			//refresh the set of holidays if it's empty or the year changes
			//NOTE: We actually need to keep track of holidays for the current and previous year since when we get close to
			//      year boundaries the calculations might need the holidays for both years.
			if (m_holidays.Count == 0 || (Bars.CurrentBar > 2 && Bars.Time[1].Year != Bars.Time[0].Year))
			{
				m_holidays.Clear();
				if (Bars.Time[1].Year != Bars.Time[0].Year) refreshHolidays(Bars.Time[1].Year);
				refreshHolidays(Bars.Time[0].Year);
			}

			//nothing to do, get holidays using Holidays get property
			return 0;
		}

		//methods
		/// <summary>
		/// Sub-classes redefine this method to refresh their calendar for a given year.
		/// </summary>
		/// <param name="year">Year for which to refresh the holidays.</param>
		protected abstract void refreshHolidays(int year);

		/// <summary>
		/// Specify which day of which week of a month and this function will get the date
		/// this function uses the month and year of the date provided.
		/// </summary>
		/// <param name="date">Specific date for which to get the adjusted holiday.</param>
		/// <param name="dayOfWeek">Day of the week if holiday falls on a specific day.</param>
		/// <param name="weekOfMonth">Specific week in the month if the holiday falls in a specific week.</param>
		/// <returns></returns>
		protected DateTime getNthDayOfNthWeek(DateTime date, DayOfWeek dayOfWeek, WeekOfMonth weekOfMonth)
		{
			DateTime firstOfMonth = new DateTime(date.Year, date.Month, 1); //get first date of month
			DateTime result = firstOfMonth.AddDays(6 - (double)firstOfMonth.AddDays(-((int)dayOfWeek + 1)).DayOfWeek); //get first dayOfWeek of month
			result = result.AddDays(((int)weekOfMonth - 1) * 7);  //get the correct week
			if (result >= firstOfMonth.AddMonths(1)) result = result.AddDays(-7);   //if day is past end of month then adjust backwards a week
			return result;
		}

	}
}