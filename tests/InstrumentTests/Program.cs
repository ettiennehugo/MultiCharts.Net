using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PowerLanguage.Function;
using S = PowerLanguage.Function._MtfMath.Session;

// Off-MC tests for M-INSTR PR-M2 + PR-M3 (MI-T12 line formats, InstrumentStreams wiring) + PR-M4 (enforcement, Class:ROOT cost keys, floor src)
// (specs/m-instr-spec.md rev 2.4 §10, §11.1): MI-T2..T9, T11, T13, T16 (core) plus synthetic Spot FX / crypto cases (their presets / class
// constants keep their VERIFY markers). Fixtures: the 13 recorded PR-M0 dump runs (§4.4.2; the dump signal itself was removed in e1f5b11).
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
			infoBigPointValue = bpv, structBigPointValue = bpv, currency = ccy, exchange = "CME", sessionName = "test", symbolExpiry = new DateTime(2038, 1, 1),
			// PR-M4 (D2): the bar zone IS the exchange zone (an Exchange-tz chart on CME), so the M3 pins keep their bar-time session text
			barZone = CT, exchangeZone = CT, barZoneSetting = "Exchange", exchangeZoneId = "Central Standard Time" };
	}
	static readonly TimeZoneInfo CT = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"), ET = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
	/// <summary>PR-M4 (D2): first bar time for WithSessions / FirstBar (a Monday; ET↔CT conversions are week-independent).</summary>
	static readonly DateTime FB = new DateTime(2026, 1, 5, 17, 0, 0);

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
		Check(e != null && e.Reason == "mismatch:tick", "T3 ES MinMove 0 -> mismatch:tick (preset cross-check runs before the tick refusal, PR-M3 review)");
		e = Throws(s => InstrumentProfile.Build(In("AAPL", EAssetClass.Stock, "AAPL", 0, 100, 1), null, s), "T3 tick0 no preset");
		Check(e != null && e.Reason == "tick", "T3 MinMove 0 without a preset -> tick");
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
		var six = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null);
		Check(5 / 99999.999999999985 != 0.00005 && six.tick == 0.00005 && six.bigPointValue == 125000 && six.tickValue == 0.00005 * 125000,
			"T3 6E binary priceScale passes the cross-check; the profile carries the PRESET's exact tick / bpv (PR-M3 review)");
		var sixBad = Throws(s => InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 12500), null, s), "T3 6E bpv");
		Check(sixBad != null && sixBad.Reason == "mismatch:bigPointValue" && sixBad.Message.Contains("(detected 12500 vs preset 125000)"), "T3 mismatch detail keeps the raw detected value: " + (sixBad == null ? "" : sixBad.Message));
		var sixTick = Throws(s => InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 10, 99999.999999999985, 125000), null, s), "T3 6E tick");
		Check(sixTick != null && sixTick.Reason == "mismatch:tick" && sixTick.Message.Contains("(detected " + InstrumentText.Num(10 / 99999.999999999985) + " vs preset 5E-05)"), "T3 tick mismatch detail is the raw R value: " + (sixTick == null ? "" : sixTick.Message));
		Check(InstrumentText.Fact(5 / 99999.999999999985) == "5E-05" && InstrumentText.Fact(0.00005 * 125000) == "6.25" && InstrumentText.Fact(0.015625) == "0.015625" && InstrumentText.Fact(99999.999999999985) == "100000",
			"T3 Fact (G15) is runtime-independent for the profile facts");
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
		string csvText = File.ReadAllText(Fx("expiry-worked.csv"));
		Check(csvText.Contains("checked by the analyst") && csvText.Contains("roll = preset roll rule applied by hand") && csvText.Contains("not a vendor date"),
			"T4 fixture distinguishes analyst-checked expiry dates from hand-computed roll dates (not a vendor splice)");
		foreach (var raw in File.ReadAllLines(Fx("expiry-worked.csv")))
		{
			string l = raw.Trim(); if (l.Length == 0 || l[0] == '#') continue;
			var c = l.Split(',');
			var p = InstrumentPresets.Find(c[0], EAssetClass.Future);
			Check(p.calendar == (c[0] == "CL" ? ECalendar.CmeEnergy : c[0] == "GC" ? ECalendar.CmeMetals : c[0] == "ZN" ? ECalendar.CmeRates : ECalendar.CmeFx), "T4 " + c[0] + " preset uses its CME product calendar");
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
		Check(six.roll == ERollRule.BusinessDaysBeforeExpiry && six.rollDays == 5 && six.calendar == ECalendar.CmeFx && six.verify == null, "T4 6E roll = BusinessDaysBeforeExpiry(5) on CmeFx (Ettienne 2026-11-04)");
		// a row with rules but no business-day calendar: PR-M4 rejects it in PresetRow.Validate (Error line, then throw unknown-field:config),
		// before Build's 'calendar' refusal (no silent default either way)
		foreach (var cal in new[] { ECalendar.Observed, ECalendar.None })
		{
			var row = new InstrumentPresetRow("CL", EAssetClass.Future, 0.01, 1000, "USD", MonthMask.All, EExpiryRule.CL25thMinus3Bd, ERollRule.BusinessDaysBeforeExpiry, 2, cal, "ENERGY", "test", null);
			var ov = new InstrumentConfig { overrides = row };
			string ve = row.Validate();
			Check(ve != null && ve.Contains("calendar=" + InstrumentText.CalendarName(cal)), "T4 PresetRow.Validate rejects calendar " + InstrumentText.CalendarName(cal) + " [" + ve + "]");
			var ce = Throws(s => InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), ov, s), "T4 no calendar " + cal);
			Check(ce != null && ce.Reason == InstrumentReason.UnknownField("config") && ce.Message.Contains("calendar=" + InstrumentText.CalendarName(cal)), "T4 override with rules and calendar " + InstrumentText.CalendarName(cal) + " -> Error + unknown-field:config");
		}
		Check(InstrumentPresets.Rows.All(r => r.Validate() == null), "T4 every built-in preset row validates (each has a business-day calendar)");
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
		Check(e != null && e.Reason == InstrumentReason.Months && e.Reason == "months", "T4 no listed month -> Error + months (PR-M3, #96 nit 2; was calendar)");
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
		var esP = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null);
		Check(InstrumentRules.IsEquityIndexQuarterly(esP), "T5 built ES profile is equity-index quarterly (host keeps EventRules for expiry queries)");
		var collected = new List<DateTime>();
		InstrumentRules.CollectRollDates(esP, 2000, 2035, "ROLL", null, collected);
		Check(collected.Count == 144 && _RollDetector.DatesDigest(collected) == "fdd63bd8f179a8d4", "T5 CollectRollDates digest fdd63bd8f179a8d4");
		bool order = collected.Count == dates.Count;
		for (int i = 0; order && i < dates.Count; i++) if (collected[i] != dates[i]) order = false;
		Check(order, "T5 CollectRollDates == EventRules.RollDate in quarter order");
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
		// M5: CollectRollDates is contract-year, not "rolls whose calendar date is in the year".
		// CLF24 rolls 2023-12-15 (inside the 2024 contract year). CLF25 rolls 2024-12-17 (January 2025 contract, not in 2024).
		var clB = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null);
		var gcB = InstrumentProfile.Build(In("@GC", EAssetClass.Future, "GC", 1, 10, 100), null, null);
		var znB = InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null, null);
		var sixB = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 1, 20000, 125000), null, null);
		foreach (var pair in new[] { new object[] { clB, 12 }, new object[] { gcB, 6 }, new object[] { znB, 4 }, new object[] { sixB, 4 } })
		{
			var bp = (InstrumentProfile)pair[0];
			int wantN = (int)pair[1];
			var bag = new List<DateTime>();
			InstrumentRules.CollectRollDates(bp, 2024, 2024, "DVP", null, bag);
			var expect = new List<DateTime>();
			var row = InstrumentRules.RollRow(bp);
			for (int m = 1; m <= 12; m++) if (MonthMask.Has(bp.contractMonths, m)) expect.Add(InstrumentRules.Roll(row, 2024, m, bp.isBusinessDay, RS, null).Value.Date);
			bag.Sort(); expect.Sort();
			bool same = bag.Count == expect.Count;
			for (int i = 0; same && i < bag.Count; i++) if (bag[i] != expect[i]) same = false;
			Check(same && bag.Count == wantN, "T6 CollectRollDates " + bp.root + " 2024 contract-year equals one roll per listed month (" + bag.Count + ")");
			Check(InstrumentCalendars.IsProduct(bp.calendar) && !InstrumentRules.IsEquityIndexQuarterly(bp), "T6 " + bp.root + " uses a CME product calendar, not the equity-index path");
		}
		var cl2024 = new List<DateTime>();
		InstrumentRules.CollectRollDates(clB, 2024, 2024, "DVP", null, cl2024);
		var cl2025 = new List<DateTime>();
		InstrumentRules.CollectRollDates(clB, 2025, 2025, "DVP", null, cl2025);
		Check(cl2024.Contains(D(2023, 12, 15)) && !cl2024.Contains(D(2024, 12, 17)) && cl2025.Contains(D(2024, 12, 17)) && !cl2025.Contains(D(2023, 12, 15)),
			"T6 CL contract-year endpoints: 2024 includes CLF24 roll 2023-12-15 and omits CLF25 roll 2024-12-17; 2025 is the reverse");
		Check(cl2024.Min() == D(2023, 12, 15) && cl2024.Max() == InstrumentRules.Roll(InstrumentRules.RollRow(clB), 2024, 12, clB.isBusinessDay, RS, null).Value.Date,
			"T6 CL 2024 contract-year lower endpoint is the January contract's prior-December roll; upper is the December contract");
		DateTime gcFebRoll = InstrumentRules.Roll(InstrumentRules.RollRow(gcB), 2024, 2, gcB.isBusinessDay, RS, null).Value.Date;
		var gc2024 = new List<DateTime>();
		InstrumentRules.CollectRollDates(gcB, 2024, 2024, "DVP", null, gc2024);
		Check(gc2024.Contains(gcFebRoll) && gcFebRoll.Year == 2024 && gcFebRoll.Month < 2 && gc2024.TrueForAll(d => d.Year == 2024),
			"T6 GC 2024 contract-year rolls stay in 2024; February's roll is in January (month before the contract), not a prior calendar year (" + Ds(gcFebRoll) + ")");
		DateTime esEx, clEx;
		bool esWin = _RollDetector.SeriesWindow(new DateTime(2020, 4, 15), out esEx);
		var clRow = InstrumentPresets.Find("CL", EAssetClass.Future);
		_RollDetector.SeriesWindowFn clWin = (DateTime day, out DateTime ex) => InstrumentRules.SeriesWindow(clRow, day, EventRules.IsNyseBusinessDay, RS, null, out ex);
		bool clHit = clWin(new DateTime(2020, 4, 15), out clEx);
		Check(!esWin && clHit && clEx == new DateTime(2020, 4, 21), "T6 2020-04-15 is inside the CL window and outside the ES quarterly window");
		var clDet = new _RollDetector { series = true, gapTicks = 8, seriesWindow = clWin };
		clDet.Step(1, new DateTime(2020, 4, 14), 40, 40, "CL", 0.01);
		Check(clDet.Step(2, new DateTime(2020, 4, 15), 40.10, 40, "CL", 0.01), "T6 detector seriesWindow delegate takes the CL roll week (10 ticks)");
		var esDet = new _RollDetector { series = true };
		esDet.Step(1, new DateTime(2020, 4, 14), 3000, 3000, "ES", 0.25);
		Check(!esDet.Step(2, new DateTime(2020, 4, 15), 3003, 3000, "ES", 0.25), "T6 null seriesWindow stays the ES quarterly window (2020-04-15 out)");
		var bare = InstrumentProfile.Build(In("6BZ26", EAssetClass.Future, "6B", 1, 10000, 62500), null, null);
		var miss = Throws(s => InstrumentRules.CollectRollDates(bare, 2020, 2021, "ROLL", s, new List<DateTime>()), "T6 no preset");
		Check(miss != null && miss.Reason == InstrumentReason.UnknownField("roll") && miss.Message.Contains("add a preset row / cost for 6B"), "T6 S5 no preset -> unknown-field:roll (" + (miss == null ? "" : miss.Message) + ")");
		var ov = new InstrumentConfig { overrides = new InstrumentPresetRow("CL", EAssetClass.Future, 0.01, 1000, "USD", MonthMask.All, EExpiryRule.CL25thMinus3Bd, ERollRule.None, 0, ECalendar.NyseStandIn, "ENERGY", "t", null) };
		var noneP = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), ov, null);
		var noneE = Throws(s => InstrumentRules.CollectRollDates(noneP, 2024, 2024, "DVP", s, new List<DateTime>()), "T6 roll None");
		Check(noneE != null && noneE.Reason == InstrumentReason.UnknownField("roll") && noneE.Message.Contains("roll rule is None"), "T6 S5 roll None -> unknown-field:roll");
		var rangeE = Throws(s => InstrumentRules.CollectRollDates(clB, 2025, 2024, "ROLL", s, new List<DateTime>()), "T6 year range");
		Check(rangeE != null && rangeE.Reason == InstrumentReason.UnknownField("roll") && rangeE.Message.Contains("2025..2024")
			&& rangeE.Action.Contains("yearFrom") && rangeE.Message.IndexOf("add a preset row", StringComparison.Ordinal) < 0,
			"T6 inverted contract-year range tells the caller to fix the years (" + (rangeE == null ? "" : rangeE.Message) + ")");
		var ev = new _EventCalendar { config = new EventConfig { enabled = true }, ownSource = EOwnContractSource.Profile };
		ev.contract = delegate (DateTime day, out DateTime exp, out DateTime roll) { exp = new DateTime(2024, 4, 21); roll = new DateTime(2024, 4, 17); return true; };
		ev.contractCalendarVerified = false;
		int refused = 0;
		ev.contractRefuse = delegate (string _) { refused++; throw new InvalidOperationException("refused"); };
		bool threw = false;
		try { ev.SessionsToExpiry(new DateTime(2024, 4, 18)); }
		catch (InvalidOperationException) { threw = true; }
		Check(threw && refused == 1, "T6 SessionsToExpiry on an unverified bound calendar calls contractRefuse and does not count");
		Check(ev.IsRollWeek(new DateTime(2024, 4, 18)) && ev.IsRollWeek(new DateTime(2024, 4, 21)) && !ev.IsRollWeek(new DateTime(2024, 4, 16)) && !ev.IsRollWeek(new DateTime(2024, 4, 22)),
			"T6 IsRollWeek uses the bound roll/expiry and does not refuse");
		Check(ev.ContractRollDate(new DateTime(2024, 4, 18)) == new DateTime(2024, 4, 17), "T6 ContractRollDate follows the bound profile");
		Check(!InstrumentRules.HasRollFacts(bare) && InstrumentRules.HasRollFacts(clB), "T6 HasRollFacts: preset yes, bare future no");
		// CLK20: static equity-index answers stay June; the bound engine's own queries, spans, and classification use April.
		// SessionsToExpiry still refuses (S4) and the Error line is the exception text.
		DateTime clk = new DateTime(2020, 4, 17);
		Check(_EventCalendar.RuleRollDate(clk) == new DateTime(2020, 6, 11), "T6 static RuleRollDate(2020-04-17) stays the equity-index roll 2020-06-11");
		bool genJune = false;
		foreach (var rec in EventRules.Generate(2020, false))
			if (rec.kind == EEventKind.QuarterlyExpiry && rec.etTime.Date == new DateTime(2020, 6, 19)) genJune = true;
		Check(genJune, "T6 static Generate(2020) still emits the equity-index QuarterlyExpiry 2020-06-19");
		var clEng = new _EventCalendar { config = new EventConfig { enabled = true }, ownSource = EOwnContractSource.Profile };
		var clSub = clB.Subject("EVENT");
		Action<string> clErr = null;
		var clSink = new List<string>();
		clErr = s => clSink.Add(s);
		var clRowB = InstrumentRules.RollRow(clB);
		clEng.contract = delegate (DateTime day, out DateTime expiry, out DateTime roll)
		{
			int cy, cm;
			InstrumentRules.ContractFor(clRowB, day, clB.isBusinessDay, clSub, clErr, out cy, out cm);
			expiry = InstrumentRules.Expiry(clRowB.expiry, cy, cm, clB.isBusinessDay, clSub, clErr);
			DateTime? rr = InstrumentRules.Roll(clRowB, cy, cm, clB.isBusinessDay, clSub, clErr);
			roll = rr ?? expiry;
			return rr.HasValue;
		};
		clEng.contractCalendarVerified = false;
		clEng.contractRefuse = delegate (string _)
		{
			InstrumentError.Fail(clErr, clSub, InstrumentReason.Calendar,
				"session count needs a verified holiday calendar; " + InstrumentText.CalendarName(clB.calendar) + " serves expiry/roll business days only (CME calendar pending M8)",
				"add a verified calendar for " + clB.root + " (PR-M8)");
		};
		clEng.error = clErr;
		string cfgErr = clEng.Configure();
		Check(cfgErr == null, "T6 CL engine Configure (" + (cfgErr ?? "ok") + ")");
		Check(clEng.ContractRollDate(clk) == clk && clEng.ContractExpiryDate(clk) == new DateTime(2020, 4, 21) && clEng.IsRollWeek(clk) && !clEng.IsRollWeek(new DateTime(2020, 6, 11)),
			"T6 bound CLK20 ContractRollDate/Expiry/IsRollWeek are 2020-04-17/2020-04-21, not the June equity-index roll");
		EventDay apr21 = clEng.Classify(new DateTime(2020, 4, 21)), jun19 = clEng.Classify(new DateTime(2020, 6, 19));
		bool aprExp = false, junExp = false, claims = false;
		foreach (var rec in clEng.EventsOn(new DateTime(2020, 4, 21))) if (rec.kind == EEventKind.QuarterlyExpiry) aprExp = true;
		foreach (var rec in clEng.EventsOn(new DateTime(2020, 6, 19))) if (rec.kind == EEventKind.QuarterlyExpiry) junExp = true;
		foreach (var rec in clEng.EventsOn(new DateTime(2020, 1, 2))) if (rec.kind == EEventKind.Claims) claims = true;
		DateTime aprUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(new DateTime(2020, 4, 17, 12, 0, 0), DateTimeKind.Unspecified), clEng.Eastern);
		DateTime junUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(new DateTime(2020, 6, 11, 12, 0, 0), DateTimeKind.Unspecified), clEng.Eastern);
		Check(apr21.ownContract == EOwnContractStatus.Evaluated && apr21.isQuarterlyExpiry == true && apr21.isRollWeek == true
			&& jun19.isQuarterlyExpiry == false && jun19.isOpex && aprExp && !junExp && claims
			&& clEng.InSpan(EEventKind.RollWeek, aprUtc) && !clEng.InSpan(EEventKind.RollWeek, junUtc),
			"T6 bound CL classification uses April own-expiry, keeps June opex and January claims, and InSpan follows the profile span");
		var s4 = Throws(s => { clSink.Clear(); clEng.contractRefuse = delegate (string __) { InstrumentError.Fail(s, clSub, InstrumentReason.Calendar, "session count needs a verified holiday calendar; " + InstrumentText.CalendarName(clB.calendar) + " serves expiry/roll business days only (CME calendar pending M8)", "add a verified calendar for " + clB.root + " (PR-M8)"); }; clEng.SessionsToExpiry(clk); }, "T6 CL SessionsToExpiry");
		Check(s4 != null && s4.Reason == InstrumentReason.Calendar && s4.Message.Contains("add a verified calendar for CL (PR-M8)") && s4.Message.Contains("CmeEnergy"),
			"T6 SessionsToExpiry with the verified flag off is Error + calendar, not a count (" + (s4 == null ? "" : s4.Message) + ")");
		clEng.contractCalendarVerified = true;
		clEng.countsAsSession = clB.isBusinessDay;
		int clSess = clEng.SessionsToExpiry(clk);
		Check(clSess > 0, "T6 CL SessionsToExpiry counts on the verified energy calendar (" + clSess + ")");
		string evTest = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "_EventCalendarTest.Strategy.CS")));
		string step = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "_CustomSignal.Strategy.CS")));
		int stepAt = step.IndexOf("private void stepEvent()", StringComparison.Ordinal);
		int stepEnd = stepAt < 0 ? -1 : step.IndexOf("private void publishEvent", stepAt, StringComparison.Ordinal);
		string stepBody = stepAt >= 0 && stepEnd > stepAt ? step.Substring(stepAt, stepEnd - stepAt) : "";
		var clSharedOnly = new _EventCalendar { config = new EventConfig { enabled = true, sharedCalendar = true, ownContract = false }, ownSource = EOwnContractSource.Profile, contract = clEng.contract };
		var clOwnOnly = new _EventCalendar { config = new EventConfig { enabled = true, sharedCalendar = false, ownContract = true }, ownSource = EOwnContractSource.Profile, contract = clEng.contract, contractCalendarVerified = false };
		Check(clSharedOnly.Configure() == null && clOwnOnly.Configure() == null, "T6 CL control engines configure");
		bool sharedOpex = false, sharedQ = false, ownOpex = false, ownQ = false;
		foreach (var r in clSharedOnly.EventsOn(new DateTime(2020, 6, 19))) { if (r.kind == EEventKind.MonthlyOpex) sharedOpex = true; if (r.kind == EEventKind.QuarterlyExpiry) sharedQ = true; }
		foreach (var r in clOwnOnly.EventsOn(new DateTime(2020, 4, 21))) if (r.kind == EEventKind.QuarterlyExpiry) ownQ = true;
		foreach (var r in clOwnOnly.EventsOn(new DateTime(2020, 6, 19))) if (r.kind == EEventKind.MonthlyOpex) ownOpex = true;
		EventDay sharedDay = clSharedOnly.Classify(new DateTime(2020, 6, 19));
		Check(sharedOpex && !sharedQ && sharedDay.ownContract == EOwnContractStatus.NotSelected && sharedDay.isRollWeek == null && sharedDay.isOpex
			&& ownQ && !ownOpex && clOwnOnly.Classify(new DateTime(2020, 4, 21)).ownContract == EOwnContractStatus.Evaluated && clOwnOnly.ContractExpiryDate(new DateTime(2020, 4, 17)) == new DateTime(2020, 4, 21),
			"T6 CL controls: ownContract off keeps June opex; sharedCalendar off keeps the April own expiry and drops opex");
		Check(evTest.Contains("toExpiry({ev.SessionsToExpiry()})") && evTest.Contains("NyseStandIn") && evTest.Contains("Do not catch it")
			&& stepBody.IndexOf("SessionsToExpiry", StringComparison.Ordinal) < 0,
			"T6 smoke calls SessionsToExpiry and says NyseStandIn stops it; stepEvent does not call SessionsToExpiry");
		Check(InstrumentRules.OwnSource(false, clB) == EOwnContractSource.EquityIndex && InstrumentRules.OwnSource(true, null) == EOwnContractSource.EquityIndex
			&& InstrumentRules.OwnSource(true, InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null)) == EOwnContractSource.EquityIndex
			&& InstrumentRules.OwnSource(true, clB) == EOwnContractSource.Profile,
			"T6 off-switch and ES stay EquityIndex; CL with facts is Profile");
		OwnFactsGap(InstrumentProfile.Build(In("AAPL", EAssetClass.Stock, "AAPL", 1, 100, 1), null, null), "AAPL");
		OwnFactsGap(bare, "6B");
	}

	static void BarEv(_EventCalendar e, DateTime et)
	{
		DateTime utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(et, DateTimeKind.Unspecified), e.Eastern);
		e.Step(et, utc.AddMinutes(-1), utc, false);
	}
	static void OwnFactsGap(InstrumentProfile p, string label)
	{
		Check(InstrumentRules.OwnSource(true, p) == EOwnContractSource.Unavailable, label + " profile on, no roll facts → Unavailable");
		var shared = new _EventCalendar { config = new EventConfig { enabled = true, sharedCalendar = true, ownContract = true }, ownSource = EOwnContractSource.Unavailable };
		var macro = new _EventCalendar { config = new EventConfig { enabled = true, classes = EEventClassMask.Macro, sharedCalendar = true, ownContract = true }, ownSource = EOwnContractSource.Unavailable };
		Action<string> sink = null;
		Action refuse = () => InstrumentError.Fail(sink, p.Subject("EVENT"), InstrumentReason.UnknownField("roll"), "no preset expiry, roll rule, contract months, or business-day calendar", "add a preset row / cost for " + p.root);
		shared.ownQueryRefuse = refuse;
		macro.ownQueryRefuse = refuse;
		Check(shared.Configure() == null && macro.Configure() == null, label + " engines configure");
		var journal = new List<string>();
		shared.journal = s => journal.Add(s);
		BarEv(shared, new DateTime(2020, 1, 2, 9, 0, 0));
		BarEv(shared, new DateTime(2020, 6, 19, 10, 0, 0));
		EventDay claims = shared.Classify(new DateTime(2020, 1, 2)), opex = shared.Classify(new DateTime(2020, 6, 19));
		bool hasClaims = false, hasOpex = false, hasOwn = false;
		foreach (var r in shared.EventsOn(new DateTime(2020, 1, 2))) if (r.kind == EEventKind.Claims) hasClaims = true;
		foreach (var r in shared.EventsOn(new DateTime(2020, 6, 19)))
		{
			if (r.kind == EEventKind.MonthlyOpex) hasOpex = true;
			if (r.kind == EEventKind.QuarterlyExpiry) hasOwn = true;
		}
		Check(hasClaims && claims.ownContract == EOwnContractStatus.Unavailable && claims.isRollWeek == null && claims.count > 0
			&& hasOpex && opex.isOpex && !hasOwn && opex.isQuarterlyExpiry == null && opex.ownContract == EOwnContractStatus.Unavailable
			&& journal.Exists(s => s.Contains("Claims@08:30") && s.Contains("ownContract(unavailable)")),
			label + " default Calendar+Macro Step/Classify keeps claims and opex, own expiry unavailable, no ES quarter");
		BarEv(macro, new DateTime(2020, 1, 2, 9, 0, 0));
		EventDay macroDay = macro.Classify(new DateTime(2020, 1, 2));
		bool macroClaims = false, macroOpex = false;
		foreach (var r in macro.EventsOn(new DateTime(2020, 1, 2)))
		{
			if (r.kind == EEventKind.Claims) macroClaims = true;
			if (r.kind == EEventKind.MonthlyOpex) macroOpex = true;
		}
		Check(macroClaims && !macroOpex && macroDay.ownContract == EOwnContractStatus.Unavailable && macroDay.isRollWeek == null && macroDay.count > 0,
			label + " Macro-only Step/Classify keeps claims, drops unselected opex, own expiry unavailable");
		var q = Throws(s => { sink = s; shared.ContractRollDate(new DateTime(2020, 4, 17)); }, label + " query");
		Check(q != null && q.Reason == InstrumentReason.UnknownField("roll") && q.Message.Contains("add a preset row / cost for " + p.root),
			label + " explicit ContractRollDate is Error + unknown-field:roll (" + (q == null ? "" : q.Message) + ")");
		var offOwn = new _EventCalendar { config = new EventConfig { enabled = true, ownContract = false, sharedCalendar = true }, ownSource = EOwnContractSource.Unavailable };
		offOwn.ownQueryRefuse = refuse;
		Check(offOwn.Configure() == null, label + " ownContract off configures");
		EventDay offDay = offOwn.Classify(new DateTime(2020, 6, 19));
		bool offOpex = false, offQ = false;
		foreach (var r in offOwn.EventsOn(new DateTime(2020, 6, 19)))
		{
			if (r.kind == EEventKind.MonthlyOpex) offOpex = true;
			if (r.kind == EEventKind.QuarterlyExpiry) offQ = true;
		}
		Check(offOpex && !offQ && offDay.ownContract == EOwnContractStatus.NotSelected && offDay.isRollWeek == null && offDay.isOpex,
			label + " ownContract off keeps opex and marks own expiry not-selected");
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
			Check(d.StartCalc != null && d.StartCalc.Length > 0 && _MtfMath.SessionMismatch(d.StartCalc, d.FirstBar) == null, "T7 " + d.File + " StartCalc == FirstCalcBar sessions (the S1 comparer)");
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
		var p2 = p.WithSessions(g, g, FB, null);
		Check(p2.hasOvernight && p2.rthSplit.source == ERthSource.EquityIndexLegacy && p.sessions == null && p2.sessions.Length == 9, "T8 WithSessions copies (profile immutable)");
		var ex = Throws(s => p.WithSessions(g, spy.FirstBar, FB, s), "T8 changed");
		Check(ex != null && ex.Reason == "sessions:changed", "T8 StartCalc vs first bar differ -> sessions:changed");
		ex = Throws(s => p.WithSessions(null, g, FB, s), "T8 none");
		Check(ex != null && ex.Reason == "sessions:none", "T8 null sessions -> sessions:none");
		var stk = InstrumentProfile.Build(In("SPY", EAssetClass.Stock, "SPY", 1, 100, 1), null, null);
		Check(stk.calendar == ECalendar.Nyse && stk.presetId == null && stk.sizingUnit == ESizingUnit.Shares && stk.expiry == null && stk.roll == null && !stk.cost.known && stk.costFloorTicks == -1, "T8 SPY: Nyse, no preset, Shares, cost Unknown");
		var cl2 = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null);
		Check(cl2.calendar == ECalendar.CmeEnergy && cl2.isBusinessDay != null && cl2.roll == ERollRule.BusinessDaysBeforeExpiry && cl2.rollDays == 2 && MonthMask.Text(cl2.contractMonths) == "FGHJKMNQUVXZ", "T8 CL preset");
		foreach (var row in InstrumentPresets.Rows) Check(row.Validate() == null, "T8 preset row valid " + row.root + " " + row.Validate());
		Check(new InstrumentPresetRow("es", EAssetClass.Future, 0, 50, "EUR", 0, EExpiryRule.ThirdFridaySoq, ERollRule.None, -1, ECalendar.None, "", "", null).Validate() != null, "T8 bad preset row rejected");
	}

	// ---------------------------------------------------------------- synthetic Spot FX / crypto (VERIFY constants)
	static void FxCrypto()
	{
		var fx = Daily(DayOfWeek.Monday, DayOfWeek.Friday, new TimeSpan(17, 0, 0), new TimeSpan(17, 0, 0), true);
		var p = InstrumentProfile.Build(In("EUR/USD", EAssetClass.SpotFx, "EUR/USD", 1, 100000, 1), null, null).WithSessions(fx, fx, FB, null);
		Check(p.root == "EURUSD" && p.presetId == null && p.calendar == ECalendar.Observed && p.sessionsPerYear == 260 && p.sizingUnit == ESizingUnit.Units && !p.cost.known, "FX profile (sessionsPerYear 260 VERIFY)");
		Check(p.hasOvernight && p.rthSplit == null, "FX 24h -> overnight, no rth split");
		Check(InstrumentPresets.ClassVerify(EAssetClass.SpotFx).StartsWith("VERIFY", StringComparison.Ordinal) && InstrumentPresets.ClassVerify(EAssetClass.Crypto).StartsWith("VERIFY", StringComparison.Ordinal), "FX/crypto keep VERIFY markers");
		var c = new List<S>(); for (var d = DayOfWeek.Sunday; d <= DayOfWeek.Saturday; d++) c.Add(new S(d, TimeSpan.Zero, d, TimeSpan.FromDays(1)));
		var cp = InstrumentProfile.Build(In("BTCUSD", EAssetClass.Crypto, "", 1, 100, 1), null, null).WithSessions(c.ToArray(), c.ToArray(), FB, null);
		Check(cp.root == "BTCUSD" && cp.rootSource == "parsed" && cp.sessionsPerYear == 365 && cp.calendar == ECalendar.Observed && cp.sizingUnit == ESizingUnit.Units, "crypto profile");
		Check(!cp.hasOvernight && cp.rthSplit != null && cp.rthSplit.source == ERthSource.ChartDaySession, "crypto 00:00-24:00 literal: no overnight (StartDay == EndDay), day split; MC template VERIFY");
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
		cfg.costs["Future:ZN"] = 1000;
		e = Throws(s => InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), cfg, s), "T9 ZN cfg");
		Check(e != null && e.Reason == "mismatch:tickValue", "T9 ZN with a cost throws mismatch:tickValue");
		Check(!InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null, null).cost.known, "T9 ZN without cost -> Unknown, no throw");
		var bad = new InstrumentConfig(); bad.costs["es"] = 0;
		Check(bad.Validate() != null && bad.Validate().StartsWith("InstrumentConfig: ", StringComparison.Ordinal), "T9 Validate rejects bad costs");
		e = Throws(s => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), bad, s), "T9 badcfg");
		Check(e != null && e.Reason == "unknown-field:config", "T9 invalid config refuses");
		// PR-M4 D1: costs keyed <Class>:<ROOT>; a root-only key or an unknown class is refused (unknown-field:config)
		Check(InstrumentConfig.CostKey(EAssetClass.Future, "ES") == "Future:ES" && InstrumentConfig.CostKey(EAssetClass.Stock, "ES") == "Stock:ES", "T9 D1 CostKey = <Class>:<ROOT>");
		Check(new InstrumentConfig().costs.Count == 2 && new InstrumentConfig().costs["Future:ES"] == 1700 && new InstrumentConfig().costs["Future:MES"] == 250, "T9 D1 defaults Future:ES 1700, Future:MES 250");
		foreach (var key in new[] { "ES", "Futures:ES", "Unsupported:ES", "Future:", "Future:es", "Future: ES", "future:ES", ":ES", "Future:ES:X" })
		{
			var rk = new InstrumentConfig(); rk.costs[key] = 1700;
			string ve = rk.Validate();
			Check(ve != null && ve.Contains("costs key " + key + " (expected <Class>:<ROOT>, e.g. Future:ES)"), "T9 D1 Validate rejects costs key '" + key + "' [" + ve + "]");
			var ke = Throws(sk => InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), rk, sk), "T9 D1 key " + key);
			Check(ke != null && ke.Reason == "unknown-field:config", "T9 D1 costs key '" + key + "' -> Error + unknown-field:config");
		}
		foreach (var cls in new[] { EAssetClass.Future, EAssetClass.Stock, EAssetClass.SpotFx, EAssetClass.Crypto, EAssetClass.Index })
		{ var ok = new InstrumentConfig(); ok.costs[InstrumentConfig.CostKey(cls, "ABC")] = 100; Check(ok.Validate() == null, "T9 D1 " + InstrumentText.ClassName(cls) + ":ABC accepted"); }
		Check(new InstrumentConfig().CostFor(EAssetClass.Future, "ES").roundTripCents == 1700 && !new InstrumentConfig().CostFor(EAssetClass.Stock, "ES").known
			&& !new InstrumentConfig().CostFor(EAssetClass.Unsupported, "ES").known && !new InstrumentConfig().CostFor(EAssetClass.Future, null).known, "T9 D1 CostFor reads only the symbol's own class row");
		Check(new InstrumentConfig().Canon().Contains(";costs=[Future:ES:1700,Future:MES:250,];"), "T9 D1 Canon costs=[Future:ES:1700,Future:MES:250,]: " + new InstrumentConfig().Canon());
		var a = new InstrumentConfig(); var b = new InstrumentConfig { costs = new Dictionary<string, long> { { "Future:MES", 250 }, { "Future:ES", 1700 } } };
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
		var a = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		var b = InstrumentProfile.Build(In("ESZ26", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		Check(a.ProfileHash() == b.ProfileHash() && a.ProfileHash().Length == 16, "T13 same facts -> same hash (name not hashed)");
		var m = InstrumentProfile.Build(In("@MES", EAssetClass.Future, "MES", 25, 100, 5), null, null).WithSessions(g, g, FB, null);
		Check(a.ProfileHash() != m.ProfileHash(), "T13 MES differs");
		var spy = Dumps().First(d => d.Name == "SPY").FirstBar;
		Check(InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(spy, spy, FB, null).ProfileHash() != a.ProfileHash(), "T13 sessions change the hash");
		var cfg = new InstrumentConfig();
		Check(!cfg.Canon().Contains(a.ProfileHash()) && !cfg.Canon().Contains("profileHash"), "T13 profileHash outside InstrumentConfig.Canon (Q7)");
		Check(a.ProfileHash() == InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null).ProfileHash(), "T13 deterministic");
		string line = a.InstrLine(1, "Future", "Exchange", "Volume");
		Check(line.StartsWith("INSTR ds(1) name(@ES) cat(Future) class(Future) root(ES rootSrc=mc) preset(ES) exch(CME) tz(Exchange) sessionName(test) sessions(n=9 Sun 17:00→Mon 15:15;", StringComparison.Ordinal)
			&& line.Contains(" dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(09:30-16:15 src=EquityIndexLegacy) tick(0.25) bpv(50) tickValue(12.5) ccy(USD) bvo(Volume) calendar(CmeEquityIndex) months(HMUZ) expiry(ThirdFridaySoq) roll(EquityIndexCustomary) cost(17.00 src=Ettienne 2026-10-31) floor(14) cluster(EQIDX) profileHash(" + a.ProfileHash() + ")"), "T13 INSTR line: " + line);
		Check(!line.Contains("stand-in"), "T13 ES line has no stand-in note");
		var clLine = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null).WithSessions(g, g, FB, null).InstrLine(1, "Future", "Exchange", "Volume");
		Check(clLine.Contains(" calendar(CmeEnergy) months(FGHJKMNQUVXZ) expiry(CL25thMinus3Bd) roll(BusinessDaysBeforeExpiry(2))"), "T13 CL INSTR line names the energy calendar: " + clLine);
		var sixLine = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(g, g, FB, null).InstrLine(1, "Future", "Exchange", "Volume");
		Check(sixLine.Contains("calendar(CmeFx)") && sixLine.Contains("roll(BusinessDaysBeforeExpiry(5))") && !sixLine.Contains("pending M8"), "T13 6E INSTR line: " + sixLine);
		Console.WriteLine("ES GLOBEX profileHash " + a.ProfileHash());
		// PR-M3 review: literal pins (computed-only checks missed the runtime-dependent "R" text). Canon uses G15 / preset values, so
		// the same hashes come out of MC (.NET Framework) and the box tools (.NET 8).
		var six = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(g, g, FB, null);
		Console.WriteLine("6E GLOBEX profileHash " + six.ProfileHash());
		Check(a.ProfileHash() == "020deca78d6392b7", "T13 ES GLOBEX profileHash pinned: " + a.ProfileHash());
		Check(six.ProfileHash() == "d60dd6584c2159e9", "T13 6E GLOBEX profileHash pinned: " + six.ProfileHash());
		Check(six.Canon().Contains(";tick=5E-05;bpv=125000;ccy=USD;"), "T13 6E Canon carries the preset tick / bpv: " + six.Canon());
		Check(sixLine == "INSTR ds(1) name(@6E) cat(Future) class(Future) root(6E rootSrc=mc) preset(6E) exch(CME) tz(Exchange) sessionName(test) sessions(n=9 Sun 17:00→Mon 15:15; Mon 15:30→Mon 16:30; Mon 17:00→Tue 15:15; Tue 15:30→Tue 16:30; Tue 17:00→Wed 15:15; Wed 15:30→Wed 16:30; Wed 17:00→Thu 15:15; Thu 15:30→Thu 16:30; Thu 17:00→Fri 15:15) xsessions(Central Standard Time n=9 Sun 17:00→Mon 15:15; Mon 15:30→Mon 16:30; Mon 17:00→Tue 15:15; Tue 15:30→Tue 16:30; Tue 17:00→Wed 15:15; Wed 15:30→Wed 16:30; Wed 17:00→Thu 15:15; Thu 15:30→Thu 16:30; Thu 17:00→Fri 15:15; rth none) dayKey(SessionEnd) dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(none src=None) tick(5E-05) bpv(125000) tickValue(6.25) ccy(USD) bvo(Volume) calendar(CmeFx) months(HMUZ) expiry(ThirdWedMinus2Bd) roll(BusinessDaysBeforeExpiry(5)) cost(Unknown) floor(Unknown src=no-cost) cluster(FX_EUR) profileHash(d60dd6584c2159e9)", "T13 6E INSTR line pinned in full: " + sixLine);
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

	// ---------------------------------------------------------------- PR-M3: MI-T12 line formats + InstrumentStreams wiring
	static void T12Lines()
	{
		var g = Globex9();
		CultureInfo saved = CultureInfo.CurrentCulture, savedUi = CultureInfo.CurrentUICulture;
		Func<string[]> lines = () =>
		{
			var st = new InstrumentStreams(3);
			var warn = new List<string>(); var err = new List<string>();
			st.Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, new InstrumentConfig(), err.Add, warn.Add);
			st.Start(2, In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), g, new InstrumentConfig(), err.Add, warn.Add);
			string l1 = st.FirstBar(1, g, FB, "Future", "Exchange→Central Standard Time", "Volume", err.Add);
			string l2 = st.FirstBar(2, g, FB, "Future", "Exchange→Central Standard Time", "Volume", err.Add);
			return new[] { l1, l2, st.RunSuffix(), string.Join("|", warn), string.Join("|", err) };
		};
		string[] inv, de;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture; inv = lines();
			CultureInfo.CurrentCulture = new CultureInfo("de-DE"); CultureInfo.CurrentUICulture = new CultureInfo("de-DE"); de = lines();
			Check(1.5.ToString() == "1,5", "T12 de-DE is active for the second run (decimal comma)");
		}
		finally { CultureInfo.CurrentCulture = saved; CultureInfo.CurrentUICulture = savedUi; }
		for (int i = 0; i < inv.Length; i++) Check(inv[i] == de[i], "T12 culture-invariant under de-DE [" + i + "]: " + de[i]);
		var es = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		// pinned whole line (ES GLOBEX fixture, default InstrumentConfig)
		string pin = "INSTR ds(1) name(@ES) cat(Future) class(Future) root(ES rootSrc=mc) preset(ES) exch(CME) tz(Exchange→Central Standard Time) sessionName(test) sessions("
			+ InstrumentText.Sessions(g) + ") xsessions(Central Standard Time " + InstrumentText.Sessions(g) + "; rth 09:30-16:15) dayKey(SessionEnd) dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(09:30-16:15 src=EquityIndexLegacy) tick(0.25) bpv(50) tickValue(12.5) ccy(USD) bvo(Volume) "
			+ "calendar(CmeEquityIndex) months(HMUZ) expiry(ThirdFridaySoq) roll(EquityIndexCustomary) cost(17.00 src=Ettienne 2026-10-31) floor(14) cluster(EQIDX) profileHash(" + es.ProfileHash() + ")";
		Check(inv[0] == pin, "T12 ES INSTR line pinned: " + inv[0]);
		Check(InstrumentText.Sessions(g).StartsWith("n=9 Sun 17:00→Mon 15:15; ", StringComparison.Ordinal), "T12 sessions text");
		Check(inv[1].StartsWith("INSTR ds(2) name(@6E) cat(Future) class(Future) root(6E rootSrc=mc) preset(6E) ", StringComparison.Ordinal) && inv[1].Contains(" bpv(125000) ") && inv[1].Contains(" ccy(USD) bvo(Volume) calendar(CmeFx) ")
			&& inv[1].Contains("roll(BusinessDaysBeforeExpiry(5))"), "T12 6E INSTR line: " + inv[1]);
		Check(inv[2] == " profileHash(1=" + es.ProfileHash() + ";2=" + InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(g, g, FB, null).ProfileHash() + ")", "T12 RUN suffix: " + inv[2]);
		Check(inv[3] == "" && inv[4] == "", "T12 clean ES/6E run: no Warning, no Error [" + inv[3] + "][" + inv[4] + "]");
		Check(!new InstrumentConfig().Canon().Contains("profileHash"), "T12 profileHash outside InstrumentConfig.Canon (Q7)");
	}

	static void M3Wiring()
	{
		var g = Globex9(); var spy = Dumps().First(d => d.Name == "SPY").FirstBar;
		var cfg = new InstrumentConfig();
		// PR-M4 (§11.1 item 1): every refusal is enforced. Start / FirstBar write exactly one Error line (the refusal), then throw
		Func<Action<Action<string>, Action<string>>, Tuple<InstrumentException, List<string>, List<string>>> run = f =>
		{
			var e1 = new List<string>(); var w1 = new List<string>(); InstrumentException x = null;
			try { f(e1.Add, w1.Add); } catch (InstrumentException e) { x = e; }
			return Tuple.Create(x, e1, w1);
		};
		Func<Tuple<InstrumentException, List<string>, List<string>>, string, bool> erroredWith = (r, why) => r.Item1 != null && r.Item1.Reason == why && r.Item2.Count == 1 && r.Item2[0] == r.Item1.Message;
		// preset cross-check mismatch (ES with a wrong big point value): Error line, then throw from Start
		var r0 = run((e, w) => new InstrumentStreams(2).Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 20), g, cfg, e, w));
		Check(r0.Item1 != null && r0.Item1.Reason.StartsWith("mismatch:", StringComparison.Ordinal) && r0.Item2.Count == 1 && r0.Item2[0] == r0.Item1.Message && r0.Item3.Count == 0, "M3 preset mismatch -> Error line then throw");
		// MC-internal mismatch
		var bad = In("@ES", EAssetClass.Future, "ES", 25, 100, 50); bad.structBigPointValue = 5;
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, bad, g, cfg, e, w)), InstrumentReason.MismatchMcBigPointValue), "M3 MC-internal mismatch -> Error then throw");
		// PR-M4: the former log-only refusals (currency, class, tick, root, calendar, months, config) now write the Error line and throw
		var rc = run((e, w) => new InstrumentStreams(3).Start(1, In("FDAX", EAssetClass.Future, "FDAX", 1, 2, 25, "EUR"), g, cfg, e, w));
		Check(erroredWith(rc, InstrumentReason.Currency) && rc.Item1.Message.StartsWith("M-INSTR: INSTR cannot run on FDAX ", StringComparison.Ordinal) && rc.Item3.Count == 0, "M4 currency (non-USD) -> Error then throw");
		var rk = run((e, w) => new InstrumentStreams(3).Start(2, In("SPX OPT", EAssetClass.Unsupported, "SPX", 1, 100, 100), g, cfg, e, w));
		Check(erroredWith(rk, InstrumentReason.Class) && rk.Item3.Count == 0, "M4 class (Unsupported) -> Error then throw");
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, In("6BZ26", EAssetClass.Future, "6B", 0, 10000, 62500), g, cfg, e, w)), InstrumentReason.Tick), "M4 tick (MinMove 0, no preset) -> Error then throw");
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, In("6BZ26", EAssetClass.Future, "6B", -1, 10000, 62500), g, cfg, e, w)), InstrumentReason.Tick), "M4 tick (negative MinMove, no preset) -> Error then throw");
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, In("", EAssetClass.Future, "", 25, 100, 50), g, cfg, e, w)), InstrumentReason.UnknownField("root")), "M4 unknown-field:root -> Error then throw");
		var badCfg0 = new InstrumentConfig(); badCfg0.costs["XX"] = -1;
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, badCfg0, e, w)), InstrumentReason.UnknownField("config")), "M4 unknown-field:config -> Error then throw");
		var noMonths = new InstrumentConfig { overrides = new InstrumentPresetRow("ES", EAssetClass.Future, 0.25, 50, "USD", 0, EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX", "t", null) };
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, noMonths, e, w)), InstrumentReason.UnknownField("config")), "M4 a future row with no contract month -> unknown-field:config (Validate), Error then throw");
		Check(!typeof(InstrumentStreams).GetMethods().Any(m => m.Name == "Enforced" || m.Name == "LogOnlyLine"), "M4 InstrumentStreams has no Enforced / LogOnlyLine (no log-only path left)");
		// S1: sessions:changed and sessions:none throw on the first bar (Error first)
		var st = new InstrumentStreams(1); var err = new List<string>(); var warn = new List<string>(); InstrumentException ex = null;
		st.Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, cfg, err.Add, warn.Add);
		try { st.FirstBar(1, spy, FB, "Future", "Exchange", "Volume", err.Add); } catch (InstrumentException e) { ex = e; }
		Check(ex != null && ex.Reason == InstrumentReason.SessionsChanged && err.Count == 1 && err[0] == ex.Message && warn.Count == 0, "M3 sessions:changed -> Error then throw");
		st = new InstrumentStreams(1); err.Clear(); warn.Clear(); ex = null;
		st.Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, cfg, err.Add, warn.Add);
		Check(err.Count == 0 && warn.Count == 0 && st.Profile(1) != null, "M4 StartCalc without sessions: instrument part built, the first bar decides");
		try { st.FirstBar(1, null, FB, "Future", "Exchange", "Volume", err.Add); } catch (InstrumentException e) { ex = e; }
		Check(ex != null && ex.Reason == InstrumentReason.SessionsNone && err.Count == 1 && err[0] == ex.Message && warn.Count == 0, "M4 sessions:none -> Error then throw (was a Warning in M3)");
		st = new InstrumentStreams(1); err.Clear();
		st.Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, cfg, err.Add, warn.Add);
		Check(st.FirstBar(1, g, FB, "Future", "Exchange", "Volume", err.Add) != null && st.FirstBar(1, g, FB, "Future", "Exchange", "Volume", err.Add) == null && !st.Pending(1) && err.Count == 0, "M3 first-bar step runs once per stream");
		// root cross-check Warning is logged at StartCalc; absent streams are skipped by RunSuffix
		st = new InstrumentStreams(4); warn.Clear();
		var rw = In("@ES", EAssetClass.Future, "MES", 25, 100, 5);
		st.Start(3, rw, g, cfg, err.Add, warn.Add);
		Check(st.Profile(3) != null && warn.Count == (st.Profile(3).rootWarning == null ? 0 : 1) && st.RunSuffix().StartsWith(" profileHash(3=", StringComparison.Ordinal), "M3 root warning logged once; only started streams on RUN: " + st.RunSuffix() + " " + string.Join("|", warn));
		Check(new InstrumentStreams(2).RunSuffix() == "", "M3 nothing started -> no RUN suffix");
		// PR-M3 review: the cross-checks run before every other refusal, so a refusal cannot hide a mismatch
		Func<InstrumentProfile.Inputs, InstrumentConfig, string> reason = (inp, c) => { var x = Throws(s => InstrumentProfile.Build(inp, c, s), "M3 order " + inp.name); return x == null ? null : x.Reason; };
		Check(reason(In("@ES", EAssetClass.Future, "ES", 25, 100, 5, "EUR"), null) == InstrumentReason.MismatchBigPointValue, "M3 order: preset mismatch beats currency");
		Check(reason(In("@ES", EAssetClass.Future, "ES", 25, 100, 50, "EUR"), null) == InstrumentReason.Currency, "M3 order: matching preset, then currency");
		var mcBad = In("@ES", EAssetClass.Unsupported, "ES", 25, 100, 50); mcBad.structBigPointValue = 5;
		Check(reason(mcBad, null) == InstrumentReason.Class, "M3 order: Unsupported class -> class refusal only, no MC-internal check (review 2)");
		var esOpt = In("ES 6000C", EAssetClass.Unsupported, "ES", 25, 100, 50); esOpt.structBigPointValue = 5; esOpt.structPriceScale = 3;
		Check(reason(esOpt, null) == InstrumentReason.Class, "M3 order: ES futures option (root ES, cost Future:ES, odd ASymbolInfo2) -> class only");
		Check(erroredWith(run((e, w) => new InstrumentStreams(1).Start(1, esOpt, g, cfg, e, w)), InstrumentReason.Class), "M4 streams: an Unsupported chart is Error + throw at StartCalc (was a log-only Warning)");
		// PR-M4 D1: costs are keyed <Class>:<ROOT>; a stock ticker ES never uses the Future:ES row
		var esStock = In("ES", EAssetClass.Stock, "ES", 1, 10000, 1);   // a stock ticker ES with a sub-penny tick: 0.01 cent tick value
		Check(reason(esStock, null) == null, "M4 stock ticker ES: no mismatch:tickValue (no preset, no Stock:ES cost)");
		var esStockP = InstrumentProfile.Build(esStock, null, null).WithSessions(g, g, FB, null);
		string esStockLine = esStockP.InstrLine(1, "Stock", "Exchange", "Volume");
		Check(esStockP.presetId == null && !esStockP.cost.known && esStockP.costFloorTicks == -1 && esStockP.floorSource == FloorSource.CostClassMismatch && esStockP.costMismatchKey == "Future:ES"
			&& esStockLine.Contains(" cost(Unknown) floor(Unknown src=cost-class-mismatch) "), "M4 D1 stock ES: cost(Unknown), floor(Unknown src=cost-class-mismatch): " + esStockLine);
		var rs = run((e, w) => new InstrumentStreams(1).Start(1, esStock, g, cfg, e, w));
		Check(rs.Item1 == null && rs.Item2.Count == 0 && rs.Item3.Count == 1 && rs.Item3[0] == "INSTR ds(1) cost row Future:ES not used: symbol class Stock root ES", "M4 D1 stock ES: one Warning naming the unused row: " + string.Join("|", rs.Item3));
		var rf = run((e, w) => new InstrumentStreams(1).Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 50), g, cfg, e, w));
		Check(rf.Item1 == null && rf.Item2.Count == 0 && rf.Item3.Count == 0, "M4 D1 ES future: no cost Warning");
		var esFut = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		Check(esFut.costFloorTicks == 14 && esFut.floorSource == null && esFut.cost.Text().StartsWith("17.00", StringComparison.Ordinal) && esFut.ProfileHash() == "020deca78d6392b7",
			"M4 D1 ES future: cost(17.00...), floor(14), profileHash 020deca78d6392b7 unchanged");
		// floor src: no-cost (preset, no cost row), no-preset (cost row, no preset); config-invalid is a refusal (unknown-field:config) before any line
		var zn = InstrumentProfile.Build(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null, null);
		Check(zn.costFloorTicks == -1 && zn.floorSource == FloorSource.NoCost && zn.costMismatchKey == null, "M4 floor src=no-cost (ZN: preset, no cost row)");
		var c6b = new InstrumentConfig(); c6b.costs["Future:6B"] = 1000;
		var b6 = InstrumentProfile.Build(In("6BZ26", EAssetClass.Future, "6B", 1, 10000, 62500), c6b, null);
		Check(b6.cost.known && b6.costFloorTicks == -1 && b6.floorSource == FloorSource.NoPreset, "M4 floor src=no-preset (6B: cost row, no preset)");
		var b6n = InstrumentProfile.Build(In("6BZ26", EAssetClass.Future, "6B", 1, 10000, 62500), null, null);
		Check(b6n.floorSource == FloorSource.NoCost, "M4 floor src precedence: no-cost before no-preset");
		Check(FloorSource.ConfigInvalid == "config-invalid" && FloorSource.CostClassMismatch == "cost-class-mismatch" && FloorSource.NoCost == "no-cost" && FloorSource.NoPreset == "no-preset", "M4 floor src names");
		Check(!esStockP.Canon().Contains("cost-class-mismatch") && !esStockP.Canon().Contains("Future:ES") && esStockP.ProfileHash() == InstrumentProfile.Build(esStock, null, null).WithSessions(g, g, FB, null).ProfileHash(),
			"M4 floor src / mismatch key are printed only, never hashed");
		Check(InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).costFloorTicks == 14, "M3 ES future: floor 14 (preset matched)");
		var noRoot = In("", EAssetClass.Future, "", 25, 100, 50); noRoot.structPriceScale = 0.5;
		Check(reason(noRoot, null) == InstrumentReason.MismatchMcPriceScale, "M3 order: MC-internal mismatch beats unknown-field:root");
		Check(reason(In("@ES", EAssetClass.Future, "ES", -25, 100, 50, "EUR"), null) == InstrumentReason.MismatchTick, "M3 order: non-positive tick vs a preset is mismatch:tick, before tick / currency");
		var badCfg = new InstrumentConfig(); badCfg.costs["XX"] = -1;
		Check(reason(In("@ES", EAssetClass.Future, "ES", 25, 100, 5), badCfg) == InstrumentReason.MismatchBigPointValue, "M3 order: preset mismatch beats an invalid config");
		var znCost = new InstrumentConfig(); znCost.costs["Future:ZN"] = 500;
		Check(reason(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000, "EUR"), znCost) == InstrumentReason.MismatchTickValue, "M3 order: cost-floor tick value (ZN 1562.5 cents) beats currency");
		Check(reason(In("@ZN", EAssetClass.Future, "ZN", 1, 64, 1000), null) == null, "M3 ZN without a cost: no tick-value check");
		var ovr = new InstrumentConfig(); ovr.costs["XX"] = -1;
		ovr.overrides = new InstrumentPresetRow("ES", EAssetClass.Future, 0.25, 5, "USD", MonthMask.Of("HMUZ"), EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX", "t", null);
		Check(reason(In("@ES", EAssetClass.Future, "ES", 25, 100, 5), ovr) == InstrumentReason.MismatchBigPointValue, "M3 order: an invalid config's override row is not trusted; the built-in preset cross-checks");
		var rm = run((e, w) => new InstrumentStreams(1).Start(1, In("@ES", EAssetClass.Future, "ES", 25, 100, 5, "EUR"), g, cfg, e, w));
		Check(erroredWith(rm, InstrumentReason.MismatchBigPointValue) && rm.Item3.Count == 0, "M3 streams: non-USD with a preset mismatch throws the mismatch");
		// nit 3: a non-InstrumentException inside a step gets its own Error line, then propagates unchanged
		err.Clear(); Exception other = null;
		try { InstrumentStreams.Guard<object>(2, s => { throw new ArgumentException("boom"); }, err.Add); } catch (ArgumentException e) { other = e; }
		Check(other != null && other.Message == "boom" && err.Count == 1 && err[0] == "INSTR ds(2) unexpected ArgumentException: boom", "M3 Guard: unexpected exception -> Error line, rethrown: " + string.Join("|", err));
		err.Clear(); ex = null;
		try { InstrumentStreams.Guard<object>(2, s => { throw InstrumentError.Fail(s, RS, InstrumentReason.Tick, "d", "a"); }, err.Add); } catch (InstrumentException e) { ex = e; }
		Check(ex != null && err.Count == 1 && err[0] == ex.Message, "M4 Guard: a refusal -> its Error line once, rethrown");
		// #96 nit 1: MonthMask.Of only on static presets; every preset row validates and round-trips its letters
		bool presetsOk = true;
		foreach (var r in new[] { "ES", "MES", "NQ", "MNQ", "CL", "GC", "ZN", "6E" })
		{
			var row = InstrumentPresets.Find(r, EAssetClass.Future);
			presetsOk &= row != null && row.Validate() == null && MonthMask.Of(MonthMask.Text(row.contractMonths)) == row.contractMonths;
		}
		Check(presetsOk, "M3 nit 1: every preset row validates; month letters round-trip");
		bool threw = false; try { MonthMask.Of("HMUA"); } catch (ArgumentException) { threw = true; }
		Check(threw, "M3 nit 1: MonthMask.Of rejects a bad letter (static data only)");
	}

	// ---------------------------------------------------------------- PR-M4 D2: profileHash over sessions in exchange time (§6.1, §11.1 item 9)
	/// <summary>Sun–Thu start → next day end (the MC "Default" futures template shape).</summary>
	static S[] Week5(int startH, int endH)
	{
		var r = new List<S>();
		for (var d = DayOfWeek.Sunday; d <= DayOfWeek.Thursday; d++) r.Add(new S(d, TimeSpan.FromHours(startH), d + 1, TimeSpan.FromHours(endH)));
		return r.ToArray();
	}
	static InstrumentProfile.Inputs Zoned(InstrumentProfile.Inputs i, TimeZoneInfo bar, string setting, string exchange = "CME")
	{
		i.barZone = bar; i.barZoneSetting = setting; i.exchange = exchange; return i;
	}
	static void D2ExchangeTime()
	{
		var six = In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000);
		S[] ct = Week5(17, 16), et = Week5(18, 17), gmt = Week5(23, 22);   // January: CT = UTC-6, ET = UTC-5
		var exch = InstrumentProfile.Build(six, null, null).WithSessions(ct, ct, FB, null);
		var local = InstrumentProfile.Build(Zoned(six, ET, "Local"), null, null).WithSessions(et, et, FB, null);
		var utc = InstrumentProfile.Build(Zoned(six, TimeZoneInfo.Utc, "GMT"), null, null).WithSessions(gmt, gmt, FB, null);
		Console.WriteLine("D2 6E Default profileHash exchange " + exch.ProfileHash() + " local " + local.ProfileHash() + " gmt " + utc.ProfileHash());
		Check(exch.ProfileHash() == "adf60a3179735f02", "D2 6E Default on an Exchange-tz chart = MC adf60a3179735f02 (" + exch.ProfileHash() + ")");
		Check(local.ProfileHash() == exch.ProfileHash() && local.Canon() == exch.Canon(), "D2 6E Default in Local ET (18:00→17:00) hashes as the Exchange CT list (" + local.ProfileHash() + ")");
		Check(utc.ProfileHash() == exch.ProfileHash(), "D2 6E Default on a GMT bar zone (January week) hashes the same (GMT in MC: VERIFY)");
		Check(InstrumentText.Sessions(local.sessions) == InstrumentText.Sessions(et) && local.InstrLine(1, "Future", "Local", "TickCount").Contains(" dayOpen(18:00) sessionEnd(17:00) "),
			"D2 engines keep the bar-time sessions, dayOpen and sessionEnd (ET 18:00 / 17:00)");
		Check(InstrumentText.Sessions(local.exchangeSessions) == InstrumentText.Sessions(ct), "D2 exchangeSessions = the CT list: " + InstrumentText.Sessions(local.exchangeSessions));
		Check(local.Canon().Contains(";sessions=" + InstrumentText.Sessions(ct) + ";") && !local.Canon().Contains("18:00"), "D2 Canon hashes the exchange-time list");
		string ll = local.InstrLine(1, "Future", "Local→America/New_York", "TickCount");
		Check(ll.Contains(" sessions(" + InstrumentText.Sessions(et) + ") xsessions(Central Standard Time " + InstrumentText.Sessions(ct) + "; rth none) dayKey("),
			"D2 INSTR keeps sessions(…) in bar time and adds xsessions(<exchange zone> …; rth …): " + ll);
		// the bar-time hash of M3 (the PT Local evidence ac8bbad6890f1214) is what D2 removes
		var m3Local = InstrumentProfile.Build(six, null, null).WithSessions(et, et, FB, null);   // ET list declared as exchange time = M3's bar-time canon
		Check(m3Local.ProfileHash() == "ac8bbad6890f1214", "D2 evidence: the ET list hashed as is = PT Local ac8bbad6890f1214 (" + m3Local.ProfileHash() + ")");

		// ES Default (Sun–Thu 17:00→16:00 CT, EquityIndexLegacy rth): 6cff053d1b215324 on either zone
		var es = In("@ES", EAssetClass.Future, "ES", 25, 100, 50);
		string esX = InstrumentProfile.Build(es, null, null).WithSessions(ct, ct, FB, null).ProfileHash();
		string esL = InstrumentProfile.Build(Zoned(es, ET, "Local"), null, null).WithSessions(et, et, FB, null).ProfileHash();
		Console.WriteLine("D2 ES Default profileHash exchange " + esX + " local " + esL);
		Check(esX == "6cff053d1b215324" && esL == esX, "D2 ES Default 6cff053d1b215324 on Exchange and Local (" + esX + ", " + esL + ")");
		Check(InstrumentProfile.Build(Zoned(es, ET, "Local"), null, null).WithSessions(et, et, FB, null).ProfileHash() != "e1927ccf1ea6f41e", "D2 ES PT Local e1927ccf1ea6f41e goes away");

		// ChartDaySession rth is recomputed from the exchange-time list (stock on NASDAQ shown on a CT bar zone)
		var aapl = In("AAPL", EAssetClass.Stock, "AAPL", 1, 100, 1); aapl.exchange = "NASDAQ"; aapl.exchangeZone = ET; aapl.exchangeZoneId = "Eastern Standard Time";
		S[] dayEt = Daily(DayOfWeek.Monday, DayOfWeek.Friday, new TimeSpan(9, 30, 0), new TimeSpan(16, 0, 0), false);
		S[] dayCt = Daily(DayOfWeek.Monday, DayOfWeek.Friday, new TimeSpan(8, 30, 0), new TimeSpan(15, 0, 0), false);
		var aEt = InstrumentProfile.Build(Zoned(aapl, ET, "Exchange", "NASDAQ"), null, null).WithSessions(dayEt, dayEt, FB, null);
		var aCt = InstrumentProfile.Build(Zoned(aapl, CT, "Local", "NASDAQ"), null, null).WithSessions(dayCt, dayCt, FB, null);
		Check(aEt.ProfileHash() == "41886799d411dd0d", "D2 AAPL (NASDAQ, ET bar zone = exchange zone) keeps MC 41886799d411dd0d (" + aEt.ProfileHash() + ")");
		Check(aCt.ProfileHash() == aEt.ProfileHash() && aCt.Canon().Contains(";rth=09:30-16:00 src=ChartDaySession"), "D2 ChartDaySession rth hashed in exchange time: " + aCt.Canon());
		Check(aCt.rthSplit.Text() == "08:30-15:00 src=ChartDaySession" && aCt.InstrLine(1, "Stock", "Local", "Volume").Contains("; rth 09:30-16:00) dayKey("), "D2 engine rth stays in bar time; xsessions shows the hashed rth");

		// refusals (Error line, then throw), strict lookups
		var noMap = six; noMap.exchange = "IDEALPRO"; noMap.exchangeZone = null; noMap.exchangeZoneId = null;
		var e1 = Throws(s => InstrumentProfile.Build(noMap, null, s), "D2 tz:exchange (exchange outside the map)");
		Check(e1 != null && e1.Reason == InstrumentReason.TzExchange && e1.Message.Contains("exchange 'IDEALPRO' not in exchangeTimeZoneId") && e1.Message.EndsWith("add the exchange to exchangeTimeZoneId."),
			"D2 tz:exchange text: " + (e1 == null ? "none" : e1.Message));
		var badId = six; badId.exchangeZone = null; badId.exchangeZoneId = "Nowhere Standard Time";
		var e2 = Throws(s => InstrumentProfile.Build(badId, null, s), "D2 tz:exchange (id does not resolve)");
		Check(e2 != null && e2.Reason == InstrumentReason.TzExchange && e2.Message.Contains("zone id 'Nowhere Standard Time' does not resolve"), "D2 tz:exchange unresolved id: " + (e2 == null ? "none" : e2.Message));
		var noBar = six; noBar.barZone = null; noBar.barZoneSetting = "Exchange";
		var e3 = Throws(s => InstrumentProfile.Build(noBar, null, s), "D2 tz:bar");
		Check(e3 != null && e3.Reason == InstrumentReason.TzBar && e3.Message.EndsWith("set the chart time zone."), "D2 tz:bar text: " + (e3 == null ? "none" : e3.Message));
		var both = noMap; both.barZone = null;
		var e4 = Throws(s => InstrumentProfile.Build(both, null, s), "D2 both unresolved");
		Check(e4 != null && e4.Reason == InstrumentReason.TzExchange, "D2 exchange checked before the bar zone (an Exchange-tz chart's bar zone is the exchange lookup)");
		var sub = new InstrumentSubject("INSTR", "@6E", EAssetClass.Future, "6E", "test");
		var e5 = Throws(s => InstrumentSessions.ToExchange(ct, CT, null, FB, sub, s), "D2 ToExchange null zone");
		Check(e5 != null && e5.Reason == InstrumentReason.TzExchange, "D2 ToExchange refuses a null zone (no fallback)");
		Check(InstrumentSessions.ToExchange(ct, CT, CT, FB, sub, null) != ct && InstrumentText.Sessions(InstrumentSessions.ToExchange(ct, CT, CT, FB, sub, null)) == InstrumentText.Sessions(ct), "D2 same zone id = identity copy");
		// wrap across the week: Sat 23:30 ET → Sat 22:30 CT; Sun 00:30 ET → Sat 23:30 CT
		var w = InstrumentSessions.ToExchange(new[] { new S(DayOfWeek.Sunday, new TimeSpan(0, 30, 0), DayOfWeek.Sunday, new TimeSpan(2, 0, 0)) }, ET, CT, FB, sub, null);
		Check(w[0].StartDay == DayOfWeek.Saturday && w[0].StartTime == new TimeSpan(23, 30, 0) && w[0].EndDay == DayOfWeek.Sunday && w[0].EndTime == new TimeSpan(1, 0, 0), "D2 conversion wraps the week");
		D2DstWeeks(six, sub);
	}

	/// <summary>PR-M4 review nit 1: ToExchange on a summer week and on the two US DST boundary weeks (2026-03-08 spring forward,
	/// 2026-11-01 fall back; both zones switch at 02:00 local). The sessions are placed on the week of the first bar.</summary>
	static void D2DstWeeks(InstrumentProfile.Inputs six, InstrumentSubject sub)
	{
		Func<S, string> hm = x => x.StartDay + " " + InstrumentText.Hm(x.StartTime) + "-" + x.EndDay + " " + InstrumentText.Hm(x.EndTime);
		// summer (EDT/CDT, UTC-4/-5): the ET list still maps to the CT list and keeps the MC hash; a GMT bar zone needs the summer list
		DateTime summer = new DateTime(2026, 7, 6, 17, 0, 0);
		S[] ct = Week5(17, 16), et = Week5(18, 17), gmtSummer = Week5(22, 21), gmtWinter = Week5(23, 22);
		Check(InstrumentText.Sessions(InstrumentSessions.ToExchange(et, ET, CT, summer, sub, null)) == InstrumentText.Sessions(ct), "D2 summer week: ET 18:00→17:00 = CT 17:00→16:00");
		var sLocal = InstrumentProfile.Build(Zoned(six, ET, "Local"), null, null).WithSessions(et, et, summer, null);
		var sGmt = InstrumentProfile.Build(Zoned(six, TimeZoneInfo.Utc, "GMT"), null, null).WithSessions(gmtSummer, gmtSummer, summer, null);
		Check(sLocal.ProfileHash() == "adf60a3179735f02" && sGmt.ProfileHash() == "adf60a3179735f02", "D2 summer week: 6E Default in Local ET and GMT 22:00→21:00 keep adf60a3179735f02 (" + sLocal.ProfileHash() + ", " + sGmt.ProfileHash() + ")");
		string wInSummer = InstrumentText.Sessions(InstrumentSessions.ToExchange(gmtWinter, TimeZoneInfo.Utc, CT, summer, sub, null));
		Check(wInSummer == InstrumentText.Sessions(Week5(18, 17)), "D2 summer week: the January GMT list (23:00→22:00) is CT 18:00→17:00 in July (UTC offsets move): " + wInSummer);

		// spring forward, week of Sun 2026-03-08: 01:30 EST (valid) → 06:30 UTC, 03:00 EDT → 07:00 UTC; a gap time 02:00 / 02:30 moves +1h
		// to 03:00 / 03:30 EDT → 07:00 / 07:30 UTC
		DateTime spring = new DateTime(2026, 3, 9, 17, 0, 0);
		var sp = InstrumentSessions.ToExchange(new[] {
			new S(DayOfWeek.Sunday, new TimeSpan(1, 30, 0), DayOfWeek.Sunday, new TimeSpan(3, 0, 0)),
			new S(DayOfWeek.Sunday, new TimeSpan(2, 0, 0), DayOfWeek.Sunday, new TimeSpan(2, 30, 0)) }, ET, TimeZoneInfo.Utc, spring, sub, null);
		Check(hm(sp[0]) == "Sunday 06:30-Sunday 07:00", "D2 spring-forward boundary: Sun 01:30 EST-03:00 EDT = 06:30-07:00 UTC: " + hm(sp[0]));
		Check(hm(sp[1]) == "Sunday 07:00-Sunday 07:30", "D2 spring-forward gap: Sun 02:00-02:30 ET (invalid) moves +1h = 07:00-07:30 UTC: " + hm(sp[1]));
		// the same gap times into CT: 07:00 / 07:30 UTC is still CST (CT switches at 08:00 UTC) = 01:00 / 01:30
		var spCt = InstrumentSessions.ToExchange(new[] { new S(DayOfWeek.Sunday, new TimeSpan(2, 0, 0), DayOfWeek.Sunday, new TimeSpan(2, 30, 0)) }, ET, CT, spring, sub, null);
		Check(hm(spCt[0]) == "Sunday 01:00-Sunday 01:30", "D2 spring-forward gap ET→CT = 01:00-01:30 CST: " + hm(spCt[0]));
		// the regular 6E Default week across the switch still hashes as MC (Sun 18:00 is after the 02:00 switch)
		Check(InstrumentProfile.Build(Zoned(six, ET, "Local"), null, null).WithSessions(et, et, spring, null).ProfileHash() == "adf60a3179735f02", "D2 spring-forward week: 6E Default Local ET keeps adf60a3179735f02");

		// fall back, week of Sun 2026-11-01: 01:00-02:00 ET happens twice; .NET ConvertTime reads an ambiguous time as standard
		// time (EST, UTC-5): 00:30 EDT → 04:30 UTC, 01:30 (ambiguous) → 06:30 UTC (not 05:30), 02:00 EST → 07:00 UTC
		DateTime fall = new DateTime(2026, 11, 2, 17, 0, 0);
		var fb = InstrumentSessions.ToExchange(new[] {
			new S(DayOfWeek.Sunday, new TimeSpan(0, 30, 0), DayOfWeek.Sunday, new TimeSpan(1, 30, 0)),
			new S(DayOfWeek.Sunday, new TimeSpan(1, 30, 0), DayOfWeek.Sunday, new TimeSpan(2, 0, 0)) }, ET, TimeZoneInfo.Utc, fall, sub, null);
		Check(ET.IsAmbiguousTime(new DateTime(2026, 11, 1, 1, 30, 0)), "D2 fall-back: Sun 2026-11-01 01:30 ET is ambiguous");
		Check(hm(fb[0]) == "Sunday 04:30-Sunday 06:30", "D2 fall-back boundary: Sun 00:30 EDT-01:30 (ambiguous, read as EST) = 04:30-06:30 UTC: " + hm(fb[0]));
		Check(hm(fb[1]) == "Sunday 06:30-Sunday 07:00", "D2 fall-back ambiguous start: Sun 01:30 (EST)-02:00 EST = 06:30-07:00 UTC: " + hm(fb[1]));
		Check(InstrumentProfile.Build(Zoned(six, ET, "Local"), null, null).WithSessions(et, et, fall, null).ProfileHash() == "adf60a3179735f02", "D2 fall-back week: 6E Default Local ET keeps adf60a3179735f02");
	}

	static void M6aWindow()
	{
		var g = Globex9();
		var et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
		var ct = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
		var subj = new InstrumentSubject("G0", "@ES", EAssetClass.Future, "ES", "GLOBEX");
		var etSessions = InstrumentSessions.ToExchange(g, ct, et, new DateTime(2024, 1, 8, 18, 0, 0), subj, null);
		Check(InstrumentSessions.CoversGate0Rth(etSessions), "M6a CME Globex 9-entry, Central to Eastern, covers 09:30-16:00 ET");
		var full = new S[5];
		var gap = new S[5];
		for (int d = 1; d <= 5; d++)
		{
			full[d - 1] = new S((DayOfWeek)d, new TimeSpan(9, 30, 0), (DayOfWeek)d, new TimeSpan(16, 0, 0));
			gap[d - 1] = new S((DayOfWeek)d, new TimeSpan(9, 30, 0), (DayOfWeek)d, new TimeSpan(15, 0, 0));
		}
		Check(InstrumentSessions.CoversGate0Rth(full), "M6a Mon-Fri 09:30-16:00 ET is covered");
		Check(!InstrumentSessions.CoversGate0Rth(gap), "M6a Mon-Fri 09:30-15:00 ET is not covered");
		var e = Throws(s => InstrumentSessions.RequireGate0Rth(gap, subj, s), "M6a S3");
		Check(e != null && e.Reason == InstrumentReason.SessionsWindowNotCovered && e.Action == InstrumentReason.ActionGate0Window
			&& e.Message.Contains("use a session template that covers 09:30\u201316:00 ET"), "M6a S3 reason and action: " + (e == null ? "" : e.Message));
	}

	static void M6eWeekly()
	{
		var g = Globex9();
		var es = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		var cl = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null).WithSessions(g, g, FB, null);
		var bare = InstrumentProfile.Build(In("@CL", EAssetClass.Future, "CL", 1, 100, 1000), null, null);
		Check(es.rthSplit != null && cl.rthSplit == null && bare.sessions == null, "M6e ES has a split, CL 24h does not, sessions not filled yet");
		InstrumentSessions.RequireWeeklyRth(es, null);
		InstrumentSessions.RequireWeeklyRth(bare, null);
		InstrumentSessions.RequireWeeklyRth(null, null);
		var ex = Throws(s => InstrumentSessions.RequireWeeklyRth(cl, s), "M6e CL");
		Check(ex != null && ex.Engine == "WEEKLY" && ex.Reason == InstrumentReason.SessionsNoRthSplit && ex.Action == "use a day-session template for CL",
			"M6e CL S7: " + (ex == null ? "" : ex.Message));
		Check(es.ProfileHash() == "020deca78d6392b7", "M6e ES pin unchanged " + es.ProfileHash());
	}

	static void M6fCluster()
	{
		Check(InstrumentPresets.Find("ES", EAssetClass.Future).cluster == "EQIDX" && InstrumentPresets.Find("MES", EAssetClass.Future).cluster == "EQIDX"
			&& InstrumentPresets.Find("NQ", EAssetClass.Future).cluster == "EQIDX" && InstrumentPresets.Find("MNQ", EAssetClass.Future).cluster == "EQIDX", "M6f preset ES/MES/NQ/MNQ EQIDX");
		Check(InstrumentPresets.Find("CL", EAssetClass.Future).cluster == "ENERGY" && InstrumentPresets.Find("GC", EAssetClass.Future).cluster == "METALS"
			&& InstrumentPresets.Find("ZN", EAssetClass.Future).cluster == "RATES" && InstrumentPresets.Find("6E", EAssetClass.Future).cluster == "FX_EUR", "M6f preset CL/GC/ZN/6E clusters");
		var g = Globex9();
		var es = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		var six = InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(g, g, FB, null);
		Check(es.cluster == "EQIDX" && es.ProfileHash() == "020deca78d6392b7", "M6f ES cluster and pin " + es.ProfileHash());
		Check(six.cluster == "FX_EUR" && six.ProfileHash() == "d60dd6584c2159e9", "M6f 6E cluster and pin " + six.ProfileHash());
		S[] ct = Week5(17, 16);
		Check(InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(ct, ct, FB, null).ProfileHash() == "6cff053d1b215324", "M6f ES Default pin");
		Check(InstrumentProfile.Build(In("@6E", EAssetClass.Future, "6E", 5, 99999.999999999985, 125000), null, null).WithSessions(ct, ct, FB, null).ProfileHash() == "adf60a3179735f02", "M6f 6E Default pin");
		var esRow = InstrumentPresets.Find("ES", EAssetClass.Future);
		var dates = new List<DateTime>();
		for (int y = 2000; y <= 2035; y++) for (int m = 3; m <= 12; m += 3) dates.Add(InstrumentRules.Roll(esRow, y, m, EventRules.IsNyseBusinessDay, RS, null).Value);
		Check(_RollDetector.DatesDigest(dates) == "fdd63bd8f179a8d4", "M6f roll digest pin");
	}

	static void M7Floor()
	{
		Check(!new InstrumentConfig().applyCostFloor, "M7 floor off by default");
		var subj = new InstrumentSubject("COST", "@ES", EAssetClass.Future, "ES", "test");
		Check(CostModel.FloorTicks(1700, 0.25, 50, subj, null) == 14 && CostModel.FloorTicks(250, 0.25, 5, subj, null) == 20, "M7 ES 14 and MES 20 stay");
		Check(CostModel.WidenedStop(100, 99, 3.5) == 96.5, "M7 a 4-tick stop widens to 14 ticks");
		Check(CostModel.WidenedStop(100, 90, 3.5) == 90, "M7 a stop already past the floor stays");
		Check(CostModel.WidenedStop(100, 101, 3.5) == 103.5, "M7 a short stop widens away from entry");
		var g = Globex9();
		var es = InstrumentProfile.Build(In("@ES", EAssetClass.Future, "ES", 25, 100, 50), null, null).WithSessions(g, g, FB, null);
		var mes = InstrumentProfile.Build(In("@MES", EAssetClass.Future, "MES", 25, 100, 5), null, null).WithSessions(g, g, FB, null);
		Check(es.costFloorTicks == 14 && mes.costFloorTicks == 20 && es.ProfileHash() == "020deca78d6392b7", "M7 profile floors and ES pin " + es.ProfileHash());
	}

	static int Main()
	{
		foreach (var t in new Action[] { T2Roots, T3Checks, T4Expiry, T5Digest, T6Windows, T7TradingDay, T8Derived, FxCrypto, T9Costs, T11Texts, T13Hash, T16SessionEnds, T12Lines, M3Wiring, D2ExchangeTime, M6aWindow, M6eWeekly, M6fCluster, M7Floor })
		{
			try { t(); } catch (Exception ex) { Check(false, t.Method.Name + " threw " + ex.GetType().Name + ": " + ex.Message); }
		}
		Check(Dumps().Count == 13, "13 PR-M0 fixtures");
		Console.WriteLine("InstrumentTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}
}
