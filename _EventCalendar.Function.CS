using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// M-EVENT (specs/m-event-spec.md rev 2) — scheduled-event calendar: US macro releases (§1.1), equity-index calendar
	// events (§1.2) and the EIA weekly petroleum report (class Energy only). Pure managed C#, MC-free (System only):
	// tests/EventTests extracts the regions below as is.
	//
	// The generated `// <event-table>` region (FOMC, CPI, NFP, ... from the official sources, EV-4.2) and s_corrections (EV-5)
	// are written by tools/m-event/build_table.py (PR B). A table kind with no row in a year gives neutral outputs
	// (NaN / false / not present), Configure journals "EVENT table kinds missing: <kinds>" (R2.1-a) and every rule kind
	// still generates.
	//
	// Times: rows are ET wall clock, converted with "Eastern Standard Time" (no DateTime.Now anywhere). Bars are intervals
	// [barOpenUtc, barCloseUtc) (EV-3.3), windows are half-open, lookups are as-of the current bar close (EV-5) by default.
	// The base never blocks orders: EventBlackout / EventFlattenNow are exposed only.
	// ============================================================================================

	// <event-rules>
	/// <summary>Event kinds. Values are stable (kindsMask bit = 1 &lt;&lt; kind, max 63; Validate asserts it).</summary>
	public enum EEventKind : byte
	{
		None = 0,
		// §1.1 macro (table kinds unless noted)
		FomcDecision = 1, FomcPress = 2, FomcMinutes = 3, Cpi = 4, Ppi = 5, Nfp = 6, Jolts = 7,
		Claims = 8,          // rule kind (Thursday 08:30, federal-holiday Wednesday)
		Pce = 9, GdpAdvance = 10, GdpSecond = 11, GdpThird = 12, RetailSales = 13,
		IsmMfg = 14, IsmSvc = 15,   // rule kinds (1st / 3rd business day 10:00 + s_ismExceptions)
		JacksonHole = 16, ChairTestimony = 17,
		EiaPetroleum = 18,   // rule kind, class Energy (Wednesday 10:30 + s_eiaExceptions)
		// §1.2 calendar (rule kinds, tier C)
		QuarterlyExpiry = 30, RollWeek = 31, MonthlyOpex = 32, IndexRebalance = 33, HalfDay = 34, VixExpiry = 35, RussellRecon = 36,
		Custom = 62          // CSV "Custom:<name>" (EventRec.name)
	}

	public enum EEventTier : byte { None = 0, T3 = 1, T2 = 2, T1 = 3, C = 8 }

	[Flags]
	/// <summary>EV-4.6b per-kind, per-day coverage state: a day inside a coverage hole (or past a kind's coverage) is Unknown.</summary>
	public enum EEventCoverage : byte { NoEvent = 0, Event = 1, Unknown = 2 }

	public enum EEventFlags : ushort
	{
		None = 0, Sep = 1, Quad = 2, Unscheduled = 4, Scheduled = 8, Delayed = 16, Canceled = 32, Csv = 64, RuleOnly = 128,
		Pm = 256   // spec-silent: the 16:00 SPXW PM-settled record of MonthlyOpex
	}

	[Flags]
	public enum EEventClassMask : byte { None = 0, Macro = 1, Calendar = 2, Energy = 4, All = 7 }

	/// <summary>One event instance. Spans (RollWeek) use etTime = start and etEnd = end.</summary>
	public struct EventRec
	{
		public EEventKind kind;
		public EEventTier tier;
		public EEventFlags flags;
		public DateTime etTime;          // ET wall clock
		public DateTime etEnd;           // spans only (else MinValue)
		public DateTime utc;
		public DateTime tradingDay;      // Globex trading date (EV-3.6)
		public DateTime knownSinceUtc;   // MinValue = known in advance
		public DateTime canceledSinceUtc;// MaxValue = never canceled
		public string refPeriod;
		public string name;              // Custom kinds
		public string source;

		public bool IsSpan { get { return etEnd != DateTime.MinValue; } }
		public EEventClassMask Class { get { return EventRules.ClassOf(kind, tier); } }

		public static EventRec Make(EEventKind kind, DateTime et, EEventTier tier, EEventFlags flags = EEventFlags.None)
		{
			EventRec r = new EventRec();
			r.kind = kind; r.tier = tier; r.flags = flags; r.etTime = et; r.etEnd = DateTime.MinValue;
			r.knownSinceUtc = DateTime.MinValue; r.canceledSinceUtc = DateTime.MaxValue;
			return r;
		}

		public string KindName { get { return kind == EEventKind.Custom ? "Custom:" + name : kind.ToString(); } }
	}

	/// <summary>Classification of one trading day (EV-6.5).</summary>
	public struct EventDay
	{
		public DateTime day;
		public EEventTier maxTier;       // macro class only (T1..T3 or None)
		public ulong kindsMask;          // bit (1 << kind)
		public bool hasFomc, hasSep, isHalfDay, isRollWeek, isQuarterlyExpiry, isOpex, isRebalance;
		public TimeSpan? firstReleaseEt; // earliest macro release (ET time of day)
		public int count;                // R2.1-g: Macro-class records only (like maxTier / firstReleaseEt)
		public bool eiaRuleOnly;         // R2.1-e: an EIA release on the day is RuleOnly (outside the published exception table)

		public bool Has(EEventKind k) { return (kindsMask & (1UL << (int)k)) != 0; }
	}

	/// <summary>One EV-5 correction row (s_corrections; empty until PR B). Times are yyyyMMddHHmm ET (0 = none).</summary>
	public struct EventCorrection
	{
		public EEventKind kind;
		public string refPeriod;
		public long originalEt;          // 0 = no original (a new unscheduled record)
		public long actualEt;            // 0 = Canceled (no replacement)
		public long announcedEt;         // 0 = not announced in advance (Unscheduled: knownSince = instant)
		public long canceledSinceEt;     // 0 = announcedEt (lapse rows set the lapse start, EV-5.2)
		public EEventFlags flags;
		public string source;
	}

	/// <summary>Protected EventConfig on _CustomSignal (no [Input]). Spec EV-6.1 defaults; off by default.</summary>
	public sealed class EventConfig
	{
		public bool enabled = false;
		public EEventClassMask classes = EEventClassMask.Macro | EEventClassMask.Calendar;   // EIA (Energy) never in the ES defaults
		public EEventTier minTier = EEventTier.T3;   // macro records below are dropped at build (tier C is kept)
		public Dictionary<EEventKind, EEventTier> tierOverride = new Dictionary<EEventKind, EEventTier>();
		public string csvPath = "";
		public bool allowPartialCoverage = false;   // EV-4.6b: false = an Unknown (coverage hole) day of a kind in the mask blocks
		public int blackoutPreMin = 0, blackoutPostMin = 0;   // 0/0 = no blackout
		public EEventTier blackoutTier = EEventTier.T1;
		public bool blackoutFlatten = false;
		public bool publish = false;
		// ---- spec-silent knobs ----
		public bool includeRussell = false;          // RussellRecon (optional, VERIFY rule) off
		internal string csvSha = null;               // set by Configure (EV-4.4: the CSV hash goes into Canon)

		public static bool IsMacroTier(EEventTier t) { return t == EEventTier.T1 || t == EEventTier.T2 || t == EEventTier.T3; }
		static bool validTier(EEventTier t) { return t == EEventTier.None || IsMacroTier(t) || t == EEventTier.C; }

		public string Validate()
		{
			var e = new List<string>();
			if (classes == EEventClassMask.None || ((byte)classes & ~(byte)EEventClassMask.All) != 0) e.Add("classes=" + classes);
			if (!validTier(minTier)) e.Add("minTier=" + minTier);
			if (blackoutPreMin < 0 || blackoutPreMin > 1440) e.Add("blackoutPreMin=" + blackoutPreMin);
			if (blackoutPostMin < 0 || blackoutPostMin > 1440) e.Add("blackoutPostMin=" + blackoutPostMin);
			if (!IsMacroTier(blackoutTier)) e.Add("blackoutTier=" + blackoutTier);
			if (tierOverride != null) foreach (var kv in tierOverride) if (!validTier(kv.Value) || kv.Value == EEventTier.None) e.Add("tierOverride " + kv.Key + "=" + kv.Value);
			foreach (EEventKind k in Enum.GetValues(typeof(EEventKind))) if ((int)k > 63) e.Add("kind " + k + " > 63");
			if (csvPath == null) e.Add("csvPath null");
			return e.Count == 0 ? null : "EventConfig: " + string.Join(", ", e);
		}

		/// <summary>Canonical config string (CS-3 RUN line / M-PORT hashes). Includes the table and CSV hashes.</summary>
		public string Canon()
		{
			var sb = new StringBuilder();
			sb.Append("enabled=").Append(enabled ? 1 : 0).Append(";classes=").Append((int)classes).Append(";minTier=").Append(minTier);
			sb.Append(";tierOverride=[");
			if (tierOverride != null) { var keys = new List<EEventKind>(tierOverride.Keys); keys.Sort(); foreach (var k in keys) sb.Append(k).Append(':').Append(tierOverride[k]).Append(','); }
			sb.Append("];csv=").Append(csvPath ?? "").Append(";csvSha=").Append(csvSha ?? "-");
			sb.Append(";partial=").Append(allowPartialCoverage ? 1 : 0);
			sb.Append(";blackout=").Append(blackoutPreMin).Append('/').Append(blackoutPostMin).Append('/').Append(blackoutTier).Append('/').Append(blackoutFlatten ? 1 : 0);
			sb.Append(";publish=").Append(publish ? 1 : 0).Append(";russell=").Append(includeRussell ? 1 : 0);
			sb.Append(";table=").Append(EventRules.TableSha());
			return sb.ToString();
		}
	}

	/// <summary>Rule kinds (§1.1 Claims/ISM/EIA, §1.2 calendar), sourced exception lists and the table decoder.</summary>
	public static partial class EventRules
	{
		public static readonly TimeSpan T0830 = new TimeSpan(8, 30, 0), T0930 = new TimeSpan(9, 30, 0), T1000 = new TimeSpan(10, 0, 0),
			T1030 = new TimeSpan(10, 30, 0), T1600 = new TimeSpan(16, 0, 0);

		/// <summary>Monday-before-third-Friday roll rule applies from this date; earlier contracts use the "Thursday eight calendar
		/// days before expiration" wording. Wayback (CDX listing of https://www.cmegroup.com/trading/equity-index/rolldates.html):
		/// capture 20220609055445 still shows the Thursday rule (ESM22 roll Thu 2022-06-09, ESU22 Thu 2022-09-08); capture
		/// 20220610060819 (https://web.archive.org/web/20220610060819/https://www.cmegroup.com/trading/equity-index/rolldates.html)
		/// is the first with the Monday rule. ESM22 therefore rolled Thu 06-09 and the Monday rule starts with ESU22 (Mon 2022-09-12);
		/// cutover 2022-07-01 sits between them.</summary>
		public static readonly DateTime RollRuleMondayFrom = new DateTime(2022, 7, 1);

		// ISM Report On Business release calendar. Source: https://www.ismworld.org/supply-management-news-and-reports/reports/rob-report-calendar/
		// (fetched 2026-10-22; 2026: Mfg Jan 5 ... Dec 1, Svc Jan 7 ... Dec 3, 10:00 ET). The 1st / 3rd NYSE business day rule reproduces
		// every 2026 date except January (ISM holiday Jan 2), listed here. Other years are RuleOnly: PR B found no archived ISM
		// calendar reachable from the official site (Wayback blocked), so pre-2026 / post-2026 dates are refused as table rows.
		static readonly int[] s_ismPublishedYears = { 2026 };
		static readonly long[] s_ismExceptions = {   // kind, yyyyMMdd
			(long)EEventKind.IsmMfg, 20260105,
			(long)EEventKind.IsmSvc, 20260107,
		};

		// EIA Weekly Petroleum Status Report alternate release schedule. Source: https://www.eia.gov/petroleum/supply/weekly/schedule.php
		// (fetched 2026-10-22). Pairs: original Wednesday (data week ending + 5 days) yyyyMMdd, alternate release yyyyMMddHHmm ET.
		// Releases before 2025-01-01 or after 2026-11-30 are RuleOnly: the EIA WPSR archive
		// (https://www.eia.gov/petroleum/supply/weekly/archive/) lists release dates but no times, and Wayback captures of the
		// schedule page were not reachable in PR B, so earlier exceptions are refused rather than guessed.
		static readonly DateTime s_eiaPublishedFrom = new DateTime(2025, 1, 1), s_eiaPublishedTo = new DateTime(2026, 11, 30);
		static readonly long[] s_eiaExceptions = {
			20250101, 202501021100,   // week ending 2024-12-27
			20250122, 202501231200,   // 2025-01-17
			20250219, 202502201200,   // 2025-02-14
			20250528, 202505291200,   // 2025-05-23
			20250903, 202509041200,   // 2025-08-29
			20251015, 202510161200,   // 2025-10-10
			20251112, 202511131200,   // 2025-11-07
			20251224, 202512291700,   // 2025-12-19 (Mon 17:00)
			20260121, 202601221200,   // 2026-01-16
			20260218, 202602191200,   // 2026-02-13
			20260527, 202605281200,   // 2026-05-22
			20260909, 202609101200,   // 2026-09-04
			20261014, 202610151200,   // 2026-10-09
			20261111, 202611121200,   // 2026-11-06
		};

		// Initial Claims: Thursday 08:30 ET; a federal holiday on Thursday moves it to Wednesday. Source: DOL ETA
		// https://oui.doleta.gov/unemploy/claims_arch.asp (fetched 2026-10-22: Wed 2026-11-25, Wed 2027-11-10, Wed 2027-11-24 — all
		// reproduced by the rule). PR B checks the rule against ALFRED release dates (rid 180); every differing date is emitted
		// by build_table.py as a generated Claims correction in s_corrections.

		public static EEventTier DefaultTier(EEventKind k)
		{
			switch (k)
			{
				case EEventKind.FomcDecision: case EEventKind.Cpi: case EEventKind.Nfp: return EEventTier.T1;
				case EEventKind.FomcPress: case EEventKind.FomcMinutes: case EEventKind.Ppi: case EEventKind.Pce: case EEventKind.GdpAdvance:
				case EEventKind.RetailSales: case EEventKind.IsmMfg: case EEventKind.JacksonHole: case EEventKind.ChairTestimony:
				case EEventKind.EiaPetroleum: return EEventTier.T2;
				case EEventKind.Jolts: case EEventKind.Claims: case EEventKind.GdpSecond: case EEventKind.GdpThird: case EEventKind.IsmSvc: return EEventTier.T3;
				case EEventKind.Custom: return EEventTier.None;
				default: return EEventTier.C;
			}
		}

		public static EEventClassMask ClassOf(EEventKind k, EEventTier tier)
		{
			if (k == EEventKind.EiaPetroleum) return EEventClassMask.Energy;
			if (k == EEventKind.Custom) return tier == EEventTier.C ? EEventClassMask.Calendar : EEventClassMask.Macro;
			return (int)k >= 30 ? EEventClassMask.Calendar : EEventClassMask.Macro;
		}

		/// <summary>Kinds that come only from the generated table (PR B).</summary>
		public static readonly EEventKind[] TableKinds = {
			EEventKind.FomcDecision, EEventKind.FomcPress, EEventKind.FomcMinutes, EEventKind.Cpi, EEventKind.Ppi, EEventKind.Nfp,
			EEventKind.Jolts, EEventKind.Pce, EEventKind.GdpAdvance, EEventKind.GdpSecond, EEventKind.GdpThird, EEventKind.RetailSales,
			EEventKind.JacksonHole, EEventKind.ChairTestimony };

		public static bool IsTableKind(EEventKind k) { return Array.IndexOf(TableKinds, k) >= 0; }

		// ---- calendar helpers ----
		public static DateTime ThirdFriday(int y, int m) { return nth(y, m, DayOfWeek.Friday, 3); }
		static DateTime nth(int y, int m, DayOfWeek d, int n) { DateTime f = new DateTime(y, m, 1); return f.AddDays(((int)d - (int)f.DayOfWeek + 7) % 7 + 7 * (n - 1)); }
		static DateTime last(int y, int m, DayOfWeek d) { DateTime l = new DateTime(y, m, DateTime.DaysInMonth(y, m)); return l.AddDays(-(((int)l.DayOfWeek - (int)d + 7) % 7)); }

		static readonly Dictionary<int, HashSet<DateTime>> s_nyse = new Dictionary<int, HashSet<DateTime>>();
		public static bool IsNyseHoliday(DateTime d)
		{
			HashSet<DateTime> h;
			lock (s_nyse) { if (!s_nyse.TryGetValue(d.Year, out h)) { h = new HashSet<DateTime>(_NyseHolidays.Rules.Holidays(d.Year)); s_nyse[d.Year] = h; } }
			return h.Contains(d.Date);
		}
		public static bool IsNyseBusinessDay(DateTime d) { return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !IsNyseHoliday(d); }
		public static DateTime PrevBusinessDayOrSelf(DateTime d) { d = d.Date; while (!IsNyseBusinessDay(d)) d = d.AddDays(-1); return d; }
		public static DateTime NthBusinessDay(int y, int m, int n) { DateTime d = new DateTime(y, m, 1); int c = 0; for (; ; d = d.AddDays(1)) if (IsNyseBusinessDay(d) && ++c == n) return d; }

		static DateTime fedObserved(DateTime d) { return d.DayOfWeek == DayOfWeek.Saturday ? d.AddDays(-1) : d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d; }
		/// <summary>US federal holiday (observed) — only the ones that can fall on a Thursday matter for Claims.</summary>
		public static bool IsFederalHoliday(DateTime d)
		{
			d = d.Date; int y = d.Year;
			if (d == fedObserved(new DateTime(y, 1, 1)) || d == fedObserved(new DateTime(y + 1, 1, 1))) return true;
			if (d == nth(y, 1, DayOfWeek.Monday, 3) || d == nth(y, 2, DayOfWeek.Monday, 3) || d == last(y, 5, DayOfWeek.Monday)) return true;
			if (y >= 2021 && d == fedObserved(new DateTime(y, 6, 19))) return true;
			if (d == fedObserved(new DateTime(y, 7, 4)) || d == nth(y, 9, DayOfWeek.Monday, 1) || d == nth(y, 10, DayOfWeek.Monday, 2)) return true;
			if (d == fedObserved(new DateTime(y, 11, 11)) || d == nth(y, 11, DayOfWeek.Thursday, 4) || d == fedObserved(new DateTime(y, 12, 25))) return true;
			return false;
		}

		/// <summary>Standard (AM) SPX options expiry (MonthlyOpex, VixExpiry): third Friday, or the preceding NYSE business day when
		/// it is an NYSE holiday (Cboe follows the NYSE calendar).</summary>
		public static DateTime OpexDate(int y, int m) { return PrevBusinessDayOrSelf(ThirdFriday(y, m)); }
		/// <summary>Equity-index futures expiry (QuarterlyExpiry, RollWeek, SessionsToExpiry), R2.1-d as amended (PR27 review) and
		/// EV-1.20: third Friday, moved back to the preceding NYSE business day when NYSE is closed (the SOQ needs an NYSE open).
		/// Authority: CME's per-contract calendar (CmeWS/mvc/ProductCalendar/Future/133), e.g. ESM26 Thu 2026-06-18, ESM27 Thu
		/// 2027-06-17 (Juneteenth), ESH08 Thu 2008-03-20 (Good Friday). Not rolldates.html (stale customary dates). No overrides.</summary>
		public static DateTime ExpiryDate(int y, int m) { return RuleExpiryDate(y, m); }
		/// <summary>The R2.1-d rule (same as OpexDate); kept as a name for the build_table.py rule-vs-CME check.</summary>
		public static DateTime RuleExpiryDate(int y, int m) { return PrevBusinessDayOrSelf(ThirdFriday(y, m)); }
		/// <summary>Customary roll date (EV §1.2): Monday before the third Friday (from RollRuleMondayFrom), else the Thursday eight
		/// calendar days before it.</summary>
		public static DateTime RollDate(int y, int m)
		{
			DateTime f = ThirdFriday(y, m);
			return f >= RollRuleMondayFrom ? f.AddDays(-4) : f.AddDays(-8);
		}
		/// <summary>VIX futures final settlement (Cboe VX contract specs, https://www.cboe.com/tradable_products/vix/vix_futures/specifications/):
		/// "the Wednesday that is 30 days prior to the third Friday of the calendar month immediately following the month in which
		/// the contract expires. If that Wednesday or the Friday that is 30 days following that Wednesday is a Cboe Options holiday,
		/// the final settlement date ... shall be on the business day immediately preceding that Wednesday."</summary>
		public static DateTime VixExpiry(int y, int m)
		{
			DateTime n = new DateTime(y, m, 1).AddMonths(1);
			DateTime f = ThirdFriday(n.Year, n.Month), w = f.AddDays(-30);
			return IsNyseHoliday(w) || IsNyseHoliday(f) ? PrevBusinessDayOrSelf(w.AddDays(-1)) : w;
		}

		static bool contains(int[] a, int v) { return Array.IndexOf(a, v) >= 0; }
		public static DateTime FromYmdhm(long v)
		{
			if (v < 100000000000L || v > 999999999999L) throw new ArgumentException("FromYmdhm expects 12-digit yyyyMMddHHmm, got " + v);
			return new DateTime((int)(v / 100000000), (int)(v / 1000000 % 100), (int)(v / 10000 % 100), (int)(v / 100 % 100), (int)(v % 100), 0);
		}
		public static long ToYmdhm(DateTime d) { return d.Year * 100000000L + d.Month * 1000000L + d.Day * 10000L + d.Hour * 100L + d.Minute; }

		/// <summary>All rule-kind records whose ET start falls in the year (unsorted, utc not yet set).</summary>
		public static List<EventRec> Generate(int y, bool includeRussell)
		{
			var r = new List<EventRec>(200);
			// Claims
			for (DateTime d = nth(y, 1, DayOfWeek.Thursday, 1); d.Year == y; d = d.AddDays(7))
			{
				DateTime day = IsFederalHoliday(d) ? d.AddDays(-1) : d;
				r.Add(EventRec.Make(EEventKind.Claims, day + T0830, EEventTier.T3, EEventFlags.RuleOnly));
			}
			// ISM
			bool ismPub = contains(s_ismPublishedYears, y);
			for (int m = 1; m <= 12; ++m)
			{
				r.Add(EventRec.Make(EEventKind.IsmMfg, ismDay(EEventKind.IsmMfg, y, m, 1) + T1000, EEventTier.T2, ismPub ? EEventFlags.None : EEventFlags.RuleOnly));
				r.Add(EventRec.Make(EEventKind.IsmSvc, ismDay(EEventKind.IsmSvc, y, m, 3) + T1000, EEventTier.T3, ismPub ? EEventFlags.None : EEventFlags.RuleOnly));
			}
			// EIA (Energy)
			for (DateTime d = nth(y, 1, DayOfWeek.Wednesday, 1); d.Year == y; d = d.AddDays(7))
			{
				long key = d.Year * 10000L + d.Month * 100 + d.Day;
				DateTime et = d + T1030;
				for (int i = 0; i < s_eiaExceptions.Length; i += 2) if (s_eiaExceptions[i] == key) { et = FromYmdhm(s_eiaExceptions[i + 1]); break; }
				bool pub = et >= s_eiaPublishedFrom && et < s_eiaPublishedTo.AddDays(1);
				r.Add(EventRec.Make(EEventKind.EiaPetroleum, et, EEventTier.T2, pub ? EEventFlags.None : EEventFlags.RuleOnly));
			}
			// §1.2 calendar
			for (int m = 1; m <= 12; ++m)
			{
				bool q = m % 3 == 0;
				DateTime o = OpexDate(y, m), x = ExpiryDate(y, m);
				r.Add(EventRec.Make(EEventKind.MonthlyOpex, o + T0930, EEventTier.C, q ? EEventFlags.Quad : EEventFlags.None));
				r.Add(EventRec.Make(EEventKind.MonthlyOpex, o + T1600, EEventTier.C, EEventFlags.Pm | (q ? EEventFlags.Quad : EEventFlags.None)));
				if (q)
				{
					// 09:30 marks the open on expiry day (SOQ calculation begins), NOT a settlement instant: the final settlement
					// price is the Special Opening Quotation, published after all components open.
					r.Add(EventRec.Make(EEventKind.QuarterlyExpiry, x + T0930, EEventTier.C, EEventFlags.Quad));
					r.Add(EventRec.Make(EEventKind.IndexRebalance, o + T1600, EEventTier.C));   // index rebalance at the NYSE close
					EventRec s = EventRec.Make(EEventKind.RollWeek, RollDate(y, m), EEventTier.C);
					s.etEnd = x + T0930;
					r.Add(s);
				}
				r.Add(EventRec.Make(EEventKind.VixExpiry, VixExpiry(y, m) + T0930, EEventTier.C, EEventFlags.RuleOnly));
			}
			for (DateTime d = new DateTime(y, 1, 1); d.Year == y; d = d.AddDays(1))
			{
				var h = _CmeHolidays.Rules.Get(d);
				if (h.Closed) continue;
				if (h.RthCloseEt.HasValue) r.Add(EventRec.Make(EEventKind.HalfDay, d + h.RthCloseEt.Value, EEventTier.C));
				else if (h.NoRth && h.HaltEt.HasValue) r.Add(EventRec.Make(EEventKind.HalfDay, d + h.HaltEt.Value, EEventTier.C));
			}
			if (includeRussell) r.Add(EventRec.Make(EEventKind.RussellRecon, last(y, 6, DayOfWeek.Friday) + T1600, EEventTier.C, EEventFlags.RuleOnly));
			return r;
		}

		static DateTime ismDay(EEventKind k, int y, int m, int n)
		{
			for (int i = 0; i < s_ismExceptions.Length; i += 2)
			{
				long v = s_ismExceptions[i + 1];
				if (s_ismExceptions[i] == (long)k && v / 10000 == y && v / 100 % 100 == m) return new DateTime(y, m, (int)(v % 100));
			}
			return NthBusinessDay(y, m, n);
		}

		/// <summary>Decoded generated table (EV-4.2). Empty in PR A.</summary>
		/// <summary>EV-4.6 coverage holes of the generated table: triples kind, first ET minute, last ET minute.</summary>
		public static long[] CoverageHoles() { return (long[])s_coverageHoles.Clone(); }
		public static string HoleWhyFor(long kind, long fromEt, long toEt)
		{
			for (int i = 0; i + 2 < s_coverageHoles.Length; i += 3)
				if (s_coverageHoles[i] == kind && s_coverageHoles[i + 1] == fromEt && s_coverageHoles[i + 2] == toEt) return s_coverageHoleWhy[i / 3];
			return null;
		}
		public static List<EventRec> Table()
		{
			var r = new List<EventRec>(s_yyyymmddhhmm.Length);
			for (int i = 0; i < s_yyyymmddhhmm.Length; ++i)
			{
				EEventKind k = (EEventKind)s_kind[i];
				EventRec e = EventRec.Make(k, FromYmdhm(s_yyyymmddhhmm[i]), DefaultTier(k), (EEventFlags)s_flags[i]);
				e.refPeriod = s_ref[i];
				r.Add(e);
			}
			return r;
		}
		public static EventCorrection[] Corrections { get { return s_corrections; } }

		static string s_tableSha;
		public static string TableSha()
		{
			if (s_tableSha == null) s_tableSha = ComputeTableSha(s_yyyymmddhhmm, s_kind, s_flags, s_ref, s_corrections, s_coverageHoles);
			return s_tableSha;
		}
		public static string ExpectedTableSha { get { return s_expectedTableSha; } }
		public static int TableRowCount { get { return s_yyyymmddhhmm.Length; } }
		/// <summary>EV-4.6b: the only engine-wide disable: the generated table is missing or its hash differs from the one
		/// build_table.py emitted. Returns null when the table is intact.</summary>
		public static string TableIntegrity(int rows, string actualSha, string expectedSha)
		{
			if (rows == 0) return "event table missing";
			if (actualSha != expectedSha) return "event table hash " + actualSha + " != expected " + expectedSha;
			return null;
		}
		/// <summary>Hash of table rows, corrections and coverage holes (any one changing changes the config hash).</summary>
		public static string ComputeTableSha(long[] et, byte[] kind, ushort[] flags, string[] refs, EventCorrection[] corr, long[] holes)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < et.Length; ++i) sb.Append(et[i]).Append(',').Append(kind[i]).Append(',').Append(flags[i]).Append(',').Append(refs[i]).Append(';');
			foreach (var c in corr) sb.Append(c.kind).Append(',').Append(c.originalEt).Append(',').Append(c.actualEt).Append(',').Append(c.announcedEt).Append(',').Append(c.canceledSinceEt).Append(',').Append((int)c.flags).Append(';');
			if (holes != null && holes.Length > 0) { sb.Append("holes"); foreach (var h in holes) sb.Append(',').Append(h); }
			return Sha256(Encoding.UTF8.GetBytes(sb.ToString())).Substring(0, 16);
		}
		public static string Sha256(byte[] b) { using (var h = SHA256.Create()) { var d = h.ComputeHash(b); var sb = new StringBuilder(64); foreach (var x in d) sb.Append(x.ToString("x2")); return sb.ToString(); } }
	}
	// </event-rules>

	// <event-table>
	// GENERATED by tools/m-event/build_table.py v1.1.0 (EV-4.5) -- do not edit by hand; re-run the script.
	// Pages fetched 2026-10-23 16:36 .. 2026-10-23 16:36 UTC (raw copies + manifest: tools/m-event/raw/). Source sha256[:16]:
	//   alfred_10.txt              3f1aa6890bd07d49  https://alfred.stlouisfed.org/release/downloaddates?rid=10&ff=txt
	//   alfred_180.txt             db4d35accccee081  https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt
	//   alfred_192.txt             404e5797f315d589  https://alfred.stlouisfed.org/release/downloaddates?rid=192&ff=txt
	//   alfred_46.txt              99bf88b0224a35df  https://alfred.stlouisfed.org/release/downloaddates?rid=46&ff=txt
	//   alfred_50.txt              9f71eb2b522c7a2e  https://alfred.stlouisfed.org/release/downloaddates?rid=50&ff=txt
	//   alfred_53.txt              fc6dd6a6fe1344dd  https://alfred.stlouisfed.org/release/downloaddates?rid=53&ff=txt
	//   alfred_54.txt              c85741e67e24a0ef  https://alfred.stlouisfed.org/release/downloaddates?rid=54&ff=txt
	//   alfred_9.txt               9559c918ae7eb120  https://alfred.stlouisfed.org/release/downloaddates?rid=9&ff=txt
	//   bea_2025.html              e9a06518036503c6  https://www.bea.gov/news/schedule/full-2025
	//   bea_2026.html              4ad3c61cf17ceba0  https://www.bea.gov/news/schedule/full
	//   bea_2027.html              d3a9e7d193f61db7  https://www.bea.gov/news/schedule/next-year
	//   bls_2016.html              da838eb21650da1f  https://www.bls.gov/schedule/2016/home.htm
	//   bls_2017.html              cefc78608ffdf1cf  https://www.bls.gov/schedule/2017/home.htm
	//   bls_2018.html              0508017239cb944b  https://www.bls.gov/schedule/2018/home.htm
	//   bls_2019.html              79d544badfec76a4  https://www.bls.gov/schedule/2019/home.htm
	//   bls_2020.html              0ee7b9af2cdef3b3  https://www.bls.gov/schedule/2020/home.htm
	//   bls_2021.html              c09deb50d5817890  https://www.bls.gov/schedule/2021/home.htm
	//   bls_2022.html              0fa59a88a3a60572  https://www.bls.gov/schedule/2022/home.htm
	//   bls_2023.html              7e2fc48b29ddf437  https://www.bls.gov/schedule/2023/home.htm
	//   bls_2024.html              87b2a4ce0bc7e2ac  https://www.bls.gov/schedule/2024/home.htm
	//   bls_2025.html              e2739b849e058c11  https://www.bls.gov/schedule/2025/home.htm
	//   bls_2026.html              1bfb9d805fb7b419  https://www.bls.gov/schedule/2026/home.htm
	//   bls_2027.html              adc95365efa2f193  https://www.bls.gov/schedule/2027/home.htm
	//   bls_lapse.html             66a58a4ded748779  https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm
	//   cboe_vx_specs.html         91f6df94a630974e  https://www.cboe.com/tradable_products/vix/vix_futures/specifications/
	//   census_marts.xlsx          110fa24643b1a33b  https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx
	//   cme_es_calendar.json       49ae6c22f53d3181  https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/133
	//   cme_rolldates.html         849521bc08efc29f  https://www.cmegroup.com/trading/equity-index/rolldates.html
	//   dol_claims.html            80e78de786a55636  https://oui.doleta.gov/unemploy/claims_arch.asp
	//   eia_wpsr_archive.html      ba0146a95af8afd7  https://www.eia.gov/petroleum/supply/weekly/archive/
	//   fed_calendars.html         72b771bb425faedb  https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm
	//   fed_hist_2016.html         32fe32a3d2210d1b  https://www.federalreserve.gov/monetarypolicy/fomchistorical2016.htm
	//   fed_hist_2017.html         1bab7926c61554f7  https://www.federalreserve.gov/monetarypolicy/fomchistorical2017.htm
	//   fed_hist_2018.html         f649bafd0c925886  https://www.federalreserve.gov/monetarypolicy/fomchistorical2018.htm
	//   fed_hist_2019.html         4fd85441c17a28e9  https://www.federalreserve.gov/monetarypolicy/fomchistorical2019.htm
	//   fed_hist_2020.html         686ec7998d50e8c0  https://www.federalreserve.gov/monetarypolicy/fomchistorical2020.htm
	//   fed_pr_20200303.html       caa2e79d587296b8  https://www.federalreserve.gov/newsevents/pressreleases/monetary20200303a.htm
	//   fed_pr_20200315.html       f6914aa595895b72  https://www.federalreserve.gov/newsevents/pressreleases/monetary20200315a.htm
	//   wayback 2022-06-10 06:08:19 UTC (https://web.archive.org/web/20220610060819/https://www.cmegroup.com/trading/equity-index/rolldates.html): first capture with the Monday rule: 2022-09 roll Mon 9/12; 2022-12 Mon 12/12 (ESM22 had already rolled Thu 6/9)
	//   wayback 2022-06-09 05:54:45 UTC (https://web.archive.org/web/20220609055445/https://www.cmegroup.com/trading/equity-index/rolldates.html): last capture with the Thursday rule: 2022-06 roll Thu 6/9; 2022-09 Thu 9/8
	// NOTE time assumed 08:30 (not in the source): BEA GdpAdvance/GdpSecond/GdpThird/Pce rows 2016-2024 (ALFRED dates only) and every RetailSales row (Census sheet has dates only).
	// NOTE GdpSecond 2019-02-28 (2018Q4): (m-1)%3 tags it Second; it was the shutdown 'Initial' estimate. s_corrections moves GdpAdvance 2018Q4 to 02-28 and cancels GdpSecond 2018Q4.
	// NOTE FomcMinutes 2020-04-08 (ref 2020-03-15) is flagged Unscheduled: minutes of an unscheduled meeting, known only at the instant.
	// NOTE FomcDecision 2020-03-18 (canceled in s_corrections) carries no Sep flag: the Fed's 2020 page lists it only as 'March 17-18 (cancelled)' with no SEP; it was a scheduled SEP meeting (EV-8.2 counts 2020 published SEP = 3). Canceled, so the flag has no runtime effect.
	// Coverage holes (EV-4.6, s_coverageHoles): 80 refused spans; a day inside one is Unknown for that kind (EV-4.6b).
	public static partial class EventRules
	{
		static readonly long[] s_yyyymmddhhmm = {
			201601080830, 201601121000, 201601150830, 201601150830, 201601200830, 201601271400, 201601290830, 201602050830,
			201602091000, 201602120830, 201602170830, 201602171400, 201602190830, 201602260830, 201603040830, 201603150830,
			201603150830, 201603160830, 201603161400, 201603161430, 201603171000, 201603250830, 201603280830, 201604010830,
			201604051000, 201604061400, 201604130830, 201604130830, 201604140830, 201604271400, 201604280830, 201604290830,
			201605060830, 201605101000, 201605130830, 201605130830, 201605170830, 201605181400, 201605270830, 201605310830,
			201606030830, 201606081000, 201606140830, 201606150830, 201606151400, 201606151430, 201606160830, 201606280830,
			201606290830, 201607061400, 201607080830, 201607121000, 201607140830, 201607150830, 201607150830, 201607271400,
			201607290830, 201608050830, 201608101000, 201608120830, 201608120830, 201608160830, 201608171400, 201609020830,
			201609071000, 201609150830, 201609150830, 201609160830, 201609211400, 201609211430, 201609300830, 201610070830,
			201610121000, 201610121400, 201610140830, 201610140830, 201610180830, 201610280830, 201610310830, 201611021400,
			201611040830, 201611081000, 201611150830, 201611160830, 201611170830, 201611231400, 201611300830, 201612020830,
			201612071000, 201612140830, 201612140830, 201612141400, 201612141430, 201612150830, 201612220830, 201701041400,
			201701060830, 201701101000, 201701130830, 201701130830, 201701180830, 201701270830, 201701300830, 201702011400,
			201702030830, 201702071000, 201702140830, 201702150830, 201702150830, 201702221400, 201702280830, 201703100830,
			201703140830, 201703150830, 201703150830, 201703151400, 201703151430, 201703161000, 201703300830, 201704051400,
			201704070830, 201704111000, 201704130830, 201704140830, 201704140830, 201704280830, 201705031400, 201705050830,
			201705091000, 201705110830, 201705120830, 201705120830, 201705241400, 201705260830, 201706020830, 201706061000,
			201706130830, 201706140830, 201706140830, 201706141400, 201706141430, 201706290830, 201706300830, 201707051400,
			201707070830, 201707111000, 201707130830, 201707140830, 201707140830, 201707261400, 201707280830, 201708040830,
			201708081000, 201708100830, 201708110830, 201708150830, 201708161400, 201709010830, 201709121000, 201709130830,
			201709140830, 201709150830, 201709201400, 201709201430, 201709280830, 201709290830, 201710060830, 201710111000,
			201710111400, 201710120830, 201710130830, 201710130830, 201710300830, 201711011400, 201711030830, 201711071000,
			201711140830, 201711150830, 201711150830, 201711221400, 201711300830, 201712080830, 201712111000, 201712120830,
			201712130830, 201712131400, 201712131430, 201712140830, 201712210830, 201712220830, 201801031400, 201801050830,
			201801091000, 201801110830, 201801120830, 201801120830, 201801260830, 201801290830, 201801311400, 201802020830,
			201802061000, 201802140830, 201802140830, 201802150830, 201802211400, 201802280830, 201803090830, 201803130830,
			201803140830, 201803140830, 201803161000, 201803211400, 201803211430, 201803280830, 201804060830, 201804100830,
			201804110830, 201804111400, 201804131000, 201804160830, 201804270830, 201804300830, 201805021400, 201805040830,
			201805081000, 201805090830, 201805100830, 201805150830, 201805231400, 201805300830, 201805310830, 201806010830,
			201806051000, 201806120830, 201806130830, 201806131400, 201806131430, 201806140830, 201806280830, 201806290830,
			201807051400, 201807060830, 201807101000, 201807110830, 201807120830, 201807160830, 201808011400, 201808030830,
			201808071000, 201808090830, 201808100830, 201808150830, 201808221400, 201808290830, 201808300830, 201809070830,
			201809111000, 201809120830, 201809130830, 201809140830, 201809261400, 201809261430, 201809270830, 201809280830,
			201810050830, 201810100830, 201810110830, 201810150830, 201810161000, 201810171400, 201810260830, 201810290830,
			201811020830, 201811061000, 201811081400, 201811090830, 201811140830, 201811150830, 201811280830, 201811290830,
			201811291400, 201812070830, 201812101000, 201812110830, 201812120830, 201812140830, 201812191400, 201812191430,
			201812210830, 201901040830, 201901081000, 201901091400, 201901110830, 201901150830, 201901160830, 201901300830,
			201901301400, 201901301430, 201902010830, 201902121000, 201902130830, 201902140830, 201902201400, 201902280830,
			201903080830, 201903120830, 201903130830, 201903151000, 201903201400, 201903201430, 201903280830, 201904050830,
			201904091000, 201904100830, 201904101400, 201904110830, 201904260830, 201904290830, 201905011400, 201905011430,
			201905030830, 201905071000, 201905090830, 201905100830, 201905150830, 201905221400, 201905300830, 201905310830,
			201906070830, 201906101000, 201906110830, 201906120830, 201906140830, 201906191400, 201906191430, 201906270830,
			201906280830, 201907050830, 201907091000, 201907101400, 201907110830, 201907120830, 201907160830, 201907260830,
			201907300830, 201907311400, 201907311430, 201908020830, 201908061000, 201908090830, 201908130830, 201908150830,
			201908211400, 201908290830, 201908300830, 201909060830, 201909101000, 201909110830, 201909120830, 201909130830,
			201909181400, 201909181430, 201909260830, 201909270830, 201910040830, 201910080830, 201910091000, 201910091400,
			201910100830, 201910160830, 201910300830, 201910301400, 201910301430, 201910310830, 201911010830, 201911051000,
			201911130830, 201911140830, 201911150830, 201911201400, 201911270830, 201912060830, 201912110830, 201912111400,
			201912111430, 201912120830, 201912130830, 201912171000, 201912200830, 202001031400, 202001100830, 202001140830,
			202001150830, 202001160830, 202001171000, 202001291400, 202001291430, 202001300830, 202001310830, 202002070830,
			202002111000, 202002130830, 202002140830, 202002190830, 202002191400, 202002270830, 202002280830, 202003031000,
			202003060830, 202003110830, 202003120830, 202003151700, 202003170830, 202003171000, 202003181400, 202003181430,
			202003260830, 202003270830, 202004030830, 202004071000, 202004081400, 202004090830, 202004100830, 202004150830,
			202004290830, 202004291400, 202004291430, 202004300830, 202005080830, 202005120830, 202005130830, 202005150830,
			202005151000, 202005201400, 202005280830, 202005290830, 202006050830, 202006091000, 202006100830, 202006101400,
			202006101430, 202006110830, 202006160830, 202006250830, 202006260830, 202007011400, 202007020830, 202007071000,
			202007100830, 202007140830, 202007160830, 202007291400, 202007291430, 202007300830, 202007310830, 202008070830,
			202008101000, 202008110830, 202008120830, 202008140830, 202008191400, 202008270830, 202008280830, 202009040830,
			202009091000, 202009100830, 202009110830, 202009160830, 202009161400, 202009161430, 202010020830, 202010061000,
			202010071400, 202010130830, 202010140830, 202010160830, 202010290830, 202011051400, 202011051430, 202011060830,
			202011101000, 202011120830, 202011130830, 202011170830, 202011251400, 202012040830, 202012091000, 202012100830,
			202012110830, 202012160830, 202012161400, 202012161430, 202012220830, 202012230830, 202101061400, 202101080830,
			202101121000, 202101130830, 202101150830, 202101150830, 202101271400, 202101271430, 202101280830, 202101290830,
			202102050830, 202102091000, 202102100830, 202102170830, 202102170830, 202102171400, 202102250830, 202102260830,
			202103050830, 202103100830, 202103111000, 202103120830, 202103160830, 202103171400, 202103171430, 202103250830,
			202103260830, 202104020830, 202104061000, 202104071400, 202104090830, 202104130830, 202104150830, 202104281400,
			202104281430, 202104290830, 202104300830, 202105070830, 202105111000, 202105120830, 202105130830, 202105140830,
			202105191400, 202105270830, 202105280830, 202106040830, 202106081000, 202106100830, 202106150830, 202106150830,
			202106161400, 202106161430, 202106240830, 202106250830, 202107020830, 202107071000, 202107071400, 202107130830,
			202107140830, 202107160830, 202107281400, 202107281430, 202107290830, 202107300830, 202108060830, 202108091000,
			202108110830, 202108120830, 202108170830, 202108181400, 202108270830, 202109030830, 202109081000, 202109100830,
			202109140830, 202109160830, 202109221400, 202109221430, 202110080830, 202110121000, 202110130830, 202110131400,
			202110140830, 202110150830, 202111031400, 202111031430, 202111050830, 202111090830, 202111100830, 202111121000,
			202111160830, 202111240830, 202111241400, 202112030830, 202112081000, 202112100830, 202112140830, 202112150830,
			202112151400, 202112151430, 202112220830, 202112230830, 202201041000, 202201051400, 202201070830, 202201120830,
			202201130830, 202201140830, 202201261400, 202201261430, 202201270830, 202201280830, 202202011000, 202202040830,
			202202100830, 202202150830, 202202160830, 202202161400, 202202240830, 202202250830, 202203040830, 202203091000,
			202203100830, 202203150830, 202203160830, 202203161400, 202203161430, 202203291000, 202203300830, 202203310830,
			202204010830, 202204061400, 202204120830, 202204130830, 202204140830, 202204280830, 202204290830, 202205031000,
			202205041400, 202205041430, 202205060830, 202205110830, 202205120830, 202205170830, 202205251400, 202205260830,
			202205270830, 202206011000, 202206030830, 202206100830, 202206140830, 202206150830, 202206151400, 202206151430,
			202206290830, 202206300830, 202207061000, 202207061400, 202207080830, 202207130830, 202207140830, 202207150830,
			202207271400, 202207271430, 202207280830, 202207290830, 202208021000, 202208050830, 202208100830, 202208110830,
			202208170830, 202208171400, 202208250830, 202208260830, 202208301000, 202209020830, 202209130830, 202209140830,
			202209150830, 202209211400, 202209211430, 202209290830, 202209300830, 202210041000, 202210070830, 202210120830,
			202210121400, 202210130830, 202210140830, 202210280830, 202211011000, 202211021400, 202211021430, 202211040830,
			202211100830, 202211150830, 202211160830, 202211231400, 202211300830, 202211301000, 202212020830, 202212090830,
			202212130830, 202212141400, 202212141430, 202212150830, 202212220830, 202301041000, 202301041400, 202301060830,
			202301120830, 202301180830, 202301180830, 202301260830, 202301270830, 202302011000, 202302011400, 202302011430,
			202302030830, 202302140830, 202302150830, 202302160830, 202302221400, 202302230830, 202302240830, 202303081000,
			202303100830, 202303140830, 202303150830, 202303150830, 202303221400, 202303221430, 202303300830, 202303310830,
			202304041000, 202304070830, 202304120830, 202304121400, 202304130830, 202304140830, 202304270830, 202304280830,
			202305021000, 202305031400, 202305031430, 202305050830, 202305100830, 202305110830, 202305160830, 202305241400,
			202305250830, 202305260830, 202305311000, 202306020830, 202306130830, 202306140830, 202306141400, 202306141430,
			202306150830, 202306290830, 202306300830, 202307051400, 202307061000, 202307070830, 202307120830, 202307130830,
			202307180830, 202307261400, 202307261430, 202307270830, 202307280830, 202308011000, 202308040830, 202308100830,
			202308110830, 202308150830, 202308161400, 202308291000, 202308300830, 202308310830, 202309010830, 202309130830,
			202309140830, 202309140830, 202309201400, 202309201430, 202309280830, 202309290830, 202310031000, 202310060830,
			202310110830, 202310111400, 202310120830, 202310170830, 202310260830, 202310270830, 202311011000, 202311011400,
			202311011430, 202311030830, 202311140830, 202311150830, 202311150830, 202311211400, 202311300830, 202312051000,
			202312080830, 202312120830, 202312130830, 202312131400, 202312131430, 202312140830, 202312210830, 202312220830,
			202401031000, 202401031400, 202401050830, 202401110830, 202401120830, 202401170830, 202401250830, 202401260830,
			202401301000, 202401311400, 202401311430, 202402020830, 202402130830, 202402150830, 202402160830, 202402211400,
			202402280830, 202402290830, 202403061000, 202403080830, 202403120830, 202403140830, 202403140830, 202403201400,
			202403201430, 202403280830, 202403290830, 202404021000, 202404050830, 202404100830, 202404101400, 202404110830,
			202404150830, 202404250830, 202404260830, 202405011000, 202405011400, 202405011430, 202405030830, 202405140830,
			202405150830, 202405150830, 202405221400, 202405300830, 202405310830, 202406041000, 202406070830, 202406120830,
			202406121400, 202406121430, 202406130830, 202406180830, 202406270830, 202406280830, 202407021000, 202407031400,
			202407050830, 202407110830, 202407120830, 202407160830, 202407250830, 202407260830, 202407301000, 202407311400,
			202407311430, 202408020830, 202408130830, 202408140830, 202408150830, 202408211400, 202408290830, 202408300830,
			202409041000, 202409060830, 202409110830, 202409120830, 202409170830, 202409181400, 202409181430, 202409260830,
			202409270830, 202410011000, 202410040830, 202410091400, 202410100830, 202410110830, 202410170830, 202410291000,
			202410310830, 202411010830, 202411071400, 202411071430, 202411130830, 202411140830, 202411150830, 202411261400,
			202411270830, 202412031000, 202412060830, 202412110830, 202412120830, 202412170830, 202412181400, 202412181430,
			202412190830, 202412200830, 202501071000, 202501081400, 202501100830, 202501140830, 202501150830, 202501160830,
			202501291400, 202501291430, 202501300830, 202501310830, 202502041000, 202502070830, 202502120830, 202502130830,
			202502140830, 202502191400, 202502270830, 202502280830, 202503070830, 202503111000, 202503120830, 202503130830,
			202503170830, 202503191400, 202503191430, 202503270830, 202503280830, 202504011000, 202504040830, 202504091400,
			202504100830, 202504110830, 202504160830, 202504291000, 202504300830, 202504301000, 202505020830, 202505071400,
			202505071430, 202505130830, 202505150830, 202505150830, 202505281400, 202505290830, 202505300830, 202506031000,
			202506060830, 202506110830, 202506120830, 202506170830, 202506181400, 202506181430, 202506260830, 202506270830,
			202507011000, 202507030830, 202507091400, 202507150830, 202507160830, 202507170830, 202507291000, 202507300830,
			202507301400, 202507301430, 202507310830, 202508010830, 202508120830, 202508140830, 202508150830, 202508201400,
			202508280830, 202508290830, 202509031000, 202509050830, 202509100830, 202509110830, 202509160830, 202509171400,
			202509171430, 202509250830, 202509260830, 202509301000, 202510030830, 202510081400, 202510150830, 202510160830,
			202510291400, 202510291430, 202510300830, 202511041000, 202511070830, 202511130830, 202511140830, 202511191400,
			202512021000, 202512050830, 202512100830, 202512101400, 202512101430, 202512110830, 202512301400, 202601071000,
			202601090830, 202601130830, 202601140830, 202601281400, 202602051000, 202602060830, 202602110830, 202602120830,
			202602181400, 202602200830, 202602200830, 202603060830, 202603110830, 202603120830, 202603130830, 202603130830,
			202603131000, 202603181400, 202603181430, 202603311000, 202604030830, 202604081400, 202604090830, 202604090830,
			202604100830, 202604140830, 202604291400, 202604291430, 202604300830, 202604300830, 202605051000, 202605080830,
			202605120830, 202605130830, 202605140830, 202605201400, 202605280830, 202605280830, 202606021000, 202606050830,
			202606100830, 202606110830, 202606170830, 202606171400, 202606171430, 202606250830, 202606250830, 202606301000,
			202607020830, 202607081400, 202607140830, 202607150830, 202607160830, 202607291400, 202607291430, 202607300830,
			202607300830, 202608041000, 202608070830, 202608120830, 202608130830, 202608140830, 202608191400, 202608260830,
			202608260830, 202609011000, 202609040830, 202609100830, 202609110830, 202609160830, 202609161400, 202609161430,
			202609291000, 202609300830, 202609300830, 202610020830, 202610071400, 202610140830, 202610150830, 202610150830,
			202610281400, 202610290830, 202610290830, 202611031000, 202611060830, 202611100830, 202611130830, 202611170830,
			202611181400, 202611250830, 202611250830, 202612011000, 202612040830, 202612091400, 202612100830, 202612150830,
			202612160830, 202612230830, 202612230830, 202612301400, 202701080830, 202701130830, 202701140830, 202701150830,
			202701271400, 202701280830, 202701280830, 202702050830, 202702110830, 202702120830, 202702170830, 202702171400,
			202702250830, 202702250830, 202703050830, 202703100830, 202703120830, 202703160830, 202703171400, 202703310830,
			202703310830, 202704020830, 202704071400, 202704130830, 202704140830, 202704160830, 202704281400, 202704290830,
			202704290830, 202705070830, 202705120830, 202705130830, 202705140830, 202705191400, 202705270830, 202705270830,
			202706040830, 202706091400, 202706100830, 202706110830, 202706160830, 202706250830, 202706250830, 202706301400,
			202707020830, 202707140830, 202707150830, 202707160830, 202707281400, 202707290830, 202707290830, 202708060830,
			202708100830, 202708110830, 202708160830, 202708181400, 202708250830, 202708250830, 202709030830, 202709100830,
			202709140830, 202709151400, 202709160830, 202709300830, 202709300830, 202710061400, 202710080830, 202710140830,
			202710150830, 202710150830, 202710271400, 202710280830, 202710280830, 202711050830, 202711100830, 202711120830,
			202711160830, 202711171400, 202711240830, 202711240830, 202712030830, 202712081400, 202712081430, 202712100830,
			202712150830, 202712150830, 202712220830, 202712220830, 202712291400,
		};
		static readonly byte[] s_kind = {
			6, 7, 5, 13, 4, 1, 10, 6, 7, 13, 5, 3, 4, 11, 6, 5, 13, 4, 1, 2, 7, 12, 9, 6,
			7, 3, 5, 13, 4, 1, 10, 9, 6, 7, 5, 13, 4, 3, 11, 9, 6, 7, 13, 5, 1, 2, 4, 12,
			9, 3, 6, 7, 5, 4, 13, 1, 10, 6, 7, 5, 13, 4, 3, 6, 7, 5, 13, 4, 1, 2, 9, 6,
			7, 3, 5, 13, 4, 10, 9, 1, 6, 7, 13, 5, 4, 3, 9, 6, 7, 5, 13, 1, 2, 4, 12, 3,
			6, 7, 5, 13, 4, 10, 9, 1, 6, 7, 5, 4, 13, 3, 11, 6, 5, 4, 13, 1, 2, 7, 12, 3,
			6, 7, 5, 4, 13, 10, 1, 6, 7, 5, 4, 13, 3, 11, 6, 7, 5, 4, 13, 1, 2, 12, 9, 3,
			6, 7, 5, 4, 13, 1, 10, 6, 7, 5, 4, 13, 3, 6, 7, 5, 4, 13, 1, 2, 12, 9, 6, 7,
			3, 5, 4, 13, 9, 1, 6, 7, 5, 4, 13, 3, 9, 6, 7, 5, 4, 1, 2, 13, 12, 9, 3, 6,
			7, 5, 4, 13, 10, 9, 1, 6, 7, 4, 13, 5, 3, 11, 6, 4, 5, 13, 7, 1, 2, 12, 6, 5,
			4, 3, 7, 13, 10, 9, 1, 6, 7, 5, 4, 13, 3, 11, 9, 6, 7, 4, 5, 1, 2, 13, 12, 9,
			3, 6, 7, 5, 4, 13, 1, 6, 7, 5, 4, 13, 3, 11, 9, 6, 7, 5, 4, 13, 1, 2, 12, 9,
			6, 5, 4, 13, 7, 3, 10, 9, 6, 7, 1, 5, 4, 13, 11, 9, 3, 6, 7, 5, 4, 13, 1, 2,
			12, 6, 7, 3, 4, 5, 13, 10, 1, 2, 6, 7, 4, 5, 3, 11, 6, 4, 5, 7, 1, 2, 12, 6,
			7, 4, 3, 5, 10, 9, 1, 2, 6, 7, 5, 4, 13, 3, 11, 9, 6, 7, 5, 4, 13, 1, 2, 12,
			9, 6, 7, 3, 4, 5, 13, 10, 9, 1, 2, 6, 7, 5, 4, 13, 3, 11, 9, 6, 7, 5, 4, 13,
			1, 2, 12, 9, 6, 5, 7, 3, 4, 13, 10, 1, 2, 9, 6, 7, 4, 5, 13, 3, 11, 6, 4, 1,
			2, 5, 13, 7, 12, 3, 6, 4, 5, 13, 7, 1, 2, 10, 9, 6, 7, 4, 13, 5, 3, 11, 9, 1,
			6, 4, 5, 1, 13, 7, 1, 2, 12, 9, 6, 7, 3, 5, 4, 13, 10, 1, 2, 9, 6, 4, 5, 13,
			7, 3, 11, 9, 6, 7, 4, 1, 2, 5, 13, 12, 9, 3, 6, 7, 5, 4, 13, 1, 2, 10, 9, 6,
			7, 5, 4, 13, 3, 11, 9, 6, 7, 5, 4, 13, 1, 2, 6, 7, 3, 4, 5, 13, 10, 1, 2, 6,
			7, 4, 5, 13, 3, 6, 7, 4, 5, 13, 1, 2, 12, 9, 3, 6, 7, 4, 5, 13, 1, 2, 10, 9,
			6, 7, 4, 5, 13, 3, 11, 9, 6, 4, 7, 5, 13, 1, 2, 12, 9, 6, 7, 3, 5, 4, 13, 1,
			2, 10, 9, 6, 7, 4, 5, 13, 3, 11, 9, 6, 7, 4, 5, 13, 1, 2, 12, 9, 6, 7, 3, 4,
			5, 13, 1, 2, 10, 9, 6, 7, 4, 5, 13, 3, 9, 6, 7, 5, 4, 13, 1, 2, 6, 7, 4, 3,
			5, 13, 1, 2, 6, 5, 4, 7, 13, 11, 3, 6, 7, 4, 5, 13, 1, 2, 12, 9, 7, 3, 6, 4,
			5, 13, 1, 2, 10, 9, 7, 6, 4, 5, 13, 3, 11, 9, 6, 7, 4, 5, 13, 1, 2, 7, 12, 9,
			6, 3, 4, 5, 13, 10, 9, 7, 1, 2, 6, 4, 5, 13, 3, 11, 9, 7, 6, 4, 5, 13, 1, 2,
			12, 9, 7, 3, 6, 4, 5, 13, 1, 2, 10, 9, 7, 6, 4, 5, 13, 3, 11, 9, 7, 6, 4, 5,
			13, 1, 2, 12, 9, 7, 6, 5, 3, 4, 13, 9, 7, 1, 2, 6, 4, 5, 13, 3, 11, 7, 6, 5,
			4, 1, 2, 13, 12, 7, 3, 6, 4, 5, 13, 10, 9, 7, 1, 2, 6, 4, 13, 5, 3, 11, 9, 7,
			6, 4, 5, 13, 1, 2, 12, 9, 7, 6, 4, 3, 5, 13, 10, 9, 7, 1, 2, 6, 4, 5, 13, 3,
			11, 9, 7, 6, 4, 5, 1, 2, 13, 12, 9, 3, 7, 6, 4, 5, 13, 1, 2, 10, 9, 7, 6, 4,
			5, 13, 3, 7, 11, 9, 6, 4, 5, 13, 1, 2, 12, 9, 7, 6, 5, 3, 4, 13, 10, 9, 7, 1,
			2, 6, 4, 5, 13, 3, 9, 7, 6, 4, 5, 1, 2, 13, 12, 9, 7, 3, 6, 4, 5, 13, 10, 9,
			7, 1, 2, 6, 4, 13, 5, 3, 11, 9, 7, 6, 4, 5, 13, 1, 2, 12, 9, 7, 6, 4, 3, 5,
			13, 10, 9, 7, 1, 2, 6, 5, 4, 13, 3, 11, 9, 7, 6, 4, 1, 2, 5, 13, 12, 9, 7, 3,
			6, 4, 5, 13, 10, 9, 7, 1, 2, 6, 5, 4, 13, 3, 11, 9, 7, 6, 4, 5, 13, 1, 2, 12,
			9, 7, 6, 3, 4, 5, 13, 7, 9, 6, 1, 2, 4, 5, 13, 3, 11, 7, 6, 4, 5, 13, 1, 2,
			12, 9, 7, 3, 6, 5, 4, 13, 1, 2, 10, 9, 7, 6, 4, 5, 13, 3, 11, 9, 6, 7, 4, 5,
			13, 1, 2, 12, 9, 7, 6, 3, 4, 5, 13, 7, 10, 9, 6, 1, 2, 4, 5, 13, 3, 11, 9, 7,
			6, 4, 5, 13, 1, 2, 12, 9, 7, 6, 3, 4, 5, 13, 7, 10, 1, 2, 9, 6, 4, 5, 13, 3,
			11, 9, 7, 6, 5, 4, 13, 1, 2, 12, 9, 7, 6, 3, 4, 5, 1, 2, 10, 7, 6, 4, 5, 3,
			7, 6, 4, 1, 2, 5, 3, 7, 6, 4, 5, 1, 7, 6, 4, 5, 3, 9, 10, 6, 4, 5, 9, 11,
			7, 1, 2, 7, 6, 3, 9, 12, 4, 5, 1, 2, 9, 10, 7, 6, 4, 5, 13, 3, 9, 11, 7, 6,
			4, 5, 13, 1, 2, 9, 12, 7, 6, 3, 4, 5, 13, 1, 2, 9, 10, 7, 6, 4, 5, 13, 3, 9,
			11, 7, 6, 5, 4, 13, 1, 2, 7, 9, 12, 6, 3, 4, 5, 13, 1, 9, 10, 7, 6, 4, 5, 13,
			3, 9, 11, 7, 6, 1, 4, 5, 13, 9, 12, 3, 6, 4, 5, 13, 1, 9, 10, 6, 4, 5, 13, 3,
			9, 11, 6, 4, 5, 13, 1, 9, 12, 6, 3, 4, 5, 13, 1, 9, 10, 6, 4, 5, 13, 3, 9, 11,
			6, 1, 4, 5, 13, 9, 12, 3, 6, 4, 5, 13, 1, 9, 10, 6, 5, 4, 13, 3, 9, 11, 6, 5,
			4, 1, 13, 9, 12, 3, 6, 4, 5, 13, 1, 9, 10, 6, 4, 5, 13, 3, 9, 11, 6, 1, 2, 4,
			5, 13, 9, 12, 3,
		};
		static readonly ushort[] s_flags = {
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
			0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 0,
		};
		static readonly string[] s_ref = {
			"2015-12", "2015-11", "2015-12", "2015-12", "2015-12", "2016-01-27", "2015Q4", "2016-01",
			"2015-12", "2016-01", "2016-01", "2016-01-27", "2016-01", "2015Q4", "2016-02", "2016-02",
			"2016-02", "2016-02", "2016-03-16", "2016-03-16", "2016-01", "2015Q4", "2016-02", "2016-03",
			"2016-02", "2016-03-16", "2016-03", "2016-03", "2016-03", "2016-04-27", "2016Q1", "2016-03",
			"2016-04", "2016-03", "2016-04", "2016-04", "2016-04", "2016-04-27", "2016Q1", "2016-04",
			"2016-05", "2016-04", "2016-05", "2016-05", "2016-06-15", "2016-06-15", "2016-05", "2016Q1",
			"2016-05", "2016-06-15", "2016-06", "2016-05", "2016-06", "2016-06", "2016-06", "2016-07-27",
			"2016Q2", "2016-07", "2016-06", "2016-07", "2016-07", "2016-07", "2016-07-27", "2016-08",
			"2016-07", "2016-08", "2016-08", "2016-08", "2016-09-21", "2016-09-21", "2016-08", "2016-09",
			"2016-08", "2016-09-21", "2016-09", "2016-09", "2016-09", "2016Q3", "2016-09", "2016-11-02",
			"2016-10", "2016-09", "2016-10", "2016-10", "2016-10", "2016-11-02", "2016-10", "2016-11",
			"2016-10", "2016-11", "2016-11", "2016-12-14", "2016-12-14", "2016-11", "2016Q3", "2016-12-14",
			"2016-12", "2016-11", "2016-12", "2016-12", "2016-12", "2016Q4", "2016-12", "2017-02-01",
			"2017-01", "2016-12", "2017-01", "2017-01", "2017-01", "2017-02-01", "2016Q4", "2017-02",
			"2017-02", "2017-02", "2017-02", "2017-03-15", "2017-03-15", "2017-01", "2016Q4", "2017-03-15",
			"2017-03", "2017-02", "2017-03", "2017-03", "2017-03", "2017Q1", "2017-05-03", "2017-04",
			"2017-03", "2017-04", "2017-04", "2017-04", "2017-05-03", "2017Q1", "2017-05", "2017-04",
			"2017-05", "2017-05", "2017-05", "2017-06-14", "2017-06-14", "2017Q1", "2017-05", "2017-06-14",
			"2017-06", "2017-05", "2017-06", "2017-06", "2017-06", "2017-07-26", "2017Q2", "2017-07",
			"2017-06", "2017-07", "2017-07", "2017-07", "2017-07-26", "2017-08", "2017-07", "2017-08",
			"2017-08", "2017-08", "2017-09-20", "2017-09-20", "2017Q2", "2017-08", "2017-09", "2017-08",
			"2017-09-20", "2017-09", "2017-09", "2017-09", "2017-09", "2017-11-01", "2017-10", "2017-09",
			"2017-10", "2017-10", "2017-10", "2017-11-01", "2017-10", "2017-11", "2017-10", "2017-11",
			"2017-11", "2017-12-13", "2017-12-13", "2017-11", "2017Q3", "2017-11", "2017-12-13", "2017-12",
			"2017-11", "2017-12", "2017-12", "2017-12", "2017Q4", "2017-12", "2018-01-31", "2018-01",
			"2017-12", "2018-01", "2018-01", "2018-01", "2018-01-31", "2017Q4", "2018-02", "2018-02",
			"2018-02", "2018-02", "2018-01", "2018-03-21", "2018-03-21", "2017Q4", "2018-03", "2018-03",
			"2018-03", "2018-03-21", "2018-02", "2018-03", "2018Q1", "2018-03", "2018-05-02", "2018-04",
			"2018-03", "2018-04", "2018-04", "2018-04", "2018-05-02", "2018Q1", "2018-04", "2018-05",
			"2018-04", "2018-05", "2018-05", "2018-06-13", "2018-06-13", "2018-05", "2018Q1", "2018-05",
			"2018-06-13", "2018-06", "2018-05", "2018-06", "2018-06", "2018-06", "2018-08-01", "2018-07",
			"2018-06", "2018-07", "2018-07", "2018-07", "2018-08-01", "2018Q2", "2018-07", "2018-08",
			"2018-07", "2018-08", "2018-08", "2018-08", "2018-09-26", "2018-09-26", "2018Q2", "2018-08",
			"2018-09", "2018-09", "2018-09", "2018-09", "2018-08", "2018-09-26", "2018Q3", "2018-09",
			"2018-10", "2018-09", "2018-11-08", "2018-10", "2018-10", "2018-10", "2018Q3", "2018-10",
			"2018-11-08", "2018-11", "2018-10", "2018-11", "2018-11", "2018-11", "2018-12-19", "2018-12-19",
			"2018Q3", "2018-12", "2018-11", "2018-12-19", "2018-12", "2018-12", "2018-12", "2018Q4",
			"2019-01-30", "2019-01-30", "2019-01", "2018-12", "2019-01", "2019-01", "2019-01-30", "2018Q4",
			"2019-02", "2019-02", "2019-02", "2019-01", "2019-03-20", "2019-03-20", "2018Q4", "2019-03",
			"2019-02", "2019-03", "2019-03-20", "2019-03", "2019Q1", "2019-03", "2019-05-01", "2019-05-01",
			"2019-04", "2019-03", "2019-04", "2019-04", "2019-04", "2019-05-01", "2019Q1", "2019-04",
			"2019-05", "2019-04", "2019-05", "2019-05", "2019-05", "2019-06-19", "2019-06-19", "2019Q1",
			"2019-05", "2019-06", "2019-05", "2019-06-19", "2019-06", "2019-06", "2019-06", "2019Q2",
			"2019-06", "2019-07-31", "2019-07-31", "2019-07", "2019-06", "2019-07", "2019-07", "2019-07",
			"2019-07-31", "2019Q2", "2019-07", "2019-08", "2019-07", "2019-08", "2019-08", "2019-08",
			"2019-09-18", "2019-09-18", "2019Q2", "2019-08", "2019-09", "2019-09", "2019-08", "2019-09-18",
			"2019-09", "2019-09", "2019Q3", "2019-10-30", "2019-10-30", "2019-09", "2019-10", "2019-09",
			"2019-10", "2019-10", "2019-10", "2019-10-30", "2019Q3", "2019-11", "2019-11", "2019-12-11",
			"2019-12-11", "2019-11", "2019-11", "2019-10", "2019Q3", "2019-12-11", "2019-12", "2019-12",
			"2019-12", "2019-12", "2019-11", "2020-01-29", "2020-01-29", "2019Q4", "2019-12", "2020-01",
			"2019-12", "2020-01", "2020-01", "2020-01", "2020-01-29", "2019Q4", "2020-01", "2020-03-03",
			"2020-02", "2020-02", "2020-02", "2020-03-15", "2020-02", "2020-01", "2020-03-18", "2020-03-18",
			"2019Q4", "2020-02", "2020-03", "2020-02", "2020-03-15", "2020-03", "2020-03", "2020-03",
			"2020Q1", "2020-04-29", "2020-04-29", "2020-03", "2020-04", "2020-04", "2020-04", "2020-04",
			"2020-03", "2020-04-29", "2020Q1", "2020-04", "2020-05", "2020-04", "2020-05", "2020-06-10",
			"2020-06-10", "2020-05", "2020-05", "2020Q1", "2020-05", "2020-06-10", "2020-06", "2020-05",
			"2020-06", "2020-06", "2020-06", "2020-07-29", "2020-07-29", "2020Q2", "2020-06", "2020-07",
			"2020-06", "2020-07", "2020-07", "2020-07", "2020-07-29", "2020Q2", "2020-07", "2020-08",
			"2020-07", "2020-08", "2020-08", "2020-08", "2020-09-16", "2020-09-16", "2020-09", "2020-08",
			"2020-09-16", "2020-09", "2020-09", "2020-09", "2020Q3", "2020-11-05", "2020-11-05", "2020-10",
			"2020-09", "2020-10", "2020-10", "2020-10", "2020-11-05", "2020-11", "2020-10", "2020-11",
			"2020-11", "2020-11", "2020-12-16", "2020-12-16", "2020Q3", "2020-11", "2020-12-16", "2020-12",
			"2020-11", "2020-12", "2020-12", "2020-12", "2021-01-27", "2021-01-27", "2020Q4", "2020-12",
			"2021-01", "2020-12", "2021-01", "2021-01", "2021-01", "2021-01-27", "2020Q4", "2021-01",
			"2021-02", "2021-02", "2021-01", "2021-02", "2021-02", "2021-03-17", "2021-03-17", "2020Q4",
			"2021-02", "2021-03", "2021-02", "2021-03-17", "2021-03", "2021-03", "2021-03", "2021-04-28",
			"2021-04-28", "2021Q1", "2021-03", "2021-04", "2021-03", "2021-04", "2021-04", "2021-04",
			"2021-04-28", "2021Q1", "2021-04", "2021-05", "2021-04", "2021-05", "2021-05", "2021-05",
			"2021-06-16", "2021-06-16", "2021Q1", "2021-05", "2021-06", "2021-05", "2021-06-16", "2021-06",
			"2021-06", "2021-06", "2021-07-28", "2021-07-28", "2021Q2", "2021-06", "2021-07", "2021-06",
			"2021-07", "2021-07", "2021-07", "2021-07-28", "2021-07", "2021-08", "2021-07", "2021-08",
			"2021-08", "2021-08", "2021-09-22", "2021-09-22", "2021-09", "2021-08", "2021-09", "2021-09-22",
			"2021-09", "2021-09", "2021-11-03", "2021-11-03", "2021-10", "2021-10", "2021-10", "2021-09",
			"2021-10", "2021Q3", "2021-11-03", "2021-11", "2021-10", "2021-11", "2021-11", "2021-11",
			"2021-12-15", "2021-12-15", "2021Q3", "2021-11", "2021-11", "2021-12-15", "2021-12", "2021-12",
			"2021-12", "2021-12", "2022-01-26", "2022-01-26", "2021Q4", "2021-12", "2021-12", "2022-01",
			"2022-01", "2022-01", "2022-01", "2022-01-26", "2021Q4", "2022-01", "2022-02", "2022-01",
			"2022-02", "2022-02", "2022-02", "2022-03-16", "2022-03-16", "2022-02", "2021Q4", "2022-02",
			"2022-03", "2022-03-16", "2022-03", "2022-03", "2022-03", "2022Q1", "2022-03", "2022-03",
			"2022-05-04", "2022-05-04", "2022-04", "2022-04", "2022-04", "2022-04", "2022-05-04", "2022Q1",
			"2022-04", "2022-04", "2022-05", "2022-05", "2022-05", "2022-05", "2022-06-15", "2022-06-15",
			"2022Q1", "2022-05", "2022-05", "2022-06-15", "2022-06", "2022-06", "2022-06", "2022-06",
			"2022-07-27", "2022-07-27", "2022Q2", "2022-06", "2022-06", "2022-07", "2022-07", "2022-07",
			"2022-07", "2022-07-27", "2022Q2", "2022-07", "2022-07", "2022-08", "2022-08", "2022-08",
			"2022-08", "2022-09-21", "2022-09-21", "2022Q2", "2022-08", "2022-08", "2022-09", "2022-09",
			"2022-09-21", "2022-09", "2022-09", "2022-09", "2022-09", "2022-11-02", "2022-11-02", "2022-10",
			"2022-10", "2022-10", "2022-10", "2022-11-02", "2022Q3", "2022-10", "2022-11", "2022-11",
			"2022-11", "2022-12-14", "2022-12-14", "2022-11", "2022Q3", "2022-11", "2022-12-14", "2022-12",
			"2022-12", "2022-12", "2022-12", "2022Q4", "2022-12", "2022-12", "2023-02-01", "2023-02-01",
			"2023-01", "2023-01", "2023-01", "2023-01", "2023-02-01", "2022Q4", "2023-01", "2023-01",
			"2023-02", "2023-02", "2023-02", "2023-02", "2023-03-22", "2023-03-22", "2022Q4", "2023-02",
			"2023-02", "2023-03", "2023-03", "2023-03-22", "2023-03", "2023-03", "2023Q1", "2023-03",
			"2023-03", "2023-05-03", "2023-05-03", "2023-04", "2023-04", "2023-04", "2023-04", "2023-05-03",
			"2023Q1", "2023-04", "2023-04", "2023-05", "2023-05", "2023-05", "2023-06-14", "2023-06-14",
			"2023-05", "2023Q1", "2023-05", "2023-06-14", "2023-05", "2023-06", "2023-06", "2023-06",
			"2023-06", "2023-07-26", "2023-07-26", "2023Q2", "2023-06", "2023-06", "2023-07", "2023-07",
			"2023-07", "2023-07", "2023-07-26", "2023-07", "2023Q2", "2023-07", "2023-08", "2023-08",
			"2023-08", "2023-08", "2023-09-20", "2023-09-20", "2023Q2", "2023-08", "2023-08", "2023-09",
			"2023-09", "2023-09-20", "2023-09", "2023-09", "2023Q3", "2023-09", "2023-09", "2023-11-01",
			"2023-11-01", "2023-10", "2023-10", "2023-10", "2023-10", "2023-11-01", "2023-10", "2023-10",
			"2023-11", "2023-11", "2023-11", "2023-12-13", "2023-12-13", "2023-11", "2023Q3", "2023-11",
			"2023-11", "2023-12-13", "2023-12", "2023-12", "2023-12", "2023-12", "2023Q4", "2023-12",
			"2023-12", "2024-01-31", "2024-01-31", "2024-01", "2024-01", "2024-01", "2024-01", "2024-01-31",
			"2023Q4", "2024-01", "2024-01", "2024-02", "2024-02", "2024-02", "2024-02", "2024-03-20",
			"2024-03-20", "2023Q4", "2024-02", "2024-02", "2024-03", "2024-03", "2024-03-20", "2024-03",
			"2024-03", "2024Q1", "2024-03", "2024-03", "2024-05-01", "2024-05-01", "2024-04", "2024-04",
			"2024-04", "2024-04", "2024-05-01", "2024Q1", "2024-04", "2024-04", "2024-05", "2024-05",
			"2024-06-12", "2024-06-12", "2024-05", "2024-05", "2024Q1", "2024-05", "2024-05", "2024-06-12",
			"2024-06", "2024-06", "2024-06", "2024-06", "2024Q2", "2024-06", "2024-06", "2024-07-31",
			"2024-07-31", "2024-07", "2024-07", "2024-07", "2024-07", "2024-07-31", "2024Q2", "2024-07",
			"2024-07", "2024-08", "2024-08", "2024-08", "2024-08", "2024-09-18", "2024-09-18", "2024Q2",
			"2024-08", "2024-08", "2024-09", "2024-09-18", "2024-09", "2024-09", "2024-09", "2024-09",
			"2024-09", "2024-10", "2024-11-07", "2024-11-07", "2024-10", "2024-10", "2024-10", "2024-11-07",
			"2024Q3", "2024-10", "2024-11", "2024-11", "2024-11", "2024-11", "2024-12-18", "2024-12-18",
			"2024Q3", "2024-11", "2024-11", "2024-12-18", "2024-12", "2024-12", "2024-12", "2024-12",
			"2025-01-29", "2025-01-29", "2024Q4", "2024-12", "2024-12", "2025-01", "2025-01", "2025-01",
			"2025-01", "2025-01-29", "2024Q4", "2025-01", "2025-02", "2025-01", "2025-02", "2025-02",
			"2025-02", "2025-03-19", "2025-03-19", "2024Q4", "2025-02", "2025-02", "2025-03", "2025-03-19",
			"2025-03", "2025-03", "2025-03", "2025-03", "2025Q1", "2025-03", "2025-04", "2025-05-07",
			"2025-05-07", "2025-04", "2025-04", "2025-04", "2025-05-07", "2025Q1", "2025-04", "2025-04",
			"2025-05", "2025-05", "2025-05", "2025-05", "2025-06-18", "2025-06-18", "2025Q1", "2025-05",
			"2025-05", "2025-06", "2025-06-18", "2025-06", "2025-06", "2025-06", "2025-06", "2025Q2",
			"2025-07-30", "2025-07-30", "2025-06", "2025-07", "2025-07", "2025-07", "2025-07", "2025-07-30",
			"2025Q2", "2025-07", "2025-07", "2025-08", "2025-08", "2025-08", "2025-08", "2025-09-17",
			"2025-09-17", "2025Q2", "2025-08", "2025-08", "2025-09", "2025-09-17", "2025-09", "2025-09",
			"2025-10-29", "2025-10-29", "2025Q3", "2025-09", "2025-10", "2025-10", "2025-10", "2025-10-29",
			"2025-10", "2025-11", "2025-11", "2025-12-10", "2025-12-10", "2025-11", "2025-12-10", "2025-11",
			"2025-12", "2025-12", "2025-12", "2026-01-28", "2025-12", "2026-01", "2026-01", "2026-01",
			"2026-01-28", "2025-12", "2025Q4", "2026-02", "2026-02", "2026-02", "2026-01", "2025Q4",
			"2026-01", "2026-03-18", "2026-03-18", "2026-02", "2026-03", "2026-03-18", "2026-02", "2025Q4",
			"2026-03", "2026-03", "2026-04-29", "2026-04-29", "2026-03", "2026Q1", "2026-03", "2026-04",
			"2026-04", "2026-04", "2026-04", "2026-04-29", "2026-04", "2026Q1", "2026-04", "2026-05",
			"2026-05", "2026-05", "2026-05", "2026-06-17", "2026-06-17", "2026-05", "2026Q1", "2026-05",
			"2026-06", "2026-06-17", "2026-06", "2026-06", "2026-06", "2026-07-29", "2026-07-29", "2026-06",
			"2026Q2", "2026-06", "2026-07", "2026-07", "2026-07", "2026-07", "2026-07-29", "2026-07",
			"2026Q2", "2026-07", "2026-08", "2026-08", "2026-08", "2026-08", "2026-09-16", "2026-09-16",
			"2026-08", "2026-08", "2026Q2", "2026-09", "2026-09-16", "2026-09", "2026-09", "2026-09",
			"2026-10-28", "2026-09", "2026Q3", "2026-09", "2026-10", "2026-10", "2026-10", "2026-10",
			"2026-10-28", "2026-10", "2026Q3", "2026-10", "2026-11", "2026-12-09", "2026-11", "2026-11",
			"2026-11", "2026-11", "2026Q3", "2026-12-09", "2026-12", "2026-12", "2026-12", "2026-12",
			"2027-01-27", "2026-12", "2026Q4", "2027-01", "2027-01", "2027-01", "2027-01", "2027-01-27",
			"2027-01", "2026Q4", "2027-02", "2027-02", "2027-02", "2027-02", "2027-03-17", "2027-02",
			"2026Q4", "2027-03", "2027-03-17", "2027-03", "2027-03", "2027-03", "2027-04-28", "2027-03",
			"2027Q1", "2027-04", "2027-04", "2027-04", "2027-04", "2027-04-28", "2027-04", "2027Q1",
			"2027-05", "2027-06-09", "2027-05", "2027-05", "2027-05", "2027-05", "2027Q1", "2027-06-09",
			"2027-06", "2027-06", "2027-06", "2027-06", "2027-07-28", "2027-06", "2027Q2", "2027-07",
			"2027-07", "2027-07", "2027-07", "2027-07-28", "2027-07", "2027Q2", "2027-08", "2027-08",
			"2027-08", "2027-09-15", "2027-08", "2027-08", "2027Q2", "2027-09-15", "2027-09", "2027-09",
			"2027-09", "2027-09", "2027-10-27", "2027-09", "2027Q3", "2027-10", "2027-10", "2027-10",
			"2027-10", "2027-10-27", "2027-10", "2027Q3", "2027-11", "2027-12-08", "2027-12-08", "2027-11",
			"2027-11", "2027-11", "2027-11", "2027Q3", "2027-12-08",
		};
		/// <summary>EV-4.6 coverage holes from refused items: kind, first ET minute, last ET minute (yyyyMMddHHmm).</summary>
		static readonly long[] s_coverageHoles = {
			9, 201602010000, 201602292359, 9, 201608010000, 201608312359, 10, 201608010000, 201608312359, 11, 201608010000, 201608312359,
			12, 201608010000, 201608312359, 10, 201609010000, 201609302359, 11, 201609010000, 201609302359, 12, 201609010000, 201609302359,
			10, 201611010000, 201611302359, 11, 201611010000, 201611302359, 12, 201611010000, 201611302359, 9, 201612220000, 201612222359,
			9, 201703010000, 201703312359, 9, 201705010000, 201705312359, 9, 201708010000, 201708312359, 10, 201708010000, 201708312359,
			11, 201708010000, 201708312359, 12, 201708010000, 201708312359, 10, 201710010000, 201710312359, 11, 201710010000, 201710312359,
			12, 201710010000, 201710312359, 10, 201711010000, 201711302359, 11, 201711010000, 201711302359, 12, 201711010000, 201711302359,
			9, 201803010000, 201803312359, 10, 201807010000, 201807312359, 11, 201807010000, 201807312359, 12, 201807010000, 201807312359,
			9, 201807310000, 201807312359, 9, 201812210000, 201812212359, 11, 201901010000, 201901312359, 12, 201901010000, 201901312359,
			9, 201903010000, 201903312359, 1, 201910110000, 201910112359, 9, 201911270000, 201911272359, 9, 201912200000, 201912202359,
			2, 202003030000, 202003032359, 2, 202003150000, 202003152359, 10, 202009010000, 202009302359, 11, 202009010000, 202009302359,
			12, 202009010000, 202009302359, 9, 202010010000, 202010312359, 10, 202011010000, 202011302359, 11, 202011010000, 202011302359,
			12, 202011010000, 202011302359, 9, 202011250000, 202011252359, 10, 202108010000, 202108312359, 11, 202108010000, 202108312359,
			12, 202108010000, 202108312359, 10, 202109010000, 202109302359, 11, 202109010000, 202109302359, 12, 202109010000, 202109302359,
			9, 202110010000, 202110312359, 10, 202110010000, 202110312359, 11, 202110010000, 202110312359, 12, 202110010000, 202110312359,
			9, 202111240000, 202111242359, 10, 202210010000, 202210312359, 11, 202210010000, 202210312359, 12, 202210010000, 202210312359,
			9, 202212010000, 202212312359, 10, 202311010000, 202311302359, 11, 202311010000, 202311302359, 12, 202311010000, 202311302359,
			10, 202410010000, 202410312359, 11, 202410010000, 202410312359, 12, 202410010000, 202410312359, 9, 202411270000, 202411272359,
			10, 202601220000, 202601222359, 11, 202601220000, 202601222359, 12, 202601220000, 202601222359, 2, 202610280000, 202610282359,
			2, 202612090000, 202612092359, 2, 202701270000, 202701272359, 2, 202703170000, 202703172359, 2, 202704280000, 202704282359,
			2, 202706090000, 202706092359, 2, 202707280000, 202707282359, 2, 202709150000, 202709152359, 2, 202710270000, 202710272359,
		};
		static readonly string[] s_coverageHoleWhy = {
			"PCE 2016-02: ALFRED lists 2 dates (2016-02-01,2016-02-26)",
			"PCE 2016-08: ALFRED lists 2 dates (2016-08-02,2016-08-29)",
			"GDP 2016-08: ALFRED lists 2 dates in the month (2016-08-03,2016-08-26): estimate ambiguous",
			"GDP 2016-08: ALFRED lists 2 dates in the month (2016-08-03,2016-08-26): estimate ambiguous",
			"GDP 2016-08: ALFRED lists 2 dates in the month (2016-08-03,2016-08-26): estimate ambiguous",
			"GDP 2016-09: ALFRED lists 2 dates in the month (2016-09-08,2016-09-29): estimate ambiguous",
			"GDP 2016-09: ALFRED lists 2 dates in the month (2016-09-08,2016-09-29): estimate ambiguous",
			"GDP 2016-09: ALFRED lists 2 dates in the month (2016-09-08,2016-09-29): estimate ambiguous",
			"GDP 2016-11: ALFRED lists 2 dates in the month (2016-11-03,2016-11-29): estimate ambiguous",
			"GDP 2016-11: ALFRED lists 2 dates in the month (2016-11-03,2016-11-29): estimate ambiguous",
			"GDP 2016-11: ALFRED lists 2 dates in the month (2016-11-03,2016-11-29): estimate ambiguous",
			"PCE 2016-12-22: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"PCE 2017-03: ALFRED lists 2 dates (2017-03-01,2017-03-31)",
			"PCE 2017-05: ALFRED lists 2 dates (2017-05-01,2017-05-30)",
			"PCE 2017-08: ALFRED lists 2 dates (2017-08-01,2017-08-31)",
			"GDP 2017-08: ALFRED lists 3 dates in the month (2017-08-03,2017-08-30,2017-08-31): estimate ambiguous",
			"GDP 2017-08: ALFRED lists 3 dates in the month (2017-08-03,2017-08-30,2017-08-31): estimate ambiguous",
			"GDP 2017-08: ALFRED lists 3 dates in the month (2017-08-03,2017-08-30,2017-08-31): estimate ambiguous",
			"GDP 2017-10: ALFRED lists 2 dates in the month (2017-10-03,2017-10-27): estimate ambiguous",
			"GDP 2017-10: ALFRED lists 2 dates in the month (2017-10-03,2017-10-27): estimate ambiguous",
			"GDP 2017-10: ALFRED lists 2 dates in the month (2017-10-03,2017-10-27): estimate ambiguous",
			"GDP 2017-11: ALFRED lists 2 dates in the month (2017-11-16,2017-11-29): estimate ambiguous",
			"GDP 2017-11: ALFRED lists 2 dates in the month (2017-11-16,2017-11-29): estimate ambiguous",
			"GDP 2017-11: ALFRED lists 2 dates in the month (2017-11-16,2017-11-29): estimate ambiguous",
			"PCE 2018-03: ALFRED lists 2 dates (2018-03-01,2018-03-29)",
			"GDP 2018-07: ALFRED lists 2 dates in the month (2018-07-27,2018-07-31): estimate ambiguous",
			"GDP 2018-07: ALFRED lists 2 dates in the month (2018-07-27,2018-07-31): estimate ambiguous",
			"GDP 2018-07: ALFRED lists 2 dates in the month (2018-07-27,2018-07-31): estimate ambiguous",
			"PCE 2018-07-31: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"PCE 2018-12-21: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"GDP 2019-01: no ALFRED date in the month",
			"GDP 2019-01: no ALFRED date in the month",
			"PCE 2019-03: ALFRED lists 2 dates (2019-03-01,2019-03-29)",
			"FomcDecision 2019-10-04 (unscheduled): statement released 2019-10-11, no time on the page; not a rate decision",
			"PCE 2019-11-27: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"PCE 2019-12-20: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"FomcPress 2020-03-03 (unscheduled): fomcpresconf page carries no time",
			"FomcPress 2020-03-15 (unscheduled): fomcpresconf page carries no time",
			"GDP 2020-09: ALFRED lists 2 dates in the month (2020-09-16,2020-09-30): estimate ambiguous",
			"GDP 2020-09: ALFRED lists 2 dates in the month (2020-09-16,2020-09-30): estimate ambiguous",
			"GDP 2020-09: ALFRED lists 2 dates in the month (2020-09-16,2020-09-30): estimate ambiguous",
			"PCE 2020-10: ALFRED lists 2 dates (2020-10-01,2020-10-30)",
			"GDP 2020-11: ALFRED lists 2 dates in the month (2020-11-03,2020-11-25): estimate ambiguous",
			"GDP 2020-11: ALFRED lists 2 dates in the month (2020-11-03,2020-11-25): estimate ambiguous",
			"GDP 2020-11: ALFRED lists 2 dates in the month (2020-11-03,2020-11-25): estimate ambiguous",
			"PCE 2020-11-25: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"GDP 2021-08: ALFRED lists 3 dates in the month (2021-08-06,2021-08-20,2021-08-26): estimate ambiguous",
			"GDP 2021-08: ALFRED lists 3 dates in the month (2021-08-06,2021-08-20,2021-08-26): estimate ambiguous",
			"GDP 2021-08: ALFRED lists 3 dates in the month (2021-08-06,2021-08-20,2021-08-26): estimate ambiguous",
			"GDP 2021-09: ALFRED lists 2 dates in the month (2021-09-24,2021-09-30): estimate ambiguous",
			"GDP 2021-09: ALFRED lists 2 dates in the month (2021-09-24,2021-09-30): estimate ambiguous",
			"GDP 2021-09: ALFRED lists 2 dates in the month (2021-09-24,2021-09-30): estimate ambiguous",
			"PCE 2021-10: ALFRED lists 2 dates (2021-10-01,2021-10-29)",
			"GDP 2021-10: ALFRED lists 2 dates in the month (2021-10-28,2021-10-29): estimate ambiguous",
			"GDP 2021-10: ALFRED lists 2 dates in the month (2021-10-28,2021-10-29): estimate ambiguous",
			"GDP 2021-10: ALFRED lists 2 dates in the month (2021-10-28,2021-10-29): estimate ambiguous",
			"PCE 2021-11-24: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"GDP 2022-10: ALFRED lists 2 dates in the month (2022-10-06,2022-10-27): estimate ambiguous",
			"GDP 2022-10: ALFRED lists 2 dates in the month (2022-10-06,2022-10-27): estimate ambiguous",
			"GDP 2022-10: ALFRED lists 2 dates in the month (2022-10-06,2022-10-27): estimate ambiguous",
			"PCE 2022-12: ALFRED lists 2 dates (2022-12-01,2022-12-23)",
			"GDP 2023-11: ALFRED lists 2 dates in the month (2023-11-17,2023-11-29): estimate ambiguous",
			"GDP 2023-11: ALFRED lists 2 dates in the month (2023-11-17,2023-11-29): estimate ambiguous",
			"GDP 2023-11: ALFRED lists 2 dates in the month (2023-11-17,2023-11-29): estimate ambiguous",
			"GDP 2024-10: ALFRED lists 2 dates in the month (2024-10-02,2024-10-30): estimate ambiguous",
			"GDP 2024-10: ALFRED lists 2 dates in the month (2024-10-02,2024-10-30): estimate ambiguous",
			"GDP 2024-10: ALFRED lists 2 dates in the month (2024-10-02,2024-10-30): estimate ambiguous",
			"PCE 2024-11-27: same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED",
			"GDP 2025Q3 Updated Estimate 2026-01-22 08:30:00: lapse-era 'Updated' estimate has no Advance/Second/Third kind",
			"GDP 2025Q3 Updated Estimate 2026-01-22 08:30:00: lapse-era 'Updated' estimate has no Advance/Second/Third kind",
			"GDP 2025Q3 Updated Estimate 2026-01-22 08:30:00: lapse-era 'Updated' estimate has no Advance/Second/Third kind",
			"FomcPress 2026-10-28: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2026-12-09: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-01-27: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-03-17: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-04-28: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-06-09: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-07-28: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-09-15: future meeting: the calendar page lists no press conference yet (no source)",
			"FomcPress 2027-10-27: future meeting: the calendar page lists no press conference yet (no source)",
		};
		static readonly string s_expectedTableSha = "228cdee716a6eafa";
		static readonly EventCorrection[] s_corrections = {
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2018-12", originalEt = 201901160830, actualEt = 201902140830, announcedEt = 201901302359, canceledSinceEt = 201901302359, flags = EEventFlags.None,
				source = "https://www.census.gov/newsroom/press-releases/2019/revised-indicator-calendar.html (first issued 2019-01-30)" },
			new EventCorrection { kind = EEventKind.GdpAdvance, refPeriod = "2018Q4", originalEt = 201901300830, actualEt = 201902280830, announcedEt = 201902062359, canceledSinceEt = 201902062359, flags = EEventFlags.None,
				source = "https://www.bea.gov/news/blog/2019-02-06/new-dates-set-gdp-personal-income-and-international-trade; https://www.bea.gov/news/2019/initial-gross-domestic-product-4th-quarter-and-annual-2018" },
			new EventCorrection { kind = EEventKind.GdpSecond, refPeriod = "2018Q4", originalEt = 201902280830, actualEt = 0, announcedEt = 201902062359, canceledSinceEt = 201902062359, flags = EEventFlags.None,
				source = "https://www.bea.gov/news/blog/2019-02-06/new-dates-set-gdp-personal-income-and-international-trade (second estimate replaced by the initial estimate)" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2019-01", originalEt = 0, actualEt = 201903110830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2019-02", originalEt = 0, actualEt = 201904010830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2019-03", originalEt = 0, actualEt = 201904180830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.FomcDecision, refPeriod = "2020-03-18", originalEt = 202003181400, actualEt = 0, announcedEt = 0, canceledSinceEt = 202003151700, flags = EEventFlags.None,
				source = "https://www.federalreserve.gov/monetarypolicy/fomchistorical2020.htm (March 17-18 (cancelled)); canceledSince = 03-15 17:00 statement (EV-5.5c design choice)" },
			new EventCorrection { kind = EEventKind.FomcPress, refPeriod = "2020-03-18", originalEt = 202003181430, actualEt = 0, announcedEt = 0, canceledSinceEt = 202003151700, flags = EEventFlags.None,
				source = "https://www.federalreserve.gov/monetarypolicy/fomchistorical2020.htm (March 17-18 (cancelled))" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2020-12-24", originalEt = 202012240830, actualEt = 202012230830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt (DOL ETA weekly claims; announcement time unknown: known at the instant)" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-01-09", originalEt = 202501090830, actualEt = 202501080830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt (DOL ETA weekly claims; announcement time unknown: known at the instant)" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-10-02", originalEt = 202510020830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Nfp, refPeriod = "2025-09", originalEt = 202510030830, actualEt = 202511200830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-10-09", originalEt = 202510090830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Cpi, refPeriod = "2025-09", originalEt = 202510150830, actualEt = 202510240830, announcedEt = 202510102359, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://www.bls.gov/bls/092025-cpi-reschedule-notice.htm (last modified 2025-10-10); https://www.bls.gov/news.release/archives/cpi_10242025.htm" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2025-09", originalEt = 202510160830, actualEt = 202511250830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-10-16", originalEt = 202510160830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-10-23", originalEt = 202510230830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-10-30", originalEt = 202510300830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.GdpAdvance, refPeriod = "2025Q3", originalEt = 202510300830, actualEt = 202512230830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bea.gov/news/schedule/full-2025 (original never released, lapse; announcement of the new date not capturable: known at the instant)" },
			new EventCorrection { kind = EEventKind.Jolts, refPeriod = "2025-09", originalEt = 202511041000, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-11-06", originalEt = 202511060830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Nfp, refPeriod = "2025-10", originalEt = 202511070830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm" },
			new EventCorrection { kind = EEventKind.Cpi, refPeriod = "2025-10", originalEt = 202511130830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm" },
			new EventCorrection { kind = EEventKind.Claims, refPeriod = "week of 2025-11-13", originalEt = 202511130830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://oui.doleta.gov/unemploy/claims_arch.asp (no releases 2025-10-02..11-13, lapse); https://alfred.stlouisfed.org/release/downloaddates?rid=180&ff=txt" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2025-10", originalEt = 202511140830, actualEt = 0, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.None,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2025-09", originalEt = 0, actualEt = 202511250830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Jolts, refPeriod = "2025-10", originalEt = 202512021000, actualEt = 202512091000, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Nfp, refPeriod = "2025-11", originalEt = 202512050830, actualEt = 202512160830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Pce, refPeriod = "2025-09", originalEt = 0, actualEt = 202512051000, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bea.gov/news/schedule/full-2025 (lapse-delayed; original date not on the page: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Cpi, refPeriod = "2025-11", originalEt = 202512100830, actualEt = 202512180830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2025-11", originalEt = 202512110830, actualEt = 202601140830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2025-10", originalEt = 0, actualEt = 202512160830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2025-12", originalEt = 202601140830, actualEt = 202601300830, announcedEt = 0, canceledSinceEt = 202510010001, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm; revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2025-11", originalEt = 0, actualEt = 202601140830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Pce, refPeriod = "2025-10/11", originalEt = 0, actualEt = 202601221000, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bea.gov/news/schedule/full (lapse-delayed; original date not on the page: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Nfp, refPeriod = "2026-01", originalEt = 202602060830, actualEt = 202602110830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm (2026 lapse start unknown: canceled at the original instant); revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2025-12", originalEt = 0, actualEt = 202602100830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Cpi, refPeriod = "2026-01", originalEt = 202602110830, actualEt = 202602130830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm (2026 lapse start unknown: canceled at the original instant); revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2026-01", originalEt = 202602120830, actualEt = 202602270830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm (2026 lapse start unknown: canceled at the original instant); revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2026-01", originalEt = 0, actualEt = 202603060830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.Ppi, refPeriod = "2026-02", originalEt = 202603120830, actualEt = 202603180830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm (2026 lapse start unknown: canceled at the original instant); revised date first-publication capture unavailable: known at the instant" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2026-02", originalEt = 0, actualEt = 202604010830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
			new EventCorrection { kind = EEventKind.RetailSales, refPeriod = "2026-03", originalEt = 0, actualEt = 202604210830, announcedEt = 0, canceledSinceEt = 0, flags = EEventFlags.Unscheduled,
				source = "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx (time assumed 08:30: the Census release-date sheet gives no time) ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)" },
		};
	}
	// </event-table>

	// <event-engine>
	/// <summary>
	/// M-EVENT engine. Host pattern: new _EventCalendar { config, journal, warnOnce }; Configure() returns an error string;
	/// Step(barTime, barOpenUtc, barCloseUtc, rollSplice) once per closed bar.
	/// </summary>
	internal sealed class _EventCalendar
	{
		public EventConfig config;
		public string label = "EVENT";
		public Action<string> journal, warnOnce;
		/// <summary>Error sink (EV-4.6b table missing / hash mismatch). Null = warnOnce.</summary>
		public Action<string> error;
		/// <summary>Bar length (EV-3.3/3.7). Zero = non-time bars (open = previous bar close). &gt;= 1 day = daily bars.</summary>
		public TimeSpan barDuration = TimeSpan.Zero;
		/// <summary>First bar instant (coverage check, EV-4.7). MinValue = skip.</summary>
		public DateTime firstBarUtc = DateTime.MinValue;
		/// <summary>Test seams: replace the generated table / corrections (null = EventRules).</summary>
		public List<EventRec> table;
		public EventCorrection[] corrections;
		/// <summary>Test seam: coverage-hole triples (kind, fromEt, toEt yyyyMMddHHmm). null = EventRules holes when table is null,
		/// none when the table seam is set.</summary>
		public long[] coverageHoles;
		/// <summary>Test seam: expected table hash (null = EventRules.ExpectedTableSha); used only with the compiled table.</summary>
		public string tableShaExpected;

		sealed class Year
		{
			public EventRec[] recs;
			public Dictionary<DateTime, int[]> dayIdx;
		}
		struct CsvRow
		{
			public int line; public string action; public EEventKind kind; public string name; public DateTime date; public TimeSpan? time;
			public EEventTier? tier; public EEventFlags flags; public string refPeriod; public DateTime? announced; public string source;
		}
		sealed class DayCache { public List<EventRec> list; public DateTime from, until; }

		TimeZoneInfo m_est;
		_CmeSessionClock m_clock;
		readonly Dictionary<int, Year> m_years = new Dictionary<int, Year>();
		readonly Dictionary<long, DayCache> m_dayCache = new Dictionary<long, DayCache>();
		List<CsvRow> m_csv = new List<CsvRow>();
		List<EventRec> m_table;
		EventCorrection[] m_corr;
		HashSet<EEventKind> m_tableKindsPresent = new HashSet<EEventKind>();
		HashSet<int> m_warnedYears = new HashSet<int>();
		bool m_configured, m_dailyWarned;
		bool m_disabled;              // EV-4.6b: table missing / hash mismatch only; every query is neutral
		// EV-4.6b per-kind Unknown spans (ET trading days, inclusive): before coverage start, interior holes, after coverage end
		struct CovSpan { public EEventKind kind; public DateTime from, to; public string why; }
		List<CovSpan> m_unk = new List<CovSpan>();
		DateTime m_unkDay = DateTime.MinValue;
		List<EEventKind> m_unkKinds = new List<EEventKind>();
		readonly Dictionary<EEventKind, int[]> m_covStats = new Dictionary<EEventKind, int[]>();   // kind -> {days, blocked days}

		// bar state
		DateTime m_barTime, m_open = DateTime.MinValue, m_now = DateTime.MinValue, m_day = DateTime.MinValue, m_lastSpliceDay = DateTime.MinValue;
		int m_curYear;
		bool m_blackout, m_flattenNow;
		EventRec m_blackoutRec;

		public TimeZoneInfo Eastern { get { return m_est; } }
		public DateTime NowUtc { get { return m_now; } }
		public DateTime BarOpenUtc { get { return m_open; } }
		public DateTime TradingDay { get { return m_day; } }
		public bool Blackout { get { return m_blackout; } }
		public bool FlattenNow { get { return m_flattenNow; } }
		public bool DailyBars { get { return barDuration >= TimeSpan.FromDays(1); } }
		public int RowsLoadedCsv { get { return m_csv.Count; } }
		/// <summary>EV-4.6b: disabled only when the event table is missing or its hash mismatches; all answers neutral.</summary>
		public bool Disabled { get { return m_disabled; } }

		void jr(string s) { if (journal != null) journal(label + " " + s); }
		void warn(string s) { if (warnOnce != null) warnOnce(label + " " + s); }
		static string ymd(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
		static string hm(DateTime d) { return d.ToString("HH:mm", CultureInfo.InvariantCulture); }

		// ------------------------------------------------------------------ configure
		public string Configure()
		{
			if (config == null) return label + ": no config";
			string v = config.Validate();
			if (v != null) return v;
			m_clock = new _CmeSessionClock(null);
			m_est = m_clock.Eastern;
			m_table = table ?? EventRules.Table();
			m_corr = corrections ?? EventRules.Corrections;
			var errs = new List<string>();
			for (int i = 0; i < m_table.Count; ++i) { string e = checkEt(m_table[i].etTime); if (e != null) errs.Add("table row " + i + " " + m_table[i].KindName + " " + e); else if (EventRules.IsTableKind(m_table[i].kind)) m_tableKindsPresent.Add(m_table[i].kind); }
			for (int i = 0; i < m_corr.Length; ++i)
				foreach (long t in new[] { m_corr[i].originalEt, m_corr[i].actualEt, m_corr[i].announcedEt, m_corr[i].canceledSinceEt })
					if (t != 0) { string e = checkEt(EventRules.FromYmdhm(t)); if (e != null) errs.Add("correction " + i + " " + m_corr[i].kind + " " + e); }
			config.csvSha = null;
			if (!string.IsNullOrEmpty(config.csvPath))
			{
				byte[] bytes;
				try { bytes = File.ReadAllBytes(config.csvPath); }
				catch (Exception ex) { return label + " csv " + config.csvPath + ": " + ex.Message; }
				string ce = LoadCsv(Encoding.UTF8.GetString(bytes));
				if (ce != null) return ce;
				config.csvSha = EventRules.Sha256(bytes);
			}
			if (errs.Count > 0) return label + ": " + string.Join("; ", errs);

			// coverage (EV-4.6/4.7)
			var missing = new List<string>();
			foreach (var k in EventRules.TableKinds) if (!m_tableKindsPresent.Contains(k)) missing.Add(k.ToString());
			jr("created classes(" + config.classes + ") minTier(" + config.minTier + ") blackout(" + config.blackoutPreMin + "/" + config.blackoutPostMin + " " + config.blackoutTier +
				(config.blackoutFlatten ? " flatten" : "") + ") csv(" + (string.IsNullOrEmpty(config.csvPath) ? "-" : config.csvPath + " rows " + m_csv.Count + " sha " + config.csvSha.Substring(0, 16)) +
				") table(" + EventRules.TableSha() + " rows " + m_table.Count + " corrections " + m_corr.Length + ")");
			if (missing.Count > 0) jr("table kinds missing: " + string.Join(",", missing));
			var cov = new StringBuilder();
			foreach (var k in EventRules.TableKinds)
			{
				if (!m_tableKindsPresent.Contains(k)) continue;
				DateTime a = DateTime.MaxValue, b = DateTime.MinValue;
				foreach (var r in m_table) if (r.kind == k) { if (r.etTime < a) a = r.etTime; if (r.etTime > b) b = r.etTime; }
				cov.Append(' ').Append(k).Append('(').Append(ymd(a)).Append("..").Append(ymd(b)).Append(')');
			}
			// EV-4.6b: per-kind Unknown spans: before the kind's coverage start, refused holes (build_table.py), after its end
			m_unk.Clear(); m_unkDay = DateTime.MinValue; m_unkKinds.Clear(); m_covStats.Clear();
			DateTime tableEnd = DateTime.MinValue;
			foreach (var r in m_table) if (r.etTime > tableEnd) tableEnd = r.etTime;
			foreach (var k in EventRules.TableKinds)
			{
				if (!m_tableKindsPresent.Contains(k)) continue;
				DateTime a = DateTime.MaxValue, b = DateTime.MinValue;
				foreach (var r in m_table) if (r.kind == k) { if (r.etTime < a) a = r.etTime; if (r.etTime > b) b = r.etTime; }
				bool gdp = k == EEventKind.GdpAdvance || k == EEventKind.GdpSecond || k == EEventKind.GdpThird;
				// covered from the reference month of the first row through the month before the next expected release
				// (GDP kinds quarterly, others monthly), capped at the table end
				DateTime s0 = new DateTime(a.Year, a.Month, 1).AddMonths(gdp ? -2 : 0);
				DateTime e = endOfMonth(b.AddMonths(gdp ? 2 : 0)).Date, ge = endOfMonth(tableEnd).Date;
				if (e > ge) e = ge;
				m_unk.Add(new CovSpan { kind = k, from = DateTime.MinValue, to = s0.AddDays(-1), why = "before coverage start (first row " + ymd(a) + ")" });
				m_unk.Add(new CovSpan { kind = k, from = e.AddDays(1), to = DateTime.MaxValue.Date, why = "after coverage end " + ymd(e) + " (last row " + ymd(b) + ")" });
				m_covStats[k] = new int[2];
			}
			long[] holes = coverageHoles ?? (table == null ? EventRules.CoverageHoles() : null);
			if (holes != null)
				for (int i = 0; i + 2 < holes.Length; i += 3)
				{
					var hk = (EEventKind)holes[i];
					if (!m_tableKindsPresent.Contains(hk)) continue;
					m_unk.Add(new CovSpan { kind = hk, from = EventRules.FromYmdhm(holes[i + 1]).Date, to = EventRules.FromYmdhm(holes[i + 2]).Date,
						why = EventRules.HoleWhyFor(holes[i], holes[i + 1], holes[i + 2]) ?? "test seam" });
				}
			// COVGAP journal, once at start (per affected day lines follow from Step)
			DateTime firstDay = firstBarUtc == DateTime.MinValue ? DateTime.MinValue : ToEt(firstBarUtc).Date;
			foreach (var u in m_unk)
				if (u.to >= firstDay)
					jr("COVGAP " + u.kind + " " + (u.from == DateTime.MinValue ? "-" : ymd(u.from)) + ".." + (u.to == DateTime.MaxValue.Date ? "-" : ymd(u.to)) + " Unknown " +
						(config.allowPartialCoverage || (kindClass(u.kind) & config.classes) == 0 ? "(NoEvent: " + (config.allowPartialCoverage ? "allowPartialCoverage" : "not in classes") + ")" : "(blocks)") + " " + u.why);
			jr("coverage rules(Claims,IsmMfg,IsmSvc,EiaPetroleum,calendar)" + cov + " ruleOnly(Claims, ISM except 2026, EIA outside 2025-01..2026-11, VixExpiry)");
			if (firstBarUtc != DateTime.MinValue)
				foreach (var c in m_corr) { long t = c.actualEt != 0 ? c.actualEt : c.originalEt; if (t != 0 && toUtc(EventRules.FromYmdhm(t)) >= firstBarUtc) jr("correction " + c.kind + " " + (c.refPeriod ?? "") + " orig(" + c.originalEt + ") actual(" + (c.actualEt == 0 ? "Canceled" : c.actualEt.ToString()) + ") announced(" + c.announcedEt + ") " + c.flags + " " + (c.source ?? "")); }
			m_configured = true;
			// EV-4.6b: engine-wide disable only for a missing generated table or a hash mismatch
			if (table == null)
			{
				string bad = EventRules.TableIntegrity(EventRules.TableRowCount, EventRules.TableSha(), tableShaExpected ?? EventRules.ExpectedTableSha);
				if (bad != null) coverageDisable(bad);
			}
			return null;
		}

		string checkEt(DateTime et)
		{
			if (m_est.IsInvalidTime(et)) return "invalid ET time " + et.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
			if (m_est.IsAmbiguousTime(et)) return "ambiguous ET time " + et.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
			return null;
		}
		DateTime toUtc(DateTime et) { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(et, DateTimeKind.Unspecified), m_est); }
		public DateTime ToEt(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), m_est); }

		// ------------------------------------------------------------------ CSV (EV-4.4)
		/// <summary>Parses the CSV text (columns action,kind,date_et,time_et,tier,flags,ref_period,announced_et,source). Errors carry line numbers.</summary>
		public string LoadCsv(string text)
		{
			if (m_est == null) { m_clock = new _CmeSessionClock(null); m_est = m_clock.Eastern; }
			m_csv = new List<CsvRow>();
			var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			var errs = new List<string>();
			for (int i = 0; i < lines.Length; ++i)
			{
				string s = lines[i].Trim();
				if (s.Length == 0 || s[0] == '#') continue;
				if (s.StartsWith("action,", StringComparison.OrdinalIgnoreCase)) continue;
				string e; CsvRow row;
				if (!parseRow(s, i + 1, out row, out e)) errs.Add("line " + (i + 1) + ": " + e + " [" + s + "]");
				else m_csv.Add(row);
			}
			return errs.Count == 0 ? null : label + " csv: " + string.Join("; ", errs);
		}

		bool parseRow(string s, int line, out CsvRow r, out string err)
		{
			r = new CsvRow(); r.line = line; err = null;
			var c = s.Split(',');
			if (c.Length < 9) { err = "expected 9 columns, got " + c.Length; return false; }
			for (int i = 0; i < c.Length; ++i) c[i] = c[i].Trim();
			r.action = c[0].ToLowerInvariant();
			if (r.action != "add" && r.action != "remove" && r.action != "retime" && r.action != "retier") { err = "unknown action '" + c[0] + "'"; return false; }
			if (c[1].StartsWith("Custom:", StringComparison.Ordinal)) { r.kind = EEventKind.Custom; r.name = c[1].Substring(7); if (r.name.Length == 0) { err = "empty Custom name"; return false; } }
			else { EEventKind k; if (!Enum.TryParse(c[1], false, out k) || k == EEventKind.None || k == EEventKind.Custom || !Enum.IsDefined(typeof(EEventKind), k)) { err = "unknown kind '" + c[1] + "'"; return false; } r.kind = k; }
			DateTime d;
			if (!DateTime.TryParseExact(c[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) { err = "bad date_et '" + c[2] + "'"; return false; }
			r.date = d;
			if (c[3].Length > 0)
			{
				TimeSpan t;
				if (!TimeSpan.TryParseExact(c[3], new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out t) || t < TimeSpan.Zero || t >= TimeSpan.FromDays(1)) { err = "bad time_et '" + c[3] + "'"; return false; }
				r.time = t;
				string e2 = checkEt(d + t); if (e2 != null) { err = e2; return false; }
			}
			if (c[4].Length > 0) { EEventTier t; if (!Enum.TryParse(c[4], false, out t) || !Enum.IsDefined(typeof(EEventTier), t)) { err = "bad tier '" + c[4] + "'"; return false; } r.tier = t; }
			if (c[5].Length > 0)
				foreach (var f in c[5].Split('|'))
				{
					EEventFlags fl; if (!Enum.TryParse(f.Trim(), false, out fl) || !Enum.IsDefined(typeof(EEventFlags), fl)) { err = "bad flag '" + f + "'"; return false; }
					r.flags |= fl;
				}
			r.refPeriod = c[6].Length > 0 ? c[6] : null;
			if (c[7].Length > 0)
			{
				DateTime a;
				if (!DateTime.TryParseExact(c[7], new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out a)) { err = "bad announced_et '" + c[7] + "'"; return false; }
				string e3 = checkEt(a); if (e3 != null) { err = "announced_et " + e3; return false; }
				r.announced = a;
			}
			r.source = string.Join(",", c, 8, c.Length - 8);
			bool needTime = r.action == "add" || r.action == "retime";
			if (needTime && !r.time.HasValue) { err = r.action + " needs time_et"; return false; }
			if (needTime && !r.announced.HasValue && !((r.flags & EEventFlags.Scheduled) != 0 && r.source.Length > 0)) { err = r.action + " needs announced_et (or flag Scheduled with a source)"; return false; }
			if (r.action == "retier" && !r.tier.HasValue) { err = "retier needs tier"; return false; }
			if (r.action == "add" && r.kind == EEventKind.Custom && !r.tier.HasValue) { err = "Custom add needs tier"; return false; }
			return true;
		}

		// ------------------------------------------------------------------ year build
		static readonly Year s_emptyYear = new Year { recs = new EventRec[0], dayIdx = new Dictionary<DateTime, int[]>() };
		Year year(int y)
		{
			Year yr;
			if (m_disabled) return s_emptyYear;
			if (m_years.TryGetValue(y, out yr)) return yr;
			yr = build(y);
			m_years[y] = yr;
			return yr;
		}

		Year build(int y)
		{
			var list = EventRules.Generate(y, config.includeRussell);
			foreach (var t in m_table) if (t.etTime.Year == y) list.Add(t);
			foreach (var c in m_corr) applyCorrection(list, c, y);
			foreach (var row in m_csv) if (row.date.Year == y) applyCsv(list, row);
			var missing = new List<string>();
			foreach (var k in EventRules.TableKinds) { bool any = false; foreach (var r in list) if (r.kind == k) { any = true; break; } if (!any) missing.Add(k.ToString()); }
			if (missing.Count > 0 && m_warnedYears.Add(y)) warn(y + " table kinds missing: " + string.Join(",", missing));

			var keep = new List<EventRec>(list.Count);
			foreach (var r0 in list)
			{
				EventRec r = r0;
				EEventTier ot;
				if (config.tierOverride != null && config.tierOverride.TryGetValue(r.kind, out ot)) r.tier = ot;
				if ((r.Class & config.classes) == 0) continue;
				if (EventConfig.IsMacroTier(config.minTier) && r.tier != EEventTier.C && r.tier < config.minTier) continue;
				r.utc = toUtc(r.etTime);
				r.tradingDay = TradingDayOf(r.utc);
				if ((r.flags & EEventFlags.Unscheduled) != 0 && r.knownSinceUtc == DateTime.MinValue) r.knownSinceUtc = r.utc;
				// R2.1-e (EV-1.19): a RuleOnly EIA instant (no published exception table that year, so holiday retimes are unknown)
				// is not known in advance: known-from = the instant itself, never MinValue.
				if (r.kind == EEventKind.EiaPetroleum && (r.flags & EEventFlags.RuleOnly) != 0 && r.knownSinceUtc == DateTime.MinValue) r.knownSinceUtc = r.utc;
				// The HalfDay marker sits at the early halt instant, which R2.1-b maps to the next session; it labels its own date.
				if (r.kind == EEventKind.HalfDay) r.tradingDay = r.etTime.Date;
				keep.Add(r);
			}
			keep.Sort((a, b) => { int c = a.utc.CompareTo(b.utc); return c != 0 ? c : ((byte)b.tier).CompareTo((byte)a.tier); });
			var yr = new Year { recs = keep.ToArray(), dayIdx = new Dictionary<DateTime, int[]>() };
			var tmp = new Dictionary<DateTime, List<int>>();
			for (int i = 0; i < yr.recs.Length; ++i) { List<int> l; if (!tmp.TryGetValue(yr.recs[i].tradingDay, out l)) tmp[yr.recs[i].tradingDay] = l = new List<int>(); l.Add(i); }
			foreach (var kv in tmp) yr.dayIdx[kv.Key] = kv.Value.ToArray();
			return yr;
		}

		DateTime utcOfYmdhm(long v) { return toUtc(EventRules.FromYmdhm(v)); }

		void applyCorrection(List<EventRec> list, EventCorrection c, int y)
		{
			if (c.originalEt != 0 && EventRules.FromYmdhm(c.originalEt).Year == y)
			{
				DateTime o = EventRules.FromYmdhm(c.originalEt);
				int i = list.FindIndex(r => r.kind == c.kind && r.etTime == o);
				if (i < 0) warn("correction original not found " + c.kind + " " + c.originalEt);
				else
				{
					EventRec r = list[i];
					long cs = c.canceledSinceEt != 0 ? c.canceledSinceEt : c.announcedEt;
					r.canceledSinceUtc = cs != 0 ? utcOfYmdhm(cs) : toUtc(o);
					r.flags |= EEventFlags.Canceled;
					list[i] = r;
				}
			}
			if (c.actualEt != 0 && EventRules.FromYmdhm(c.actualEt).Year == y)
			{
				EventRec n = EventRec.Make(c.kind, EventRules.FromYmdhm(c.actualEt), EventRules.DefaultTier(c.kind), c.flags | (c.originalEt != 0 ? EEventFlags.Delayed : EEventFlags.None));
				n.refPeriod = c.refPeriod; n.source = c.source;
				if (c.announcedEt != 0) n.knownSinceUtc = utcOfYmdhm(c.announcedEt);
				else if ((c.flags & EEventFlags.Unscheduled) != 0) n.knownSinceUtc = toUtc(n.etTime);
				list.Add(n);
			}
		}

		bool rowMatches(EventRec r, CsvRow row)
		{
			if (r.kind != row.kind) return false;
			if (row.kind == EEventKind.Custom && r.name != row.name) return false;
			if (row.action == "retime" && row.refPeriod != null) return r.refPeriod == row.refPeriod;
			if (r.etTime.Date != row.date) return false;
			return row.action == "retime" || !row.time.HasValue || r.etTime.TimeOfDay == row.time.Value;
		}

		void applyCsv(List<EventRec> list, CsvRow row)
		{
			DateTime known = row.announced.HasValue ? toUtc(row.announced.Value) : DateTime.MinValue;
			switch (row.action)
			{
				case "add":
				{
					EventRec n = EventRec.Make(row.kind, row.date + row.time.Value, row.tier ?? EventRules.DefaultTier(row.kind), row.flags | EEventFlags.Csv);
					n.name = row.name; n.refPeriod = row.refPeriod; n.source = row.source; n.knownSinceUtc = known;
					list.Add(n);
					break;
				}
				case "remove":
				{
					int c = list.RemoveAll(r => rowMatches(r, row));
					if (c == 0) warn("csv line " + row.line + ": remove matched nothing");
					break;
				}
				case "retier":
				{
					int c = 0;
					for (int i = 0; i < list.Count; ++i) if (rowMatches(list[i], row)) { EventRec r = list[i]; r.tier = row.tier.Value; r.flags |= EEventFlags.Csv; list[i] = r; ++c; }
					if (c == 0) warn("csv line " + row.line + ": retier matched nothing");
					break;
				}
				case "retime":
				{
					int i = list.FindIndex(r => rowMatches(r, row) && (r.flags & EEventFlags.Canceled) == 0);
					if (i < 0) { warn("csv line " + row.line + ": retime matched nothing"); break; }
					EventRec o = list[i];
					DateTime nt = row.date + row.time.Value;
					EventRec n = o;
					n.etTime = nt; n.flags = (o.flags | row.flags | EEventFlags.Csv | EEventFlags.Delayed) & ~EEventFlags.Canceled;
					n.knownSinceUtc = known; n.canceledSinceUtc = DateTime.MaxValue; n.source = row.source;
					if (row.tier.HasValue) n.tier = row.tier.Value;
					if (row.announced.HasValue && row.announced.Value > o.etTime) n.flags |= EEventFlags.Unscheduled;   // EV-5.2: announced after the original
					o.flags |= EEventFlags.Canceled;
					o.canceledSinceUtc = row.announced.HasValue && known < toUtc(o.etTime) ? known : toUtc(o.etTime);   // announced after the original: it never happened
					list[i] = o;
					list.Add(n);
					break;
				}
			}
		}

		// ------------------------------------------------------------------ trading day (EV-3.6)
		/// <summary>Globex trading date of an instant: the session open at that instant, else the next session to open.</summary>
		public DateTime TradingDayOf(DateTime utc)
		{
			DateTime et = ToEt(utc);
			if (m_clock.StateEt(et) == _CmeSessionClock.GlobexState.Open) return m_clock.GlobexDateEt(et);
			// R2.1-b: on a half day an instant after the early halt (before 17:00) is out of session like any other and maps to the
			// next session open (below). Before the halt (or with no halt) a weekday instant before 17:00 keeps its date.
			TimeSpan? halt = m_clock.HolidayHaltEt(et.Date);
			bool afterHalt = halt.HasValue && et.TimeOfDay >= halt.Value;
			if (!afterHalt && et.TimeOfDay < m_clock.GlobexClose && et.DayOfWeek != DayOfWeek.Saturday && et.DayOfWeek != DayOfWeek.Sunday && !m_clock.IsGlobexClosedDay(et.Date)) return et.Date;
			DateTime d = et.TimeOfDay >= m_clock.GlobexOpen ? et.Date.AddDays(2) : et.Date.AddDays(1);
			while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || m_clock.IsGlobexClosedDay(d)) d = d.AddDays(1);
			return d;
		}
		/// <summary>The session open (18:00 ET of the previous calendar day) of a trading date, in UTC — the default as-of point.</summary>
		public DateTime SessionOpenUtc(DateTime day)
		{
			// Always 18:00 ET of the previous calendar day. Weekday Globex closures (New Year, Christmas, Good Friday) are full
			// calendar-day closures that end with an 18:00 reopen on the closed day itself, so for the next trading day this is
			// the real reopen too: Fri Christmas 2026 -> Mon 12-28 opens Sun 12-27 18:00; Thu Christmas 2025 -> Fri 12-26 opens
			// Thu 12-25 18:00. A closed date is never a trading day (TradingDayOf skips it), so it has no session open; callers
			// passing one get the 18:00 before it (as-of point only). Checked for every trading day 2016-2028 in EventTests.
			return toUtc(day.Date.AddDays(-1) + m_clock.GlobexOpen);
		}

		// ------------------------------------------------------------------ step
		public void Step(DateTime barTime, DateTime barOpenUtc, DateTime barCloseUtc, bool rollSplice)
		{
			if (!m_configured) return;
			if (m_disabled) return;
			m_barTime = barTime;
			m_open = barOpenUtc;
			m_now = barCloseUtc;
			int y = ToEt(m_now).Year;
			if (y != m_curYear)
			{
				m_curYear = y;
				var drop = new List<int>(); foreach (var k in m_years.Keys) if (k < y - 1 || k > y + 1) drop.Add(k);
				foreach (var k in drop) m_years.Remove(k);
				m_dayCache.Clear();
				year(y - 1); year(y); year(y + 1);
			}
			DateTime day = TradingDayOf(m_open);
			if (rollSplice) m_lastSpliceDay = day;
			if (day != m_day)
			{
				m_day = day;
				if (m_dayCache.Count > 64) m_dayCache.Clear();
				coverageDay(day);
				EventDay d = Classify(day);
				// R2.1-e: RuleOnly EIA is known only at its instant, so the day line checks it ex post
				bool eiaRo = journal != null && ClassifyExPost(day).eiaRuleOnly;
				if ((d.kindsMask != 0 || eiaRo) && journal != null)
				{
					var ks = new StringBuilder();
					foreach (var r in EventsOn(day, EEventClassMask.All)) { if (ks.Length > 0) ks.Append(','); ks.Append(r.KindName).Append('@').Append(hm(r.etTime)); }
					jr("day " + ymd(day) + " at " + barTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " tier(" + d.maxTier + ") kinds(" + ks + ") firstRelease(" +
						(d.firstReleaseEt.HasValue ? d.firstReleaseEt.Value.ToString(@"hh\:mm") + " ET" : "-") + ")" + (d.isRollWeek ? " rollWeek" : "") + (d.isHalfDay ? " halfDay" : "") + (eiaRo ? " EiaRuleOnly" : ""));
				}
			}
			// blackout (exposed only; the base never blocks orders)
			bool was = m_blackout;
			EventRec hit;
			// R2.1-c (EV-6.7): Macro classes only; Energy is never OR'd in. Energy consumers (e.g. A2-20 on CL) call
			// IsInWindow(..., EEventClassMask.Energy) explicitly.
			m_blackout = (config.blackoutPreMin > 0 || config.blackoutPostMin > 0) && windowHit(config.blackoutTier, config.blackoutPreMin, config.blackoutPostMin, EEventClassMask.Macro, out hit, false) && (m_blackoutRec = hit).kind != EEventKind.None;
			m_flattenNow = config.blackoutFlatten && m_blackout && !was;
			if (m_blackout != was)
				jr("blackout " + (m_blackout ? "on" : "off") + " at " + barTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " " + m_blackoutRec.KindName + " " + m_blackoutRec.etTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " ET pre(" + config.blackoutPreMin + ") post(" + config.blackoutPostMin + ")" + (m_flattenNow ? " flatten" : ""));
		}

		void coverageDisable(string why)
		{
			// EV-4.6b: table missing / hash mismatch: Error line, engine disabled, neutral answers (no throw)
			string msg = label + " " + why + "; engine disabled";
			if (error != null) error(msg); else if (warnOnce != null) warnOnce(msg);
			jr(msg);
			m_disabled = true; m_blackout = m_flattenNow = false; m_years.Clear(); m_dayCache.Clear();
		}
		static DateTime endOfMonth(DateTime d) { return new DateTime(d.Year, d.Month, 1).AddMonths(1).AddMinutes(-1); }
		EEventClassMask kindClass(EEventKind k) { return EventRules.ClassOf(k, effTier(k)); }
		/// <summary>Effective tier of a kind: config.tierOverride, else the default tier (as applied to table rows).</summary>
		EEventTier effTier(EEventKind k)
		{
			EEventTier ot;
			if (config != null && config.tierOverride != null && config.tierOverride.TryGetValue(k, out ot)) return ot;
			return EventRules.DefaultTier(k);
		}

		// ------------------------------------------------------------------ coverage (EV-4.6b)
		List<EEventKind> unknownKinds(DateTime day)
		{
			day = day.Date;
			if (day != m_unkDay)
			{
				m_unkDay = day; m_unkKinds.Clear();
				foreach (var u in m_unk) if (day >= u.from && day <= u.to && !m_unkKinds.Contains(u.kind)) m_unkKinds.Add(u.kind);
			}
			return m_unkKinds;
		}
		bool kindBlocks(EEventKind k, DateTime day, EEventClassMask mask, EEventTier minTier)
		{
			var r = EventRec.Make(k, day, effTier(k), EEventFlags.None);
			return tierOk(ref r, minTier, mask);
		}

		/// <summary>EV-4.6b tri-state for one kind and ET trading day: Unknown inside a coverage hole, before the kind's first
		/// covered month or after its coverage end; else Event / NoEvent from the calendar (ex post). Rule-only and absent
		/// kinds are never Unknown.</summary>
		public EEventCoverage Coverage(EEventKind kind, DateTime day)
		{
			if (m_disabled || !m_configured) return EEventCoverage.NoEvent;
			if (unknownKinds(day).Contains(kind)) return EEventCoverage.Unknown;
			foreach (var r in EventsOnExPost(day.Date)) if (r.kind == kind) return EEventCoverage.Event;
			return EEventCoverage.NoEvent;
		}

		/// <summary>EV-4.6b: true when an Unknown kind blocks this day for a consumer: only if allowPartialCoverage = false and the
		/// kind is in the consumer's mask (and passes minTier); otherwise Unknown counts as NoEvent.</summary>
		public bool CoverageBlocks(DateTime day, EEventClassMask mask, EEventTier minTier, out EEventKind kind)
		{
			kind = EEventKind.None;
			if (m_disabled || !m_configured || config.allowPartialCoverage) return false;
			foreach (var k in unknownKinds(day)) if (kindBlocks(k, day, mask, minTier)) { kind = k; return true; }
			return false;
		}
		public bool CoverageBlocks(DateTime day, EEventClassMask mask) { EEventKind k; return CoverageBlocks(day, mask, EEventTier.None, out k); }

		void coverageDay(DateTime day)
		{
			var uk = unknownKinds(day);
			foreach (var kv in m_covStats)
			{
				kv.Value[0]++;
				if (!config.allowPartialCoverage && uk.Contains(kv.Key) && kindBlocks(kv.Key, day, config.classes, EEventTier.None)) kv.Value[1]++;
			}
			if (uk.Count == 0 || journal == null) return;
			var sb = new StringBuilder();
			foreach (var k in uk)
			{
				if (sb.Length > 0) sb.Append(',');
				sb.Append(k).Append(!config.allowPartialCoverage && kindBlocks(k, day, config.classes, EEventTier.None) ? "(blocks)" : "(NoEvent)");
			}
			jr("COVGAP day " + ymd(day) + " Unknown " + sb);
		}

		/// <summary>EV-4.6b report: share of Unknown-blocked trading days per kind in [from, to] for the consumer mask; a line
		/// ending FLAG means the share exceeds 5%. Run-time counts (days stepped) are in CoverageStats().</summary>
		public List<string> CoverageReport(DateTime from, DateTime to, EEventClassMask mask, out bool flagged)
		{
			flagged = false;
			var res = new List<string>();
			foreach (var k in EventRules.TableKinds)
			{
				if (!m_tableKindsPresent.Contains(k)) continue;
				int n = 0, b = 0;
				for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
				{
					if (!EventRules.IsNyseBusinessDay(d)) continue;
					++n;
					if (!config.allowPartialCoverage && unknownKinds(d).Contains(k) && kindBlocks(k, d, mask, EEventTier.None)) ++b;
				}
				double p = n == 0 ? 0 : 100.0 * b / n;
				bool f = p > 5.0; flagged |= f;
				res.Add("COVSHARE " + ymd(from) + ".." + ymd(to) + " " + k + " blocked " + b + "/" + n + " " + p.ToString("0.0", CultureInfo.InvariantCulture) + "%" + (f ? " FLAG" : ""));
			}
			return res;
		}
		public List<string> CoverageStats()
		{
			var res = new List<string>();
			foreach (var kv in m_covStats)
			{
				double p = kv.Value[0] == 0 ? 0 : 100.0 * kv.Value[1] / kv.Value[0];
				res.Add("COVSHARE run " + kv.Key + " blocked " + kv.Value[1] + "/" + kv.Value[0] + " " + p.ToString("0.0", CultureInfo.InvariantCulture) + "%" + (p > 5.0 ? " FLAG" : ""));
			}
			return res;
		}

		// ------------------------------------------------------------------ visibility (EV-5)
		static bool tierOk(ref EventRec r, EEventTier minTier, EEventClassMask mask)
		{
			if (r.IsSpan) return false;
			if ((r.Class & mask) == 0) return false;
			if (minTier == EEventTier.None) return true;
			if (minTier == EEventTier.C) return r.tier == EEventTier.C;
			return r.tier != EEventTier.C && r.tier >= minTier;
		}
		/// <summary>Visible as of `now`: known in advance (knownSince ≤ now) or already happened, and not canceled before min(now, instant).</summary>
		static bool visible(ref EventRec r, DateTime now)
		{
			if (r.knownSinceUtc > now && r.utc > now) return false;
			DateTime m = now < r.utc ? now : r.utc;
			return r.canceledSinceUtc > m;
		}
		static bool exPost(ref EventRec r) { return r.canceledSinceUtc > r.utc; }

		static int upper(EventRec[] a, DateTime t) { int lo = 0, hi = a.Length; while (lo < hi) { int m = (lo + hi) >> 1; if (a[m].utc <= t) lo = m + 1; else hi = m; } return lo; }

		// ------------------------------------------------------------------ API (EV-6.4–6.12)
		void dailyQuery() { if (!m_dailyWarned) { m_dailyWarned = true; warn("window queries on daily bars return false (EV-3.7)"); } }

		public bool NextEvent(DateTime nowUtc, EEventTier minTier, EEventClassMask mask, out EventRec e)
		{
			e = default(EventRec);
			int y = ToEt(nowUtc).Year;
			for (int yy = y; yy <= y + 1; ++yy)
			{
				var a = year(yy).recs;
				for (int i = upper(a, nowUtc); i < a.Length; ++i)
					if (tierOk(ref a[i], minTier, mask) && visible(ref a[i], nowUtc)) { e = a[i]; return true; }
			}
			return false;
		}
		public bool PrevEvent(DateTime nowUtc, EEventTier minTier, EEventClassMask mask, out EventRec e)
		{
			e = default(EventRec);
			int y = ToEt(nowUtc).Year;
			for (int yy = y; yy >= y - 1; --yy)
			{
				var a = year(yy).recs;
				for (int i = upper(a, nowUtc) - 1; i >= 0; --i)
					if (tierOk(ref a[i], minTier, mask) && visible(ref a[i], nowUtc)) { e = a[i]; return true; }
			}
			return false;
		}
		public bool NextEvent(EEventTier minTier, out EventRec e) { return NextEvent(m_now, minTier, EEventClassMask.Macro, out e); }
		public bool PrevEvent(EEventTier minTier, out EventRec e) { return PrevEvent(m_now, minTier, EEventClassMask.Macro, out e); }

		/// <summary>Minutes from the current bar close to the next qualifying event; NaN when none (empty table, beyond coverage).</summary>
		public double MinutesTo(EEventTier minTier, EEventClassMask mask = EEventClassMask.Macro)
		{ EventRec e; return NextEvent(m_now, minTier, mask, out e) ? (e.utc - m_now).TotalMinutes : double.NaN; }
		public double MinutesSince(EEventTier minTier, EEventClassMask mask = EEventClassMask.Macro)
		{ EventRec e; return PrevEvent(m_now, minTier, mask, out e) ? (m_now - e.utc).TotalMinutes : double.NaN; }
		public double MinutesSinceAt(DateTime nowUtc, EEventTier minTier, EEventClassMask mask = EEventClassMask.Macro)
		{ EventRec e; return PrevEvent(nowUtc, minTier, mask, out e) ? (nowUtc - e.utc).TotalMinutes : double.NaN; }

		/// <summary>True when the bar interval [open, close) overlaps [t − pre, t + post) of a qualifying event (EV-6.7). The
		/// pre-side needs the event known as of the bar close; an unknown (unscheduled) event only contributes [t, t + post).</summary>
		public bool IsInWindow(EEventTier minTier, int preMin, int postMin, EEventClassMask mask = EEventClassMask.Macro)
		{ EventRec e; return windowHit(minTier, preMin, postMin, mask, out e, true); }
		public bool IsInWindowAt(DateTime openUtc, DateTime closeUtc, EEventTier minTier, int preMin, int postMin, EEventClassMask mask, out EventRec e)
		{ return window(openUtc, closeUtc, minTier, preMin, postMin, mask, out e); }

		bool windowHit(EEventTier minTier, int preMin, int postMin, EEventClassMask mask, out EventRec e, bool warnDaily)
		{
			e = default(EventRec);
			if (m_disabled || m_now == DateTime.MinValue) return false;   // disabled (EV-4.6b) or no bar stepped yet
			if (DailyBars) { if (warnDaily) dailyQuery(); return false; }
			return window(m_open, m_now, minTier, preMin, postMin, mask, out e);
		}

		bool window(DateTime open, DateTime close, EEventTier minTier, int preMin, int postMin, EEventClassMask mask, out EventRec e)
		{
			e = default(EventRec);
			// EV-4.6b: an Unknown day of a kind in the mask blocks (fail closed) when allowPartialCoverage = false
			EEventKind uk;
			if (!config.allowPartialCoverage && m_unk.Count > 0)
			{
				DateTime td = TradingDayOf(close);
				if (CoverageBlocks(td, mask, minTier, out uk)) { e = EventRec.Make(uk, td, effTier(uk), EEventFlags.None); return true; }
			}
			TimeSpan pre = TimeSpan.FromMinutes(preMin), post = TimeSpan.FromMinutes(postMin);
			DateTime lo = open - post, hi = close + pre;
			int y0 = ToEt(lo).Year, y1 = ToEt(hi).Year;
			for (int y = y0; y <= y1; ++y)
			{
				var a = year(y).recs;
				for (int i = upper(a, lo); i < a.Length && a[i].utc < hi; ++i)
				{
					if (!tierOk(ref a[i], minTier, mask)) continue;
					DateTime u = a[i].utc;
					DateTime m = close < u ? close : u;
					if (a[i].canceledSinceUtc <= m) continue;
					bool known = a[i].knownSinceUtc <= close;
					bool hitW = known ? (open < u + post && u - pre < close) : (u < close && open < u + post);
					if (hitW) { e = a[i]; return true; }
				}
			}
			return false;
		}

		/// <summary>Blackout over Macro-class events only (R2.1-c): EIA/Energy never contributes; query IsInWindow with Energy.</summary>
		public bool EventBlackout { get { return m_blackout; } }
		public bool EventFlattenNow { get { return m_flattenNow; } }

		/// <summary>Events whose trading day is `day`, as-of the current bar close (cached; no allocation on a hit).</summary>
		public List<EventRec> EventsOn(DateTime day, EEventClassMask mask = EEventClassMask.All)
		{
			day = day.Date;
			long key = (day.Ticks << 3) | (long)mask;
			DayCache c;
			if (m_dayCache.TryGetValue(key, out c) && m_now >= c.from && m_now < c.until) return c.list;
			c = new DayCache { list = new List<EventRec>(), from = m_now, until = DateTime.MaxValue };
			foreach (var yy in new[] { day.Year - 1, day.Year })
			{
				var yr = year(yy); int[] idx;
				if (!yr.dayIdx.TryGetValue(day, out idx)) continue;
				foreach (int i in idx)
				{
					EventRec r = yr.recs[i];
					if (r.IsSpan || (r.Class & mask) == 0) continue;
					foreach (var t in new[] { r.knownSinceUtc, r.canceledSinceUtc, r.utc }) if (t > m_now && t < c.until) c.until = t;
					if (visible(ref r, m_now)) c.list.Add(r);
				}
			}
			m_dayCache[key] = c;
			return c.list;
		}
		public List<EventRec> EventsOnExPost(DateTime day, bool includeCanceled = false, EEventClassMask mask = EEventClassMask.All)
		{
			var l = new List<EventRec>();
			day = day.Date;
			foreach (var yy in new[] { day.Year - 1, day.Year })
			{
				var yr = year(yy); int[] idx;
				if (!yr.dayIdx.TryGetValue(day, out idx)) continue;
				foreach (int i in idx) { EventRec r = yr.recs[i]; if (!r.IsSpan && (r.Class & mask) != 0 && (includeCanceled || exPost(ref r))) l.Add(r); }
			}
			return l;
		}
		public List<EventRec> EventsInRange(DateTime fromDay, DateTime toDay, EEventClassMask mask = EEventClassMask.All)
		{ var l = new List<EventRec>(); for (DateTime d = fromDay.Date; d <= toDay.Date; d = d.AddDays(1)) l.AddRange(EventsOn(d, mask)); return l; }
		public List<EventRec> EventsInRangeExPost(DateTime fromDay, DateTime toDay, bool includeCanceled = false, EEventClassMask mask = EEventClassMask.All)
		{ var l = new List<EventRec>(); for (DateTime d = fromDay.Date; d <= toDay.Date; d = d.AddDays(1)) l.AddRange(EventsOnExPost(d, includeCanceled, mask)); return l; }

		/// <summary>Classify a trading day as-of its session open (default, EV-5) or as-of `asOfUtc`.</summary>
		public EventDay Classify(DateTime day) { return classify(day.Date, SessionOpenUtc(day.Date), false); }
		public EventDay Classify(DateTime day, DateTime asOfUtc) { return classify(day.Date, asOfUtc, false); }
		public EventDay ClassifyExPost(DateTime day) { return classify(day.Date, DateTime.MaxValue, true); }

		EventDay classify(DateTime day, DateTime asOf, bool exp)
		{
			EventDay d = new EventDay(); d.day = day;
			foreach (var yy in new[] { day.Year - 1, day.Year })
			{
				var yr = year(yy); int[] idx;
				if (!yr.dayIdx.TryGetValue(day, out idx)) continue;
				foreach (int i in idx)
				{
					EventRec r = yr.recs[i];
					if (r.IsSpan) continue;
					if (exp ? !exPost(ref r) : !(r.knownSinceUtc <= asOf && r.canceledSinceUtc > asOf)) continue;
					d.kindsMask |= 1UL << (int)r.kind;
					if (r.kind == EEventKind.EiaPetroleum && (r.flags & EEventFlags.RuleOnly) != 0) d.eiaRuleOnly = true;
					if (r.Class == EEventClassMask.Macro && EventConfig.IsMacroTier(r.tier))
					{   // R2.1-g: count / maxTier / firstRelease are Macro only
						d.count++;
						if (r.tier > d.maxTier) d.maxTier = r.tier;
						if (!d.firstReleaseEt.HasValue || r.etTime.TimeOfDay < d.firstReleaseEt.Value) d.firstReleaseEt = r.etTime.TimeOfDay;
					}
					switch (r.kind)
					{
						case EEventKind.FomcDecision: d.hasFomc = true; if ((r.flags & EEventFlags.Sep) != 0) d.hasSep = true; break;
						case EEventKind.HalfDay: d.isHalfDay = true; break;
						case EEventKind.QuarterlyExpiry: d.isQuarterlyExpiry = true; break;
						case EEventKind.MonthlyOpex: d.isOpex = true; break;
						case EEventKind.IndexRebalance: d.isRebalance = true; break;
					}
				}
			}
			d.isRollWeek = IsRollWeek(day);
			return d;
		}

		// ---- roll / expiry (EV-6.9) ----
		/// <summary>Trading day inside a RollWeek span [roll date, expiry date] (rule kind; the class mask does not apply).</summary>
		public bool IsRollWeek(DateTime day)
		{
			day = day.Date;
			DateTime q = nextQuarterlyExpiry(day);
			return day >= RollDateFor(q) && day <= q;
		}
		public bool IsRollWeek() { return m_day != DateTime.MinValue && IsRollWeek(m_day); }
		/// <summary>A roll splice (rollOn(1)) was seen on the current trading day.</summary>
		public bool IsRollDay() { return m_day != DateTime.MinValue && m_lastSpliceDay == m_day; }
		/// <summary>Globex sessions after `day` up to and including the next quarterly expiry date (0 on expiry day).</summary>
		public int SessionsToExpiry(DateTime day)
		{
			day = day.Date;
			DateTime q = nextQuarterlyExpiry(day);
			int n = 0;
			for (DateTime d = day.AddDays(1); d <= q; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !m_clock.IsGlobexClosedDay(d)) ++n;
			return n;
		}
		public int SessionsToExpiry() { return SessionsToExpiry(m_day); }
		static DateTime nextQuarterlyExpiry(DateTime day)
		{
			int y = day.Year, m = (day.Month + 2) / 3 * 3;
			DateTime q = EventRules.ExpiryDate(y, m);
			if (q < day) { m += 3; if (m > 12) { m -= 12; ++y; } q = EventRules.ExpiryDate(y, m); }
			return q;
		}
		public static DateTime RollDateFor(DateTime expiry) { return EventRules.RollDate(expiry.Year, expiry.Month); }
		/// <summary>Customary roll date of the quarterly contract expiring nearest on/after `day` (roll-splice tabulation).</summary>
		public static DateTime RuleRollDate(DateTime day) { return RollDateFor(nextQuarterlyExpiry(day.Date)); }

		/// <summary>Instant inside a span record of `kind` (RollWeek: [roll date 00:00 ET, expiry 09:30 ET)).</summary>
		public bool InSpan(EEventKind kind, DateTime utc)
		{
			int y = ToEt(utc).Year;
			for (int yy = y - 1; yy <= y; ++yy)
				foreach (var r in year(yy).recs)
					if (r.kind == kind && r.IsSpan && utc >= r.utc && utc < toUtc(r.etEnd)) return true;
			return false;
		}
		public bool InSpan(EEventKind kind) { return InSpan(kind, m_now); }

		/// <summary>The latest T1 instant at or before the current bar close (ET), false when none (empty table).</summary>
		public bool LastT1Instant(out DateTime et)
		{
			EventRec e;
			if (PrevEvent(m_now, EEventTier.T1, EEventClassMask.Macro, out e)) { et = e.etTime; return true; }
			et = DateTime.MinValue; return false;
		}
	}
	// </event-engine>
}
