using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _RollDetector / _RollPending (Phase 3 S4).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	struct B { public int bar; public DateTime day, time; public double o, c; public string name; }

	// Reference: the pre-S4 _VolumeProfileFuturesBase.detectRoll body, verbatim but for the MC accessors.
	sealed class Ref
	{
		public bool series, table; public int gap = 8; public HashSet<DateTime> dates = new HashSet<DateTime>();
		public List<int> rolls = new List<int>(); DateTime lastDay = DateTime.MinValue; int lastBar = -1; string lastName;
		public void Step(B[] s, int i, double tick)
		{
			int bar = s[i].bar; if (bar == lastBar) return; lastBar = bar;
			string name = s[i].name; DateTime gx = s[i].day;
			bool newDay = gx != lastDay; bool roll = lastName != null && name != lastName;
			if (newDay && lastDay != DateTime.MinValue && bar > 1)
			{
				if (table && dates.Contains(gx.Date)) roll = true;
				if (series && gx.Month % 3 == 0 && gx.Day <= 21 && gap > 0 && Math.Abs(s[i].o - s[i - 1].c) >= gap * tick - tick * 1e-6) roll = true;
			}
			if (roll && (rolls.Count == 0 || rolls[rolls.Count - 1] != bar)) rolls.Add(bar);
			lastName = name; lastDay = gx;
		}
	}

	static B[] Series(int days, Func<DateTime, int, double> gapAt, Func<DateTime, string> nameOf, DateTime start)
	{
		var l = new List<B>(); double px = 5000; int bar = 1; DateTime d = start;
		for (int k = 0; k < days; k++, d = d.AddDays(1))
		{
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
			for (int m = 0; m < 4; m++)
			{
				double o = px + (m == 0 ? gapAt(d, m) : 0);
				px = o + (bar % 3 - 1) * 0.25;
				l.Add(new B { bar = bar++, day = d, time = d.AddHours(9).AddMinutes(30 + 60 * m), o = o, c = px, name = nameOf(d) });
			}
		}
		return l.ToArray();
	}

	static List<int> Run(_RollDetector det, B[] s, double tick, bool twice = false)
	{
		var got = new List<int>();
		for (int i = 0; i < s.Length; i++)
		{
			if (det.Step(s[i].bar, s[i].day, s[i].o, i > 0 ? s[i - 1].c : double.NaN, s[i].name, tick)) got.Add(s[i].bar);
			if (twice && det.Step(s[i].bar, s[i].day, s[i].o, i > 0 ? s[i - 1].c : double.NaN, s[i].name, tick)) got.Add(-s[i].bar);
		}
		return got;
	}

	static void Main()
	{
		var start = new DateTime(2025, 2, 24);
		// gaps: 3 ticks most days, 10 ticks on 2025-03-12 (in the window), 12 ticks on 2025-04-08 (out of the window), 9 on 2025-03-24 (day 24: out)
		Func<DateTime, int, double> gap = (d, m) => d == new DateTime(2025, 3, 12) ? 2.5 : d == new DateTime(2025, 4, 8) ? 3.0 : d == new DateTime(2025, 3, 24) ? 2.25 : 0.75;
		var s = Series(60, gap, d => "ES", start);

		// 1. ported detector == pre-S4 futures-base detector
		foreach (var mode in new[] { (true, false), (false, true), (true, true) })
		{
			var r = new Ref { series = mode.Item1, table = mode.Item2 }; r.dates.Add(new DateTime(2025, 3, 17));
			for (int i = 0; i < s.Length; i++) r.Step(s, i, 0.25);
			var det = new _RollDetector { series = mode.Item1, table = mode.Item2, dates = r.dates };
			var got = Run(det, s, 0.25, true);
			Check(got.SequenceEqual(r.rolls) && det.RollBars.SequenceEqual(r.rolls), $"S4 port identical to VPF-4 (series={mode.Item1} table={mode.Item2}) rolls [{string.Join(",", got)}]");
		}
		var ds = new _RollDetector { series = true };
		var sr = Run(ds, s, 0.25);
		int b0312 = s.First(x => x.day == new DateTime(2025, 3, 12)).bar;
		Check(sr.Count == 1 && sr[0] == b0312, "S4 series: only the in-window gap >= 8 ticks (Mar 12), not Mar 24 / Apr 8");
		Check(Run(new _RollDetector { series = true, gapTicks = 11 }, s, 0.25).Count == 0, "S4 series: gapTicks threshold respected");

		// 2. table mode
		var dt = new _RollDetector { table = true, dates = new HashSet<DateTime> { new DateTime(2025, 3, 17), new DateTime(2025, 6, 1) } };
		var tr = Run(dt, s, 0.25);
		Check(tr.Count == 1 && tr[0] == s.First(x => x.day == new DateTime(2025, 3, 17)).bar, "S4 table: first bar of the listed trading day");

		// 3. name change (any mode that is not Off; mid-day too)
		var sn = Series(10, (d, m) => 0, d => d < new DateTime(2025, 3, 3) ? "ESH25" : "ESM25", start);
		var nr = Run(new _RollDetector { table = true }, sn, 0.25);
		Check(nr.Count == 1 && nr[0] == sn.First(x => x.name == "ESM25").bar, "S4 name change flags the first bar of the new contract");

		// 5. Off gives no rolls (the host never calls Step when Off; a detector with neither mode still catches only names)
		Check(Run(new _RollDetector(), s, 0.25).Count == 0, "S4 Off (no series / table): no rolls on a same-name series");

		// 4. Data2 pending flag: Data1 roll at 10:30; Data2 60m bars at 10:00 / 11:00 -> fires at 11:00, once, idempotent per bar
		var p = new _RollPending();
		Check(!p.Fire(1, new DateTime(2025, 3, 12, 10, 0, 0)), "S4 pending: nothing armed -> false");
		p.Arm(new DateTime(2025, 3, 12, 10, 30, 0));
		Check(!p.Fire(2, new DateTime(2025, 3, 12, 10, 0, 0)), "S4 pending: Data2 bar before the Data1 roll -> false");
		Check(p.Fire(3, new DateTime(2025, 3, 12, 11, 0, 0)) && p.Fire(3, new DateTime(2025, 3, 12, 11, 0, 0)), "S4 pending fires on the first Data2 close >= the roll (repeat calls same bar true)");
		Check(!p.Fire(4, new DateTime(2025, 3, 12, 12, 0, 0)) && !p.Armed, "S4 pending: cleared after firing");
		p.Arm(new DateTime(2025, 6, 13, 9, 30, 0));
		Check(p.Fire(9, new DateTime(2025, 6, 13, 9, 30, 0)), "S4 pending: same-timestamp close fires");

		Console.WriteLine($"RollTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
