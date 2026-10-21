using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _DailyContext (spec missing-functionality-specs.md rev 4 §5, §0.3).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime D0 = new DateTime(2024, 1, 1);   // Monday

	static _DailyContext Ctx(Action<DailyConfig> f = null, List<string> j = null)
	{
		var c = new DailyConfig(); if (f != null) f(c);
		var d = new _DailyContext { config = c }; if (j != null) d.journal = s => j.Add(s);
		string e = d.Configure(); if (e != null) throw new Exception(e);
		return d;
	}
	static List<double[]> Days(int n, int seed, double start = 4000)
	{
		var r = new Random(seed); var l = new List<double[]>(); double p = start;
		for (int i = 0; i < n; i++) { double o = p + r.NextDouble() * 4 - 2, c = o + r.NextDouble() * 60 - 30; l.Add(new[] { o, Math.Max(o, c) + r.NextDouble() * 20, Math.Min(o, c) - r.NextDouble() * 20, c }); p = c; }
		return l;
	}

	static void Main()
	{
		Validation(); Indicators(); Roll(); OnRollIntraday(); Weeks(); OutOfOrder();
		Console.WriteLine($"DailyTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	static void Validation()
	{
		var c = new DailyConfig { atrLength = 0, smaLength = 0, tsmomLookbacks = new[] { 0 } };
		string e = c.Validate();
		Check(e != null && e.Contains("atrLength=0") && e.Contains("smaLength=0") && e.Contains("tsmomLookbacks contains 0"), "Validate names every bad value");
		var d = Ctx();
		Check(!d.Valid && double.IsNaN(d.Atr20d) && double.IsNaN(d.Sma200) && double.IsNaN(d.PrevClose) && double.IsNaN(d.Tsmom(63)), "neutral outputs before data");
	}

	static void Indicators()
	{
		var d = Ctx(); var days = Days(300, 1);
		for (int i = 0; i < days.Count; i++) d.AddDay(D0.AddDays(i), days[i][0], days[i][1], days[i][2], days[i][3]);
		// Wilder ATR: seed SMA of the first 20 TRs, then (prev*19 + tr)/20
		double atr = 0; for (int i = 1; i <= 20; i++) atr += tr(days, i); atr /= 20;
		for (int i = 21; i < days.Count; i++) atr = (atr * 19 + tr(days, i)) / 20;
		Check(Near(d.Atr20d, atr, 1e-6) && d.Valid, "Wilder ATR20d (seed = SMA of first 20 TR)");
		Check(Near(d.Sma200, days.Skip(100).Average(x => x[3]), 1e-6), "SMA200 of daily closes");
		Check(Near(d.Tsmom(252), days[299][3] / days[47][3] - 1, 1e-12) && Near(d.Tsmom(63), days[299][3] / days[236][3] - 1, 1e-12), "TSMOM(n) = C/C[n] - 1");
		double up = 0, dn = 0;
		for (int i = 1; i <= 2; i++) { double ch = days[i][3] - days[i - 1][3]; up += Math.Max(ch, 0); dn += Math.Max(-ch, 0); }
		up /= 2; dn /= 2;
		for (int i = 3; i < days.Count; i++) { double ch = days[i][3] - days[i - 1][3]; up = (up + Math.Max(ch, 0)) / 2; dn = (dn + Math.Max(-ch, 0)) / 2; }
		Check(Near(d.Rsi2, dn == 0 ? 100 : 100 - 100 / (1 + up / dn), 1e-9), "Wilder RSI(2)");
		var last = days[299];
		Check(d.PrevHigh == last[1] && d.PrevLow == last[2] && d.PrevClose == last[3] && Near(d.PrevRange, last[1] - last[2]), "previous day H/L/C/range");
		Check(Near(d.GapPct(last[3] * 1.01), 0.01), "GapPct vs previous close");
		var short20 = Ctx(); for (int i = 0; i < 20; i++) short20.AddDay(D0.AddDays(i), days[i][0], days[i][1], days[i][2], days[i][3]);
		Check(double.IsNaN(short20.Atr20d) && !short20.Valid, "ATR NaN until 20 TRs (21 days)");
	}
	static double tr(List<double[]> d, int i) { double pc = d[i - 1][3]; return Math.Max(d[i][1], pc) - Math.Min(d[i][2], pc); }

	static void Roll()
	{
		var j = new List<string>();
		var d = Ctx(null, j); var days = Days(260, 2);
		for (int i = 0; i < 250; i++) d.AddDay(D0.AddDays(i), days[i][0], days[i][1], days[i][2], days[i][3]);
		double prevClose = d.PrevClose, smaBefore = d.Sma200, tsBefore = d.Tsmom(63);
		const double off = 25;   // new contract trades 25 above the old one
		d.AddDay(D0.AddDays(250), prevClose + off + 3, prevClose + off + 10, prevClose + off - 5, prevClose + off + 6, true, off);
		Check(j.Any(s => s.Contains("roll reset") && s.Contains("25")) && !double.IsNaN(smaBefore), "roll journaled with the offset");
		// SMA after the roll day: previous 199 adjusted closes + roll-day close, all in new-contract space
		double expect = (Enumerable.Range(51, 199).Sum(i => days[i][3] + off) + prevClose + off + 6) / 200;
		Check(Near(d.Sma200, expect, 1e-6), "SMA200 difference-adjusted over the splice");
		Check(double.IsNaN(d.Atr20d), "ATR seed restarts at the roll (splice TR skipped)");
		Check(Near(d.Tsmom(1), 6 / (prevClose + off), 1e-12), "return series ratio-continuous: roll-day return excludes the splice gap");
		Check(Near(d.Tsmom(63) + 1, (tsBefore + 1) * (prevClose + off + 6) / (prevClose + off) * days[186][3] / days[187][3], 1e-9), "TSMOM over the splice uses ratio-adjusted closes");
		for (int i = 251; i < 271; i++) { double b = prevClose + off; d.AddDay(D0.AddDays(i), b, b + 10, b - 10, b); }
		Check(!double.IsNaN(d.Atr20d) && Near(d.Atr20d, 20, 1e-9), "ATR valid again after 20 post-roll TRs, from new-contract days only");
	}

	static void OnRollIntraday()
	{
		var d = Ctx(); var days = Days(30, 3);
		for (int i = 0; i < 30; i++) d.AddDay(D0.AddDays(i), days[i][0], days[i][1], days[i][2], days[i][3]);
		double pc = d.PrevClose, ph = d.PrevHigh;
		d.OnRoll(D0.AddDays(30), 12);
		Check(Near(d.PrevClose, pc + 12) && Near(d.PrevHigh, ph + 12) && Near(d.GapPct(pc + 12), 0), "OnRoll adjusts history immediately (GapPct uses the adjusted close)");
		d.AddDay(D0.AddDays(30), pc + 12, pc + 20, pc + 5, pc + 15);
		Check(double.IsNaN(d.Atr20d), "day after OnRoll: TR skipped, ATR reseeding");
	}

	static void Weeks()
	{
		var d = Ctx();
		// week 1: Mon..Fri highs 10..14 lows 0..4; week 2 Monday
		for (int i = 0; i < 5; i++) d.AddDay(D0.AddDays(i), 5, 10 + i, i, 5);
		Check(double.IsNaN(d.PrevWeekHigh), "no previous week during the first week");
		d.AddDay(D0.AddDays(7), 5, 8, 2, 5);
		Check(d.PrevWeekHigh == 14 && d.PrevWeekLow == 0, "previous week high/low from daily bars (Monday-start)");
		d.SetPrevWeek(30, 1);
		d.AddDay(D0.AddDays(14), 5, 8, 2, 5);
		Check(d.PrevWeekHigh == 30 && d.PrevWeekLow == 1, "weekly stream values override the daily aggregation");
	}

	static void OutOfOrder()
	{
		var w = new List<string>(); var d = Ctx(); d.warnOnce = s => w.Add(s);
		Check(d.AddDay(D0.AddDays(2), 1, 2, 0, 1) && !d.AddDay(D0.AddDays(2), 1, 2, 0, 1) && !d.AddDay(D0.AddDays(1), 1, 2, 0, 1) && w.Count == 1 && d.Days == 1, "duplicate / out-of-order days ignored, warned once");
	}
}
