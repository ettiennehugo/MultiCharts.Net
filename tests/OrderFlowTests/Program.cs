using System;
using System.Collections.Generic;
using OF = Extracted.OrderFlow;
using Side = Extracted.OrderFlow.Side;

// Spec VPF-5 unit tests: tick-rule approximation of the aggressor side and synthetic footprints.
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }
	const double TICK = 0.25;

	// footprint from explicit rows: (price, ask, bid)
	static OF.Footprint Fp(params double[] rows)
	{
		var fp = new OF.Footprint(TICK);
		for (int i = 0; i < rows.Length; i += 3)
		{
			if (rows[i + 1] > 0) fp.Add(rows[i], rows[i + 1], Side.Buy);
			if (rows[i + 2] > 0) fp.Add(rows[i], rows[i + 2], Side.Sell);
			if (rows[i + 1] <= 0 && rows[i + 2] <= 0) fp.Add(rows[i], 0, Side.Unknown);
		}
		return fp;
	}

	static int Main()
	{
		// --- tick rule ---
		var tr = new OF.TickRule(TICK);
		Check(tr.Classify(100) == Side.Unknown, "first trade unclassified");
		Check(tr.Classify(100) == Side.Unknown, "zero tick after unknown stays unknown");
		Check(tr.Classify(100.25) == Side.Buy, "uptick = buy");
		Check(tr.Classify(100.25) == Side.Buy, "zero tick inherits buy");
		Check(tr.Classify(100.00) == Side.Sell, "downtick = sell");
		Check(tr.Classify(100.00) == Side.Sell && tr.Classify(100.0000001) == Side.Sell, "zero tick inherits sell (sub-tick noise is a zero tick)");
		Check(tr.Classify(100.50) == Side.Buy && tr.LastSide == Side.Buy, "uptick after sells");
		tr.Reset();
		Check(tr.Classify(99) == Side.Unknown, "reset");

		// --- tick rule driving a footprint (one bar of ticks) ---
		var fp0 = new OF.Footprint(TICK);
		var rule = new OF.TickRule(TICK);
		double[,] ticks = { { 100, 5 }, { 100.25, 3 }, { 100.25, 2 }, { 100, 4 }, { 99.75, 6 }, { 99.75, 1 }, { 100, 2 } };
		for (int i = 0; i < ticks.GetLength(0); i++) fp0.Add(ticks[i, 0], ticks[i, 1], rule.Classify(ticks[i, 0]));
		Check(Near(fp0.UnclassifiedVolume, 5) && Near(fp0.BuyVolume, 3 + 2 + 2) && Near(fp0.SellVolume, 4 + 6 + 1), "tick-rule volumes buy 7 sell 11 unclassified 5");
		Check(Near(fp0.Delta, -4) && Near(fp0.TotalVolume, 23), "delta -4, total 23");
		Check(Near(fp0.Ask(100.25), 5) && Near(fp0.Bid(99.75), 7) && Near(fp0.Bid(100), 4) && Near(fp0.Ask(100), 2), "per-price rows");
		Check(Near(fp0.High, 100.25) && Near(fp0.Low, 99.75) && Near(fp0.Open, 100) && Near(fp0.Close, 100), "OHLC from ticks");

		// --- diagonal imbalances (ratio 3, min volume 1) ---
		// price  ask  bid
		// 101.00  2    1
		// 100.75 30    2
		// 100.50 25    8
		// 100.25 20    5
		// 100.00  3   12
		var fp = Fp(100.00, 3, 12, 100.25, 20, 5, 100.50, 25, 8, 100.75, 30, 2, 101.00, 2, 1);
		var buy = OF.Imbalances(fp, Side.Buy, 3, 1);
		// buy at p: ask(p) >= 3 x bid(p - tick): 100.25: 20 >= 36? no; 100.50: 25 >= 15 yes; 100.75: 30 >= 24 yes; 101: 2 >= 6 no; 100.00: bid(99.75)=0 -> 3 >= 0 yes
		Check(buy.Count == 3 && Near(buy[0], 100.00) && Near(buy[1], 100.50) && Near(buy[2], 100.75), "buy imbalances 100.00, 100.50, 100.75 got " + string.Join(",", buy));
		var sell = OF.Imbalances(fp, Side.Sell, 3, 1);
		// sell at p: bid(p) >= 3 x ask(p + tick): 100.00: 12 >= 60 no; 100.75: 2 >= 6 no; 101.00: 1 >= 0 (no row above) yes
		Check(sell.Count == 1 && Near(sell[0], 101.00), "sell imbalance at the high only");
		Check(OF.Imbalances(fp, Side.Sell, 3, 2).Count == 0, "min volume filters a 1-lot row");
		Check(OF.Imbalances(fp, Side.Unknown, 3, 1).Count == 0 && OF.Imbalances(null, Side.Buy, 3, 1).Count == 0, "unknown side / null footprint -> empty");

		// --- stacked ---
		var stk = Fp(100.00, 30, 1, 100.25, 30, 1, 100.50, 30, 1, 100.75, 1, 1, 101.00, 30, 1, 101.25, 30, 1);
		var st3 = OF.StackedImbalances(stk, Side.Buy, 3, 1, 3);
		Check(st3.Count == 1 && Near(st3[0][0], 100.00) && Near(st3[0][1], 100.50) && Near(st3[0][2], 3), "one stack of 3 (100.00-100.50)");
		var st2 = OF.StackedImbalances(stk, Side.Buy, 3, 1, 2);
		Check(st2.Count == 2 && Near(st2[1][0], 101.00) && Near(st2[1][2], 2), "minCount 2: two stacks");
		Check(OF.StackedImbalances(stk, Side.Sell, 3, 1, 3).Count == 0, "no sell stack");
		var gap = Fp(100.00, 30, 0, 100.50, 30, 0, 100.75, 30, 0);   // 100.25 empty row breaks the run
		Check(OF.StackedImbalances(gap, Side.Buy, 3, 1, 3).Count == 0 && OF.StackedImbalances(gap, Side.Buy, 3, 1, 2).Count == 1, "an empty row breaks a stack");

		// --- unfinished auction ---
		var ua = Fp(100.00, 0, 5, 100.25, 4, 4, 100.50, 3, 2);
		Check(OF.UnfinishedAuction(ua, true), "high traded both sides = unfinished");
		Check(!OF.UnfinishedAuction(ua, false), "low traded sellers only = finished");
		Check(!OF.UnfinishedAuction(ua, true, 6), "min volume at the extreme");

		// --- absorption hypothesis ---
		// sellers aggressive (delta < 0), close barely below previous close, volume parked at the low -> Buy (bullish)
		var ab = Fp(100.00, 40, 120, 100.25, 10, 20, 100.50, 5, 10);
		ab.Close = 100.25;
		Check(OF.Absorption(ab, 100.25, 0.95, 0.9, 1, 2) == Side.Buy, "selling absorbed at the low");
		Check(OF.Absorption(ab, 100.25, 0.5, 0.9, 1, 2) == Side.Unknown, "not exceptional volume");
		Check(OF.Absorption(ab, 101.00, 0.95, 0.9, 1, 2) == Side.Unknown, "price followed the sellers (3 ticks) = no absorption");
		var ab2 = Fp(100.00, 10, 5, 100.25, 20, 10, 100.50, 120, 40);
		ab2.Close = 100.50;
		Check(OF.Absorption(ab2, 100.50, 0.95, 0.9, 1, 2) == Side.Sell, "buying absorbed at the high");
		var ab3 = Fp(100.00, 120, 40, 100.25, 10, 10, 100.50, 10, 5, 100.75, 5, 5, 101.00, 5, 5);
		ab3.Close = 101.00;
		Check(OF.Absorption(ab3, 101.00, 0.95, 0.9, 1, 2) == Side.Unknown, "buyers but volume at the low, not the high");
		Check(OF.Absorption(ab, double.NaN, 0.95, 0.9, 1, 2) == Side.Unknown, "no previous close");

		// --- UpTicks / DownTicks fallback: bar delta only, no footprint rows -> pattern helpers empty / false ---
		var fb = new OF.Footprint(TICK);
		fb.Add(100, 10, Side.Buy);
		fb.SetBarTotals(70, 30);
		Check(fb.Source == OF.Source.UpDownTicks && Near(fb.Delta, 40) && !fb.HasRows, "fallback delta = up - down, rows cleared");
		Check(OF.Imbalances(fb, Side.Buy, 3, 1).Count == 0 && !OF.UnfinishedAuction(fb, true) && OF.Absorption(fb, 100, 1, 0.9, 1, 2) == Side.Unknown, "fallback: no per-price patterns");

		// --- percent rank, delta z ---
		var vols = new List<double> { 10, 20, 30, 40 };
		Check(Near(OF.PercentRank(vols, 30), 0.75) && Near(OF.PercentRank(vols, 50), 1) && double.IsNaN(OF.PercentRank(new List<double>(), 1)), "percent rank");
		Check(Near(OF.DeltaZ(new List<double> { 1, 2, 3 }, 4), 2) && double.IsNaN(OF.DeltaZ(new List<double> { 5 }, 1)) && double.IsNaN(OF.DeltaZ(new List<double> { 2, 2 }, 3)), "delta z / NaN paths");

		// --- delta history: anchored CVD and same-minute z from prior sessions only ---
		var h = new OF.DeltaHistory();
		Check(double.IsNaN(h.AnchoredCvd), "no anchor -> NaN");
		DateTime d1 = new DateTime(2024, 3, 11), d2 = d1.AddDays(1), d3 = d1.AddDays(2), d4 = d1.AddDays(3);
		h.Anchor(1);
		h.Add(1, 5, d1, 600); h.Add(2, -3, d1, 601); h.Add(2, 99, d1, 601);   // repeated bar ignored
		Check(Near(h.AnchoredCvd, 2) && Near(h.DeltaOf(2), -3) && h.Count == 2, "CVD 5 - 3, a bar is added once");
		h.Anchor(3);
		h.Add(3, 10, d2, 600); h.Add(4, 1, d2, 601);
		Check(Near(h.AnchoredCvd, 11) && Near(h.Cvd(2, 4), 8), "re-anchor; Cvd(range)");
		h.Add(5, 15, d3, 600);
		// minute 600 on d4 with value 20: priors {5, 10, 15} -> mean 10, sd 5 -> z 2; today's own value never in the baseline
		h.Add(6, 20, d4, 600);
		Check(Near(h.SameMinuteZ(600, d4, 20, 20), 2), "same-minute z vs prior sessions only");
		Check(Math.Abs(h.SameMinuteZ(600, d4, 20, 2) - 7.5 / Math.Sqrt(12.5)) < 1e-9, "z over the last 2 prior sessions {15, 10}: (20 - 12.5) / 3.536");
		Check(double.IsNaN(h.SameMinuteZ(601, d2, 1, 20)), "fewer than 2 prior sessions -> NaN");

		Console.WriteLine("OrderFlowTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}
}
