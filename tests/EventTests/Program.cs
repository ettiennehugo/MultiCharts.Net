using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for M-EVENT (specs/m-event-spec.md rev 2, EV-8). PR A: rule kinds + synthetic records via the table /
// corrections seams. Deferred to PR B: EV-8.2 (generated table vs official sources) and the real EV-8.5 data cases.
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static readonly TimeZoneInfo Est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
	static DateTime U(DateTime et) { return TimeZoneInfo.ConvertTimeToUtc(et, Est); }
	static DateTime ET(int y, int m, int d, int h = 0, int mi = 0) { return new DateTime(y, m, d, h, mi, 0); }

	static _EventCalendar Eng(Action<EventConfig> f = null, List<EventRec> table = null, EventCorrection[] corr = null, List<string> j = null, List<string> w = null, int barMin = 1)
	{
		// synthetic tables start mid-history: partial coverage on by default (R2.1-h has its own tests in FirstBarCoverage)
		var c = new EventConfig { enabled = true, allowPartialCoverage = true }; if (f != null) f(c);
		var e = new _EventCalendar { config = c, table = table ?? new List<EventRec>(), corrections = corr ?? new EventCorrection[0], barDuration = TimeSpan.FromMinutes(barMin) };
		if (j != null) e.journal = s => j.Add(s);
		if (w != null) e.warnOnce = s => w.Add(s);
		string err = e.Configure(); if (err != null) throw new Exception(err);
		return e;
	}
	/// <summary>Step one bar whose CLOSE is `etClose` (ET).</summary>
	static void Bar(_EventCalendar e, DateTime etClose, int barMin = 1, bool splice = false)
	{ DateTime c = U(etClose); e.Step(etClose, c.AddMinutes(-barMin), c, splice); }
	static List<EventRec> Gen(int y) { return EventRules.Generate(y, false); }
	static bool Has(List<EventRec> l, EEventKind k, DateTime et) { return l.Any(r => r.kind == k && r.etTime == et); }
	static EventRec T1(EEventKind k, DateTime et, EEventFlags f = EEventFlags.None) { var r = EventRec.Make(k, et, EventRules.DefaultTier(k), f); return r; }

	static void Main()
	{
		Rules(); TimeAndDays(); Windows(); AsOf(); HalfDays(); Roll(); Csv(); NextPrev(); ConfigCanon(); EmptyTable(); SessionOpens(); BlackoutMacroOnly(); EiaRuleOnly(); FirstBarCoverage(); Perf();
		Console.WriteLine($"EventTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	// ---- EV-8.1 rule kinds
	static void Rules()
	{
		var g26 = Gen(2026);
		int[] mfg = { 5, 2, 2, 1, 1, 1, 1, 3, 1, 1, 2, 1 }, svc = { 7, 4, 4, 6, 5, 3, 6, 5, 3, 5, 4, 3 };   // ismworld.org 2026 calendar
		for (int m = 1; m <= 12; ++m)
		{
			Check(Has(g26, EEventKind.IsmMfg, ET(2026, m, mfg[m - 1], 10)), "ISM Mfg 2026-" + m);
			Check(Has(g26, EEventKind.IsmSvc, ET(2026, m, svc[m - 1], 10)), "ISM Svc 2026-" + m);
		}
		Check(g26.Where(r => r.kind == EEventKind.IsmMfg).All(r => (r.flags & EEventFlags.RuleOnly) == 0), "ISM 2026 published (not RuleOnly)");
		Check(Gen(2024).Where(r => r.kind == EEventKind.IsmMfg).All(r => (r.flags & EEventFlags.RuleOnly) != 0), "ISM 2024 RuleOnly");
		// EIA
		var g25 = Gen(2025);
		Check(Has(g25, EEventKind.EiaPetroleum, ET(2025, 1, 2, 11)) && !Has(g25, EEventKind.EiaPetroleum, ET(2025, 1, 1, 10, 30)), "EIA 2025-01-01 -> Thu 01-02 11:00");
		Check(Has(g25, EEventKind.EiaPetroleum, ET(2025, 12, 29, 17)), "EIA 2025-12-24 -> Mon 12-29 17:00");
		Check(Has(g26, EEventKind.EiaPetroleum, ET(2026, 11, 12, 12)), "EIA 2026-11-11 -> Thu 11-12 12:00");
		Check(Has(g26, EEventKind.EiaPetroleum, ET(2026, 3, 4, 10, 30)), "EIA normal Wednesday 10:30");
		Check(g26.Where(r => r.kind == EEventKind.EiaPetroleum).All(r => r.tier == EEventTier.T2 && r.Class == EEventClassMask.Energy), "EIA T2 Energy");
		Check(g26.Count(r => r.kind == EEventKind.EiaPetroleum) == 52, "EIA 52 records 2026");
		// Claims (DOL)
		var g27 = Gen(2027);
		Check(Has(g26, EEventKind.Claims, ET(2026, 11, 25, 8, 30)) && !Has(g26, EEventKind.Claims, ET(2026, 11, 26, 8, 30)), "Claims Thanksgiving 2026 -> Wed 11-25");
		Check(Has(g27, EEventKind.Claims, ET(2027, 11, 10, 8, 30)) && Has(g27, EEventKind.Claims, ET(2027, 11, 24, 8, 30)), "Claims 2027 Veterans/Thanksgiving Wednesdays");
		Check(Has(g26, EEventKind.Claims, ET(2026, 3, 12, 8, 30)), "Claims normal Thursday");
		// Roll dates vs CME rolldates page (2025-2028)
		int[,] cme = { { 2025, 3, 21, 17 }, { 2025, 6, 20, 16 }, { 2025, 9, 19, 15 }, { 2025, 12, 19, 15 }, { 2026, 3, 20, 16 }, { 2026, 6, 18, 15 }, { 2026, 9, 18, 14 }, { 2026, 12, 18, 14 },
			{ 2027, 3, 19, 15 }, { 2027, 6, 18, 14 }, { 2027, 9, 17, 13 }, { 2027, 12, 17, 13 }, { 2028, 3, 17, 13 }, { 2028, 6, 16, 12 }, { 2028, 9, 15, 11 }, { 2028, 12, 15, 11 } };
		for (int i = 0; i < cme.GetLength(0); ++i)
		{
			int y = cme[i, 0], m = cme[i, 1];
			// R2.1-d: 2027-06 (Juneteenth observed Fri 06-18, CME open) is 06-18; 2026-06 comes from the override table.
			Check(EventRules.ExpiryDate(y, m) == new DateTime(y, m, cme[i, 2]), "CME expiry " + y + "-" + m);
			Check(EventRules.RollDate(y, m) == new DateTime(y, m, cme[i, 3]), "CME roll " + y + "-" + m);
		}
		// R2.1-d: futures expiry moves only for CME closures; options (Opex) still follow NYSE.
		Check(EventRules.ExpiryDate(2027, 6) == new DateTime(2027, 6, 18) && EventRules.OpexDate(2027, 6) == new DateTime(2027, 6, 17), "2027-06 futures 06-18, SPX opex 06-17");
		Check(Has(g27, EEventKind.QuarterlyExpiry, ET(2027, 6, 18, 9, 30)) && Has(g27, EEventKind.RollWeek, ET(2027, 6, 14)), "2027-06 QuarterlyExpiry 06-18 + roll span");
		for (int i = 0; i < EventRules.s_expiryOverrides.Length; i += 2)
		{
			long k = EventRules.s_expiryOverrides[i]; int oy = (int)(k / 100), om = (int)(k % 100);
			Check(EventRules.RuleExpiryDate(oy, om) != EventRules.ExpiryDate(oy, om), "override " + k + " differs from the rule (else drop it)");
		}
		// whole range: every quarterly expiry is a CME trading weekday and a Friday unless a CME closure / override moved it
		for (int y = 2016; y <= 2028; ++y)
			for (int m = 3; m <= 12; m += 3)
			{
				DateTime x = EventRules.ExpiryDate(y, m), f = EventRules.ThirdFriday(y, m);
				Check(x <= f && x > f.AddDays(-7) && x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday && !_CmeHolidays.Rules.Get(x).Closed, "expiry valid " + y + "-" + m);
				if (x != f) Console.WriteLine("expiry moved " + y + "-" + m + ": " + f.ToString("MM-dd") + " -> " + x.ToString("MM-dd"));
			}
		Check(EventRules.RollDate(2018, 3) == new DateTime(2018, 3, 8), "2018 roll: Thursday 8 days before");
		Check(EventRules.ExpiryDate(2022, 4) == new DateTime(2022, 4, 14), "Opex Good Friday 2022 -> Thu");
		Check(Has(g26, EEventKind.MonthlyOpex, ET(2026, 6, 18, 9, 30)) && Has(g26, EEventKind.MonthlyOpex, ET(2026, 6, 18, 16)), "Opex Juneteenth 2026 AM + PM");
		Check(Has(g26, EEventKind.QuarterlyExpiry, ET(2026, 6, 18, 9, 30)) && Has(g26, EEventKind.IndexRebalance, ET(2026, 6, 18, 16)), "Quarterly + rebalance 2026-06");
		Check(g26.Count(r => r.kind == EEventKind.RollWeek) == 4 && g26.Where(r => r.kind == EEventKind.RollWeek).All(r => r.IsSpan), "4 RollWeek spans");
		var vix = g26.Where(r => r.kind == EEventKind.VixExpiry).ToList();
		Check(vix.Count == 12 && vix.Any(r => r.etTime.Date == new DateTime(2026, 10, 21)), "VIX expiry Oct 2026 = Wed 10-21");
		Check(!g26.Any(r => r.kind == EEventKind.RussellRecon) && EventRules.Generate(2026, true).Any(r => r.kind == EEventKind.RussellRecon), "Russell optional");
		Check(EventRules.IsFederalHoliday(new DateTime(2025, 12, 25)) && !EventRules.IsFederalHoliday(new DateTime(2025, 12, 26)), "federal holiday");
	}

	// ---- EV-8.3 time and trading days
	static void TimeAndDays()
	{
		var e = Eng(null, new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 6, 8, 30)), T1(EEventKind.Cpi, ET(2026, 3, 13, 8, 30)) });
		Bar(e, ET(2026, 3, 6, 8, 0));
		var a = e.EventsOn(new DateTime(2026, 3, 6)).First(r => r.kind == EEventKind.Cpi);
		Check(a.utc == new DateTime(2026, 3, 6, 13, 30, 0), "EST 08:30 = 13:30Z");
		Bar(e, ET(2026, 3, 13, 8, 0));
		var b = e.EventsOn(new DateTime(2026, 3, 13)).First(r => r.kind == EEventKind.Cpi);
		Check(b.utc == new DateTime(2026, 3, 13, 12, 30, 0), "EDT 08:30 = 12:30Z");
		foreach (var bad in new[] { ET(2026, 3, 8, 2, 30), ET(2026, 11, 1, 1, 30) })
		{
			var c = new _EventCalendar { config = new EventConfig { enabled = true }, table = new List<EventRec> { T1(EEventKind.Cpi, bad) }, corrections = new EventCorrection[0] };
			string err = c.Configure();
			Check(err != null && (err.Contains("invalid") || err.Contains("ambiguous")), "Configure rejects " + bad);
		}
		Check(e.TradingDayOf(U(ET(2026, 3, 10, 18, 0))) == new DateTime(2026, 3, 11), ">= 18:00 -> next day");
		Check(e.TradingDayOf(U(ET(2026, 3, 10, 14, 0))) == new DateTime(2026, 3, 10), "14:00 same day");
		Check(e.TradingDayOf(U(ET(2026, 3, 10, 17, 30))) == new DateTime(2026, 3, 11), "break -> next session");
		Check(e.TradingDayOf(U(ET(2026, 3, 8, 12, 0))) == new DateTime(2026, 3, 9), "Sunday -> Monday");
		Check(e.TradingDayOf(U(ET(2026, 3, 7, 12, 0))) == new DateTime(2026, 3, 9), "Saturday -> Monday");
		Check(e.TradingDayOf(U(ET(2024, 3, 29, 8, 30))) == new DateTime(2024, 4, 1), "closed Good Friday 2024 -> Monday");
		// Good Friday NFP with an abbreviated CME equity session (closes 08:15 CT = 09:15 ET): the 08:30 release stays on the day.
		// 2026: CME clearing advisory https://www.cmegroup.com/tools-information/holiday-calendar/files/2026/2026-good-friday-clearing-advisory.pdf
		foreach (var gf in new[] { new DateTime(2021, 4, 2), new DateTime(2026, 4, 3) })
		{
			var h = _CmeHolidays.Rules.Get(gf);
			var eg = Eng(null, new List<EventRec> { T1(EEventKind.Nfp, gf + new TimeSpan(8, 30, 0)) });
			Check(!h.Closed && h.HaltEt == new TimeSpan(9, 15, 0), "Good Friday " + gf.Year + ": CME abbreviated, halt 09:15 ET");
			Check(eg.TradingDayOf(U(gf + new TimeSpan(8, 30, 0))) == gf && eg.EventsOnExPost(gf).Any(r => r.kind == EEventKind.Nfp), "Good Friday NFP " + gf.Year + " maps to the same day");
			Check(eg.TradingDayOf(U(gf + new TimeSpan(9, 30, 0))) == gf.AddDays(3), "Good Friday " + gf.Year + " after the halt -> Monday");
		}
		Check(e.TradingDayOf(U(ET(2026, 3, 6, 17, 30))) == new DateTime(2026, 3, 9), "Friday after close -> Monday");
	}

	// ---- EV-8.4 windows on 1m / 5m / daily
	static void Windows()
	{
		var tbl = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) };
		var e = Eng(c => { c.blackoutPreMin = 5; c.blackoutPostMin = 10; c.blackoutFlatten = true; }, tbl);
		var on = new List<int>(); int flat = 0;
		for (DateTime t = ET(2026, 3, 11, 8, 0); t <= ET(2026, 3, 11, 9, 0); t = t.AddMinutes(1))
		{
			Bar(e, t);
			if (e.IsInWindow(EEventTier.T1, 5, 10)) on.Add(t.Hour * 100 + t.Minute);
			Check(e.EventBlackout == e.IsInWindow(EEventTier.T1, 5, 10), "blackout mirrors window " + t);
			if (e.EventFlattenNow) flat++;
		}
		Check(on.Count == 15 && on.First() == 826 && on.Last() == 840, "1m window bars 08:26..08:40 (" + string.Join(",", on) + ")");
		Check(flat == 1, "flattenNow once");
		var e5 = Eng(null, tbl, null, null, null, 5); on.Clear();
		for (DateTime t = ET(2026, 3, 11, 8, 0); t <= ET(2026, 3, 11, 9, 0); t = t.AddMinutes(5)) { Bar(e5, t, 5); if (e5.IsInWindow(EEventTier.T1, 5, 10)) on.Add(t.Hour * 100 + t.Minute); }
		Check(string.Join(",", on) == "830,835,840", "5m window bars 08:30,08:35,08:40 (" + string.Join(",", on) + ")");
		Bar(e5, ET(2026, 3, 11, 8, 35), 5);
		Check(!e5.IsInWindow(EEventTier.T1, 0, 0), "0/0 window empty");
		Check(e5.IsInWindow(EEventTier.T2, 5, 10) && !e5.IsInWindow(EEventTier.C, 5, 10), "tier filter");
		var w = new List<string>();
		var ed = Eng(null, tbl, null, null, w, 1440);
		Bar(ed, ET(2026, 3, 11, 17, 0), 1440); Bar(ed, ET(2026, 3, 12, 17, 0), 1440);
		Check(!ed.IsInWindow(EEventTier.T1, 600, 600) && !ed.IsInWindow(EEventTier.T1, 600, 600), "daily bars: window false");
		Check(w.Count(s => s.Contains("daily")) == 1, "daily bars: one warning");
	}

	// ---- EV-8.5 logic (synthetic): unscheduled, canceled, delayed, as-of vs ex-post
	static void AsOf()
	{
		var tbl = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 4, 10, 8, 30)), T1(EEventKind.Cpi, ET(2026, 5, 12, 8, 30)) };
		var corr = new[] {
			new EventCorrection { kind = EEventKind.FomcDecision, actualEt = 202603111000, flags = EEventFlags.Unscheduled, source = "synthetic" },
			new EventCorrection { kind = EEventKind.Cpi, originalEt = 202604100830, announcedEt = 202604011200, source = "synthetic cancel" },
			new EventCorrection { kind = EEventKind.Cpi, originalEt = 202605120830, actualEt = 202605200830, announcedEt = 202605081000, source = "synthetic delay" },
		};
		var e = Eng(null, tbl, corr);
		Bar(e, ET(2026, 3, 11, 9, 45));
		Check(!e.IsInWindow(EEventTier.T1, 30, 30), "unscheduled: no pre-side at 09:45");
		EventRec n; Check(!e.NextEvent(EEventTier.T1, out n) || n.etTime != ET(2026, 3, 11, 10, 0), "unscheduled not visible to NextEvent before");
		Check(!e.Classify(new DateTime(2026, 3, 11)).hasFomc, "unscheduled not in as-of Classify");
		Check(e.ClassifyExPost(new DateTime(2026, 3, 11)).hasFomc, "unscheduled in ex-post Classify");
		Bar(e, ET(2026, 3, 11, 10, 15));
		Check(e.IsInWindow(EEventTier.T1, 30, 30), "unscheduled: post-side at 10:15");
		Check(Math.Abs(e.MinutesSince(EEventTier.T1) - 15) < 1e-9, "MinutesSince unscheduled = 15");
		// canceled
		Bar(e, ET(2026, 3, 31, 12, 0));
		Check(e.NextEvent(EEventTier.T1, out n) && n.etTime == ET(2026, 4, 10, 8, 30), "before cancel announcement: Cpi 04-10 next");
		Bar(e, ET(2026, 4, 2, 12, 0));
		Check(e.NextEvent(EEventTier.T1, out n) && n.etTime != ET(2026, 4, 10, 8, 30), "after cancel announcement: skipped");
		Check(e.EventsOnExPost(new DateTime(2026, 4, 10)).All(r => r.kind != EEventKind.Cpi) && e.EventsOnExPost(new DateTime(2026, 4, 10), true).Any(r => r.kind == EEventKind.Cpi), "ex-post excludes canceled unless asked");
		Bar(e, ET(2026, 4, 10, 8, 35));
		Check(!e.IsInWindow(EEventTier.T1, 10, 10), "canceled: no window");
		// delayed
		Check(!e.Classify(new DateTime(2026, 5, 12)).Has(EEventKind.Cpi), "delayed: original gone as-of session open (after announcement)");
		Check(e.Classify(new DateTime(2026, 5, 12), U(ET(2026, 5, 7, 12, 0))).Has(EEventKind.Cpi), "delayed: original present as-of before announcement");
		Check(!e.Classify(new DateTime(2026, 5, 20), U(ET(2026, 5, 7, 12, 0))).Has(EEventKind.Cpi), "delayed: new date unknown before announcement");
		var d20 = e.Classify(new DateTime(2026, 5, 20));
		Check(d20.Has(EEventKind.Cpi) && d20.maxTier == EEventTier.T1, "delayed: new date T1 after announcement");
		Check(e.EventsOnExPost(new DateTime(2026, 5, 20)).Any(r => r.kind == EEventKind.Cpi && (r.flags & EEventFlags.Delayed) != 0), "delayed flag");
		// lapse row: canceledSince explicit
		var e2 = Eng(null, new List<EventRec> { T1(EEventKind.Nfp, ET(2026, 10, 2, 8, 30)) },
			new[] { new EventCorrection { kind = EEventKind.Nfp, originalEt = 202610020830, canceledSinceEt = 202610010001, announcedEt = 202610030900 } });
		Bar(e2, ET(2026, 10, 1, 12, 0));
		Check(!e2.NextEvent(EEventTier.T1, out n) || n.etTime != ET(2026, 10, 2, 8, 30), "lapse: canceled from the lapse start");
		Bar(e2, ET(2026, 9, 30, 12, 0));
	}

	// ---- EV-8.6 half days
	static void HalfDays()
	{
		string p = Path.GetTempFileName();
		File.WriteAllText(p, "action,kind,date_et,time_et,tier,flags,ref_period,announced_et,source\nadd,Custom:Late,2025-11-28,14:00,T3,Scheduled,,,test\n");
		var e = Eng(c => c.csvPath = p);
		Bar(e, ET(2025, 11, 28, 9, 0));
		var d = e.Classify(new DateTime(2025, 11, 28));
		Check(d.isHalfDay && e.Classify(new DateTime(2025, 12, 24)).isHalfDay && !e.Classify(new DateTime(2025, 12, 23)).isHalfDay, "half days 11-28 / 12-24");
		// R2.1-b: 14:00 is after the 13:15 halt -> the event belongs to the next session (Mon 12-01)
		Check(!e.EventsOn(new DateTime(2025, 11, 28)).Any(r => r.name == "Late") && e.EventsOnExPost(new DateTime(2025, 12, 1)).Any(r => r.name == "Late" && r.etTime.Hour == 14), "after-halt event moves to the next session");
		Check(!d.firstReleaseEt.HasValue, "half day: no macro release before the halt");
		Check(e.EventsOn(new DateTime(2025, 11, 28)).Any(r => r.kind == EEventKind.HalfDay && r.etTime.TimeOfDay == new TimeSpan(13, 15, 0)), "HalfDay 13:15");
		// R2.1-b: after the early halt (13:15 ET) an instant is out of session -> next session open (Mon 12-01)
		Check(e.TradingDayOf(U(ET(2025, 11, 28, 13, 0))) == new DateTime(2025, 11, 28), "half day before halt keeps the date");
		Check(e.TradingDayOf(U(ET(2025, 11, 28, 13, 15))) == new DateTime(2025, 12, 1), "half day at halt -> next session");
		Check(e.TradingDayOf(U(ET(2025, 11, 28, 14, 0))) == new DateTime(2025, 12, 1), "half day after halt -> next session (R2.1-b)");
		Check(e.TradingDayOf(U(ET(2025, 12, 24, 14, 0))) == new DateTime(2025, 12, 26), "Christmas Eve after halt -> Fri 12-26");
		File.Delete(p);
	}

	// ---- SessionOpenUtc vs weekday Globex closures (Fri Christmas 2026, Thu Christmas 2025, Good Friday, New Year)
	static void SessionOpens()
	{
		var e = Eng(); var clk = new _CmeSessionClock(null);
		Check(e.SessionOpenUtc(new DateTime(2026, 12, 28)) == U(ET(2026, 12, 27, 18, 0)), "Fri Christmas 2026: Mon 12-28 opens Sun 18:00");
		Check(e.SessionOpenUtc(new DateTime(2025, 12, 26)) == U(ET(2025, 12, 25, 18, 0)), "Thu Christmas 2025: Fri 12-26 opens Thu 18:00");
		int bad = 0;
		for (DateTime d = new DateTime(2016, 1, 4); d.Year <= 2028; d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || clk.IsGlobexClosedDay(d)) continue;
			DateTime o = TimeZoneInfo.ConvertTimeFromUtc(e.SessionOpenUtc(d), Est);
			bool ok = clk.StateEt(o) == _CmeSessionClock.GlobexState.Open && clk.GlobexDateEt(o) == d && clk.StateEt(o.AddMinutes(-1)) != _CmeSessionClock.GlobexState.Open
				&& e.TradingDayOf(e.SessionOpenUtc(d)) == d;
			if (!ok && bad++ < 5) Console.WriteLine("session open mismatch " + d.ToString("yyyy-MM-dd") + " " + o.ToString("MM-dd HH:mm") + " " + clk.StateEt(o));
		}
		Check(bad == 0, "SessionOpenUtc = real reopen for every trading day 2016-2028");
	}

	// ---- R2.1-c: EventBlackout is Macro-only; Energy only via an explicit mask
	static void BlackoutMacroOnly()
	{
		var tbl = new List<EventRec>();
		var e = Eng(c => { c.blackoutPreMin = 5; c.blackoutPostMin = 10; c.blackoutTier = EEventTier.T2; c.classes = EEventClassMask.Macro | EEventClassMask.Calendar | EEventClassMask.Energy; }, tbl);
		Bar(e, ET(2026, 3, 4, 10, 32));   // EIA Wed 10:30 (T2 Energy); ISM Svc 10:00 is T3
		Check(!e.EventBlackout && e.IsInWindow(EEventTier.T2, 5, 10, EEventClassMask.Energy) && !e.IsInWindow(EEventTier.T2, 5, 10), "EIA: no blackout, Energy window explicit");
	}

	// ---- R2.1-e/g: EIA outside the published range is RuleOnly, journals EiaRuleOnly, known-from = its instant (never
	// MinValue); Classify count / maxTier / firstRelease are Macro only
	static void EiaRuleOnly()
	{
		var j = new List<string>();
		var e = Eng(c => c.classes = EEventClassMask.Macro | EEventClassMask.Calendar | EEventClassMask.Energy, null, null, j);
		Bar(e, ET(2024, 3, 6, 9, 0));
		var r = e.EventsOnExPost(new DateTime(2024, 3, 6)).First(x => x.kind == EEventKind.EiaPetroleum);
		Check((r.flags & EEventFlags.RuleOnly) != 0 && r.knownSinceUtc == r.utc, "EIA 2024 RuleOnly, knownSince = instant");
		Check(j.Any(x => x.StartsWith("EVENT day 2024-03-06") && x.EndsWith(" EiaRuleOnly")), "day journal EiaRuleOnly");
		var d = e.ClassifyExPost(new DateTime(2024, 3, 6));
		Check(d.eiaRuleOnly && d.count == 0 && d.maxTier == EEventTier.None && !d.firstReleaseEt.HasValue && d.Has(EEventKind.EiaPetroleum), "Energy-only day: Macro count 0");
		Check((Gen(2026).First(x => x.kind == EEventKind.EiaPetroleum && x.etTime.Month == 3).flags & EEventFlags.RuleOnly) == 0, "EIA 2026 published");
	}

	// ---- EV-4.7 firstBarUtc from the host: Configure fails hard
	static void FirstBarCoverage()
	{
		var c = new EventConfig { enabled = true };
		var e = new _EventCalendar { config = c, table = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }, corrections = new EventCorrection[0], barDuration = TimeSpan.FromMinutes(1), firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		var errs = new List<string>(); e.error = s => errs.Add(s);
		Check(e.Configure() == null, "coverage shortfall: Configure never fails (R2.1-h)");
		Bar(e, ET(2026, 3, 11, 8, 31));
		Check(e.Disabled && errs.Count == 1 && errs[0] == "EVENT coverage Cpi starts 2026-03-11 after the first bar (set allowPartialCoverage); engine disabled", "first Step: Error line + disabled");
		Check(!e.IsInWindow(EEventTier.T1, 5, 10) && e.EventsOn(new DateTime(2026, 3, 11)).Count == 0 && !e.EventBlackout, "disabled: neutral answers");
		var e3 = Eng(c3 => c3.allowPartialCoverage = false, new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }); var errs3 = new List<string>(); e3.error = s => errs3.Add(s);
		Bar(e3, ET(2026, 1, 5, 9, 0));
		Check(e3.Disabled && errs3.Count == 1, "no host firstBarUtc: first Step's bar, also disables");
		var e2 = new _EventCalendar { config = new EventConfig { enabled = true, allowPartialCoverage = true }, table = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }, corrections = new EventCorrection[0], firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		Check(e2.Configure() == null, "allowPartialCoverage -> ok");
		Bar(e2, ET(2026, 1, 5, 9, 0)); Check(!e2.Disabled, "allowPartialCoverage -> stays enabled");
	}

	// ---- EV-8.7 roll
	static void Roll()
	{
		var e = Eng();
		Check(e.IsRollWeek(new DateTime(2018, 3, 8)) && e.IsRollWeek(new DateTime(2018, 3, 16)) && !e.IsRollWeek(new DateTime(2018, 3, 7)), "2018 roll week (Thursday rule)");
		Check(e.IsRollWeek(new DateTime(2026, 6, 15)) && e.IsRollWeek(new DateTime(2026, 6, 18)) && !e.IsRollWeek(new DateTime(2026, 6, 19)) && !e.IsRollWeek(new DateTime(2026, 6, 12)), "2026-06 roll week");
		Check(e.SessionsToExpiry(new DateTime(2026, 6, 15)) == 3 && e.SessionsToExpiry(new DateTime(2026, 6, 18)) == 0, "SessionsToExpiry");
		Bar(e, ET(2026, 6, 15, 10, 0), 1, true);
		Check(e.IsRollDay() && e.IsRollWeek() && e.Classify(new DateTime(2026, 6, 15)).isRollWeek, "IsRollDay after splice");
		Check(e.InSpan(EEventKind.RollWeek), "InSpan RollWeek");
		Bar(e, ET(2026, 6, 16, 10, 0));
		Check(!e.IsRollDay(), "IsRollDay next day false");
		Check(_EventCalendar.RuleRollDate(new DateTime(2018, 3, 9)) == new DateTime(2018, 3, 8), "RuleRollDate");
	}

	// ---- EV-8.8 CSV
	static void Csv()
	{
		var e = Eng();
		string err = e.LoadCsv("action,kind,date_et,time_et,tier,flags,ref_period,announced_et,source\n# c\nadd,Bogus,2026-01-05,08:30,T1,,,2026-01-01 09:00,x\nadd,Cpi,2026-01-05,08:30,T1,,,,x\nretier,Cpi,2026-01-05,,,,,,x\nadd,Cpi,2026-03-08,02:30,T1,Scheduled,,,x\n");
		Check(err != null && err.Contains("line 3") && err.Contains("unknown kind") && err.Contains("line 4") && err.Contains("announced_et") && err.Contains("line 5") && err.Contains("retier needs tier") && err.Contains("line 6") && err.Contains("invalid"), "CSV errors with line numbers: " + err);
		string p = Path.GetTempFileName();
		File.WriteAllText(p, string.Join("\n",
			"action,kind,date_et,time_et,tier,flags,ref_period,announced_et,source",
			"add,Cpi,2026-02-11,08:30,T1,Scheduled,Jan 2026,,bls.gov",
			"add,Custom:Speech,2026-02-12,13:00,T2,,,2026-02-01 09:00,fed",
			"remove,Claims,2026-02-12,08:30,,,,,test",
			"retier,IsmMfg,2026-02-02,,T1,,,,test",
			"add,Ppi,2026-02-13,08:30,T2,Scheduled,Jan 2026,,bls.gov",
			"retime,Ppi,2026-02-20,08:30,,,Jan 2026,2026-02-14 09:00,bls.gov"));
		var e2 = Eng(c => c.csvPath = p);
		Bar(e2, ET(2026, 2, 10, 12, 0));
		Check(e2.Classify(new DateTime(2026, 2, 11)).maxTier == EEventTier.T1, "CSV add Cpi");
		var d12 = e2.EventsOn(new DateTime(2026, 2, 12));
		Check(d12.Any(r => r.kind == EEventKind.Custom && r.name == "Speech" && (r.flags & EEventFlags.Csv) != 0) && !d12.Any(r => r.kind == EEventKind.Claims), "CSV Custom + remove");
		Check(e2.EventsOn(new DateTime(2026, 2, 2)).Any(r => r.kind == EEventKind.IsmMfg && r.tier == EEventTier.T1), "CSV retier");
		var ppi20 = e2.EventsOnExPost(new DateTime(2026, 2, 20)).FirstOrDefault(r => r.kind == EEventKind.Ppi);
		Check(ppi20.kind == EEventKind.Ppi && (ppi20.flags & EEventFlags.Unscheduled) != 0, "CSV retime announced after original -> Unscheduled");
		Check(e2.EventsOnExPost(new DateTime(2026, 2, 13)).All(r => r.kind != EEventKind.Ppi), "CSV retime cancels original");
		string sha = EventRules.Sha256(File.ReadAllBytes(p));
		Check(e2.config.Canon().Contains("csvSha=" + sha), "CSV SHA-256 in Canon");
		File.Delete(p);
	}

	// ---- EV-8.9 Next/Prev/MinutesTo/Since, beyond coverage
	static void NextPrev()
	{
		var w = new List<string>();
		var e = Eng(null, null, null, null, w);
		Bar(e, ET(2026, 3, 11, 8, 0));
		Check(Math.Abs(e.MinutesTo(EEventTier.T3) - 1470) < 1e-9, "MinutesTo next Claims = 1470");
		EventRec p; Check(e.PrevEvent(EEventTier.T3, out p) && p.kind == EEventKind.Claims && p.etTime == ET(2026, 3, 5, 8, 30), "PrevEvent Claims 03-05");
		Check(Math.Abs(e.MinutesSince(EEventTier.T2) - (U(ET(2026, 3, 11, 8, 0)) - U(ET(2026, 3, 2, 10, 0))).TotalMinutes) < 1e-9, "MinutesSince T2 = ISM Mfg 03-02");
		Check(double.IsNaN(e.MinutesTo(EEventTier.T1)) && double.IsNaN(e.MinutesSince(EEventTier.T1)), "T1 NaN with empty table");
		Check(w.Any(s => s.Contains("table kinds missing")), "beyond coverage warning");
		Check(e.MinutesTo(EEventTier.T2, EEventClassMask.Energy) is double v && double.IsNaN(v), "Energy excluded by default classes");
		DateTime t; Check(!e.LastT1Instant(out t), "LastT1Instant false");
		var e2 = Eng(c => c.classes |= EEventClassMask.Energy);
		Bar(e2, ET(2026, 3, 11, 8, 0));
		Check(Math.Abs(e2.MinutesTo(EEventTier.T2, EEventClassMask.Energy) - 150) < 1e-9, "EIA next 10:30 = 150 min");
		Check(!e2.IsInWindow(EEventTier.T2, 300, 0), "EIA not in Macro window mask");
	}

	// ---- EV-8.12 config
	static void ConfigCanon()
	{
		Check(new EventConfig().Validate() == null && !new EventConfig().enabled, "defaults valid, off");
		Check(new EventConfig().classes == (EEventClassMask.Macro | EEventClassMask.Calendar), "default classes Macro|Calendar");
		var bad = new EventConfig { classes = EEventClassMask.None, blackoutPreMin = -1, blackoutTier = EEventTier.C };
		string v = bad.Validate();
		Check(v != null && v.Contains("classes") && v.Contains("blackoutPreMin") && v.Contains("blackoutTier"), "Validate names bad values");
		var a = new EventConfig(); var b = new EventConfig();
		Check(a.Canon() == b.Canon(), "Canon stable");
		b.tierOverride[EEventKind.Claims] = EEventTier.T2;
		Check(a.Canon() != b.Canon(), "Canon tracks tierOverride");
		var e = Eng(c => c.tierOverride[EEventKind.Claims] = EEventTier.T1);
		Bar(e, ET(2026, 3, 11, 8, 0));
		Check(Math.Abs(e.MinutesTo(EEventTier.T1) - 1470) < 1e-9, "tierOverride applied");
		var e3 = Eng(c => c.minTier = EEventTier.T2);
		Bar(e3, ET(2026, 3, 11, 8, 0));
		Check(!e3.EventsOn(new DateTime(2026, 3, 12)).Any(r => r.kind == EEventKind.Claims), "minTier drops T3 at build");
	}

	// ---- ruling 1: empty table
	static void EmptyTable()
	{
		var j = new List<string>();
		var e = Eng(null, null, null, j);
		Check(j.Any(s => s.StartsWith("EVENT table missing: FomcDecision,") && s.Contains("Cpi") && s.Contains("Nfp")), "journal EVENT table missing");
		Bar(e, ET(2026, 3, 12, 7, 0));
		Check(!e.IsInWindow(EEventTier.T1, 30, 30) && !e.Classify(new DateTime(2026, 3, 12)).hasFomc, "neutral outputs");
		Check(j.Any(s => s.StartsWith("EVENT day 2026-03-12") && s.Contains("Claims@08:30")), "day journal line");
		Check(EventRules.Table().Count == 0 && EventRules.Corrections.Length == 0, "PR A table empty");
	}

	// ---- EV-6.11 performance
	static void Perf()
	{
		var e = Eng(c => { c.blackoutPreMin = 5; c.blackoutPostMin = 10; });
		var sw = Stopwatch.StartNew(); int n = 0;
		for (DateTime t = ET(2025, 1, 2, 0, 0); t < ET(2025, 7, 1); t = t.AddMinutes(1)) { if (Est.IsInvalidTime(t)) continue; if (e.TradingDayOf(U(t)).DayOfWeek == DayOfWeek.Saturday) continue; Bar(e, t); e.IsInWindow(EEventTier.T3, 5, 10); e.EventsOn(e.TradingDay); n++; }
		sw.Stop();
		Console.WriteLine($"perf: {n} bars in {sw.ElapsedMilliseconds} ms");
		Check(sw.ElapsedMilliseconds < 20000, "perf half-year of 1m bars");
	}
}
