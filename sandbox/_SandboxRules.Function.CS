using System;

namespace PowerLanguage.Strategy
{
	/// <summary>Sandbox-only decisions from 2026-11-15 20:06 ET. Not a study. GX reads FlatByHHMM in the exchange zone, so 1555 is 15:55 there.</summary>
	public enum ESandboxSide { Long, Short, Both }

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
