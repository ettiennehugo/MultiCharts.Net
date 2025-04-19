using System;
using System.Collections.Generic;

namespace PowerLanguage
{
	namespace Function
	{
		public sealed class USHolidays : FunctionSimple<int>
		{
			//Types
			private enum WeekOfMonth
			{
				First = 1,
				Second = 2,
				Third = 3,
				Fourth = 4,
				Last = 5,
			}

			//Properties
			public List<DateTime> Holidays { get { return _holidays; } }

			//Attributes
			private List<DateTime> _holidays;

			//Methods
			public USHolidays(CStudyControl _master) : base(_master) { }
			public USHolidays(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create() { }

			protected override void StartCalc()
			{
				_holidays = new List<DateTime>();
			}

			protected override int CalcBar()
			{
				//refresh the set of holidays if it's empty or the year changes
				//NOTE: We actually need to keep track of holidays for the current and previous year since when we get close to
				//      year boundaries the calculations might need the holidays for both years.
				if (_holidays.Count == 0 || (Bars.CurrentBar > 2 && Bars.Time[1].Year != Bars.Time[0].Year))
				{
					_holidays.Clear();
					if (Bars.Time[1].Year != Bars.Time[0].Year) refreshHolidays(Bars.Time[1].Year);
					refreshHolidays(Bars.Time[0].Year);
				}

				//nothing to do, get holidays using Holidays get property
				return 0;
			}

			//Refresh the set of US holidays for a given year. 
			// https://canro91.github.io/2018/10/22/YeahTomorrowIsAHoliday/
			// https://stackoverflow.com/questions/18326446/how-to-calculate-holidays-for-the-usa
			// http://www.usa.gov/citizens/holidays.shtml      
			// http://archive.opm.gov/operating_status_schedules/fedhol/2013.asp
			private void refreshHolidays(int year)
			{
				//omit new years if it falls on a Saturday in which case it would be moved to the previous year
				//(see code below that add's that), if it falls on a Sunday it will be moved to the Monda
				DateTime newYears = new DateTime(year, 1, 1);
				if (newYears.DayOfWeek != DayOfWeek.Saturday) _holidays.Add(newYears);

				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 1, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Martin Luther King Jr day, third Monday in Jan
				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 2, 1), DayOfWeek.Monday, WeekOfMonth.Third)); //Washington's birthday, third Monday in Feb
				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 5, 1), DayOfWeek.Monday, WeekOfMonth.Last)); //Memorial day, last Monday in May
				_holidays.Add(new DateTime(year, 6, 19)); //Juneteenth National Independance day, June 19th
				_holidays.Add(new DateTime(year, 7, 4)); //Independance day, July 4th
				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 9, 1), DayOfWeek.Monday, WeekOfMonth.First)); //Labor day, first Monday of September
				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 10, 1), DayOfWeek.Monday, WeekOfMonth.Second)); //Columnbus day, second Monday of October
				_holidays.Add(new DateTime(year, 11, 11)); //Veterans day, November 11
				_holidays.Add(getNthDayOfNthWeek(new DateTime(year, 11, 1), DayOfWeek.Thursday, WeekOfMonth.Fourth)); //Thanksgiving day, fourth Thursday of November
				_holidays.Add(new DateTime(year, 12, 25)); //Christmas day, December 25

				//next year's new years holiday can actually fall in this year if it falls on a Saturday, add it to
				//this year if that's the case
				newYears = new DateTime(year + 1, 1, 1);
				if (newYears.DayOfWeek == DayOfWeek.Saturday) _holidays.Add(newYears.AddDays(-1)); //add next year's new years on Friday of this year

				//Saturday holidays move to Friday and Sunday to Monday
				for (int i = 0; i < _holidays.Count; i++)
				{
					DateTime holiday = _holidays[i];
					if (holiday.DayOfWeek == DayOfWeek.Saturday)
						_holidays[i] = holiday.AddDays(-1);
					else if (holiday.DayOfWeek == DayOfWeek.Sunday)
						_holidays[i] = holiday.AddDays(1);
				}
			}

			//specify which day of which week of a month and this function will get the date
			//this function uses the month and year of the date provided
			private DateTime getNthDayOfNthWeek(DateTime date, DayOfWeek dayOfWeek, WeekOfMonth weekOfMonth)
			{
				DateTime firstOfMonth = new DateTime(date.Year, date.Month, 1); //get first date of month
				DateTime result = firstOfMonth.AddDays(6 - (double)firstOfMonth.AddDays(-((int)dayOfWeek + 1)).DayOfWeek); //get first dayOfWeek of month
				result = result.AddDays(((int)weekOfMonth - 1) * 7);  //get the correct week
				if (result >= firstOfMonth.AddMonths(1)) result = result.AddDays(-7);   //if day is past end of month then adjust backwards a week
				return result;
			}

		}
	}
}