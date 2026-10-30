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
		Validation(); Indicators(); Roll(); OnRollIntraday(); Weeks(); OutOfOrder(); S2Hv(); S2Lbr(); S2Roll(); S2Pct(); S2HalfDay(); EveningSplice(); RollCarryAtr();
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

	// ---- Spec S2: HV / ROC1 / LBR ----
	static void Feed(_DailyContext d, IList<double> closes, int start = 0)
	{
		for (int i = 0; i < closes.Count; i++) { DateTime t = D0.AddDays(start + i); while (t.DayOfWeek == DayOfWeek.Saturday || t.DayOfWeek == DayOfWeek.Sunday) { start++; t = D0.AddDays(start + i); } d.AddDay(t, closes[i], closes[i], closes[i], closes[i]); }
	}
	static void S2Hv()
	{
		var d = Ctx(c => c.hvLengths = new[] { 6, 20 });
		var cl = new List<double>(); for (int i = 0; i < 7; i++) cl.Add(100 * Math.Pow(1.01, i));
		Feed(d, cl.Take(6).ToList());
		Check(double.IsNaN(d.Hv(6)), "S2 HV6 NaN with 6 closes (needs 7)");
		Feed(d, cl.Skip(6).ToList(), 9);
		Check(Near(d.Hv(6), 0, 1e-12), "S2 constant 1% return -> HV6 = 0 got " + d.Hv(6));
		Check(double.IsNaN(d.Hv(20)) && double.IsNaN(d.Hv(100)), "S2 HV20 NaN early, unconfigured length NaN");
		// alternating +/-0.01 log returns: sample SD over 6 = sqrt(6e-4/5); x sqrt(252)
		var a = Ctx(c => c.hvLengths = new[] { 6 });
		var al = new List<double> { 100 }; for (int i = 1; i <= 6; i++) al.Add(al[i - 1] * Math.Exp(i % 2 == 1 ? 0.01 : -0.01));
		Feed(a, al);
		Check(Near(a.Hv(6), Math.Sqrt(252) * Math.Sqrt(6e-4 / 5), 1e-12), "S2 alternating +/-1% -> sqrt(252) x sample SD got " + a.Hv(6));
		var off = Ctx(); var w = new List<string>(); off.warnOnce = x => w.Add(x); Feed(off, cl);
		Check(double.IsNaN(off.Hv(6)) && double.IsNaN(off.Lbr) && w.Count == 1, "S2 off by default: NaN and one warning");
	}
	static void S2Lbr()
	{
		// closes 100,102,101,104,103,107,105,106,110,108 -> ROC1 2,-1,3,-1,4,-2,1,4,-2 -> d = -3,4,-4,5,-6,3,3,-6
		// seed (5th close): up 4/3, dn 7/3 -> 36.3636; then 62.1622, 35.9375, 51.3353, 64.2312, 35.7837
		var d = Ctx(c => c.lbrRsiLength = 3);
		double[] c10 = { 100, 102, 101, 104, 103, 107, 105, 106, 110, 108 };
		double[] exp = { double.NaN, double.NaN, double.NaN, double.NaN, 36.36363636363636, 62.16216216216216, 35.9375, 51.33531157270029, 64.23118865866957, 35.78371810449575 };
		bool ok = true; DateTime t = D0;
		for (int i = 0; i < 10; i++)
		{
			d.AddDay(t, c10[i], c10[i], c10[i], c10[i]); t = t.AddDays(t.DayOfWeek == DayOfWeek.Friday ? 3 : 1);
			ok &= double.IsNaN(exp[i]) ? double.IsNaN(d.Lbr) : Near(d.Lbr, exp[i], 1e-9);
		}
		Check(ok, "S2 LBR = Wilder RSI(3) of ROC1, 5-close warmup, hand series");
		Check(Near(d.Roc1, -2) && Near(d.RthClosePrev, 108), "S2 ROC1 and RthClosePrev");
	}
	static void S2Roll()
	{
		// new contract trades at k x the old: with offset = old close x (k - 1) the ratio-adjusted HV equals the unrolled series
		var r = new Random(7); var cl = new List<double> { 4000 }; for (int i = 1; i < 40; i++) cl.Add(cl[i - 1] * Math.Exp((r.NextDouble() - 0.5) * 0.02));
		var plain = Ctx(c => c.hvLengths = new[] { 6, 20 }); Feed(plain, cl);
		var rolled = Ctx(c => c.hvLengths = new[] { 6, 20 }); double k = 1.003; DateTime t = D0;
		for (int i = 0; i < cl.Count; i++)
		{
			double v = i >= 25 ? cl[i] * k : cl[i];
			rolled.AddDay(t, v, v, v, v, i == 25, i == 25 ? cl[24] * (k - 1) : double.NaN);
			t = t.AddDays(t.DayOfWeek == DayOfWeek.Friday ? 3 : 1);
		}
		Check(Near(plain.Hv(6), rolled.Hv(6), 1e-12) && Near(plain.Hv(20), rolled.Hv(20), 1e-12), "S2 roll offset -> same HV as the gap-free series");
	}
	static void S2Pct()
	{
		var d = Ctx(c => { c.hvLengths = new[] { 2 }; c.hvPctLookback = 4; });
		// closes giving HV2 history; check today's mid-rank against the previous 4 HV2 values computed by HvOf
		double[] cl = { 100, 101, 100, 102, 101, 101.5, 103, 100 };
		var hv = new List<double>(); DateTime t = D0;
		for (int i = 0; i < cl.Length; i++) { d.AddDay(t, cl[i], cl[i], cl[i], cl[i]); t = t.AddDays(t.DayOfWeek == DayOfWeek.Friday ? 3 : 1); hv.Add(d.Hv(2)); }
		double cur = hv[hv.Count - 1]; var prev = hv.Skip(hv.Count - 5).Take(4).ToList();
		double expPct = 100.0 * (prev.Count(x => x < cur) + 0.5 * prev.Count(x => x == cur)) / 4;
		Check(prev.All(x => !double.IsNaN(x)) && Near(d.HvPercentile(2), expPct), "S2 HvPercentile mid-rank among the previous 4 got " + d.HvPercentile(2) + " exp " + expPct);
		var e = Ctx(c => { c.hvLengths = new[] { 2 }; c.hvPctLookback = 4; }); Feed(e, new double[] { 100, 101, 102, 101, 103 });
		Check(double.IsNaN(e.HvPercentile(2)), "S2 HvPercentile NaN until lookback is full");
		var j = new List<string>(); var f = Ctx(c => { c.hvLengths = new[] { 2 }; c.hvPctLookback = 2; }, j);
		DateTime tt = D0; var dates = new List<DateTime>();
		foreach (double x in new double[] { 100, 101, 102, 101, 103, 104, 102 }) { f.AddDay(tt, x, x, x, x); dates.Add(tt); tt = tt.AddDays(tt.DayOfWeek == DayOfWeek.Friday ? 3 : 1); }
		var fv = j.Where(s => s.Contains("hv2 first valid")).ToList(); var fp = j.Where(s => s.Contains("hv2pct first valid")).ToList();
		Check(fv.Count == 1 && fv[0].Contains(dates[2].ToString("yyyy-MM-dd")), "L3 hv2 first valid journalled once on the 3rd close: " + string.Join("|", fv));
		Check(fp.Count == 1 && fp[0].Contains(dates[4].ToString("yyyy-MM-dd")), "L3 hv2pct first valid journalled once after 2 more days: " + string.Join("|", fp));
	}
	static void S2HalfDay()
	{
		// day after Thanksgiving 2024 (Globex date 2024-11-29): RTH value window ends 13:15 ET via the early-close hook
		var clock = new _CmeSessionClock(TimeZoneInfo.Utc) { RthEarlyCloseHook = g => g == new DateTime(2024, 11, 29) ? new TimeSpan(13, 15, 0) : (TimeSpan?)null };
		var sel = new _RthCloseSelector(); DateTime day = new DateTime(2024, 11, 29);
		double[] tod = { 9.5, 10, 13, 13.25, 13.5, 16 }; double[] px = { 1, 2, 3, 4, 5, 6 };
		for (int i = 0; i < tod.Length; i++) { DateTime et = day.AddHours(tod[i]); sel.Observe(day, clock.IsRthValueBarEt(et), px[i]); }
		double done = sel.Observe(day.AddDays(3), false, 7);
		Check(Near(done, 4), "S2 half day: RTH close = last bar stamped <= 13:15 ET got " + done);
		var normal = new _RthCloseSelector(); DateTime nd = new DateTime(2024, 11, 26);
		for (int i = 0; i < tod.Length; i++) normal.Observe(nd, clock.IsRthValueBarEt(nd.AddHours(tod[i])), px[i]);
		Check(Near(normal.Observe(nd.AddDays(1), false, 0), 6), "S2 normal day: RTH close = the 16:00 bar");
	}

	// PR #46 review Q3: an ETH futures chart whose vendor splices at the 18:00 ET Globex open. Mirrors _CustomSignal.stepDaily
	// (dataStream 0): Table flag on the first bar of a new key listed in RollDates, AddDay(previous key), then
	// OnRoll(key, roll-bar open - PrevClose). Globex key (_CmeSessionClock.GlobexTradingDayEt): offset = the spread.
	// Calendar key: the Sunday-evening stub is already new-contract, so the offset is ~0 and the old-contract history is not
	// difference-adjusted (the stub day's TR is dropped by the ATR reseed here; the level history keeps the spread).
	static void EveningSplice()
	{
		DateTime roll = new DateTime(2024, 3, 11), splice = new DateTime(2024, 3, 10, 18, 0, 0);   // Mon roll; Globex opens Sun 18:00 ET
		const double spread = 40;
		Func<Func<DateTime, DateTime>, double[]> run = key =>
		{
			var d = Ctx(); double offset = double.NaN; int nanAfterRoll = 0; bool rolled = false; DateTime aggDay = DateTime.MinValue; double o = 0, h = 0, l = 0, c = 0; bool first = true;
			int k = 0;
			for (DateTime t = new DateTime(2024, 1, 1, 18, 0, 0); t < new DateTime(2024, 4, 20); t = t.AddHours(1))
			{
				var dw = t.DayOfWeek; var tod = t.TimeOfDay;
				if (tod.Hours == 17) continue;                                                     // daily 17:00-18:00 ET halt
				if (dw == DayOfWeek.Saturday || (dw == DayOfWeek.Friday && tod.Hours >= 18) || (dw == DayOfWeek.Sunday && tod.Hours < 18)) continue;
				double px = 4000 + 10 * Math.Sin(k++ * 0.7) + (t >= splice ? spread : 0);
				double bo = px, bh = px + 3, bl = px - 3, bc = px + 1;
				DateTime kk = key(t);
				bool r = !first && kk != aggDay && kk == roll;
				if (kk != aggDay)
				{
					if (aggDay != DateTime.MinValue) { d.AddDay(aggDay, o, h, l, c); if (rolled && double.IsNaN(d.Atr20d)) nanAfterRoll++; }
					aggDay = kk; o = bo; h = bh; l = bl;
				}
				else { h = Math.Max(h, bh); l = Math.Min(l, bl); }
				c = bc; first = false;
				if (r && d.Days > 0) { offset = bo - d.PrevClose; d.OnRoll(kk, offset); rolled = true; }
			}
			return new[] { offset, d.Atr20d, nanAfterRoll };
		};
		var gx = run(t => _CmeSessionClock.GlobexTradingDayEt(t));
		var cal = run(t => t.Date);
		Check(_CmeSessionClock.GlobexTradingDayEt(splice) == roll && _CmeSessionClock.GlobexTradingDayEt(splice.AddMinutes(-1)) == roll.AddDays(-1)
			&& _CmeSessionClock.GlobexTradingDayEt(new DateTime(2024, 3, 11, 16, 59, 0)) == roll, "Globex key: ET >= 18:00 -> next date");
		Check(Math.Abs(gx[0] - spread) < 25, "evening splice, Globex key: roll offset ~ the spread (" + gx[0].ToString("0.##") + ")");
		Check(Math.Abs(cal[0]) < 25 && Math.Abs(cal[0]) < Math.Abs(gx[0]) - 20, "evening splice, calendar key: offset ~0, the known failure (" + cal[0].ToString("0.##") + ")");
		// PR #46 re-review: ATR20d. The roll reseeds Wilder (applyRoll), so ATR20d is NaN for the next 20 completed days (the
		// default atrRestartOnRoll = true reseed; _Test6 carries instead, rulings (h) and RollCarryAtr), then finite and free of the 40-point spread (bar TRs are ~26 here).
		Console.WriteLine("EveningSplice: Globex offset " + gx[0].ToString("0.##") + " atr " + gx[1].ToString("0.##") + " nanDays " + gx[2] + "; calendar offset " + cal[0].ToString("0.##") + " atr " + cal[1].ToString("0.##") + " nanDays " + cal[2]);
		// Holidays (rulings (i), accepted, documented): the key is a pure 18:00 ET rule. MLK 2024-01-15: Sun 18:00 -> Mon 01-15
		// (a short holiday day of its own), Mon 18:00 -> Tue 01-16. CME books the holiday session to the next trade date.
		Check(_CmeSessionClock.GlobexTradingDayEt(new DateTime(2024, 1, 14, 18, 0, 0)) == new DateTime(2024, 1, 15)
			&& _CmeSessionClock.GlobexTradingDayEt(new DateTime(2024, 1, 15, 12, 0, 0)) == new DateTime(2024, 1, 15)
			&& _CmeSessionClock.GlobexTradingDayEt(new DateTime(2024, 1, 15, 18, 0, 0)) == new DateTime(2024, 1, 16), "Globex key on a Monday holiday: own short day (documented, rulings (i))");
		Check(gx[2] == 20, "post-roll ATR20d blackout: NaN for 20 completed days after the roll (" + gx[2] + ")");
		Check(!double.IsNaN(gx[1]) && gx[1] < spread, "ATR20d after the blackout is finite and below the spread (" + gx[1].ToString("0.##") + ")");
	}

	// PR #46 (DECIDED by Ettienne): atrRestartOnRoll = false carries ATR20d on from the roll-adjusted history.
	static void RollCarryAtr()
	{
		var d = Ctx(c => c.atrRestartOnRoll = false); var days = Days(260, 2);
		for (int i = 0; i < 250; i++) d.AddDay(D0.AddDays(i), days[i][0], days[i][1], days[i][2], days[i][3]);
		double atr0 = d.Atr20d, pc = d.PrevClose; const double off = 25;
		// the splice day: a 500-point range vs the old close would dominate a TR; it must be skipped, not reseeded
		d.AddDay(D0.AddDays(250), pc + off, pc + off + 250, pc + off - 250, pc + off, true, off);
		Check(!double.IsNaN(atr0) && Near(d.Atr20d, atr0, 1e-12), "carry: splice-day TR excluded, ATR20d unchanged on the roll day (" + atr0.ToString("0.###") + ")");
		bool anyNaN = false; double expect = atr0, prev = d.PrevClose;
		for (int i = 251; i < 271; i++)
		{
			d.AddDay(D0.AddDays(i), prev, prev + 10, prev - 10, prev);
			expect = (expect * 19 + 20) / 20; anyNaN |= double.IsNaN(d.Atr20d);
		}
		Check(!anyNaN && Near(d.Atr20d, expect, 1e-9), "carry: no NaN blackout after the roll; Wilder continues from the pre-roll ATR");
		var r = Ctx(); Check(r.config.atrRestartOnRoll, "atrRestartOnRoll defaults to true (old reseed behaviour)");
	}
}
