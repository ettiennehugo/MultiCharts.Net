using System;

namespace PowerLanguage.Strategy
{
	/// <summary>Sandbox-only decisions from 2026-11-15 20:06 ET. Not a study. GX reads FlatByHHMM in the exchange zone, so 1555 is 15:55 there.</summary>
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
		public const int Flat1555 = 1555;

		/// <summary>0 means the standard 15:55 exchange flat. Any other value is an Eastern catalogue clock converted into the exchange zone.</summary>
		public static int FlatBy(int etOverrideHhmm, string exchange)
		{
			if (etOverrideHhmm <= 0) return Flat1555;
			return EtToExchangeHhmm(etOverrideHhmm, exchange);
		}

		public static int EtToExchangeHhmm(int etHhmm, string exchange)
		{
			return Shift(etHhmm, -LagMinutes(exchange));
		}

		/// <summary>sessionClock is Eastern. True once that Eastern clock has reached 15:55 in the exchange zone.</summary>
		public static bool PastFlat(TimeSpan etTod, string exchange)
		{
			int now = etTod.Hours * 100 + etTod.Minutes;
			int limit = Shift(Flat1555, LagMinutes(exchange));
			return now >= limit && now < 1800;
		}

		public static void ApplyPit(string name, ref TimeSpan start, ref TimeSpan end, ref TimeSpan valueEnd)
		{
			string root = Root(name);
			if (root == "CL") { start = new TimeSpan(9, 0, 0); end = new TimeSpan(14, 30, 0); valueEnd = end; }
			else if (root == "GC") { start = new TimeSpan(8, 20, 0); end = new TimeSpan(13, 30, 0); valueEnd = end; }
			else if (root == "ZN" || root == "6E") { start = new TimeSpan(8, 20, 0); end = new TimeSpan(15, 0, 0); valueEnd = end; }
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
			// Same tokens as _CustomSignal.exchangeTokenZoneId. Central is 60 minutes behind Eastern.
			// NYMEX and COMEX are Eastern in that map, so 1555 is 15:55 ET there.
			if (string.IsNullOrEmpty(exchange)) return 0;
			string e = exchange.ToUpperInvariant();
			string[] central = { "CME", "XCME", "GLOBEX", "CBOT", "ECBOT", "CBT", "XCBT", "CBOE" };
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
