using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Spec VPF-7 acceptance: a non-VP strategy (derives _CustomSignal, not _VolumeProfileFuturesBase) that owns a
	/// _CmeSessionClock. Places no orders. At TraceLevel Info it logs one line per Globex trading day (first bar, Globex
	/// state, sessions and RTH bounds) so the clock can be compared with _VolumeProfileFuturesBase on the same chart.
	/// </summary>
	public class _CmeSessionClockTest : _CustomSignal
	{
		//attribute
		private _CmeSessionClock m_clock;
		private DateTime m_lastGlobexDate = DateTime.MinValue;

		//interface methods
		public _CmeSessionClockTest(object _ctx) : base(_ctx) { }

		protected override void StartCalc()
		{
			base.StartCalc();
			m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info));   // CME equity-index windows and holiday table by default
			m_lastGlobexDate = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime barTime = Bars.Time[0];
			DateTime gx = m_clock.GlobexDate(barTime);
			if (gx == m_lastGlobexDate) return;
			m_lastGlobexDate = gx;
			if (!traceOn(ETraceLevel.Info)) return;
			DateTime et = m_clock.EasternOf(barTime);
			DateTime open, close;
			bool rth = m_clock.TrySessionBoundsEt(et, _CmeSessionClock.MarketSession.Rth, out open, out close);
			trace(ETraceLevel.Info, $"VPF-7 clock globexDate({gx:yyyy-MM-dd}) firstBarEt({et:yyyy-MM-dd HH:mm}) state({m_clock.State(barTime)}) " +
				$"sessions({m_clock.Sessions(barTime)}) noRth({m_clock.IsNoRthDay(gx)}) rth({(rth ? open.ToString("HH:mm") + "-" + close.ToString("HH:mm") : "-")})");
		}
	}
}
