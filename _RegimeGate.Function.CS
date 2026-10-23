using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// ================================================================================================================
	// _RegimeGate — first pre-registered consumer of the G-R regime engine (specs/regime-consumer-prereg.md rev 2, C1)
	// ================================================================================================================
	// MC-free (linked into tests/RegimeGateTests). Holds:
	//   - A263Config / A263Core: the A2-63 host core (catalogue A2-63, Analyst 2's pre-registered core, UNCHANGED):
	//     long-only buy stop at O + 0.5 x ATR20d, O = the 09:30 ET RTH open, active 09:30-15:30 ET, one entry per day,
	//     exit 15:55 ET, no stop, no target, no filter, 1 contract.
	//   - RegimeGateConfig / RegimeGate: arms U (ungated, journal only), G (skip post-hysteresis HMM Label 2, filtered
	//     posteriors, AsOf < t, !UsingFallback), A (engine forced to the RG-03 AtrTercile fallback with
	//     fitSessions = minSessions = 100000; skip tercile 2). The gate only ever removes the day's entry: exits, the 15:55
	//     flatten and size are never touched (prereg §2.2).
	//   - AtrTercileTracker: a mirror of the engine's RG-03 ring (same AtrPercentile / AtrBucket) so every arm journals the
	//     ATR tercile.
	//   - The REGGATE journal line (prereg §7) and the config hash (FNV-1a 64 over a canonical, culture-invariant string).
	// USE RULE (engine header): Regime is a prior / bucket, never an entry trigger; this file only BLOCKS entries.
	// ================================================================================================================

	public enum ERegimeGateArm { U = 0, G = 1, A = 2 }

	/// <summary>Gate configuration (no [Input]); set in code. Default arm U.</summary>
	public sealed class RegimeGateConfig
	{
		public ERegimeGateArm arm = ERegimeGateArm.U;
		/// <summary>Placebo stream seed (prereg §4.1, SplitMix64 _Rng); journaled only, the MC run draws nothing.</summary>
		public ulong placeboSeed = RegimeGate.PlaceboSeed;
		public string Validate()
		{
			if (arm != ERegimeGateArm.U && arm != ERegimeGateArm.G && arm != ERegimeGateArm.A) return "RegimeGateConfig invalid: arm=" + (int)arm;
			return null;
		}
	}

	/// <summary>A2-63 host core settings (pre-registered core; changing any value is a new trial, prereg §2.1).</summary>
	public sealed class A263Config
	{
		public double k = 0.5;                                    // buy stop = O + k x ATR20d
		public int atrLength = 20;                                // M-DAILY DailyConfig.atrLength
		public TimeSpan rthOpen = new TimeSpan(9, 30, 0);         // O = the first RTH bar's open
		public TimeSpan activeEnd = new TimeSpan(15, 30, 0);      // the stop is working 09:30-15:30 ET
		public TimeSpan exitTime = new TimeSpan(15, 55, 0);       // flat at 15:55 ET
		public TimeSpan rthClose = new TimeSpan(16, 0, 0);
		public int contracts = 1;
		/// <summary>CME early-close days (ruling 2): exit on the first bar closing at or after the early close minus this.</summary>
		public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
	}

	/// <summary>One session's gate decision, read once at the 09:30 latch and held for the session.</summary>
	public struct RegimeGateDecision
	{
		public DateTime session, asOf;
		public ERegimeGateArm arm;
		public int label, raw, atrTercile;
		public double pFilt;
		public bool fb, usable, wouldBlock, allow;
		/// <summary>Why the day is not usable: Ok, Warmup (engine not yet Valid / no label: warm-up or never fitted), Fallback (state Valid
		/// but the engine is on the RG-03 ATR fallback, in arms U/G; in arm A the fallback IS the gate and counts as Ok), Invalid (state
		/// Valid but otherwise not usable: AsOf not before t, label or posteriors out of range).</summary>
		public ERegimeGateReason reason;
	}

	public enum ERegimeGateReason { Ok = 0, Warmup = 1, Invalid = 2, Fallback = 3 }

	/// <summary>Per-arm running count of sessions and of days with no usable regime (rulings 3 / a), split into warm-up, invalid-label
	/// and ATR-fallback days (usable + warmup + invalid + fallback = sessions), plus CME no-RTH (holiday-halt) days (ruling b).</summary>
	public sealed class RegimeGateCounts
	{
		public int sessions, usable, warmup, invalid, fallback, blocked, noRth;
		public void Add(RegimeGateDecision d, bool noRthDay = false)
		{
			sessions++;
			if (d.reason == ERegimeGateReason.Ok) usable++;
			else if (d.reason == ERegimeGateReason.Warmup) warmup++;
			else if (d.reason == ERegimeGateReason.Fallback) fallback++;
			else invalid++;
			if (d.wouldBlock) blocked++;
			if (noRthDay) noRth++;
		}
		public string Suffix() { return " nSess(" + sessions + ") nUsable(" + usable + ") nWarm(" + warmup + ") nInv(" + invalid + ") nFb(" + fallback + ") nBlock(" + blocked + ") nNoRth(" + noRth + ")"; }
	}

	public static class RegimeGate
	{
		public const int BlockedLabel = 2;                  // stressed (highest f2 mean) / ATR tercile 2
		public const ulong PlaceboSeed = 20261022;          // prereg §4.1
		public const int ArmAFitSessions = 100000;          // prereg §2.3: never fitted -> AtrTercile fallback every day
		public const string Version = "A263-RG-2";

		/// <summary>Prereg §2.1 / §2.3: enabled, publish off; arm A forces fitSessions = minSessions = 100000. Nothing else changes.</summary>
		public static void ApplyArm(RegimeGateConfig g, RegimeConfig r)
		{
			r.enabled = true;
			r.publish = false;
			if (g.arm == ERegimeGateArm.A) { r.fitSessions = ArmAFitSessions; r.minSessions = ArmAFitSessions; }
		}

		/// <summary>
		/// Prereg §2.2 / §2.3. Usable iff Valid and AsOf &lt; session (and, except in arm A, !UsingFallback). wouldBlock = usable and
		/// Label == 2 (post-hysteresis label, never RawArgmax). allow = arm U, or not wouldBlock. A not-usable day is ALLOWED in
		/// every arm (the run is then U's trade list minus the blocked days; not-usable days are excluded from all arms offline).
		/// </summary>
		public static RegimeGateDecision Decide(ERegimeGateArm arm, RegimeState s, DateTime session, int atrTercile)
		{
			var d = new RegimeGateDecision { session = session.Date, arm = arm, label = -1, raw = -1, atrTercile = atrTercile, pFilt = double.NaN, asOf = DateTime.MinValue };
			if (s != null)
			{
				d.asOf = s.AsOf; d.label = s.Label; d.raw = s.RawArgmax; d.fb = s.UsingFallback;
				if (s.Probs != null && s.Label >= 0 && s.Label < s.Probs.Length) d.pFilt = s.Probs[s.Label];
				d.usable = s.Valid && s.Label >= 0 && s.Probs != null && s.Label < s.Probs.Length && s.AsOf != DateTime.MinValue && s.AsOf.Date < session.Date && (arm == ERegimeGateArm.A || !s.UsingFallback);
			}
			bool warm = s == null || !s.Valid || s.Label < 0 || s.AsOf == DateTime.MinValue;
			bool fbOnly = !warm && s.UsingFallback && arm != ERegimeGateArm.A && s.AsOf.Date < session.Date && s.Label < (s.Probs == null ? 0 : s.Probs.Length);
			d.reason = d.usable ? ERegimeGateReason.Ok : warm ? ERegimeGateReason.Warmup : fbOnly ? ERegimeGateReason.Fallback : ERegimeGateReason.Invalid;
			d.wouldBlock = d.usable && d.label == BlockedLabel;
			d.allow = arm == ERegimeGateArm.U || !d.wouldBlock;
			return d;
		}

		static readonly CultureInfo IC = CultureInfo.InvariantCulture;
		static string D(DateTime t) { return t == DateTime.MinValue ? "-" : t.ToString("yyyy-MM-dd", IC); }
		static string P(double v) { return double.IsNaN(v) ? "NaN" : v.ToString("0.####", IC); }

		/// <summary>Prereg §7 consumer line (+ atrT, wouldBlock, entry signal, config hash, placebo seed). One per session, every arm.</summary>
		public static string Line(RegimeGateDecision d, bool entrySignal, string cfgHash, ulong placeboSeed)
		{
			return "REGGATE " + D(d.session) + " arm(" + d.arm + ") asOf(" + D(d.asOf) + ") label(" + d.label + ") raw(" + d.raw + ") pFilt(" + P(d.pFilt) +
				") fb(" + (d.fb ? 1 : 0) + ") usable(" + (d.usable ? 1 : 0) + ") allow(" + (d.allow ? 1 : 0) + ") atrT(" + d.atrTercile +
				") wouldBlock(" + (d.wouldBlock ? 1 : 0) + ") why(" + d.reason + ") signal(" + (entrySignal ? 1 : 0) + ") cfg(" + cfgHash + ") seed(" + placeboSeed.ToString(IC) + ")";
		}

		/// <summary>Per-trade fields (prereg §7: the REGGATE fields of its session), for journalArm's context column.</summary>
		public static IDictionary<string, string> TradeFields(RegimeGateDecision d, string cfgHash, ulong placeboSeed)
		{
			return new SortedDictionary<string, string>(StringComparer.Ordinal)
			{
				{ "arm", d.arm.ToString() }, { "asOf", D(d.asOf) }, { "label", d.label.ToString(IC) }, { "raw", d.raw.ToString(IC) },
				{ "pFilt", P(d.pFilt) }, { "fb", d.fb ? "1" : "0" }, { "usable", d.usable ? "1" : "0" }, { "atrT", d.atrTercile.ToString(IC) },
				{ "wouldBlock", d.wouldBlock ? "1" : "0" }, { "why", d.reason.ToString() }, { "cfg", cfgHash }, { "seed", placeboSeed.ToString(IC) },
			};
		}

		/// <summary>
		/// Prereg §7 / P3 §0.5 hash field list: the named fields the config hash covers (RegimeConfig + gate arm/seed + host core).
		/// The hash is over exactly these names, sorted ordinally, as "name=value;" (invariant culture). It does NOT depend on the
		/// order fields are produced in, and fields added later (not in this list) do not change it. Changing this list is a new trial.
		/// </summary>
		public static readonly string[] HashFields =
		{
			"v",
			"rg.enabled", "rg.method", "rg.K", "rg.kSelect", "rg.features", "rg.fitSessions", "rg.minSessions", "rg.refit", "rg.refitEveryN",
			"rg.restarts", "rg.maxIter", "rg.tol", "rg.regCovar", "rg.transPrior", "rg.warmFilter", "rg.switchProb", "rg.switchMargin", "rg.seed",
			"rg.fallback", "rg.publish", "rg.atrPctLookback", "rg.parkinsonBars", "rg.bicDiagnostic", "rg.rollSuspectZ", "rg.gapBusinessDays",
			"gate.arm", "gate.blocked", "gate.seed",
			"host.k", "host.atrLength", "host.rthOpen", "host.activeEnd", "host.exitTime", "host.rthClose", "host.contracts", "host.halfDayExitBefore",
		};

		/// <summary>All config fields by name (a superset of HashFields is allowed; extras are journaled, not hashed).</summary>
		public static Dictionary<string, string> CanonFields(RegimeConfig r, RegimeGateConfig g, A263Config h)
		{
			var f = new Dictionary<string, string>(StringComparer.Ordinal);
			Func<double, string> n = v => v.ToString("R", IC);
			f["v"] = Version;
			f["rg.enabled"] = r.enabled ? "1" : "0"; f["rg.method"] = r.method.ToString(); f["rg.K"] = r.K.ToString(IC); f["rg.kSelect"] = r.kSelect.ToString();
			var fs = new List<string>(); if (r.features != null) foreach (var x in r.features) fs.Add(x.ToString());
			f["rg.features"] = string.Join(",", fs);
			f["rg.fitSessions"] = r.fitSessions.ToString(IC); f["rg.minSessions"] = r.minSessions.ToString(IC); f["rg.refit"] = r.refit.ToString();
			f["rg.refitEveryN"] = r.refitEveryN.ToString(IC); f["rg.restarts"] = r.restarts.ToString(IC); f["rg.maxIter"] = r.maxIter.ToString(IC);
			f["rg.tol"] = n(r.tol); f["rg.regCovar"] = n(r.regCovar); f["rg.transPrior"] = n(r.transPrior); f["rg.warmFilter"] = r.warmFilter.ToString(IC);
			f["rg.switchProb"] = n(r.switchProb); f["rg.switchMargin"] = n(r.switchMargin); f["rg.seed"] = r.seed.ToString(IC); f["rg.fallback"] = r.fallback.ToString();
			f["rg.publish"] = r.publish ? "1" : "0"; f["rg.atrPctLookback"] = r.atrPctLookback.ToString(IC); f["rg.parkinsonBars"] = r.parkinsonBars.ToString(IC);
			f["rg.bicDiagnostic"] = r.bicDiagnostic ? "1" : "0"; f["rg.rollSuspectZ"] = n(r.rollSuspectZ); f["rg.gapBusinessDays"] = r.gapBusinessDays.ToString(IC);
			f["gate.arm"] = g.arm.ToString(); f["gate.blocked"] = BlockedLabel.ToString(IC); f["gate.seed"] = g.placeboSeed.ToString(IC);
			f["host.k"] = n(h.k); f["host.atrLength"] = h.atrLength.ToString(IC); f["host.rthOpen"] = h.rthOpen.ToString("c", IC); f["host.activeEnd"] = h.activeEnd.ToString("c", IC);
			f["host.exitTime"] = h.exitTime.ToString("c", IC); f["host.rthClose"] = h.rthClose.ToString("c", IC); f["host.contracts"] = h.contracts.ToString(IC);
			f["host.halfDayExitBefore"] = h.halfDayExitBefore.ToString("c", IC);
			return f;
		}

		/// <summary>Canonical string over the named HashFields (sorted ordinally); throws if a named field is missing.</summary>
		public static string Canon(IDictionary<string, string> fields)
		{
			var names = new List<string>(HashFields); names.Sort(StringComparer.Ordinal);
			var sb = new StringBuilder();
			foreach (var k in names)
			{
				string v; if (!fields.TryGetValue(k, out v)) throw new ArgumentException("config hash field missing: " + k);
				sb.Append(k).Append('=').Append(v).Append(';');
			}
			return sb.ToString();
		}

		public static string Canon(RegimeConfig r, RegimeGateConfig g, A263Config h) { return Canon(CanonFields(r, g, h)); }

		/// <summary>FNV-1a 64 over the UTF-8 bytes of s, as 16 lower-case hex digits.</summary>
		public static string Fnv1a64(string s)
		{
			ulong h = 14695981039346656037UL;
			foreach (byte b in Encoding.UTF8.GetBytes(s ?? "")) { h ^= b; h *= 1099511628211UL; }
			return h.ToString("x16", IC);
		}

		public static string Hash(RegimeConfig r, RegimeGateConfig g, A263Config h) { return Fnv1a64(Canon(r, g, h)); }
		public static string Hash(IDictionary<string, string> fields) { return Fnv1a64(Canon(fields)); }
	}

	/// <summary>
	/// Mirror of the engine's RG-03 ring (_RegimeEngine.AddDay): for each completed day, the percentile of that day's ATR20d among
	/// the previous atrPctLookback values (computed before the push), then the push. Tercile = AtrBucket(pct, K), -1 until full.
	/// </summary>
	public sealed class AtrTercileTracker
	{
		readonly double[] m_ring; int m_count, m_next; readonly int m_K;
		public DateTime LastDate { get; private set; } = DateTime.MinValue;
		public int Tercile { get; private set; } = -1;
		public AtrTercileTracker(int lookback, int K) { m_ring = new double[Math.Max(3, lookback)]; m_K = K; }
		public void AddDay(DateTime date, double atr20d)
		{
			if (date == DateTime.MinValue || date <= LastDate) return;
			LastDate = date;
			Tercile = _RegimeEngine.AtrBucket(_RegimeEngine.AtrPercentile(m_ring, m_next, m_count, atr20d), m_K);
			if (atr20d > 0 && !double.IsInfinity(atr20d)) { m_ring[m_next] = atr20d; m_next = (m_next + 1) % m_ring.Length; if (m_count < m_ring.Length) m_count++; }
		}
	}

	/// <summary>What the host does on this closed bar (MC places the orders; the core only decides).</summary>
	public struct A263Action
	{
		public bool latched;        // this bar latched the session (O, threshold, gate decision)
		public bool sendStop;       // send the buy stop at `stop` for the next bar
		public double stop;
		public bool exit;           // send the market exit for the next bar
	}

	/// <summary>
	/// A2-63 core per closed bar. All times are exchange (ET) wall-clock times from the bar CLOSE stamp, so the windows hold through
	/// DST. The session latches on the first bar whose close is in (rthOpen, rthClose] on a new ET date: O = that bar's open,
	/// stop = O + k x atr20d. atr20d must be the M-DAILY Wilder ATR over COMPLETED PRIOR sessions only: if atrAsOf is not before the
	/// session date (or ATR is not finite, or the day has no RTH) there is no entry that day. The stop is sent on every closed bar
	/// with close time in [latch, min(activeEnd, ExitAt)) while flat, not yet entered today and allowed by the gate. ExitAt = exitTime,
	/// or on a CME early-close day min(exitTime, earlyClose - halfDayExitBefore). The exit is sent on the first closed bar with close
	/// time >= ExitAt while long, or on any later bar while long (catch-up). Known limitation: the stop works from the bar AFTER
	/// the latch bar, so a breakout inside the first bar is missed (small on 5m/1m).
	/// </summary>
	public sealed class A263Core
	{
		public readonly A263Config config;
		public A263Core(A263Config c) { config = c ?? new A263Config(); }
		public DateTime Session { get; private set; } = DateTime.MinValue;
		public double Open { get; private set; } = double.NaN;
		public double Atr { get; private set; } = double.NaN;
		public DateTime AtrAsOf { get; private set; } = DateTime.MinValue;
		public double Threshold { get; private set; } = double.NaN;
		public TimeSpan ExitAt { get; private set; }
		public TimeSpan? EarlyClose { get; private set; }
		public bool NoRth { get; private set; }
		public bool AtrRejected { get; private set; }   // ATR not from completed prior sessions (atrAsOf >= session)
		public bool Entered { get; private set; }
		public bool Allowed { get; private set; } = true;

		/// <summary>True when this bar starts a new RTH session (call Latch with the gate decision, then Step).</summary>
		public bool IsLatchBar(DateTime etClose)
		{
			TimeSpan t = etClose.TimeOfDay;
			return etClose.Date != Session && t > config.rthOpen && t <= config.rthClose;
		}

		/// <param name="earlyCloseEt">CME RTH early close (ET) for the session, null on a normal day (_CmeHolidays.Rules.Get(date).RthCloseEt).</param>
		/// <param name="noRth">CME day without an RTH session (holiday halt): no entry.</param>
		public void Latch(DateTime etClose, double barOpen, double atr20d, DateTime atrAsOf, bool allow, TimeSpan? earlyCloseEt = null, bool noRth = false)
		{
			Session = etClose.Date; Open = barOpen; Atr = atr20d; AtrAsOf = atrAsOf; Entered = false; Allowed = allow; EarlyClose = earlyCloseEt; NoRth = noRth;
			ExitAt = config.exitTime;
			if (earlyCloseEt.HasValue && earlyCloseEt.Value - config.halfDayExitBefore < ExitAt) ExitAt = earlyCloseEt.Value - config.halfDayExitBefore;
			AtrRejected = atrAsOf == DateTime.MinValue || atrAsOf.Date >= Session;
			Threshold = !noRth && !AtrRejected && atr20d > 0 && !double.IsInfinity(atr20d) && barOpen > 0 ? barOpen + config.k * atr20d : double.NaN;
		}

		/// <summary>Would the ungated core place an entry today (a valid threshold exists)? Journaled as signal().</summary>
		public bool HasSignal { get { return !double.IsNaN(Threshold); } }

		public A263Action Step(DateTime etClose, bool isLong)
		{
			var a = new A263Action { stop = double.NaN };
			if (isLong && etClose.Date == Session) Entered = true;
			TimeSpan t = etClose.TimeOfDay;
			if (isLong) { if (etClose.Date != Session || t >= ExitAt) a.exit = true; return a; }
			if (etClose.Date != Session || Entered || !Allowed || double.IsNaN(Threshold)) return a;
			if (t >= config.rthOpen && t < config.activeEnd && t < ExitAt) { a.sendStop = true; a.stop = Threshold; }
			return a;
		}

		/// <summary>Host fields for the REGGATE line (ruling 1/2: ATR at the open, its as-of date, the session's exit time).</summary>
		public string Suffix()
		{
			var ic = CultureInfo.InvariantCulture;
			return " O(" + (double.IsNaN(Open) ? "NaN" : Open.ToString("R", ic)) + ") atr20d(" + (double.IsNaN(Atr) ? "NaN" : Atr.ToString("R", ic)) + ") atrAsOf(" +
				(AtrAsOf == DateTime.MinValue ? "-" : AtrAsOf.ToString("yyyy-MM-dd", ic)) + ") atrOk(" + (AtrRejected ? 0 : 1) + ") thr(" +
				(double.IsNaN(Threshold) ? "NaN" : Threshold.ToString("R", ic)) + ") exitAt(" + ExitAt.ToString(@"hh\:mm", ic) + ") halfDay(" +
				(EarlyClose.HasValue ? 1 : 0) + ") noRth(" + (NoRth ? 1 : 0) + ")";
		}
	}
}
