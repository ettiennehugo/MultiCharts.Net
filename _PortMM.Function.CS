using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// M-PORT (specs/m-port-spec.md rev 1): portfolio money management for MultiCharts .NET Portfolio Trader.
	// Pure C# (no MultiCharts types) so the whole allocator is unit-tested off-MC (tests/PortMMTests).
	// _CustomSignal publishes the pm.* keys (_PortKeys) into MyPortfolioData; _PortfolioRiskManager (the PT money-management
	// signal) maps PortfolioStrategies to PortStratView, calls _PortMM.Decide and applies the PortDecisions
	// (AllowEntries*, EntryContracts, ForceClosePosition, Paused, Status). Exits are never blocked.
	// ============================================================================================

	/// <summary>Standard portfolio-data keys (spec §3).</summary>
	public static class _PortKeys
	{
		public const int Version = 1;
		public const string Ver = "pm.ver", Id = "pm.id", Family = "pm.family", Cluster = "pm.cluster";
		public const string Eq = "pm.eq", Pnl = "pm.pnl", Ret = "pm.ret", URet = "pm.uRet", Vol = "pm.vol";
		public const string RDollar = "pm.R$", QtyCode = "pm.qtyCode", RiskNew = "pm.riskNew", RiskOpen = "pm.riskOpen", OpenR = "pm.openR";
		/// <summary>Optional (rev 2 §3): $ per contract at the widened stop (cost floor); NaN/absent = pm.R$. Always written by portfolioArm.</summary>
		public const string RDollarW = "pm.R$w";
		public const string State = "pm.state", Side = "pm.side", Regime = "pm.regime", Score = "pm.score", Stamp = "pm.stamp", Day = "pm.day", Pos = "pm.pos";
		/// <summary>Optional (phase3 §3.3 / Q10): G-R filtered regime probabilities, double[] (empty = unknown), written at the session close
		/// only when RegimeConfig.publish is set; a new array each time (copy-on-write, never mutated after publishing).</summary>
		public const string RegimeP = "pm.regimeP";
		/// <summary>M-EVENT optional keys (EV-7.1), written only when PortfolioPublish &amp;&amp; EventConfig.publish &amp;&amp; the engine exists.
		/// EvtTier: max macro tier of the trading day (0 none, 1 T3, 2 T2, 3 T1), EvtMask: kinds bitmask (bits &lt; 53) — both once per
		/// trading day; EvtWin: 1 in a blackout window, else 0; EvtNext: minutes to the next blackoutTier event (NaN none) — every close.</summary>
		public const string EvtTier = "pm.evtTier";
		public const string EvtMask = "pm.evtMask";
		public const string EvtWin = "pm.evtWin";
		public const string EvtNext = "pm.evtNext";
		// book-level values written by the MM into CommonPortfolioData
		public const string Mult = "pm.mult", Heat = "pm.heat", Dd = "pm.dd", Rho = "pm.rho";
	}

	public enum EPortState { Flat = 0, Armed = 1, Long = 2, Short = 3, Exiting = 4, Halted = 5 }
	public enum EPortSizing { CodeSize = 0, RiskParity = 1, VolTarget = 2 }

	/// <summary>Immutable ring of (day, value) daily returns; Append returns a new instance (copy-on-write, safe to share).</summary>
	public sealed class _PortReturns
	{
		public const int DefaultCapacity = 260;
		private readonly DateTime[] m_day; private readonly double[] m_val;
		public int Count { get { return m_day.Length; } }
		public int Capacity { get; private set; }
		public DateTime LastDay { get { return m_day.Length == 0 ? DateTime.MinValue : m_day[m_day.Length - 1]; } }
		public _PortReturns(int capacity = DefaultCapacity) { Capacity = Math.Max(1, capacity); m_day = new DateTime[0]; m_val = new double[0]; }
		private _PortReturns(int capacity, DateTime[] d, double[] v) { Capacity = capacity; m_day = d; m_val = v; }
		public DateTime Day(int i) { return m_day[i]; }
		public double Value(int i) { return m_val[i]; }
		/// <summary>New instance with (day, value) appended (oldest dropped beyond Capacity). A day not after LastDay replaces nothing and returns this.</summary>
		public _PortReturns Append(DateTime day, double value)
		{
			if (double.IsNaN(value) || double.IsInfinity(value) || (m_day.Length > 0 && day <= LastDay)) return this;
			int keep = Math.Min(m_day.Length, Capacity - 1), skip = m_day.Length - keep;
			var d = new DateTime[keep + 1]; var v = new double[keep + 1];
			Array.Copy(m_day, skip, d, 0, keep); Array.Copy(m_val, skip, v, 0, keep);
			d[keep] = day.Date; v[keep] = value;
			return new _PortReturns(Capacity, d, v);
		}
		public Dictionary<DateTime, double> ToMap() { var m = new Dictionary<DateTime, double>(); for (int i = 0; i < m_day.Length; i++) m[m_day[i]] = m_val[i]; return m; }
	}

	public sealed class PortDdStep { public double dd; public double mult; public PortDdStep(double dd, double mult) { this.dd = dd; this.mult = mult; } }

	/// <summary>M-PORT configuration (spec §5.1). Code defaults; no [Input]. Override _PortfolioRiskManager.Config in a subclass to change.</summary>
	public sealed class PortMmConfig
	{
		public EPortSizing sizing = EPortSizing.RiskParity;
		public double bookVolTarget = 0.10;
		public double riskPerTrade = 0.005;
		/// <summary>Hard per-trade cap (spec §5.1 P1): n × widened R$ (pm.R$w, else pm.R$) &gt; this × book ⇒ skip, never resize. ≤ 0.01.</summary>
		public double maxTradeRisk = 0.01;
		public double heatCap = 0.04;
		public double clusterCap = 0.02;
		public double rhoMerge = 0.9;
		public int rhoWin = 60;
		public int rhoMinDays = 40;
		public int maxPositions = 6;
		public PortDdStep[] ddSteps = { new PortDdStep(0.05, 0.5), new PortDdStep(0.10, 0.25), new PortDdStep(0.15, 0.0) };
		public int ddRecoverHighDays = 20;
		public double dailyLossStop = 0.02;
		public double dailyLossFlatten = 0.03;
		public double stratKillR = -8;
		public int stratKillTrades = 20;
		/// <summary>§5.4 4a: losing closes (R &lt; -scratchR) in a strategy's own session (pm.day) that deny its entries for that session.</summary>
		public int stratDailyLosses = 2;
		/// <summary>|R| &lt;= scratchR is a scratch: not a loss for 4a, still summed into weekly R.</summary>
		public double scratchR = 0.05;
		/// <summary>§5.4 4b: weekly closed R &lt;= weeklyHalveR ⇒ size × weeklyHalveMult; &lt;= weeklyObserveR ⇒ entries denied (observe only).
		/// Both clear at the strategy's first session of the next ISO week (hard-coded; no Manual reset option).</summary>
		public double weeklyHalveR = -3, weeklyHalveMult = 0.5, weeklyObserveR = -6;
		public int staleBars = 3;
		public int minContracts = 1;
		/// <summary>MM evaluations after an approved entry with no position before the one-per-strategy "PT Max % Capital at Risk
		/// per Position" warning (MC does not expose that PT setting to studies; spec §8 step 5). 0 = off.</summary>
		public int fillCheckEvals = 3;
		public double idmCap = 2.5;
		/// <summary>Outer hard ceiling on the combined WIDENED open + new risk of all strategies, fraction of book (0.10 = 10%).
		/// Set by the _PortfolioRiskManager TotalRiskCapPct [Input] (spec §5.4 item 0); both it and heatCap apply, the tighter binds.</summary>
		public double totalRiskCap = 0.10;

		public string Validate()
		{
			var e = new List<string>();
			if (!(bookVolTarget > 0)) e.Add($"bookVolTarget={bookVolTarget}");
			if (!(riskPerTrade > 0 && riskPerTrade < 1)) e.Add($"riskPerTrade={riskPerTrade}");
			if (!(maxTradeRisk > 0 && maxTradeRisk <= 0.01 && riskPerTrade <= maxTradeRisk)) e.Add($"maxTradeRisk={maxTradeRisk} (0 < riskPerTrade <= maxTradeRisk <= 0.01)");
			if (!(heatCap > 0 && heatCap <= 1)) e.Add($"heatCap={heatCap}");
			if (!(clusterCap > 0 && clusterCap <= heatCap)) e.Add($"clusterCap={clusterCap} (0 < clusterCap <= heatCap)");
			if (!(rhoMerge > 0 && rhoMerge <= 1)) e.Add($"rhoMerge={rhoMerge}");
			if (rhoWin < 3 || rhoMinDays < 3 || rhoMinDays > rhoWin) e.Add($"rhoWin={rhoWin} rhoMinDays={rhoMinDays}");
			if (maxPositions < 1) e.Add($"maxPositions={maxPositions}");
			if (ddSteps == null) e.Add("ddSteps null");
			else for (int i = 0; i < ddSteps.Length; i++)
				if (!(ddSteps[i].dd > 0 && ddSteps[i].dd < 1) || ddSteps[i].mult < 0 || ddSteps[i].mult > 1 || (i > 0 && !(ddSteps[i].dd > ddSteps[i - 1].dd && ddSteps[i].mult <= ddSteps[i - 1].mult)))
				{ e.Add($"ddSteps[{i}]=({ddSteps[i].dd},{ddSteps[i].mult}) (dd ascending in (0,1), mult non-increasing in [0,1])"); break; }
			if (ddRecoverHighDays < 1) e.Add($"ddRecoverHighDays={ddRecoverHighDays}");
			if (!(dailyLossStop > 0) || !(dailyLossFlatten > dailyLossStop) || !(dailyLossFlatten < 1)) e.Add($"dailyLossStop={dailyLossStop} dailyLossFlatten={dailyLossFlatten} (0 < stop < flatten < 1)");
			if (!(stratKillR < 0) || stratKillTrades < 1) e.Add($"stratKillR={stratKillR} stratKillTrades={stratKillTrades}");
			if (stratDailyLosses < 1) e.Add($"stratDailyLosses={stratDailyLosses}");
			if (!(scratchR >= 0 && scratchR < 1)) e.Add($"scratchR={scratchR}");
			if (!(weeklyObserveR < weeklyHalveR && weeklyHalveR < 0)) e.Add($"weeklyObserveR={weeklyObserveR} weeklyHalveR={weeklyHalveR} (observe < halve < 0)");
			if (!(weeklyHalveMult > 0 && weeklyHalveMult < 1)) e.Add($"weeklyHalveMult={weeklyHalveMult}");
			if (staleBars < 1) e.Add($"staleBars={staleBars}");
			if (minContracts < 1) e.Add($"minContracts={minContracts}");
			if (fillCheckEvals < 0) e.Add($"fillCheckEvals={fillCheckEvals}");
			if (!(idmCap >= 1)) e.Add($"idmCap={idmCap}");
			if (!(totalRiskCap > 0 && totalRiskCap <= 1)) e.Add($"totalRiskCap={totalRiskCap} (0 < cap <= 1)");
			return e.Count == 0 ? null : "PortMmConfig invalid: " + string.Join(", ", e);
		}

		/// <summary>Deep copy (ddSteps cloned).</summary>
		public PortMmConfig Copy()
		{
			var c = (PortMmConfig)MemberwiseClone();
			if (ddSteps != null) { c.ddSteps = new PortDdStep[ddSteps.Length]; for (int i = 0; i < ddSteps.Length; i++) c.ddSteps[i] = new PortDdStep(ddSteps[i].dd, ddSteps[i].mult); }
			return c;
		}

		public override string ToString()
		{
			var dd = new List<string>(); if (ddSteps != null) foreach (var s in ddSteps) dd.Add($"{s.dd:0.###}->{s.mult:0.###}");
			return $"sizing({sizing}) riskPerTrade({riskPerTrade}) maxTradeRisk({maxTradeRisk}) volTarget({bookVolTarget}) heatCap({heatCap}) clusterCap({clusterCap}) rhoMerge({rhoMerge}) rhoWin({rhoWin}) " +
				$"maxPositions({maxPositions}) dd({string.Join(" ", dd)}) dailyStop({dailyLossStop}) dailyFlatten({dailyLossFlatten}) kill({stratKillR}R/{stratKillTrades}) session({stratDailyLosses} losses, scratch {scratchR}R) weekly(halve {weeklyHalveR}R x{weeklyHalveMult}, observe {weeklyObserveR}R) stale({staleBars}) minContracts({minContracts}) fillCheck({fillCheckEvals}) totalRiskCap({totalRiskCap})";
		}
	}

	/// <summary>
	/// M-PORT JSON configuration (spec §5.1 "Loaded from ConfigPath JSON if given; else defaults"). Field names are the
	/// PortMmConfig field names (case-insensitive); see samples/PortMmConfig.example.json. Partial JSON overrides only the
	/// fields present. Unknown fields, bad values and unreadable files produce warnings and fall back to the defaults: a bad
	/// field keeps its default; if the merged result fails PortMmConfig.Validate() the whole file is rejected (defaults).
	/// Never throws.
	/// </summary>
	public static class PortMmConfigLoader
	{
		/// <summary>Load from a file. Empty path = defaults (no warning). Returns the config to use (never null).</summary>
		public static PortMmConfig LoadFile(string path, PortMmConfig defaults, List<string> warnings, List<string> info)
		{
			if (string.IsNullOrWhiteSpace(path)) return defaults;
			string json;
			try
			{
				if (!File.Exists(path)) { warnings.Add($"PortMmConfig: file not found '{path}', using code defaults"); return defaults; }
				json = File.ReadAllText(path);
			}
			catch (Exception ex) { warnings.Add($"PortMmConfig: cannot read '{path}' ({ex.GetType().Name}: {ex.Message}), using code defaults"); return defaults; }
			PortMmConfig c = Parse(json, defaults, warnings, path);
			if (!ReferenceEquals(c, defaults)) info.Add($"PortMmConfig loaded from '{path}': {c}");
			return c;
		}

		/// <summary>Merge the JSON text onto a copy of the defaults. Returns defaults itself when the text is rejected as a whole.</summary>
		public static PortMmConfig Parse(string json, PortMmConfig defaults, List<string> warnings, string source = "json")
		{
			JObject o;
			try
			{
				JToken t = JToken.Parse(json ?? "");
				o = t as JObject;
				if (o == null) { warnings.Add($"PortMmConfig: {source} is not a JSON object, using code defaults"); return defaults; }
			}
			catch (JsonException ex) { warnings.Add($"PortMmConfig: {source} is not valid JSON ({ex.Message}), using code defaults"); return defaults; }

			PortMmConfig c = Clone(defaults);
			foreach (JProperty p in o.Properties())
			{
				string n = p.Name.ToLowerInvariant();
				JToken v = p.Value;
				switch (n)
				{
					case "sizing":
						{
							EPortSizing sz;
							if (v.Type == JTokenType.String && Enum.TryParse((string)v, true, out sz) && Enum.IsDefined(typeof(EPortSizing), sz)) c.sizing = sz;
							else bad(warnings, p, "CodeSize | RiskParity | VolTarget");
							break;
						}
					case "bookvoltarget": num(warnings, p, 0, 5, false, ref c.bookVolTarget); break;
					case "riskpertrade": num(warnings, p, 0, 1, false, ref c.riskPerTrade); break;
					case "maxtraderisk": num(warnings, p, 0, 1, false, ref c.maxTradeRisk); break;
					case "heatcap": num(warnings, p, 0, 1, true, ref c.heatCap); break;
					case "clustercap": num(warnings, p, 0, 1, true, ref c.clusterCap); break;
					case "rhomerge": num(warnings, p, 0, 1, true, ref c.rhoMerge); break;
					case "rhowin": integer(warnings, p, 3, 10000, ref c.rhoWin); break;
					case "rhomindays": integer(warnings, p, 3, 10000, ref c.rhoMinDays); break;
					case "maxpositions": integer(warnings, p, 1, 1000, ref c.maxPositions); break;
					case "ddsteps": ddSteps(warnings, p, c); break;
					case "ddrecoverhighdays": integer(warnings, p, 1, 1000, ref c.ddRecoverHighDays); break;
					case "dailylossstop": num(warnings, p, 0, 1, false, ref c.dailyLossStop); break;
					case "dailylossflatten": num(warnings, p, 0, 1, false, ref c.dailyLossFlatten); break;
					case "stratkillr":
						{
							double x = c.stratKillR;
							if (tryNum(v, out x) && x < 0) c.stratKillR = x; else bad(warnings, p, "a negative number of R");
							break;
						}
					case "stratkilltrades": integer(warnings, p, 1, 10000, ref c.stratKillTrades); break;
					case "stratdailylosses": integer(warnings, p, 1, 1000, ref c.stratDailyLosses); break;
					case "scratchr": num(warnings, p, 0, 1, true, ref c.scratchR); break;
					case "weeklyhalvemult": num(warnings, p, 0, 1, false, ref c.weeklyHalveMult); break;
					case "weeklyhalver":
					case "weeklyobserver":
						{
							double x;
							if (tryNum(v, out x) && x < 0) { if (n == "weeklyhalver") c.weeklyHalveR = x; else c.weeklyObserveR = x; } else bad(warnings, p, "a negative number of R");
							break;
						}
					case "stalebars": integer(warnings, p, 1, 10000, ref c.staleBars); break;
					case "mincontracts": integer(warnings, p, 1, 100000, ref c.minContracts); break;
					case "fillcheckevals": integer(warnings, p, 0, 1000, ref c.fillCheckEvals); break;
					case "idmcap": { double x; if (tryNum(v, out x) && x >= 1 && x <= 10) c.idmCap = x; else bad(warnings, p, "a number in [1, 10]"); break; }
					default:
						if (!n.StartsWith("_") && !n.StartsWith("$")) warnings.Add($"PortMmConfig: unknown field '{p.Name}' ignored");   // "_comment"-style keys are allowed
						break;
				}
			}
			string err = c.Validate();
			if (err != null) { warnings.Add($"PortMmConfig: {source} rejected ({err}), using code defaults"); return defaults; }
			return c;
		}

		public static PortMmConfig Clone(PortMmConfig d)
		{
			return d.Copy();
		}

		private static bool tryNum(JToken v, out double x)
		{
			x = double.NaN;
			if (v.Type == JTokenType.Float || v.Type == JTokenType.Integer) { x = v.Value<double>(); return !double.IsNaN(x) && !double.IsInfinity(x); }
			return false;
		}

		private static void num(List<string> w, JProperty p, double lo, double hi, bool hiInclusive, ref double field)
		{
			double x;
			if (tryNum(p.Value, out x) && x > lo && (hiInclusive ? x <= hi : x < hi)) field = x;
			else bad(w, p, string.Format(CultureInfo.InvariantCulture, "a number in ({0}, {1}{2}", lo, hi, hiInclusive ? "]" : ")"));
		}

		private static void integer(List<string> w, JProperty p, int lo, int hi, ref int field)
		{
			if (p.Value.Type == JTokenType.Integer)
			{
				long x = p.Value.Value<long>();
				if (x >= lo && x <= hi) { field = (int)x; return; }
			}
			bad(w, p, $"an integer in [{lo}, {hi}]");
		}

		private static void ddSteps(List<string> w, JProperty p, PortMmConfig c)
		{
			var a = p.Value as JArray;
			if (a == null) { bad(w, p, "an array of { \"dd\": x, \"mult\": y }"); return; }
			var steps = new List<PortDdStep>();
			foreach (JToken t in a)
			{
				var s = t as JObject; double dd, mult;
				if (s == null || s["dd"] == null || s["mult"] == null || !tryNum(s["dd"], out dd) || !tryNum(s["mult"], out mult))
				{ bad(w, p, "an array of { \"dd\": x, \"mult\": y }"); return; }
				steps.Add(new PortDdStep(dd, mult));
			}
			var tmp = new PortMmConfig { ddSteps = steps.ToArray() };
			string err = tmp.Validate();
			if (err != null && err.Contains("ddSteps")) { bad(w, p, "dd ascending in (0,1), mult non-increasing in [0,1]"); return; }
			c.ddSteps = steps.ToArray();
		}

		private static void bad(List<string> w, JProperty p, string expected)
		{
			w.Add($"PortMmConfig: bad value for '{p.Name}' ({p.Value.ToString(Formatting.None)}); expected {expected}; keeping the default");
		}
	}

	/// <summary>One strategy as the MM sees it this calculation (built by _PortfolioRiskManager from the published keys + IStrategy).</summary>
	public sealed class PortStratView
	{
		public int index;
		public string id = "";
		public bool publishes;          // pm.ver == _PortKeys.Version
		public bool stale;              // pm.stamp older than staleBars of its own bars
		public bool currencyOk = true;  // StrategyCurrencyCode == USD
		public bool paused;
		public EPortState state;
		public int side;                // pending entry side (+1/-1)
		public double rDollar = double.NaN;   // $ per contract of 1R
		public double rDollarW = double.NaN;  // pm.R$w: $ per contract at the widened stop (NaN = rDollar)
		public int qtyCode;
		public double riskOpen;
		public double vol = double.NaN; // $ daily vol per contract
		public string cluster = "";
		public int position;            // signed contracts (PositionSide x |CurrentPosition|), from IStrategy
		public double pnl;              // closed + open P&L in $
		public DateTime day = DateTime.MinValue;   // pm.day: the strategy's own trading day (4a/4b); MinValue ⇒ the book day
		public double score = double.NaN;
		public _PortReturns ret, uRet;
	}

	public sealed class PortDecision
	{
		public int index;
		public bool allowEntries, allowLong, allowShort;
		public int entryContracts = -1;  // ALWAYS applied: -1 = code size (entries denied anyway when allowEntries is false)
		public bool forceClose, pause;
		public string status = "";
		public string reason = "";
	}

	/// <summary>M-PORT rev 2 (spec §3, §5.1 step 5): pure publisher-side helpers for pm.R$w and the sizeForRisk 1% skip.
	/// _CustomSignal calls these so the off-MC tests (T-P19, T-P21) run the shipped logic.</summary>
	public static class _PortArm
	{
		/// <summary>Per-trade budget of the chart-side skip: a fixed 1% of equity (never the MM's riskPerTrade).</summary>
		public const double TradeBudgetFraction = 0.01;

		/// <summary>$ per contract at the widened stop; NaN when widenedStop is not given/finite or is closer to entry than the structural stop.</summary>
		public static double RDollarW(double entry, double stop, double widenedStop, double bpv)
		{
			if (double.IsNaN(widenedStop) || double.IsInfinity(widenedStop)) return double.NaN;
			double ds = Math.Abs(entry - stop), dw = Math.Abs(entry - widenedStop);
			if (!(dw >= ds)) return double.NaN;
			double r = dw * bpv;
			return r > 0 && !double.IsInfinity(r) ? r : double.NaN;
		}

		/// <summary>True for a finite widened stop closer to entry than the structural stop (a strategy bug: it is ignored, R$w = NaN).</summary>
		public static bool WidenedCloser(double entry, double stop, double widenedStop)
		{
			if (double.IsNaN(widenedStop) || double.IsInfinity(widenedStop)) return false;
			return Math.Abs(entry - widenedStop) < Math.Abs(entry - stop);
		}

		/// <summary>One warning per strategy instance: true (and sets the flag) the first time a closer widened stop is seen.</summary>
		public static bool WarnCloserOnce(ref bool warned, double entry, double stop, double widenedStop)
		{
			if (warned || !WidenedCloser(entry, stop, widenedStop)) return false;
			warned = true;
			return true;
		}

		/// <summary>pm.riskNew = Rw x qtyCode, Rw = R$ when R$w is NaN.</summary>
		public static double RiskNew(double rDollar, double rDollarW, int qtyCode)
		{
			return (double.IsNaN(rDollarW) ? rDollar : rDollarW) * Math.Max(0, qtyCode);
		}

		/// <summary>True when qty x widened R$ is strictly above the $ budget (exactly at the budget is allowed).</summary>
		public static bool OverTradeBudget(int qty, double rDollarW, double budget)
		{
			return qty > 0 && !double.IsNaN(rDollarW) && (double)qty * rDollarW > budget;
		}

		/// <summary>sizeForRisk skip: only when it will not arm (chart run or Data2); qty is the final qty (after maxQty, notional cap, round lots).</summary>
		public static bool SkipForBudget(bool willArm, int qty, double entry, double stop, double widenedStop, double bpv, double budget)
		{
			if (willArm) return false;
			return OverTradeBudget(qty, RDollarW(entry, stop, widenedStop, bpv), budget);
		}

		/// <summary>The keys portfolioArm writes; pm.R$w is always written (NaN when not widened) so no stale value survives.</summary>
		public static void Write(Action<string, object> set, double rDollar, double rDollarW, int qtyCode)
		{
			set(_PortKeys.RDollar, rDollar);
			set(_PortKeys.RDollarW, rDollarW);
			set(_PortKeys.QtyCode, qtyCode);
			set(_PortKeys.RiskNew, RiskNew(rDollar, rDollarW, qtyCode));
		}

		/// <summary>startPortfolio initialisation of pm.R$w.</summary>
		public static void Init(Action<string, object> set) { set(_PortKeys.RDollarW, double.NaN); }
	}

	/// <summary>M-PORT total open-risk cap (spec §5.4 item 0): the TotalRiskCapPct [Input] and the widened-risk check.</summary>
	public static class _PortRiskCap
	{
		public const double DefaultPct = 10;

		/// <summary>Input percent (0, 100] -> fraction. NaN/inf/<= 0 -> default 10% with a warning; > 100 -> clamped to 100% with a warning.</summary>
		public static double Fraction(double pct, out string warning)
		{
			warning = null;
			if (double.IsNaN(pct) || double.IsInfinity(pct) || pct <= 0)
			{ warning = $"TotalRiskCapPct({pct}) must be in (0, 100]: using the default {DefaultPct}%"; return DefaultPct / 100; }
			if (pct > 100) { warning = $"TotalRiskCapPct({pct}) > 100: clamped to 100%"; return 1.0; }
			return pct / 100;
		}

		/// <summary>Widened $ per contract: pm.R$w when finite and >= pm.R$, else pm.R$.</summary>
		public static double Rw(double rDollar, double rDollarW)
		{
			return double.IsNaN(rDollarW) || double.IsInfinity(rDollarW) || !(rDollarW >= rDollar) ? rDollar : rDollarW;
		}

		/// <summary>True when the new widened risk would push the combined total strictly over the cap (exactly at the cap is allowed). Unknown risk (NaN) breaks it.</summary>
		public static bool Breaks(double total, double newRisk, double capDollar)
		{
			if (double.IsNaN(newRisk) || newRisk < 0) return true;
			return total + newRisk > capDollar + 1e-9;
		}
	}

	/// <summary>Pure M-PORT math (spec §5.3, §5.5).</summary>
	public static class _PortMath
	{
		/// <summary>Pearson rho over the last `win` common days of two return maps (needs >= minDays common days, else NaN).</summary>
		public static double Pearson(_PortReturns a, _PortReturns b, int win, int minDays, Func<DateTime, bool> dayFilter = null)
		{
			if (a == null || b == null) return double.NaN;
			var mb = b.ToMap(); var xs = new List<double>(); var ys = new List<double>();
			for (int i = a.Count - 1; i >= 0 && xs.Count < win; i--)
			{
				double y; DateTime d = a.Day(i);
				if (mb.TryGetValue(d, out y) && (dayFilter == null || dayFilter(d))) { xs.Add(a.Value(i)); ys.Add(y); }
			}
			if (xs.Count < minDays) return double.NaN;
			return Corr(xs, ys);
		}

		public static double Corr(IList<double> x, IList<double> y)
		{
			int n = x.Count; if (n < 2) return double.NaN;
			double mx = 0, my = 0; for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; } mx /= n; my /= n;
			double sxy = 0, sxx = 0, syy = 0; for (int i = 0; i < n; i++) { double dx = x[i] - mx, dy = y[i] - my; sxy += dx * dy; sxx += dx * dx; syy += dy * dy; }
			return sxx > 0 && syy > 0 ? sxy / Math.Sqrt(sxx * syy) : double.NaN;
		}

		/// <summary>Union-find cluster ids: same seed always merges; pairs with rho[i,j] >= threshold merge (NaN never merges).</summary>
		public static int[] Clusters(string[] seeds, double[,] rho, double threshold)
		{
			int n = seeds.Length; var p = new int[n]; for (int i = 0; i < n; i++) p[i] = i;
			Func<int, int> find = null; find = i => p[i] == i ? i : (p[i] = find(p[i]));
			for (int i = 0; i < n; i++)
				for (int j = i + 1; j < n; j++)
					if ((!string.IsNullOrEmpty(seeds[i]) && seeds[i] == seeds[j]) || (rho != null && rho[i, j] >= threshold)) p[find(i)] = find(j);
			var id = new int[n]; for (int i = 0; i < n; i++) id[i] = find(i);
			return id;
		}

		/// <summary>Instrument diversification multiplier 1/sqrt(w'Rw), equal weights; missing rho = 0 (same cluster id = 1); capped.</summary>
		public static double Idm(int[] active, double[,] rho, int[] cluster, double cap)
		{
			int k = active.Length; if (k <= 1) return 1;
			double w = 1.0 / k, s = 0;
			for (int a = 0; a < k; a++)
				for (int b = 0; b < k; b++)
				{
					int i = active[a], j = active[b];
					double r = i == j ? 1 : (rho != null && !double.IsNaN(rho[i, j]) ? rho[i, j] : (cluster != null && cluster[i] == cluster[j] ? 1 : 0));
					s += w * w * r;
				}
			return s > 0 ? Math.Min(cap, 1 / Math.Sqrt(s)) : cap;
		}

		/// <summary>Drawdown stage implied by dd (0 = no throttle, i = ddSteps[i-1] hit).</summary>
		public static int DdStage(PortDdStep[] steps, double dd)
		{
			int st = 0; for (int i = 0; i < steps.Length; i++) if (dd >= steps[i].dd - 1e-12) st = i + 1; return st;
		}

		/// <summary>Contracts for a new entry (spec §5.3); -1 = code size; 0 = none.</summary>
		public static int Size(PortMmConfig c, double mult, double book, double rDollar, int qtyCode, double vol, int k, double idm)
		{
			switch (c.sizing)
			{
				case EPortSizing.CodeSize: return mult > 0 ? -1 : 0;
				case EPortSizing.VolTarget:
					if (!(vol > 0) || k < 1) return 0;
					return (int)Math.Floor(mult * (c.bookVolTarget / Math.Sqrt(252)) * book * idm / (k * vol) + 1e-9);
				default:
					if (!(rDollar > 0)) return 0;
					return (int)Math.Floor(mult * c.riskPerTrade * book / rDollar + 1e-9);
			}
		}
	}

	/// <summary>M-PORT allocator state + per-calc decision (spec §5.2, §5.4). One instance per MM run.</summary>
	internal sealed class _PortMM
	{
		public PortMmConfig config = new PortMmConfig();
		public Action<string> journal;

		private double m_peak = double.NaN, m_lastBook = double.NaN, m_dayOpenBook = double.NaN;
		private DateTime m_day = DateTime.MinValue;
		private int m_ddStage;
		private bool m_dayStopped, m_dayFlattened;
		private _PortReturns m_bookRet = new _PortReturns();
		private readonly List<double> m_dailyCloses = new List<double>();
		private readonly Dictionary<int, KillTracker> m_kill = new Dictionary<int, KillTracker>();
		private double[,] m_rho; private int[] m_clusterId; private string[] m_seeds; private DateTime m_rhoDay = DateTime.MinValue; private int m_rhoN = -1;

		private sealed class KillTracker
		{
			public int lastPos; public double pnlAtOpen; public double riskAtOpen; public readonly Queue<double> r = new Queue<double>(); public bool killed; public string reason = "";
			public DateTime sessDay = DateTime.MinValue, week = DateTime.MinValue; public int sessLosses; public double weekR;
		}

		public double Mult { get; private set; } = 1;
		public double Heat { get; private set; }
		public double Drawdown { get; private set; }
		public double Book { get; private set; }
		public double[,] Rho { get { return m_rho; } }
		public int DdStageNow { get { return m_ddStage; } }
		public bool DayStopped { get { return m_dayStopped; } }

		public string Configure() { return config.Validate(); }
		/// <summary>Combined widened open + accepted-new risk after the last Decide, $.</summary>
		public double TotalRisk { get; private set; }
		private readonly Dictionary<int, string> m_skipLogged = new Dictionary<int, string>();
		private readonly HashSet<int> m_skippedNow = new HashSet<int>();
		private readonly HashSet<int> m_rwWarned = new HashSet<int>();
		// approved-but-unfilled entries (lazy: allocated on the first approval); PT drops orders silently when its
		// "Max % Capital at Risk per Position" is below the approved risk, and studies cannot read that setting
		private sealed class PendingFill { public int qty; public double risk; public int evals; public bool approved; }
		private Dictionary<int, PendingFill> m_pendingFill;
		private HashSet<int> m_fillWarned;

		private void notePending(PortStratView s, int qty, double risk)
		{
			if (m_pendingFill == null) { m_pendingFill = new Dictionary<int, PendingFill>(); m_fillWarned = new HashSet<int>(); }
			PendingFill p;
			if (!m_pendingFill.TryGetValue(s.index, out p)) { p = new PendingFill(); m_pendingFill[s.index] = p; }
			p.qty = qty; p.risk = risk; p.approved = true;   // latest approval; evals keeps counting while it stays unfilled
		}

		/// <summary>One Warning per strategy when approved entries never turn into a position; re-armed after a fill.</summary>
		private void checkFills(IList<PortStratView> v)
		{
			if (m_pendingFill == null) return;
			foreach (var s in v)
			{
				if (s.position != 0) { m_pendingFill.Remove(s.index); m_fillWarned.Remove(s.index); continue; }   // filled -> re-arm
				PendingFill p;
				if (!m_pendingFill.TryGetValue(s.index, out p)) continue;
				if (s.state != EPortState.Armed || !p.approved) { m_pendingFill.Remove(s.index); continue; }   // withdrew or no longer approved: not a drop
				p.approved = false;
				if (++p.evals >= config.fillCheckEvals && m_fillWarned.Add(s.index))
					journal?.Invoke($"PORT Warning: {s.id}#{s.index} entry approved (qty {(p.qty)}, risk ${p.risk:0.00}) but no position after {p.evals} MM evaluations - " +
						"likely Portfolio Trader 'Max % Capital at Risk per Position' set below the approved risk; it must be >= 1% (maxTradeRisk)");
			}
		}


		/// <summary>Deny one entry attempt and journal it at Info once per (strategy, reason); re-logged when the reason changes or the skip clears.</summary>
		private void skip(PortDecision di, PortStratView s, string reason, string detail)
		{
			di.reason = reason; m_skippedNow.Add(s.index);
			string last;
			if (m_skipLogged.TryGetValue(s.index, out last) && last == reason) return;
			m_skipLogged[s.index] = reason;
			journal?.Invoke($"PORT Info: skip {s.id}#{s.index} {reason}: {detail}");
		}

		/// <summary>
		/// One MM calculation. day = trading-day key of the step; initialCapital = book starting capital; currencyOk = portfolio
		/// currency is USD. Returns one decision per view (same order). Never touches exits.
		/// </summary>
		public PortDecision[] Decide(DateTime day, double initialCapital, bool currencyOk, IList<PortStratView> v)
		{
			int n = v.Count; var d = new PortDecision[n]; m_skippedNow.Clear();
			for (int i = 0; i < n; i++) d[i] = new PortDecision { index = v[i].index };   // deny-all (Rank/Rotation pattern)

			double book = initialCapital; foreach (var s in v) if (s.publishes && !double.IsNaN(s.pnl)) book += s.pnl;
			Book = book;
			newDay(day.Date, book);
			if (double.IsNaN(m_peak) || book > m_peak) m_peak = book;
			Drawdown = m_peak > 0 ? Math.Max(0, 1 - book / m_peak) : 0;
			updateDdStage();
			double dayPnl = book - m_dayOpenBook;
			if (!m_dayStopped && dayPnl <= -config.dailyLossStop * m_dayOpenBook) { m_dayStopped = true; journal?.Invoke($"PORT daily loss stop {day:yyyy-MM-dd}: dayPnL({dayPnl:0}) <= -{config.dailyLossStop:P1} of {m_dayOpenBook:0} -> entries denied for the session"); }
			if (!m_dayFlattened && dayPnl <= -config.dailyLossFlatten * m_dayOpenBook) { m_dayFlattened = true; journal?.Invoke($"PORT daily loss flatten {day:yyyy-MM-dd}: dayPnL({dayPnl:0}) <= -{config.dailyLossFlatten:P1} -> ForceClosePosition all"); }
			double mult = m_ddStage == 0 ? 1 : config.ddSteps[m_ddStage - 1].mult;
			if (m_dayStopped) mult = 0;
			Mult = mult;

			updateKills(v, day);
			checkFills(v);
			updateClusters(day.Date, v);

			double heat = 0; var clHeat = new Dictionary<int, double>(); int open = 0;
			double capDollar = config.totalRiskCap * book;   // outer ceiling, widened risk (pm.riskOpen already uses the active = widened stop)
			for (int i = 0; i < n; i++)
			{
				var s = v[i]; double r = s.publishes && s.riskOpen > 0 ? s.riskOpen : 0;
				heat += r; int c = m_clusterId[i]; double h; clHeat.TryGetValue(c, out h); clHeat[c] = h + r;
				if (s.position != 0) open++;
			}

			// kill switches first: flatten, pause once flat
			for (int i = 0; i < n; i++)
			{
				var s = v[i]; KillTracker k = tracker(s.index);
				bool kill = k.killed || (s.publishes && s.state == EPortState.Halted);
				if (m_dayFlattened && s.position != 0) { d[i].forceClose = true; d[i].reason = "daily flatten"; }
				if (kill)
				{
					if (!k.killed) { k.killed = true; k.reason = s.state == EPortState.Halted ? "own halt" : k.reason; journal?.Invoke($"PORT kill {s.id}#{s.index}: {k.reason}"); }
					if (s.position != 0) { d[i].forceClose = true; d[i].reason = "kill: " + k.reason; } else if (!s.paused) d[i].pause = true;
				}
			}

			// candidates: Armed, valid publisher, not stale, not paused/killed, flat
			var cand = new List<int>();
			for (int i = 0; i < n; i++)
			{
				var s = v[i];
				string why = s.publishes ? null : "no pm.ver";
				if (why == null && s.stale) why = "stale";
				if (why == null && (!currencyOk || !s.currencyOk)) why = "currency not USD";
				if (why == null && (s.paused || tracker(s.index).killed)) why = "paused/killed";
				if (why == null && s.state != EPortState.Armed) why = s.state.ToString();
				if (why == null && s.position != 0) why = "in position";
				if (why == null && (s.side != 1 && s.side != -1)) why = "no side";
				if (why == null && !(s.rDollar > 0)) why = "no R$";   // CodeSize too: unmeasurable risk cannot pass heat/cluster/total caps
				if (why != null)
				{
					bool entryAttempt = why == "stale" || why == "no R$" || why == "currency not USD" || why == "paused/killed";
					if (entryAttempt)
					{   // a rejected entry attempt: journal at Info (once per strategy+reason), keep an earlier reason (e.g. kill)
						string prior = d[i].reason;
						skip(d[i], s, why, $"risk ${(s.rDollar > 0 ? s.rDollar * Math.Max(1, s.qtyCode) : double.NaN):0.00} pm.R$ {s.rDollar} pm.R$w {s.rDollarW} heat ${heat:0.00} cap ${config.totalRiskCap * book:0.00}");
						if (!string.IsNullOrEmpty(prior)) d[i].reason = prior;
					}
					else if (string.IsNullOrEmpty(d[i].reason)) d[i].reason = why;
					continue;
				}
				cand.Add(i);
			}
			double total = heat;
			cand.Sort((a, b) =>
			{
				double sa = v[a].score, sb = v[b].score;
				bool na = double.IsNaN(sa), nb = double.IsNaN(sb);
				if (na != nb) return na ? 1 : -1;
				if (!na && sa != sb) return sb.CompareTo(sa);
				return v[a].index.CompareTo(v[b].index);
			});

			int[] active = activeIdx(v);
			double idm = config.sizing == EPortSizing.VolTarget ? _PortMath.Idm(active, m_rho, m_clusterId, config.idmCap) : 1;
			double tradeCap = config.maxTradeRisk * book, heatCapD = config.heatCap * book, clusterCapD = config.clusterCap * book;
			foreach (int i in cand)
			{
				var s = v[i];
				double rw1 = _PortRiskCap.Rw(s.rDollar, s.rDollarW);   // widened $/contract; NaN/inf/closer-than-structural ⇒ structural
				if (!double.IsNaN(s.rDollarW) && !double.IsInfinity(s.rDollarW) && s.rDollarW < s.rDollar && m_rwWarned.Add(s.index))
					journal?.Invoke($"PORT Warning: {s.id}#{s.index} pm.R$w {s.rDollarW} < pm.R$ {s.rDollar} (bad publisher) - using pm.R$ (warned once)");
				if (mult <= 0) { skip(d[i], s, m_dayStopped ? "daily stop" : "dd throttle", $"mult({mult}) risk ${rw1 * Math.Max(0, s.qtyCode):0.00}/qtyCode total ${total:0.00} cap ${capDollar:0.00}"); continue; }
				string tw; double sm = throttle(s, out tw); KillTracker tk = tracker(s.index);
				if (sm <= 0)
				{
					skip(d[i], s, tw, tw == "session stop"
						? $"losses({tk.sessLosses}) >= {config.stratDailyLosses} on {tk.sessDay:yyyy-MM-dd} risk ${rw1 * Math.Max(1, s.qtyCode):0.00}/qtyCode budget ${tradeCap:0.00}"
						: $"PORT observe: week R {tk.weekR:0.00} <= {config.weeklyObserveR}R risk ${rw1 * Math.Max(1, s.qtyCode):0.00}/qtyCode budget ${tradeCap:0.00}");
					continue;
				}
				double m = mult * sm;   // approved throttles only (ddSteps x weekly halve); caps below never resize
				if (config.sizing == EPortSizing.CodeSize && m < 1)
				{   // T-P13: EntryContracts -1 cannot be scaled
					skip(d[i], s, "throttle under CodeSize", $"mult {m:0.###} (dd {mult:0.###} x {(tw ?? "none")} {sm:0.###}) qtyCode {s.qtyCode} risk ${rw1 * Math.Max(0, s.qtyCode):0.00} budget ${tradeCap:0.00}");
					continue;
				}
				if (open >= config.maxPositions) { skip(d[i], s, "maxPositions", $"open({open}) >= maxPositions({config.maxPositions}) total ${total:0.00} cap ${capDollar:0.00}"); continue; }
				int q = _PortMath.Size(config, m, book, s.rDollar, s.qtyCode, s.vol, Math.Max(1, active.Length), idm);
				int nq;   // contracts whose risk is checked; never changed by any cap below (skip only)
				if (q == -1)
				{   // CodeSize: the strategy's own qty
					if (s.qtyCode <= 0) { skip(d[i], s, "no qtyCode", $"qtyCode({s.qtyCode}) risk unknown cap ${tradeCap:0.00}"); continue; }
					nq = s.qtyCode;
				}
				else
				{
					if (q < config.minContracts) { skip(d[i], s, "size < minContracts", $"q({q}) risk ${q * rw1:0.00} total ${total:0.00} cap ${capDollar:0.00}"); continue; }
					nq = q;
				}
				int c = m_clusterId[i];
				double r = nq * rw1;   // WIDENED risk for every check (spec §5.2)
				if (r > tradeCap + 1e-9) { skip(d[i], s, "per-trade budget", $"widened risk ${r:0.00} (n {nq} x Rw {rw1:0.00}) > budget ${tradeCap:0.00} ({config.maxTradeRisk:P2} of book)"); continue; }
				if (heat + r > heatCapD + 1e-9) { skip(d[i], s, "heat cap", $"risk ${r:0.00} (n {nq}) heat ${heat:0.00} room ${Math.Max(0, heatCapD - heat):0.00} cap ${heatCapD:0.00}"); continue; }
				if (clHeat[c] + r > clusterCapD + 1e-9) { skip(d[i], s, "cluster cap", $"risk ${r:0.00} (n {nq}) cluster ${clHeat[c]:0.00} room ${Math.Max(0, clusterCapD - clHeat[c]):0.00} cap ${clusterCapD:0.00}"); continue; }
				if (_PortRiskCap.Breaks(total, r, capDollar)) { skip(d[i], s, "total risk cap", $"risk ${r:0.00} total ${total:0.00} cap ${capDollar:0.00}"); continue; }
				heat += r; clHeat[c] += r; total += r;
				if (config.fillCheckEvals > 0) notePending(s, nq, r);
				d[i].allowEntries = true; d[i].allowLong = s.side > 0; d[i].allowShort = s.side < 0; d[i].entryContracts = q; d[i].reason = "allowed";
				open++;
			}
			Heat = book > 0 ? heat / book : double.NaN;
			foreach (int k in new List<int>(m_skipLogged.Keys)) if (!m_skippedNow.Contains(k)) m_skipLogged.Remove(k);   // cleared -> re-log next time
			TotalRisk = total;

			for (int i = 0; i < n; i++)
			{
				var s = v[i];
				d[i].status = $"{s.state} n={(d[i].allowEntries ? d[i].entryContracts.ToString() : "-")} mult={mult:0.00} cl={m_clusterId[i]} {d[i].reason}";
			}
			m_lastBook = book;
			return d;
		}

		private int[] activeIdx(IList<PortStratView> v)
		{
			var a = new List<int>(); for (int i = 0; i < v.Count; i++) if (v[i].publishes && !tracker(v[i].index).killed) a.Add(i); return a.ToArray();
		}

		private KillTracker tracker(int index) { KillTracker k; if (!m_kill.TryGetValue(index, out k)) { k = new KillTracker(); m_kill[index] = k; } return k; }

		private void newDay(DateTime day, double book)
		{
			if (day == m_day) return;
			if (m_day != DateTime.MinValue && !double.IsNaN(m_lastBook))
			{
				if (m_dayOpenBook > 0) m_bookRet = m_bookRet.Append(m_day, m_lastBook / m_dayOpenBook - 1);
				m_dailyCloses.Add(m_lastBook); if (m_dailyCloses.Count > config.ddRecoverHighDays) m_dailyCloses.RemoveAt(0);
			}
			m_day = day; m_dayOpenBook = double.IsNaN(m_lastBook) ? book : m_lastBook; m_dayStopped = m_dayFlattened = false;
		}

		/// <summary>Stage rises immediately; falls to the dd-implied stage only at a new ddRecoverHighDays high or when dd is below half the current step.</summary>
		private void updateDdStage()
		{
			int implied = _PortMath.DdStage(config.ddSteps, Drawdown);
			if (implied > m_ddStage) { m_ddStage = implied; journal?.Invoke($"PORT drawdown {Drawdown:P1} -> stage {m_ddStage} mult {config.ddSteps[m_ddStage - 1].mult}"); return; }
			if (implied == m_ddStage || m_ddStage == 0) return;
			bool newHigh = m_dailyCloses.Count > 0 && Book >= max(m_dailyCloses);
			bool halfStep = Drawdown < config.ddSteps[m_ddStage - 1].dd / 2;
			if (newHigh || halfStep) { m_ddStage = implied; journal?.Invoke($"PORT drawdown recovered ({(newHigh ? "new high" : "dd < half step")}) dd {Drawdown:P1} -> stage {m_ddStage}"); }
		}

		private static double max(List<double> a) { double m = double.MinValue; foreach (var x in a) if (x > m) m = x; return m; }

		/// <summary>Closed-trade R per strategy: on position open remember pnl and initial risk (R$ x |position|); on flat, R = dPnl / risk.</summary>
		private void updateKills(IList<PortStratView> v, DateTime bookDay)
		{
			foreach (var s in v)
			{
				if (!s.publishes) continue;
				KillTracker k = tracker(s.index);
				DateTime sd = (s.day != DateTime.MinValue ? s.day : bookDay).Date;
				if (sd != k.sessDay) { k.sessDay = sd; k.sessLosses = 0; }   // 4a resets on the strategy's own new session
				DateTime wk = IsoWeek(sd);
				if (wk != k.week) { k.week = wk; k.weekR = 0; }               // 4b halve and observe both clear on a new ISO week
				bool flip = k.lastPos != 0 && s.position != 0 && Math.Sign(k.lastPos) != Math.Sign(s.position);   // reversal = close + open
				if (k.lastPos != 0 && (s.position == 0 || flip))
				{
					if (k.riskAtOpen > 0) closed(s, k, (s.pnl - k.pnlAtOpen) / k.riskAtOpen);
					else journal?.Invoke($"PORT kill-R missing {s.id}#{s.index}: trade opened without pm.R$, not recorded");
				}
				if (s.position != 0 && (k.lastPos == 0 || flip))
				{
					k.pnlAtOpen = s.pnl;
					k.riskAtOpen = s.rDollar > 0 ? s.rDollar * Math.Abs((double)s.position) : double.NaN;
				}
				k.lastPos = s.position;
			}
		}

		private void closed(PortStratView s, KillTracker k, double rr)
		{
			if (double.IsNaN(rr) || double.IsInfinity(rr)) return;
			k.r.Enqueue(rr); while (k.r.Count > config.stratKillTrades) k.r.Dequeue();
			double sum = 0; foreach (var x in k.r) sum += x;
			if (!k.killed && sum <= config.stratKillR) { k.killed = true; k.reason = $"cumulative {sum:0.0}R over last {k.r.Count} trades <= {config.stratKillR}R"; journal?.Invoke($"PORT kill {s.id}#{s.index}: {k.reason}"); }
			if (rr < -config.scratchR) k.sessLosses++;   // |R| <= scratchR is a scratch
			double before = k.weekR; k.weekR += rr;
			if (k.sessLosses == config.stratDailyLosses && rr < -config.scratchR) journal?.Invoke($"PORT session stop {s.id}#{s.index}: {k.sessLosses} losses on {k.sessDay:yyyy-MM-dd}, entries denied for the session");
			if (before > config.weeklyObserveR && k.weekR <= config.weeklyObserveR) journal?.Invoke($"PORT weekly observe {s.id}#{s.index}: week R {k.weekR:0.00} <= {config.weeklyObserveR}R, observe only until next ISO week");
			else if (before > config.weeklyHalveR && k.weekR <= config.weeklyHalveR) journal?.Invoke($"PORT weekly halve {s.id}#{s.index}: week R {k.weekR:0.00} <= {config.weeklyHalveR}R, size x{config.weeklyHalveMult} until next ISO week");
		}

		/// <summary>Monday of the ISO week of a trading day; a Sunday (evening ETH) key belongs to the following Monday's week.</summary>
		public static DateTime IsoWeek(DateTime d)
		{
			d = d.Date; if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
			return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
		}

		/// <summary>Per-strategy throttle multiplier (§5.2 gates 3–5) and its reason: 0 = session stop / weekly observe, weeklyHalveMult, or 1.</summary>
		private double throttle(PortStratView s, out string why)
		{
			why = null; KillTracker k = tracker(s.index);
			if (k.sessLosses >= config.stratDailyLosses) { why = "session stop"; return 0; }
			if (k.weekR <= config.weeklyObserveR) { why = "weekly observe"; return 0; }
			if (k.weekR <= config.weeklyHalveR) { why = "weekly halve"; return config.weeklyHalveMult; }
			return 1;
		}

		private void updateClusters(DateTime day, IList<PortStratView> v)
		{
			int n = v.Count;
			if (n == m_rhoN && day == m_rhoDay && m_clusterId != null) return;
			m_rhoN = n; m_rhoDay = day;
			m_seeds = new string[n]; for (int i = 0; i < n; i++) m_seeds[i] = v[i].cluster ?? "";
			m_rho = new double[n, n];
			var bm = m_bookRet.ToMap(); double sd = 0, mean = 0; int bc = m_bookRet.Count;
			if (bc > 1) { for (int i = 0; i < bc; i++) mean += m_bookRet.Value(i); mean /= bc; for (int i = 0; i < bc; i++) sd += Math.Pow(m_bookRet.Value(i) - mean, 2); sd = Math.Sqrt(sd / bc); }
			Func<DateTime, bool> stressed = dd => { double r; return sd > 0 && bm.TryGetValue(dd, out r) && r < mean - sd; };
			for (int i = 0; i < n; i++)
			{
				m_rho[i, i] = 1;
				for (int j = i + 1; j < n; j++)
				{
					_PortReturns a = pick(v[i]), b = pick(v[j]);
					double r = _PortMath.Pearson(a, b, config.rhoWin, config.rhoMinDays);
					double rs = _PortMath.Pearson(a, b, config.rhoWin, 10, stressed);
					double m = double.IsNaN(rs) ? r : (double.IsNaN(r) ? rs : Math.Max(r, rs));
					m_rho[i, j] = m_rho[j, i] = m;
				}
			}
			m_clusterId = _PortMath.Clusters(m_seeds, m_rho, config.rhoMerge);
		}

		/// <summary>Strategy returns when it has >= rhoMinDays of them, else the underlying's (spec §5.5).</summary>
		private _PortReturns pick(PortStratView s) { return s.ret != null && s.ret.Count >= config.rhoMinDays ? s.ret : s.uRet; }
	}
}
