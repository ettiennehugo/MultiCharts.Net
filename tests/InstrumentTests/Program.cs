using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PowerLanguage.Function;
using S = PowerLanguage.Function._MtfMath.Session;

// Off-MC tests for M-INSTR PR-M2 (specs/m-instr-spec.md rev 2.3 §10): MI-T2..T9, T11, T13, T16 (core) plus synthetic Spot FX /
// crypto cases (their presets / class constants keep their VERIFY markers). Fixtures: the 13 PR-M0 _InstrDump runs (§4.4.2).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
	static readonly InstrumentSubject RS = new InstrumentSubject("INSTR", "@X", EAssetClass.Future, "X", "s");
	static string Fx(string f) { return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", f); }
	static DateTime D(int y, int m, int d, int h = 0, int mi = 0) { return new DateTime(y, m, d, h, mi, 0); }
	static string Ds(DateTime d) { return d.ToString("yyyy-MM-dd", Inv); }

	/// <summary>Runs f; returns the InstrumentException (null if none) and checks the Error line was written first with the same text.</summary>
	static InstrumentException Throws(Action<Action<string>> f, string what)
	{
		var sink = new List<string>();
		try { f(s => sink.Add(s)); }
		catch (InstrumentException e) { Check(sink.Count == 1 && sink[0] == e.Message, what + ": Error line before throw"); return e; }
		return null;
	}

	static InstrumentProfile.Inputs In(string name, EAssetClass cls, string mcRoot, double minMove, double infoPs, double bpv, string ccy = "USD")
	{
		return new InstrumentProfile.Inputs { name = name, assetClass = cls, mcRoot = mcRoot, minMove = minMove, infoPriceScale = infoPs, structPriceScale = 1.0 / infoPs,
			infoBigPointValue = bpv, structBigPointValue = bpv, currency = ccy, exchange = "CME", sessionName = "test", symbolExpiry = new DateTime(2038, 1, 1) };
	}

	// ---------------------------------------------------------------- fixtures
	sealed class Dump
	{
		public string File, Name, Cat, SessionName, TzMode, App;
		public double MinMove, PriceScale, Bpv;
		public S[] StartCalc, FirstBar;
		public DateTime FirstBarTime;
		public List<DateTime> SessionEndBars = new List<DateTime>();
	}
	static S[] ParseSessions(string t)
	{
		// "n=9; Sunday 17:00:00 -> Monday 15:15:00; ..." (test-side text of the fixture; DayOfWeek parsed here only, never in study code)
		var parts = t.Split(';').Select(p => p.Trim()).ToList();
		int n = int.Parse(parts[0].Substring(2), Inv);
		var r = new List<S>();
		for (int i = 1; i <= n; i++)
		{
			var a = parts[i].Split(new[] { " -> " }, StringSplitOptions.None);
			var x = a[0].Split(' '); var y = a[1].Split(' ');
			r.Add(new S((DayOfWeek)Enum.Parse(typeof(DayOfWeek), x[0]), TimeSpan.Parse(x[1], Inv), (DayOfWeek)Enum.Parse(typeof(DayOfWeek), y[0]), TimeSpan.Parse(y[1], Inv)));
		}
		return r.ToArray();
	}
	static string Between(string l, string open)
	{
		int i = l.IndexOf(open, StringComparison.Ordinal); if (i < 0) return null; i += open.Length;
		int depth = 1, j = i;
		for (; j < l.Length; j++) { if (l[j] == '(') depth++; else if (l[j] == ')' && --depth == 0) break; }
		return l.Substring(i, j - i);
	}
	static Dump Load(string file)
	{
		var d = new Dump { File = file };
		foreach (var raw in File.ReadAllLines(Fx(file)))
		{
			string l = raw.TrimEnd('\r');
			if (l.StartsWith("M-INSTR PR-M0 dump", StringComparison.Ordinal)) d.App = Between(l, "app(");
			if (!l.StartsWith("A1 ds(1)", StringComparison.Ordinal) && !l.StartsWith("A2 ds(1)", StringComparison.Ordinal)) continue;
			if (l.StartsWith("A1 ds(1) name(", StringComparison.Ordinal)) d.Name = Between(l, "name(");
			else if (l.StartsWith("A1 ds(1) cat(", StringComparison.Ordinal)) d.Cat = Between(l, "cat(");
			else if (l.StartsWith("A1 ds(1) tz(", StringComparison.Ordinal)) d.TzMode = Between(l, "tz(");
			else if (l.StartsWith("A1 ds(1) sessionName(", StringComparison.Ordinal)) d.SessionName = Between(l, "sessionName(");
			else if (l.StartsWith("A1 ds(1) minMove(", StringComparison.Ordinal))
			{ d.MinMove = double.Parse(Between(l, "minMove("), Inv); d.PriceScale = double.Parse(Between(l, "priceScale("), Inv); d.Bpv = double.Parse(Between(l, "bpv("), Inv); }
			else if (l.StartsWith("A2 ds(1) at(StartCalc) sessions(", StringComparison.Ordinal)) d.StartCalc = ParseSessions(Between(l, "sessions("));
			else if (l.StartsWith("A2 ds(1) at(FirstCalcBar) sessions(", StringComparison.Ordinal)) d.FirstBar = ParseSessions(Between(l, "sessions("));
			else if (l.StartsWith("A2 ds(1) firstBar time(", StringComparison.Ordinal)) d.FirstBarTime = DateTime.ParseExact(Between(l, "time("), "yyyy-MM-dd HH:mm:ss", Inv);
			else if (l.Contains(" sessionEndBar(")) d.SessionEndBars.Add(DateTime.ParseExact(Between(l, "time("), "yyyy-MM-dd HH:mm:ss", Inv));
		}
		return d;
	}
	static List<Dump> s_dumps;
	static List<Dump> Dumps()
	{
		if (s_dumps != null) return s_dumps;
		s_dumps = new List<Dump>();
		foreach (var row in File.ReadAllLines(Fx("index.txt")).Where(r => r.Trim().Length > 0)) s_dumps.Add(Load(row.Trim().Split('|')[4]));
		return s_dumps;
	}
	static EAssetClass ClassOfFixture(string cat) { return cat == "Future" ? EAssetClass.Future : cat == "Stock" ? EAssetClass.Stock : EAssetClass.Unsupported; }

	// ---------------------------------------------------------------- MI-T2 roots
	static void T2Roots()
	{
		var fut = new[] { "@ES", "ES", "@ES#C", "ES", "ESZ24", "ES", "ESZ4", "ES", "ESZ2024", "ES", "ES 12-24", "ES", "ESZ24:XCME", "ES", "CLZ2026", "CL", "@6E", "6E", "6EZ26", "6E",
			"Z4", "Z", "MESZ26", "MES", "MESH27", "MES", "@MNQ", "MNQ", "GCQ26", "GC", "ZNH27", "ZN", "6BZ26", "6B" };
		for (int i = 0; i < fut.Length; i += 2)
		{
			Check(InstrumentRoot.Parse(fut[i], EAssetClass.Future) == fut[i + 1], "T2 parse " + fut[i] + " -> " + InstrumentRoot.Parse(fut[i], EAssetClass.Future));
			Check(InstrumentRoot.Parse(fut[i], EAssetClass.Future) == _MtfMath.SymbolRoot(fut[i]), "T2 parse == _MtfMath.SymbolRoot for futures " + fut[i]);
		}
		Check(InstrumentRoot.Parse("6EZ26", EAssetClass.Future) != InstrumentRoot.Parse("6BZ26", EAssetClass.Future), "T2 6E vs 6B differ");
		Check(InstrumentRoot.Parse("EUR/USD", EAssetClass.SpotFx) == "EURUSD", "T2 EUR/USD SpotFx -> EURUSD");
		Check(InstrumentRoot.Parse("EUR/USD", EAssetClass.SpotFx) != InstrumentRoot.Parse("EUR/JPY", EAssetClass.SpotFx), "T2 EUR/USD vs EUR/JPY differ");
		Check(InstrumentRoot.Parse("EUR/USD", EAssetClass.Future) == "EUR", "T2 '/' removed for SpotFx only");
		Check(InstrumentRoot.Parse("BRK.B", EAssetClass.Stock) == "BRK.B", "T2 stock keeps the share class BRK.B");
		Check(_MtfMath.SymbolRoot("BRK.B") == "BRK", "T2 M1a SymbolRoot BRK.B -> BRK unchanged");
		Check(InstrumentRoot.Parse("AAPL", EAssetClass.Stock) == "AAPL" && InstrumentRoot.Parse(" spy ", EAssetClass.Stock) == "SPY", "T2 stocks");
		Check(InstrumentRoot.Parse("BTCUSD", EAssetClass.Crypto) == "BTCUSD", "T2 BTCUSD");
		Check(InstrumentPresets.Find("ES", EAssetClass.Stock) == null && InstrumentPresets.Find("ES", EAssetClass.Future) != null, "T2 'ES' as a stock gets no preset");
		Check(InstrumentPresets.Find("MES", EAssetClass.Future).bigPointValue == 5 && InstrumentPresets.Find("6B", EAssetClass.Future) == null, "T2 exact root match (no prefix)");

		// _RootDump fixtures (§4.4.3): (name, mcRoot, UnsupportedFields) -> root / src, through the typed <instr-mc-fields> region on a mirror enum
		var rows = new object[] { "@ES", "ES", 0, "ES", "mc", "@CL", "CL", 0, "CL", "mc", "@6E", "6E", 0, "6E", "mc", "SPY", "SPY", 0, "SPY", "mc",
			"@6E", "", 0, "6E", "parsed", "@ES", "ES", 1, "ES", "parsed", "@ES", "ES", -1, "ES", "parsed", "@ES", "ES", 512, "ES", "mc" };
		for (int i = 0; i < rows.Length; i += 5)
		{
			string name = (string)rows[i]; EAssetClass cls = name == "SPY" ? EAssetClass.Stock : EAssetClass.Future;
			bool uns = Mirror.InstrumentMcFields.RootUnsupported((Mirror.MTPA_SymbolFields)(int)rows[i + 2]);
			var r = InstrumentRoot.Resolve(name, cls, (string)rows[i + 1], uns, "T", "s", null);
			Check(r.Root == (string)rows[i + 3] && r.Source == (string)rows[i + 4] && r.Warning == null, "T2 rootdump " + name + "/" + rows[i + 1] + "/" + rows[i + 2] + " -> " + r.Root + "/" + r.Source);
		}
		var w = InstrumentRoot.Resolve("@ES", EAssetClass.Future, " mes ", false, "T", "s", null);
		Check(w.Root == "MES" && w.Source == "mc" && w.Warning == "INSTR root-crosscheck mc(MES) parsed(ES) name(@ES)", "T2 !SameRoot -> Warning, MC wins: " + w.Warning);
		Check(InstrumentRoot.Resolve("EUR/USD", EAssetClass.SpotFx, "EUR/USD", false, "T", "s", null).Root == "EURUSD", "T2 SpotFx MC root '/' removed");
		Check(InstrumentRoot.Resolve("@ES", EAssetClass.Future, "  ", false, "T", "s", null).Source == "parsed", "T2 whitespace MC root -> parsed");
		var e = Throws(s => InstrumentRoot.Resolve("", EAssetClass.Future, "", false, "INSTR", "s", s), "T2 empty");
		Check(e != null && e.Reason == "unknown-field:root", "T2 empty root rejected");
		e = Throws(s => InstrumentRoot.Resolve("#C", EAssetClass.Future, null, true, "INSTR", "s", s), "T2 #C");
		Check(e != null && e.Reason == "unknown-field:root", "T2 '#C' with root unsupported rejected");
		Check(!InstrumentRoot.UsableExpiry(new DateTime(2038, 1, 1)) && !InstrumentRoot.UsableExpiry(new DateTime(1900, 1, 1)) && InstrumentRoot.UsableExpiry(new DateTime(2026, 12, 18)), "T2 SymbolExpiry placeholders");
		var inp = In("ESZ26", EAssetClass.Future, "ES", 25, 100, 50); inp.symbolExpiry = new DateTime(2026, 12, 18);
		Check(InstrumentProfile.Build(inp, null, null).contractExpiry == new DateTime(2026, 12, 18), "T2 SymbolExpiry used");
		Check(InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).contractExpiry == null, "T2 placeholder -> calendar");
	}

	// ---------------------------------------------------------------- MI-T3 cross-checks
	static void T3Checks()
	{
		var subj = new InstrumentSubject("INSTR", "@X", EAssetClass.Future, "X", "s");
		var ok = new[] { new[] { 0.01, 100, 50, 50 }, new[] { 1E-05, 99999.999999999985, 125000, 125000 }, new[] { 0.01, 100, 1000, 1000 }, new[] { 0.01, 100, 1, 1 } };
		foreach (var r in ok) Check(Throws(s => InstrumentChecks.McInternal(r[0], r[1], r[2], r[3], subj, s), "T3 ok") == null, "T3 MC-internal ok " + r[0] + "x" + r[1]);
		var e = Throws(s => InstrumentChecks.McInternal(0.1, 100, 50, 50, subj, s), "T3 ps");
		Check(e != null && e.Reason == "mismatch:mcPriceScale", "T3 PS 0.1 vs 100 -> mismatch:mcPriceScale");
		e = Throws(s => InstrumentChecks.McInternal(0.01, 100, 50, 5, subj, s), "T3 bpv");
		Check(e != null && e.Reason == "mismatch:mcBigPointValue", "T3 BPV 50 vs 5 -> mismatch:mcBigPointValue");
		// preset cross-check (step 8)
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 50, 100, 50), null, s), "T3 tick");
		Check(e != null && e.Reason == "mismatch:tick", "T3 ES tick 0.5 -> mismatch:tick");
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 5), null, s), "T3 bpvp");
		Check(e != null && e.Reason == "mismatch:bigPointValue", "T3 ES bpv 5 -> mismatch:bigPointValue");
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 0, 100, 50), null, s), "T3 tick0");
		Check(e != null && e.Reason == "tick", "T3 MinMove 0 -> tick");
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50, null), null, s), "T3 ccy");
		Check(e != null && e.Reason == "currency", "T3 non-USD -> currency");
		e = Throws(s => InstrumentProfile.Build(In("X", EAssetClass.Unsupported, "X", 1, 100, 1), null, s), "T3 class");
		Check(e != null && e.Reason == "class", "T3 Unsupported -> class");
		// every PR-M0 fixture builds (class from cat, priceScale/BPV as dumped; struct = 1/Info per §4.4.3)
		foreach (var d in Dumps())
		{
			var cls = ClassOfFixture(d.Cat);
			var p = InstrumentProfile.Build(In(d.Name, cls, "", d.MinMove, d.PriceScale, d.Bpv), null, null);
			var pr = InstrumentPresets.Find(p.root, cls);
			Check(cls == EAssetClass.Stock ? pr == null : pr != null && p.presetId == pr.root, "T3 fixture preset " + d.File + " " + d.Name);
			Check(p.rootSource == "parsed", "T3 fixture rootSrc parsed (no MC root dumped) " + d.File);
		}
		Check(InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).tick == 5 / 99999.999999999985, "T3 6E tick from the binary priceScale");
	}

	// ---------------------------------------------------------------- MI-T4 expiry
	static void T4Expiry()
	{
		var es = InstrumentPresets.Find("ES", EAssetClass.Future);
		int n = 0;
		for (int y = 2016; y <= 2027; y++) for (int m = 3; m <= 12; m += 3)
		{
			Check(InstrumentRules.Expiry(es.expiry, y, m, EventRules.IsNyseBusinessDay, RS, null) == EventRules.RuleExpiryDate(y, m), "T4 ES expiry " + y + "-" + m); n++;
			Check(InstrumentRules.Expiry(EExpiryRule.ThirdFridaySoq, y, m, EventRules.IsNyseBusinessDay, RS, null) == EventRules.RuleExpiryDate(y, m), "T4 MES/NQ/MNQ share ThirdFridaySoq");
		}
		Check(n == 48, "T4 48 ES rows");
		foreach (var raw in File.ReadAllLines(Fx("expiry-worked.csv")))
		{
			string l = raw.Trim(); if (l.Length == 0 || l[0] == '#') continue;
			var c = l.Split(',');
			var p = InstrumentPresets.Find(c[0], EAssetClass.Future);
			Check(p.calendar == ECalendar.NyseStandIn, "T4 " + c[0] + " preset names the NYSE stand-in calendar");
			var bd = InstrumentCalendars.Require(p.calendar, RS, null);   // the preset's calendar, not an injected default
			int y = int.Parse(c[2], Inv), m = int.Parse(c[3], Inv);
			DateTime got = InstrumentRules.Expiry(p.expiry, y, m, bd, RS, null);
			Check(Ds(got) == c[4], "T4 " + c[1] + " expiry " + Ds(got) + " want " + c[4]);
			DateTime roll = InstrumentRules.Roll(p, y, m, bd, RS, null).Value;
			Check(Ds(roll) == c[5], "T4 " + c[1] + " roll " + Ds(roll) + " want " + c[5]);
		}
		// analyst-sourced rows that the old weekday fallback got wrong (CLF26 12-22, ZNH24 03-20, ZNZ25 12-22), via a built profile
		var clp = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null);
		var znp = InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null, null);
		Check(clp.isBusinessDay != null && Ds(InstrumentRules.Expiry(clp.expiry.Value, 2026, 1, clp.isBusinessDay, RS, null)) == "2025-12-19", "T4 CLF26 LTD 2025-12-19 (NYSE stand-in; chadv25-391)");
		Check(Ds(InstrumentRules.Expiry(znp.expiry.Value, 2024, 3, znp.isBusinessDay, RS, null)) == "2024-03-19", "T4 ZNH24 LTD 2024-03-19 (Good Friday skipped)");
		Check(Ds(InstrumentRules.Expiry(znp.expiry.Value, 2025, 12, znp.isBusinessDay, RS, null)) == "2025-12-19", "T4 ZNZ25 LTD 2025-12-19 (Barchart)");
		for (DateTime d = D(2016, 1, 1); d <= D(2030, 12, 31); d = d.AddDays(1))
			if (InstrumentCalendars.BusinessDay(ECalendar.NyseStandIn)(d) != EventRules.IsNyseBusinessDay(d)) { Check(false, "T4 NyseStandIn == EventRules.IsNyseBusinessDay " + Ds(d)); break; }
		Check(InstrumentCalendars.BusinessDay(ECalendar.Observed) == null && InstrumentCalendars.BusinessDay(ECalendar.None) == null, "T4 Observed / None have no business-day calendar");
		var six = InstrumentPresets.Find("6E", EAssetClass.Future);
		Check(six.roll == ERollRule.BusinessDaysBeforeExpiry && six.rollDays == 5 && six.verify == InstrumentCalendars.NyseStandInNote, "T4 6E roll = BusinessDaysBeforeExpiry(5) (Ettienne 2026-11-04)");
		// a root whose preset has rules but no calendar assigned: Error line, then throw 'calendar' (no silent default)
		foreach (var cal in new[] { ECalendar.Observed, ECalendar.None })
		{
			var ov = new InstrumentConfig { overrides = new InstrumentPresetRow("CL", EAssetClass.Future, 0.01, 1000, "USD", MonthMask.All, EExpiryRule.CL25thMinus3Bd, ERollRule.BusinessDaysBeforeExpiry, 2, cal, "ENERGY", "test", null) };
			var ce = Throws(s => InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), ov, s), "T4 no calendar " + cal);
			Check(ce != null && ce.Reason == InstrumentReason.Calendar && ce.Message.Contains("calendar " + InstrumentText.CalendarName(cal)), "T4 preset with rules and calendar " + InstrumentText.CalendarName(cal) + " -> Error + calendar");
		}
		Check(InstrumentProfile.Build(In("6BZ26", EAssetClass.Future, "6B", 1, 10000, 62500), null, null).isBusinessDay == null, "T4 future without a preset: no rules, no calendar needed, no throw");
		// R6 / §6.4 S4: the business-day calendar is required; no silent weekday fallback (it gave CLF26 12-22, ZNH24 03-20, ZNZ25 12-22)
		var cl = InstrumentPresets.Find("CL", EAssetClass.Future);
		var e = Throws(s => InstrumentRules.Expiry(EExpiryRule.CL25thMinus3Bd, 2026, 1, null, RS, s), "T4 no calendar expiry");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 Expiry without a calendar -> calendar");
		e = Throws(s => InstrumentRules.Roll(cl, 2026, 1, null, RS, s), "T4 no calendar roll");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 Roll without a calendar -> calendar");
		e = Throws(s => InstrumentRules.Nominal(EExpiryRule.ThirdFridaySoq, 2026, 3, null, RS, s), "T4 no calendar nominal");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 Nominal without a calendar -> calendar (even ThirdFridaySoq)");
		DateTime ex; int cy, cm;
		e = Throws(s => InstrumentRules.SeriesWindow(cl, D(2026, 1, 5), null, RS, s, out ex), "T4 no calendar window");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 SeriesWindow without a calendar -> calendar");
		e = Throws(s => InstrumentRules.ContractFor(cl, D(2026, 1, 5), null, RS, s, out cy, out cm), "T4 no calendar contract");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 ContractFor without a calendar -> calendar");
		e = Throws(s => InstrumentRules.ContractFor(new InstrumentPresetRow("X", EAssetClass.Future, 1, 1, "USD", 0, EExpiryRule.ThirdLastBd, ERollRule.None, 0, ECalendar.Observed, "X", "", null), D(2026, 1, 5), EventRules.IsNyseBusinessDay, RS, s, out cy, out cm), "T4 no months");
		Check(e != null && e.Reason == InstrumentReason.Calendar, "T4 no listed month -> Error + calendar");
	}

	// ---------------------------------------------------------------- MI-T5 / T6 roll + windows
	static void T5Digest()
	{
		var es = InstrumentPresets.Find("ES", EAssetClass.Future);
		Check(InstrumentRules.EquityRollMondayFrom == EventRules.RollRuleMondayFrom, "T5 cutover == EventRules.RollRuleMondayFrom");
		var dates = new List<DateTime>();
		for (int y = 2000; y <= 2035; y++) for (int m = 3; m <= 12; m += 3)
		{
			DateTime r = InstrumentRules.Roll(es, y, m, EventRules.IsNyseBusinessDay, RS, null).Value; dates.Add(r);
			Check(r == EventRules.RollDate(y, m), "T5 roll == EventRules.RollDate " + y + "-" + m);
		}
		Check(_RollDetector.DatesDigest(dates) == "fdd63bd8f179a8d4", "T5 digest " + _RollDetector.DatesDigest(dates));
	}
	static void T6Windows()
	{
		var es = InstrumentPresets.Find("ES", EAssetClass.Future);
		int days = 0, inWin = 0;
		for (DateTime d = D(2000, 1, 1); d <= D(2035, 12, 31); d = d.AddDays(1))
		{
			DateTime e1, e2;
			bool a = _RollDetector.SeriesWindow(d, out e1), b = InstrumentRules.SeriesWindow(es, d, EventRules.IsNyseBusinessDay, RS, null, out e2);
			if (a != b || e1 != e2) { Check(false, "T6 ES SeriesWindow identity " + Ds(d) + " " + a + "/" + b + " " + Ds(e1) + "/" + Ds(e2)); break; }
			days++; if (a) inWin++;
		}
		Check(days == (D(2035, 12, 31) - D(2000, 1, 1)).Days + 1 && inWin > 0, "T6 ES SeriesWindow identical every day 2000-2035 (" + inWin + " window days)");
		foreach (var root in new[] { "CL", "GC", "ZN", "6E" })
		{
			var p = InstrumentPresets.Find(root, EAssetClass.Future);
			var keys = new HashSet<DateTime>(); bool okWeek = true;
			for (DateTime d = D(2024, 1, 1); d <= D(2024, 12, 31); d = d.AddDays(1))
			{
				DateTime ex;
				if (!InstrumentRules.SeriesWindow(p, d, EventRules.IsNyseBusinessDay, RS, null, out ex)) continue;
				keys.Add(ex);
				int y, m; InstrumentRules.ContractFor(p, d, EventRules.IsNyseBusinessDay, RS, null, out y, out m);
				DateTime r = InstrumentRules.Roll(p, y, m, EventRules.IsNyseBusinessDay, RS, null).Value;
				if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || Math.Abs((r - d).Days) > 4 || r > ex) okWeek = false;
			}
			int want = root == "CL" ? 12 : root == "GC" ? 6 : 4;
			Check(okWeek, "T6 " + root + " windows: weekdays of the roll week, roll before expiry");
			Check(keys.Count >= want - 1 && keys.Count <= want + 1, "T6 " + root + " ~" + want + " windows in 2024: " + keys.Count);
		}
		var cl = InstrumentPresets.Find("CL", EAssetClass.Future);
		Check(Ds(InstrumentRules.Roll(cl, 2020, 5, EventRules.IsNyseBusinessDay, RS, null).Value) == "2020-04-17", "T6 CLK20 roll = 2 bd before 2020-04-21");
		var zn = InstrumentPresets.Find("ZN", EAssetClass.Future);
		Check(Ds(InstrumentRules.Roll(zn, 2025, 12, EventRules.IsNyseBusinessDay, RS, null).Value) == "2025-11-26", "T6 ZNZ25 roll = 1 bd before FND 2025-11-28 (Barchart)");
		var gc = InstrumentPresets.Find("GC", EAssetClass.Future);
		Check(Ds(InstrumentRules.Roll(gc, 2024, 8, EventRules.IsNyseBusinessDay, RS, null).Value) == "2024-07-30", "T6 GCQ24 roll = 1 bd before FND 2024-07-31 (VERIFY)");
		Check(InstrumentRules.Roll(new InstrumentPresetRow("X", EAssetClass.Future, 1, 1, "USD", MonthMask.All, EExpiryRule.ThirdLastBd, ERollRule.None, 0, ECalendar.Observed, "X", "", null), 2024, 1, EventRules.IsNyseBusinessDay, RS, null) == null, "T6 ERollRule.None -> no roll");
	}

	// ---------------------------------------------------------------- MI-T7 / T8 sessions + derived fields
	static S[] Globex9()
	{
		return Dumps().First(d => d.File.StartsWith("01M6HSDBFRQ9ZZFA", StringComparison.Ordinal)).FirstBar;
	}
	static S[] Daily(DayOfWeek a, DayOfWeek b, TimeSpan st, TimeSpan en, bool overnight)
	{
		var r = new List<S>();
		for (var d = a; d <= b; d++) r.Add(overnight ? new S(d - 1 < DayOfWeek.Sunday ? DayOfWeek.Saturday : d - 1, st, d, en) : new S(d, st, d, en));
		return r.ToArray();
	}
	static void T7TradingDay()
	{
		var g = Globex9(); var subj = new InstrumentSubject("T", "@ES", EAssetClass.Future, "ES", "GLOBEX"); InstrumentException e0;
		Check(g.Length == 9, "T7 GLOBEX fixture has 9 entries");
		Check(InstrumentSessions.TradingDayKey(g, D(2026, 10, 25, 17, 7), subj, null) == D(2026, 10, 26), "T7 Sun 17:07 -> Mon");
		Check(InstrumentSessions.TradingDayKey(g, D(2026, 10, 26, 15, 31), subj, null) == D(2026, 10, 26), "T7 Mon 15:31 (2nd session) -> Mon");
		Check(InstrumentSessions.TradingDayKey(g, D(2026, 10, 26, 17, 1), subj, null) == D(2026, 10, 27), "T7 Mon 17:01 -> Tue");
		Check(InstrumentSessions.TradingDayKey(g, D(2026, 10, 30, 15, 15), subj, null) == D(2026, 10, 30), "T7 Fri 15:15 (end inclusive) -> Fri");
		e0 = Throws(s => InstrumentSessions.TradingDayKey(g, D(2026, 10, 30, 16, 0), subj, s), "T7 fri16");
		Check(e0 != null && e0.Reason == "sessions:bar-outside", "T7 fixture has no Fri 15:30-16:30 entry (9-entry list) -> Fri 16:00 outside");
		var e = Throws(s => InstrumentSessions.TradingDayKey(g, D(2026, 10, 25, 17, 0), subj, s), "T7 start");
		Check(e != null && e.Reason == "sessions:bar-outside", "T7 bar exactly at a session start is outside (start exclusive)");
		e = Throws(s => InstrumentSessions.TradingDayKey(g, D(2026, 10, 31, 12, 0), subj, s), "T7 sat");
		Check(e != null && e.Reason == "sessions:bar-outside", "T7 Saturday bar -> sessions:bar-outside");
		e = Throws(s => InstrumentSessions.TradingDayKey(g, D(2026, 10, 26, 15, 20), subj, s), "T7 halt");
		Check(e != null && e.Reason == "sessions:bar-outside", "T7 15:20 in the 15:15-15:30 gap -> outside");
		e = Throws(s => InstrumentSessions.TradingDayKey(new S[0], D(2026, 10, 26, 10, 0), subj, s), "T7 none");
		Check(e != null && e.Reason == "sessions:none", "T7 no sessions -> sessions:none");
		// every fixture: StartCalc == first bar list; first bar keys to the date of the first session-end bar; each session-end bar is
		// inside a session whose trading day is its own date
		foreach (var d in Dumps())
		{
			Check(InstrumentSessions.Same(d.StartCalc, d.FirstBar), "T7 " + d.File + " StartCalc == FirstCalcBar sessions");
			DateTime k;
			Check(d.SessionEndBars.Count > 0, "T7 " + d.File + " has session-end bars");
			k = InstrumentSessions.TradingDayKey(d.FirstBar, d.FirstBarTime, subj, null);
			Check(k == d.SessionEndBars[0].Date, "T7 " + d.File + " first bar " + d.FirstBarTime + " key " + Ds(k) + " == first session end date");
			foreach (var t in d.SessionEndBars) Check(InstrumentSessions.TradingDayKey(d.FirstBar, t, subj, null) == t.Date, "T7 " + d.File + " session end " + t + " keyed to its date");
		}
		// crypto 24 h (two encodings) and FX Sun-Fri 17:00
		var c1 = new List<S>(); var c2 = new List<S>();
		for (var d = DayOfWeek.Sunday; d <= DayOfWeek.Saturday; d++) { c1.Add(new S(d, TimeSpan.Zero, d, TimeSpan.FromDays(1))); c2.Add(new S(d, TimeSpan.Zero, d == DayOfWeek.Saturday ? DayOfWeek.Sunday : d + 1, TimeSpan.Zero)); }
		foreach (var c in new[] { c1.ToArray(), c2.ToArray() })
		{
			Check(InstrumentSessions.TradingDayKey(c, D(2026, 11, 4, 0, 1), subj, null) == D(2026, 11, 4), "T7 crypto 00:01 -> same date");
			Check(InstrumentSessions.TradingDayKey(c, D(2026, 11, 5, 0, 0), subj, null) == D(2026, 11, 4), "T7 crypto 24:00 bar -> the day it closes (boundary VERIFY vs 'UTC date')");
			Check(InstrumentSessions.TradingDayKey(c, D(2026, 11, 7, 23, 59), subj, null) == D(2026, 11, 7), "T7 crypto Saturday");
		}
		var fx = Daily(DayOfWeek.Monday, DayOfWeek.Friday, new TimeSpan(17, 0, 0), new TimeSpan(17, 0, 0), true);
		Check(InstrumentSessions.TradingDayKey(fx, D(2026, 11, 1, 17, 1), subj, null) == D(2026, 11, 2), "T7 FX Sun 17:01 -> Mon");
		Check(InstrumentSessions.TradingDayKey(fx, D(2026, 11, 6, 17, 0), subj, null) == D(2026, 11, 6), "T7 FX Fri 17:00 -> Fri");
	}
	static void T8Derived()
	{
		var g = Globex9(); DateTime o, e;
		Check(InstrumentSessions.DayOpen(g, D(2026, 10, 26), out o) && o == D(2026, 10, 25, 17, 0), "T8 ES dayOpen Mon = Sun 17:00");
		Check(InstrumentSessions.SessionEnd(g, D(2026, 10, 26), out e) && e == D(2026, 10, 26, 16, 30), "T8 ES sessionEnd from the list = 16:30 (R14; data stops 16:00)");
		Check(!InstrumentSessions.DayOpen(g, D(2026, 10, 31), out o), "T8 Saturday has no session");
		Check(InstrumentSessions.HasOvernight(g), "T8 ES GLOBEX hasOvernight");
		var spy = Dumps().First(d => d.Name == "SPY");
		Check(!InstrumentSessions.HasOvernight(spy.FirstBar), "T8 SPY day-only");
		var r = RthSplit.Of(spy.FirstBar, EAssetClass.Stock, "SPY");
		Check(r != null && r.source == ERthSource.ChartDaySession && r.start == new TimeSpan(4, 15, 0) && r.end == new TimeSpan(20, 0, 0), "T8 SPY rth = chart session 04:15-20:00");
		r = RthSplit.Of(g, EAssetClass.Future, "ES");
		Check(r != null && r.source == ERthSource.EquityIndexLegacy && r.start == _CmeSessionClock.DefaultRthStart && r.valueEnd == _CmeSessionClock.DefaultRthValueEnd && r.end == _CmeSessionClock.DefaultRthEnd, "T8 ES 24h -> EquityIndexLegacy");
		Check(RthSplit.Of(g, EAssetClass.Future, "MNQ").source == ERthSource.EquityIndexLegacy, "T8 MNQ legacy");
		var cl = Dumps().First(d => d.Name == "@CL" && d.TzMode == "Exchange");
		Check(RthSplit.Of(cl.FirstBar, EAssetClass.Future, "CL") == null, "T8 CL 24h -> no rth split (S7)");
		Check(RthSplit.Of(g, EAssetClass.Future, "ES") != null && RthSplit.Of(null, EAssetClass.Future, "ES") == null, "T8 no sessions -> null");
		// profile fields
		var p = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null);
		Check(p.tick == 0.25 && p.bigPointValue == 50 && p.tickValue == 12.5 && p.calendar == ECalendar.CmeEquityIndex && p.expiry == EExpiryRule.ThirdFridaySoq
			&& p.roll == ERollRule.EquityIndexCustomary && p.sizingUnit == ESizingUnit.Contracts && p.sessionsPerYear == 252 && p.cluster == "EQIDX" && MonthMask.Text(p.contractMonths) == "HMUZ", "T8 ES profile fields");
		var p2 = p.WithSessions(g, g, null);
		Check(p2.hasOvernight && p2.rthSplit.source == ERthSource.EquityIndexLegacy && p.sessions == null && p2.sessions.Length == 9, "T8 WithSessions copies (profile immutable)");
		var ex = Throws(s => p.WithSessions(g, spy.FirstBar, s), "T8 changed");
		Check(ex != null && ex.Reason == "sessions:changed", "T8 StartCalc vs first bar differ -> sessions:changed");
		ex = Throws(s => p.WithSessions(null, g, s), "T8 none");
		Check(ex != null && ex.Reason == "sessions:none", "T8 null sessions -> sessions:none");
		var stk = InstrumentProfile.Build(In("SPY", EAssetClass.Stock, "SPY", 1, 100, 1), null, null);
		Check(stk.calendar == ECalendar.Nyse && stk.presetId == null && stk.sizingUnit == ESizingUnit.Shares && stk.expiry == null && stk.roll == null && !stk.cost.known && stk.costFloorTicks == -1, "T8 SPY: Nyse, no preset, Shares, cost Unknown");
		var cl2 = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null);
		Check(cl2.calendar == ECalendar.NyseStandIn && cl2.isBusinessDay != null && cl2.roll == ERollRule.BusinessDaysBeforeExpiry && cl2.rollDays == 2 && MonthMask.Text(cl2.contractMonths) == "FGHJKMNQUVXZ", "T8 CL preset");
		foreach (var row in InstrumentPresets.Rows) Check(row.Validate() == null, "T8 preset row valid " + row.root + " " + row.Validate());
		Check(new InstrumentPresetRow("es", EAssetClass.Future, 0, 50, "EUR", 0, EExpiryRule.ThirdFridaySoq, ERollRule.None, -1, ECalendar.None, "", "", null).Validate() != null, "T8 bad preset row rejected");
	}

	// ---------------------------------------------------------------- synthetic Spot FX / crypto (VERIFY constants)
	static void FxCrypto()
	{
		var fx = Daily(DayOfWeek.Monday, DayOfWeek.Friday, new TimeSpan(17, 0, 0), new TimeSpan(17, 0, 0), true);
		var p = InstrumentProfile.Build(In("EUR/USD", EAssetClass.SpotFx, "EUR/USD", 1, 100000, 1), null, null).WithSessions(fx, fx, null);
		Check(p.root == "EURUSD" && p.presetId == null && p.calendar == ECalendar.Observed && p.sessionsPerYear == 260 && p.sizingUnit == ESizingUnit.Units && !p.cost.known, "FX profile (sessionsPerYear 260 VERIFY)");
		Check(p.hasOvernight && p.rthSplit == null, "FX 24h -> overnight, no rth split");
		Check(InstrumentPresets.ClassVerify(EAssetClass.SpotFx).StartsWith("VERIFY", StringComparison.Ordinal) && InstrumentPresets.ClassVerify(EAssetClass.Crypto).StartsWith("VERIFY", StringComparison.Ordinal), "FX/crypto keep VERIFY markers");
		var c = new List<S>(); for (var d = DayOfWeek.Sunday; d <= DayOfWeek.Saturday; d++) c.Add(new S(d, TimeSpan.Zero, d, TimeSpan.FromDays(1)));
		var cp = InstrumentProfile.Build(In("BTCUSD", EAssetClass.Crypto, "", 1, 100, 1), null, null).WithSessions(c.ToArray(), c.ToArray(), null);
		Check(cp.root == "BTCUSD" && cp.rootSource == "parsed" && cp.sessionsPerYear == 365 && cp.calendar == ECalendar.Observed && cp.sizingUnit == ESizingUnit.Units, "crypto profile");
		Check(!cp.hasOvernight && cp.rthSplit != null && cp.rthSplit.source == ERthSource.ChartDaySession, "crypto 00:00-24:00 literal: no overnight, day split (VERIFY, PR-M4)");
		Check(cp.InstrLine(1, "Crypto", "Exchange", "TickCount").Contains("dayOpen(00:00) sessionEnd(24:00)"), "crypto INSTR dayOpen/sessionEnd: " + cp.InstrLine(1, "Crypto", "Exchange", "TickCount"));
	}

	// ---------------------------------------------------------------- MI-T9 costs
	static void T9Costs()
	{
		var subj = new InstrumentSubject("INSTR", "@X", EAssetClass.Future, "X", "s");
		Check(CostModel.FloorTicks(1700, 0.25, 50, subj, null) == 14, "T9 ES 1700 -> 14");
		Check(CostModel.FloorTicks(250, 0.25, 5, subj, null) == 20, "T9 MES 250 -> 20");
		Check(CostModel.FloorTicks(125, 0.25, 50, subj, null) == 1 && CostModel.FloorTicks(126, 0.25, 50, subj, null) == 2, "T9 ceil boundary");
		var e = Throws(s => CostModel.FloorTicks(1000, 0.015625, 1000, subj, s), "T9 ZN");
		Check(e != null && e.Reason == "mismatch:tickValue", "T9 ZN tick value 1562.5 cents -> mismatch:tickValue");
		var es = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null);
		var mes = InstrumentProfile.Build(In("@MES", EAssetClass.Future, "MES", 25, 100, 5), null, null);
		Check(es.cost.known && es.cost.roundTripCents == 1700 && es.costFloorTicks == 14 && mes.costFloorTicks == 20, "T9 profile costs ES/MES");
		Check(es.cost.Text() == "17.00 src=Ettienne 2026-10-31" && mes.cost.Text().StartsWith("2.50", StringComparison.Ordinal), "T9 cost text");
		var cfg = new InstrumentConfig(); Check(cfg.Validate() == null && !cfg.applyCostFloor && cfg.enabled && cfg.overrides == null, "T9 config defaults");
		cfg.costs["ZN"] = 1000;
		e = Throws(s => InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), cfg, s), "T9 ZN cfg");
		Check(e != null && e.Reason == "mismatch:tickValue", "T9 ZN with a cost throws mismatch:tickValue");
		Check(!InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null, null).cost.known, "T9 ZN without cost -> Unknown, no throw");
		var bad = new InstrumentConfig(); bad.costs["es"] = 0;
		Check(bad.Validate() != null && bad.Validate().StartsWith("InstrumentConfig: ", StringComparison.Ordinal), "T9 Validate rejects bad costs");
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), bad, s), "T9 badcfg");
		Check(e != null && e.Reason == "unknown-field:config", "T9 invalid config refuses");
		var a = new InstrumentConfig(); var b = new InstrumentConfig { costs = new Dictionary<string, long> { { "MES", 250 }, { "ES", 1700 } } };
		Check(a.Canon() == b.Canon(), "T9 Canon order-independent");
		b.applyCostFloor = true; Check(a.Canon() != b.Canon(), "T9 Canon covers applyCostFloor");
		// overrides replace the preset row for that root + class
		var o = new InstrumentConfig { overrides = new InstrumentPresetRow("ZN", EAssetClass.Future, 0.015625, 1000, "USD", MonthMask.Of("HMUZ"), EExpiryRule.LastBdMinus7Bd, ERollRule.None, 0, ECalendar.NyseStandIn, "RATES", "user", null) };
		Check(InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), o, null).roll == ERollRule.None, "T9 overrides row used");
	}

	// ---------------------------------------------------------------- MI-T11 exception texts
	static void T11Texts()
	{
		Check(InstrumentException.Format("VPFUT", "SPY", EAssetClass.Stock, "SPY", "Default", "class", null, "use a futures chart")
			== "M-INSTR: VPFUT cannot run on SPY (class Stock, root SPY, sessions Default): class. use a futures chart.", "T11 class text");
		Check(InstrumentException.Format("INSTR", "@ES", EAssetClass.Future, "ES", "GLOBEX", "mismatch:tick", "detected 0.5 vs preset 0.25", "Fix it.")
			== "M-INSTR: INSTR cannot run on @ES (class Future, root ES, sessions GLOBEX): mismatch:tick (detected 0.5 vs preset 0.25). Fix it.", "T11 detail text");
		Check(InstrumentException.Format("INSTR", "", EAssetClass.Future, "", "", "unknown-field:root", null, null) == "M-INSTR: INSTR cannot run on  (class Future, root ?, sessions ?): unknown-field:root.", "T11 empty fields");
		Check(InstrumentReason.UnknownField("root") == "unknown-field:root" && InstrumentReason.Mismatch("tick") == InstrumentReason.MismatchTick, "T11 reason helpers");
		var codes = new[] { InstrumentReason.Class, InstrumentReason.Tick, InstrumentReason.VolumeBasis, InstrumentReason.Currency, InstrumentReason.SessionsNone, InstrumentReason.SessionsChanged,
			InstrumentReason.SessionsBarOutside, InstrumentReason.SessionsNoRthSplit, InstrumentReason.SessionsWindowNotCovered, InstrumentReason.Calendar };
		Check(string.Join(",", codes) == "class,tick,volume-basis,currency,sessions:none,sessions:changed,sessions:bar-outside,sessions:no-rth-split,sessions:window-not-covered,calendar", "T11 §6.3 reason codes");
		var e = Throws(s => InstrumentProfile.Build(In("SPX", EAssetClass.Unsupported, "SPX", 1, 100, 1), null, s), "T11 class");
		Check(e != null && e.Message.StartsWith("M-INSTR: INSTR cannot run on SPX (class Unsupported, root ?, sessions test): class.", StringComparison.Ordinal), "T11 thrown text: " + (e == null ? "" : e.Message));
	}

	// ---------------------------------------------------------------- MI-T13 profileHash
	static void T13Hash()
	{
		var g = Globex9();
		var a = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, null);
		var b = InstrumentProfile.Build(In("ESZ26", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, null);
		Check(a.ProfileHash() == b.ProfileHash() && a.ProfileHash().Length == 16, "T13 same facts -> same hash (name not hashed)");
		var m = InstrumentProfile.Build(In("@MES", EAssetClass.Future, "MES", 25, 100, 5), null, null).WithSessions(g, g, null);
		Check(a.ProfileHash() != m.ProfileHash(), "T13 MES differs");
		var spy = Dumps().First(d => d.Name == "SPY").FirstBar;
		Check(InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(spy, spy, null).ProfileHash() != a.ProfileHash(), "T13 sessions change the hash");
		var cfg = new InstrumentConfig();
		Check(!cfg.Canon().Contains(a.ProfileHash()) && !cfg.Canon().Contains("profileHash"), "T13 profileHash outside InstrumentConfig.Canon (Q7)");
		Check(a.ProfileHash() == InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, null).ProfileHash(), "T13 deterministic");
		string line = a.InstrLine(1, "Future", "Exchange", "Volume");
		Check(line.StartsWith("INSTR ds(1) name(@ES) cat(Future) class(Future) root(ES rootSrc=mc) preset(ES) exch(CME) tz(Exchange) sessionName(test) sessions(n=9 Sun 17:00→Mon 15:15;", StringComparison.Ordinal)
			&& line.Contains(" dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(09:30-16:15 src=EquityIndexLegacy) tick(0.25) bpv(50) tickValue(12.5) ccy(USD) bvo(Volume) calendar(CmeEquityIndex) months(HMUZ) expiry(ThirdFridaySoq) roll(EquityIndexCustomary) cost(17.00 src=Ettienne 2026-10-31) floor(14) cluster(EQIDX) profileHash(" + a.ProfileHash() + ")"), "T13 INSTR line: " + line);
		Check(!line.Contains("stand-in"), "T13 ES line has no stand-in note");
		var clLine = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null).WithSessions(g, g, null).InstrLine(1, "Future", "Exchange", "Volume");
		Check(clLine.Contains(" calendar(NyseStandIn; calendar: NYSE stand-in (CME calendar pending M8)) months(FGHJKMNQUVXZ) expiry(CL25thMinus3Bd) roll(BusinessDaysBeforeExpiry(2))"), "T13 CL INSTR line names the NYSE stand-in: " + clLine);
		var sixLine = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(g, g, null).InstrLine(1, "Future", "Exchange", "Volume");
		Check(sixLine.Contains("calendar(NyseStandIn; calendar: NYSE stand-in (CME calendar pending M8))") && sixLine.Contains("roll(BusinessDaysBeforeExpiry(5))"), "T13 6E INSTR line: " + sixLine);
		Console.WriteLine("ES GLOBEX profileHash " + a.ProfileHash());
	}

	// ---------------------------------------------------------------- MI-T16 R14 session ends
	static void T16SessionEnds()
	{
		var g = Globex9(); var subj = new InstrumentSubject("T", "@ES", EAssetClass.Future, "ES", "GLOBEX");
		var det = new SessionEndDetector(g);
		var ev = new List<SessionEndDetector.EndEvent>();
		Action<DateTime, bool> feed = (t, l) => ev.AddRange(det.Update(t, l, subj, null));
		feed(D(2026, 10, 25, 17, 7), false); feed(D(2026, 10, 26, 15, 15), false);   // 'E': close == 15:15 end of session 0
		feed(D(2026, 10, 26, 15, 31), false); feed(D(2026, 10, 26, 16, 0), true);     // 'L': data stops 16:00 with LastBarInSession
		feed(D(2026, 10, 26, 16, 0), true);                                            // duplicate CalcBar: no second event
		feed(D(2026, 10, 26, 17, 1), false);                                           // next session: previous already fired
		feed(D(2026, 10, 27, 15, 0), false); feed(D(2026, 10, 27, 15, 31), false);    // gap 15:00 -> 15:31 skips the 15:15 end: 'N' on 15:00
		feed(D(2026, 10, 27, 15, 59), false); feed(D(2026, 10, 27, 17, 1), false);    // no LastBarInSession: 'N' attributed to 15:59
		Check(ev.Count == 4, "T16 4 session ends: " + string.Join(" ", ev.Select(x => x.Why + "@" + x.BarTime.ToString("MM-dd HH:mm", Inv))));
		if (ev.Count == 4)
		{
			Check(ev[0].Why == 'E' && ev[0].Index == 0 && ev[0].SessionEnd == D(2026, 10, 26, 15, 15) && ev[0].TradingDay == D(2026, 10, 26), "T16 (a) end time");
			Check(ev[1].Why == 'L' && ev[1].Index == 1 && ev[1].SessionEnd == D(2026, 10, 26, 16, 30) && ev[1].BarTime == D(2026, 10, 26, 16, 0), "T16 (c) LastBarInSession at 16:00 for the 16:30 end");
			Check(ev[2].Why == 'N' && ev[2].BarTime == D(2026, 10, 27, 15, 0) && ev[2].TradingDay == D(2026, 10, 27), "T16 (b) next session, attributed to the previous bar");
			Check(ev[3].Why == 'N' && ev[3].BarTime == D(2026, 10, 27, 15, 59) && ev[3].SessionEnd == D(2026, 10, 27, 16, 30), "T16 (b) last session of the day");
		}
		var e = Throws(s => det.Update(D(2026, 10, 31, 12, 0), false, subj, s), "T16 outside");
		Check(e != null && e.Reason == "sessions:bar-outside", "T16 bar outside -> sessions:bar-outside");
		// fixtures: each logged LastBarInSession bar fires exactly one 'L' (or 'E') event
		foreach (var d in Dumps())
		{
			var dd = new SessionEndDetector(d.FirstBar); int n = 0;
			foreach (var t in d.SessionEndBars) { var r = dd.Update(t, true, subj, null); n += r.Count(x => x.Why == 'L' || x.Why == 'E'); }
			// several fixtures log the same bar twice (repeat CalcBar on the session-end bar): deduped to one event per distinct bar
			Check(n == d.SessionEndBars.Distinct().Count(), "T16 " + d.File + " one end per distinct LastBarInSession bar (" + n + "/" + d.SessionEndBars.Count + ")");
		}
	}

	static int Main()
	{
		foreach (var t in new Action[] { T2Roots, T3Checks, T4Expiry, T5Digest, T6Windows, T7TradingDay, T8Derived, FxCrypto, T9Costs, T11Texts, T13Hash, T16SessionEnds })
		{
			try { t(); } catch (Exception ex) { Check(false, t.Method.Name + " threw " + ex.GetType().Name + ": " + ex.Message); }
		}
		Check(Dumps().Count == 13, "13 PR-M0 fixtures");
		Console.WriteLine("InstrumentTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}
}
