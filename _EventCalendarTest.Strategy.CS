using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// M-EVENT acceptance strategy (specs/m-event-spec.md rev 2, EV-8.10 / P-EV1 / roll check). Places NO orders.
	///
	/// SETUP: an ES/NQ (CME equity index) chart, 1-5 minute bars, Exchange time zone, several years of data
	/// (the roll mode wants 2016-2024). Apply this strategy; set TraceLevel to Journal (smoke) or Info (probe / roll).
	///
	/// RUN (Mode input):
	///   0 smoke - EventConfig.enabled with defaults. Expect the "EVENT created ..." journal line; with the PR A empty table also
	///             "EVENT table missing: <kinds>", day lines and no exception. Rule kinds (claims, ISM, opex, expiry, roll week, half day)
	///             still appear. The trade journal gains evtDayTier,evtMinSince when a derived strategy trades.
	///   1 probe P-EV1 - one EVPROBE line per scheduled event at ProbeTier+ (default T3 so rule-only Claims shows up while the table
	///             is empty until PR B): ET time, UTC, the bar whose [open, close) contains it, and the week class
	///             (dst = US/EU DST mismatch weeks, mismatch = chart zone offset differs from ET, normal). Ends with an EVPROBE summary.
	///             CHECK: every 08:30 ET event lands on the bar that closes at 08:31..08:35 ET equivalent in the chart zone, in all classes.
	///   2 roll - RollDetection = Series; one EVROLL line per detected splice day with RuleRollDate and the offset in sessions.
	///             CHECK against tools/m-event/out/roll_splices_2016_2024.csv; offsets other than 0 are the VERIFY items for PR B.
	/// </summary>
	public class _EventCalendarTest : _CustomSignal
	{
		[Input] public int Mode { get; set; }
		[Input] public int ProbeTier { get; set; }

		private DateTime m_lastDay = DateTime.MinValue, m_lastSplice = DateTime.MinValue;
		private int m_probeCount, m_probeDst, m_probeMismatch, m_rollCount;
		private EventRec m_pending; private bool m_hasPending;
		private bool m_startLogged;   // startup line written for this run

		public _EventCalendarTest(object _ctx) : base(_ctx) { Mode = 0; ProbeTier = 1; }

		protected override void Create()
		{
			base.Create();
			EventConfig.enabled = true;
			EventConfig.minTier = EEventTier.T3;
			RollDetection = ERollDetection.Series;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_lastDay = m_lastSplice = DateTime.MinValue;
			m_probeCount = m_probeDst = m_probeMismatch = m_rollCount = 0; m_hasPending = false;
			m_startLogged = false;
		}

		/// <summary>One EVTEST summary per run so an empty run is visible (also echoed to Output, independent of TraceLevel).</summary>
		protected override void StopCalc()
		{
			if (m_startLogged)
			{
				string msg = $"EVTEST end mode({Mode}) rolls({m_rollCount}) probes({m_probeCount}) dst({m_probeDst}) mismatch({m_probeMismatch}) lastDay({m_lastDay:yyyy-MM-dd}) lastSplice({m_lastSplice:yyyy-MM-dd})";
				try { Output.WriteLine($"{GetType().Name}: {msg}"); } catch { }
				try { if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, msg); } catch { }
			}
			base.StopCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (!m_startLogged)
			{
				// First bar of the run: also echoed to Output so a run with TraceLevel None / no EventCalendar is visible.
				m_startLogged = true;
				string msg = $"EVTEST start mode({Mode}) probeTier({ProbeTier}) traceLevel({TraceLevel}) rollDetection({RollDetection}) eventCalendar({(EventCalendar == null ? "null" : "ok")})";
				try { Output.WriteLine($"{GetType().Name}: {msg}"); } catch { }
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, msg);
			}
			if (Bars.Status != EBarState.Close) return;
			_EventCalendar ev = EventCalendar;
			if (ev == null) return;
			if (Mode == 1) probe(ev);
			else if (Mode == 2) roll(ev);
			else if (ev.TradingDay != m_lastDay && traceOn(ETraceLevel.Info))
			{
				m_lastDay = ev.TradingDay;
				EventDay d = ev.Classify(m_lastDay);
				trace(ETraceLevel.Info, $"EVSMOKE day({m_lastDay:yyyy-MM-dd}) maxTier({d.maxTier}) count({d.count}) rollWeek({ev.IsRollWeek()}) toExpiry({ev.SessionsToExpiry()}) blackout({EventBlackout})");
			}
		}

		private void probe(_EventCalendar ev)
		{
			EEventTier t = (EEventTier)Math.Max(1, Math.Min(3, ProbeTier));
			if (!m_hasPending) m_hasPending = ev.NextEvent(t, out m_pending);
			if (!m_hasPending) return;
			DateTime closeEt = ev.ToEt(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Bars.Time[0], DateTimeKind.Unspecified), BarTimeZone));
			DateTime eventEt = m_pending.etTime;
			if (closeEt <= eventEt) return;   // half-open: the event bar is the first whose close is after the event instant
			DateTime jan = new DateTime(m_pending.utc.Year, 1, 15, 12, 0, 0, DateTimeKind.Utc);
			TimeSpan offChart = BarTimeZone.GetUtcOffset(m_pending.utc), offEt = ev.ToEt(m_pending.utc) - m_pending.utc;
			TimeSpan diffJan = BarTimeZone.GetUtcOffset(jan) - (ev.ToEt(jan) - jan);
			string cls = offChart - offEt != diffJan ? "dst" : (offChart != offEt ? "mismatch" : "normal");
			m_probeCount++; if (cls == "dst") m_probeDst++; else if (cls == "mismatch") m_probeMismatch++;
			if (traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, $"EVPROBE kind({m_pending.kind}) tier({m_pending.tier}) et({eventEt:yyyy-MM-dd HH:mm}) utc({m_pending.utc:yyyy-MM-dd HH:mm}) bar({Bars.Time[0]:yyyy-MM-dd HH:mm}) barEt({closeEt:HH:mm}) lagMin({(closeEt - eventEt).TotalMinutes:0}) week({cls}) n({m_probeCount}) dst({m_probeDst}) mismatch({m_probeMismatch})");
			m_hasPending = false;
		}

		private void roll(_EventCalendar ev)
		{
			if (!ev.IsRollDay() || ev.TradingDay == m_lastSplice) return;
			m_lastSplice = ev.TradingDay;
			m_rollCount++;
			DateTime rule = _EventCalendar.RuleRollDate(m_lastSplice.AddDays(-14));
			int off = 0; DateTime a = rule < m_lastSplice ? rule : m_lastSplice, b = rule < m_lastSplice ? m_lastSplice : rule;
			for (DateTime d = a; d < b; d = d.AddDays(1)) if (EventRules.IsNyseBusinessDay(d)) off++;
			if (rule > m_lastSplice) off = -off;
			if (traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, $"EVROLL splice({m_lastSplice:yyyy-MM-dd}) rule({rule:yyyy-MM-dd}) offsetSessions({off}) n({m_rollCount})");
		}
	}
}
