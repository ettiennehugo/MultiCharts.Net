using ATCenterProxy.interop;
using PowerLanguage.Function;
using System;
using System.Runtime.CompilerServices;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// THROW-AWAY TEST MONEY-MANAGEMENT SIGNAL for Portfolio Trader live checks P5b L1-L8 (specs/m-port-spec.md §9,
	/// specs/live-check-sheet.md Check 7). Apply via Portfolio Settings -> Money management signal -> Select. The strategies
	/// carry _TestPortArm (_TestPortArm.Strategy.CS) with the SAME Test/Variant. Stand-alone; delete after the sitting.
	///
	/// COMMON SETUP: see _TestPortArm header (ES/NQ 1 min dxFeed, L7 ES 1 min + CL 5 min, exposure 100%/100%, Strategy
	/// Properties size 1, backtest first; L6 also real-time on IBKR paper). Every Output line starts "P5b L&lt;n&gt; MM".
	/// L8 is free on every run: StartCalc prints "P5b L8 PortfolioCurrency(..)", first CalcBar prints
	/// "P5b L8 StrategyCurrencyCode #i(..)" per strategy.  PASS L8: both USD (eMTPA_MCSC_USD).
	/// L5 is free on every run: PASS = _TestPortMM appears in the Select list, and "P5b L5 MM selectable+running" is printed.
	///
	/// TEST / VARIANT / STEPS / EXPECTED (tick pass/fail from the journal; rows map 1:1 to Check 7)
	///  L1 (Variant a). Steps: backtest 1 session. MM denies all every call; AllowEntries=true for index 0 only on odd MM bars.
	///     Journal: "P5b L1 MM bar(n) odd(..) allow0(..)" each call; ARM "FILL entry time(..)".
	///     PASS: index 0 (@ES) entry fill time = open of the bar AFTER an odd MM bar (same-step effect); index 1 (@NQ) never fills.
	///     FAIL: index 0 fills after even bars (one-step lag) or index 1 fills.
	///  L2 (a/b/c + one run with NO MM). ARM codes 3 contracts; Strategy Properties size = 1. MM allows entries for all.
	///     a = EntryContracts never touched, b = EntryContracts=0 every call, c = EntryContracts=-1 every call.
	///     No-MM run: clear the Money management signal in Portfolio Settings (ARM alone, Test=L2).
	///     Journal: ARM "FILL entry ... size(n)"; MM "P5b L2 MM variant(..) EntryContracts #i before(..) after(..)".
	///     EXPECTED: no MM = 3 (code size, record it); a = 1 (default 0 => properties size; record the "before" value);
	///     b = 1; c = 3.  PASS: b = 1 and c = 3.
	///  L3 (a/b). ARM buys once and holds. MM: T = first MM bar on which index 0 is long and has been for 3 MM calls;
	///     ForceClosePosition() on T (once). a = AllowExits left true; b = AllowExits=false on index 0 every call.
	///     Journal: "P5b L3 MM FORCECLOSE T bar(..) time(..) close(..) allowExits(..)", then each later call
	///     "P5b L3 MM after-T pos(..) allowExits(..)" until flat, then "P5b L3 MM RESULT closed(Y/N) withAllowExitsFalse(..)";
	///     ARM "FILL exit time(..) price(..)".
	///     PASS: exit fill at T+1 open (compare price with ARM "barOpen") in a, and the close also happens in b
	///     (RESULT closed(Y) withAllowExitsFalse(True)). Record whether it filled at T close or T+1 open.
	///  L4 (a/b, BACKTEST ONLY). ARM buys once with a sell stop at entry - 10 ticks. a = Paused=true on index 0 as soon as
	///     it is long (control b = never pause). Journal: "P5b L4 MM PAUSED ..", then each call "P5b L4 MM paused(..) pos(..)
	///     stamp(..) stampAdvancing(..)"; ARM "STOP crossed .." and (if it fills) "FILL exit ..".
	///     RECORD for a: stop filled? (ARM FILL exit after STOP crossed = Y), pm.stamp still updates (stampAdvancing True /
	///     ARM STAMP lines continue)? b must show the stop filling (control). Documented either way = pass.
	///  L6 (a). ARM publishes a _TestPortCounter object. MM reads PortfolioData["test.counter"] by reference each call.
	///     Run 1 backtest; run 2 real-time PT on IBKR paper (auto-trading); then stop/restart PT and run again.
	///     Journal: MM "P5b L6 MM #i hash(..) publisherHash(..) sameRef(..) sameAsLastCall(..) value(..) created(..)";
	///     ARM "P5b L6 ARM COUNTER hash(..) value(..)".
	///     PASS: in backtest AND real time, MM hash == ARM hash (sameRef True) and MM value tracks the ARM value; after the
	///     restart the value starts again at 1 with a new created() time (no persistence, as assumed).
	///  L7 (a). Portfolio @ES 1 min (index 0) + @CL 5 min (index 1). Journal each MM call:
	///     "P5b L7 MM call(n) time(..) wall(..)" and "P5b L7 MM #i sym(..) stamp(..) lag(..)".
	///     PASS: an MM call on every 1-min step; CL stamp only advances every 5 min (lag 0-4 min).
	///  REG (G-R acceptance trace A-REG, phase3-specs §3.3). Strategies carry a _CustomSignal derivative with
	///     RegimeConfig.enabled = publish = true (backtest). MM allows all entries and, for each strategy, on every change of its
	///     pm.day prints "A-REG MM #i sym(..) day(..) pm.regime(..) pm.regimeP(p0,p1,p2|none|empty)" (one line per strategy per day).
	///     PASS: pm.regime = REGIME journal label + 1 of the previous close (0 while unknown); pm.regimeP equals that day's
	///     REGIME p(..) line; strategies on the same underlying agree; no line before the first REGIME daily line.
	/// </summary>
	public class _TestPortMM : PortfolioSignalObject
	{
		[Input] public string Test { get; set; }
		[Input] public string Variant { get; set; }

		private string m_t, m_v;
		private int m_calls, m_longCalls, m_l3State;   // L3: 0 waiting, 1 force-closed, 2 done
		private bool m_l8Done, m_l4Paused;
		private object m_lastRef;
		private System.Collections.Generic.Dictionary<int, string> m_regDay = new System.Collections.Generic.Dictionary<int, string>();
		private DateTime m_lastStamp;

		public _TestPortMM(object _ctx) : base(_ctx) { }

		protected override void Create() { Test = "L1"; Variant = "a"; }

		protected override void StartCalc()
		{
			m_t = (Test ?? "L1").Trim().ToUpperInvariant();
			m_v = (Variant ?? "a").Trim().ToLowerInvariant();
			m_calls = 0; m_longCalls = 0; m_l3State = 0; m_l8Done = false; m_l4Paused = false; m_lastRef = null; m_lastStamp = DateTime.MinValue;
			Output.WriteLine($"P5b L5 MM selectable+running test({m_t}) variant({m_v}) app({Environment.ApplicationCode}) appDomain({AppDomain.CurrentDomain.Id})");
			Output.WriteLine($"P5b L8 PortfolioCurrency({PortfolioCurrency}) usd({PortfolioCurrency == MTPA_MCSymbolCurrency.eMTPA_MCSC_USD})");
		}

		protected override void CalcBar()
		{
			m_calls++;
			int n = PortfolioStrategies.Count;
			if (!m_l8Done)
			{
				m_l8Done = true;
				Output.WriteLine($"P5b L5 MM first CalcBar strategies({n}) bars({Bars.Info.Name})");
				for (int i = 0; i < n; i++) { IStrategy s = sig(i); Output.WriteLine($"P5b L8 StrategyCurrencyCode #{i} sym({(s != null ? s.Bars.Info.Name : "?")}) code({(s != null ? s.StrategyCurrencyCode.ToString() : "?")})"); }
			}
			string tm = $"{Bars.Time[0]:yyyy-MM-dd HH:mm}";

			switch (m_t)
			{
				case "L1":
				{
					bool odd = Bars.CurrentBar % 2 == 1;
					for (int i = 0; i < n; i++) PortfolioStrategies[i].AllowEntries = false;
					if (odd && n > 0) PortfolioStrategies[0].AllowEntries = true;
					Output.WriteLine($"P5b L1 MM bar({Bars.CurrentBar}) time({tm}) odd({odd}) allow0({odd}) pos0({pos(0)}) pos1({pos(1)})");
					break;
				}
				case "L2":
					for (int i = 0; i < n; i++)
					{
						IPortfolioStrategy ps = PortfolioStrategies[i];
						ps.AllowEntries = true;
						int before = ps.EntryContracts;
						if (m_v == "b") ps.EntryContracts = 0; else if (m_v == "c") ps.EntryContracts = -1;
						if (m_calls <= 3 || pos(i) == 0) Output.WriteLine($"P5b L2 MM variant({m_v}) time({tm}) EntryContracts #{i} before({before}) after({ps.EntryContracts}) pos({pos(i)})");
					}
					break;
				case "L3":
				{
					allowAll(n);
					if (n == 0) break;
					IPortfolioStrategy p0 = PortfolioStrategies[0];
					if (m_v == "b") p0.AllowExits = false;
					int p = pos(0);
					if (m_l3State == 0)
					{
						if (p != 0) m_longCalls++;
						if (m_longCalls >= 3)
						{
							p0.ForceClosePosition();
							m_l3State = 1;
							Output.WriteLine($"P5b L3 MM FORCECLOSE T bar({Bars.CurrentBar}) time({tm}) close({sig(0).Bars.Close[0]}) pos({p}) allowExits({p0.AllowExits}) variant({m_v})");
						}
					}
					else if (m_l3State == 1)
					{
						Output.WriteLine($"P5b L3 MM after-T bar({Bars.CurrentBar}) time({tm}) pos({p}) allowExits({p0.AllowExits})");
						if (p == 0) { m_l3State = 2; Output.WriteLine($"P5b L3 MM RESULT closed(Y) time({tm}) withAllowExitsFalse({m_v == "b"})"); }
						else if (Bars.LastBarOnChart) Output.WriteLine($"P5b L3 MM RESULT closed(N) withAllowExitsFalse({m_v == "b"})");
					}
					break;
				}
				case "L4":
				{
					allowAll(n);
					if (n == 0) break;
					IPortfolioStrategy p0 = PortfolioStrategies[0];
					if (m_v != "b" && !m_l4Paused && pos(0) != 0) { p0.Paused = true; m_l4Paused = true; Output.WriteLine($"P5b L4 MM PAUSED #0 bar({Bars.CurrentBar}) time({tm}) pos({pos(0)})"); }
					object o = get(p0.PortfolioData, _PortKeys.Stamp);
					DateTime st = o is DateTime ? (DateTime)o : DateTime.MinValue;
					Output.WriteLine($"P5b L4 MM variant({m_v}) time({tm}) paused({p0.Paused}) pos({pos(0)}) stamp({st:yyyy-MM-dd HH:mm}) stampAdvancing({st > m_lastStamp})");
					m_lastStamp = st;
					break;
				}
				case "L6":
					allowAll(n);
					for (int i = 0; i < n; i++)
					{
						IPortfolioData pd = PortfolioStrategies[i].PortfolioData;
						object o = get(pd, _TestPortArm.KeyCounter);
						object h = get(pd, _TestPortArm.KeyCounterHash);
						_TestPortCounter c = o as _TestPortCounter;
						int hash = o != null ? RuntimeHelpers.GetHashCode(o) : 0;
						string val = c != null ? c.Value.ToString() : (o == null ? "null" : "type " + o.GetType().FullName);
						Output.WriteLine($"P5b L6 MM #{i} time({tm}) hash({hash}) publisherHash({h}) sameRef({h is int && (int)h == hash}) sameAsLastCall({(i == 0 ? ReferenceEquals(o, m_lastRef).ToString() : "-")}) value({val}) created({(c != null ? c.Created.ToString("HH:mm:ss.fff") : "-")}) rt({Environment.IsRealTimeCalc}) auto({Environment.IsAutoTradingMode}) appDomain({AppDomain.CurrentDomain.Id})");
						if (i == 0) m_lastRef = o;
					}
					break;
				case "L7":
					allowAll(n);
					Output.WriteLine($"P5b L7 MM call({m_calls}) time({Bars.Time[0]:yyyy-MM-dd HH:mm:ss}) wall({DateTime.Now:HH:mm:ss.fff})");
					for (int i = 0; i < n; i++)
					{
						object o = get(PortfolioStrategies[i].PortfolioData, _PortKeys.Stamp);
						IStrategy s = sig(i);
						string lag = o is DateTime ? (Bars.Time[0] - (DateTime)o).TotalMinutes.ToString("0.#") + "m" : "-";
						Output.WriteLine($"P5b L7 MM #{i} sym({(s != null ? s.Bars.Info.Name : "?")}) stamp({(o is DateTime ? ((DateTime)o).ToString("yyyy-MM-dd HH:mm") : "none")}) lag({lag})");
					}
					break;
				case "REG":
					allowAll(n);
					for (int i = 0; i < n; i++)
					{
						IPortfolioData pd = PortfolioStrategies[i].PortfolioData;
						object day = get(pd, _PortKeys.Day);
						string dk = day != null ? (day is DateTime ? ((DateTime)day).ToString("yyyy-MM-dd") : day.ToString()) : null;
						string last;
						if (dk == null || (m_regDay.TryGetValue(i, out last) && last == dk)) continue;
						m_regDay[i] = dk;
						IStrategy s = sig(i);
						object rg = get(pd, _PortKeys.Regime), rp = get(pd, _PortKeys.RegimeP);
						var pa = rp as double[];
						string ps = rp == null ? "none" : pa == null ? "?" : pa.Length == 0 ? "empty" : string.Join(",", Array.ConvertAll(pa, v => v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)));
						Output.WriteLine($"A-REG MM #{i} sym({(s != null ? s.Bars.Info.Name : "?")}) day({dk}) pm.regime({rg ?? "none"}) pm.regimeP({ps})");
					}
					break;
				default:
					allowAll(n);
					break;
			}
		}

		private void allowAll(int n) { for (int i = 0; i < n; i++) PortfolioStrategies[i].AllowEntries = true; }
		private IStrategy sig(int i) { if (i >= PortfolioStrategies.Count) return null; var s = PortfolioStrategies[i].Signals; return s != null && s.Length > 0 ? s[0] : null; }
		private int pos(int i) { IStrategy s = sig(i); return s != null ? s.PositionSide : 0; }
		private static object get(IPortfolioData pd, string key) { return pd != null && pd.ContainsKey(key) ? pd[key] : null; }
	}
}
