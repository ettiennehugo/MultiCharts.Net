using System;
using System.Collections.Generic;
using System.Linq;
using OF = Extracted.OrderFlow;
using Src = Extracted.OrderFlow.FlowSource;
using Q = Extracted.OrderFlow.FlowQuality;
using Side = Extracted.OrderFlow.Side;
using Regime = Extracted.OrderFlow.Regime;

// Spec VPF-5 Rev 2.5 acceptance tests T1-T8 (analyst-base-class-spec.md §D), run on the shipped <order-flow> block.
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }
	const double TICK = 0.25;
	static readonly DateTime T0 = new DateTime(2024, 3, 13, 10, 0, 0);
	static DateTime Ms(int ms) { return T0.AddMilliseconds(ms); }

	static OF.BarInput Bar(int n, DateTime close, Regime r = Regime.TickStream, double vol = 0, double up = 0, double down = 0, DateTime? session = null, double hi = 0, double lo = 0, double cl = 0)
	{
		DateTime s = session ?? T0.Date;
		return new OF.BarInput { BarNumber = n, CloseTime = close, SessionKey = s, ResetKey = s, Regime = r, Volume = vol, UpTicks = up, DownTicks = down, High = hi, Low = lo, Close = cl };
	}

	static OF.FlowEngine Engine(bool quotes, OF.VolumeBasis basis = OF.VolumeBasis.Unknown)
	{
		return new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = quotes, BuildVolumeOn = basis });
	}

	// footprint from explicit levels (price, bid, ask) as a Quote bar
	static OF.FootprintBar Ladder(params double[] lv)
	{
		var fp = new OF.FootprintBar { Tick = TICK, Source = Src.Quote, Quality = Q.High };
		for (int i = 0; i < lv.Length; i += 3)
		{
			if (lv[i + 1] > 0) fp.add(lv[i], lv[i + 1], Side.Sell);
			if (lv[i + 2] > 0) fp.add(lv[i], lv[i + 2], Side.Buy);
		}
		return fp;
	}

	static string Sig(OF.FlowEngine e, int ago)
	{
		var fp = e.Closed(ago);
		string lad = fp == null ? "-" : string.Join(";", fp.Rows().Select(r => r[0] + ":" + r[1] + "/" + r[2]));
		string imb = string.Join(";", e.DiagonalImbalances(ago, 3, 1).Select(i => i.Price + "" + i.Side));
		return e.BarDelta(ago) + "|" + e.CumDelta(ago) + "|" + lad + "|" + imb + "|" + e.Absorption(ago, Side.Sell, 2, 2.0, 1) + "|" + e.Absorption(ago, Side.Buy, 2, 2.0, 1);
	}

	static int Main()
	{
		// ---- T1 tick-rule delta ----
		var e1 = Engine(false);
		double[,] t1 = { { 100.00, 2 }, { 100.25, 3 }, { 100.25, 1 }, { 100.00, 4 }, { 100.00, 2 }, { 100.25, 1 } };
		for (int i = 0; i < t1.GetLength(0); i++) e1.AddTrade(Ms(i * 100), t1[i, 0], t1[i, 1]);
		var b1 = e1.CloseBar(Bar(1, Ms(1000)));
		Check(Near(b1.Buy, 7) && Near(b1.Sell, 6) && Near(e1.BarDelta(0).Value, 1), "T1 buy 7 sell 6 delta +1");
		Check(Near(b1.Bid(100.00), 6) && Near(b1.Ask(100.00), 2) && Near(b1.Bid(100.25), 0) && Near(b1.Ask(100.25), 5), "T1 ladder 100.00 6/2, 100.25 0/5");
		Check(e1.BarDelta(0).Source == Src.TickRule && e1.BarDelta(0).Quality == Q.Medium, "T1 source TickRule, Medium");

		// ---- T2 zero-tick inheritance across bar and session boundaries ----
		var e2 = Engine(false);
		e2.AddTrade(Ms(0), 100.25, 1); e2.AddTrade(Ms(100), 100.00, 2);   // bar A ends with a down-tick
		e2.CloseBar(Bar(1, Ms(500)));
		e2.AddTrade(Ms(600), 100.00, 5);                                   // bar B first trade, same price
		var b2 = e2.CloseBar(Bar(2, Ms(1000)));
		Check(Near(b2.Sell, 5) && Near(b2.Buy, 0), "T2 zero tick at bar start inherits SELL");
		e2.AddTrade(Ms(1100), 100.00, 4);                                  // next session, same price
		var s2 = T0.Date.AddDays(1);
		var b3 = e2.CloseBar(Bar(3, Ms(1500), session: s2));
		Check(Near(b3.Sell, 4), "T2 sign carries across a session boundary");
		Check(Near(e2.CumDelta(0).Value, -4) && Near(e2.CumDelta(1).Value, 1 - 2 - 5), "T2 cum delta resets at the session, the sign state does not");

		// ---- T3 Lee-Ready ----
		var e3 = Engine(true);
		e3.AddQuote(Ms(0), true, 99.75); e3.AddQuote(Ms(0), false, 100.00);
		e3.AddTrade(Ms(10), 100.00, 5);                                    // at ask -> BUY
		e3.AddTrade(Ms(20), 99.75, 3);                                     // at bid -> SELL
		e3.AddQuote(Ms(30), true, 100.00); e3.AddQuote(Ms(30), false, 100.50);
		e3.AddTrade(Ms(40), 100.25, 2);                                    // inside; previous 99.75 -> uptick BUY
		e3.CloseBar(Bar(1, Ms(1000)));
		var d3 = e3.BarDelta(0);
		Check(Near(d3.Value, 4) && d3.Source == Src.Quote && d3.Quality == Q.High, "T3 Lee-Ready delta +4, Quote/High got " + d3);
		var e3s = Engine(true);   // same trades, the inside trade's quote is 1001 ms old
		e3s.AddQuote(Ms(0), true, 99.75); e3s.AddQuote(Ms(0), false, 100.00);
		e3s.AddTrade(Ms(10), 100.00, 5); e3s.AddTrade(Ms(20), 99.75, 3);
		e3s.AddQuote(Ms(30), true, 100.00); e3s.AddQuote(Ms(30), false, 100.50);
		e3s.AddTrade(Ms(1031), 100.25, 2);
		e3s.CloseBar(Bar(1, Ms(2000)));
		var d3s = e3s.BarDelta(0);
		Check(Near(d3s.Value, 4) && d3s.Source == Src.TickRule && d3s.Quality == Q.Medium && e3s.Closed(0).StaleQuoteTrades == 1, "T3 stale quote -> trade by tick rule, bar Medium got " + d3s);
		var e3m = Engine(true);   // AllowMixed = false: a TickRule bar downgrades the rest of the session
		e3m.AddQuote(Ms(0), true, 99.75); e3m.AddQuote(Ms(0), false, 100.00);
		e3m.AddTrade(Ms(5000), 100.00, 1); e3m.CloseBar(Bar(1, Ms(6000)));          // stale -> TickRule
		e3m.AddQuote(Ms(6100), true, 99.75); e3m.AddQuote(Ms(6100), false, 100.00);
		e3m.AddTrade(Ms(6200), 100.00, 1); e3m.CloseBar(Bar(2, Ms(7000)));          // fresh -> Quote, reported Medium
		Check(e3m.BarDelta(0).Source == Src.Quote && e3m.BarDelta(0).Quality == Q.Medium && e3m.CumDelta(0).Quality == Q.Medium, "T3 mixed session downgraded (AllowMixed=false)");

		// ---- T4 truncation / no lookahead; T5 history vs real-time parity (offline replay) ----
		var rnd = new Random(7);
		var trades = new List<Tuple<DateTime, double, double>>();
		var quotes = new List<Tuple<DateTime, bool, double>>();
		double px = 100;
		for (int i = 0; i < 4000; i++)
		{
			DateTime t = Ms(i * 37);
			if (i % 3 == 0) { quotes.Add(Tuple.Create(t, true, px - TICK)); quotes.Add(Tuple.Create(t, false, px + (rnd.Next(4) == 0 ? 2 * TICK : TICK))); }
			px += TICK * (rnd.Next(5) - 2) * (rnd.Next(3) == 0 ? 1 : 0);
			trades.Add(Tuple.Create(t.AddMilliseconds(5), px + TICK * (rnd.Next(3) - 1), (double)(1 + rnd.Next(20))));
		}
		int nBars = 30; int barMs = 4000 * 37 / nBars + 1;
		Func<int, DateTime> closeOf = k => Ms(k * barMs);
		Func<int, OF.BarInput> input = k => Bar(k, closeOf(k), session: T0.Date.AddDays(k / 10), hi: 0, lo: 0, cl: 0);
		// history: everything delivered first, then the bars close
		var hist = Engine(true);
		foreach (var q in quotes) hist.AddQuote(q.Item1, q.Item2, q.Item3);
		foreach (var tr in trades) hist.AddTrade(tr.Item1, tr.Item2, tr.Item3);
		var histSig = new List<string>();
		for (int k = 1; k <= nBars; k++) { hist.CloseBar(input(k)); histSig.Add(Sig(hist, 0)); }
		// real time: events delivered just before each close
		var rt = Engine(true);
		int qi = 0, ti = 0; var rtSig = new List<string>();
		for (int k = 1; k <= nBars; k++)
		{
			while (qi < quotes.Count && quotes[qi].Item1 <= closeOf(k)) { rt.AddQuote(quotes[qi].Item1, quotes[qi].Item2, quotes[qi].Item3); qi++; }
			while (ti < trades.Count && trades[ti].Item1 <= closeOf(k)) { rt.AddTrade(trades[ti].Item1, trades[ti].Item2, trades[ti].Item3); ti++; }
			rt.CloseBar(input(k)); rtSig.Add(Sig(rt, 0));
		}
		Check(histSig.SequenceEqual(rtSig), "T5 history vs real-time replay: identical per-bar delta, cum delta, ladder, imbalances, absorption");
		Check(hist.Closed(0).Source == Src.Quote || hist.Closed(0).Source == Src.TickRule, "T5 generated bars carry trade-based sources");
		// truncation: re-run with every event after close(t) removed; the as-of-t outputs must not change
		bool trunc = true;
		for (int t = 1; t <= nBars; t++)
		{
			var tr = Engine(true);
			foreach (var q in quotes) if (q.Item1 <= closeOf(t)) tr.AddQuote(q.Item1, q.Item2, q.Item3);
			foreach (var x in trades) if (x.Item1 <= closeOf(t)) tr.AddTrade(x.Item1, x.Item2, x.Item3);
			for (int k = 1; k <= t; k++) tr.CloseBar(input(k));
			if (Sig(tr, 0) != histSig[t - 1]) { trunc = false; Console.WriteLine("T4 mismatch at bar " + t); break; }
		}
		Check(trunc, "T4 truncated re-run identical for every bar t");
		// absorption at t is Developing until t+1 closes, then final
		var ea = Engine(true);
		ea.AddQuote(Ms(0), true, 99.75); ea.AddQuote(Ms(0), false, 100.00);
		ea.AddTrade(Ms(1), 99.75, 60); ea.AddTrade(Ms(2), 100.00, 5);            // bids absorb 60 at the low
		ea.AddQuote(Ms(3), true, 100.00); ea.AddQuote(Ms(3), false, 100.25);
		ea.AddTrade(Ms(4), 100.25, 5); ea.AddTrade(Ms(5), 100.50, 2);
		ea.AddQuote(Ms(6), true, 100.25); ea.AddQuote(Ms(6), false, 100.50);
		ea.AddTrade(Ms(7), 100.25, 3);
		ea.CloseBar(Bar(1, Ms(100), hi: 100.50, lo: 99.75, cl: 100.25));
		var a0 = ea.Absorption(0, Side.Sell);
		Check(a0.Developing && Near(a0.Value, 1) && a0.Quality == Q.High, "T4 absorption provisional and Developing before the next bar closes got " + a0);
		ea.AddQuote(Ms(150), true, 100.00); ea.AddQuote(Ms(150), false, 100.25);
		ea.AddTrade(Ms(160), 100.00, 2);
		ea.CloseBar(Bar(2, Ms(200), hi: 100.25, lo: 100.00, cl: 100.00));
		var a1 = ea.Absorption(1, Side.Sell);
		Check(!a1.Developing && Near(a1.Value, 1), "T4 absorption final once the next bar held the low");
		var eb = Engine(true);   // same, but the next bar trades below the low
		eb.AddQuote(Ms(0), true, 99.75); eb.AddQuote(Ms(0), false, 100.00);
		eb.AddTrade(Ms(1), 99.75, 60); eb.AddTrade(Ms(2), 100.00, 5);
		eb.AddQuote(Ms(3), true, 100.00); eb.AddQuote(Ms(3), false, 100.25);
		eb.AddTrade(Ms(4), 100.25, 5); eb.AddTrade(Ms(7), 100.25, 3);
		eb.CloseBar(Bar(1, Ms(100), hi: 100.25, lo: 99.75, cl: 100.25));
		eb.AddQuote(Ms(150), true, 99.25); eb.AddQuote(Ms(150), false, 99.50);
		eb.AddTrade(Ms(160), 99.50, 2);
		eb.CloseBar(Bar(2, Ms(200), hi: 99.50, lo: 99.50, cl: 99.50));
		Check(Near(eb.Absorption(1, Side.Sell).Value, 0) && !eb.Absorption(1, Side.Sell).Developing, "T4 absorption rejected when the next bar breaks the low");
		var et = Engine(false);   // TickRule quality -> absorption Unknown
		et.AddTrade(Ms(1), 99.75, 60); et.AddTrade(Ms(2), 100.00, 5); et.AddTrade(Ms(3), 100.25, 5);
		et.CloseBar(Bar(1, Ms(100)));
		Check(et.Absorption(0, Side.Sell).Source == Src.Unavailable && et.Absorption(0, Side.Sell).Quality == Q.Unknown, "absorption needs Quote quality: TickRule -> Unknown");

		// ---- T6 imbalance vectors (ratio 3.0, minVol 10, minStack 3) ----
		var t6 = Ladder(100.00, 10, 5, 100.25, 4, 40, 100.50, 6, 30, 100.75, 5, 25, 101.00, 20, 8);
		var imb = OF.Imbalances(t6, 3.0, 10, true);
		var buys = imb.Where(i => i.Side == Side.Buy).Select(i => i.Price).ToList();
		Check(buys.SequenceEqual(new[] { 100.25, 100.50, 100.75 }), "T6 buy imbalances 100.25, 100.50, 100.75 got " + string.Join(",", buys));
		Check(Near(imb.First(i => i.Price == 100.25).Ratio, 4.0) && Near(imb.First(i => i.Price == 100.50).Ratio, 7.5), "T6 ratios 4.0, 7.5");
		Check(!imb.Any(i => i.Side == Side.Sell), "T6 no sell imbalances (101.00 is an edge)");
		var st6 = OF.Stacked(t6, 3.0, 10, 3, true);
		Check(st6.Runs.Count == 1 && Near(st6.Runs[0][0], 100.25) && Near(st6.Runs[0][1], 100.75) && st6.Longest == 3 && st6.Quality == Q.High, "T6 stacked buy 3 at 100.25-100.75");
		Check(OF.Imbalances(Ladder(100.00, 2, 0, 100.25, 0, 8), 3.0, 10, true).Count == 0, "T6 minVol: ask 8 vs bid 2 (ratio 4) < minVol -> none");
		Check(OF.Imbalances(Ladder(100.00, 10, 0, 100.25, 0, 30), 3.0, 10, true).Count(i => i.Side == Side.Buy) == 1, "T6 exact boundary 30 vs 10 = 3.0 -> imbalance");
		Check(OF.Imbalances(Ladder(100.00, 10, 0, 100.25, 0, 25), 2.0, 10, true).Count == 0, "ratio is floored at 3.0 (2.0 requested, 2.5 actual -> none)");
		Check(OF.Stacked(t6, 3.0, 10, 2, true).Runs.Count == 1 && OF.Stacked(Ladder(100.00, 10, 0, 100.25, 0, 40, 100.50, 0, 40), 3, 10, 2, true).Runs.Count == 0, "minStack floored at 3");
		var inf = OF.Imbalances(Ladder(100.00, 1, 0, 100.25, 0, 0, 100.50, 0, 12), 3, 10, true);
		Check(inf.Count == 1 && double.IsPositiveInfinity(inf[0].Ratio), "zero diagonal inside the ladder = +Infinity");
		// TickRule ladder: quality Low, extremes excluded
		var e6 = Engine(false);
		foreach (var x in new[] { 100.00, 100.25, 100.50, 100.75, 101.00 }) for (int k = 0; k < 3; k++) e6.AddTrade(Ms((int)(x * 100) + k), x, 20);
		e6.CloseBar(Bar(1, Ms(20000)));
		var i6 = e6.DiagonalImbalances(0, 3, 10);
		Check(i6.Count > 0 && i6.All(i => i.Quality == Q.Low && i.Price > 100.00 && i.Price < 101.00), "TickRule imbalances: Low quality, bar extremes excluded");

		// ---- T7 UpTicks/DownTicks gate (BarUpDown) ----
		var e7 = Engine(false, OF.VolumeBasis.TradeVolume);
		e7.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 13, up: 7, down: 6));
		Check(e7.BarDelta(0).Source == Src.BarUpDown && e7.BarDelta(0).Quality == Q.Low && Near(e7.BarDelta(0).Value, 1) && !e7.Closed(0).HasLadder, "T7 up 7 + down 6 == 13 -> BarUpDown, Low, delta +1, no ladder");
		Check(e7.DiagonalImbalances(0).Count == 0 && e7.Absorption(0, Side.Sell).Source == Src.Unavailable, "T7 BarUpDown is never used for ladders");
		e7.CloseBar(Bar(2, Ms(200), Regime.BarsOnly, vol: 13, up: 6, down: 6));
		Check(e7.BarDelta(0).Source == Src.Unavailable && double.IsNaN(e7.BarDelta(0).Value), "T7 zero-tick volume dropped (6 + 6 != 13) -> Unavailable");
		Check(e7.CumDelta(0).Source == Src.Unavailable, "a cumulative value over an Unavailable bar is Unavailable");
		var e7u = Engine(false);   // BuildVolumeOn not stated
		e7u.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 13, up: 7, down: 6));
		Check(e7u.BarDelta(0).Source == Src.Unavailable, "T7 BarUpDown needs BuildVolumeOn = TradeVolume");
		var e7g = Engine(true, OF.VolumeBasis.TradeVolume);   // tick gap: volume but no ticks -> falls to BarUpDown
		e7g.CloseBar(Bar(1, Ms(100), Regime.TickStream, vol: 13, up: 7, down: 6));
		Check(e7g.BarDelta(0).Source == Src.BarUpDown, "tick gap falls back to BarUpDown when it qualifies");
		var e7c = Engine(true);
		e7c.AddTrade(Ms(10), 100, 3); e7c.AddTrade(Ms(20), 100.25, 2);
		e7c.CloseBar(Bar(1, Ms(100))); e7c.CloseBar(Bar(2, Ms(200), vol: 0));
		Check(e7c.BarDelta(1).Source == Src.TickRule && e7c.BarDelta(0).Source == Src.Quote && Near(e7c.BarDelta(0).Value, 0), "no quotes before the trades -> TickRule; empty bar keeps the tick regime with delta 0");

		// ---- T8 Unavailable regime ----
		bool threw = false;
		try
		{
			var e8 = Engine(false, OF.VolumeBasis.TradeVolume);
			e8.AddTrade(Ms(10), 100, 5); e8.AddTrade(Ms(20), 100.25, 5);
			e8.CloseBar(Bar(1, Ms(100), Regime.IogHistoryNoMagnifier, vol: 10, up: 5, down: 5));
			Check(e8.BarDelta(0).Source == Src.Unavailable && e8.CumDelta(0).Source == Src.Unavailable && e8.DiagonalImbalances(0).Count == 0
				&& !e8.StackedImbalances(0).Found && e8.Absorption(0, Side.Buy).Quality == Q.Unknown && e8.PendingTrades == 0, "T8 IOG history without magnifier: everything Unavailable, ticks consumed");
			var e8t = new OF.FlowEngine(TICK, new OF.FlowConfig { BuildVolumeOn = OF.VolumeBasis.TickCount });
			e8t.AddTrade(Ms(10), 100, 5);
			e8t.CloseBar(Bar(1, Ms(100), vol: 1, up: 1, down: 0));
			Check(e8t.BarDelta(0).Source == Src.Unavailable && e8t.Closed(0).Quality == Q.Unknown, "T8 Build Volume On = Tick Count: Unavailable");
			var e8e = Engine(true);
			Check(e8e.BarDelta(0).Source == Src.Unavailable && e8e.CumDelta(3).Source == Src.Unavailable && e8e.Footprint0() == null && e8e.Absorption(-1, Side.Sell).Source == Src.Unavailable, "T8 no bars / bad index: Unavailable");
		}
		catch (Exception ex) { threw = true; Console.WriteLine(ex); }
		Check(!threw, "T8 no exceptions");

		// magnifier 1 tick / IOG real time are trade-tick regimes
		var e9 = Engine(false);
		e9.AddTrade(Ms(10), 100, 5); e9.AddTrade(Ms(20), 99.75, 2);
		e9.CloseBar(Bar(1, Ms(100), Regime.IogMagnifierOneTick, vol: 7));
		Check(e9.BarDelta(0).Source == Src.TickRule && Near(e9.BarDelta(0).Value, 3), "IOG magnifier 1-tick -> TickRule");

		// ---- S1 McBidAsk tier ----
		Check((int)Src.Aggressor == 0 && (int)Src.McBidAsk == 1 && (int)Src.Quote == 2 && (int)Src.TickRule == 3 && (int)Src.BarUpDown == 4 && (int)Src.Unavailable == 5, "S1 enum order Aggressor < McBidAsk < Quote < TickRule < BarUpDown < Unavailable");
		Check(OF.McBidAskMethod.Contains("quote-classified, not aggressor"), "S1 method tag says quote-classified, not aggressor");
		{
			// worst-of: an McBidAsk bar followed by a Quote bar -> CumDelta source Quote
			var cfg = new OF.FlowConfig { QuotesExpected = true, McBidAsk = OF.EMcBidAsk.Prefer, AllowMixed = true };
			var em = new OF.FlowEngine(TICK, cfg);
			em.AddMcLevels(Ms(1000), new[] { 100.00, 100.25 }, new[] { 3.0, 5.0 }, new[] { 4.0, 0.0 });
			var m1 = em.CloseBar(Bar(1, Ms(1000), vol: 12));
			Check(m1.Source == Src.McBidAsk && m1.Quality == Q.High && Near(m1.Buy, 8) && Near(m1.Sell, 4) && m1.HasLadder, "S1 exact-volume MC set overrides the chain: McBidAsk/High, buy 8 sell 4");
			Check(Near(m1.Bid(100.00), 4) && Near(m1.Ask(100.00), 3) && Near(m1.Ask(100.25), 5) && Near(m1.McDelta, 4), "S1 MC ladder rows and McDelta");
			em.AddQuote(Ms(1100), true, 100.00); em.AddQuote(Ms(1100), false, 100.25);
			em.AddTrade(Ms(1200), 100.25, 2);
			var m2 = em.CloseBar(Bar(2, Ms(2000), vol: 2));
			Check(m2.Source == Src.Quote && m2.McMiss == "missing" && em.McMissing == 1, "S1 missing MC set falls through to Quote and is counted");
			var cd = em.CumDelta(0);
			Check(cd.Source == Src.Quote && Near(cd.Value, 6), "S1 worst-of McBidAsk + Quote -> Quote got " + cd);
			// tolerance miss
			em.AddMcLevels(Ms(3000), new[] { 100.00 }, new[] { 5.0 }, new[] { 4.0 });   // 9 vs bar 10 -> 10% > 0.5%
			em.AddQuote(Ms(2400), true, 100.00); em.AddQuote(Ms(2400), false, 100.25);
			em.AddTrade(Ms(2500), 100.25, 10);
			var m3 = em.CloseBar(Bar(3, Ms(3000), vol: 10));
			Check(m3.Source == Src.Quote && m3.McMiss == "volMismatch" && em.McVolMismatch == 1 && double.IsNaN(m3.McDelta), "S1 tolerance miss falls through to Quote");
			// within tolerance (0.4%)
			em.AddMcLevels(Ms(4000), new[] { 100.00 }, new[] { 250.0 }, new[] { 251.0 });
			em.AddQuote(Ms(3400), true, 100.00); em.AddQuote(Ms(3400), false, 100.25);
			em.AddTrade(Ms(3500), 100.25, 503);
			var m4 = em.CloseBar(Bar(4, Ms(4000), vol: 503));
			Check(m4.Source == Src.McBidAsk && Near(m4.Delta, -1) && Near(m4.ChainDelta, 503), "S1 0.4% volume mismatch accepted, chain delta kept for T9");
			// a set stamped at another time never matches; older sets are dropped
			em.AddMcLevels(Ms(4500), new[] { 100.00 }, new[] { 1.0 }, new[] { 0.0 });
			var m5 = em.CloseBar(Bar(5, Ms(5000), vol: 0));
			Check(m5.McMiss == "missing" && em.PendingMcSets == 0, "S1 off-stamp set does not match and is dropped");
			// CrossCheckOnly: recorded, source stays Quote
			var ec = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = true, McBidAsk = OF.EMcBidAsk.CrossCheckOnly });
			ec.AddQuote(Ms(0), true, 100.00); ec.AddQuote(Ms(0), false, 100.25);
			ec.AddTrade(Ms(100), 100.25, 4);
			ec.AddMcLevels(Ms(1000), new[] { 100.25 }, new[] { 3.0 }, new[] { 1.0 });
			var c1 = ec.CloseBar(Bar(1, Ms(1000), vol: 4));
			Check(c1.Source == Src.Quote && Near(c1.Delta, 4) && Near(c1.McDelta, 2), "S1 CrossCheckOnly: Quote stays the source, MC delta recorded");
			// Off: sets ignored
			var eo = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false });
			eo.AddMcLevels(Ms(1000), new[] { 100.25 }, new[] { 3.0 }, new[] { 1.0 });
			eo.AddTrade(Ms(100), 100.25, 4);
			var o1 = eo.CloseBar(Bar(1, Ms(1000), vol: 4));
			Check(o1.Source == Src.TickRule && double.IsNaN(o1.McDelta) && o1.McMiss == null, "S1 Off (default): MC sets ignored");
			Check(new OF.FlowConfig().McBidAsk == OF.EMcBidAsk.Off && Near(new OF.FlowConfig().mcVolTolerance, 0.005), "S1 defaults Off, tolerance 0.005");
		}

		Console.WriteLine("OrderFlowTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}

	static OF.FootprintBar Footprint0(this OF.FlowEngine e) { return e.Closed(0); }
}
