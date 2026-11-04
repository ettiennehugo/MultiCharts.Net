// ============================================================================================
// M-INSTR PR-M2: instrument profile core (specs/m-instr-spec.md rev 2.3, §4.3, §4.5, §5, §6.3, §8, §9). PR-M3 wires it:
// _CustomSignal.buildProfiles() (StartCalc) and the first-CalcBar session re-check go through InstrumentStreams below. LOG ONLY
// apart from the cross-checks: mismatch:* and sessions:changed throw (Error line first); every other refusal is a Warning line
// until PR-M4 applies the profile to engines. No engine reads a profile yet.
//
// <instr-core> is MC-free (System only + _MtfMath.Session / SymbolRoot rules): tests/InstrumentTests/gen.sh extracts it.
// <instr-mc-fields> uses one typed MC enum (MTPA_SymbolFields); tests compile it against a mirror enum. The host adapter
// (InstrumentMcReader, end of file) reads typed Bars.Info / Bars.Info.ASymbolInfo2 members and compiles only in PowerLanguage
// Editor (and the box stub type check). MC enums are never handled as strings (R1): host code converts typed values to the core's
// own enums / plain numbers; log labels for MC enums are formatted by the host and only ever printed.
//
// Import order (PowerLanguage Editor): _MtfMath.Function.CS, _NyseHolidays / _EventCalendar.Function.CS (EventRules; all already
// present), then _InstrumentProfile.Function.CS, then
// _CustomSignal.Strategy.CS (its nested EAssetClass moved here as a top-level type), then Compile.
// ============================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ATCenterProxy.interop;

namespace PowerLanguage.Function
{
	// <instr-core>
	// <instr-asset-class>
	/// <summary>M-INSTR §4.2 / §5.1 asset class of a stream. Host maps the typed ESymbolCategory with _CustomSignal.ClassOf.</summary>
	public enum EAssetClass { Future, Stock, SpotFx, Crypto, Index, Unsupported }
	// </instr-asset-class>

	/// <summary>§5.1 holiday / early-close calendar (no session times). NyseStandIn: the NYSE holiday calendar as an explicit stand-in for a CME product calendar (CL/GC/ZN/6E until PR-M8;
	/// Ettienne 2026-11-04). Observed / None carry no business-day calendar: expiry / roll rules refuse with 'calendar'.</summary>
	public enum ECalendar { CmeEquityIndex, Nyse, NyseStandIn, Observed, None }

	/// <summary>§8.2 expiry rules (contract month m of year y).</summary>
	public enum EExpiryRule { ThirdFridaySoq, CL25thMinus3Bd, ThirdLastBd, LastBdMinus7Bd, ThirdWedMinus2Bd }

	/// <summary>§8.2 roll rules; the "(n)" parameter of BusinessDaysBeforeExpiry / BeforeFirstNotice is InstrumentPresetRow.rollDays.</summary>
	public enum ERollRule { EquityIndexCustomary, BusinessDaysBeforeExpiry, BeforeFirstNotice, None }

	/// <summary>§4.5 / §5.1 where the RTH segment comes from (N1).</summary>
	public enum ERthSource { ChartDaySession, EquityIndexLegacy, None }

	/// <summary>§5.1 sizing unit by class.</summary>
	public enum ESizingUnit { Contracts, Shares, Units }

	/// <summary>§6.3 reason codes (the text after "): ").</summary>
	public static class InstrumentReason
	{
		public const string Class = "class", Tick = "tick", VolumeBasis = "volume-basis", Currency = "currency", Calendar = "calendar";
		/// <summary>PR-M3 (#96 review nit): a preset / override row with no contract month listed in the next 24 months.</summary>
		public const string Months = "months";
		public const string SessionsNone = "sessions:none", SessionsChanged = "sessions:changed", SessionsBarOutside = "sessions:bar-outside";
		public const string SessionsNoRthSplit = "sessions:no-rth-split", SessionsWindowNotCovered = "sessions:window-not-covered";
		public const string MismatchTick = "mismatch:tick", MismatchBigPointValue = "mismatch:bigPointValue", MismatchTickValue = "mismatch:tickValue";
		public const string MismatchMcPriceScale = "mismatch:mcPriceScale", MismatchMcBigPointValue = "mismatch:mcBigPointValue";
		public static string UnknownField(string f) { return "unknown-field:" + f; }
		public static string Mismatch(string f) { return "mismatch:" + f; }
		public const string ActionSessions = "check the chart's session template";
		public const string ActionQuoteManager = "Check the symbol's settings in QuoteManager";
	}

	/// <summary>§6.3 refusal: "M-INSTR: &lt;ENGINE&gt; cannot run on &lt;name&gt; (class &lt;c&gt;, root &lt;r&gt;, sessions &lt;sessionName&gt;):
	/// &lt;reason&gt;[ (detail)]. &lt;action&gt;." Always thrown through InstrumentError.Fail, which writes the Error line first.</summary>
	public sealed class InstrumentException : InvalidOperationException
	{
		public readonly string Engine, Name, Root, SessionName, Reason, Detail, Action;
		public readonly EAssetClass AssetClass;
		public InstrumentException(string engine, string name, EAssetClass cls, string root, string sessionName, string reason, string detail, string action)
			: base(Format(engine, name, cls, root, sessionName, reason, detail, action))
		{ Engine = engine; Name = name; AssetClass = cls; Root = root; SessionName = sessionName; Reason = reason; Detail = detail; Action = action; }

		public static string Format(string engine, string name, EAssetClass cls, string root, string sessionName, string reason, string detail, string action)
		{
			string a = string.IsNullOrEmpty(action) ? "" : " " + action + (action.EndsWith(".", StringComparison.Ordinal) ? "" : ".");
			return "M-INSTR: " + (engine ?? "INSTR") + " cannot run on " + (name ?? "") + " (class " + InstrumentText.ClassName(cls) + ", root " + (string.IsNullOrEmpty(root) ? "?" : root)
				+ ", sessions " + (string.IsNullOrEmpty(sessionName) ? "?" : sessionName) + "): " + reason + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")") + "." + a;
		}
	}

	/// <summary>Who is being refused: the fields of the §6.3 prefix.</summary>
	public struct InstrumentSubject
	{
		public string Engine, Name, Root, SessionName;
		public EAssetClass AssetClass;
		public InstrumentSubject(string engine, string name, EAssetClass cls, string root, string sessionName) { Engine = engine; Name = name; AssetClass = cls; Root = root; SessionName = sessionName; }
	}

	/// <summary>Hard rule: log an Error line before any throw. The sink gets the exception text (the host writes it at Error, one
	/// File.AppendAllText per line); a null sink only skips the write.</summary>
	public static class InstrumentError
	{
		public static InstrumentException Fail(Action<string> errorSink, InstrumentSubject s, string reason, string detail, string action)
		{
			var e = new InstrumentException(s.Engine, s.Name, s.AssetClass, s.Root, s.SessionName, reason, detail, action);
			if (errorSink != null) errorSink(e.Message);
			throw e;
		}
	}

	/// <summary>Culture-invariant formatting shared by the INSTR line and the exception text (log only; nothing parses these).</summary>
	public static class InstrumentText
	{
		static readonly CultureInfo s_inv = CultureInfo.InvariantCulture;
		/// <summary>Diagnostics only (mismatch / refusal details): the raw detected value, round-trip. "R" output for a 17-digit
		/// value differs by runtime (.NET 8 5.000000000000001E-05 vs .NET Framework 5.0000000000000009E-05), so it never feeds a
		/// hash or the INSTR facts.</summary>
		public static string Num(double v) { return v.ToString("R", s_inv); }
		/// <summary>Profile facts (Canon / profileHash and the INSTR line): "G15" is the same on .NET Framework (MC) and .NET 8 (box
		/// tools, tests). Preset values are short literals, so G15 prints them exactly (0.00005 -> 5E-05, 0.015625, 12.5).</summary>
		public static string Fact(double v) { return v.ToString("G15", s_inv); }
		public static string Int(long v) { return v.ToString(s_inv); }
		public static string Hm(TimeSpan t) { return t.Days >= 1 && t.Hours == 0 && t.Minutes == 0 ? "24:00" : t.ToString(@"hh\:mm", s_inv); }
		// Own enums only (EAssetClass etc. are ours, not MultiCharts'): explicit switches so the text is fixed by code, not by names.
		public static string ClassName(EAssetClass c)
		{
			switch (c) { case EAssetClass.Future: return "Future"; case EAssetClass.Stock: return "Stock"; case EAssetClass.SpotFx: return "SpotFx"; case EAssetClass.Crypto: return "Crypto"; case EAssetClass.Index: return "Index"; default: return "Unsupported"; }
		}
		public static string CalendarName(ECalendar c)
		{
			switch (c) { case ECalendar.CmeEquityIndex: return "CmeEquityIndex"; case ECalendar.Nyse: return "Nyse"; case ECalendar.NyseStandIn: return "NyseStandIn"; case ECalendar.Observed: return "Observed"; default: return "None"; }
		}
		public static string ExpiryName(EExpiryRule r)
		{
			switch (r) { case EExpiryRule.ThirdFridaySoq: return "ThirdFridaySoq"; case EExpiryRule.CL25thMinus3Bd: return "CL25thMinus3Bd"; case EExpiryRule.ThirdLastBd: return "ThirdLastBd"; case EExpiryRule.LastBdMinus7Bd: return "LastBdMinus7Bd"; default: return "ThirdWedMinus2Bd"; }
		}
		public static string RollName(ERollRule r, int n)
		{
			switch (r)
			{
				case ERollRule.EquityIndexCustomary: return "EquityIndexCustomary";
				case ERollRule.BusinessDaysBeforeExpiry: return "BusinessDaysBeforeExpiry(" + Int(n) + ")";
				case ERollRule.BeforeFirstNotice: return "BeforeFirstNotice(" + Int(n) + ")";
				default: return "None";
			}
		}
		public static string RthSourceName(ERthSource s)
		{
			switch (s) { case ERthSource.ChartDaySession: return "ChartDaySession"; case ERthSource.EquityIndexLegacy: return "EquityIndexLegacy"; default: return "None"; }
		}
		public static string SizingName(ESizingUnit u) { switch (u) { case ESizingUnit.Contracts: return "Contracts"; case ESizingUnit.Shares: return "Shares"; default: return "Units"; } }
		public static string Day3(DayOfWeek d)
		{
			switch (d) { case DayOfWeek.Sunday: return "Sun"; case DayOfWeek.Monday: return "Mon"; case DayOfWeek.Tuesday: return "Tue"; case DayOfWeek.Wednesday: return "Wed"; case DayOfWeek.Thursday: return "Thu"; case DayOfWeek.Friday: return "Fri"; default: return "Sat"; }
		}
		public static string Sessions(_MtfMath.Session[] s)
		{
			if (s == null) return "n=0";
			var sb = new StringBuilder("n=" + Int(s.Length));
			for (int i = 0; i < s.Length; i++) sb.Append(i == 0 ? " " : "; ").Append(Day3(s[i].StartDay)).Append(' ').Append(Hm(s[i].StartTime)).Append('→').Append(Day3(s[i].EndDay)).Append(' ').Append(Hm(s[i].EndTime));
			return sb.ToString();
		}
	}

	/// <summary>Contract-month bit mask: bit (month - 1). Letters FGHJKMNQUVXZ.</summary>
	public static class MonthMask
	{
		public const string Codes = "FGHJKMNQUVXZ";
		public const int All = 0xFFF;
		/// <summary>Letters → mask. Called ONLY on the static preset literals of InstrumentPresets.s_rows (type initialiser: no logger in
		/// scope, so no Error line; a bad literal fails InstrumentTests T8 at type init, never a chart). Never call it on user or config
		/// text: InstrumentConfig.overrides carries a mask, which InstrumentPresetRow.Validate range-checks.</summary>
		public static int Of(string letters)
		{
			int m = 0;
			foreach (char c in letters ?? "") { int i = Codes.IndexOf(c); if (i < 0) throw new ArgumentException("month code " + c); m |= 1 << i; }
			return m;
		}
		public static bool Has(int mask, int month) { return month >= 1 && month <= 12 && (mask & (1 << (month - 1))) != 0; }
		public static string Text(int mask)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < 12; i++) if ((mask & (1 << i)) != 0) sb.Append(Codes[i]);
			return sb.ToString();
		}
	}

	/// <summary>§5.2 / §8.1 one preset row: exchange facts only, no session times. Immutable once built.</summary>
	public sealed class InstrumentPresetRow
	{
		public readonly string root;
		public readonly EAssetClass assetClass;
		public readonly double tick, bigPointValue;
		public readonly string currency;
		public readonly int contractMonths;
		public readonly EExpiryRule expiry;
		public readonly ERollRule roll;
		public readonly int rollDays;
		public readonly ECalendar calendar;
		public readonly string cluster, source, verify;   // verify: null = sourced; else the open VERIFY items of the row
		public InstrumentPresetRow(string root, EAssetClass cls, double tick, double bigPointValue, string currency, int contractMonths, EExpiryRule expiry,
			ERollRule roll, int rollDays, ECalendar calendar, string cluster, string source, string verify)
		{
			this.root = root; assetClass = cls; this.tick = tick; this.bigPointValue = bigPointValue; this.currency = currency; this.contractMonths = contractMonths;
			this.expiry = expiry; this.roll = roll; this.rollDays = rollDays; this.calendar = calendar; this.cluster = cluster; this.source = source; this.verify = verify;
		}
		public string Validate()
		{
			var e = new List<string>();
			if (string.IsNullOrWhiteSpace(root) || root != root.Trim().ToUpperInvariant()) e.Add("root=" + (root ?? "null"));
			if (!(tick > 0) || double.IsInfinity(tick)) e.Add("tick=" + InstrumentText.Num(tick));
			if (!(bigPointValue > 0) || double.IsInfinity(bigPointValue)) e.Add("bigPointValue=" + InstrumentText.Num(bigPointValue));
			if (currency != "USD") e.Add("currency=" + (currency ?? "null"));
			if ((contractMonths & ~MonthMask.All) != 0 || (assetClass == EAssetClass.Future && contractMonths == 0)) e.Add("contractMonths=" + InstrumentText.Int(contractMonths));
			if (rollDays < 0 || rollDays > 30) e.Add("rollDays=" + InstrumentText.Int(rollDays));
			return e.Count == 0 ? null : "InstrumentPresetRow " + (root ?? "?") + ": " + string.Join(", ", e);
		}
		public string Canon()
		{
			return root + "|" + InstrumentText.ClassName(assetClass) + "|" + InstrumentText.Fact(tick) + "|" + InstrumentText.Fact(bigPointValue) + "|" + currency + "|" + MonthMask.Text(contractMonths)
				+ "|" + InstrumentText.ExpiryName(expiry) + "|" + InstrumentText.RollName(roll, rollDays) + "|" + InstrumentText.CalendarName(calendar) + "|" + cluster;
		}
	}

	/// <summary>§8.1 futures presets (sources read 2026-11-02). Match on root AND class (§4.3 step 7). Adding a root = one row + sources.</summary>
	public static class InstrumentPresets
	{
		const string Eq = "HMUZ";
		static readonly InstrumentPresetRow[] s_rows = {
			new InstrumentPresetRow("ES", EAssetClass.Future, 0.25, 50, "USD", MonthMask.Of(Eq), EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX",
				"CME ES specs; _EventCalendar RuleExpiryDate/RollDate", null),
			new InstrumentPresetRow("MES", EAssetClass.Future, 0.25, 5, "USD", MonthMask.Of(Eq), EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX",
				"CME Micro E-mini FAQ", null),
			new InstrumentPresetRow("NQ", EAssetClass.Future, 0.25, 20, "USD", MonthMask.Of(Eq), EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX",
				"CME Micro FAQ", "VERIFY roll vs vendor splice"),
			new InstrumentPresetRow("MNQ", EAssetClass.Future, 0.25, 2, "USD", MonthMask.Of(Eq), EExpiryRule.ThirdFridaySoq, ERollRule.EquityIndexCustomary, 0, ECalendar.CmeEquityIndex, "EQIDX",
				"CME Micro FAQ", "VERIFY roll as NQ"),
			new InstrumentPresetRow("CL", EAssetClass.Future, 0.01, 1000, "USD", MonthMask.All, EExpiryRule.CL25thMinus3Bd, ERollRule.BusinessDaysBeforeExpiry, 2, ECalendar.NyseStandIn, "ENERGY",
				"CME CL specs; CME clearing notice chadv25-391 (CLF26 LTD 2025-12-19)", "VERIFY roll vs vendor splice; " + InstrumentCalendars.NyseStandInNote),
			new InstrumentPresetRow("GC", EAssetClass.Future, 0.10, 100, "USD", MonthMask.Of("GJMQVZ"), EExpiryRule.ThirdLastBd, ERollRule.BeforeFirstNotice, 1, ECalendar.NyseStandIn, "METALS",
				"CME GC specs; CME GC fact card", "VERIFY roll (first notice = last business day of the prior month); " + InstrumentCalendars.NyseStandInNote),
			new InstrumentPresetRow("ZN", EAssetClass.Future, 0.015625, 1000, "USD", MonthMask.Of(Eq), EExpiryRule.LastBdMinus7Bd, ERollRule.BeforeFirstNotice, 1, ECalendar.NyseStandIn, "RATES",
				"CME ZN specs; Barchart (ZNZ25 LTD 2025-12-19, FND 2025-11-28)", "VERIFY bigPointValue (not dumped); " + InstrumentCalendars.NyseStandInNote),
			new InstrumentPresetRow("6E", EAssetClass.Future, 0.00005, 125000, "USD", MonthMask.Of(Eq), EExpiryRule.ThirdWedMinus2Bd, ERollRule.BusinessDaysBeforeExpiry, 5, ECalendar.NyseStandIn, "FX_EUR",
				"CME 6E specs; CME Rulebook ch. 261 / rule 26101.G; bpv CONFIRMED 01M6HTKV3SV1Q5QDXBFTCH8D3K.txt; roll 5 business days (~1 week) before expiry (Ettienne 2026-11-04)",
				InstrumentCalendars.NyseStandInNote),
		};

		public static IList<InstrumentPresetRow> Rows { get { return Array.AsReadOnly(s_rows); } }

		/// <summary>Exact root AND class match; null when none (no longest-prefix search: MES != ES, 6E != 6B).</summary>
		public static InstrumentPresetRow Find(string root, EAssetClass cls)
		{
			if (string.IsNullOrEmpty(root)) return null;
			foreach (var r in s_rows) if (r.assetClass == cls && string.Equals(r.root, root, StringComparison.Ordinal)) return r;
			return null;
		}

		/// <summary>§5.1 / §8.3 class constants: sizing unit, annualisation (FX 260 VERIFY), default calendar for roots without a preset.</summary>
		public static int SessionsPerYear(EAssetClass c) { return c == EAssetClass.Crypto ? 365 : c == EAssetClass.SpotFx ? 260 : 252; }
		public static ESizingUnit SizingUnit(EAssetClass c) { return c == EAssetClass.Future ? ESizingUnit.Contracts : c == EAssetClass.Stock ? ESizingUnit.Shares : ESizingUnit.Units; }
		public static ECalendar DefaultCalendar(EAssetClass c) { return c == EAssetClass.Stock ? ECalendar.Nyse : c == EAssetClass.Unsupported ? ECalendar.None : ECalendar.Observed; }
		/// <summary>Class-level VERIFY notes kept with the FX / crypto defaults (§4.5, §8.3).</summary>
		public static string ClassVerify(EAssetClass c)
		{
			if (c == EAssetClass.SpotFx) return "VERIFY sessionsPerYear 260; hasOvernight for 24h templates fixed in PR-M4";
			if (c == EAssetClass.Crypto) return "VERIFY hasOvernight for 00:00-24:00 templates fixed in PR-M4";
			return null;
		}
	}

	/// <summary>§4.3 root resolution. MC's stored root (ASymbolInfo2.SymbolRoot) is primary; the class-aware parser is the fallback.</summary>
	public static class InstrumentRoot
	{
		public struct Result
		{
			public string Root, Source, Warning;   // Source "mc" | "parsed"; Warning = the INSTR root-crosscheck line or null
		}

		/// <summary>Steps 1–2, class aware: trim, leading '@', '#…' suffix, upper invariant; '/' removed for SpotFx only; stocks keep
		/// their share class (BRK.B); other classes: leading digits + letter run, final month letter dropped when the run has >= 2 letters
		/// and a digit follows (same rule as _MtfMath.SymbolRoot).</summary>
		public static string Parse(string name, EAssetClass cls)
		{
			if (string.IsNullOrEmpty(name)) return "";
			string s = name.Trim();
			if (s.Length > 0 && s[0] == '@') s = s.Substring(1);
			int hash = s.IndexOf('#');
			if (hash >= 0) s = s.Substring(0, hash);
			s = s.ToUpperInvariant();
			if (cls == EAssetClass.SpotFx) s = s.Replace("/", "");
			if (cls == EAssetClass.Stock)
			{
				int k = 0;
				while (k < s.Length && (char.IsLetterOrDigit(s[k]) || s[k] == '.' || s[k] == '-')) k++;
				return s.Substring(0, k).TrimEnd('.', '-');
			}
			int digits = 0;
			while (digits < s.Length && char.IsDigit(s[digits])) digits++;
			int i = digits;
			while (i < s.Length && char.IsLetter(s[i])) i++;
			if (i - digits >= 2 && i < s.Length && char.IsDigit(s[i]) && MonthMask.Codes.IndexOf(s[i - 1]) >= 0) i--;
			return s.Substring(0, i);
		}

		/// <summary>MC root primary unless the host reports eMTPA_SF_Root unsupported (typed test in &lt;instr-mc-fields&gt;) or it is blank.
		/// Both roots empty → Error + throw unknown-field:root. A differing parse gives the Warning text; MC wins.</summary>
		public static Result Resolve(string name, EAssetClass cls, string mcRoot, bool mcRootUnsupported, string engine, string sessionName, Action<string> errorSink)
		{
			var r = Pick(name, cls, mcRoot, mcRootUnsupported);
			if (string.IsNullOrEmpty(r.Root))
				InstrumentError.Fail(errorSink, new InstrumentSubject(engine, name, cls, "", sessionName), InstrumentReason.UnknownField("root"), "SymbolRoot empty and the name gives no root", InstrumentReason.ActionQuoteManager);
			return r;
		}

		/// <summary>The root Resolve would return, without the refusal (Root "" when there is none). Build uses it to run the enforced
		/// cross-checks before any log-only refusal (PR-M3 review).</summary>
		public static Result Pick(string name, EAssetClass cls, string mcRoot, bool mcRootUnsupported)
		{
			string parsed = Parse(name, cls);
			bool useMc = !mcRootUnsupported && !string.IsNullOrWhiteSpace(mcRoot);
			var r = new Result();
			if (useMc)
			{
				r.Root = mcRoot.Trim().ToUpperInvariant(); r.Source = "mc";
				if (cls == EAssetClass.SpotFx) r.Root = r.Root.Replace("/", "");   // the Spot-FX "/" rule applies to MC's root too (EURUSD)
				if (parsed.Length > 0 && !_MtfMath.SameRoot(r.Root, parsed))
					r.Warning = "INSTR root-crosscheck mc(" + r.Root + ") parsed(" + parsed + ") name(" + (name ?? "") + ")";
			}
			else { r.Root = parsed ?? ""; r.Source = "parsed"; }
			return r;
		}

		/// <summary>§4.4 B1: SymbolExpiry is a placeholder unless strictly inside (1900-01-01, 2038-01-01).</summary>
		public static readonly DateTime ExpiryPlaceholderLow = new DateTime(1900, 1, 1), ExpiryPlaceholderHigh = new DateTime(2038, 1, 1);
		public static bool UsableExpiry(DateTime symbolExpiry) { return symbolExpiry > ExpiryPlaceholderLow && symbolExpiry < ExpiryPlaceholderHigh; }
	}

	/// <summary>§4.3 cross-checks. Relative tolerance 1e-9 (covers MC's binary priceScale, e.g. @6E 99999.999999999985).</summary>
	public static class InstrumentChecks
	{
		public const double RelTol = 1e-9;
		public static bool Close(double a, double b) { return Math.Abs(a - b) <= RelTol * Math.Max(Math.Abs(a), Math.Abs(b)); }

		/// <summary>MC-internal: ASymbolInfo2.PriceScale × Info.PriceScale = 1 ±1e-9 relative, BigPointValue equal.</summary>
		public static void McInternal(double structPriceScale, double infoPriceScale, double structBpv, double infoBpv, InstrumentSubject s, Action<string> errorSink)
		{
			double x = structPriceScale * infoPriceScale;
			if (!(Math.Abs(x - 1.0) <= RelTol))
				InstrumentError.Fail(errorSink, s, InstrumentReason.MismatchMcPriceScale, "ASymbolInfo2.PriceScale " + InstrumentText.Num(structPriceScale) + " × Info.PriceScale " + InstrumentText.Num(infoPriceScale)
					+ " = " + InstrumentText.Num(x) + ", expected 1 ±1e-9", InstrumentReason.ActionQuoteManager);
			if (!(structBpv == infoBpv))
				InstrumentError.Fail(errorSink, s, InstrumentReason.MismatchMcBigPointValue, "ASymbolInfo2.BigPointValue " + InstrumentText.Num(structBpv) + " vs Info.BigPointValue " + InstrumentText.Num(infoBpv),
					InstrumentReason.ActionQuoteManager);
		}

		/// <summary>tick = MinMove / Info.PriceScale; must be > 0 and finite.</summary>
		public static double Tick(double minMove, double infoPriceScale, InstrumentSubject s, Action<string> errorSink)
		{
			double t = infoPriceScale > 0 ? minMove / infoPriceScale : 0;
			if (!(t > 0) || double.IsInfinity(t) || double.IsNaN(t))
				InstrumentError.Fail(errorSink, s, InstrumentReason.Tick, "MinMove " + InstrumentText.Num(minMove) + " / PriceScale " + InstrumentText.Num(infoPriceScale), InstrumentReason.ActionQuoteManager);
			return t;
		}

		/// <summary>§4.3 step 8: detected tick and BPV equal the preset within 1e-9 relative.</summary>
		public static void Preset(InstrumentPresetRow p, double tick, double bpv, InstrumentSubject s, Action<string> errorSink)
		{
			if (p == null) return;
			if (!Close(tick, p.tick))
				InstrumentError.Fail(errorSink, s, InstrumentReason.MismatchTick, "detected " + InstrumentText.Num(tick) + " vs preset " + InstrumentText.Num(p.tick), "Fix the symbol in QuoteManager or set InstrumentConfig.overrides");
			if (!Close(bpv, p.bigPointValue))
				InstrumentError.Fail(errorSink, s, InstrumentReason.MismatchBigPointValue, "detected " + InstrumentText.Num(bpv) + " vs preset " + InstrumentText.Num(p.bigPointValue), "Fix the symbol in QuoteManager or set InstrumentConfig.overrides");
		}
	}

	/// <summary>§5.1 CostModel: round trip in integer cents; Known = false when InstrumentConfig.costs has no entry for the root.</summary>
	public struct CostModel
	{
		public readonly long roundTripCents;
		public readonly string source;
		public readonly bool known;
		public CostModel(long roundTripCents, string source) { this.roundTripCents = roundTripCents; this.source = source; known = true; }
		public static CostModel Unknown { get { return new CostModel(); } }
		public string Text() { return known ? InstrumentText.Int(roundTripCents / 100) + "." + (roundTripCents % 100).ToString("00", CultureInfo.InvariantCulture) + " src=" + (source ?? "?") : "Unknown"; }

		/// <summary>§9.4: tickValueCents = tick × bpv × 100 must be within 1e-6 of an integer (else Error + throw mismatch:tickValue);
		/// floor = ceil(roundTripCents × 10 / tickValueCents) in long arithmetic, (a + b − 1) / b. ES 1700 → 14, MES 250 → 20.</summary>
		public static int FloorTicks(long roundTripCents, double tick, double bigPointValue, InstrumentSubject s, Action<string> errorSink)
		{
			double tvc = tick * bigPointValue * 100.0;
			double r = Math.Round(tvc);
			if (!(Math.Abs(tvc - r) <= 1e-6) || r < 1)
				InstrumentError.Fail(errorSink, s, InstrumentReason.MismatchTickValue, "tickValueCents " + InstrumentText.Num(tvc) + " is not an integer", "Set an integer-cent cost floor for " + (s.Root ?? "?"));
			if (roundTripCents < 0)
				InstrumentError.Fail(errorSink, s, InstrumentReason.UnknownField("cost"), "roundTripCents " + InstrumentText.Int(roundTripCents), "add a preset row / cost for " + (s.Root ?? "?"));
			long b = (long)r, a = checked(roundTripCents * 10);
			return checked((int)((a + b - 1) / b));
		}
	}

	/// <summary>§5.3 InstrumentConfig (protected on _CustomSignal in PR-M3; no inputs).</summary>
	public sealed class InstrumentConfig
	{
		public bool enabled = true;              // log-only until PR-M4
		public bool applyCostFloor = false;      // PR-M7, opt-in (§9.3)
		public InstrumentPresetRow overrides = null;
		public Dictionary<string, long> costs = new Dictionary<string, long>(StringComparer.Ordinal) { { "ES", 1700 }, { "MES", 250 } };
		public string costSource = "Ettienne 2026-10-31";
		public Dictionary<string, string> dataNotes = new Dictionary<string, string>(StringComparer.Ordinal);

		public string Validate()
		{
			var e = new List<string>();
			if (costs == null) e.Add("costs null");
			else foreach (var kv in costs) { if (string.IsNullOrWhiteSpace(kv.Key) || kv.Key != kv.Key.Trim().ToUpperInvariant()) e.Add("costs key " + (kv.Key ?? "null")); if (kv.Value <= 0) e.Add("costs " + kv.Key + "=" + InstrumentText.Int(kv.Value)); }
			if (dataNotes == null) e.Add("dataNotes null");
			if (overrides != null) { string o = overrides.Validate(); if (o != null) e.Add("overrides: " + o); }
			return e.Count == 0 ? null : "InstrumentConfig: " + string.Join(", ", e);
		}

		/// <summary>Canonical config string. profileHash is NOT part of it (rev-3 Q7: profile facts stay outside every configHash).</summary>
		public string Canon()
		{
			var sb = new StringBuilder();
			sb.Append("instr:enabled=").Append(enabled ? 1 : 0).Append(";costFloor=").Append(applyCostFloor ? 1 : 0);
			sb.Append(";overrides=").Append(overrides == null ? "-" : overrides.Canon());
			sb.Append(";costs=[");
			if (costs != null) { var k = new List<string>(costs.Keys); k.Sort(StringComparer.Ordinal); foreach (var r in k) sb.Append(r).Append(':').Append(InstrumentText.Int(costs[r])).Append(','); }
			sb.Append("];costSrc=").Append(costSource ?? "").Append(";notes=[");
			if (dataNotes != null) { var k = new List<string>(dataNotes.Keys); k.Sort(StringComparer.Ordinal); foreach (var r in k) sb.Append(r).Append(':').Append(dataNotes[r]).Append(','); }
			sb.Append(']');
			return sb.ToString();
		}

		public CostModel CostFor(string root)
		{
			long c;
			return costs != null && root != null && costs.TryGetValue(root, out c) ? new CostModel(c, costSource) : CostModel.Unknown;
		}
	}

	/// <summary>Business-day calendars for the expiry / roll rules. Reuses the repo's NYSE calendar (EventRules.IsNyseBusinessDay over
	/// _NyseHolidays.Rules); no second holiday table. CmeEquityIndex = the NYSE days EventRules already uses for ES expiry / roll.</summary>
	public static class InstrumentCalendars
	{
		public const string NyseStandInNote = "calendar: NYSE stand-in (CME calendar pending M8)";
		/// <summary>The calendar's business-day test; null when none is assigned (Observed / None).</summary>
		public static Func<DateTime, bool> BusinessDay(ECalendar c)
		{
			switch (c)
			{
				case ECalendar.CmeEquityIndex: case ECalendar.Nyse: case ECalendar.NyseStandIn: return EventRules.IsNyseBusinessDay;
				default: return null;
			}
		}
		/// <summary>Log note for a stand-in calendar (printed on the INSTR line); null otherwise.</summary>
		public static string Note(ECalendar c) { return c == ECalendar.NyseStandIn ? NyseStandInNote : null; }
		/// <summary>BusinessDay(c), required: no calendar assigned → Error line, then throw 'calendar' (R6 / §6.4 S4).</summary>
		public static Func<DateTime, bool> Require(ECalendar c, InstrumentSubject s, Action<string> errorSink)
		{
			Func<DateTime, bool> bd = BusinessDay(c);
			if (bd == null) InstrumentError.Fail(errorSink, s, InstrumentReason.Calendar, "no business-day calendar assigned (calendar " + InstrumentText.CalendarName(c) + ") for the expiry/roll rules", "assign a calendar to the preset");
			return bd;
		}
	}

	/// <summary>§8.2 expiry / roll rule arithmetic. isBusinessDay is injected and REQUIRED (R6 / §6.4 S4: no silent weekday fallback;
	/// a missing calendar refuses with reason 'calendar'). Callers pass InstrumentCalendars.Require(profile.calendar) (= the profile's
	/// isBusinessDay); CL/GC/ZN/6E use the NYSE stand-in until PR-M8. Contract = (year, month).</summary>
	public static class InstrumentRules
	{
		/// <summary>Monday-before-third-Friday cutover (= EventRules.RollRuleMondayFrom; pinned equal by InstrumentTests).</summary>
		public static readonly DateTime EquityRollMondayFrom = new DateTime(2022, 4, 1);

		/// <summary>The business-day calendar is required: null logs the Error line and throws 'calendar'.</summary>
		static Func<DateTime, bool> requireCalendar(Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink)
		{
			if (isBusinessDay == null) InstrumentError.Fail(errorSink, s, InstrumentReason.Calendar, "no business-day calendar for the expiry/roll rules", "supply the instrument's exchange calendar");
			return isBusinessDay;
		}

		public static DateTime Nth(int y, int m, DayOfWeek d, int n) { DateTime f = new DateTime(y, m, 1); return f.AddDays(((int)d - (int)f.DayOfWeek + 7) % 7 + 7 * (n - 1)); }
		public static DateTime ThirdFriday(int y, int m) { return Nth(y, m, DayOfWeek.Friday, 3); }
		static DateTime prevOrSelf(DateTime d, Func<DateTime, bool> bd) { d = d.Date; while (!bd(d)) d = d.AddDays(-1); return d; }
		static DateTime back(DateTime d, int n, Func<DateTime, bool> bd) { d = d.Date; for (int c = 0; c < n;) { d = d.AddDays(-1); if (bd(d)) c++; } return d; }
		static DateTime lastBd(int y, int m, Func<DateTime, bool> bd) { return prevOrSelf(new DateTime(y, m, DateTime.DaysInMonth(y, m)), bd); }

		public static DateTime Expiry(EExpiryRule rule, int y, int m, Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink)
		{
			Func<DateTime, bool> bd = requireCalendar(isBusinessDay, s, errorSink);
			switch (rule)
			{
				case EExpiryRule.ThirdFridaySoq: return prevOrSelf(ThirdFriday(y, m), bd);
				case EExpiryRule.CL25thMinus3Bd:
				{
					DateTime p = new DateTime(y, m, 1).AddMonths(-1), d25 = new DateTime(p.Year, p.Month, 25);
					return back(d25, bd(d25) ? 3 : 4, bd);
				}
				case EExpiryRule.ThirdLastBd: return back(lastBd(y, m, bd), 2, bd);
				case EExpiryRule.LastBdMinus7Bd: return back(lastBd(y, m, bd), 7, bd);
				default: return back(Nth(y, m, DayOfWeek.Wednesday, 3), 2, bd);
			}
		}

		/// <summary>Nominal contract date used as the per-contract key and for "next contract": the unadjusted third Friday for
		/// ThirdFridaySoq (as _RollDetector.SeriesWindow), else the expiry.</summary>
		public static DateTime Nominal(EExpiryRule rule, int y, int m, Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink)
		{
			requireCalendar(isBusinessDay, s, errorSink);
			return rule == EExpiryRule.ThirdFridaySoq ? ThirdFriday(y, m) : Expiry(rule, y, m, isBusinessDay, s, errorSink);
		}

		/// <summary>Roll date of contract (y, m); null for ERollRule.None. First notice (BeforeFirstNotice) = last business day of the
		/// month before the contract month (VERIFY per root).</summary>
		public static DateTime? Roll(InstrumentPresetRow p, int y, int m, Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink)
		{
			Func<DateTime, bool> bd = requireCalendar(isBusinessDay, s, errorSink);
			switch (p.roll)
			{
				case ERollRule.EquityIndexCustomary: { DateTime f = ThirdFriday(y, m); return f >= EquityRollMondayFrom ? f.AddDays(-4) : f.AddDays(-8); }
				case ERollRule.BusinessDaysBeforeExpiry: return back(Expiry(p.expiry, y, m, bd, s, errorSink), p.rollDays, bd);
				case ERollRule.BeforeFirstNotice: { DateTime pm = new DateTime(y, m, 1).AddMonths(-1); return back(lastBd(pm.Year, pm.Month, bd), p.rollDays, bd); }
				default: return null;
			}
		}

		/// <summary>The listed contract (in p.contractMonths) whose nominal date is the first on/after day.</summary>
		public static void ContractFor(InstrumentPresetRow p, DateTime day, Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink, out int year, out int month)
		{
			requireCalendar(isBusinessDay, s, errorSink);
			DateTime d = day.Date; int y = d.Year, m = d.Month;
			for (int k = 0; k < 24; k++)
			{
				if (MonthMask.Has(p.contractMonths, m) && Nominal(p.expiry, y, m, isBusinessDay, s, errorSink) >= d) { year = y; month = m; return; }
				if (++m > 12) { m = 1; y++; }
			}
			year = 0; month = 0;
			InstrumentError.Fail(errorSink, s, InstrumentReason.Months, "no listed contract month within 24 months of " + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " for " + p.root, "check the preset's contract months");
		}

		/// <summary>MI-T6 generalised SeriesWindow: [Monday, Friday] of the week holding the roll date of the next contract (nominal
		/// date on/after tradingDay). expiry = that contract's nominal date (latch key). ES = _RollDetector.SeriesWindow.</summary>
		public static bool SeriesWindow(InstrumentPresetRow p, DateTime tradingDay, Func<DateTime, bool> isBusinessDay, InstrumentSubject s, Action<string> errorSink, out DateTime expiry)
		{
			int y, m; ContractFor(p, tradingDay, isBusinessDay, s, errorSink, out y, out m);
			expiry = Nominal(p.expiry, y, m, isBusinessDay, s, errorSink);
			DateTime? r = Roll(p, y, m, isBusinessDay, s, errorSink);
			if (r == null) return false;
			DateTime rule = r.Value, from = rule.AddDays(-(((int)rule.DayOfWeek + 6) % 7)), d = tradingDay.Date;
			return d >= from && d <= from.AddDays(4);
		}
	}

	/// <summary>§4.5 derived session fields (MC-free) over the stream's Sessions list in session (= bar) time. Sessions are a weekly
	/// template; bar close t belongs to the session instance with start &lt; t &lt;= end. The trading-day key is the calendar date of
	/// that instance's end; an end at exactly 00:00 (24:00) keys to the day it closes (the previous date).</summary>
	public static class InstrumentSessions
	{
		public struct Hit
		{
			public int Index;              // index in the Sessions list
			public DateTime Start, End;    // this instance
			public DateTime Key;           // trading day
		}

		static TimeSpan duration(_MtfMath.Session s)
		{
			TimeSpan d = TimeSpan.FromDays((((int)s.EndDay - (int)s.StartDay) % 7 + 7) % 7) + s.EndTime - s.StartTime;
			if (d <= TimeSpan.Zero) d += TimeSpan.FromDays(7);
			return d;
		}
		public static DateTime KeyOfEnd(DateTime end) { return end.TimeOfDay == TimeSpan.Zero ? end.Date.AddDays(-1) : end.Date; }

		/// <summary>The session instance containing bar close t (start exclusive, end inclusive); false when none.</summary>
		public static bool Find(_MtfMath.Session[] sessions, DateTime t, out Hit hit)
		{
			hit = new Hit { Index = -1 };
			if (sessions == null) return false;
			DateTime week = t.Date.AddDays(-(int)t.DayOfWeek);
			for (int i = 0; i < sessions.Length; i++)
			{
				TimeSpan dur = duration(sessions[i]);
				for (int w = -7; w <= 0; w += 7)
				{
					DateTime st = week.AddDays(w + (int)sessions[i].StartDay) + sessions[i].StartTime, en = st + dur;
					if (st < t && t <= en) { hit.Index = i; hit.Start = st; hit.End = en; hit.Key = KeyOfEnd(en); return true; }
				}
			}
			return false;
		}

		/// <summary>tradingDayKey(t); a bar outside every session → Error + throw sessions:bar-outside (S1).</summary>
		public static DateTime TradingDayKey(_MtfMath.Session[] sessions, DateTime t, InstrumentSubject s, Action<string> errorSink)
		{
			Hit h;
			if (sessions == null || sessions.Length == 0) InstrumentError.Fail(errorSink, s, InstrumentReason.SessionsNone, null, InstrumentReason.ActionSessions);
			if (!Find(sessions, t, out h))
				InstrumentError.Fail(errorSink, s, InstrumentReason.SessionsBarOutside, "bar " + t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " outside " + InstrumentText.Sessions(sessions), InstrumentReason.ActionSessions);
			return h.Key;
		}

		/// <summary>The session instances whose end keys to trading day key, ordered by start.</summary>
		public static List<Hit> Day(_MtfMath.Session[] sessions, DateTime key)
		{
			var r = new List<Hit>();
			if (sessions == null) return r;
			DateTime k = key.Date, week = k.AddDays(-(int)k.DayOfWeek);
			for (int i = 0; i < sessions.Length; i++)
			{
				TimeSpan dur = duration(sessions[i]);
				for (int w = -14; w <= 7; w += 7)
				{
					DateTime st = week.AddDays(w + (int)sessions[i].StartDay) + sessions[i].StartTime, en = st + dur;
					if (KeyOfEnd(en) == k) r.Add(new Hit { Index = i, Start = st, End = en, Key = k });
				}
			}
			r.Sort((a, b) => a.Start.CompareTo(b.Start));
			return r;
		}

		/// <summary>dayOpen = start of the first session of the trading day; false when the day has no session.</summary>
		public static bool DayOpen(_MtfMath.Session[] sessions, DateTime key, out DateTime open)
		{
			var d = Day(sessions, key); open = d.Count > 0 ? d[0].Start : default(DateTime); return d.Count > 0;
		}
		/// <summary>sessionEnd = end of the last session of the trading day, from the list (R14: GLOBEX 16:30 although data stops 16:00).</summary>
		public static bool SessionEnd(_MtfMath.Session[] sessions, DateTime key, out DateTime end)
		{
			var d = Day(sessions, key); end = default(DateTime);
			foreach (var h in d) if (h.End > end) end = h.End;
			return d.Count > 0;
		}
		/// <summary>hasOvernight = some session crosses midnight in session time (StartDay != EndDay). 24 h single-day templates: VERIFY (PR-M4).</summary>
		public static bool HasOvernight(_MtfMath.Session[] sessions)
		{
			if (sessions == null) return false;
			foreach (var s in sessions) if (s.StartDay != s.EndDay) return true;
			return false;
		}

		/// <summary>S1: null/empty → sessions:none; StartCalc vs first-bar read differ → sessions:changed.</summary>
		public static void Require(_MtfMath.Session[] startCalc, _MtfMath.Session[] firstBar, InstrumentSubject s, Action<string> errorSink)
		{
			if (startCalc == null || startCalc.Length == 0 || firstBar == null || firstBar.Length == 0) InstrumentError.Fail(errorSink, s, InstrumentReason.SessionsNone, null, InstrumentReason.ActionSessions);
			// detail text from the shared M1a comparer (Data1 = the StartCalc list, other = the first-bar list)
			string mismatch = _MtfMath.SessionMismatch(startCalc, firstBar);
			if (mismatch != null) InstrumentError.Fail(errorSink, s, InstrumentReason.SessionsChanged, "StartCalc vs first bar: " + mismatch, InstrumentReason.ActionSessions);
		}
	}

	/// <summary>§4.5 rthSplit (N1). Times are of day; EquityIndexLegacy values are ET (_CmeSessionClock defaults).</summary>
	public sealed class RthSplit
	{
		public readonly TimeSpan start, valueEnd, end;
		public readonly ERthSource source;
		public RthSplit(TimeSpan start, TimeSpan valueEnd, TimeSpan end, ERthSource source) { this.start = start; this.valueEnd = valueEnd; this.end = end; this.source = source; }
		public string Text() { return InstrumentText.Hm(start) + "-" + InstrumentText.Hm(end) + " src=" + InstrumentText.RthSourceName(source); }

		static bool equityIndexRoot(string r) { return r == "ES" || r == "MES" || r == "NQ" || r == "MNQ"; }

		/// <summary>Day-only chart (!hasOvernight) → the chart session (earliest start .. latest end); 24 h chart + ES/MES/NQ/MNQ futures →
		/// EquityIndexLegacy (09:30, value end 16:00, flag 16:15 ET); else null (S7).</summary>
		public static RthSplit Of(_MtfMath.Session[] sessions, EAssetClass cls, string root)
		{
			if (sessions == null || sessions.Length == 0) return null;
			if (!InstrumentSessions.HasOvernight(sessions))
			{
				TimeSpan s = TimeSpan.MaxValue, e = TimeSpan.MinValue;
				foreach (var x in sessions) { if (x.StartTime < s) s = x.StartTime; if (x.EndTime > e) e = x.EndTime; }
				return new RthSplit(s, e, e, ERthSource.ChartDaySession);
			}
			if (cls == EAssetClass.Future && equityIndexRoot(root))
				return new RthSplit(_CmeSessionClock.DefaultRthStart, _CmeSessionClock.DefaultRthValueEnd, _CmeSessionClock.DefaultRthEnd, ERthSource.EquityIndexLegacy);
			return null;
		}
	}

	/// <summary>R14 session-end detector, one per stream. A session ends (a) on a bar whose close equals the instance end, (b) on the
	/// first bar of a different session instance (attributed to the previous bar), or (c) when LastBarInSession is true. Each
	/// (trading day, session index) fires once, so a duplicate CalcBar at the same time resets nothing twice.</summary>
	public sealed class SessionEndDetector
	{
		public struct EndEvent
		{
			public int Index; public DateTime TradingDay, SessionEnd, BarTime; public char Why;   // 'E' end time, 'N' next session, 'L' LastBarInSession
		}
		readonly _MtfMath.Session[] m_sessions;
		bool m_hasPrev; InstrumentSessions.Hit m_prev; DateTime m_prevBar;
		readonly HashSet<EndKey> m_fired = new HashSet<EndKey>();
		readonly Queue<EndKey> m_order = new Queue<EndKey>();
		public SessionEndDetector(_MtfMath.Session[] sessions) { m_sessions = sessions; }

		/// <summary>Dedupe key: one session instance = (end time, session index); compared field by field, no bit packing.</summary>
		struct EndKey : IEquatable<EndKey>
		{
			public readonly DateTime End; public readonly int Index;
			public EndKey(InstrumentSessions.Hit h) { End = h.End; Index = h.Index; }
			public bool Equals(EndKey o) { return End == o.End && Index == o.Index; }
			public override bool Equals(object o) { return o is EndKey && Equals((EndKey)o); }
			public override int GetHashCode() { return unchecked(End.GetHashCode() * 31 + Index); }
		}
		bool fire(InstrumentSessions.Hit h, DateTime bar, char why, List<EndEvent> outList)
		{
			var k = new EndKey(h);
			if (!m_fired.Add(k)) return false;
			m_order.Enqueue(k); while (m_order.Count > 64) m_fired.Remove(m_order.Dequeue());
			outList.Add(new EndEvent { Index = h.Index, TradingDay = h.Key, SessionEnd = h.End, BarTime = bar, Why = why });
			return true;
		}

		/// <summary>Feed one bar (close time t). Returns the session ends detected on this call (0..2). A bar outside every session
		/// → Error + throw sessions:bar-outside.</summary>
		public List<EndEvent> Update(DateTime t, bool lastBarInSession, InstrumentSubject s, Action<string> errorSink)
		{
			var r = new List<EndEvent>();
			InstrumentSessions.Hit h;
			if (!InstrumentSessions.Find(m_sessions, t, out h))
				InstrumentError.Fail(errorSink, s, InstrumentReason.SessionsBarOutside, "bar " + t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " outside " + InstrumentText.Sessions(m_sessions), InstrumentReason.ActionSessions);
			if (m_hasPrev && (m_prev.Index != h.Index || m_prev.End != h.End)) fire(m_prev, m_prevBar, 'N', r);
			if (t == h.End) fire(h, t, 'E', r);
			else if (lastBarInSession) fire(h, t, 'L', r);
			m_hasPrev = true; m_prev = h; m_prevBar = t;
			return r;
		}
	}

	/// <summary>§5.1 immutable profile (instrument facts at StartCalc; session facts filled on the first bar via WithSessions).</summary>
	public sealed class InstrumentProfile
	{
		public string name, root, rootSource, presetId, currency, sessionName, cluster, dataNotes, exchange;
		public EAssetClass assetClass;
		public double tick, bigPointValue, tickValue, minMove;
		public ECalendar calendar;
		public int contractMonths, rollDays, sessionsPerYear, costFloorTicks;   // costFloorTicks -1 = undefined (cost Unknown or non-integer tick value)
		public EExpiryRule? expiry;
		public ERollRule? roll;
		public DateTime? contractExpiry;   // ASymbolInfo2.SymbolExpiry when not a placeholder
		public CostModel cost;
		public ESizingUnit sizingUnit;
		public _MtfMath.Session[] sessions;
		public bool hasOvernight;
		public RthSplit rthSplit;
		public string rootWarning;
		public Func<DateTime, bool> isBusinessDay;   // from calendar (InstrumentCalendars); not hashed (calendar is)

		InstrumentProfile() { }

		public InstrumentSubject Subject(string engine) { return new InstrumentSubject(engine, name, assetClass, root, sessionName); }

		/// <summary>Plain inputs (the host adapter reads them typed from Bars.Info / ASymbolInfo2).</summary>
		public struct Inputs
		{
			public string name, mcRoot, exchange, currency, sessionName;   // currency: ISO code from the typed SymbolCurrency, null = not USD/unknown
			public bool mcRootUnsupported;
			public EAssetClass assetClass;
			public double minMove, infoPriceScale, structPriceScale, infoBigPointValue, structBigPointValue;
			public DateTime symbolExpiry;
		}

		/// <summary>StartCalc part of buildProfiles(). Two phases (PR-M3 review): first the ENFORCED cross-checks on whatever data is
		/// there (MC-internal, preset tick / bpv, cost-floor tick value), so a log-only refusal can never hide a mismatch; then the
		/// refusals (class, config, root, tick, currency Q8). Every refusal writes the Error line first. Profile facts: once the preset
		/// cross-check passes, tick / bpv / tickValue are the PRESET's exact values (runtime-independent text and hash); the raw
		/// detected values appear only in mismatch details. No preset: the detected values (printed with G15).</summary>
		public static InstrumentProfile Build(Inputs i, InstrumentConfig cfg, Action<string> errorSink)
		{
			const string Eng = "INSTR";
			if (cfg == null) cfg = new InstrumentConfig();
			// ---- phase 1: enforced cross-checks (mismatch:*), before any log-only refusal
			var pick = InstrumentRoot.Pick(i.name, i.assetClass, i.mcRoot, i.mcRootUnsupported);
			var sx = new InstrumentSubject(Eng, i.name, i.assetClass, pick.Root, i.sessionName);
			InstrumentChecks.McInternal(i.structPriceScale, i.infoPriceScale, i.structBigPointValue, i.infoBigPointValue, sx, errorSink);   // raw inputs only
			double rawTick = i.infoPriceScale > 0 ? i.minMove / i.infoPriceScale : 0;
			string v = cfg.Validate();
			// an invalid config's override row is not trusted: the built-in preset still cross-checks (the config refusal follows in phase 2)
			InstrumentPresetRow pr = pick.Root.Length == 0 ? null
				: v == null && cfg.overrides != null && cfg.overrides.assetClass == i.assetClass && cfg.overrides.root == pick.Root ? cfg.overrides : InstrumentPresets.Find(pick.Root, i.assetClass);
			InstrumentChecks.Preset(pr, rawTick, i.infoBigPointValue, sx, errorSink);   // a non-positive detected tick vs a preset is mismatch:tick
			double tick = pr != null ? pr.tick : rawTick, bpv = pr != null ? pr.bigPointValue : i.infoBigPointValue;
			CostModel cost = v == null ? cfg.CostFor(pick.Root) : CostModel.Unknown;
			bool tickOk = tick > 0 && !double.IsInfinity(tick) && !double.IsNaN(tick);
			int floor = cost.known && tickOk ? CostModel.FloorTicks(cost.roundTripCents, tick, bpv, sx, errorSink) : -1;   // mismatch:tickValue
			// ---- phase 2: refusals (log only in M3, enforced from PR-M4)
			var subj0 = new InstrumentSubject(Eng, i.name, i.assetClass, "", i.sessionName);
			if (i.assetClass == EAssetClass.Unsupported) InstrumentError.Fail(errorSink, subj0, InstrumentReason.Class, null, "use a Future, Stock, SpotFx, Crypto or Index symbol");
			if (v != null) InstrumentError.Fail(errorSink, subj0, InstrumentReason.UnknownField("config"), v, "fix InstrumentConfig");
			var rr = InstrumentRoot.Resolve(i.name, i.assetClass, i.mcRoot, i.mcRootUnsupported, Eng, i.sessionName, errorSink);
			var p = new InstrumentProfile { name = i.name, assetClass = i.assetClass, root = rr.Root, rootSource = rr.Source, rootWarning = rr.Warning, exchange = i.exchange, sessionName = i.sessionName, minMove = i.minMove };
			InstrumentSubject s = p.Subject(Eng);
			InstrumentChecks.Tick(i.minMove, i.infoPriceScale, s, errorSink);   // refusal only; with a preset the mismatch above already fired
			if (i.currency != "USD") InstrumentError.Fail(errorSink, s, InstrumentReason.Currency, "SymbolCurrency " + (i.currency ?? "not USD"), "USD instruments only (Q8)");
			p.currency = i.currency;
			p.tick = tick;
			p.bigPointValue = bpv;
			p.tickValue = tick * bpv;   // preset product (0.00005 × 125000); printed / hashed with G15
			p.presetId = pr == null ? null : pr.root;
			p.calendar = pr == null ? InstrumentPresets.DefaultCalendar(i.assetClass) : pr.calendar;
			p.contractMonths = pr == null ? 0 : pr.contractMonths;
			p.expiry = pr == null ? (EExpiryRule?)null : pr.expiry;
			p.roll = pr == null ? (ERollRule?)null : pr.roll;
			p.rollDays = pr == null ? 0 : pr.rollDays;
			p.cluster = pr == null ? null : pr.cluster;
			// R6 / §6.4 S4: a preset with expiry / roll rules needs an assigned business-day calendar (no silent weekday default)
			p.isBusinessDay = p.expiry != null || p.roll != null ? InstrumentCalendars.Require(p.calendar, s, errorSink) : InstrumentCalendars.BusinessDay(p.calendar);
			p.contractExpiry = InstrumentRoot.UsableExpiry(i.symbolExpiry) ? i.symbolExpiry.Date : (DateTime?)null;
			p.sessionsPerYear = InstrumentPresets.SessionsPerYear(i.assetClass);
			p.sizingUnit = InstrumentPresets.SizingUnit(i.assetClass);
			p.cost = cost;
			string note; p.dataNotes = cfg.dataNotes != null && cfg.dataNotes.TryGetValue(p.root, out note) ? note : null;
			p.costFloorTicks = floor;
			return p;
		}

		/// <summary>First-bar part: S1 checks (sessions:none / sessions:changed) and the session-derived fields. Returns a copy.</summary>
		public InstrumentProfile WithSessions(_MtfMath.Session[] startCalc, _MtfMath.Session[] firstBar, Action<string> errorSink)
		{
			InstrumentSessions.Require(startCalc, firstBar, Subject("INSTR"), errorSink);
			var c = (InstrumentProfile)MemberwiseClone();
			c.sessions = (_MtfMath.Session[])firstBar.Clone();
			c.hasOvernight = InstrumentSessions.HasOvernight(c.sessions);
			c.rthSplit = RthSplit.Of(c.sessions, assetClass, root);
			return c;
		}

		/// <summary>Canonical profile text (facts only) and its FNV-1a 64 hash. Outside every configHash (rev-3 Q7).</summary>
		public string Canon()
		{
			var sb = new StringBuilder();
			sb.Append("class=").Append(InstrumentText.ClassName(assetClass)).Append(";root=").Append(root).Append(";preset=").Append(presetId ?? "-");
			sb.Append(";tick=").Append(InstrumentText.Fact(tick)).Append(";bpv=").Append(InstrumentText.Fact(bigPointValue)).Append(";ccy=").Append(currency);
			sb.Append(";cal=").Append(InstrumentText.CalendarName(calendar)).Append(";months=").Append(MonthMask.Text(contractMonths));
			sb.Append(";expiry=").Append(expiry == null ? "-" : InstrumentText.ExpiryName(expiry.Value)).Append(";roll=").Append(roll == null ? "-" : InstrumentText.RollName(roll.Value, rollDays));
			sb.Append(";cost=").Append(cost.known ? InstrumentText.Int(cost.roundTripCents) : "-").Append(";floor=").Append(InstrumentText.Int(costFloorTicks));
			sb.Append(";cluster=").Append(cluster ?? "-").Append(";spy=").Append(InstrumentText.Int(sessionsPerYear)).Append(";sessions=").Append(InstrumentText.Sessions(sessions));
			sb.Append(";rth=").Append(rthSplit == null ? "-" : rthSplit.Text());
			return sb.ToString();
		}
		public string ProfileHash()
		{
			ulong h = 14695981039346656037UL;
			unchecked { foreach (byte b in Encoding.UTF8.GetBytes(Canon())) { h ^= b; h *= 1099511628211UL; } }   // FNV-1a wraps mod 2^64 by design
			return h.ToString("x16", CultureInfo.InvariantCulture);
		}

		/// <summary>§6.1 INSTR line (Info, once per stream after the first-bar session read). catLabel / tzLabel / bvoLabel are the host's
		/// log formatting of the MC enums (printed only).</summary>
		public string InstrLine(int ds, string catLabel, string tzLabel, string bvoLabel)
		{
			string dayOpen = "?", sessEnd = "?";
			if (sessions != null && sessions.Length > 0)
			{
				// representative trading day: the first session's own day
				InstrumentSessions.Hit h; DateTime probe = new DateTime(2024, 1, 7).AddDays((int)sessions[0].StartDay) + sessions[0].StartTime + TimeSpan.FromMinutes(1);
				DateTime o, e;
				if (InstrumentSessions.Find(sessions, probe, out h) && InstrumentSessions.DayOpen(sessions, h.Key, out o) && InstrumentSessions.SessionEnd(sessions, h.Key, out e))
				{ dayOpen = InstrumentText.Hm(o.TimeOfDay); sessEnd = InstrumentText.Hm(e - h.Key.Date); }   // relative to the trading day: a 00:00 close the next day prints 24:00
			}
			var sb = new StringBuilder();
			sb.Append("INSTR ds(").Append(InstrumentText.Int(ds)).Append(") name(").Append(name).Append(") cat(").Append(catLabel).Append(") class(").Append(InstrumentText.ClassName(assetClass));
			sb.Append(") root(").Append(root).Append(" rootSrc=").Append(rootSource).Append(") preset(").Append(presetId ?? "none").Append(") exch(").Append(exchange).Append(") tz(").Append(tzLabel);
			sb.Append(") sessionName(").Append(sessionName).Append(") sessions(").Append(InstrumentText.Sessions(sessions)).Append(") dayKey(SessionEnd) dayOpen(").Append(dayOpen);
			sb.Append(") sessionEnd(").Append(sessEnd).Append(") overnight(").Append(hasOvernight ? "yes" : "no").Append(") rth(").Append(rthSplit == null ? "none src=None" : rthSplit.Text());
			sb.Append(") tick(").Append(InstrumentText.Fact(tick)).Append(") bpv(").Append(InstrumentText.Fact(bigPointValue)).Append(") tickValue(").Append(InstrumentText.Fact(tickValue));
			sb.Append(") ccy(").Append(currency).Append(") bvo(").Append(bvoLabel).Append(") calendar(").Append(InstrumentText.CalendarName(calendar));
			string calNote = InstrumentCalendars.Note(calendar);
			if (calNote != null) sb.Append("; ").Append(calNote);
			sb.Append(") months(").Append(MonthMask.Text(contractMonths));
			sb.Append(") expiry(").Append(expiry == null ? "Unknown" : InstrumentText.ExpiryName(expiry.Value)).Append(") roll(").Append(roll == null ? "Unknown" : InstrumentText.RollName(roll.Value, rollDays));
			sb.Append(") cost(").Append(cost.Text()).Append(") floor(").Append(costFloorTicks < 0 ? "Unknown" : InstrumentText.Int(costFloorTicks)).Append(") cluster(").Append(cluster ?? "none");
			if (dataNotes != null) sb.Append(") dataNotes(").Append(dataNotes);   // Q7: logged on INSTR (none configured by default)
			sb.Append(") profileHash(").Append(ProfileHash()).Append(')');
			return sb.ToString();
		}
	}

	/// <summary>
	/// PR-M3 wiring state, MC-free: one slot per data stream. StartCalc builds the instrument part (Build) and keeps the StartCalc
	/// session read; the first CalcBar re-reads the sessions (S1 re-check, WithSessions) and yields the INSTR line. M3 is LOG ONLY
	/// apart from the cross-checks: Enforced() reasons write the Error line and throw; any other refusal is one Warning line
	/// ("INSTR ds(n) log-only ...") and the stream keeps going without a profile (or, for a session refusal, with the StartCalc part).
	/// </summary>
	public sealed class InstrumentStreams
	{
		readonly InstrumentProfile[] m_profile;     // per stream (index = data stream); null = absent, not built, or refused (log only)
		readonly _MtfMath.Session[][] m_startCalc;
		readonly bool[] m_started, m_pending;

		public InstrumentStreams(int maxDataStream)
		{
			int n = Math.Max(1, maxDataStream) + 1;
			m_profile = new InstrumentProfile[n]; m_startCalc = new _MtfMath.Session[n][]; m_started = new bool[n]; m_pending = new bool[n];
		}

		public int Length { get { return m_profile.Length; } }
		public InstrumentProfile Profile(int ds) { return ds >= 0 && ds < m_profile.Length ? m_profile[ds] : null; }
		public bool Pending(int ds) { return ds >= 0 && ds < m_pending.Length && m_pending[ds]; }

		/// <summary>The cross-checks M3 enforces: §4.3 MC-internal / tick / preset mismatches and the S1 StartCalc vs first-bar
		/// re-check (sessions:changed). Everything else is log only until PR-M4.</summary>
		public static bool Enforced(string reason)
		{
			return reason != null && (reason.StartsWith("mismatch:", StringComparison.Ordinal) || reason == InstrumentReason.SessionsChanged);
		}

		public static string LogOnlyLine(int ds, string refusal)
		{
			return "INSTR ds(" + InstrumentText.Int(ds) + ") log-only, not enforced until PR-M4: " + refusal;
		}

		/// <summary>Runs one core step with a buffered error sink. Enforced reason: errorSink(the refusal) - the Error line - then
		/// rethrow. Other InstrumentException: warnSink(LogOnlyLine) and null. Any other exception: its own Error line, then rethrown
		/// unchanged (the host's buildProfiles catch-all then only adds the log-only Warning; see there).</summary>
		public static T Guard<T>(int ds, Func<Action<string>, T> step, Action<string> errorSink, Action<string> warnSink) where T : class
		{
			string msg = null;
			try { return step(m => msg = m); }
			catch (InstrumentException e)
			{
				if (Enforced(e.Reason)) { if (errorSink != null) errorSink(msg ?? e.Message); throw; }
				if (warnSink != null) warnSink(LogOnlyLine(ds, msg ?? e.Message));
				return null;
			}
			catch (Exception e)
			{
				// not a refusal (a bug or an MC failure inside the step): Error line here, then rethrow unchanged
				if (errorSink != null) errorSink("INSTR ds(" + InstrumentText.Int(ds) + ") unexpected " + e.GetType().Name + ": " + e.Message);
				throw;
			}
		}

		/// <summary>StartCalc: the instrument part of stream ds (null inputs = the typed read failed; the host logged why) and the
		/// StartCalc session read (null = none / unreadable). Logs the root-crosscheck Warning when the parse differs from MC.</summary>
		public void Start(int ds, InstrumentProfile.Inputs? i, _MtfMath.Session[] startCalcSessions, InstrumentConfig cfg, Action<string> errorSink, Action<string> warnSink)
		{
			m_started[ds] = true; m_pending[ds] = true; m_startCalc[ds] = startCalcSessions;
			m_profile[ds] = i == null ? null : Guard(ds, s => InstrumentProfile.Build(i.Value, cfg, s), errorSink, warnSink);
			if (m_profile[ds] != null && m_profile[ds].rootWarning != null && warnSink != null) warnSink(m_profile[ds].rootWarning);
		}

		/// <summary>First CalcBar of stream ds: S1 re-check and the session fields. Returns the INSTR line (null when the stream has no
		/// profile). A log-only session refusal keeps the StartCalc part (sessions n=0 on the line).</summary>
		public string FirstBar(int ds, _MtfMath.Session[] firstBarSessions, string catLabel, string tzLabel, string bvoLabel, Action<string> errorSink, Action<string> warnSink)
		{
			if (!Pending(ds)) return null;
			m_pending[ds] = false;
			InstrumentProfile p = m_profile[ds];
			if (p == null) return null;
			_MtfMath.Session[] sc = m_startCalc[ds];
			InstrumentProfile full = Guard(ds, s => p.WithSessions(sc, firstBarSessions, s), errorSink, warnSink);
			if (full != null) m_profile[ds] = full;
			return m_profile[ds].InstrLine(ds, catLabel, tzLabel, bvoLabel);
		}

		/// <summary>RUN line suffix: " profileHash(1=&lt;hash&gt;;2=none)" over the started streams; none = no profile (log-only
		/// refusal). Outside every configHash (rev-3 Q7). Empty when no stream was started (InstrumentConfig.enabled false).</summary>
		public string RunSuffix()
		{
			var sb = new StringBuilder();
			for (int ds = 1; ds < m_profile.Length; ds++)
			{
				if (!m_started[ds]) continue;
				sb.Append(sb.Length == 0 ? " profileHash(" : ";").Append(InstrumentText.Int(ds)).Append('=').Append(m_profile[ds] == null ? "none" : m_profile[ds].ProfileHash());
			}
			return sb.Length == 0 ? "" : sb.Append(')').ToString();
		}
	}
	// </instr-core>

	/// <summary>Typed MC members for the profile (R1). Called by _CustomSignal.buildProfiles() (PR-M3).</summary>
	public static class InstrumentMcFields
	{
		// <instr-mc-fields>
		/// <summary>§4.3 step 3: parser fallback when MC flags the root field as not provided (typed; also true for ALL = -1).</summary>
		public static bool RootUnsupported(MTPA_SymbolFields mcUnsupported) { return (mcUnsupported & MTPA_SymbolFields.eMTPA_SF_Root) != 0; }
		// </instr-mc-fields>

		/// <summary>ISO code of the typed SymbolCurrency; only USD is mapped (non-USD refuses, Q8), anything else = null.</summary>
		public static string CurrencyCode(MTPA_MCSymbolCurrency mcCurrency) { return mcCurrency == MTPA_MCSymbolCurrency.eMTPA_MCSC_USD ? "USD" : null; }

		/// <summary>Reads Bars.Info + Bars.Info.ASymbolInfo2 into the core's plain inputs. cls comes from _CustomSignal.ClassOf (typed).</summary>
		public static InstrumentProfile.Inputs Read(IInstrumentSettings info, EAssetClass cls)
		{
			var si = info.ASymbolInfo2;
			var i = new InstrumentProfile.Inputs();
			i.name = info.Name; i.exchange = info.Exchange; i.sessionName = info.SessionName; i.assetClass = cls;
			i.mcRoot = si.SymbolRoot; i.mcRootUnsupported = RootUnsupported(si.UnsupportedFields);
			i.currency = CurrencyCode(si.SymbolCurrency);
			i.minMove = info.MinMove; i.infoPriceScale = info.PriceScale; i.infoBigPointValue = info.BigPointValue;
			i.structPriceScale = si.PriceScale; i.structBigPointValue = si.BigPointValue;
			i.symbolExpiry = si.SymbolExpiry;
			return i;
		}
	}

	/// <summary>§4.5 SessionReader (host): typed IInstrument.Sessions / SessionObject members into the MC-free _MtfMath.Session.
	/// null when the list is null. Used in StartCalc and for the first-CalcBar re-check (and by _CustomSignal.mtfSessions).</summary>
	public static class SessionReader
	{
		public static _MtfMath.Session[] Read(IInstrument b)
		{
			IROList<SessionObject> list = b.Sessions;
			if (list == null) return null;
			var r = new _MtfMath.Session[list.Count];
			for (int i = 0; i < r.Length; i++) { SessionObject so = list[i]; r[i] = new _MtfMath.Session(so.StartDay, so.StartTime, so.EndDay, so.EndTime); }
			return r;
		}
	}
}
