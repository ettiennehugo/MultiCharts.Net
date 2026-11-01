using PowerLanguage.Function;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// THROW-AWAY TEST SIGNAL for live Check 10 (specs/live-check-sheet.md, M-PORT PR-C throttles, #72). Derived from _CustomSignal so
	/// every entry is armed through sizeForRisk (pm.R$, pm.qtyCode, pm.side) and the base publishes pm.day / pm.pos / pm.eq.
	/// Pair it with the MM _TestPortThrottleMM (_TestPortThrottleMM.Strategy.CS = _PortfolioRiskManager + a JSON ConfigPath).
	/// Delete both (and the folder below) after the sitting. No [Input]s besides the inherited TraceLevel.
	///
	/// BEHAVIOUR (deterministic, trades often, loses often)
	///   On every closed bar while flat: last bar Close &gt; Open =&gt; buy next bar market; Close &lt; Open =&gt; sell short; doji =&gt; nothing.
	///   Size = sizeForRisk(Bars, Close, Close -/+ stopTicks ticks, 0.005) (min 1; it arms pm.R$ = stopTicks x tick x BPV).
	///   Exits: protective stop at fill -/+ stopTicks ticks (every bar), time exit at market after holdBars bars in the trade.
	///   mode "reverse": in addition, while in a position held &gt;= 2 bars, a closed bar against the position sends the opposite
	///   entry (MC reverses long-&gt;short / short-&gt;long in one order, armed like any entry) - check (d).
	///   Output lines start "THR #&lt;symbol&gt;": START (settings), ARM side/size/R$, FILL entry/exit, REVERSE.
	///
	/// SETTINGS (no Input): optional file  %USERPROFILE%\Documents\_TestPortThrottle\signal.json , read at StartCalc, e.g.
	///   { "mode": "normal", "stopTicks": 4, "holdBars": 6 }      mode = normal | reverse ; defaults as shown (file missing = defaults).
	///   The same file applies to both strategies; edit it and recalculate (Ctrl+R / re-run the backtest) to switch.
	///
	/// SETUP: Portfolio Trader, 2 strategies with ONLY this signal: index 0 = @ES 5 min, index 1 = @CL 5 min (different pm.day keys),
	///   several weeks of data (one Sunday-evening session inside), Strategy Properties size 1, Exposure / Max % capital at risk 100%.
	///   Money management signal = _TestPortThrottleMM, which reads %USERPROFILE%\Documents\_TestPortThrottle\mm.json (missing = code
	///   defaults, Starter profile, RiskParity). Read the PowerLanguage .NET Editor Output (PORT ... lines from the MM, THR from here).
	///
	/// RUNS -> CHECKS
	///   Run 1 (RiskParity, mode normal)  mm.json: { "sizing": "RiskParity", "weeklyHalveR": -1, "weeklyObserveR": -2 }
	///          (a) session stop: 2 losing closes on one strategy =&gt; "PORT session stop" + "skip ... session stop" for that one only;
	///          (b) "PORT weekly halve" (EntryContracts ~ half) then "PORT weekly observe" / "skip ... weekly observe"; no reset on the
	///              Sunday-evening session, cleared at the first session of the next ISO week. (The -1/-2 overrides make this happen in
	///              days instead of weeks; a confirmation run with the defaults -3/-6 is optional.)
	///   Run 2 (CodeSize, mode normal)     mm.json: { "sizing": "CodeSize", "weeklyHalveR": -1, "weeklyObserveR": -2 }
	///          (c) once week R &lt;= -1 (halve) or ddSteps stage 1: "skip ... throttle under CodeSize", no order sent.
	///              To hit ddSteps quickly add  "ddSteps": [ { "dd": 0.002, "mult": 0.5 }, { "dd": 0.004, "mult": 0.25 }, { "dd": 0.5, "mult": 0.0 } ].
	///   Run 3 (RiskParity, mode reverse)  signal.json: { "mode": "reverse" }, mm.json as run 1 (or {} for defaults)
	///          (d) a THR "REVERSE long-&gt;short" followed by a closed trade whose R counts (session-stop count increases / weekly R moves).
	/// </summary>
	public class _TestPortThrottle : _CustomSignal
	{
		public const string Folder = "_TestPortThrottle";
		private IOrderMarket m_le, m_se, m_lxTime, m_sxTime;
		private IOrderPriced m_lxStop, m_sxStop;
		private bool m_reverse;
		private int m_stopTicks, m_holdBars, m_entryBar, m_lastPos;

		public _TestPortThrottle(object _ctx) : base(_ctx) { }

		/// <summary>%USERPROFILE%\Documents\_TestPortThrottle\&lt;name&gt; (shared with _TestPortThrottleMM).</summary>
		public static string SettingsFile(string name)
		{
			return Path.Combine(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), Folder), name);
		}

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "ThrLE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "ThrSE", EOrderAction.SellShort));
			m_lxTime = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "ThrLXt", EOrderAction.Sell, OrderExit.FromAll));
			m_sxTime = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "ThrSXt", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxStop = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "ThrLXs", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStop = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "ThrSXs", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_reverse = false; m_stopTicks = 4; m_holdBars = 6; m_entryBar = -1; m_lastPos = 0;
			string file = SettingsFile("signal.json"), note = "defaults (no file)";
			try
			{
				if (File.Exists(file))
				{
					string s = File.ReadAllText(file);
					Match m = Regex.Match(s, "\"mode\"\\s*:\\s*\"(\\w+)\"");
					if (m.Success) m_reverse = string.Equals(m.Groups[1].Value, "reverse", StringComparison.OrdinalIgnoreCase);
					m_stopTicks = readInt(s, "stopTicks", m_stopTicks, 1, 100);
					m_holdBars = readInt(s, "holdBars", m_holdBars, 1, 1000);
					note = "file " + file;
				}
			}
			catch (Exception ex) { note = "defaults (read failed: " + ex.Message + ")"; }
			j($"START mode({(m_reverse ? "reverse" : "normal")}) stopTicks({m_stopTicks}) holdBars({m_holdBars}) settings({note}) app({Environment.ApplicationCode})");
		}

		private static int readInt(string s, string key, int def, int min, int max)
		{
			Match m = Regex.Match(s, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
			int v;
			if (!m.Success || !int.TryParse(m.Groups[1].Value, out v) || v < min || v > max) return def;
			return v;
		}

		protected override void CalcBar()
		{
			base.CalcBar();   // publishes pm.day / pm.pos / pm.eq first; arming must come after (see _CustomSignal.portfolioBar)
			int pos = StrategyInfo.MarketPosition;
			if (pos != m_lastPos)
			{
				if (pos != 0 && (m_lastPos == 0 || Math.Sign(pos) != Math.Sign(m_lastPos))) m_entryBar = Bars.CurrentBar;
				j($"FILL pos({m_lastPos}->{pos}) avg({StrategyInfo.AvgEntryPrice}) bar({Bars.Time[0]:yyyy-MM-dd HH:mm}) netProfit({NetProfit:0.00})");
				m_lastPos = pos;
			}
			if (Bars.Status != EBarState.Close) return;
			double tick = tickSize();
			int dir = Bars.Close[0] > Bars.Open[0] ? 1 : Bars.Close[0] < Bars.Open[0] ? -1 : 0;

			if (pos != 0)
			{
				int held = m_entryBar < 0 ? 0 : Bars.CurrentBar - m_entryBar;
				double stop = pos > 0 ? StrategyInfo.AvgEntryPrice - m_stopTicks * tick : StrategyInfo.AvgEntryPrice + m_stopTicks * tick;
				portfolioStop(stop);
				if (m_reverse && held >= 2 && dir != 0 && dir != Math.Sign(pos))
				{
					j($"REVERSE {(pos > 0 ? "long->short" : "short->long")} held({held})");
					enter(dir, tick);   // the opposite entry closes the open position and opens the new one in one order
					return;
				}
				if (held >= m_holdBars) { if (pos > 0) m_lxTime.Send(); else m_sxTime.Send(); }
				else if (pos > 0) m_lxStop.Send(stop); else m_sxStop.Send(stop);
				return;
			}
			if (dir != 0) enter(dir, tick);
		}

		private void enter(int dir, double tick)
		{
			double entry = Bars.Close[0], stop = dir > 0 ? entry - m_stopTicks * tick : entry + m_stopTicks * tick;
			int qty = sizeForRisk(Bars, entry, stop, 0.005, default(SizingLimits));   // arms pm.R$ in Portfolio Trader
			if (qty <= 0) { qty = 1; portfolioArm(dir, entry, stop, qty); }        // equity unknown: still arm, code size 1
			j($"ARM side({dir}) qty({qty}) entry({entry}) stop({stop}) R$({Math.Abs(entry - stop) * Bars.Info.BigPointValue:0.00}) bar({Bars.Time[0]:yyyy-MM-dd HH:mm})");
			if (dir > 0) m_le.Send(qty); else m_se.Send(qty);
		}

		private void j(string msg) { Output.WriteLine($"THR #{Bars.Info.Name} {msg}"); }
	}
}
