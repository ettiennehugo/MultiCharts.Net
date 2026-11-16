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
		static readonly TimeSpan EquityOpen = new TimeSpan(9, 30, 0);
		static readonly TimeSpan EquityClose = new TimeSpan(16, 0, 0);

		/// <summary>US stock or ETF. ETF is <see cref="ESymbolCategory.Stock"/>. NYSE, ARCA, NASDAQ, AMEX and BATS are the cash venues. Their regular session is 09:30–16:00 ET, not the template's extended-hours end.</summary>
		public static bool UsEquity(string exchange, ESymbolCategory category)
		{
			if (category == ESymbolCategory.Stock) return true;
			if (string.IsNullOrEmpty(exchange)) return false;
			string e = exchange.ToUpperInvariant();
			string[] venues = { "NYSE", "XNYS", "NYSEARCA", "NASDAQ", "NASD", "XNAS", "ARCA", "ARCX", "AMEX", "XASE", "NYSEMKT", "NYSEAMEX", "BATS", "BZX", "BYX", "BATY" };
			for (int i = 0; i < venues.Length; i++)
				if (Token(e, venues[i])) return true;
			return false;
		}

		/// <summary>Regular close in US Eastern. A US stock or ETF is 16:00 ET. The named pits and index futures come from the sandbox table. Anything else uses a daytime session-template end, then 16:00 ET.</summary>
		public static TimeSpan RegularCloseEt(string symbol, TimeSpan listedSessionEnd, string exchange, ESymbolCategory category)
		{
			if (UsEquity(exchange, category)) return EquityClose;
			return RegularCloseEt(symbol, listedSessionEnd);
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
			return EquityClose;
		}

		public static TimeSpan DaytimeEnd(TimeSpan listedEnd)
		{
			if (listedEnd > new TimeSpan(12, 0, 0) && listedEnd < new TimeSpan(17, 0, 0)) return listedEnd;
			return TimeSpan.Zero;
		}

		/// <summary>A positive override is an Eastern catalogue clock. Zero is five minutes before the regular close, in the exchange zone.</summary>
		public static int FlatBy(int etOverrideHhmm, string exchange, string symbol, TimeSpan listedSessionEnd, ESymbolCategory category)
		{
			if (etOverrideHhmm > 0) return EtToExchangeHhmm(etOverrideHhmm, exchange);
			TimeSpan flat = RegularCloseEt(symbol, listedSessionEnd, exchange, category) - TimeSpan.FromMinutes(5);
			if (flat < TimeSpan.Zero) flat += TimeSpan.FromDays(1);
			return EtToExchangeHhmm(flat.Hours * 100 + flat.Minutes, exchange);
		}

		public static int FlatBy(int etOverrideHhmm, string exchange, string symbol, TimeSpan listedSessionEnd)
		{
			return FlatBy(etOverrideHhmm, exchange, symbol, listedSessionEnd, ESymbolCategory.None);
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
		public static bool PastFlat(TimeSpan etTod, string symbol, TimeSpan listedSessionEnd, string exchange, ESymbolCategory category)
		{
			TimeSpan flat = RegularCloseEt(symbol, listedSessionEnd, exchange, category) - TimeSpan.FromMinutes(5);
			if (flat < TimeSpan.Zero) flat += TimeSpan.FromDays(1);
			int now = etTod.Hours * 100 + etTod.Minutes;
			int limit = flat.Hours * 100 + flat.Minutes;
			return now >= limit && now < 1800;
		}

		public static bool PastFlat(TimeSpan etTod, string symbol, TimeSpan listedSessionEnd)
		{
			return PastFlat(etTod, symbol, listedSessionEnd, "", ESymbolCategory.None);
		}

		public static bool PastFlat(TimeSpan etTod, string exchange)
		{
			return PastFlat(etTod, "", TimeSpan.Zero);
		}

		/// <summary>Regular-session start, end, and value end. A futures base passes the three spans to ApplyPit.</summary>
		public static void PitWindows(string symbol, TimeSpan listedEnd, string exchange, ESymbolCategory category, out TimeSpan start, out TimeSpan end, out TimeSpan valueEnd)
		{
			if (UsEquity(exchange, category))
			{
				start = EquityOpen;
				end = EquityClose;
				valueEnd = EquityClose;
				return;
			}
			PitWindows(symbol, listedEnd, out start, out end, out valueEnd);
		}

		public static void PitWindows(string symbol, TimeSpan listedEnd, out TimeSpan start, out TimeSpan end, out TimeSpan valueEnd)
		{
			valueEnd = RegularCloseEt(symbol, listedEnd);
			end = valueEnd;
			start = EquityOpen;
			string root = Root(symbol);
			if (root == "CL") start = new TimeSpan(9, 0, 0);
			else if (root == "GC" || root == "ZN" || root == "6E") start = new TimeSpan(8, 20, 0);
		}

		public static void ApplyPit(string name, ref TimeSpan start, ref TimeSpan end, ref TimeSpan valueEnd, TimeSpan listedEnd, string exchange, ESymbolCategory category)
		{
			if (UsEquity(exchange, category))
			{
				start = EquityOpen;
				end = EquityClose;
				valueEnd = EquityClose;
				return;
			}
			ApplyPit(name, ref start, ref end, ref valueEnd, listedEnd);
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
