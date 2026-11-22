using System;

namespace PowerLanguage.Strategy
{
	/// <summary>Sandbox-only decisions. GX reads FlatByHHMM in the exchange zone. The flat is five minutes before that market's regular close.</summary>
	public enum ESandboxSide { Long, Short, Both }

	public enum ESandboxGate
	{
		Off, ValueMigration, Balance, DayType, Noise, Trend, Compression, ThreeFamily, FitRegime
	}

	public enum ESandboxExit
	{
		Off, HvnTarget, NakedPoc, BalanceWidth, Partial1R, FixedR, Partial15R, BreakEven3R, FreeRoll
	}

	public enum ERest { Look, InPosition, Done, Resend }

	public static class _SandboxRules
	{
		/// <summary>Analyst MC17 run (A2-15): MaxBarsBack-safe lookback. Sandbox studies run at MaxBarsBack 49, so Bars.X[ago] past 49
		/// aborts the study. Studies read deeper Data1 history from an engine ring (requireRing in StartCalc) through this:
		/// the closed bar `ago` (0 = this bar), or a NaN bar with time MinValue while the ring is still shorter than ago.</summary>
		public static PowerLanguage.Function.RingBar RingAgo(PowerLanguage.Function._BarRing r, int ago)
		{
			if (r == null || ago < 0 || ago >= r.Count)
				return new PowerLanguage.Function.RingBar { bar = -1, time = DateTime.MinValue, tradingDay = DateTime.MinValue, o = double.NaN, h = double.NaN, l = double.NaN, c = double.NaN, v = double.NaN, up = double.NaN, down = double.NaN };
			return r[ago];
		}

		/// <summary>Regular close in US Eastern. The named pits and index futures come from the sandbox table. Anything else uses a daytime session-template end, then 16:00 ET (a stock close).</summary>
		public static TimeSpan RegularCloseEt(string symbol, TimeSpan listedSessionEnd)
		{
			string root = Root(symbol);
			if (root == "CL") return new TimeSpan(14, 30, 0);
			if (root == "GC") return new TimeSpan(13, 30, 0);
			if (root == "ZN" || root == "6E") return new TimeSpan(15, 0, 0);
			if (root == "ES" || root == "NQ" || root == "MES" || root == "MNQ" || root == "YM" || root == "RTY")
				return new TimeSpan(16, 0, 0);
			if (listedSessionEnd > new TimeSpan(12, 0, 0) && listedSessionEnd < new TimeSpan(17, 0, 0))
				return listedSessionEnd;
			return new TimeSpan(16, 0, 0);
		}

		public static TimeSpan DaytimeEnd(TimeSpan listedEnd)
		{
			if (listedEnd > new TimeSpan(12, 0, 0) && listedEnd < new TimeSpan(17, 0, 0)) return listedEnd;
			return TimeSpan.Zero;
		}

		/// <summary>A positive override is an Eastern catalogue clock. Zero is five minutes before the regular close, in the exchange zone.</summary>
		public static int FlatBy(int etOverrideHhmm, string exchange, string symbol, TimeSpan listedSessionEnd)
		{
			if (etOverrideHhmm > 0) return EtToExchangeHhmm(etOverrideHhmm, exchange);
			TimeSpan flat = RegularCloseEt(symbol, listedSessionEnd) - TimeSpan.FromMinutes(5);
			if (flat < TimeSpan.Zero) flat += TimeSpan.FromDays(1);
			return EtToExchangeHhmm(flat.Hours * 100 + flat.Minutes, exchange);
		}

		public static int FlatBy(int etOverrideHhmm, string exchange)
		{
			return FlatBy(etOverrideHhmm, exchange, "", TimeSpan.Zero);
		}

		public static int EtToExchangeHhmm(int etHhmm, string exchange)
		{
			return Shift(etHhmm, -LagMinutes(exchange));
		}

		/// <summary>sessionClock is Eastern. True once that clock is within five minutes of the regular close, and before 18:00 ET.</summary>
		public static bool PastFlat(TimeSpan etTod, string symbol, TimeSpan listedSessionEnd)
		{
			TimeSpan flat = RegularCloseEt(symbol, listedSessionEnd) - TimeSpan.FromMinutes(5);
			if (flat < TimeSpan.Zero) flat += TimeSpan.FromDays(1);
			int now = etTod.Hours * 100 + etTod.Minutes;
			int limit = flat.Hours * 100 + flat.Minutes;
			return now >= limit && now < 1800;
		}

		public static bool PastFlat(TimeSpan etTod, string exchange)
		{
			return PastFlat(etTod, "", TimeSpan.Zero);
		}

		/// <summary>Analyst MC17 (A2-35 held over Good Friday 2023): the session end on this bar's Globex trading date, in ET. The regular
		/// close, or the CME calendar's earlier Globex halt (holiday 13:00, Good Friday / mourning 09:15 / 09:30) or early RTH close (13:15),
		/// from _CmeHolidays.Rules (the M8 / VPF-6 table, keyed on the Globex trade date).</summary>
		public static TimeSpan SessionEndEt(DateTime globexDate, string symbol, TimeSpan listedSessionEnd)
		{
			TimeSpan end = RegularCloseEt(symbol, listedSessionEnd);
			PowerLanguage.Function._CmeHolidays.Rules.Day d = PowerLanguage.Function._CmeHolidays.Rules.Get(globexDate);
			if (d.HaltEt.HasValue && d.HaltEt.Value < end) end = d.HaltEt.Value;
			if (d.RthCloseEt.HasValue && d.RthCloseEt.Value < end) end = d.RthCloseEt.Value;
			return end;
		}

		/// <summary>The flat-by cutoff for this ET bar time: that day's session end minus five minutes. False on the evening part of a
		/// Globex date (at or after 18:00 ET the bar belongs to the next date) and on weekend bars.</summary>
		public static bool PastFlat(DateTime barEt, string symbol, TimeSpan listedSessionEnd)
		{
			DateTime gx = PowerLanguage.Function._CmeSessionClock.GlobexTradingDayEt(barEt);
			if (barEt.Date != gx.Date) return false;
			TimeSpan flat = SessionEndEt(gx, symbol, listedSessionEnd) - TimeSpan.FromMinutes(5);
			if (flat < TimeSpan.Zero) flat += TimeSpan.FromDays(1);
			int now = barEt.Hour * 100 + barEt.Minute;
			int limit = flat.Hours * 100 + flat.Minutes;
			return now >= limit && now < 1800;
		}

		/// <summary>True on a day whose session ends before the regular close (early close, holiday halt, abbreviated session) once the
		/// bar is at or past that day's end minus five minutes. The studies' own mustFlatten() exits use this; GX-managed positions get
		/// the early close from GX itself (PR #146). On a regular day this is false.</summary>
		public static bool EarlyFlatDue(DateTime barEt, string symbol, TimeSpan listedSessionEnd, out string why)
		{
			why = null;
			DateTime gx = PowerLanguage.Function._CmeSessionClock.GlobexTradingDayEt(barEt);
			if (barEt.Date != gx.Date) return false;
			TimeSpan regular = RegularCloseEt(symbol, listedSessionEnd), end = SessionEndEt(gx, symbol, listedSessionEnd);
			if (end >= regular || !PastFlat(barEt, symbol, listedSessionEnd)) return false;
			why = PowerLanguage.Function._CmeHolidays.Rules.Get(gx).Name + " session ends " + end.ToString(@"hh\:mm") + " ET";
			return true;
		}

		/// <summary>The reason a sandbox study that flattens with its own orders (YV-11) journals: that Globex date's session end in ET,
		/// named by the CME calendar on an early close or holiday halt, otherwise "regular". GX-managed studies journal FLATBY in GX's
		/// EXIT CLOSE line instead.</summary>
		public static string FlatByReason(DateTime barEt, string symbol, TimeSpan listedSessionEnd)
		{
			DateTime gx = PowerLanguage.Function._CmeSessionClock.GlobexTradingDayEt(barEt);
			TimeSpan regular = RegularCloseEt(symbol, listedSessionEnd), end = SessionEndEt(gx, symbol, listedSessionEnd);
			string name = end < regular ? PowerLanguage.Function._CmeHolidays.Rules.Get(gx).Name : "regular";
			return name + " session ends " + end.ToString(@"hh\:mm") + " ET";
		}

		public static bool EarlyFlatDue(DateTime barEt, string symbol, TimeSpan listedSessionEnd)
		{
			string why;
			return EarlyFlatDue(barEt, symbol, listedSessionEnd, out why);
		}

		/// <summary>Regular-session start, end, and value end. A futures base passes the three spans to ApplyPit.</summary>
		public static void PitWindows(string symbol, TimeSpan listedEnd, out TimeSpan start, out TimeSpan end, out TimeSpan valueEnd)
		{
			valueEnd = RegularCloseEt(symbol, listedEnd);
			end = valueEnd;
			start = new TimeSpan(9, 30, 0);
			string root = Root(symbol);
			if (root == "CL") start = new TimeSpan(9, 0, 0);
			else if (root == "GC" || root == "ZN" || root == "6E") start = new TimeSpan(8, 20, 0);
		}

		public static void ApplyPit(string name, ref TimeSpan start, ref TimeSpan end, ref TimeSpan valueEnd, TimeSpan listedEnd)
		{
			string root = Root(name);
			if (root == "CL") { start = new TimeSpan(9, 0, 0); end = new TimeSpan(14, 30, 0); }
			else if (root == "GC") { start = new TimeSpan(8, 20, 0); end = new TimeSpan(13, 30, 0); }
			else if (root == "ZN" || root == "6E") { start = new TimeSpan(8, 20, 0); end = new TimeSpan(15, 0, 0); }
			valueEnd = RegularCloseEt(name, listedEnd);
		}

		public static void ApplyPit(string name, ref TimeSpan start, ref TimeSpan end, ref TimeSpan valueEnd)
		{
			ApplyPit(name, ref start, ref end, ref valueEnd, valueEnd);
		}

		public static bool GateAllows(ESandboxGate gate, bool buy, double close, double priorPoc, double sessionHigh, double sessionLow, double atr, double sma, double priorClose, int families)
		{
			if (gate == ESandboxGate.Off) return true;
			if (gate == ESandboxGate.ValueMigration) return !double.IsNaN(priorPoc) && (buy ? close >= priorPoc : close <= priorPoc);
			if (gate == ESandboxGate.Balance) return atr > 0 && sessionHigh - sessionLow < atr;
			if (gate == ESandboxGate.DayType) return atr > 0 && sessionHigh - sessionLow >= atr;
			if (gate == ESandboxGate.Noise) return atr > 0 && sessionHigh - sessionLow < 0.5 * atr;
			if (gate == ESandboxGate.Trend) return !double.IsNaN(sma) && (buy ? priorClose > sma : priorClose < sma);
			if (gate == ESandboxGate.Compression) return atr > 0 && sessionHigh - sessionLow < 0.6 * atr;
			if (gate == ESandboxGate.ThreeFamily) return families >= 3;
			if (gate == ESandboxGate.FitRegime) return !double.IsNaN(sma) && (buy ? close >= sma : close <= sma);
			return true;
		}

		/// <summary>
		/// What a host gate reads, so StartCalc can turn exactly that on when the Gate input is not Off (Off needs nothing).
		/// ValueMigration reads the prior RTH profile (Own RTH profiles). Trend and FitRegime read the daily SMA200
		/// (NeedsDaily); compressionUsesDaily is for hosts whose Compression arm reads the daily ATR20 (PS-01). ThreeFamily
		/// reads the DVP confluence (DvpConfig). Balance, DayType and Noise read ATR(14) and the session only.
		/// </summary>
		public static void GateNeeds(ESandboxGate gate, bool compressionUsesDaily, out bool ownRth, out bool daily, out bool dvp)
		{
			ownRth = gate == ESandboxGate.ValueMigration;
			daily = gate == ESandboxGate.Trend || gate == ESandboxGate.FitRegime || (compressionUsesDaily && gate == ESandboxGate.Compression);
			dvp = gate == ESandboxGate.ThreeFamily;
		}

		/// <summary>True when the GateAllows arm for this gate reads the daily SMA200.</summary>
		public static bool GateReadsDaily(ESandboxGate gate) { return gate == ESandboxGate.Trend || gate == ESandboxGate.FitRegime; }

		/// <summary>
		/// Why the GateAllows inputs for this gate are not ready, or null when they are. A not-ready gate blocks (GateAllows
		/// would return false on the same inputs); the host logs the reason as a Warning once per day.
		/// </summary>
		public static string GateNotReady(ESandboxGate gate, double priorPoc, double sma, double priorClose, double atr, bool dvpOn)
		{
			switch (gate)
			{
				case ESandboxGate.Off: return null;
				case ESandboxGate.ValueMigration: return priorPoc > 0 ? null : "prior RTH profile not built yet";
				case ESandboxGate.Trend: return double.IsNaN(sma) ? "daily SMA200 not ready" : (priorClose > 0 ? null : "prior RTH close unknown");
				case ESandboxGate.FitRegime: return double.IsNaN(sma) ? "daily SMA200 not ready" : null;
				case ESandboxGate.ThreeFamily: return dvpOn ? null : "DVP engine off";
				case ESandboxGate.Balance:
				case ESandboxGate.DayType:
				case ESandboxGate.Noise:
				case ESandboxGate.Compression: return atr > 0 ? null : "ATR(14) not ready";
				default: return null;
			}
		}

		public static bool ScaleExit(ESandboxExit mode, out double t1R, out int scalePct, out bool breakEven)
		{
			t1R = 0; scalePct = 0; breakEven = false;
			if (mode == ESandboxExit.Partial1R) { t1R = 1; scalePct = 50; breakEven = true; return true; }
			if (mode == ESandboxExit.Partial15R) { t1R = 1.5; scalePct = 50; breakEven = true; return true; }
			if (mode == ESandboxExit.FreeRoll) { t1R = 2.5; scalePct = 50; breakEven = true; return true; }
			if (mode == ESandboxExit.BreakEven3R) { t1R = 3; scalePct = 0; breakEven = true; return true; }
			if (mode == ESandboxExit.FixedR) { t1R = 1; scalePct = 0; breakEven = false; return true; }
			return false;
		}

		public static int ScaleQty(ESandboxExit mode, bool headingScales)
		{
			if (headingScales) return 2;
			if (mode == ESandboxExit.Partial1R || mode == ESandboxExit.Partial15R || mode == ESandboxExit.FreeRoll) return 2;
			return 1;
		}

		public static ERest Rest(int position, ref int prev, ref bool filled, ref int fillAnchor, int anchor, ref bool restOn, bool windowOpen)
		{
			if (prev == 0 && position != 0) { filled = true; fillAnchor = anchor; }
			prev = position;
			if (position != 0) { restOn = false; return ERest.InPosition; }
			if (filled && fillAnchor == anchor) { restOn = false; return ERest.Done; }
			if (!restOn) return ERest.Look;
			if (!windowOpen) { restOn = false; return ERest.Done; }
			return ERest.Resend;
		}

		/// <summary>Coder 2026-11-22 (Analyst native retest of #152: A2-28 / A2-35 stopped on the fill bar, then re-entered): a position
		/// that opens and closes inside one bar never shows MarketPosition != 0 on a bar close, so the position test alone misses
		/// the fill. entriesToday is the strategy's entry count for this trading day from the position history (TradesToday); a
		/// rise since the last call is a fill on the resting anchor, whether or not the position is still open.</summary>
		public static ERest Rest(int position, int entriesToday, ref int seenEntries, ref int prev, ref bool filled, ref int fillAnchor, int anchor, ref bool restOn, bool windowOpen)
		{
			if (entriesToday > seenEntries) { filled = true; fillAnchor = anchor; }
			seenEntries = entriesToday;
			return Rest(position, ref prev, ref filled, ref fillAnchor, anchor, ref restOn, windowOpen);
		}

		public static bool Allows(ESandboxSide side, bool buy)
		{
			if (side == ESandboxSide.Both) return true;
			return buy ? side == ESandboxSide.Long : side == ESandboxSide.Short;
		}

		static int LagMinutes(string exchange)
		{
			// Central is 60 minutes behind Eastern. PR #141 puts NYMEX, COMEX, XNYM and XCEC on Central with CME and CBOT.
			if (string.IsNullOrEmpty(exchange)) return 0;
			string e = exchange.ToUpperInvariant();
			string[] central = { "CME", "XCME", "GLOBEX", "CBOT", "ECBOT", "CBT", "XCBT", "CBOE", "NYMEX", "COMEX", "XNYM", "XCEC" };
			for (int i = 0; i < central.Length; i++)
				if (Token(e, central[i])) return 60;
			return 0;
		}

		static bool Token(string exchange, string token)
		{
			int n = token.Length, i = 0;
			while (i + n <= exchange.Length)
			{
				int at = exchange.IndexOf(token, i, StringComparison.Ordinal);
				if (at < 0) return false;
				bool left = at == 0 || !char.IsLetterOrDigit(exchange[at - 1]);
				bool right = at + n == exchange.Length || !char.IsLetterOrDigit(exchange[at + n]);
				if (left && right) return true;
				i = at + 1;
			}
			return false;
		}

		static string Root(string name)
		{
			if (string.IsNullOrEmpty(name)) return "";
			int i = 0;
			while (i < name.Length && !char.IsLetter(name[i])) i++;
			int j = i;
			while (j < name.Length && char.IsLetter(name[j])) j++;
			if (j < name.Length && char.IsDigit(name[j]))
			{
				string with = name.Substring(i, j - i + 1).ToUpperInvariant();
				if (with == "6E") return with;
			}
			return name.Substring(i, j - i).ToUpperInvariant();
		}

		static int Shift(int hhmm, int minutes)
		{
			int mins = (hhmm / 100) * 60 + (hhmm % 100) + minutes;
			mins %= 24 * 60;
			if (mins < 0) mins += 24 * 60;
			return (mins / 60) * 100 + (mins % 60);
		}
	}
}
