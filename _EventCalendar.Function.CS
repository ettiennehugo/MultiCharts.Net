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
	// PR A scope: rules, types, engine and API. The generated `// <event-table>` region (FOMC, CPI, NFP, ... from the official
	// sources, EV-4.2) and s_corrections (EV-5) are EMPTY until PR B (tools/m-event/build_table.py). With an empty table the
	// engine never throws: table kinds give neutral outputs (NaN / false / not present), Configure journals
	// "EVENT table missing: <kinds>" and every rule kind still generates.
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
		public int count;

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
		public bool allowPartialCoverage = false;
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

		/// <summary>Monday-before-third-Friday roll rule applies from this date (CME rolldates page; earlier years use the
		/// "Thursday eight calendar days before expiration" wording). VERIFY the cutover from archived CME pages in PR B.</summary>
		public static readonly DateTime RollRuleMondayFrom = new DateTime(2023, 1, 1);

		// ISM Report On Business release calendar. Source: https://www.ismworld.org/supply-management-news-and-reports/reports/rob-report-calendar/
		// (fetched 2026-10-22; 2026: Mfg Jan 5 ... Dec 1, Svc Jan 7 ... Dec 3, 10:00 ET). The 1st / 3rd NYSE business day rule reproduces
		// every 2026 date except January (ISM holiday Jan 2), listed here. Other years are RuleOnly (TODO PR B: archived calendars).
		static readonly int[] s_ismPublishedYears = { 2026 };
		static readonly long[] s_ismExceptions = {   // kind, yyyyMMdd
			(long)EEventKind.IsmMfg, 20260105,
			(long)EEventKind.IsmSvc, 20260107,
		};

		// EIA Weekly Petroleum Status Report alternate release schedule. Source: https://www.eia.gov/petroleum/supply/weekly/schedule.php
		// (fetched 2026-10-22). Pairs: original Wednesday (data week ending + 5 days) yyyyMMdd, alternate release yyyyMMddHHmm ET.
		// Releases before 2025-01-01 or after 2026-11-30 are RuleOnly (TODO PR B: Wayback captures of the same page).
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
		// reproduced by the rule). RuleOnly until PR B checks the archive.

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

		/// <summary>Standard (AM) expiry date: third Friday, or the preceding NYSE business day when it is a holiday.</summary>
		public static DateTime ExpiryDate(int y, int m) { return PrevBusinessDayOrSelf(ThirdFriday(y, m)); }
		/// <summary>Customary roll date (EV §1.2): Monday before the third Friday (from RollRuleMondayFrom), else the Thursday eight
		/// calendar days before it.</summary>
		public static DateTime RollDate(int y, int m)
		{
			DateTime f = ThirdFriday(y, m);
			return f >= RollRuleMondayFrom ? f.AddDays(-4) : f.AddDays(-8);
		}
		/// <summary>VIX futures final settlement: the Wednesday 30 days before the next month's standard SPX expiry (holiday:
		/// the preceding business day is used, then a holiday Wednesday moves to the preceding business day). VERIFY (PR B).</summary>
		public static DateTime VixExpiry(int y, int m)
		{
			DateTime n = new DateTime(y, m, 1).AddMonths(1);
			DateTime w = ExpiryDate(n.Year, n.Month).AddDays(-30);
			return PrevBusinessDayOrSelf(w);
		}

		static bool contains(int[] a, int v) { return Array.IndexOf(a, v) >= 0; }
		public static DateTime FromYmdhm(long v) { return new DateTime((int)(v / 100000000), (int)(v / 1000000 % 100), (int)(v / 10000 % 100), (int)(v / 100 % 100), (int)(v % 100), 0); }
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
				DateTime x = ExpiryDate(y, m);
				r.Add(EventRec.Make(EEventKind.MonthlyOpex, x + T0930, EEventTier.C, q ? EEventFlags.Quad : EEventFlags.None));
				r.Add(EventRec.Make(EEventKind.MonthlyOpex, x + T1600, EEventTier.C, EEventFlags.Pm | (q ? EEventFlags.Quad : EEventFlags.None)));
				if (q)
				{
					r.Add(EventRec.Make(EEventKind.QuarterlyExpiry, x + T0930, EEventTier.C, EEventFlags.Quad));
					r.Add(EventRec.Make(EEventKind.IndexRebalance, x + T1600, EEventTier.C));
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
			if (s_tableSha != null) return s_tableSha;
			var sb = new StringBuilder();
			for (int i = 0; i < s_yyyymmddhhmm.Length; ++i) sb.Append(s_yyyymmddhhmm[i]).Append(',').Append(s_kind[i]).Append(',').Append(s_flags[i]).Append(',').Append(s_ref[i]).Append(';');
			foreach (var c in s_corrections) sb.Append(c.kind).Append(',').Append(c.originalEt).Append(',').Append(c.actualEt).Append(',').Append(c.announcedEt).Append(',').Append(c.canceledSinceEt).Append(',').Append((int)c.flags).Append(';');
			s_tableSha = Sha256(Encoding.UTF8.GetBytes(sb.ToString())).Substring(0, 16);
			return s_tableSha;
		}
		public static string Sha256(byte[] b) { using (var h = SHA256.Create()) { var d = h.ComputeHash(b); var sb = new StringBuilder(64); foreach (var x in d) sb.Append(x.ToString("x2")); return sb.ToString(); } }
	}
	// </event-rules>

	// <event-table>
	// GENERATED by tools/m-event/build_table.py (PR B) from the official sources (EV-4.2). EMPTY in PR A.
	public static partial class EventRules
	{
		static readonly long[] s_yyyymmddhhmm = { };
		static readonly byte[] s_kind = { };
		static readonly ushort[] s_flags = { };
		static readonly string[] s_ref = { };
		static readonly EventCorrection[] s_corrections = { };
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
		/// <summary>Bar length (EV-3.3/3.7). Zero = non-time bars (open = previous bar close). &gt;= 1 day = daily bars.</summary>
		public TimeSpan barDuration = TimeSpan.Zero;
		/// <summary>First bar instant (coverage check, EV-4.7). MinValue = skip.</summary>
		public DateTime firstBarUtc = DateTime.MinValue;
		/// <summary>Test seams: replace the generated table / corrections (null = EventRules).</summary>
		public List<EventRec> table;
		public EventCorrection[] corrections;

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
			if (missing.Count > 0) jr("table missing: " + string.Join(",", missing));
			var cov = new StringBuilder();
			foreach (var k in EventRules.TableKinds)
			{
				if (!m_tableKindsPresent.Contains(k)) continue;
				DateTime a = DateTime.MaxValue, b = DateTime.MinValue;
				foreach (var r in m_table) if (r.kind == k) { if (r.etTime < a) a = r.etTime; if (r.etTime > b) b = r.etTime; }
				cov.Append(' ').Append(k).Append('(').Append(ymd(a)).Append("..").Append(ymd(b)).Append(')');
				if (firstBarUtc != DateTime.MinValue && toUtc(a) > firstBarUtc && !config.allowPartialCoverage)
					return label + ": coverage " + k + " starts " + ymd(a) + " after the first bar (set allowPartialCoverage)";
			}
			jr("coverage rules(Claims,IsmMfg,IsmSvc,EiaPetroleum,calendar)" + cov + " ruleOnly(Claims, ISM except 2026, EIA outside 2025-01..2026-11, VixExpiry)");
			if (firstBarUtc != DateTime.MinValue)
				foreach (var c in m_corr) { long t = c.actualEt != 0 ? c.actualEt : c.originalEt; if (t != 0 && toUtc(EventRules.FromYmdhm(t)) >= firstBarUtc) jr("correction " + c.kind + " " + (c.refPeriod ?? "") + " orig(" + c.originalEt + ") actual(" + (c.actualEt == 0 ? "Canceled" : c.actualEt.ToString()) + ") announced(" + c.announcedEt + ") " + c.flags + " " + (c.source ?? "")); }
			m_configured = true;
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
		Year year(int y)
		{
			Year yr;
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
			// spec-silent (EV-8.6): a holiday halt before the normal 17:00 close keeps the date (half-day events after the close)
			if (et.TimeOfDay < m_clock.GlobexClose && et.DayOfWeek != DayOfWeek.Saturday && et.DayOfWeek != DayOfWeek.Sunday && !m_clock.IsGlobexClosedDay(et.Date)) return et.Date;
			DateTime d = et.TimeOfDay >= m_clock.GlobexOpen ? et.Date.AddDays(2) : et.Date.AddDays(1);
			while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || m_clock.IsGlobexClosedDay(d)) d = d.AddDays(1);
			return d;
		}
		/// <summary>The session open (18:00 ET of the previous calendar day) of a trading date, in UTC — the default as-of point.</summary>
		public DateTime SessionOpenUtc(DateTime day)
		{
			return toUtc(day.Date.AddDays(-1) + m_clock.GlobexOpen);   // Monday: Sunday 18:00; after a closed day: its 18:00 reopen
		}

		// ------------------------------------------------------------------ step
		public void Step(DateTime barTime, DateTime barOpenUtc, DateTime barCloseUtc, bool rollSplice)
		{
			if (!m_configured) return;
			if (m_now == DateTime.MinValue && firstBarUtc == DateTime.MinValue) firstCoverageCheck(barCloseUtc);
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
				EventDay d = Classify(day);
				if (d.count > 0 && journal != null)
				{
					var ks = new StringBuilder();
					foreach (var r in EventsOn(day, EEventClassMask.All)) { if (ks.Length > 0) ks.Append(','); ks.Append(r.KindName).Append('@').Append(hm(r.etTime)); }
					jr("day " + ymd(day) + " at " + barTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " tier(" + d.maxTier + ") kinds(" + ks + ") firstRelease(" +
						(d.firstReleaseEt.HasValue ? d.firstReleaseEt.Value.ToString(@"hh\:mm") + " ET" : "-") + ")" + (d.isRollWeek ? " rollWeek" : "") + (d.isHalfDay ? " halfDay" : ""));
				}
			}
			// blackout (exposed only; the base never blocks orders)
			bool was = m_blackout;
			EventRec hit;
			m_blackout = (config.blackoutPreMin > 0 || config.blackoutPostMin > 0) && windowHit(config.blackoutTier, config.blackoutPreMin, config.blackoutPostMin, EEventClassMask.Macro, out hit, false) && (m_blackoutRec = hit).kind != EEventKind.None;
			m_flattenNow = config.blackoutFlatten && m_blackout && !was;
			if (m_blackout != was)
				jr("blackout " + (m_blackout ? "on" : "off") + " at " + barTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " " + m_blackoutRec.KindName + " " + m_blackoutRec.etTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " ET pre(" + config.blackoutPreMin + ") post(" + config.blackoutPostMin + ")" + (m_flattenNow ? " flatten" : ""));
		}

		/// <summary>EV-4.7 on the first bar when the host did not pass firstBarUtc: a table kind starting after the first bar is a
		/// warning (the run continues; the engine never throws mid-run) unless allowPartialCoverage.</summary>
		void firstCoverageCheck(DateTime first)
		{
			if (config.allowPartialCoverage) return;
			foreach (var k in m_tableKindsPresent)
			{
				DateTime a = DateTime.MaxValue;
				foreach (var r in m_table) if (r.kind == k && r.etTime < a) a = r.etTime;
				if (toUtc(a) > first) warn("coverage " + k + " starts " + ymd(a) + " after the first bar (set allowPartialCoverage)");
			}
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
			if (DailyBars) { if (warnDaily) dailyQuery(); return false; }
			return window(m_open, m_now, minTier, preMin, postMin, mask, out e);
		}

		bool window(DateTime open, DateTime close, EEventTier minTier, int preMin, int postMin, EEventClassMask mask, out EventRec e)
		{
			e = default(EventRec);
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
					d.count++;
					d.kindsMask |= 1UL << (int)r.kind;
					if (r.Class == EEventClassMask.Macro && EventConfig.IsMacroTier(r.tier))
					{
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
