using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using S = PowerLanguage.Function._CmeSessionClock.MarketSession;
using G = PowerLanguage.Function._CmeSessionClock.GlobexState;

// Spec VPF-7 unit tests for _CmeSessionClock: known sessions, holidays, bounds, time zones, hooks, cache, and a sweep
// against the pre-VPF-7 _VolumeProfileFuturesBase code (Legacy, extracted from git by run.sh).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static DateTime T(int y, int mo, int d, int h, int mi) { return new DateTime(y, mo, d, h, mi, 0); }
	static TimeZoneInfo Zone(string win, string iana) { try { return TimeZoneInfo.FindSystemTimeZoneById(win); } catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(iana); } }
	static HashSet<DateTime> s_nyse = new HashSet<DateTime>();
	static bool Nyse(DateTime d) { return s_nyse.Contains(d.Date); }

	static int Main()
	{
		for (int y = 2016; y <= 2027; y++) foreach (var d in _NyseHolidays.Rules.Holidays(y)) s_nyse.Add(d);
		TimeZoneInfo eastern = Zone("Eastern Standard Time", "America/New_York");
		var c = new _CmeSessionClock(eastern);

		// --- weekly cycle (Wed 2024-03-13, Sun 2024-03-10, Fri 2024-03-15) ---
		Check(c.State(T(2024, 3, 9, 12, 0)) == G.Closed, "Saturday closed");
		Check(c.State(T(2024, 3, 10, 17, 59)) == G.Closed, "Sunday 17:59 closed");
		Check(c.State(T(2024, 3, 10, 18, 0)) == G.Open, "Sunday 18:00 open");
		Check(c.Sessions(T(2024, 3, 10, 18, 30)) == S.Asia, "Sunday 18:30 Asia");
		Check(c.GlobexDate(T(2024, 3, 10, 18, 30)) == new DateTime(2024, 3, 11), "Sunday 18:30 is Monday's Globex date");
		Check(c.GlobexDate(T(2024, 3, 11, 17, 59)) == new DateTime(2024, 3, 11), "Monday 17:59 Globex date Monday");
		Check(c.State(T(2024, 3, 13, 17, 30)) == G.Halt, "Wednesday 17:30 halt");
		Check(c.State(T(2024, 3, 13, 18, 0)) == G.Open, "Wednesday 18:00 open");
		Check(c.State(T(2024, 3, 15, 17, 0)) == G.Closed, "Friday 17:00 closed");
		Check(c.State(T(2024, 3, 15, 16, 59)) == G.Open, "Friday 16:59 open");
		Check(c.Sessions(T(2024, 3, 13, 10, 5)) == (S.Europe | S.NewYork | S.Rth), "10:05 Europe|NewYork|Rth");
		Check(c.Sessions(T(2024, 3, 13, 8, 30)) == (S.Europe | S.NewYork | S.Premarket), "08:30 premarket");
		Check(c.Sessions(T(2024, 3, 13, 16, 20)) == (S.NewYork | S.PostMarket), "16:20 postmarket");
		Check(c.Sessions(T(2024, 3, 13, 2, 0)) == S.Asia, "02:00 Asia");
		Check(c.Sessions(T(2024, 3, 13, 3, 0)) == S.Europe, "03:00 Europe");
		Check(c.Sessions(T(2024, 3, 13, 16, 15)) == (S.NewYork | S.PostMarket), "16:15 RTH flag ends");
		Check(c.PrimarySessionEt(T(2024, 3, 13, 10, 5)) == S.Rth, "primary 10:05 Rth");
		Check(c.PrimarySessionEt(T(2024, 3, 13, 8, 30)) == S.Premarket, "primary 08:30 Premarket");
		Check(c.InSession(T(2024, 3, 13, 10, 5), S.Rth) && !c.InSession(T(2024, 3, 13, 10, 5), S.Asia), "InSession");
		Check(c.InAny(T(2024, 3, 13, 10, 5), S.Asia | S.Rth), "InAny");

		// --- bounds and elapsed time ---
		DateTime o, cl;
		Check(c.TrySessionBoundsEt(T(2024, 3, 13, 1, 0), S.Asia, out o, out cl) && o == T(2024, 3, 12, 18, 0) && cl == T(2024, 3, 13, 3, 0), "Asia bounds wrap");
		Check(c.TrySessionBoundsEt(T(2024, 3, 13, 11, 0), S.Rth, out o, out cl) && o == T(2024, 3, 13, 9, 30) && cl == T(2024, 3, 13, 16, 15), "RTH bounds");
		Check(!c.TrySessionBoundsEt(T(2024, 3, 13, 11, 0), S.Rth | S.Europe, out o, out cl), "combined flags have no bounds");
		Check(c.MinutesSinceOpenEt(T(2024, 3, 13, 10, 30), S.Rth) == 60, "60 min since RTH open");
		Check(c.MinutesSinceOpenEt(T(2024, 3, 13, 9, 0), S.Rth) == -1, "before RTH open = -1");
		Check(c.MinutesUntilCloseEt(T(2024, 3, 13, 16, 0), S.Rth) == 15, "15 min to RTH close");
		Check(c.SessionLength(S.Asia) == TimeSpan.FromHours(9), "Asia length 9h");
		Check(c.SessionLength(S.Rth) == new TimeSpan(6, 45, 0), "RTH length 6h45");
		Check(c.IsOpeningRangeEt(T(2024, 3, 13, 9, 45), S.Rth, 30) && !c.IsOpeningRangeEt(T(2024, 3, 13, 10, 0), S.Rth, 30), "opening range 30");
		Check(c.IsRthAfterInitialBalanceEt(T(2024, 3, 13, 10, 30), 60) && !c.IsRthAfterInitialBalanceEt(T(2024, 3, 13, 10, 29), 60), "after IB");

		// --- RTH value window (close-stamped, D5 16:00) ---
		Check(!c.IsRthValueBarEt(T(2024, 3, 13, 9, 30)) && c.IsRthValueBarEt(T(2024, 3, 13, 9, 31)), "value window opens after 09:30");
		Check(c.IsRthValueBarEt(T(2024, 3, 13, 16, 0)) && !c.IsRthValueBarEt(T(2024, 3, 13, 16, 1)), "value window ends 16:00");
		Check(c.RthValueEndEt(new DateTime(2024, 11, 29)) == new TimeSpan(13, 15, 0), "value end 13:15 day after Thanksgiving");

		// --- holidays (CME table) ---
		Check(c.State(T(2022, 12, 25, 18, 30)) == G.Closed, "Sun 25 Dec 2022 evening = Globex date Mon 26 Dec, closed");
		Check(c.IsGlobexClosedDay(new DateTime(2022, 12, 26)), "Mon 26 Dec 2022 closed (Sunday observance)");
		Check(c.IsGlobexClosedDay(new DateTime(2024, 3, 29)), "Good Friday 2024 closed");
		Check(c.State(T(2024, 11, 28, 12, 59)) == G.Open && c.State(T(2024, 11, 28, 13, 0)) == G.Halt, "Thanksgiving 2024 halt 13:00");
		Check((c.Sessions(T(2024, 11, 28, 10, 0)) & S.Rth) == 0 && c.IsNoRthDay(new DateTime(2024, 11, 28)), "Thanksgiving no RTH");
		Check((c.Sessions(T(2024, 11, 29, 13, 14)) & S.Rth) != 0 && (c.Sessions(T(2024, 11, 29, 13, 15)) & S.Rth) == 0, "early RTH close 13:15");
		Check(c.TrySessionBoundsEt(T(2024, 11, 29, 10, 0), S.Rth, out o, out cl) && cl == T(2024, 11, 29, 13, 15), "RTH bounds honour early close");
		Check(c.MinutesUntilCloseEt(T(2024, 11, 29, 13, 0), S.Rth) == 15, "minutes to early close");
		Check(!c.IsNoRthDay(new DateTime(2024, 3, 29)), "Good Friday is a closure, not a no-RTH day");

		// --- without the table ---
		var nt = new _CmeSessionClock(eastern) { UseCmeHolidayTable = false };
		Check(!nt.IsGlobexClosedDay(new DateTime(2024, 3, 29)) && nt.State(T(2024, 3, 29, 10, 0)) == G.Open, "no table: Good Friday open");
		Check(nt.IsGlobexClosedDay(new DateTime(2022, 12, 26)) && nt.IsGlobexClosedDay(new DateTime(2023, 1, 2)), "no table: Sunday holidays observed Monday");
		Check(!nt.IsGlobexClosedDay(new DateTime(2021, 12, 24)) && !nt.IsGlobexClosedDay(new DateTime(2021, 12, 31)), "no table: Saturday holidays close nothing");
		Check(nt.State(T(2024, 11, 28, 13, 30)) == G.Open && nt.HolidayHaltEt(new DateTime(2024, 11, 28)) == null, "no table: no holiday halt");

		// --- hooks override the defaults ---
		var hk = new _CmeSessionClock(eastern);
		hk.GlobexClosedDayHook = d => d == new DateTime(2024, 3, 13);
		hk.NoRthDayHook = d => d == new DateTime(2024, 3, 14);
		hk.RthEarlyCloseHook = d => d == new DateTime(2024, 3, 12) ? new TimeSpan(12, 0, 0) : (TimeSpan?)null;
		hk.HolidayHaltHook = d => d == new DateTime(2024, 3, 11) ? new TimeSpan(14, 0, 0) : (TimeSpan?)null;
		Check(hk.State(T(2024, 3, 13, 10, 0)) == G.Closed && hk.State(T(2024, 3, 12, 18, 30)) == G.Closed, "closed hook incl. prior evening");
		Check((hk.Sessions(T(2024, 3, 14, 10, 0)) & (S.Rth | S.Premarket | S.PostMarket)) == 0 && (hk.Sessions(T(2024, 3, 14, 10, 0)) & S.Europe) != 0, "no-RTH hook");
		Check((hk.Sessions(T(2024, 3, 12, 12, 0)) & S.Rth) == 0 && (hk.Sessions(T(2024, 3, 12, 11, 59)) & S.Rth) != 0, "early close hook");
		Check(hk.State(T(2024, 3, 11, 14, 0)) == G.Halt && hk.State(T(2024, 3, 11, 13, 59)) == G.Open, "halt hook");
		Check(!hk.IsGlobexClosedDay(new DateTime(2024, 3, 29)), "hook replaces the table");

		// --- custom windows (e.g. CL RTH 09:00-14:30) ---
		var cl2 = new _CmeSessionClock(eastern) { RthStart = new TimeSpan(9, 0, 0), RthEnd = new TimeSpan(14, 30, 0) };
		Check((cl2.Sessions(T(2024, 3, 13, 9, 5)) & S.Rth) != 0 && (cl2.Sessions(T(2024, 3, 13, 14, 30)) & S.Rth) == 0, "custom RTH window");
		Check((cl2.Sessions(T(2024, 3, 13, 8, 30)) & S.Premarket) != 0 && (cl2.Sessions(T(2024, 3, 13, 9, 0)) & S.Premarket) == 0, "premarket follows RthStart");

		// --- bar time zones ---
		TimeZoneInfo central = Zone("Central Standard Time", "America/Chicago");
		var ct = new _CmeSessionClock(central);
		Check(ct.EasternOf(T(2024, 3, 13, 8, 30)) == T(2024, 3, 13, 9, 30), "Central 08:30 = 09:30 ET");
		Check(ct.GlobexDate(T(2024, 3, 10, 17, 0)) == new DateTime(2024, 3, 11), "Central Sun 17:00 = 18:00 ET, Monday's Globex date");
		Check(ct.State(T(2024, 3, 10, 16, 59)) == G.Closed, "Central Sun 16:59 closed");
		var utc = new _CmeSessionClock(TimeZoneInfo.Utc);
		Check(utc.EasternOf(T(2024, 7, 1, 13, 30)) == T(2024, 7, 1, 9, 30), "UTC 13:30 = 09:30 EDT");
		Check(utc.EasternOf(T(2024, 1, 2, 14, 30)) == T(2024, 1, 2, 9, 30), "UTC 14:30 = 09:30 EST");
		Check(c.ToEastern(DateTime.SpecifyKind(T(2024, 7, 1, 13, 30), DateTimeKind.Utc)) == T(2024, 7, 1, 9, 30), "Utc kind converted");
		Check(c.ToEastern(T(2024, 3, 10, 2, 30)) == T(2024, 3, 10, 2, 30), "Eastern zone: no conversion");
		TimeZoneInfo pacific = Zone("Pacific Standard Time", "America/Los_Angeles");
		var pt = new _CmeSessionClock(pacific);
		Check(pt.ToEastern(T(2024, 3, 10, 2, 30)) == T(2024, 3, 10, 6, 30), "Pacific spring-forward gap moves +1h (03:30 PDT = 06:30 EDT)");
		Check(pt.EasternOf(T(2024, 11, 3, 1, 30)) == T(2024, 11, 3, 4, 30), "Pacific fall-back ambiguous 01:30 resolves as standard (PST)");

		// --- cache ---
		var cc = new _CmeSessionClock(central);
		DateTime bt = T(2024, 3, 13, 8, 30);
		Check(cc.Sessions(bt) == (S.Europe | S.NewYork | S.Rth), "cache warm");
		cc.BarTimeZone = eastern;
		Check(cc.Sessions(bt) == (S.Europe | S.NewYork | S.Premarket), "zone change invalidates the cache");
		cc.RthStart = new TimeSpan(8, 0, 0);
		Check(cc.Sessions(bt) == (S.Europe | S.NewYork | S.Premarket), "window change keeps the cache (pre-VPF-7 behaviour)");
		cc.Invalidate();
		Check((cc.Sessions(bt) & S.Rth) != 0, "Invalidate drops the cache");

		// --- equivalence with the pre-VPF-7 futures base, 2017-2026, every 5 minutes, table on and off ---
		foreach (bool table in new[] { true, false })
		{
			var leg = new Legacy.LegacyClock { UseTable = table, NyseHoliday = Nyse };
			var clk = new _CmeSessionClock(eastern) { UseCmeHolidayTable = table };
			// the futures base wires its virtual hooks; here: the same NYSE no-RTH rule as the legacy stand-in
			clk.NoRthDayHook = d => Nyse(d) && !clk.IsGlobexClosedDay(d);
			int n = 0, bad = 0;
			var singles = new[] { S.Asia, S.Europe, S.NewYork, S.Rth, S.Premarket, S.PostMarket };
			for (DateTime et = new DateTime(2017, 1, 1); et < new DateTime(2027, 1, 1); et = et.AddMinutes(5))
			{
				n++;
				bool ok = (int)leg.State(et) == (int)clk.StateEt(et)
					&& (int)leg.Sessions(et) == (int)clk.SessionsEt(et)
					&& leg.Gx(et) == clk.GlobexDateEt(et)
					&& leg.RthValueBar(et) == clk.IsRthValueBarEt(et);
				if (et.Minute == 0)
					foreach (var s in singles)
					{
						DateTime lo, lc, no, nc;
						bool lb = leg.TrySessionBounds(et, (Legacy.LegacyClock.MarketSession)(int)s, out lo, out lc);
						bool nb = clk.TrySessionBoundsEt(et, s, out no, out nc);
						ok &= lb == nb && lo == no && lc == nc;
					}
				if (!ok && bad++ < 5) Console.WriteLine("FAIL legacy mismatch table=" + table + " at " + et.ToString("yyyy-MM-dd HH:mm"));
			}
			Check(bad == 0, "legacy equivalence table=" + table + " over " + n + " ET times (" + bad + " mismatches)");
		}

		Console.WriteLine("SessionClockTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}
}
