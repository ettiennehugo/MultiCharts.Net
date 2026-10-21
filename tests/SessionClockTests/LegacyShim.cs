using System;
using System.Collections.Generic;

namespace Legacy
{
	// Stand-ins for the MultiCharts-bound parts of the pre-VPF-7 futures base, so the verbatim legacy cores compile.
	public partial class LegacyClock
	{
		protected TimeSpan m_rthValueEnd = new TimeSpan(16, 0, 0);
		public Func<DateTime, bool> NyseHoliday;   // _CustomSignal.Holidays.isHoliday
		public bool UseTable { get { return UseCmeHolidayTable; } set { UseCmeHolidayTable = value; } }

		public LegacyClock()
		{
			m_asiaStart = new TimeSpan(18, 0, 0); m_asiaEnd = new TimeSpan(3, 0, 0);
			m_euStart = new TimeSpan(3, 0, 0); m_euEnd = new TimeSpan(11, 30, 0);
			m_nyStart = new TimeSpan(8, 0, 0); m_nyEnd = new TimeSpan(17, 0, 0);
			m_rthStart = new TimeSpan(9, 30, 0); m_rthEnd = new TimeSpan(16, 15, 0);
			m_haltStart = new TimeSpan(17, 0, 0); m_haltEnd = new TimeSpan(18, 0, 0);
			m_globexOpen = new TimeSpan(18, 0, 0); m_globexClose = new TimeSpan(17, 0, 0);
		}

		// pre-VPF-7 IsNoRthDay: Holidays != null && Holidays.isHoliday(d) && !IsGlobexClosedDay(gx)
		protected virtual bool IsNoRthDay(DateTime globexDate)
		{
			return NyseHoliday != null && NyseHoliday(globexDate.Date) && !IsGlobexClosedDay(globexDate);
		}

		public GlobexState State(DateTime et) { return globexStateEt(et); }
		public MarketSession Sessions(DateTime et) { return sessionsEt(et); }
		public DateTime Gx(DateTime et) { return globexDateEt(et); }
		public bool RthValueBar(DateTime et) { return IsRthValueBarEt(et); }
	}
}
