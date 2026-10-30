using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for M-EVENT (specs/m-event-spec.md rev 2, EV-8). PR A: rule kinds + synthetic records via the table /
// corrections seams. PR B: EV-8.2 integrity of the generated table (TableIntegrity) and the real EV-8.5 cases (RealData).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static readonly TimeZoneInfo Est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
	static DateTime U(DateTime et) { return TimeZoneInfo.ConvertTimeToUtc(et, Est); }
	static DateTime ET(int y, int m, int d, int h = 0, int mi = 0) { return new DateTime(y, m, d, h, mi, 0); }

	static _EventCalendar Eng(Action<EventConfig> f = null, List<EventRec> table = null, EventCorrection[] corr = null, List<string> j = null, List<string> w = null, int barMin = 1)
	{
		// synthetic tables start mid-history: partial coverage on by default (EV-4.6b has its own tests in FirstBarCoverage)
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
	const string RollDigestPin = "fdd63bd8f179a8d4";
	static string FindUp(string rel)
	{
		for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent) { string p = Path.Combine(d.FullName, rel); if (File.Exists(p)) return p; }
		for (var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); d != null; d = d.Parent) { string p = Path.Combine(d.FullName, rel); if (File.Exists(p)) return p; }
		throw new FileNotFoundException(rel);
	}
	static List<EventRec> Gen(int y) { return EventRules.Generate(y, false); }
	static bool Has(List<EventRec> l, EEventKind k, DateTime et) { return l.Any(r => r.kind == k && r.etTime == et); }
	static EventRec T1(EEventKind k, DateTime et, EEventFlags f = EEventFlags.None) { var r = EventRec.Make(k, et, EventRules.DefaultTier(k), f); return r; }

	static void Main()
	{
		Rules(); TimeAndDays(); Windows(); AsOf(); HalfDays(); Roll(); Csv(); NextPrev(); ConfigCanon(); EmptyTable(); SessionOpens(); BlackoutMacroOnly(); EiaRuleOnly(); FirstBarCoverage(); TableIntegrity(); RealData(); Pr27(); NoCoverageFlag(); Perf();
		{   // PR D / PR #46 review: _Test6 RollDates = EventRules.RollDate, pinned to CME-published roll dates (not the generated CSV)
			var cme = new[] { new DateTime(2021, 12, 9), new DateTime(2022, 3, 10), new DateTime(2022, 6, 13), new DateTime(2022, 9, 12),
				new DateTime(2025, 3, 17), new DateTime(2026, 6, 15), new DateTime(2027, 9, 13) };
			foreach (var d in cme) Check(EventRules.RollDate(d.Year, d.Month) == d, "CME roll date " + d.ToString("yyyy-MM-dd"));
			// The generated table must match the rule too. NOTE: needs tools/m-event/out/roll_splices_2016_2024.csv on disk (FindUp from the
			// test directory); a checkout without the file fails here with FileNotFoundException.
			string rp = FindUp("tools/m-event/out/roll_splices_2016_2024.csv");
			var rows = File.ReadAllLines(rp).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).ToList();
			bool all = rows.Count == 36;
			foreach (var r in rows)
			{
				DateTime exp = DateTime.ParseExact(r[1], "yyyy-MM-dd", CultureInfo.InvariantCulture), rr = DateTime.ParseExact(r[2], "yyyy-MM-dd", CultureInfo.InvariantCulture);
				if (EventRules.RollDate(exp.Year, exp.Month) != rr) { all = false; Console.WriteLine("roll table mismatch " + r[0]); }
			}
			Check(all, "PR D: roll_splices_2016_2024.csv rule_roll was regenerated from EventRules.RollDate (36 rows)");
			// PR #46 review Q1: _Test6's RollDates (2000-2035) digest = A263Config.RollDatesDigest2000To2035 (also pinned in RegimeGateTests)
			var set = new List<DateTime>(); for (int y = 2000; y <= 2035; y++) for (int m = 3; m <= 12; m += 3) set.Add(EventRules.RollDate(y, m));
			string dg = PowerLanguage.Function._RollDetector.DatesDigest(set);
			Console.WriteLine("roll dates digest 2000-2035 = " + dg);
			Check(set.Count == 144 && dg == RollDigestPin, "_Test6 roll-date digest pinned (re-pin = new build, update A263Config.RollDatesDigest2000To2035)");
		}
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
			{ 2027, 3, 19, 15 }, { 2027, 6, 17, 14 }, { 2027, 9, 17, 13 }, { 2027, 12, 17, 13 }, { 2028, 3, 17, 13 }, { 2028, 6, 16, 12 }, { 2028, 9, 15, 11 }, { 2028, 12, 15, 11 } };
		for (int i = 0; i < cme.GetLength(0); ++i)
		{
			int y = cme[i, 0], m = cme[i, 1];
			// R2.1-d as amended (PR27): every row (incl. 2026-06 / 2027-06 Juneteenth) comes from the NYSE-business-day rule.
			Check(EventRules.ExpiryDate(y, m) == new DateTime(y, m, cme[i, 2]), "CME expiry " + y + "-" + m);
			Check(EventRules.RollDate(y, m) == new DateTime(y, m, cme[i, 3]), "CME roll " + y + "-" + m);
		}
		// R2.1-d amended (PR27 review) / EV-1.20: third Friday, back to the preceding NYSE business day. CME per-contract calendar
		// (ProductCalendar/Future/133): ESM27 last trade Thu 2027-06-17 (Juneteenth); no override table any more.
		Check(EventRules.RuleExpiryDate(2027, 6) == new DateTime(2027, 6, 17) && EventRules.ExpiryDate(2027, 6) == new DateTime(2027, 6, 17) && EventRules.OpexDate(2027, 6) == new DateTime(2027, 6, 17), "2027-06 ESM27 06-17 from the rule, SPX opex 06-17");
		Check(Has(g27, EEventKind.QuarterlyExpiry, ET(2027, 6, 17, 9, 30)) && Has(g27, EEventKind.RollWeek, ET(2027, 6, 14)), "2027-06 QuarterlyExpiry 06-17 + roll span");
		// regression: Good Friday 2008-03-21 -> ESH08 expired Thu 2008-03-20
		Check(EventRules.ExpiryDate(2008, 3) == new DateTime(2008, 3, 20), "ESH08 expiry 2008-03-20 (Good Friday)");
		for (int y = 2016; y <= 2035; ++y) for (int m = 3; m <= 12; m += 3) Check(EventRules.ExpiryDate(y, m) == EventRules.OpexDate(y, m), "expiry == opex rule " + y + "-" + m);
		// single NYSE holiday source: tools/m-event/nyse_holidays.txt (read by build_table.py) == _NyseHolidays.Rules 2000-2035
		{
			string hp = FindUp("tools/m-event/nyse_holidays.txt");
			var fileDays = new HashSet<DateTime>(File.ReadAllLines(hp).Where(l => l.Trim().Length > 0 && !l.StartsWith("#")).Select(l => DateTime.ParseExact(l.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture)));
			var csDays = new HashSet<DateTime>();
			for (DateTime d = new DateTime(2000, 1, 1); d.Year <= 2035; d = d.AddDays(1)) if (EventRules.IsNyseHoliday(d)) csDays.Add(d);
			Check(fileDays.SetEquals(csDays), "nyse_holidays.txt == _NyseHolidays 2000-2035 (" + fileDays.Count + " vs " + csDays.Count + ")");
		}
		// whole range: every quarterly expiry is an NYSE business day and a Friday unless an NYSE closure moved it
		for (int y = 2016; y <= 2028; ++y)
			for (int m = 3; m <= 12; m += 3)
			{
				DateTime x = EventRules.ExpiryDate(y, m), f = EventRules.ThirdFriday(y, m);
				Check(x <= f && x > f.AddDays(-7) && x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday && EventRules.IsNyseBusinessDay(x), "expiry valid " + y + "-" + m);
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

	// ---- PR H: NoCoverage flag on query results + warn once for a queried class that was not built
	static void NoCoverageFlag()
	{
		var tbl = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) };
		var w = new List<string>();
		var e = new _EventCalendar { config = new EventConfig { enabled = true }, table = tbl, corrections = new EventCorrection[0], barDuration = TimeSpan.FromMinutes(1), firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		e.warnOnce = s => w.Add(s);
		Check(e.Configure() == null, "H: configure");
		Bar(e, ET(2026, 1, 5, 9, 0));
		// day / kind
		Check(e.NoCoverage(EEventKind.Cpi, new DateTime(2026, 1, 5)) && e.NoCoverage(new DateTime(2026, 1, 5), EEventClassMask.Macro), "H: Unknown day -> NoCoverage (kind, day)");
		Check(!e.NoCoverage(EEventKind.Cpi, new DateTime(2026, 3, 12)) && !e.NoCoverage(new DateTime(2026, 3, 12), EEventClassMask.Macro), "H: covered day -> no NoCoverage");
		Check(e.NoCoverage(EEventKind.EiaPetroleum, new DateTime(2026, 3, 12)), "H: kind of an unbuilt class (Energy) -> NoCoverage");
		Check(!e.NoCoverage(new DateTime(2026, 1, 5), EEventClassMask.Calendar), "H: Unknown Macro kind does not flag a Calendar-only mask");
		// EventDay
		Check(e.ClassifyExPost(new DateTime(2026, 1, 5)).noCoverage, "H: EventDay.noCoverage on an Unknown day");
		// window result: the blocking record is marked NoCoverage, a real event is not
		EventRec r;
		Check(e.IsInWindowAt(U(ET(2026, 1, 5, 8, 59)), U(ET(2026, 1, 5, 9, 0)), EEventTier.T1, 5, 10, EEventClassMask.Macro, out r) && r.NoCoverage && (r.flags & EEventFlags.NoCoverage) != 0, "H: fail-closed window result carries NoCoverage");
		Bar(e, ET(2026, 3, 11, 8, 31));
		Check(e.IsInWindowAt(U(ET(2026, 3, 11, 8, 30)), U(ET(2026, 3, 11, 8, 31)), EEventTier.T1, 5, 10, EEventClassMask.Macro, out r) && !r.NoCoverage && r.kind == EEventKind.Cpi, "H: real event result has no NoCoverage");
		// year: the synthetic table has no table kinds other than Cpi -> 2026 lacks table kinds
		Check(e.NoCoverageYear(2026, EEventClassMask.Macro) && !e.NoCoverageYear(2026, EEventClassMask.Energy), "H: NoCoverageYear for a year built without table kinds; unbuilt class never counts");
		// warn once per unbuilt class, never for All or built classes
		w.Clear();
		e.NextEvent(EEventTier.T1, out r);
		e.EventsOn(new DateTime(2026, 3, 11));
		Check(w.Count == 0, "H: no warning for Macro / All masks");
		e.IsInWindow(EEventTier.T2, 0, 0, EEventClassMask.Energy);
		e.EventsOnExPost(new DateTime(2026, 3, 11), false, EEventClassMask.Energy);
		e.NextEvent(e.NowUtc, EEventTier.None, EEventClassMask.Energy | EEventClassMask.Macro, out r);
		Check(w.Count(s => s.Contains("not built")) == 1 && w[0].Contains("Energy"), "H: unbuilt class warns once (" + string.Join(" | ", w) + ")");
		var e4 = Eng(c => c.classes = EEventClassMask.Macro, tbl, null, null, w);
		Bar(e4, ET(2026, 3, 12, 9, 0)); w.Clear(); e4.PrevEvent(e4.NowUtc, EEventTier.None, EEventClassMask.Calendar | EEventClassMask.Energy, out r);
		e4.PrevEvent(e4.NowUtc, EEventTier.None, EEventClassMask.Calendar, out r);
		Check(w.Count == 1 && w[0].Contains("Calendar") && w[0].Contains("Energy"), "H: one warning naming both missing classes, then silent");
		// disabled engine (hash mismatch) has no coverage anywhere
		var bad = new _EventCalendar { config = new EventConfig { enabled = true }, corrections = null, barDuration = TimeSpan.FromMinutes(1), tableShaExpected = "0000000000000000" };
		bad.error = s => { }; bad.Configure();
		Check(bad.Disabled && bad.NoCoverage(new DateTime(2026, 3, 12)) && bad.NoCoverageYear(2026), "H: disabled engine -> NoCoverage");
		Check(bad.NoCoverage(EEventKind.Cpi, new DateTime(2026, 3, 12)), "H: disabled engine -> NoCoverage(kind, day)");
		// unbuilt-class check in IsInWindowAt and EventsOn (fresh engines, Macro|Calendar built)
		var wa = new List<string>(); var ea = Eng(null, tbl, null, null, wa); Bar(ea, ET(2026, 3, 12, 9, 0)); wa.Clear();
		ea.IsInWindowAt(ea.NowUtc, ea.NowUtc, EEventTier.None, 0, 0, EEventClassMask.Energy, out r);
		Check(wa.Count(s => s.Contains("not built") && s.Contains("Energy")) == 1, "H: IsInWindowAt warns for an unbuilt class (" + string.Join(" | ", wa) + ")");
		var wb = new List<string>(); var eb = Eng(null, tbl, null, null, wb); Bar(eb, ET(2026, 3, 12, 9, 0)); wb.Clear();
		eb.EventsOn(new DateTime(2026, 3, 11), EEventClassMask.Energy);
		Check(wb.Count(s => s.Contains("not built") && s.Contains("Energy")) == 1, "H: EventsOn warns for an unbuilt class (" + string.Join(" | ", wb) + ")");
		// re-Configure resets the warned set
		eb.Configure(); Bar(eb, ET(2026, 3, 12, 9, 1)); wb.Clear();
		eb.EventsOn(new DateTime(2026, 3, 11), EEventClassMask.Energy);
		Check(wb.Count(s => s.Contains("not built")) == 1, "H: re-Configure resets warn-once");
		// real table, partial=true still reports NoCoverage on a hole day even though it does not block
		var real = new _EventCalendar { config = new EventConfig { enabled = true, allowPartialCoverage = true }, table = EventRules.Table(), corrections = EventRules.Corrections, coverageHoles = EventRules.CoverageHoles(), barDuration = TimeSpan.FromMinutes(1) };
		Check(real.Configure() == null, "H: real configure");
		Bar(real, ET(2020, 9, 15, 10, 0));
		Check(real.NoCoverage(EEventKind.GdpAdvance, new DateTime(2020, 9, 15)) && real.Classify(new DateTime(2020, 9, 15)).noCoverage && !real.IsInWindow(EEventTier.None, 0, 0), "H: hole day NoCoverage under partial=true (not blocking)");
		Check(!real.NoCoverage(EEventKind.Cpi, new DateTime(2020, 9, 11)), "H: covered kind on a covered day");
	}

	// ---- EV-4.6b: a kind starting after the first bar is Unknown before its coverage (no engine disable)
	static void FirstBarCoverage()
	{
		var c = new EventConfig { enabled = true };
		var e = new _EventCalendar { config = c, table = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }, corrections = new EventCorrection[0], barDuration = TimeSpan.FromMinutes(1), firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		var errs = new List<string>(); e.error = s => errs.Add(s);
		Check(e.Configure() == null, "coverage shortfall: Configure never fails");
		Bar(e, ET(2026, 1, 5, 9, 0));
		Check(!e.Disabled && errs.Count == 0, "EV-4.6b: coverage start after the first bar never disables");
		Check(e.Coverage(EEventKind.Cpi, new DateTime(2026, 1, 5)) == EEventCoverage.Unknown && e.IsInWindow(EEventTier.T1, 5, 10), "before coverage start: Unknown, blocks (partial=false)");
		Bar(e, ET(2026, 3, 11, 8, 31));
		Check(e.Coverage(EEventKind.Cpi, new DateTime(2026, 3, 11)) == EEventCoverage.Event && e.IsInWindow(EEventTier.T1, 5, 10) && e.EventsOn(new DateTime(2026, 3, 11)).Count == 1, "covered month: Event");
		Check(e.Coverage(EEventKind.Cpi, new DateTime(2026, 3, 12)) == EEventCoverage.NoEvent, "covered day without a release: NoEvent");
		var e3 = Eng(c3 => c3.allowPartialCoverage = false, new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }); var errs3 = new List<string>(); e3.error = s => errs3.Add(s);
		Bar(e3, ET(2026, 1, 5, 9, 0));
		Check(!e3.Disabled && errs3.Count == 0 && e3.IsInWindow(EEventTier.T1, 5, 10), "no host firstBarUtc: Unknown blocks, no disable");
		// effective tier (config.tierOverride) decides the minTier filter for Unknown, not the default tier
		Check(EventRules.DefaultTier(EEventKind.Cpi) == EEventTier.T1, "Cpi default tier T1");
		var eo = Eng(co => { co.allowPartialCoverage = false; co.tierOverride[EEventKind.Cpi] = EEventTier.T3; }, new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) });
		Bar(eo, ET(2026, 1, 5, 9, 0));
		EEventKind ok_;
		Check(!eo.IsInWindow(EEventTier.T1, 5, 10) && !eo.CoverageBlocks(new DateTime(2026, 1, 5), EEventClassMask.Macro, EEventTier.T1, out ok_), "tierOverride Cpi=T3: Unknown does not block a T1 query");
		Check(eo.IsInWindow(EEventTier.T3, 5, 10) && eo.CoverageBlocks(new DateTime(2026, 1, 5), EEventClassMask.Macro, EEventTier.T3, out ok_) && ok_ == EEventKind.Cpi, "tierOverride Cpi=T3: blocks a T3 query");
		var e2 = new _EventCalendar { config = new EventConfig { enabled = true, allowPartialCoverage = true }, table = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }, corrections = new EventCorrection[0], firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		Check(e2.Configure() == null, "allowPartialCoverage -> ok");
		Bar(e2, ET(2026, 1, 5, 9, 0)); Check(!e2.Disabled && !e2.IsInWindow(EEventTier.T1, 5, 10), "allowPartialCoverage -> Unknown counts as NoEvent");
		// engine-wide disable only for a missing table / hash mismatch
		Check(EventRules.TableIntegrity(EventRules.TableRowCount, EventRules.TableSha(), EventRules.ExpectedTableSha) == null, "compiled table hash == build_table.py hash");
		Check(EventRules.TableIntegrity(0, "x", "x") == "event table missing", "missing table detected");
		var bad = new _EventCalendar { config = new EventConfig { enabled = true }, corrections = null, barDuration = TimeSpan.FromMinutes(1), tableShaExpected = "0000000000000000" };
		var errsB = new List<string>(); bad.error = s => errsB.Add(s);
		Check(bad.Configure() == null && bad.Disabled && errsB.Count == 1 && errsB[0].Contains("hash") && errsB[0].EndsWith("engine disabled"), "hash mismatch disables the engine: " + string.Join("|", errsB));
		var good = new _EventCalendar { config = new EventConfig { enabled = true }, barDuration = TimeSpan.FromMinutes(1) };
		Check(good.Configure() == null && !good.Disabled, "compiled table intact -> enabled");
		// R2.1-d amendment: historical NYSE closures in the single source; CME equity-futures status from CME notices
		foreach (var d in new[] { new DateTime(2001, 9, 11), new DateTime(2001, 9, 12), new DateTime(2001, 9, 13), new DateTime(2001, 9, 14), new DateTime(2004, 6, 11), new DateTime(2007, 1, 2), new DateTime(2012, 10, 29), new DateTime(2012, 10, 30) })
			Check(EventRules.IsNyseHoliday(d) && !EventRules.IsNyseBusinessDay(d), "NYSE closed " + d.ToString("yyyy-MM-dd"));
		Check(EventRules.IsNyseBusinessDay(new DateTime(2001, 9, 17)) && EventRules.IsNyseBusinessDay(new DateTime(2012, 10, 31)), "NYSE reopened 2001-09-17 / 2012-10-31");
		Func<DateTime, _CmeHolidays.Rules.Day> cme = _CmeHolidays.Rules.Get;
		var d911 = cme(new DateTime(2001, 9, 11));
		Check(!d911.Closed && d911.NoRth && d911.HaltEt == new TimeSpan(9, 30, 0), "CME 2001-09-11 equity Globex halted 08:30 CT");
		Check(new[] { 12, 13, 14 }.All(x => cme(new DateTime(2001, 9, x)).Closed), "CME 2001-09-12..14 equity index closed");
		Check(!cme(new DateTime(2001, 9, 17)).NoRth, "CME 2001-09-17 normal");
		var r04 = cme(new DateTime(2004, 6, 11)); Check(!r04.Closed && r04.NoRth && r04.HaltEt == new TimeSpan(9, 0, 0), "CME 2004-06-11 Reagan: Globex to 08:00 CT");
		var f07 = cme(new DateTime(2007, 1, 2)); Check(!f07.Closed && f07.NoRth && f07.HaltEt == new TimeSpan(9, 15, 0), "CME 2007-01-02 Ford: halt 08:15 CT");
		Check(new[] { 29, 30 }.All(x => { var q = cme(new DateTime(2012, 10, x)); return !q.Closed && q.NoRth && q.HaltEt == new TimeSpan(9, 15, 0); }), "CME 2012-10-29/30 Sandy: halt 08:15 CT");
		Check(!cme(new DateTime(2012, 10, 31)).NoRth, "CME 2012-10-31 normal");
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
		string errNc = Eng().LoadCsv("action,kind,date_et,time_et,tier,flags,ref_period,announced_et,source\nadd,Cpi,2026-02-11,08:30,T1,Scheduled|NoCoverage,Jan 2026,,x\n");
		Check(errNc != null && errNc.Contains("line 2") && errNc.Contains("NoCoverage"), "CSV rejects the query-only NoCoverage flag: " + errNc);
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
		Check(j.Any(s => s.StartsWith("EVENT table kinds missing: FomcDecision,") && s.Contains("Cpi") && s.Contains("Nfp")), "journal EVENT table kinds missing (R2.1-a)");
		Bar(e, ET(2026, 3, 12, 7, 0));
		Check(!e.IsInWindow(EEventTier.T1, 30, 30) && !e.Classify(new DateTime(2026, 3, 12)).hasFomc, "neutral outputs");
		Check(j.Any(s => s.StartsWith("EVENT day 2026-03-12") && s.Contains("Claims@08:30")), "day journal line");
		Check(EventRules.Table().Count > 1000 && EventRules.Corrections.Length > 0, "PR B table generated");
	}

	// ---- EV-8.2 integrity of the generated table (PR B)
	static void TableIntegrity()
	{
		var t = EventRules.Table(); var cx = EventRules.Corrections;
		var corrected = new HashSet<string>(cx.Where(c => c.originalEt != 0).Select(c => c.kind + "@" + c.originalEt));
		Func<EventRec, bool> isCx = r => corrected.Contains(r.kind + "@" + EventRules.ToYmdhm(r.etTime));
		for (int y = 2016; y <= 2027; ++y)
		{
			var dec = t.Where(r => r.kind == EEventKind.FomcDecision && r.etTime.Year == y && (r.flags & EEventFlags.Unscheduled) == 0).ToList();
			Check(dec.Count == 8, "FOMC " + y + ": 8 scheduled meetings (" + dec.Count + ")");
			// 2020: the March 17-18 SEP meeting was canceled (the table keeps the original row; s_corrections cancels it), but the
			// Fed's 2020 page lists no SEP for it, so 2020 has 3 SEP rows (PR27 review: kept as a comment, see the region NOTE).
			Check(dec.Count(r => (r.flags & EEventFlags.Sep) != 0) == (y == 2020 ? 3 : 4), "FOMC " + y + ": SEP count");
			foreach (var d in dec)
			{
				var mins = t.Where(r => r.kind == EEventKind.FomcMinutes && r.refPeriod == d.refPeriod).ToList();
				if (mins.Count == 0) { if (!isCx(d)) Console.WriteLine("EV-8.2 report: no minutes row for " + d.refPeriod); continue; }
				if (mins[0].etTime.Date != d.etTime.Date.AddDays(21)) Console.WriteLine("EV-8.2 report: minutes " + d.refPeriod + " " + mins[0].etTime.ToString("yyyy-MM-dd") + " != decision + 21");
			}
			foreach (var k in new[] { EEventKind.Cpi, EEventKind.Ppi, EEventKind.Nfp })
			{
				var rows = t.Where(r => r.kind == k && r.etTime.Year == y).ToList();
				Check(rows.Count == 12 && rows.Select(r => r.refPeriod).Distinct().Count() == 12, k + " " + y + ": one release per reference month (" + rows.Count + ")");
			}
		}
		foreach (var k in new[] { EEventKind.Cpi, EEventKind.Ppi, EEventKind.Nfp, EEventKind.Jolts, EEventKind.RetailSales, EEventKind.Pce, EEventKind.GdpAdvance, EEventKind.GdpSecond, EEventKind.GdpThird })
		{
			var refs = t.Where(r => r.kind == k).GroupBy(r => r.refPeriod).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
			Check(refs.Count == 0, k + ": duplicate reference periods " + string.Join(",", refs));
		}
		var ok = new HashSet<TimeSpan> { new TimeSpan(8, 30, 0), new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0), new TimeSpan(14, 0, 0), new TimeSpan(14, 30, 0) };
		int badT = 0, hol = 0;
		foreach (var r in t)
		{
			bool flagged = (r.flags & (EEventFlags.Unscheduled | EEventFlags.Delayed)) != 0 || isCx(r);
			if (!ok.Contains(r.etTime.TimeOfDay) && !flagged) { badT++; Console.WriteLine("FAIL time " + r.kind + " " + r.etTime); }
			if (EventRules.IsFederalHoliday(r.etTime.Date) && !flagged) { hol++; Console.WriteLine("FAIL holiday " + r.kind + " " + r.etTime); }
		}
		Check(badT == 0, "table times in {08:30,10:00,10:30,14:00,14:30} unless flagged");
		Check(hol == 0, "no release on a federal holiday unless corrected");
		Check(t.GroupBy(r => r.kind + "@" + r.etTime.Ticks).All(g => g.Count() == 1), "no duplicate rows");
		Check(cx.All(c => !string.IsNullOrEmpty(c.source)), "every correction cites a source");
	}

	// ---- EV-8.5 real data (PR B)
	static void RealData()
	{
		Func<_EventCalendar> eng = () => Eng(c => c.allowPartialCoverage = false, EventRules.Table(), EventRules.Corrections);
		EventRec n;
		// 2020-03-03 10:00 unscheduled FOMC cut
		var e = eng();
		Bar(e, ET(2020, 3, 3, 9, 45));
		Check(!e.IsInWindow(EEventTier.T1, 30, 30), "2020-03-03: no pre-window at 09:45 (unscheduled)");
		Check(!e.NextEvent(EEventTier.T1, out n) || n.etTime != ET(2020, 3, 3, 10, 0), "2020-03-03: not visible to NextEvent before 10:00");
		Bar(e, ET(2020, 3, 3, 10, 15));
		Check(e.IsInWindow(EEventTier.T1, 30, 30), "2020-03-03: post-window at 10:15");
		Check(e.PrevEvent(EEventTier.T1, out n) && n.etTime == ET(2020, 3, 3, 10, 0) && n.kind == EEventKind.FomcDecision, "2020-03-03: PrevEvent = FOMC 10:00");
		Check(!e.Classify(new DateTime(2020, 3, 3)).hasFomc && e.ClassifyExPost(new DateTime(2020, 3, 3)).hasFomc, "2020-03-03: as-of no FOMC, ex-post FOMC");
		// 2020-03-15 17:00 (Sunday) -> trade day 03-16; scheduled 03-18 meeting canceled from 03-15 17:00
		Check(e.ClassifyExPost(new DateTime(2020, 3, 16)).hasFomc, "2020-03-15 17:00 lands on trade day 03-16");
		Check(!e.EventsOnExPost(new DateTime(2020, 3, 18)).Any(r => r.kind == EEventKind.FomcDecision), "2020-03-18 decision canceled");
		// NFP Sep 2025 (lapse): original 2025-10-03 08:30 canceled from 2025-10-01 00:01
		var e2 = eng();
		Bar(e2, ET(2025, 10, 3, 8, 25));
		Check(!e2.IsInWindow(EEventTier.T1, 10, 10), "NFP Sep 2025: no pre-window at 2025-10-03 08:25");
		// CPI Sep 2025: 10-15 -> 10-24 08:30
		Check(!e2.EventsOnExPost(new DateTime(2025, 10, 15)).Any(r => r.kind == EEventKind.Cpi), "CPI Sep 2025: nothing on 10-15");
		Check(e2.EventsOnExPost(new DateTime(2025, 10, 24)).Any(r => r.kind == EEventKind.Cpi && r.etTime == ET(2025, 10, 24, 8, 30)), "CPI Sep 2025: 10-24 08:30");
		// CPI Oct 2025: canceled (original 11-13)
		Check(!e2.EventsOnExPost(new DateTime(2025, 11, 13)).Any(r => r.kind == EEventKind.Cpi) && e2.EventsOnExPost(new DateTime(2025, 11, 13), true).Any(r => r.kind == EEventKind.Cpi), "CPI Oct 2025 canceled");
	}

	// ---- PR27 review (analysts 1+2): roll cutover, coverage holes/end, DST, half day, widths, hash
	static void Pr27()
	{
		// roll cutover 2022-04-01 (Wayback 20220610060819 lists ESM22 Mon 6/13; PR #46 review)
		Check(EventRules.RollDate(2022, 6) == new DateTime(2022, 6, 13) && EventRules.RollDate(2022, 9) == new DateTime(2022, 9, 12), "ESM22 rolls Mon 06-13, ESU22 Mon 09-12");
		Check(EventRules.RollDate(2022, 3) == new DateTime(2022, 3, 10), "ESH22 rolls Thu 03-10");
		// FromYmdhm width assert
		bool t8 = false, t13 = false;
		try { EventRules.FromYmdhm(20260101L); } catch (ArgumentException) { t8 = true; }
		try { EventRules.FromYmdhm(2026010108300L); } catch (ArgumentException) { t13 = true; }
		Check(t8 && t13 && EventRules.FromYmdhm(202601010830L) == new DateTime(2026, 1, 1, 8, 30, 0), "FromYmdhm rejects non-12-digit values");
		// corrections alone change the table hash (and so EventConfig.Canon)
		long[] et = { 202601020830L }; byte[] kd = { 4 }; ushort[] fl = { 0 }; string[] rf = { "2025-12" };
		var c1 = new[] { new EventCorrection { kind = EEventKind.Cpi, originalEt = 202601020830L, actualEt = 202601050830L, source = "x" } };
		var c2 = new[] { new EventCorrection { kind = EEventKind.Cpi, originalEt = 202601020830L, actualEt = 202601060830L, source = "x" } };
		Check(EventRules.ComputeTableSha(et, kd, fl, rf, c1, null) != EventRules.ComputeTableSha(et, kd, fl, rf, c2, null), "corrections-only change -> TableSha changes");
		Check(EventRules.ComputeTableSha(et, kd, fl, rf, c1, null) != EventRules.ComputeTableSha(et, kd, fl, rf, c1, new long[] { 4, 202601010000L, 202601312359L }), "holes-only change -> TableSha changes");
		Check(new EventConfig().Canon().Contains(EventRules.TableSha()), "Canon carries TableSha");
		// whole table loads with zero rejections; no row on a DST-skipped or ambiguous ET minute 2016-2027
		var jl = new List<string>();
		var full = Eng(c => c.allowPartialCoverage = false, EventRules.Table(), EventRules.Corrections, jl);
		Check(!jl.Any(x => x.Contains("reject") || x.Contains("invalid")), "real table: zero rejections");
		var est = TimeZoneInfo.FindSystemTimeZoneById(TzId());
		int dstDays = 0;
		for (DateTime d = new DateTime(2016, 1, 1); d.Year <= 2027; d = d.AddDays(1))
			if (est.IsDaylightSavingTime(d.AddHours(12)) != est.IsDaylightSavingTime(d.AddDays(-1).AddHours(12))) dstDays++;
		Check(dstDays == 24, "24 DST changes 2016-2027 (" + dstDays + ")");
		Check(EventRules.Table().All(r => !est.IsInvalidTime(r.etTime) && !est.IsAmbiguousTime(r.etTime)), "no table row on a DST gap/overlap minute");
		Check(EventRules.Table().Concat(EventRules.Corrections.Where(c => c.actualEt != 0).Select(c => EventRec.Make(c.kind, EventRules.FromYmdhm(c.actualEt), EEventTier.T1, EEventFlags.None)))
			.All(r => full.TradingDayOf(U(r.etTime)) >= r.etTime.Date.AddDays(-1)), "every row maps to a trading day");
		// Sunday 2020-03-15 17:00 (unscheduled cut) lands on the 03-16 trading day: EventsOn and IsInWindow
		var e = Eng(c => c.allowPartialCoverage = false, EventRules.Table(), EventRules.Corrections);
		Bar(e, ET(2020, 3, 15, 17, 10));
		Check(e.IsInWindow(EEventTier.T1, 30, 30), "2020-03-15 17:10: in the post-window of the 17:00 cut");
		Check(e.EventsOn(new DateTime(2020, 3, 16)).Any(r => r.kind == EEventKind.FomcDecision && r.etTime == ET(2020, 3, 15, 17, 0)), "EventsOn(03-16) holds the 03-15 17:00 decision");
		Check(!e.EventsOn(new DateTime(2020, 3, 13)).Any(r => r.etTime == ET(2020, 3, 15, 17, 0)), "not on Fri 03-13");
		// half day 2026-11-27: a pre-halt event is labelled on its own day; its window does not leak into the next session
		var h = Eng(null, new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 11, 27, 12, 30)) });
		Bar(h, ET(2026, 11, 27, 12, 45));
		Check(h.Classify(new DateTime(2026, 11, 27)).isHalfDay && h.EventsOn(new DateTime(2026, 11, 27)).Any(r => r.kind == EEventKind.Cpi), "half day 2026-11-27: Cpi 12:30 on its own day");
		Check(h.IsInWindow(EEventTier.T1, 30, 120), "half day: in window before the halt");
		Bar(h, ET(2026, 11, 29, 18, 5));
		Check(!h.IsInWindow(EEventTier.T1, 30, 120) && !h.EventsOn(new DateTime(2026, 11, 30)).Any(r => r.kind == EEventKind.Cpi), "half day: window does not leak into the 11-30 session");
		// EV-4.6b: per-kind, per-day coverage (tri-state); holes never disable the engine
		Func<bool, List<string>, EEventClassMask, _EventCalendar> real = (partial, jl0, mask) =>
		{
			var x = new _EventCalendar { config = new EventConfig { enabled = true, allowPartialCoverage = partial, classes = mask }, table = EventRules.Table(), corrections = EventRules.Corrections,
				coverageHoles = EventRules.CoverageHoles(), barDuration = TimeSpan.FromMinutes(1) };
			if (jl0 != null) x.journal = s => jl0.Add(s);
			x.error = s => { throw new Exception("error sink: " + s); };
			if (x.Configure() != null) throw new Exception("configure");
			return x;
		};
		var all = EEventClassMask.Macro | EEventClassMask.Calendar;
		// (1) a hole day returns Unknown (GDP 2020-09 hole), the kinds around it stay tri-state
		var s1 = real(false, null, all); Bar(s1, ET(2020, 9, 15, 10, 0));
		Check(s1.Coverage(EEventKind.GdpAdvance, new DateTime(2020, 9, 15)) == EEventCoverage.Unknown, "EV-4.6b(1) GdpAdvance hole day 2020-09-15 -> Unknown");
		Check(s1.Coverage(EEventKind.Cpi, new DateTime(2020, 9, 11)) == EEventCoverage.Event && s1.Coverage(EEventKind.Cpi, new DateTime(2020, 9, 15)) == EEventCoverage.NoEvent, "Cpi 2020-09: Event / NoEvent");
		Check(!s1.Disabled, "hole never disables the engine");
		// (2) blocks under allowPartialCoverage = false
		EEventKind bk;
		Check(s1.CoverageBlocks(new DateTime(2020, 9, 15), EEventClassMask.Macro, EEventTier.None, out bk) && s1.IsInWindow(EEventTier.None, 0, 0, EEventClassMask.Macro), "EV-4.6b(2) hole day blocks under partial=false (" + bk + ")");
		// (3) NoEvent under allowPartialCoverage = true
		var s3 = real(true, null, all); Bar(s3, ET(2020, 9, 15, 10, 0));
		Check(s3.Coverage(EEventKind.Pce, new DateTime(2022, 12, 15)) == EEventCoverage.Unknown && !s3.CoverageBlocks(new DateTime(2020, 9, 15), EEventClassMask.Macro) && !s3.IsInWindow(EEventTier.T1, 0, 0, EEventClassMask.Macro), "EV-4.6b(3) partial=true: Unknown counts as NoEvent");
		// (4) end of coverage (JOLTS after 2026-12) behaves like a hole
		var s4 = real(false, null, all); Bar(s4, ET(2027, 1, 4, 10, 0));
		Check(s4.Coverage(EEventKind.Jolts, new DateTime(2027, 1, 4)) == EEventCoverage.Unknown && s4.Coverage(EEventKind.Jolts, new DateTime(2026, 12, 31)) != EEventCoverage.Unknown, "EV-4.6b(4) JOLTS after 2026-12 -> Unknown");
		Check(s4.CoverageBlocks(new DateTime(2027, 1, 4), EEventClassMask.Macro, EEventTier.None, out bk) && !s4.Disabled, "EV-4.6b(4) end of coverage blocks like a hole, engine enabled");
		// (5) a hole for a kind not in the mask never blocks: 2022-12 PCE hole, consumer mask without PCE's class
		var pceCls = EventRules.ClassOf(EEventKind.Pce, EventRules.DefaultTier(EEventKind.Pce));
		var s5 = real(false, null, all); Bar(s5, ET(2022, 12, 15, 10, 0));
		var unk = new List<EEventKind>(); foreach (EEventKind k in Enum.GetValues(typeof(EEventKind))) if (s5.Coverage(k, new DateTime(2022, 12, 15)) == EEventCoverage.Unknown) unk.Add(k);
		var other = EEventClassMask.Calendar | EEventClassMask.Energy;
		Check(unk.Contains(EEventKind.Pce) && unk.All(k => (EventRules.ClassOf(k, EventRules.DefaultTier(k)) & other) == 0) && !s5.CoverageBlocks(new DateTime(2022, 12, 15), other) && !s5.IsInWindow(EEventTier.None, 0, 0, other),
			"EV-4.6b(5) PCE hole (" + pceCls + ") never blocks a mask without it [" + string.Join(",", unk) + "]");
		Check(s5.CoverageBlocks(new DateTime(2022, 12, 15), pceCls), "same day blocks a mask with PCE's class");
		// (6) tier filter: an Unknown hole for a kind below the query's minimum tier never blocks; at/above it does
		Func<EEventTier?, List<string>, EEventTier, _EventCalendar> t6 = (ovr, jl6, cmin) =>
		{
			var x = new _EventCalendar { config = new EventConfig { enabled = true, allowPartialCoverage = false, minTier = cmin },
				table = new List<EventRec> { T1(EEventKind.Cpi, ET(2026, 1, 13, 8, 30)), T1(EEventKind.Cpi, ET(2026, 3, 11, 8, 30)) }, corrections = new EventCorrection[0],
				coverageHoles = new long[] { (long)EEventKind.Cpi, 202602100000, 202602122359 }, barDuration = TimeSpan.FromMinutes(1) };
			if (ovr.HasValue) x.config.tierOverride[EEventKind.Cpi] = ovr.Value;
			if (jl6 != null) x.journal = q => jl6.Add(q);
			x.Configure(); Bar(x, ET(2026, 2, 11, 10, 0)); return x;
		};
		var j6 = new List<string>(); var lo6 = t6(EEventTier.T3, j6, EEventTier.T3); var hi6 = t6(null, null, EEventTier.T3);
		var jb6 = new List<string>(); t6(EEventTier.T3, jb6, EEventTier.T1);   // engine minTier T1, Cpi effective T3
		var day6 = new DateTime(2026, 2, 11); EEventKind k6; bool f6;
		Check(lo6.Coverage(EEventKind.Cpi, day6) == EEventCoverage.Unknown && !lo6.CoverageBlocks(day6, EEventClassMask.Macro, EEventTier.T1, out k6) && !lo6.IsInWindow(EEventTier.T1, 0, 0), "EV-4.6b(6) Unknown below minTier (override T3 < T1) does not block");
		Check(lo6.CoverageBlocks(day6, EEventClassMask.Macro, EEventTier.T3, out k6) && lo6.IsInWindow(EEventTier.T3, 0, 0), "EV-4.6b(6) same hole at the minimum (T3) blocks");
		Check(hi6.CoverageBlocks(day6, EEventClassMask.Macro, EEventTier.T1, out k6) && hi6.IsInWindow(EEventTier.T1, 0, 0), "EV-4.6b(6) same hole above the minimum (default T1, T1 query) blocks");
		Check(jb6.Any(q => q.Contains("COVGAP day 2026-02-11") && q.Contains("Cpi(belowMinTier)")), "below-minTier Unknown day still journals COVGAP, labelled (belowMinTier)");
		Check(j6.Any(q => q.Contains("COVGAP day 2026-02-11") && q.Contains("Cpi(blocks)")), "at-minTier Unknown day journals (blocks)");
		var rl = lo6.CoverageReport(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), EEventClassMask.Macro, EEventTier.T1, out f6);
		var rh = lo6.CoverageReport(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), EEventClassMask.Macro, EEventTier.T3, out f6);
		Check(rl.Any(q => q.Contains("Cpi blocked 0/")) && rh.Any(q => q.Contains("Cpi blocked 3/")), "below-minTier days excluded from the blocked share: " + rl[0] + " | " + rh[0]);
		Check(real(false, null, all).Coverage(EEventKind.Cpi, new DateTime(2026, 1, 6)) != EEventCoverage.Unknown, "no hole 2026-01-06 for Cpi");
		// COVGAP journal: once at start (spans reaching the first bar) and once per affected day
		var jh = new List<string>();
		var x2 = new _EventCalendar { config = new EventConfig { enabled = true }, table = EventRules.Table(), corrections = EventRules.Corrections, coverageHoles = EventRules.CoverageHoles(),
			barDuration = TimeSpan.FromMinutes(1), firstBarUtc = U(ET(2026, 1, 2, 9, 0)) };
		x2.journal = s => jh.Add(s); x2.Configure();
		Check(jh.Any(s => s.Contains("COVGAP FomcPress 2026-10-28")) && !jh.Any(s => s.Contains("COVGAP Pce 2020-10")), "COVGAP start lines: spans after the first bar only");
		Check(jh.Any(s => s.StartsWith("EVENT COVGAP Jolts 2027-01-01..-") && s.Contains("after coverage end 2026-12-31")), "COVGAP start line: Jolts coverage end");
		int n0 = jh.Count;
		Bar(x2, ET(2026, 10, 28, 10, 0)); Bar(x2, ET(2026, 10, 28, 10, 1)); Bar(x2, ET(2026, 10, 29, 10, 0));
		var dl = jh.Skip(n0).Where(s => s.Contains("COVGAP day")).ToList();
		Check(dl.Count(s => s.Contains("COVGAP day 2026-10-28") && s.Contains("FomcPress(blocks)")) == 1, "COVGAP once per affected day: " + string.Join("|", dl));
		// share report per kind and window; >5% flagged
		var rep = real(false, null, all); bool flg;
		foreach (var w in new[] { new[] { 2016, 2019 }, new[] { 2020, 2022 }, new[] { 2023, 2026 } })
		{
			var lines = rep.CoverageReport(new DateTime(w[0], 1, 1), new DateTime(w[1], 12, 31), all, out flg);
			foreach (var l in lines) Console.WriteLine(l);
			Check(lines.Count > 0, "COVSHARE report " + w[0] + "-" + w[1] + (flg ? " (has FLAG)" : ""));
		}
		var fr = rep.CoverageReport(new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), all, out flg);
		Check(flg && fr.Any(l => l.Contains("Jolts") && l.EndsWith("FLAG")), "window past coverage end is flagged >5%");
		var rp = real(true, null, all); rp.CoverageReport(new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), all, out flg);
		Check(!flg, "partial=true: nothing blocked, no flag");
		Check(x2.CoverageStats().Any(l => l.StartsWith("COVSHARE run FomcPress blocked 1/2")), "run-time COVSHARE counts: " + string.Join("|", x2.CoverageStats()));
	}
	static string TzId() { try { TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); return "Eastern Standard Time"; } catch { return "America/New_York"; } }

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
