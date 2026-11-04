using System;
using System.Collections.Generic;
using System.Linq;
using OF = Extracted.OrderFlow;
using Src = Extracted.OrderFlow.FlowSource;
using Q = Extracted.OrderFlow.FlowQuality;
using Side = Extracted.OrderFlow.Side;
using Regime = Extracted.OrderFlow.Regime;
using IH = Extracted.InstrHost;
using EAssetClass = Extracted.EAssetClass;
using Cat = Extracted.ESymbolCategory;
using LX = Extracted.LegacyExit;
using LXMode = Extracted.ESpecOrdersMode;

// Spec VPF-5 Rev 2.5 acceptance tests T1-T8 (analyst-base-class-spec.md §D), run on the shipped <order-flow> block.
static class Program
{
	static int s_fail, s_pass;
	static bool lxThrows(Action a) { try { a(); return false; } catch (InvalidOperationException) { return true; } }
	static string lxMessage(Action a) { try { a(); return ""; } catch (InvalidOperationException ex) { return ex.Message; } }
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }
	const double TICK = 0.25;
	static readonly DateTime T0 = new DateTime(2024, 3, 13, 10, 0, 0);
	static string MethodBody(string src, string sig)
	{
		int i = src.IndexOf(sig); if (i < 0) return null;
		int b = src.IndexOf('{', i), d = 0;
		for (int k = b; k < src.Length; k++) { if (src[k] == '{') d++; else if (src[k] == '}' && --d == 0) return src.Substring(b, k - b + 1); }
		return null;
	}
	// Typed-enum lint (Ettienne's rule, 2026-11-01): MultiCharts enums and API members are never coded as strings - no ToString()
	// compares or switches, no name strings for enum members, no Enum.Parse/TryParse of a non-repo enum, no reflection by member
	// name. One comment-stripped source line in, the ids of the rules it breaks out. ownEnums/ownTypes = enums/classes/structs
	// declared in this repo (parsing the repo's own config enums and reflecting over its own types is fine).
	// EResolution / ChartType member names (Points, Change, OrigPoints, PointAndFigure, LineBreak, HeikinAshi, FootPrint, CumDelta, ...)
	// and ESymbolCategory names (Future, Stock confirmed on Ettienne's install; the rest documented or plausible spellings)
	const string McNames = "Tick|Minute|Second|Hour|Day|Week|Month|Quarter|Year|Volume|Contract|Points|Point|PointOriginal|OrigPoints|Change|Range|"
		+ "Renko|Kagi|PointAndFigure|LineBreak|HeikinAshi|FootPrint|CumDelta|CumulativeDelta|Regular|"
		+ "Future|Futures|Stock|Forex|Crypto|Currency|Option|Index|Cash|Fund|MutualFund|Money|Bond|Spread|TickCount";
	static List<string> TypedEnumLint(string line, HashSet<string> ownEnums, HashSet<string> ownTypes)
	{
		var r = new List<string>();
		Func<string, bool> has = p => System.Text.RegularExpressions.Regex.IsMatch(line, p);
		if (has(@"\.ToString\(\)\s*(==|!=)") || has(@"(==|!=)\s*[\w\.\[\]]+\.ToString\(\)")) r.Add("tostring-compare");
		if (has(@"switch\s*\([^)]*\.ToString\(\)\s*\)")) r.Add("tostring-switch");
		if (has(@"\.ToString\(\)\s*\.(Equals|StartsWith|EndsWith|Contains|IndexOf)\s*\(") || has(@"(?<!\w)Equals\([^;]*\.ToString\(\)")) r.Add("tostring-equals");
		if (has(@"\.(Category|ChartType|Type)\.ToString\(\)")) r.Add("mc-enum-tostring");
		if (has(@"(==|!=)\s*""(" + McNames + @")""") || has(@"""(" + McNames + @")""\s*(==|!=)") || has(@"case\s+""(" + McNames + @")""\s*:")
			|| has(@"\(\s*""(FOREX|CRYPTO|CURRENCY|FUTURE|FUTURES|STOCK)""\s*\)")) r.Add("mc-member-name-string");
		// any casing of a whole MC member name handed to a string compare: x.Equals("future"), Contains("FOREX"),
		// string.Compare(a, "Tick", true), StartsWith("renko"), "Day".Equals(x). The name must sit inside the call's own argument
		// list (one level of nested parens allowed, e.g. Compare(a.Trim(), "Tick")): `if (s.Contains(k)) label = "Tick";` is not a compare.
		if (has(@"(?i)(?<!\w)(Equals|Contains|Compare|StartsWith|EndsWith)\s*\((?:[^;()]|\([^;()]*\))*?""(" + McNames + @")""")
			|| has(@"(?i)""(" + McNames + @")""\s*\.\s*(Equals|Contains|CompareTo|StartsWith|EndsWith)\s*\(")) r.Add("mc-name-string-compare");
		// enum reflection over a non-repo enum: Enum.GetName/IsDefined/ToObject(typeof(EResolution), ..)
		foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, @"Enum\.(GetName|IsDefined|ToObject)\s*\(\s*typeof\(\s*([\w\.]+)\s*\)"))
		{
			string ty = m.Groups[2].Value;
			if (!ownEnums.Contains(ty.Substring(ty.LastIndexOf('.') + 1))) r.Add("enum-reflect(" + m.Groups[1].Value + " " + ty + ")");
		}
		if (has(@"(?<!\w)Type\.GetType\s*\(\s*@?""")) r.Add("type-by-name");
		if (has(@"GetType\(\)\.Name\s*(==|!=)") || has(@"\.Name\s*(==|!=)\s*""\w*Attribute""")) r.Add("type-name-compare");
		foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, @"Enum\.(Try)?Parse\b"))
		{
			// the parsed enum: Parse(typeof(T)..), Parse<T>/TryParse<T>, or TryParse(.., out v) checked by Enum.IsDefined(typeof(T), v) on the line
			var t = System.Text.RegularExpressions.Regex.Match(line.Substring(m.Index), @"^Enum\.(?:Try)?Parse\s*(?:<\s*([\w\.]+)\s*>|\(\s*typeof\(\s*([\w\.]+)\s*\))");
			string ty = t.Success ? (t.Groups[1].Success ? t.Groups[1].Value : t.Groups[2].Value) : null;
			if (ty == null) { var d = System.Text.RegularExpressions.Regex.Match(line, @"Enum\.IsDefined\(\s*typeof\(\s*([\w\.]+)\s*\)"); if (d.Success) ty = d.Groups[1].Value; }
			if (ty == null || !ownEnums.Contains(ty.Substring(ty.LastIndexOf('.') + 1))) r.Add("enum-parse(" + (ty ?? "?") + ")");
		}
		foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, @"\.(GetProperty|GetField|GetMethod|GetMember|GetEvent|GetProperties|GetFields|InvokeMember)\s*\("))
		{
			// reflection over the repo's own type (typeof(Own).GetField(..)) is fine; anything else reads MultiCharts members by name
			var o = System.Text.RegularExpressions.Regex.Match(line.Substring(0, m.Index), @"typeof\(\s*(\w+)\s*\)\s*$");
			bool own = o.Success && ownTypes.Contains(o.Groups[1].Value);
			bool typedSweep = (m.Groups[1].Value == "GetProperties" || m.Groups[1].Value == "GetFields");   // no member name: not by-name
			if (!own && !typedSweep) r.Add("reflection-by-name(" + m.Groups[1].Value + ")");
		}
		return r;
	}
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

		// ---- PR G: CumDelta Truncated flag + MaxBars cap warning ----
		{
			var warns = new List<string>();
			var ec = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false }) { warnOnce = w => warns.Add(w) };
			Check(ec.Cfg.MaxBars == 5000 && ec.BarCap == 5000, "G1 default cap 5000 bars");
			double gpx = 100;
			for (int n = 1; n <= 5000; n++) { gpx += TICK; ec.AddTrade(Ms(n * 10), gpx, 1); ec.CloseBar(Bar(n, Ms(n * 10 + 5))); }   // every trade an uptick: delta +1 per bar
			var c5000 = ec.CumDelta(0);
			Check(Near(c5000.Value, 5000) && !c5000.Truncated && ec.DroppedBars == 0 && warns.Count == 0, "G1 at the cap (5000 bars): full CumDelta, not Truncated, no warning got " + c5000);
			gpx += TICK; ec.AddTrade(Ms(50010), gpx, 1); ec.CloseBar(Bar(5001, Ms(50015)));
			var c5001 = ec.CumDelta(0);
			Check(Near(c5001.Value, 5000) && c5001.Truncated && ec.DroppedBars == 1, "G1 bar 5001 drops bar 1: CumDelta Truncated (partial sum of the kept 5000) got " + c5001);
			Check(c5001.IsAvailable && c5001.Source == Src.TickRule && c5001.ToString().EndsWith(" trunc"), "G1 Truncated value stays available, tagged ' trunc'");
			Check(warns.Count == 1 && warns[0].StartsWith("OF MaxBars cap 5000 hit at bar 5001"), "G1 one warning when the cap is first hit: " + (warns.Count > 0 ? warns[0] : "none"));
			gpx += TICK; ec.AddTrade(Ms(50020), gpx, 1); ec.CloseBar(Bar(5002, Ms(50025)));
			Check(warns.Count == 1 && ec.DroppedBars == 2 && ec.CumDelta(0).Truncated, "G1 warning once only; later drops still Truncated");
			Check(!ec.BarDelta(0).Truncated && !OF.FlowValue.Unavailable().Truncated, "G1 BarDelta / Unavailable never Truncated");

			// small cap (min 10): a new reset period is complete; the period that lost bars is Truncated
			var es = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, MaxBars = 3 });   // no sinks: must not throw
			Check(es.BarCap == 10, "G2 cap floor 10");
			var sA = T0.Date; var sB = T0.Date.AddDays(1);
			int bn = 0; gpx = 100;
			for (int k = 0; k < 12; k++) { bn++; gpx += TICK; es.AddTrade(Ms(bn * 10), gpx, 1); es.CloseBar(Bar(bn, Ms(bn * 10 + 5), session: sA)); }
			Check(es.DroppedBars == 2 && es.CumDelta(0).Truncated && Near(es.CumDelta(0).Value, 10), "G2 session A 12 bars, cap 10: Truncated, sums the kept 10");
			bn++; gpx += TICK; es.AddTrade(Ms(bn * 10), gpx, 1); es.CloseBar(Bar(bn, Ms(bn * 10 + 5), session: sB));
			Check(!es.CumDelta(0).Truncated && Near(es.CumDelta(0).Value, 1) && es.CumDelta(1).Truncated, "G2 new reset period complete; prior period still Truncated");
			for (int k = 0; k < 9; k++) { bn++; gpx += TICK; es.AddTrade(Ms(bn * 10), gpx, 1); es.CloseBar(Bar(bn, Ms(bn * 10 + 5), session: sB)); }
			Check(!es.CumDelta(0).Truncated && Near(es.CumDelta(0).Value, 10), "G2 period B fills the cap exactly (all A dropped): not Truncated");
			bn++; gpx += TICK; es.AddTrade(Ms(bn * 10), gpx, 1); es.CloseBar(Bar(bn, Ms(bn * 10 + 5), session: sB));
			Check(es.CumDelta(0).Truncated && Near(es.CumDelta(0).Value, 10), "G2 period B's first bar dropped: Truncated");
		}

		// ---- PR G: journal line on the session's first quality downgrade ----
		{
			var lines = new List<string>();
			var ej = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = true }) { journal = j => lines.Add(j) };
			int t = 0, n = 0;
			Action<DateTime, bool> close = (sess, fresh) =>
			{
				n++; t += 10000;
				if (fresh) { ej.AddQuote(Ms(t - 100), true, 99.75); ej.AddQuote(Ms(t - 100), false, 100.00); }
				ej.AddTrade(Ms(t - 50), 100.00, 1);
				ej.CloseBar(Bar(n, Ms(t), session: sess));
			};
			var js1 = T0.Date; var js2 = T0.Date.AddDays(1);
			close(js1, true); close(js1, true);
			Check(lines.Count == 0 && ej.Closed(0).Quality == Q.High, "J1 Quote/High session: no journal line");
			close(js1, false);                                     // stale quote -> TickRule / Medium
			Check(lines.Count == 1 && lines[0] == "OF quality downgrade session(" + js1.ToString("yyyy-MM-dd") + ") bar(3) High->Medium source(TickRule) staleQuoteTrades(1)",
				"J1 first downgrade journaled: " + (lines.Count > 0 ? lines[0] : "none"));
			ej.CloseBar(new OF.BarInput { BarNumber = ++n, CloseTime = Ms(t += 10000), SessionKey = js1, ResetKey = js1, Regime = Regime.IogHistoryNoMagnifier });   // -> Unknown
			Check(lines.Count == 1 && ej.Closed(0).Quality == Q.Unknown, "J1 a further downgrade in the same session is not journaled again");
			close(js2, true);
			Check(lines.Count == 1, "J2 a new session's first bar sets its baseline (no line)");
			close(js2, false);
			Check(lines.Count == 2 && lines[1].Contains("session(" + js2.ToString("yyyy-MM-dd") + ")") && lines[1].Contains("High->Medium"), "J2 next session's first downgrade journaled once");
			var en = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false });   // journal null: no throw
			en.AddTrade(Ms(10), 100, 1); en.CloseBar(Bar(1, Ms(20)));
			en.CloseBar(new OF.BarInput { BarNumber = 2, CloseTime = Ms(40), SessionKey = T0.Date, ResetKey = T0.Date, Regime = Regime.IogHistoryNoMagnifier });
			Check(en.Closed(0).Quality == Q.Unknown, "J3 no journal sink: downgrade handled silently");
		}

		// ---- T1 DataLoader thread safety: callbacks run off the calc thread (MC "Bars. Calculation thread only.") ----
		{
			int calcThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
			int offThreadSinkCalls = 0;
			// stub calc-thread-only sink: throws (like MC Bars/Output) when called from another thread
			Action<string> calcOnly = s => { if (System.Threading.Thread.CurrentThread.ManagedThreadId != calcThread) { offThreadSinkCalls++; throw new InvalidOperationException("Bars. Calculation thread only."); } };
			var cfg = new OF.FlowConfig { QuotesExpected = true, McBidAsk = OF.EMcBidAsk.Prefer, MaxBars = 10 };
			var ef = new OF.FlowEngine(TICK, cfg) { journal = calcOnly, warnOnce = calcOnly };
			Exception loaderEx = null;
			var loader = new System.Threading.Thread(() =>   // fake DataLoader: History callback on its own thread
			{
				try
				{
					for (int i = 0; i < 30; i++)
					{
						ef.AddMcLevels(Ms(1000 * (i + 1)), new[] { 100.00 }, new[] { 1.0 }, new[] { 1.0 });
						ef.AddQuote(Ms(1000 * i + 100), true, 100.00); ef.AddQuote(Ms(1000 * i + 100), false, 100.25);
						ef.AddTrade(Ms(1000 * i + 200), 100.25, 2);
					}
				}
				catch (Exception ex) { loaderEx = ex; }
			});
			loader.Start(); loader.Join();
			Check(loaderEx == null && offThreadSinkCalls == 0, "T1 fake loader thread: AddMcLevels/AddQuote/AddTrade never call a calc-thread sink");
			Exception calcEx = null;
			try { for (int i = 0; i < 30; i++) ef.CloseBar(Bar(i + 1, Ms(1000 * (i + 1)), vol: 2)); } catch (Exception ex) { calcEx = ex; }
			Check(calcEx == null && ef.DroppedBars > 0, "T1 sinks fire only from CloseBar on the calc thread (MaxBars warning reached)");

			// source guard: the strategy's DataLoader callbacks and their helpers must not reference calc-thread-only members
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileFuturesBase.Strategy.CS"));
			string[] offThread = { "onFlowData", "onMcLevels", "flowTick", "mcSet", "mcDiag", "enqueueFlowMsg", "onChunkData", "releaseChunks" };   // + tick-chunk spec §5.2 callback helpers
			string[] banned = { "Bars.", "Bars[", "Output", "trace(", "engineSink", "Environment.", "ToEastern", "clock()", "tickSize()", "BarsOfData", "StatusLine" };
			foreach (string m in offThread)
			{
				string body = MethodBody(src, "private void " + m + "(");
				Check(body != null, "T1 source guard found " + m);
				if (body == null) continue;
				string hit = banned.FirstOrDefault(b => body.Contains(b));
				Check(hit == null, "T1 " + m + " (DataLoader thread) uses no calc-thread member, found " + hit);
			}
			// Recalc spec §6: helpers the callback fallback calls (non-void signatures)
			foreach (string sig in new[] { "private _OrderFlow.RecalcDecision mcRecalcDecision(", "private string mcTriggeredLine(" })
			{
				string body = MethodBody(src, sig);
				Check(body != null, "R-G source guard found " + sig);
				if (body == null) continue;
				string hit = banned.FirstOrDefault(b => body.Contains(b));
				Check(hit == null, "R-G " + sig + " (DataLoader thread) uses no calc-thread member, found " + hit);
			}
			// onMcLevels: ExecControl.Recalculate() only as the last statement, outside the try (spec §6)
			{
				string body = MethodBody(src, "private void onMcLevels(");
				int rc = body == null ? -1 : body.IndexOf("ExecControl.Recalculate()");
				int lastTry = body == null ? -1 : body.LastIndexOf("catch (Exception ex)");
				Check(rc > lastTry && lastTry > 0 && body.Substring(rc).TrimEnd().EndsWith("}") && body.Substring(rc + "ExecControl.Recalculate();".Length).Split('\n').Skip(1).All(l => l.Trim() == "}" || l.Trim() == ""),
					"R-G onMcLevels: Recalculate() after the try/catch and last");
				string chk = MethodBody(src, "private bool mcRecalcCheck(");
				int r2 = chk == null ? -1 : chk.IndexOf("ExecControl.Recalculate();");
				Check(r2 > 0 && !chk.Contains("catch (") && chk.Substring(r2).Contains("return true;") && chk.Substring(r2 + 26).Trim().StartsWith("// ") , "R-G mcRecalcCheck: no try, Recalculate() then return");
			}
		}

		// ---- Recalc spec §9: McLevelCache ----
		{
			var c = new OF.McLevelCache();
			Check(c.MissReason("s", Ms(0), Ms(1)) == "empty" && !c.Covers(Ms(0), Ms(1)), "RC1 empty cache misses (empty)");
			c.Signature = "s"; c.RequestFrom = Ms(0); c.RequestTo = Ms(100000);
			double[] cp1 = { 100.00, 100.25 }, ca1 = { 1, 2 }, cb1 = { 3, 0 };
			for (int i = 1; i <= 5; i++) c.Add(Ms(i * 10000), cp1, ca1, cb1);
			cp1[0] = 999;   // the cache cloned its input
			c.Add(Ms(30000), new[] { 100.00 }, new[] { 5.0 }, new[] { 0.0 });   // replace one set
			Check(c.Sets == 5 && c.Levels == 9 && c.First == Ms(10000) && c.Last == Ms(50000), "RC1 Add counts sets/levels, replace adjusts levels: " + c.Sets + "/" + c.Levels);
			Check(c.MissReason("s", Ms(10000), Ms(50000)) == "incomplete", "RC1 not HistoryDone -> incomplete");
			c.HistoryDone = true;
			Check(c.MissReason("s", Ms(10000), Ms(50000)) == null && c.Covers(Ms(0), Ms(100000)), "RC1 complete + inside the requested window -> hit");
			Check(c.MissReason("s", Ms(10000), Ms(200000)) == "coverage", "RC1 past the requested window -> coverage");
			Check(c.MissReason("t", Ms(10000), Ms(50000)) == "signature", "RC1 other signature -> signature");
			Check(c.Covers(Ms(0), Ms(5000)), "RC1 coverage is the requested window, not First/Last (no levels at the edges)");

			// replay == direct AddMcLevels
			Func<bool, string> run = viaCache =>
			{
				var e = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, McBidAsk = OF.EMcBidAsk.Prefer, mcVolTolerance = 0.5 });
				var cc = new OF.McLevelCache();
				for (int i = 1; i <= 4; i++)
				{
					double[] lpx = { 100.00, 100.25 }, lask = { i, 1 }, lbid = { 1, 2 * i };
					if (viaCache) cc.Add(Ms(i * 1000), lpx, lask, lbid); else e.AddMcLevels(Ms(i * 1000), lpx, lask, lbid);
				}
				if (viaCache) Check(cc.ReplayInto(e) == 4, "RC2 ReplayInto returns the set count");
				var parts = new List<string>();
				for (int i = 1; i <= 4; i++) { e.CloseBar(Bar(i, Ms(i * 1000), Regime.BarsOnly, vol: 2 + 3 * i)); parts.Add(Sig(e, 0) + "|" + e.Closed(0).Source + "|" + e.Closed(0).McDelta); }
				return string.Join("#", parts) + "|" + e.McUsed;
			};
			string direct = run(false), cached = run(true);
			Check(direct == cached && direct.EndsWith("|4"), "RC2 cache replay gives the same footprints as direct AddMcLevels: " + cached);
			c.Clear();
			Check(c.Sets == 0 && c.Levels == 0 && c.Signature == null && !c.HistoryDone && c.ReplayInto(new OF.FlowEngine(TICK, new OF.FlowConfig())) == 0 && c.MissReason("s", Ms(0), Ms(1)) == "empty",
				"RC3 Clear (signature change) empties the cache");
		}

		// ---- Recalc spec §9: McRecalcGate ----
		{
			var cfg = new OF.FlowConfig();
			Check(cfg.McRecalcOnLoad && cfg.McRecalcMax == 1 && cfg.McIdlePollMs == 1000 && cfg.McIdleTimeoutSec == 900 && !cfg.McRecalcFromCallback, "RG0 FlowConfig recalc defaults");
			var g = new OF.McRecalcGate();
			g.BeginPass();
			Check(g.Pass == 1 && g.Decide(false, true, false, true, 1, "s") == OF.RecalcDecision.Wait, "RG1 history not done -> Wait");
			Check(g.Decide(true, true, false, true, 1, "s") == OF.RecalcDecision.SkipNoMissed, "RG2 no missed bars -> SkipNoMissed");
			g.NoteMissed(Ms(3000)); g.NoteMissed(Ms(1000)); g.NoteMissed(Ms(2000));
			Check(g.MissedBeforeDone == 3 && g.MissedFirst == Ms(1000) && g.MissedLast == Ms(3000), "RG3 missed counters");
			Check(g.Decide(true, false, false, true, 1, "s") == OF.RecalcDecision.SkipNoMissed, "RG3 cache does not cover the missed bars -> SkipNoMissed");
			Check(g.Decide(true, true, false, false, 1, "s") == OF.RecalcDecision.SkipDisabled, "RG4 McRecalcOnLoad false -> SkipDisabled");
			Check(g.Decide(true, true, true, true, 1, "s") == OF.RecalcDecision.SkipAutoTrading, "RG5 auto trading -> SkipAutoTrading");
			Check(g.Decide(true, true, false, true, 1, "s") == OF.RecalcDecision.Recalc, "RG6 missed + covered -> Recalc");
			g.NoteRecalc("s");
			g.BeginPass();   // pass 2 (after Recalculate): missed counters restart, count kept
			Check(g.Pass == 2 && g.MissedBeforeDone == 0 && g.RecalcCount == 1 && g.CountFor("s") == 1, "RG7 BeginPass keeps the recalc count");
			g.NoteMissed(Ms(1000));
			Check(g.Decide(true, true, false, true, 1, "s") == OF.RecalcDecision.SkipGuard, "RG8 second recalc for the same signature -> SkipGuard (no loop)");
			Check(g.Decide(true, true, false, true, 1, "t") == OF.RecalcDecision.Recalc && g.CountFor("t") == 0, "RG9 a new signature may recalc once");
			g.NoteRecalc("t");
			Check(g.RecalcSignature == "t" && g.RecalcCount == 1, "RG9 NoteRecalc restarts the count on a new signature");
			Check(OF.McRecalcGate.SkipReason(OF.RecalcDecision.SkipNoMissed) == "noMissedBars" && OF.McRecalcGate.SkipReason(OF.RecalcDecision.SkipGuard) == "guard"
				&& OF.McRecalcGate.SkipReason(OF.RecalcDecision.SkipAutoTrading) == "autoTrading" && OF.McRecalcGate.SkipReason(OF.RecalcDecision.SkipDisabled) == "disabled", "RG10 skip reasons");
		}

		// ---- Gate-ABA (Live Check 9a rerun, Ettienne 2026-11-01): A -> B -> A must allow a recalc for the fresh A download ----
		{
			// mirrors mcStartOrReplay (cache miss -> Forget on a signature miss) and mcRecalcCheck (Decide, process guard, NoteRecalc)
			var cache = new OF.McLevelCache(); var gate = new OF.McRecalcGate(); var pg = new OF.McRecalcLoopGuard();
			var win = TimeSpan.FromSeconds(600); DateTime now = T0; int maxPerKey = 3;
			Func<string, int, bool, OF.RecalcDecision> pass = (sig, levelSets, missed) =>
			{
				gate.BeginPass();
				OF.McCacheMiss miss = cache.Miss(sig, Ms(0), Ms(60000));
				if (miss != OF.McCacheMiss.None)
				{
					if (miss == OF.McCacheMiss.Signature && gate.RecalcSignature != null) gate.Forget();
					cache.Clear(); cache.Signature = sig; cache.RequestFrom = Ms(0); cache.RequestTo = Ms(60000);
					if (missed) gate.NoteMissed(Ms(1000));
					for (int i = 0; i < levelSets; i++) cache.Add(Ms(1000 * (i + 1)), new[] { 100.0 }, new[] { 1.0 }, new[] { 1.0 });
					cache.HistoryDone = true;
				}
				var d = gate.Decide(true, gate.MissedBeforeDone > 0 && cache.Sets > 0, false, true, 1, sig);
				if (d == OF.RecalcDecision.Recalc && !pg.TryAcquire(OF.GuardKey("Ns.S", sig), now, maxPerKey, win)) d = OF.RecalcDecision.SkipProcessGuard;
				if (d == OF.RecalcDecision.Recalc) gate.NoteRecalc(sig);
				now = now.AddSeconds(10);
				return d;
			};
			Check(pass("A", 3, true) == OF.RecalcDecision.Recalc && gate.RecalcSignature == "A" && gate.RecalcCount == 1, "AB1 A: first download with missed bars -> Recalc");
			Check(pass("A", 3, true) == OF.RecalcDecision.SkipNoMissed, "AB1 A after Recalculate: cache hit, nothing missed -> no recalc");
			Check(pass("B", 3, false) == OF.RecalcDecision.SkipNoMissed && gate.RecalcSignature == null && gate.RecalcCount == 0, "AB2 B (no missed bars): signature miss forgets A's recalc");
			Check(cache.Miss("A", Ms(0), Ms(60000)) == OF.McCacheMiss.Signature, "AB3 back to A: the cache holds B -> signature miss");
			Check(pass("A", 3, true) == OF.RecalcDecision.Recalc && gate.RecalcSignature == "A" && gate.RecalcCount == 1, "AB3 A -> B -> A: the fresh A download may recalc again");
			// B with no level set (completed, Sets 0) still counts as a different request
			Check(pass("B", 0, false) == OF.RecalcDecision.SkipNoMissed && gate.RecalcSignature == null && cache.Miss("A", Ms(0), Ms(60000)) == OF.McCacheMiss.Signature,
				"AB4 B with no level set: A is still a signature miss (not empty) and the gate was reset");
			// same signature: a fresh pass with missed bars that is not a signature miss is refused at McRecalcMax
			var g5 = new OF.McRecalcGate(); g5.BeginPass(); g5.NoteMissed(Ms(1000));
			Check(g5.Decide(true, true, false, true, 1, "A") == OF.RecalcDecision.Recalc, "AB5 setup: first recalc for A");
			g5.NoteRecalc("A"); g5.BeginPass(); g5.NoteMissed(Ms(1000));
			var c5 = new OF.McLevelCache { Signature = "A" }; c5.RequestFrom = Ms(0); c5.RequestTo = Ms(60000);
			Check(c5.Miss("A", Ms(0), Ms(60000)) == OF.McCacheMiss.Incomplete && g5.Decide(true, true, false, true, 1, "A") == OF.RecalcDecision.SkipGuard && g5.CountFor("A") == 1,
				"AB5 same signature again (incomplete, not a signature miss -> no Forget) is refused at McRecalcMax = 1");
			Check(g5.Decide(true, true, false, true, 2, "A") == OF.RecalcDecision.Recalc, "AB5 McRecalcMax = 2 allows the second");
			// process guard: alternating A/B (each a signature miss -> Forget) still trips the static guard at 3 per key per window
			int recalcs = 0;
			for (int k = 0; k < 6; k++) if (pass(k % 2 == 0 ? "C" : "D", 3, true) == OF.RecalcDecision.Recalc) recalcs++;
			Check(recalcs == 6, "AB6 alternating C/D (6 switches in 60 s): the per-object gate is reset on each signature miss");
			Check(pass("C", 3, true) == OF.RecalcDecision.SkipProcessGuard && gate.RecalcSignature == null && pg.CountFor(OF.GuardKey("Ns.S", "C"), now, win) == 3,
				"AB7 the 4th recalc of C within 600 s is refused by the process guard (McRecalcGuardMaxPerKey 3)");
			Check(pass("D", 3, true) == OF.RecalcDecision.SkipProcessGuard, "AB7 and of D");
			now = now.AddSeconds(600);
			Check(pass("C", 3, true) == OF.RecalcDecision.Recalc, "AB7 allowed again after McRecalcGuardWindowSec");
			// skip line format
			var g8 = new OF.McRecalcGate();
			Check(g8.SkipLine(OF.RecalcDecision.SkipGuard, "A", 0) == "OF McBidAsk recalc skipped reason(guard) recalcCount(0/0) recalcSig(none)", "AB8 skip line, McRecalcMax = 0: count 0/0, no previous recalc");
			g8.NoteRecalc("A");
			Check(g8.SkipLine(OF.RecalcDecision.SkipGuard, "A", 1) == "OF McBidAsk recalc skipped reason(guard) recalcCount(1/1) recalcSig(A)", "AB8 skip line, after a recalc: 1/1 and its signature");
			Check(g8.SkipLine(OF.RecalcDecision.SkipNoMissed, "B", 1) == "OF McBidAsk recalc skipped reason(noMissedBars) recalcCount(0/1) recalcSig(A)", "AB8 skip line, other signature: count 0, the gate's signature");
			g8.Forget();
			Check(g8.RecalcSignature == null && g8.RecalcCount == 0 && g8.CountFor("A") == 0 && g8.Pass == 0, "AB9 Forget clears the signature and count only");
			Check(OF.McLevelCache.MissName(OF.McCacheMiss.None) == null && OF.McLevelCache.MissName(OF.McCacheMiss.Signature) == "signature" && OF.McLevelCache.MissName(OF.McCacheMiss.Empty) == "empty"
				&& OF.McLevelCache.MissName(OF.McCacheMiss.Incomplete) == "incomplete" && OF.McLevelCache.MissName(OF.McCacheMiss.Coverage) == "coverage", "AB10 miss journal names");
			// strategy wiring (source)
			string vpa = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileFuturesBase.Strategy.CS"));
			string sr = MethodBody(vpa, "private void mcStartOrReplay(") ?? "";
			Check(sr.Contains("miss = m_mcCache.Miss(sig, firstBar, lastBar);") && sr.Contains("if (miss == _OrderFlow.McCacheMiss.Signature && m_mcGate.RecalcSignature != null) { m_mcGate.Forget(); forgot = true; }")
				&& sr.IndexOf("m_mcGate.Forget()") < sr.IndexOf("m_mcCache.Clear();") && !sr.Contains("MissReason") && !sr.Contains("\"signature\""), "AB11 mcStartOrReplay: typed signature miss resets the gate (under m_flowLock, before Clear)");
			string rc = MethodBody(vpa, "private bool mcRecalcCheck(") ?? "";
			Check(rc.Contains("skipLine = m_mcGate.SkipLine(d, m_mcSig, FlowCfg.McRecalcMax);") && rc.Contains("engineSink(ETraceLevel.Journal)?.Invoke(skipLine);")
				&& rc.IndexOf("skipLine = m_mcGate.SkipLine") < rc.IndexOf("if (!done) return false;"), "AB12 mcRecalcCheck: the skip line is built under m_flowLock with recalcCount/recalcSig");
		}

		// ---- Wait mode per-session source summary (gate-ABA PR item 3) ----
		{
			var t = new OF.SessionSourceTally(); DateTime sa = new DateTime(2026, 10, 27), sb = sa.AddDays(1);
			Check(t.Add(sa, OF.FlowSource.Quote) == null && t.Add(sa, OF.FlowSource.Quote) == null && t.Add(sa, OF.FlowSource.TickRule) == null, "SS1 same session: no line");
			string l1 = t.Add(sb, OF.FlowSource.Quote);
			Check(l1 == "OF tick source session(2026-10-27) bars(3) quote(2) tickRule(1) mcBidAsk(0) aggressor(0) barUpDown(0) unavailable(0)", "SS2 session change: the finished session's line [" + l1 + "]");
			t.Add(sb, OF.FlowSource.Quote);
			string l2 = t.Flush();
			Check(l2 == "OF tick source session(2026-10-28) bars(2) quote(2) tickRule(0) mcBidAsk(0) aggressor(0) barUpDown(0) unavailable(0)", "SS3 Flush: the open session, an all-Quote session is visible (tickRule(0)) [" + l2 + "]");
			Check(t.Flush() == null, "SS4 Flush twice: nothing more");
			t.Add(sb, OF.FlowSource.BarUpDown);
			Check(t.Flush() == null, "SS5 deduped: a session already reported is not reported again");
			var t2 = new OF.SessionSourceTally();
			t2.Add(sa, OF.FlowSource.McBidAsk); t2.Add(sa, OF.FlowSource.BarUpDown); t2.Add(sa, OF.FlowSource.Unavailable); t2.Add(sa, OF.FlowSource.Aggressor);
			Check(t2.Flush() == "OF tick source session(2026-10-27) bars(4) quote(0) tickRule(0) mcBidAsk(1) aggressor(1) barUpDown(1) unavailable(1)", "SS6 every source counted");
			string vps = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileFuturesBase.Strategy.CS"));
			Check(vps.Contains("if (closedFp != null && m_chunks != null && FlowCfg.TickChunkWaitMs > 0)") && vps.Contains("srcLine = m_srcTally.Add(closedFp.SessionKey, closedFp.Source);")
				&& vps.Contains("if (srcLine != null) engineSink(ETraceLevel.Info)?.Invoke(srcLine);") && vps.Contains("lock (m_flowLock) srcLine = m_srcTally != null ? m_srcTally.Flush() : null;")
				&& vps.Contains("m_srcTally = null;"), "SS7 strategy: wait mode only, Info, per pass (reset in StartCalc), open session flushed in StopCalc");
		}

		// ---- volMismatch fix (Live Check 9a, Ettienne 2026-11-01): bar total = Bars.Ticks, Build Volume On, diag line ----
		{
			var cfg = new OF.FlowConfig { QuotesExpected = false, McBidAsk = OF.EMcBidAsk.Prefer, AllowMixed = true };
			var ev = new OF.FlowEngine(TICK, cfg);
			// bar: up 5, down 4, unchanged 3 -> Bars.Ticks 12, intraday Bars.Volume 5 (up only); MC set ask 7 + bid 5 = 12
			ev.AddMcLevels(Ms(1000), new[] { 100.00, 100.25 }, new[] { 3.0, 4.0 }, new[] { 5.0, 0.0 });
			var v1 = ev.CloseBar(Bar(1, Ms(1000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			Check(v1.Source == Src.McBidAsk && v1.McMiss == null && Near(v1.McAsk, 7) && Near(v1.McBid, 5) && ev.McVolMismatch == 0, "VB1 total = Ticks (12) matches ask+bid 12 -> McBidAsk");
			ev.AddMcLevels(Ms(2000), new[] { 100.00 }, new[] { 7.0 }, new[] { 5.0 });
			var v2 = ev.CloseBar(Bar(2, Ms(2000), Regime.BarsOnly, vol: 5, up: 5, down: 4));
			Check(v2.Source == Src.Unavailable && v2.McMiss == "volMismatch" && ev.McVolMismatch == 1 && Near(v2.McAsk, 7) && Near(v2.McBid, 5) && double.IsNaN(v2.McDelta),
				"VB2 the old feed (up volume 5 vs 12) is a volMismatch; McAsk/McBid kept for the diag");
			var v3 = ev.CloseBar(Bar(3, Ms(3000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			Check(v3.McMiss == "missing" && double.IsNaN(v3.McAsk) && double.IsNaN(v3.McBid), "VB3 no set: McAsk/McBid NaN");
			// non-futures Build Volume On = Tick Count (Ettienne 5:22 PM): no exception, no Warning; McBidAsk never compared, reason volumeBasis, Info once
			var warns = new List<string>(); var infos = new List<string>(); bool vbThrew = false;
			var evk = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, McBidAsk = OF.EMcBidAsk.Prefer, BuildVolumeOn = OF.VolumeBasis.TickCount }) { warnOnce = w => warns.Add(w), infoOnce = w => infos.Add(w) };
			OF.FootprintBar k1 = null, k2 = null;
			try {
			evk.AddMcLevels(Ms(1000), new[] { 100.00 }, new[] { 7.0 }, new[] { 5.0 });
			k1 = evk.CloseBar(Bar(1, Ms(1000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			evk.AddMcLevels(Ms(2000), new[] { 100.00 }, new[] { 7.0 }, new[] { 5.0 });
			k2 = evk.CloseBar(Bar(2, Ms(2000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			} catch (Exception) { vbThrew = true; }
			Check(!vbThrew && k1 != null && k1.Source == Src.Unavailable && k1.McMiss == "volumeBasis" && k2.McMiss == "volumeBasis" && evk.McVolMismatch == 0 && evk.McUsed == 0 && double.IsNaN(k1.McDelta),
				"VB4 TickCount: Unavailable reason(volumeBasis), not volMismatch");
			Check(warns.Count == 0 && infos.Count == 1 && infos[0] == OF.FlowEngine.VolumeBasisInfo && infos[0].StartsWith("OF McBidAsk Unavailable reason(volumeBasis)"),
				"VB5 TickCount: no exception, no Warning, the volumeBasis line once at Info (" + warns.Count + "/" + infos.Count + ")");
			var tvInfo = new List<string>();
			var etv = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, McBidAsk = OF.EMcBidAsk.Prefer, BuildVolumeOn = OF.VolumeBasis.TradeVolume }) { warnOnce = w => tvInfo.Add(w), infoOnce = w => tvInfo.Add(w) };
			etv.AddMcLevels(Ms(1000), new[] { 100.00 }, new[] { 7.0 }, new[] { 5.0 });
			var tv1 = etv.CloseBar(Bar(1, Ms(1000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			Check(tv1.Source == OF.FlowSource.McBidAsk && tv1.McMiss == null && tvInfo.Count == 0, "VB5b Trade Volume passes: McBidAsk, no note");
			var wo = new List<string>();
			var eo = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, BuildVolumeOn = OF.VolumeBasis.TickCount }) { warnOnce = w => wo.Add(w), infoOnce = w => wo.Add(w) };
			var o1 = eo.CloseBar(Bar(1, Ms(1000), Regime.BarsOnly, vol: 12, up: 5, down: 4));
			Check(o1.Source == Src.Unavailable && o1.McMiss == null && wo.Count == 0, "VB6 TickCount with McBidAsk Off: Unavailable as before, no reason, no note");
			var eu = new OF.FlowEngine(TICK, new OF.FlowConfig { QuotesExpected = false, McBidAsk = OF.EMcBidAsk.Prefer }) { warnOnce = w => wo.Add(w) };
			eu.AddMcLevels(Ms(1000), new[] { 100.00 }, new[] { 7.0 }, new[] { 5.0 });
			Check(eu.CloseBar(Bar(1, Ms(1000), Regime.BarsOnly, vol: 12, up: 5, down: 4)).Source == Src.McBidAsk && wo.Count == 0, "VB7 Unknown basis (unmapped BVO value): existing behaviour, MC set compared");
			// BarUpDownQualifies against the Ticks total
			Check(OF.FlowEngine.BarUpDownQualifies(OF.VolumeBasis.TradeVolume, 13, 7, 6) && !OF.FlowEngine.BarUpDownQualifies(OF.VolumeBasis.TradeVolume, 16, 7, 6)
				&& !OF.FlowEngine.BarUpDownQualifies(OF.VolumeBasis.Unknown, 13, 7, 6) && !OF.FlowEngine.BarUpDownQualifies(OF.VolumeBasis.TickCount, 13, 7, 6),
				"BU1 up + down == Ticks qualifies; unchanged volume (7 + 6 != 16) does not; TradeVolume only");
			var evb = Engine(false, OF.VolumeBasis.TradeVolume);
			evb.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 16, up: 7, down: 6));
			Check(evb.BarDelta(0).Source == Src.Unavailable, "BU2 unchanged volume: up 7 + down 6 != Ticks 16 -> Unavailable");
			var ed = Engine(true, OF.VolumeBasis.TradeVolume);   // all-down bar in a tick gap: Bars.Volume (up) is 0, Ticks is 8
			ed.CloseBar(Bar(1, Ms(100), Regime.TickStream, vol: 8, up: 0, down: 8));
			Check(ed.BarDelta(0).Source == Src.BarUpDown && Near(ed.BarDelta(0).Value, -8), "BU3 all-down bar with total 8 is not an empty bar: BarUpDown delta -8");
			// diag picker: first 10 closed bars + first 10 volMismatch bars, counted apart
			var dg = new OF.McVolDiag(); int taken = 0; var takenAt = new List<int>();
			for (int i = 0; i < 30; i++) if (dg.Take(i < 10 ? null : "volMismatch")) { taken++; takenAt.Add(i); }
			Check(taken == 20 && takenAt[9] == 9 && takenAt[10] == 10 && takenAt[19] == 19, "DG1 10 closed + 10 volMismatch bars, nothing after (" + taken + ")");
			var dg2 = new OF.McVolDiag(); int t2 = 0;
			for (int i = 0; i < 30; i++) if (dg2.Take(i < 3 || i >= 20 ? "volMismatch" : (i % 2 == 0 ? "missing" : null))) t2++;
			Check(t2 == 17 && dg2.Mismatch == 10 && dg2.Closed == 30, "DG2 early mismatches count in both; other misses only as closed bars (" + t2 + ")");
			string dl = OF.McVolDiag.Line(42, new DateTime(2026, 10, 27, 9, 31, 0), 5, 12, 5, 4, 10, 7, 5, "volMismatch");
			Check(dl == "OF McBidAsk diag vol bar(42) t(2026-10-27 09:31:00) volume(5) ticks(12) up(5) down(4) prevTicks(10) mcAsk(7) mcBid(5) mcSum(12) miss(volMismatch)", "DG3 line format [" + dl + "]");
			string dn = OF.McVolDiag.Line(1, new DateTime(2026, 10, 27, 9, 31, 0), 5, 12.5, 5, 4, double.NaN, double.NaN, double.NaN, null);
			Check(dn == "OF McBidAsk diag vol bar(1) t(2026-10-27 09:31:00) volume(5) ticks(12.5) up(5) down(4) prevTicks(NaN) mcAsk(NaN) mcBid(NaN) mcSum(NaN) miss(none)", "DG4 NaN / no miss [" + dn + "]");
			string vf = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileFuturesBase.Strategy.CS"));
			string vb = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileBase.Strategy.CS"));
			string cs16 = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_CustomSignal.Strategy.CS"));
			Check(vf.Contains("double barTicks = Bars.Ticks[0], barUp = Bars.UpTicks[0], barDown = Bars.DownTicks[0];") && vf.Contains("Volume = barTicks, UpTicks = barUp, DownTicks = barDown")
				&& !vf.Contains("Volume = Bars.Volume[0], UpTicks") && vf.Contains("infoOnce = queueSink(ETraceLevel.Info)"), "VB8 strategy feeds Bars.Ticks[0] as the bar total; the volumeBasis note is Info");
			Check(vb.Contains("Bars.Status == EBarState.Close && Bars.Ticks[0] == 0") && !vb.Contains("Bars.Volume[0] == 0"), "VB9 zero-volume check reads Bars.Ticks[0]");
			// Build Volume On from the typed Bars.Info.BVO (_McApiDump4: Volume (249) / TickCount (324)); futures + Tick Count throws at StartCalc
			string vbErr = OF.FlowEngine.VolumeBasisError(true, OF.VolumeBasis.TickCount, "Data1 @ES");
			Check(vbErr == "Futures data stream Data1 @ES: Build Volume On must be Trade Volume (Format Instrument > Settings), but it is set to Tick Count. "
				+ "Tick Count counts trades, not contracts, so volume profiles, VWAP and order flow would be wrong.", "VB10 futures + Tick Count: the StartCalc error names the stream [" + vbErr + "]");
			string vsc = MethodBody(vb, "protected override void StartCalc()") ?? "";
			int iMap = vsc.IndexOf("ChartVolumeBasis = volumeBasisOf(Bars.Info.BVO);", StringComparison.Ordinal), iRef = vsc.IndexOf("refuseTickCountFutures();", StringComparison.Ordinal),
				iBase = vsc.IndexOf("base.StartCalc();", StringComparison.Ordinal), iVal = vsc.IndexOf("validateInputs();", StringComparison.Ordinal);
			string rtf = MethodBody(vb, "private void refuseTickCountFutures()") ?? "";
			int iErr = rtf.IndexOf("string error = _OrderFlow.FlowEngine.VolumeBasisError(isFuturesCategory(Bars.Info.Category), ChartVolumeBasis, \"Data1 \" + Bars.Info.Name);", StringComparison.Ordinal),
				iLog = rtf.IndexOf("traceAlways(ETraceLevel.Error, Bars.Info.Name, error);", StringComparison.Ordinal), iThrow = rtf.IndexOf("throw new InvalidOperationException(error);", StringComparison.Ordinal);
			int iRun = vsc.IndexOf("issueRunId();", StringComparison.Ordinal);
			Check(iRun >= 0 && iMap > iRun && iRef > iMap && iBase > iRef && iVal > iBase && iErr >= 0 && iLog > iErr && iThrow > iLog && !vsc.Contains("catch") && !rtf.Contains("catch")
				&& vsc.IndexOf("volumeBasisError", StringComparison.Ordinal) < 0,
				"VB11 _VolumeProfileBase.StartCalc: typed BVO map + refuseTickCountFutures BEFORE base.StartCalc (no engine, level study or portfolio row starts); Error logged, then InvalidOperationException");
			Check(OF.FlowEngine.VolumeBasisError(true, OF.VolumeBasis.TradeVolume, "@ES") == null && OF.FlowEngine.VolumeBasisError(true, OF.VolumeBasis.Unknown, "@ES") == null,
				"VB12 futures + Trade Volume passes; futures + Unknown keeps the old behaviour (no throw)");
			Check(OF.FlowEngine.VolumeBasisError(false, OF.VolumeBasis.TickCount, "AAPL") == null && OF.FlowEngine.VolumeBasisError(false, OF.VolumeBasis.TradeVolume, "AAPL") == null,
				"VB13 stock + Tick Count: no exception (the Info-once note is VB5)");
			string vbo = MethodBody(vb, "protected static _OrderFlow.VolumeBasis volumeBasisOf(BuildVolumeOn bvo)") ?? "";
			Check(vbo.Contains("switch (bvo)") && vbo.Contains("case BuildVolumeOn.Volume: return _OrderFlow.VolumeBasis.TradeVolume;") && vbo.Contains("case BuildVolumeOn.TickCount: return _OrderFlow.VolumeBasis.TickCount;")
				&& vbo.Contains("default: return _OrderFlow.VolumeBasis.Unknown;") && !vbo.Contains("ToString") && !vbo.Contains("Enum.") && !vbo.Contains("\"") && !vbo.Contains("249") && !vbo.Contains("324") && !vbo.Contains("(int)"),
				"VB14 typed BuildVolumeOn map: Volume -> TradeVolume, TickCount -> TickCount, else Unknown (typed members only: no strings, int literals or Enum.*)");
			int iCfg = vf.IndexOf("FlowCfg.BuildVolumeOn = ChartVolumeBasis;", StringComparison.Ordinal), iNew = vf.IndexOf("m_flow = new _OrderFlow.FlowEngine(", StringComparison.Ordinal);
			string vfsc = MethodBody(vf, "protected override void StartCalc()") ?? "";
			int iFb = vfsc.IndexOf("base.StartCalc();", StringComparison.Ordinal), iFo = vfsc.IndexOf("startOrderFlow();", StringComparison.Ordinal);
			Check(iCfg >= 0 && iNew > iCfg && iFb >= 0 && iFo > iFb, "VB15 VPFB: base.StartCalc (sets ChartVolumeBasis, refuses futures + Tick Count) runs before startOrderFlow, which sets FlowCfg.BuildVolumeOn before the engine is built");
			// VB16 BLOCKER fix: the Error line does not depend on TraceLevel (default None). LogFileSingleton is MC-bound (_CustomSignal), so
			// this is a source check: traceAlways has no TraceLevel / traceOn gate and writes straight through LogFileSingleton.WriteTrace,
			// whose body filters nothing by level.
			string ta = MethodBody(cs16, "protected void traceAlways(ETraceLevel traceLevel, string symbol, string message)") ?? "";
			string wt16 = MethodBody(cs16, "public void WriteTrace(IOutput output,") ?? "";
			Check(ta.Contains("if (m_logger == null) m_logger = LogFileSingleton.GetInstance(null);") && ta.Contains("m_logger.WriteTrace(null, traceLevel, LastCalcBarTime,")
				&& !ta.Contains("TraceLevel") && !ta.Contains("traceOn") && !ta.Contains("Output") && !ta.Contains("Bars.Time")
				&& wt16.Contains("File.AppendAllText(m_logFilePath, line + System.Environment.NewLine, s_utf8NoBom);") && !wt16.Contains("TraceLevel") && !wt16.Contains("level <") && !wt16.Contains("level >")
				&& !rtf.Contains("trace(ETraceLevel") && !rtf.Contains("traceOn("),
				"VB16 the Build Volume On Error is written at TraceLevel None: traceAlways has no level gate, one File.AppendAllText, no Output / Bars.Time");
			// VB17 FutureRolover is futures (typed); M-INSTR F2 moved the helper to _CustomSignal (one copy, shared with the host's roll checks)
			string ifc = MethodBody(cs16, "protected static bool isFuturesCategory(ESymbolCategory category)") ?? "";
			Check(!vb.Contains("bool isFuturesCategory("), "VB17b _VolumeProfileBase uses the inherited _CustomSignal.isFuturesCategory (no second copy)");
			Check(ifc.Contains("return category == ESymbolCategory.Future || category == ESymbolCategory.FutureRolover;") && !ifc.Contains("ToString") && !ifc.Contains("Enum.") && !ifc.Contains("\"") && !ifc.Contains("(int)"),
				"VB17 isFuturesCategory: ESymbolCategory.Future or ESymbolCategory.FutureRolover, typed members only");
			// VB18 Data2..N: typed BarsOfData(n).Info.BVO / Category, stream named in the message, first offender throws
			Check(rtf.Contains("foreach (int stream in volumeFedStreams())") && rtf.Contains("var info = BarsOfData(stream).Info;")
				&& rtf.Contains("error = _OrderFlow.FlowEngine.VolumeBasisError(isFuturesCategory(info.Category), volumeBasisOf(info.BVO), \"Data\" + stream + \" \" + info.Name);")
				&& rtf.Contains("if (error != null) break;") && !rtf.Contains("ToString") && !rtf.Contains("Enum.") && !rtf.Contains("(int)"),
				"VB18 Data2..N feeding a profile / VWAP: typed BarsOfData(n).Info.BVO + Category, message names the stream");
			string d2 = OF.FlowEngine.VolumeBasisError(true, OF.VolumeBasis.TickCount, "Data2 @ES");
			Check(d2 != null && d2.StartsWith("Futures data stream Data2 @ES: Build Volume On must be Trade Volume", StringComparison.Ordinal)
				&& OF.FlowEngine.VolumeBasisError(false, OF.VolumeBasis.TickCount, "Data2 $SPX.X") == null && OF.FlowEngine.VolumeBasisError(true, OF.VolumeBasis.TradeVolume, "Data2 @ES") == null,
				"VB19 Data2 futures + Tick Count refused and named; Data2 non-futures + Tick Count and futures + Trade Volume pass");
			// VB20 which streams feed a profile / VWAP (from the code: VWAP slots, AVWAP, own profile, hooked MC profiles, range / weekly / DVP)
			string vfs = MethodBody(vb, "private SortedSet<int> volumeFedStreams()") ?? "";
			Check(vfs.Contains("if (Vwap1Type != EVwapType.Disabled) fed.Add(Vwap1DataStream);") && vfs.Contains("if (Vwap2Type != EVwapType.Disabled) fed.Add(Vwap2DataStream);")
				&& vfs.Contains("if (Vwap3Type != EVwapType.Disabled) fed.Add(Vwap3DataStream);") && vfs.Contains("if (AvwapEvents != EAvwapAnchor.None) fed.Add(AvwapLimits.dataStream);")
				&& vfs.Contains("if (ownEngineOn()) fed.Add(DevProfileDataStream);") && vfs.Contains("IProfilesCollection pack = getPack(s);") && vfs.Contains("if (pack != null && pack.Visible) fed.Add(s);")
				&& vfs.Contains("foreach (int s in RangeProfiles.streams) fed.Add(s);") && vfs.Contains("if (WeeklyProfiles.enabled) fed.Add(WeeklyProfiles.dataStream);")
				&& vfs.Contains("foreach (int s in DvpConfig.streams) fed.Add(s);") && vfs.Contains("fed.RemoveWhere(s => s < 2 || s > max);"),
				"VB20 volumeFedStreams: VWAP 1..3, AVWAP, own profile, visible MC profiles, RangeProfiles, WeeklyProfiles, DVP; Data2..MaxDataStream only");
			// VB22 the run id is made before the BVO check (issueRunId) and _CustomSignal.StartCalc keeps it: one id per StartCalc
			string csc = MethodBody(cs16, "protected override void StartCalc()") ?? "";
			string iri = MethodBody(cs16, "protected void issueRunId()") ?? "";
			int iKeep = csc.IndexOf("if (!m_runIdIssued) m_runId = Guid.NewGuid().ToString(\"N\").Substring(0, 8);", StringComparison.Ordinal), iClr = csc.IndexOf("m_runIdIssued = false;", StringComparison.Ordinal);
			Check(iKeep >= 0 && iClr > iKeep && iri.Contains("m_runId = Guid.NewGuid().ToString(\"N\").Substring(0, 8);") && iri.Contains("m_runIdIssued = true;")
				&& System.Text.RegularExpressions.Regex.Matches(csc, @"m_runId\s*=").Count == 1 && System.Text.RegularExpressions.Regex.Matches(cs16, @"m_runIdIssued\s*=\s*true").Count == 1,
				"VB22 issueRunId before the BVO check; _CustomSignal.StartCalc keeps that id (one id per StartCalc) and clears the flag");
			// order flow reads Data1 only: VPFB never reads another stream's bars
			Check(!vf.Contains("BarsOfData(") && !vf.Contains("barsOf("), "VB21 order flow (VPFB) reads Data1 only, so Data2..N are checked only for profiles / VWAP");
			int lk = vf.IndexOf("string srcLine = null, volDiagLine = null;"), tr = vf.IndexOf("if (volDiagLine != null) trace(ETraceLevel.Info, volDiagLine);"), sl = tr < 0 ? -1 : vf.IndexOf("if (srcLine != null) engineSink(ETraceLevel.Info)?.Invoke(srcLine);", tr, StringComparison.Ordinal);
			string mid = lk >= 0 && tr > lk ? vf.Substring(lk, tr - lk) : "";
			Check(lk >= 0 && tr > lk && sl > tr && mid.Contains("lock (m_flowLock)") && mid.Contains("volDiagLine = _OrderFlow.McVolDiag.Line(") && mid.Contains("srcLine = m_srcTally.Add(")
				&& !mid.Contains("trace(") && !mid.Contains("Invoke(srcLine)"), "DG5 diag + tally lines built under m_flowLock, logged after it on the calc thread");
			Check(vf.Contains("m_volDiag = null;") && (MethodBody(vf, "private void onFlowData(") ?? "x").IndexOf("m_volDiag") < 0 && (MethodBody(vf, "private void onFlowData(") ?? "trace(").IndexOf("trace(") < 0,
				"DG6 diag counters reset per pass; the DataLoader callback never touches the diag or trace");
		}

		// ---- Tick-chunk spec §7.3: CloseBar backlog ----
		{
			var eb1 = Engine(false);
			eb1.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 1, up: 1));
			eb1.CloseBar(Bar(2, Ms(200), Regime.BarsOnly, vol: 1, up: 1));
			eb1.CloseBar(Bar(3, Ms(300), Regime.BarsOnly, vol: 1, up: 1));
			eb1.AddTrade(Ms(50), 100.00, 2); eb1.AddTrade(Ms(150), 100.25, 3); eb1.AddTrade(Ms(300), 100.50, 4);   // late: bars 1-3
			eb1.AddTrade(Ms(350), 100.75, 5);                                                                      // bar 4
			var f4 = eb1.CloseBar(Bar(4, Ms(400)));
			Check(f4.BacklogTrades == 3 && eb1.BacklogDropped == 3 && Near(f4.Buy + f4.Sell, 5), "BL1 trades of closed BarsOnly bars are backlog, not added to bar 4");
			var eb2 = Engine(false);
			var bi = Bar(1, Ms(200)); bi.OpenTime = Ms(100); bi.TimeBased = true;
			eb2.AddTrade(Ms(50), 100.00, 2); eb2.AddTrade(Ms(150), 100.25, 3);
			var g1 = eb2.CloseBar(bi);
			Check(g1.BacklogTrades == 1 && Near(g1.Buy + g1.Sell, 3), "BL2 first bar: trades at or before OpenTime are backlog");
			var eb3 = Engine(false);
			eb3.AddTrade(Ms(50), 100.00, 2); var g3 = eb3.CloseBar(Bar(1, Ms(100)));
			eb3.AddTrade(Ms(150), 100.25, 3); var g4 = eb3.CloseBar(Bar(2, Ms(200)));
			Check(g3.BacklogTrades == 0 && g4.BacklogTrades == 0 && eb3.BacklogDropped == 0, "BL3 in-order trades are never backlog");
		}

		// ---- Tick-chunk spec §5.1: Globex day plan ----
		{
			var b17 = new TimeSpan(17, 0, 0);
			var pd = OF.PlanGlobexDays(new DateTime(2026, 9, 1), new DateTime(2026, 9, 18), null, b17);
			Check(pd.Count == 14, "TC1 Sep 1-18 2026: 14 weekdays");
			bool contiguous = true;
			for (int i = 1; i < pd.Count; i++)
				if (pd[i].GlobexDate.DayOfWeek != DayOfWeek.Monday && pd[i].FromEt != pd[i - 1].ToEt) contiguous = false;
			var mon = pd.First(d => d.GlobexDate == new DateTime(2026, 9, 14));
			Check(contiguous && mon.FromEt == new DateTime(2026, 9, 13, 17, 0, 0) && mon.ToEt == new DateTime(2026, 9, 14, 17, 0, 0), "TC2 contiguous within a week; Monday starts Sunday 17:00 ET");
			var closed = new List<DateTime>();
			var pl = OF.PlanGlobexDays(new DateTime(2026, 9, 1), new DateTime(2026, 9, 18), d => d == new DateTime(2026, 9, 7), b17, closed);
			Check(pl.Count == 13 && closed.Count == 1 && closed[0] == new DateTime(2026, 9, 7) && pl.All(d => d.GlobexDate.DayOfWeek != DayOfWeek.Saturday && d.GlobexDate.DayOfWeek != DayOfWeek.Sunday), "TC3 closed day and weekends skipped, closed day reported");
		}

		// ---- Tick-chunk spec §5.1: scheduler ----
		{
			var b17 = new TimeSpan(17, 0, 0);
			var days = OF.PlanGlobexDays(new DateTime(2026, 9, 1), new DateTime(2026, 9, 3), null, b17);
			var st = new[] { 0, 1 };
			var s1 = new OF.TickChunkScheduler(days, st, 1, 4, 1000);
			var n1 = s1.Next(DateTime.MinValue, true, null);
			Check(n1.Count == 2 && n1.All(c => c.DayIndex == 0 && c.State == OF.ChunkState.InFlight) && s1.Next(DateTime.MinValue, true, null).Count == 0, "TS1 MaxInFlight 1: one day (both streams) at a time");
			Check(s1.Find(1000) != null && s1.Find(1000).Stream == 0 && s1.NextId == 1006 && s1.Pending() == 6, "TS2 unique ids from the seed, all pending");
			Check(s1.Complete(n1[0], true, 10, null) == "done" && s1.ReleasableInOrder().Count == 1 && !s1.ReadyAt(days[0].FromEt.AddHours(5)), "TS3 one stream done: released, bar not ready");
			s1.Complete(n1[1], true, 7, null);
			Check(s1.ReleasableInOrder().Count == 1 && s1.ReadyAt(days[0].FromEt.AddHours(5)) && s1.ReadyAt(days[0].ToEt) && !s1.ReadyAt(days[1].FromEt.AddHours(1)), "TS4 day ready on both streams; next day not");
			var n2 = s1.Next(DateTime.MinValue, true, null);
			Check(n2.Count == 2 && n2.All(c => c.DayIndex == 1), "TS5 next day issued");
			var c0 = n2.First(c => c.Stream == 0);
			Check(s1.Complete(c0, false, 0, null) == "split4", "TS6 first failure splits into 4");
			var n3 = s1.Next(DateTime.MinValue, true, null);
			Check(n3.Count == 4 && n3.All(c => c.Sub > 0 && c.Stream == 0 && c.Attempt == 2) && n3[0].FromEt == days[1].FromEt && n3[3].ToEt == days[1].ToEt && n3[1].FromEt == n3[0].ToEt, "TS7 sub-windows issued while the day is in flight, contiguous");
			Check(s1.Complete(n3[1], false, 0, null) == "giveUp", "TS8 a failed sub-window gives up");
			foreach (var c in n3) if (c != n3[1]) s1.Complete(c, true, 1, null);
			s1.Complete(n2.First(c => c.Stream == 1), true, 1, null);
			var rel = s1.ReleasableInOrder();
			Check(rel.Count == 4 && !s1.ReadyAt(n3[1].ToEt) && s1.ReadyAt(n3[2].ToEt), "TS9 released in order past the failed sub; its bars not ready, later ones are");
			var s3 = new OF.TickChunkScheduler(days, st, 3, 4, 2000);
			Check(s3.Next(DateTime.MinValue, true, null).Count == 6, "TS10 MaxInFlight 3: all three days");
			var s4 = new OF.TickChunkScheduler(days, st, 1, 4, 3000);
			var sk = new List<OF.TickChunk>();
			var n4 = s4.Next(days[1].ToEt.AddHours(1), true, sk);
			Check(sk.Count == 4 && sk.All(c => c.State == OF.ChunkState.Skipped && c.SkipReason == "late") && n4.Count == 2 && n4.All(c => c.DayIndex == 2 && c.Rt), "TS11 late days skipped, the last (RT) day never");
			var s5 = new OF.TickChunkScheduler(days, st, 1, 4, 4000);
			Check(s5.Next(days[2].ToEt.AddHours(1), false, null).Count == 2 && s5.All().Count(c => c.Rt) == 2 && !s5.All().First().Rt, "TS12 Load mode requests late days; only the last day is RT");
			foreach (var c in n4) s4.Complete(c, true, 1, null);
			s4.ReleasableInOrder();
			Check(s4.ReadyAt(days[2].ToEt.AddHours(30)) && !s4.ReadyAt(days[0].ToEt) && s4.Pending() == 0, "TS13 RT chunk covers bars after its window; skipped days not ready; nothing pending");
		}

		// ---- PR #79 review (Analyst, 2026-11-01) ----
		{
			// B1: process-wide loop guard
			var g = new OF.McRecalcLoopGuard();
			var w = TimeSpan.FromMinutes(10);
			Check(g.TryAcquire("a", T0, 1, w) && !g.TryAcquire("a", T0.AddMinutes(1), 1, w) && g.CountFor("a", T0.AddMinutes(1), w) == 1, "LG1 max 1: the second recalc of a signature within the window is refused");
			Check(g.TryAcquire("b", T0.AddMinutes(1), 1, w), "LG2 keyed by signature: another signature is allowed");
			Check(g.TryAcquire("a", T0.AddMinutes(10).AddSeconds(1), 1, w) && g.CountFor("a", T0.AddMinutes(10).AddSeconds(1), w) == 1, "LG3 allowed again after the window");
			var g2 = new OF.McRecalcLoopGuard();
			Check(g2.TryAcquire("s", T0, 2, w) && g2.TryAcquire("s", T0.AddSeconds(1), 2, w) && !g2.TryAcquire("s", T0.AddSeconds(2), 2, w) && !g2.TryAcquire(null, T0, 2, w) && !g2.TryAcquire("z", T0, 0, w), "LG4 max 2 allows two; null signature or max 0 never");
			for (int i = 0; i < 80; i++) g2.TryAcquire("k" + i, T0, 1, w);
			g2.TryAcquire("late", T0.AddHours(1), 1, w);
			Check(g2.Signatures <= 66, "LG5 expired signatures are pruned");
			Check(OF.McRecalcGate.SkipReason(OF.RecalcDecision.SkipProcessGuard) == "processGuard", "LG6 skip reason processGuard");
			string vp = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileFuturesBase.Strategy.CS"));
			string dec = MethodBody(vp, "private _OrderFlow.RecalcDecision mcRecalcDecision(") ?? "";
			Check(vp.Contains("private static readonly _OrderFlow.McRecalcLoopGuard s_mcRecalcGuard") && vp.Contains("private static readonly object s_mcRecalcGuardLock")
				&& dec.Contains("lock (s_mcRecalcGuardLock)") && dec.Contains("s_mcRecalcGuard.TryAcquire(m_mcGuardKey, DateTime.UtcNow, FlowCfg.McRecalcGuardMaxPerKey, mcGuardWindow())") && dec.Contains("m_mcGate.Decide("), "LG7 strategy: static guard under a static lock, checked with the instance gate in mcRecalcDecision (bar/idle and callback)");

			// B2: stale callbacks
			Check(OF.LegacyStream(1001, 1000, 3) == 1 && OF.LegacyStream(1000, 1000, 3) == 0 && OF.LegacyStream(1002, 1000, 3) == 2, "SC1 this pass's legacy states map to streams 0..2");
			Check(OF.LegacyStream(1003, 1000, 3) == -1 && OF.LegacyStream(1002, 1000, 1) == -1 && OF.LegacyStream(5000, 1000, 3) == -1 && OF.LegacyStream(999, 1000, 3) == -1, "SC2 out of range (chunk ids, earlier passes, unrequested streams) rejected");
			Check(OF.LegacyStream(1000, 0, 3) == -1 && OF.LegacyStream("x", 1000, 3) == -1 && OF.LegacyStream(null, 1000, 3) == -1, "SC3 no legacy request this pass / non-int state rejected");
			object r1 = new object(), r2 = new object();
			Check(OF.IsCurrentRequest(5, 5) && !OF.IsCurrentRequest(4, 5) && !OF.IsCurrentRequest(5, 0) && !OF.IsCurrentRequest(0, 0) && !OF.IsCurrentRequest("5", 5) && !OF.IsCurrentRequest(null, 5),
				"SC4 McBidAsk callback accepted on the current request id alone");
			Check(OF.SameResult(r1, null) && OF.SameResult(r1, r1) && !OF.SameResult(r1, r2), "SC4b SameResult (diagnostic only)");
			string ofd = MethodBody(vp, "private void onFlowData(") ?? "", mcl = MethodBody(vp, "private void onMcLevels(") ?? "", rep = MethodBody(vp, "private void mcStartOrReplay(") ?? "";
			int ls = ofd.IndexOf("LegacyStream("), ft = ofd.IndexOf("flowTick(");
			Check(ls > 0 && ft > ls && ofd.Contains("if (stream < 0) return;"), "SC5 onFlowData: legacy branch rejects stale states before any tick");
			int ic = mcl.IndexOf("IsCurrentRequest("), ms = mcl.IndexOf("mcSet(");
			Check(ic > 0 && ms > ic, "SC6 onMcLevels: stale callbacks dropped before any level set");
			int sr = mcl.IndexOf("if (!_OrderFlow.SameResult(result, m_mcResult))");
			string afterSr = sr < 0 ? "" : string.Join("\n", mcl.Substring(sr).Split('\n').Skip(1).Take(2));
			Check(sr > ic && ms > sr && afterSr.Contains("mcDiag(\"resultMismatch\"") && !afterSr.Contains("return"), "SC8 a different result object with the current id is logged once (resultMismatch) and accepted");
			Check(rep.Contains("m_mcResult = null; m_mcReqId = 0;") && rep.IndexOf("m_mcReqId = 0;") < rep.IndexOf("startMcLevelLoader(tailFrom"), "SC7 cache hit clears the current request before the tail request");

			// S1: empty completed cache is a miss
			var ce = new OF.McLevelCache { Signature = "s", RequestFrom = Ms(0), RequestTo = Ms(100000), HistoryDone = true };
			Check(ce.MissReason("s", Ms(10000), Ms(50000)) == "empty", "RC4 HistoryDone with no level set -> miss (empty)");
			ce.Add(Ms(20000), new[] { 100.0 }, new[] { 1.0 }, new[] { 2.0 });
			Check(ce.MissReason("s", Ms(10000), Ms(50000)) == null, "RC4 the same cache with a level set -> hit");

			// S3: chunk timeout
			var b17 = new TimeSpan(17, 0, 0);
			var days = OF.PlanGlobexDays(new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), null, b17);
			var sch = new OF.TickChunkScheduler(days, new[] { 0, 1 }, 1, 4, 7000);
			var n = sch.Next(DateTime.MinValue, true, null);
			foreach (var c in n) c.IssuedAt = T0;
			var to = TimeSpan.FromSeconds(300);
			Check(sch.TimedOut(T0.AddSeconds(299), to).Count == 0 && sch.TimedOut(T0.AddSeconds(300), to).Count == 2 && sch.TimedOut(T0.AddHours(1), TimeSpan.Zero).Count == 0, "TO1 in-flight chunks time out after TickChunkTimeoutSec; 0 = off");
			n[1].LastActivity = T0.AddSeconds(200);
			var tout = sch.TimedOut(T0.AddSeconds(300), to);
			Check(tout.Count == 1 && tout[0] == n[0], "TO2 a callback (LastActivity) restarts the silence");
			Check(sch.Complete(tout[0], false, 0, null) == "split4" && tout[0].Replaced && sch.TimedOut(T0.AddHours(1), to).All(c => c != n[0]), "TO3 a timed-out chunk is failed and split; it no longer times out");
			var subs = sch.Next(DateTime.MinValue, true, null);
			foreach (var c in subs) c.IssuedAt = T0.AddSeconds(400);
			Check(subs.Count == 4 && subs.All(c => c.Attempt == 2 && c.Sub > 0), "TO4 sub-chunks issued with attempt(2)");
			var subOut = sch.TimedOut(T0.AddSeconds(700), to).Where(c => c.Sub > 0).ToList();
			Check(subOut.Count == 4 && sch.Complete(subOut[0], false, 0, null) == "giveUp", "TO5 a timed-out sub-chunk gives up");
			string pump = MethodBody(vp, "private void pumpTickChunks(") ?? "";
			Check(pump.Contains("m_chunks.TimedOut(") && pump.Contains("req.Subscribe2RT = c.Rt && m_chunkLive;") && !pump.Contains("catch { }"), "TO6 pump: timeouts applied, RT only on a live chart, no empty catch");
			int el = pump.IndexOf("endLoads(toEnd"), lk = pump.IndexOf("lock (m_flowLock)");
			string stop = MethodBody(vp, "private void stopOrderFlowLoads(") ?? "";
			Check(el > lk && !pump.Substring(lk, el - lk).Contains("DataLoader.EndLoadData") && stop.Contains("endLoads(") && !stop.Contains("DataLoader.EndLoadData"), "TO7 EndLoadData called outside m_flowLock (pump, stop)");

			// nit: OpenTime cut only on time-based resolutions
			var en = Engine(false);
			var bn = Bar(1, Ms(200)); bn.OpenTime = Ms(100);   // TimeBased false (tick/volume/range chart)
			en.AddTrade(Ms(50), 100.00, 2); en.AddTrade(Ms(150), 100.25, 3);
			var gn = en.CloseBar(bn);
			Check(gn.BacklogTrades == 0 && Near(gn.Buy + gn.Sell, 5), "BL4 non-time-based bar: OpenTime does not cut");
			var en2 = Engine(false);
			en2.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 1, up: 1));
			en2.AddTrade(Ms(100), 100.00, 2); en2.AddTrade(Ms(150), 100.25, 3);
			var bn2 = Bar(2, Ms(200)); bn2.OpenTime = Ms(120);
			var gn2 = en2.CloseBar(bn2);
			Check(gn2.BacklogTrades == 1 && Near(gn2.Buy + gn2.Sell, 3), "BL5 non-time-based bar: the previous-close cut still applies");
			Check(vp.Contains("TimeBased = timeBasedResolution()") && (MethodBody(vp, "private DateTime barOpenTime(") ?? "").Contains("if (!timeBasedResolution()) return DateTime.MinValue;"), "BL6 strategy sets TimeBased; no OpenTime off time-based charts");
			Check(vp.Contains("protected bool RecalcPending"), "RP1 RecalcPending exposed to derived strategies");

			// ---- PR #79 review 2 (Analyst, c92c65f) ----
			// item 1: guard key = strategy type + signature; a few recalcs per key per window
			Check(OF.GuardKey("Ns.A", "s") == "Ns.A|s" && OF.GuardKey("Ns.A", "s") != OF.GuardKey("Ns.B", "s") && OF.GuardKey("Ns.A", null) == null, "GK1 guard key: strategy type + signature; null without a signature");
			var cfg2 = new OF.FlowConfig();
			Check(cfg2.McRecalcGuardMaxPerKey == 3 && cfg2.McRecalcMax == 1 && cfg2.McRecalcGuardWindowSec == 600, "GK2 defaults: 3 per key per 600 s, instance gate 1");
			var g3 = new OF.McRecalcLoopGuard(); string key = OF.GuardKey("Ns.A", "ES|feed|Minute|5|sig");
			Check(g3.TryAcquire(key, T0, cfg2.McRecalcGuardMaxPerKey, w) && g3.TryAcquire(key, T0.AddSeconds(5), cfg2.McRecalcGuardMaxPerKey, w), "GK3 two identical charts (same key) each get their recalc within the window");
			Check(g3.TryAcquire(key, T0.AddSeconds(10), 3, w) && !g3.TryAcquire(key, T0.AddSeconds(15), 3, w) && g3.TryAcquire(OF.GuardKey("Ns.B", "ES|feed|Minute|5|sig"), T0.AddSeconds(15), 3, w),
				"GK4 a loop on one key stops after 3 recalcs per window; another strategy type is separate");
			Check(rep.Contains("m_mcGuardKey = _OrderFlow.GuardKey(GetType().FullName, sig);") && !vp.Contains("s_mcRecalcGuard.TryAcquire(m_mcSig") && !vp.Contains("s_mcRecalcGuard.CountFor(m_mcSig"), "GK5 strategy keys the static guard by GuardKey (also RecalcPending)");
			// item 3: window 0 does not disable the guard
			string gw = MethodBody(vp, "private TimeSpan mcGuardWindow(") ?? vp.Substring(Math.Max(0, vp.IndexOf("private TimeSpan mcGuardWindow(")));
			string fcs = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_OrderFlow.Function.CS"));
			int fw = fcs.IndexOf("public int McRecalcGuardWindowSec");
			Check(gw.Contains("Math.Max(1, FlowCfg.McRecalcGuardWindowSec)") && fw > 0 && fcs.Substring(Math.Max(0, fw - 500), 500).Contains("does NOT disable the guard"), "WG1 McRecalcGuardWindowSec <= 0 clamps to 1 s, documented on the field");
			var g4 = new OF.McRecalcLoopGuard();
			Check(g4.TryAcquire("k", T0, 1, TimeSpan.FromSeconds(1)) && !g4.TryAcquire("k", T0.AddMilliseconds(500), 1, TimeSpan.FromSeconds(1)) && g4.TryAcquire("k", T0.AddSeconds(1), 1, TimeSpan.FromSeconds(1)), "WG2 1 s window still guards within the second");
			// item 2: chunk timeout clock is UTC
			string ocd = MethodBody(vp, "private void onChunkData(") ?? "";
			Check(pump.Contains("DateTime now = DateTime.UtcNow;") && pump.Contains("c.IssuedAt = DateTime.UtcNow;") && !pump.Contains("DateTime.Now") && ocd.Contains("c.LastActivity = DateTime.UtcNow;") && !ocd.Contains("DateTime.Now"),
				"TO8 chunk IssuedAt/LastActivity/timeout use DateTime.UtcNow");
			// item 4: early return clears m_chunkLoads
			Check(stop.Contains("if (m_flowLoads.Count == 0) { m_chunkLoads = null; return; }"), "ST1 stopOrderFlowLoads early return clears m_chunkLoads");

			// Probe 2026-11-01: StopCalc runs on the UI thread. Nothing on the StopCalc path reads Bars/Output; trace() off the calc
			// thread stamps the bar time cached on the calc thread.
			string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_CustomSignal.Strategy.CS"));
			string tr = MethodBody(cs, "public void trace(ETraceLevel traceLevel, string message)") ?? "";
			bool trGuarded = tr.Length > 0;
			foreach (string ln in tr.Split('\n'))
			{
				string t = ln.Trim();
				if (t.StartsWith("//") || !(t.Contains("Bars.") || t.Contains("Output"))) continue;
				if (!(t.StartsWith("if (onCalc)") || t == "m_logger = LogFileSingleton.GetInstance(onCalc ? Output : null);" || t.StartsWith("try { Output.WriteLine($\"{GetType().Name} {Bars.Info.Name}: trace log "))) trGuarded = false;
			}
			// the path line is the one Bars/Output line not starting with "if (onCalc)": it must sit in the onCalc && !m_tracePathShown block
			string trPath = MethodBody(tr.Substring(Math.Max(0, tr.IndexOf("if (onCalc && !m_tracePathShown)"))), "if (onCalc && !m_tracePathShown)") ?? "";
			int trLog = MethodBody(tr.Substring(Math.Max(0, tr.IndexOf("if (m_logger == null)"))), "if (m_logger == null)")?.IndexOf("Output.WriteLine") ?? 0;
			trGuarded = trGuarded && trPath.Contains("try { Output.WriteLine($\"{GetType().Name} {Bars.Info.Name}: trace log ") && tr.Split(new[] { ": trace log " }, StringSplitOptions.None).Length == 2;
			Check(trGuarded && tr.Contains("bool onCalc = onCalcThread();") && tr.Contains("else m_logger.WriteTrace(null, traceLevel, LastCalcBarTime, m_traceZone ?? \"?\", this.GetType().Name, m_traceSymbol ?? \"\", RunId, message);"),
				"SC1 trace(): every Bars/Output read is behind onCalc; off the calc thread it stamps the cached bar time and passes no Output");
			string note = MethodBody(cs, "protected void noteCalcThread()") ?? "";
			string ct = MethodBody(cs, "private bool onCalcThread()") ?? "";
			string csBar = MethodBody(cs, "protected override void CalcBar()") ?? "";
			Check(note.Contains("m_calcThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;") && note.Contains("System.Threading.Volatile.Write(ref m_lastCalcBarTicks, Bars.Time[0].Ticks);")
				&& ct.Contains("m_calcThreadId != 0 && System.Threading.Thread.CurrentThread.ManagedThreadId == m_calcThreadId") && csBar.TrimStart('{').TrimStart().StartsWith("noteCalcThread();"),
				"SC2 CalcBar caches the calc thread id and bar time first; unknown thread (0) counts as off the calc thread");
			string wt = MethodBody(cs, "public void WriteTrace(IOutput output,") ?? "";
			string ctor = MethodBody(cs, "private LogFileSingleton(IOutput output)") ?? "";
			Check(wt.Contains("if (output != null) try { output.WriteLine(") && !wt.Contains("m_output") && ctor.Contains("if (m_output != null) m_output.WriteLine($\"Log file created")
				&& ctor.Contains("if (m_output != null) m_output.WriteLine($\"Fallback log file"), "SC3 log singleton: null Output (off the calc thread) is never written, not even the singleton's");
			string vpBar = MethodBody(vp, "protected override void CalcBar()") ?? "";
			string ev = MethodBody(vp, "protected override void OnRecalcLastBarAfterEvent()") ?? "";
			Check(vpBar.IndexOf("noteCalcThread();") >= 0 && vpBar.IndexOf("noteCalcThread();") < vpBar.IndexOf("drainFlowQueue();")
				&& ev.IndexOf("noteCalcThread();") >= 0 && ev.IndexOf("noteCalcThread();") < ev.IndexOf("drainFlowQueue();"), "SC4 VP caches the calc thread before draining (CalcBar, OnRecalcLastBarAfterEvent)");
			// #79 nits (typed-enums follow-up): the deferred per-instance path line, a volatile thread id, the bar time as long ticks
			// through Volatile.Write/Read, and the id/ticks reset per pass.
			int pathFlag = trPath.IndexOf("m_tracePathShown = true;"), pathOut = trPath.IndexOf("Output.WriteLine(");
			Check(trLog == -1 && pathFlag > 0 && pathOut > pathFlag, "SC7 'trace log <path>' is printed on the first ON-thread trace (flag set first), not when the logger is created");
			string csStart = MethodBody(cs, "protected override void StartCalc()") ?? "";
			string csStop = MethodBody(cs, "protected override void StopCalc()") ?? "";
			string[] stopStmts = System.Text.RegularExpressions.Regex.Replace(csStop, @"//[^\n]*", "").Trim('{', '}', ' ', '\t', '\r', '\n').Split(';').Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("//")).ToArray();
			Check(cs.Contains("private volatile int m_calcThreadId;") && cs.Contains("private long m_lastCalcBarTicks;") && !cs.Contains("m_lastCalcBarTime")
				&& cs.Contains("protected DateTime LastCalcBarTime { get { return new DateTime(System.Threading.Volatile.Read(ref m_lastCalcBarTicks)); } }"),
				"SC8 m_calcThreadId volatile; cached bar time = long ticks read with Volatile.Read");
			Check(csStart.Contains("m_calcThreadId = 0;") && csStart.Contains("System.Threading.Volatile.Write(ref m_lastCalcBarTicks, 0L);")
				&& stopStmts.Length >= 2 && stopStmts[stopStmts.Length - 2] == "base.StopCalc()" && stopStmts[stopStmts.Length - 1].StartsWith("m_calcThreadId = 0"),
				"SC9 StartCalc resets the thread id and bar ticks; StopCalc resets the id last, after base.StopCalc() (no catch around base.*)");
			// SC5 is a NON-TRANSITIVE static lint: it scans only the text of the method bodies listed in stopPath for banned substrings.
			// It does not follow calls, so every helper reachable from StopCalc must be listed here by hand (a new helper that reads
			// Bars under another name, or a call into an unlisted method, is NOT caught), and a banned word in a string literal fails it.
			string[] stopBanned = { "Bars.", "Bars[", "Output", "trace(", "BarsOfData", "Environment.", "ToEastern", "clock()", "tickSize()", "StatusLine", "noteCalcThread" };
			string[][] stopPath = {
				new[] { "vp", "protected override void StopCalc()" }, new[] { "vp", "private void drainFlowQueue()" }, new[] { "vp", "private string mcStopLine(" },
				new[] { "vp", "private void tickChunkSummary()" }, new[] { "vp", "private void stopOrderFlowLoads(" }, new[] { "vp", "private void endLoads(" },
				new[] { "cs", "protected override void StopCalc()" }, new[] { "cs", "private void stopEngines()" }, new[] { "cs", "private void stopTick()" } };
			foreach (string[] m in stopPath)
			{
				string body = MethodBody(m[0] == "vp" ? vp : cs, m[1]);
				string hit = null;
				if (body != null) foreach (string b in stopBanned) if (body.Contains(b)) { hit = b; break; }
				Check(body != null && hit == null, "SC5 StopCalc path " + m[0] + " " + m[1] + " touches no calc-thread-only API" + (hit == null ? "" : " (" + hit + ")"));
			}
			Check(!vp.Contains("PROBE P-RC") && !fcs.Contains("PROBE P-RC"), "SC6 PROBE P-RC markers resolved");

			// P-RC-EResolution (Ettienne's EResolution dump 2026-11-01; Analyst ruling): exact = Second/Minute/Hour/Day, Size x unit;
			// Week/Month/Quarter/Year calendar-aligned -> non-time fallback (logged once); everything else non-time.
			Check(OF.ExactBarLength(TimeSpan.FromSeconds(1), 15) == TimeSpan.FromSeconds(15) && OF.ExactBarLength(TimeSpan.FromMinutes(1), 5) == TimeSpan.FromMinutes(5)
				&& OF.ExactBarLength(TimeSpan.FromHours(1), 4) == TimeSpan.FromHours(4) && OF.ExactBarLength(TimeSpan.FromDays(1), 1) == TimeSpan.FromDays(1), "ER1 exact bar length = Size x unit (15 s, 5 m, 4 h, 1 d)");
			Check(OF.ExactBarLength(TimeSpan.FromHours(1), 0) == TimeSpan.Zero && OF.ExactBarLength(TimeSpan.Zero, 5) == TimeSpan.Zero && OF.ExactBarLength(TimeSpan.FromDays(1), long.MaxValue) == TimeSpan.Zero,
				"ER2 size 0, unit 0 and overflow give no bound (Zero)");
			string rk = MethodBody(vp, "private _OrderFlow.ResolutionKind resolutionKind(") ?? "";
			Check(rk.Contains("case EResolution.Second: unit = TimeSpan.FromSeconds(1); return _OrderFlow.ResolutionKind.Exact;") && rk.Contains("case EResolution.Minute: unit = TimeSpan.FromMinutes(1); return _OrderFlow.ResolutionKind.Exact;")
				&& rk.Contains("case EResolution.Hour: unit = TimeSpan.FromHours(1); return _OrderFlow.ResolutionKind.Exact;") && rk.Contains("case EResolution.Day: unit = TimeSpan.FromDays(1); return _OrderFlow.ResolutionKind.Exact;")
				&& rk.Contains("r.ChartType != ChartType.Regular) return _OrderFlow.ResolutionKind.NonTime;"), "ER3 Regular Second/Minute/Hour/Day map to Exact with 1 s/1 m/1 h/1 d units");
			int wk = rk.IndexOf("case EResolution.Week:"), cal = rk.IndexOf("return _OrderFlow.ResolutionKind.Calendar;");
			Check(wk > 0 && cal > wk && rk.Substring(wk, cal - wk).Contains("case EResolution.Month:") && rk.Substring(wk, cal - wk).Contains("case EResolution.Quarter:") && rk.Substring(wk, cal - wk).Contains("case EResolution.Year:")
				&& rk.Contains("default: return _OrderFlow.ResolutionKind.NonTime;"), "ER4 Week/Month/Quarter/Year -> Calendar; every other member -> NonTime");
			string tb = MethodBody(vp, "private bool timeBasedResolution()") ?? "";
			string bo = MethodBody(vp, "private DateTime barOpenTime()") ?? "";
			Check(tb.Contains("return k == _OrderFlow.ResolutionKind.Exact;") && tb.Contains("k == _OrderFlow.ResolutionKind.Calendar && !m_calendarResLogged") && tb.Contains("engineSink(ETraceLevel.Info)")
				&& tb.IndexOf("m_calendarResLogged = true;") < tb.IndexOf("engineSink("), "ER5 calendar resolutions fall back to non-time (no OpenTime cut), logged once at Info");
			Check(!rk.Contains("Duration()") && !tb.Contains("Duration()") && !bo.Contains("Duration()") && bo.Contains("OF.ExactBarLength".Replace("OF.", "_OrderFlow.") + "(unit, r.Size)")
				&& !vp.Contains("Enum.Parse") && !rk.Contains("(int)"), "ER6 typed switch: no Duration(), Enum.Parse or numeric casts; first-bar open = close - Size x unit");
			var ec = Engine(false);   // a Week/Month bar: TimeBased false -> OpenTime ignored, previous-close cut only
			ec.CloseBar(Bar(1, Ms(100), Regime.BarsOnly, vol: 1, up: 1));
			ec.AddTrade(Ms(100), 100.00, 2); ec.AddTrade(Ms(150), 100.25, 3);
			var bw = Bar(2, Ms(200)); bw.OpenTime = Ms(180); bw.TimeBased = false;
			var gw2 = ec.CloseBar(bw);
			Check(gw2.BacklogTrades == 1 && Near(gw2.Buy + gw2.Sell, 3), "ER7 calendar (non-time) bar: OpenTime does not cut, previous-close cut does");
			// ---- TE: typed MultiCharts enums and members (Ettienne's rule, 2026-11-01; typed-enums follow-up to #79) ----
			{
				string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../.."));
				var studies = System.IO.Directory.GetFiles(root, "*.CS").Where(f => System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(f), @"\.(Strategy|Function|Indicator|Signal)\.CS$"))
					.OrderBy(f => f, StringComparer.Ordinal).ToList();
				var ownEnums = new HashSet<string>(); var ownTypes = new HashSet<string>();
				var texts = new Dictionary<string, string>();
				foreach (string f in studies)
				{
					string s = System.IO.File.ReadAllText(f); texts[System.IO.Path.GetFileName(f)] = s;
					s = System.Text.RegularExpressions.Regex.Replace(s, @"//[^\n]*", "");   // declarations only, not comments
					foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(s, @"\benum\s+(\w+)")) ownEnums.Add(m.Groups[1].Value);
					foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(s, @"\b(?:class|struct)\s+(\w+)")) ownTypes.Add(m.Groups[1].Value);
				}
				string[] mcEnums = { "EResolution", "ESymbolCategory", "BuildVolumeOn", "ChartType", "DataRequestType", "RequestQuoteField", "EOrderAction", "InputAttribute", "Bar", "Resolution" };
				Check(studies.Count > 50 && ownEnums.Contains("EEventKind") && ownEnums.Contains("EEventTier") && ownEnums.Contains("EEventFlags") && ownEnums.Contains("EPortProfile") && ownEnums.Contains("EPortSizing")
					&& ownEnums.Contains("EEventVolatility") && ownEnums.Contains("EGate0BarUnit") && !mcEnums.Any(mn => ownEnums.Contains(mn) || ownTypes.Contains(mn)),
					"TE0 lint whitelist = enums/types declared in this repo (" + studies.Count + " study files); no MultiCharts enum or type among them");
				// Exemptions, each listed in the PR. None left: M-INSTR PR-M1b removed the three autoTradingDay Category-name exemptions
				// (typed ClassOf switch now) and PR-M1e retired _InstrProbe (the reflection probe; the typed dump that replaced it was removed in e1f5b11).
				var exemptFiles = new HashSet<string>();
				string[] exemptLines = { };
				var used = new HashSet<string>();
				var hits = new List<string>();
				foreach (var kv in texts)
				{
					if (exemptFiles.Contains(kv.Key)) continue;
					string[] lines = kv.Value.Split('\n');
					for (int i = 0; i < lines.Length; i++)
					{
						string code = System.Text.RegularExpressions.Regex.Replace(lines[i], @"//.*$", "").Trim();
						if (code.Length == 0) continue;
						var rules = TypedEnumLint(code, ownEnums, ownTypes);
						if (rules.Count == 0) continue;
						string ek = kv.Key + "|" + code;
						if (exemptLines.Contains(ek)) { used.Add(ek); continue; }
						hits.Add(kv.Key + ":" + (i + 1) + " " + string.Join(",", rules));
					}
				}
				Check(hits.Count == 0, "TE1 no MultiCharts enum/member coded as a string in strategy/function/indicator sources [" + string.Join(" | ", hits) + "]");
				Check(used.Count == exemptLines.Length, "TE2 every lint exemption still matches a source line (no stale exemptions): " + used.Count + "/" + exemptLines.Length);
				// the lint catches the patterns this PR removed (and lets the repo's own enums/types through)
				string[] bad = {
					"&& Bars.Info.Resolution.Type.ToString() != \"Day\" && Bars.Info.Category.ToString() == \"Future\")", "switch (r.Type.ToString())",
					"oneTick = src.Info.Resolution.Type.ToString() == \"Tick\";", "if (a.GetType().Name == \"InputAttribute\") return true;",
					"cache = (System.Reflection.MemberInfo)o.GetType().GetProperty(n) ?? o.GetType().GetField(n);", "var c = (ESymbolCategory)Enum.Parse(typeof(ESymbolCategory), s);",
					"Enum.TryParse<EResolution>(s, out r);", "switch (resolutionType ?? \"\") { case \"Day\": return false; }", "if (c.Contains(\"FOREX\")) return x;",
					"bool f = string.Equals(cat, Bars.Info.Category.ToString());", "if (type == \"Second\" && size <= 0)", "x = b.GetType().InvokeMember(\"Close\", f, null, b, null);", "if (\"Future\".Equals(Bars.Info.Category.ToString())) return;" };
				var missed = bad.Where(b => TypedEnumLint(b, ownEnums, ownTypes).Count == 0).ToList();
				Check(missed.Count == 0, "TE3 lint flags MC-enum ToString compares/switches, member-name strings, Enum.Parse of MC enums and reflection by name [" + string.Join(" | ", missed) + "]");
				string[] good = {
					"economicEvent.volatility = (EEventVolatility)Enum.Parse(typeof(EEventVolatility), line.volatility);",
					"EEventTier t; if (!Enum.TryParse(c[4], false, out t) || !Enum.IsDefined(typeof(EEventTier), t)) return false;",
					"var prop = typeof(Entry).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);",
					"if (Bars.Info.Category == ESymbolCategory.Future && r.Type != EResolution.Day) return;", "sameAsLastCall({ReferenceEquals(o, m_lastRef).ToString()})", "trace(ETraceLevel.Info, $\"resolution({info.Resolution.Type} {info.Resolution.Size})\");" };
				var flagged = good.Where(gs => TypedEnumLint(gs, ownEnums, ownTypes).Count != 0).ToList();
				// TE11: one probe per rule added in the #80 review (rule id must be the one reported)
				var probes = new[] {
					new[] { "if (Bars.Info.Category.ToString().Equals(\"future\", StringComparison.OrdinalIgnoreCase)) return;", "mc-name-string-compare" },
					new[] { "if (cat.Contains(\"Forex\")) return x;", "mc-name-string-compare" },
					new[] { "if (string.Compare(t, \"HEIKINASHI\", true) == 0) ha = true;", "mc-name-string-compare" },
					new[] { "if (s.StartsWith(\"renko\")) brick = true;", "mc-name-string-compare" },
					new[] { "if (s.EndsWith(\"PointAndFigure\")) pf = true;", "mc-name-string-compare" },
					new[] { "if (\"CumDelta\".Equals(ct)) cd = true;", "mc-name-string-compare" },
					new[] { "if (string.Compare(a.Trim(), \"Tick\", true) == 0) t = true;", "mc-name-string-compare" },
					new[] { "if (type == \"OrigPoints\" || type == \"LineBreak\") return;", "mc-member-name-string" },
					new[] { "string n = Enum.GetName(typeof(EResolution), r);", "enum-reflect(GetName EResolution)" },
					new[] { "if (Enum.IsDefined(typeof(ESymbolCategory), \"Forex\")) fx = true;", "enum-reflect(IsDefined ESymbolCategory)" },
					new[] { "var c = (ChartType)Enum.ToObject(typeof(ChartType), 3);", "enum-reflect(ToObject ChartType)" },
					new[] { "var t = Type.GetType(\"PowerLanguage.EResolution, PowerLanguage\");", "type-by-name" },
					new[] { "var t = System.Type.GetType(@\"PowerLanguage.InputAttribute\");", "type-by-name" } };
				var probeMiss = probes.Where(pr => !TypedEnumLint(pr[0], ownEnums, ownTypes).Contains(pr[1])).Select(pr => pr[1] + ": " + pr[0]).ToList();
				Check(probeMiss.Count == 0, "TE11 new rules fire with their own id (case-insensitive name compares, Enum.GetName/IsDefined/ToObject of MC enums, Type.GetType by name) [" + string.Join(" | ", probeMiss) + "]");
				string[] good2 = {
					"string n = Enum.GetName(typeof(EEventTier), t);", "var k = (EEventKind)Enum.ToObject(typeof(EEventKind), 2);",
					"if (s.Equals(\"Daily\", StringComparison.OrdinalIgnoreCase)) d = true;", "if (row.Contains(\"TickRule\")) tr = true;", "Type t = o.GetType();",
					"if (s.Contains(k)) label = \"Tick\";", "if (a.Equals(b.Trim())) kind = \"Day\";" };
				var flagged2 = good2.Where(gs => TypedEnumLint(gs, ownEnums, ownTypes).Count != 0).ToList();
				Check(flagged2.Count == 0, "TE12 new rules pass repo-own enum reflection, non-MC words, o.GetType() and an MC name after the call's closing paren [" + string.Join(" | ", flagged2) + "]");
				Check(ownTypes.Contains("Entry") && flagged.Count == 0, "TE4 lint passes the repo's own enum parses, own-type reflection and typed compares [" + string.Join(" | ", flagged) + "]");

				// the typed replacements themselves
				string tb2 = MethodBody(cs, "private static TimeSpan twoBars(Resolution r)") ?? "";
				Check(tb2.Contains("switch (r.Type)") && tb2.Contains("case EResolution.Second: unit = TimeSpan.FromSeconds(1); break;") && tb2.Contains("case EResolution.Minute: unit = TimeSpan.FromMinutes(1); break;")
					&& tb2.Contains("case EResolution.Hour: unit = TimeSpan.FromHours(1); break;") && tb2.Contains("case EResolution.Day: unit = TimeSpan.FromDays(1); break;")
					&& tb2.Contains("case EResolution.Week: unit = TimeSpan.FromDays(7); break;") && tb2.Contains("default: return TimeSpan.Zero;")
					&& tb2.Contains("return _OrderFlow.ExactBarLength(_OrderFlow.ExactBarLength(unit, r.Size), 2);") && !tb2.Contains("ToString"), "TE5 XMKT twoBars: typed EResolution switch, 2 x Size x unit via ExactBarLength");
				Check(OF.ExactBarLength(OF.ExactBarLength(TimeSpan.FromMinutes(1), 5), 2) == TimeSpan.FromMinutes(10) && OF.ExactBarLength(OF.ExactBarLength(TimeSpan.FromDays(7), 1), 2) == TimeSpan.FromDays(14)
					&& OF.ExactBarLength(OF.ExactBarLength(TimeSpan.FromDays(1), long.MaxValue), 2) == TimeSpan.Zero, "TE6 twoBars arithmetic: 5 m -> 10 m, 1 week -> 14 d, overflow -> Zero (staleness off)");
				string eq2 = MethodBody(cs, "private static void enqueueTick(_TickStream t, Bar b)") ?? "";
				Check(eq2.Contains("t.Enqueue(b.Time, b.Close, (double)b.TotalVolume);") && !cs.Contains("tickBarMember") && !cs.Contains("System.Reflection.MemberInfo"), "TE7 M-TICK enqueueTick reads the typed Bar fields; tickBarMember reflection removed");
				string ip = MethodBody(cs, "private static bool isInputProperty(System.Reflection.PropertyInfo p)") ?? "";
				Check(ip.Contains("return Attribute.IsDefined(p, typeof(InputAttribute), true);") && !ip.Contains(".Name"), "TE8 RUN-line inputs: typed InputAttribute (inherit = true), not a type-name compare");
				Check(cs.Contains("&& Bars.Info.Resolution.Type != EResolution.Day && isFuturesCategory(Bars.Info.Category))")
					&& cs.Contains("if (!m_rollWarned) { m_rollWarned = true; if (isFuturesCategory(Bars.Info.Category)) {") && !cs.Contains("Category.ToString()") && !cs.Contains("Type.ToString()"),
					"TE9 _CustomSignal roll checks use typed isFuturesCategory (Future or FutureRolover, M-INSTR F2) / EResolution.Day; no Category.ToString()");
				string vpb = texts["_VolumeProfileBase.Strategy.CS"];
				Check(vpb.Contains("oneTick = src != null && src.Info.Resolution.Type == EResolution.Tick && src.Info.Resolution.Size == 1;"), "TE10 _VolumeProfileBase own-mode 1-tick test uses EResolution.Tick");
			}
		}


		// ---- M-INSTR PR-M1b (spec rev 2.3 §3.2 F2/F4, §3.3, §4.2): the shipped <instr-host-core> region of _CustomSignal, compiled
		// against a mirror of ESymbolCategory with the §4.1 member values (no test project references PowerLanguage). ----
		{
			var all = (Cat[])Enum.GetValues(typeof(Cat));
			string spec41 = "Future=0,FutureOption=1,Stock=2,StockOption=3,Index=4,CurrOption=5,MutualFund=6,MoneyMarketFund=7,IndexOption=8,Cash=9,Bond=10,Spread=11,Forex=12,CPCSymbol=13,Composites=14,FutureRoot=15,FutureOptionRoot=16,StockOptionRoot=17,FutureRolover=18,Commodity=19,Crypto=20,Warrant=21,None=254";
			Check(all.Length == 23 && string.Join(",", all.Select(c => c + "=" + (int)c)) == spec41, "MI-B0 mirror ESymbolCategory = the 23 members and values of spec §4.1");
			// F2: futures = Future or FutureRolover, nothing else
			var fut = all.Where(IH.isFuturesCategory).ToList();
			Check(fut.Count == 2 && fut.Contains(Cat.Future) && fut.Contains(Cat.FutureRolover), "MI-B1 isFuturesCategory: true for Future and FutureRolover only [" + string.Join(",", fut) + "]");
			// §4.2 ClassOf, every member
			var expect = new Dictionary<Cat, EAssetClass> {
				{ Cat.Future, EAssetClass.Future }, { Cat.FutureRolover, EAssetClass.Future }, { Cat.Stock, EAssetClass.Stock },
				{ Cat.Forex, EAssetClass.SpotFx }, { Cat.Crypto, EAssetClass.Crypto }, { Cat.Index, EAssetClass.Index } };
			var wrong = new List<string>();
			foreach (Cat c in all)
			{
				EAssetClass want = expect.ContainsKey(c) ? expect[c] : EAssetClass.Unsupported, got;
				try { got = IH.ClassOf(c); } catch (Exception ex) { wrong.Add(c + " threw " + ex.GetType().Name); continue; }
				if (got != want) wrong.Add(c + "->" + got);
			}
			Check(wrong.Count == 0, "MI-B2 ClassOf maps all 23 ESymbolCategory members per §4.2 (Future/FutureRolover -> Future, Stock, Forex -> SpotFx, Crypto, Index, rest -> Unsupported) [" + string.Join(" | ", wrong) + "]");
			string unmapped = null;
			try { IH.ClassOf((Cat)99); } catch (InvalidOperationException ex) { unmapped = ex.Message; }
			Check(unmapped == "M-INSTR: unmapped ESymbolCategory 99 (not in spec §4.1); update ClassOf (§4.2) for this MC build.", "MI-B3 ClassOf throws on a value outside §4.1 (a later MC build) [" + unmapped + "]");
			// L2462: the typed Auto trading day equals main's Category-name substring rule for every category (differential)
			Func<DateTime, string, DateTime> toZone = (t, id) => id == "UTC" ? t.AddHours(5) : id == "Eastern Standard Time" ? t.AddHours(-1) : t.AddDays(1000);
			Func<DateTime, string, DateTime> mainAuto = (t, category) => {   // main @ d2b41b7 autoTradingDay(barTime, Category.ToString())
				string c = (category ?? "").ToUpperInvariant();
				if (c.Contains("FOREX") || (c.Contains("CURRENCY") && !c.Contains("CRYPTO"))) return toZone(t, "Eastern Standard Time").AddHours(7).Date;
				if (c.Contains("CRYPTO")) return toZone(t, "UTC").Date;
				return t.Date; };
			int cases = 0; var diff = new List<string>();
			foreach (Cat c in all)
				for (int h = 0; h < 24 * 3; h++)
					for (int m = 0; m < 60; m += 15)
					{
						DateTime t = new DateTime(2026, 3, 6, 0, 0, 0).AddHours(h).AddMinutes(m); cases++;
						DateTime a = mainAuto(t, c.ToString()), b = IH.autoTradingDayOf(IH.ClassOf(c), t, toZone);
						if (a != b && diff.Count < 5) diff.Add(c + " " + t.ToString("s") + " main " + a.ToString("d") + " new " + b.ToString("d"));
					}
			Check(cases == 23 * 72 * 4 && diff.Count == 0, "MI-B4 typed Auto trading day == main's Category-name rule for all 23 categories (" + cases + " bar times) [" + string.Join(" | ", diff) + "]");
			DateTime t18 = new DateTime(2026, 3, 6, 18, 30, 0);
			Check(IH.autoTradingDayOf(EAssetClass.SpotFx, t18, toZone) == new DateTime(2026, 3, 7) && IH.autoTradingDayOf(EAssetClass.Crypto, new DateTime(2026, 3, 6, 20, 0, 0), toZone) == new DateTime(2026, 3, 7)
				&& IH.autoTradingDayOf(EAssetClass.Future, t18, toZone) == new DateTime(2026, 3, 6) && IH.autoTradingDayOf(EAssetClass.Stock, t18, toZone) == new DateTime(2026, 3, 6),
				"MI-B5 Auto: SpotFx rolls at 17:00 NY (+7h), Crypto keys by UTC date, Future/Stock by bar date");
			// F4 + PR-M4 (§11.1 item 4): tick resolution, no 0.01 fallback, no PriceScale == 0 path, no Math.Abs
			Func<double, bool> bad = x => double.IsNaN(x);
			Check(IH.tickFromMinMove(25, 100) == 0.25 && IH.tickFromMinMove(1, 100) == 0.01 && Math.Abs(IH.tickFromMinMove(5, 100000) - 5e-5) < 1e-18
				&& bad(IH.tickFromMinMove(-25, 100)) && bad(IH.tickFromMinMove(25, -100)) && bad(IH.tickFromMinMove(2, 0)),
				"MI-B6 tick = MinMove / PriceScale (ES 25/100 = 0.25, stock 1/100, 6E 5/100000); a negative ratio or PriceScale = 0 is unresolved (PR-M4: no Math.Abs, no MinMove path)");
			Check(bad(IH.tickFromMinMove(0, 100)) && bad(IH.tickFromMinMove(0, 0)) && bad(IH.tickFromMinMove(double.NaN, 100)) && bad(IH.tickFromMinMove(1, double.NaN))
				&& bad(IH.tickFromMinMove(-1, 0)) && bad(IH.tickFromMinMove(double.PositiveInfinity, 1)) && bad(IH.tickFromMinMove(double.PositiveInfinity, 0)) && bad(IH.tickFromMinMove(1e-300, 1e300))
				&& bad(IH.tickFromMinMove(1, double.PositiveInfinity)),
				"MI-B7 a missing, zero, negative, NaN, infinite or underflowing tick is unresolved (NaN), never 0.01");
			var cult = System.Threading.Thread.CurrentThread.CurrentCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
			string te = IH.tickError("@6E", 0.5, 1000), te0 = IH.tickError(null, double.NaN, double.NaN);
			System.Threading.Thread.CurrentThread.CurrentCulture = cult;
			Check(te == "M-INSTR F4: no usable tick size for @6E (Bars.Info.MinMove = 0.5, PriceScale = 1000). There is no 0.01 fallback: set Min Movement / Price Scale for the symbol in QuoteManager."
				&& te0.StartsWith("M-INSTR F4: no usable tick size for Data1 (Bars.Info.MinMove = NaN, PriceScale = NaN)"), "MI-B8 tick error text names the symbol and both values, culture-invariant (de-DE) [" + te + "]");
			// host wiring (MC-bound methods: source checks)
			string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_CustomSignal.Strategy.CS"));
			string code = System.Text.RegularExpressions.Regex.Replace(cs, @"//[^\n]*", "");
			string rtk = MethodBody(cs, "private double resolveTick()") ?? "";
			int iN = rtk.IndexOf("name = Bars.Info.Name;"), iTry = rtk.IndexOf("try"), iNameErr = rtk.IndexOf("string error = tickError(name, mm, ps);");
			int iT = rtk.IndexOf("double t = tickFromMinMove(mm, ps);"), iR = rtk.IndexOf("if (t > 0) return t;"), iE = rtk.IndexOf("traceAlways(ETraceLevel.Error, name, error);"), iX = rtk.IndexOf("throw new InvalidOperationException(error);");
			int iC = rtk.IndexOf("catch (Exception ex)"), iCE = rtk.IndexOf("traceAlways(ETraceLevel.Error, name, readError);"), iCX = rtk.IndexOf("throw new InvalidOperationException(readError, ex);");
			Check(iT > 0 && iR > iT && iE > iR && iX > iE && iC > 0 && iCE > iC && iCX > iCE && iT > iCX && iN > iTry && iN < iC && System.Text.RegularExpressions.Regex.Matches(rtk, @"Bars\.Info\.Name").Count == 1 && iNameErr > iR && !System.Text.RegularExpressions.Regex.IsMatch(rtk, @"catch\s*\{")
				&& !code.Contains("TICK_FALLBACK") && !System.Text.RegularExpressions.Regex.IsMatch(rtk, @"return\s+0\.01")
				&& rtk.IndexOf("InstrumentProfile p1 = InstrumentProfileOf(1);") >= 0 && rtk.IndexOf("if (p1 != null && p1.tick > 0 && !double.IsInfinity(p1.tick)) return p1.tick;") > 0
				&& rtk.IndexOf("if (p1 != null && p1.tick > 0 && !double.IsInfinity(p1.tick)) return p1.tick;") < iTry,
				"MI-B9 resolveTick: the Data1 profile tick first (PR-M4); else an unresolved tick or an unreadable Bars.Info (MinMove, PriceScale or Name, all read inside the try) writes an Error line (traceAlways) and throws; no bare catch, no TICK_FALLBACK");
			{
				string vpbSrc = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VolumeProfileBase.Strategy.CS"));
				string vceSrc = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_VpConfluenceEngine.Function.CS"));
				Func<string, string> noCmt = x => System.Text.RegularExpressions.Regex.Replace(x, @"//[^\n]*", "");
				string bp = MethodBody(cs, "private void buildProfiles()") ?? "", fb = MethodBody(cs, "private void profilesFirstBar()") ?? "";
				Check(!noCmt(vpbSrc).Contains("FALLBACK_TICK") && !noCmt(vceSrc).Contains("FALLBACK_TICK") && !code.Contains("FALLBACK_TICK") && bp.Length > 0 && fb.Length > 0
					&& !code.Contains("LogOnlyLine") && bp.Contains("throw InstrumentError.Fail(err, ") && bp.Contains("InstrumentReason.Class") && bp.Contains("throw instrReadFailed(ds, name, \"sessions at StartCalc\", ex);")
					&& fb.Contains("throw instrReadFailed(ds, null, \"sessions on the first bar\", ex);"),
					"MI-B13 PR-M4: FALLBACK_TICK gone (VPB, VpConfluence); buildProfiles / profilesFirstBar enforce (unmapped category = 'class' Error + throw, read failures Error + throw, no log-only line)");
			}
			Check(code.Contains("&& Bars.Info.Resolution.Type != EResolution.Day && isFuturesCategory(Bars.Info.Category))")
				&& code.Contains("if (!m_rollWarned) { m_rollWarned = true; if (isFuturesCategory(Bars.Info.Category)) {")
				&& System.Text.RegularExpressions.Regex.IsMatch(code, @"m_rollGlobexKey = m_rollDet\.series && calendarKeyed && intraday\s*&& isFuturesCategory\(Bars\.Info\.Category\);")
				&& System.Text.RegularExpressions.Regex.Matches(code, @"==\s*ESymbolCategory\.Future\b").Count == 1,
				"MI-B10 F2: DAILY roll-offset warning, RollDetection-Off warning and the roll Globex key use isFuturesCategory; Category == Future only inside isFuturesCategory");
			string cod1 = MethodBody(cs, "private EAssetClass classOfData1()") ?? "";
			int iCo = cod1.IndexOf("try { return ClassOf(category); }"), iCc = cod1.IndexOf("catch (InvalidOperationException ex)"), iCt = cod1.IndexOf("traceAlways(ETraceLevel.Error, null, ex.Message);"), iCr = cod1.IndexOf("throw;");
			Check(iCo > 0 && iCc > iCo && iCt > iCc && cod1.IndexOf("throw;", iCt) > iCt,
				"MI-B12 an unmapped ESymbolCategory writes an Error line (traceAlways) before ClassOf's exception leaves tradingDayKey");
			int iRt = cod1.IndexOf("try { category = Bars.Info.Category; }"), iRc = cod1.IndexOf("catch (Exception ex)"), iRe = cod1.IndexOf("traceAlways(ETraceLevel.Error, null, readError);"), iRx = cod1.IndexOf("throw new InvalidOperationException(readError, ex);");
			Check(iRt > 0 && iRc > iRt && iRe > iRc && iRx > iRe && iCo > iRx && System.Text.RegularExpressions.Regex.Matches(cod1, @"[=(,]\s*Bars\.Info\.Category\b").Count == 1,
				"MI-B12b classOfData1 reads Bars.Info.Category only inside a try: a failing MC read writes an Error line, then throws (inner = the MC exception)");
			Check(code.Contains("if (TradingDayRoll == ETradingDayRoll.Auto) return autoTradingDay(barTime, classOfData1());")
				&& code.Contains("return autoTradingDayOf(assetClass, barTime, ToZone);") && !code.Contains("Category.ToString()") && !code.Contains("\"FOREX\"") && !code.Contains("\"CRYPTO\""),
				"MI-B11 L2462: tradingDayKey Auto passes the typed ClassOf(Bars.Info.Category) (via classOfData1); no Category.ToString() or category-name strings left");
		}


		// ---- M-INSTR PR-M1c (spec rev 2.3 §3.2 F3, rows 60/70): _ExitHalfRiskHalfRoll's <legacy-exit-core>, compiled against a mirror of ESpecOrdersMode ----
		{
			// F3a: one tick = MinMove / PriceScale (Info.PriceScale convention of §4.4.3: ES 100, 6E 99999.999999999985)
			Check(LX.oneTick(25, 100) == 0.25 && LX.oneTick(1, 100) == 0.01 && Math.Abs(LX.oneTick(5, 99999.999999999985) - 5e-5) < 1e-15 && LX.oneTick(1, 10) == 0.1,
				"MI-C1 F3a one tick = MinMove / PriceScale (ES 0.25, CL 0.01, 6E 0.00005, GC 0.1)");
			Check(LX.oneTick(25, 100) != 1 / 100.0 * 50, "MI-C2 F3a ES tick is no longer 1 / PriceScale * BigPointValue (0.5)");
			// F3b: GenerateStopLoss amount in currency; PerContract per contract, PerPosition for the whole position
			Check(LX.stopLossAmount(4.25, 50, LXMode.PerContract, 3) == 212.5 && LX.stopLossAmount(4.25, 50, LXMode.PerPosition, 3) == 637.5
				&& LX.stopLossAmount(4.25, 50, LXMode.PerPosition, -3) == 637.5 && LX.stopLossAmount(0.0125, 125000, LXMode.PerContract, 2) == 1562.5,
				"MI-C3 F3b stop amount = distance x BigPointValue per contract (ES 4.25 pt = $212.50, 6E 0.0125 = $1562.50), x open contracts in PerPosition");
			// MC's stop price from the amount (PG 4.6.2: EntryPrice - amount / contracts / BigPointValue in PerPosition) lands at the intended distance
			double entry = 4800, low1 = 4795.75, tick = LX.oneTick(25, 100), dist = entry - low1 - tick;
			double stopPc = entry - LX.stopLossAmount(dist, 50, LXMode.PerContract, 4) / 50, stopPp = entry - LX.stopLossAmount(dist, 50, LXMode.PerPosition, 4) / 4 / 50;
			Check(Math.Abs(stopPc - (low1 + tick)) < 1e-9 && Math.Abs(stopPp - (low1 + tick)) < 1e-9 && Math.Abs(dist - 4) < 1e-9,
				"MI-C4 F3b round trip: MC's stop price from the amount = the price the study meant (entry - distance), in both modes [" + stopPc + ", " + stopPp + "]");
			string lx = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../../_ExitHalfRiskHalfRoll.Strategy.CS"));
			string lxc = System.Text.RegularExpressions.Regex.Replace(lx, @"//[^\n]*", "");
			// fix/exit-half-risk-legacy: the first-stop offsets moved into firstStopDistance (one tick outside the prior bar, long and short)
			Check(!lxc.Contains("1 / m_entryResolution.Info.PriceScale") && lxc.Contains("tick = checkedTick(m_entryResolution.Info.MinMove, m_entryResolution.Info.PriceScale, m_entryResolution.Info.Name);")
				&& lxc.Contains("return isLong ? entryPrice - (priorLow - tick) : (priorHigh + tick) - entryPrice;")
				&& lxc.Contains("firstStopDistance(entryPrice, m_entryResolution.Low[1], m_entryResolution.High[1], tick, isLong), tick);")
				&& lxc.Contains("return isLong ? low - tick : high + tick;") && lxc.Contains("m_tick = tick;")
				&& lxc.Contains("rollStopLevel(m_entryResolution.Low[0], m_entryResolution.High[0], tick, isLong);")
				&& lxc.Contains("rollStopLevel(m_longerResolution.Low[1], m_longerResolution.High[1], m_tick, isLong);")
				&& LX.rollStopLevel(4790, 4810, 0.25, true) == 4789.75 && LX.rollStopLevel(4790, 4810, 0.25, false) == 4810.25,
				"MI-C5 F3a wiring: the one-tick offsets (first stop via firstStopDistance, roll level entry bar / previous day via rollStopLevel) use oneTick(MinMove, PriceScale)");
			Check(lxc.Contains("GenerateStopLoss(stopLossAmount(m_firstStopDistance, m_bigPointValue, CurSpecOrdersMode, this.CurrentPosition.OpenLots));")
				&& lxc.Contains("bigPointValue = checkedBigPointValue(m_entryResolution.Info.BigPointValue, m_entryResolution.Info.Name);")
				&& lxc.Contains("m_bigPointValue = bigPointValue;") && lxc.Contains("m_firstStopDistance = atLeastOneTick(firstStopDistance(")
				&& lxc.IndexOf("checkedBigPointValue(m_entryResolution") < lxc.IndexOf("m_bigPointValue = bigPointValue;")
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"GenerateStopLoss\(").Count == 1,
				"MI-C6 F3b wiring: GenerateStopLoss gets the currency amount (guarded BigPointValue, distance clamped to one tick) for the strategy's CurSpecOrdersMode");
			// #92 review: guards (message-only throws) and the negative-distance clamp
			Check(lxThrows(() => LX.checkedTick(0, 100, "ES")) && lxThrows(() => LX.checkedTick(-25, 100, "ES")) && lxThrows(() => LX.checkedTick(25, 0, "ES"))
				&& lxThrows(() => LX.checkedTick(double.NaN, 100, "ES")) && lxThrows(() => LX.checkedTick(25, double.PositiveInfinity, "ES")) && lxThrows(() => LX.checkedTick(double.PositiveInfinity, 100, "ES"))
				&& lxMessage(() => LX.checkedTick(0, 100, "ESZ26")).Contains("ESZ26") && lxMessage(() => LX.checkedTick(0, 100, "ESZ26")).Contains("MinMove 0")
				&& lxMessage(() => LX.checkedTick(0, 100, "ESZ26")).Contains("PriceScale 100") && lxMessage(() => LX.checkedTick(0, 100, "ESZ26")).Contains("fix in QuoteManager")
				&& LX.checkedTick(25, 100, "ES") == 0.25,
				"MI-C7 F3a tick guard: zero/negative/NaN/infinite tick throws InvalidOperationException naming symbol, MinMove, PriceScale, 'fix in QuoteManager'; ES passes 0.25");
			Check(lxThrows(() => LX.checkedBigPointValue(0, "ES")) && lxThrows(() => LX.checkedBigPointValue(-50, "ES")) && lxThrows(() => LX.checkedBigPointValue(double.NaN, "ES"))
				&& lxThrows(() => LX.checkedBigPointValue(double.PositiveInfinity, "ES")) && lxMessage(() => LX.checkedBigPointValue(0, "ESZ26")).Contains("ESZ26")
				&& lxMessage(() => LX.checkedBigPointValue(0, "ESZ26")).Contains("fix in QuoteManager") && LX.checkedBigPointValue(50, "ES") == 50,
				"MI-C8 F3b BigPointValue guard: <= 0 / NaN / infinite throws InvalidOperationException naming symbol, 'fix in QuoteManager'; ES 50 passes");
			Check(LX.atLeastOneTick(-1.5, 0.25) == 0.25 && LX.atLeastOneTick(0, 0.25) == 0.25 && LX.atLeastOneTick(0.1, 0.25) == 0.25 && LX.atLeastOneTick(double.NaN, 0.25) == 0.25
				&& LX.atLeastOneTick(4, 0.25) == 4 && LX.stopLossAmount(LX.atLeastOneTick(-1.5, 0.25), 50, LXMode.PerPosition, 2) == 25,
				"MI-C9 F3b negative/sub-tick/NaN first-stop distance clamps to one tick (ES $12.50/contract), never a negative GenerateStopLoss amount");

			// ---- fix/exit-half-risk-legacy: (1) stop orders get a PRICE, not a distance; (2) long/short symmetry; (3) orders re-sent every bar ----
			// (1) price vs distance. ES long: entry 4800, prior low 4795.75 -> distance 4.5, stop price 4795.50 (Low[1] - tick), not 4.5
			double fxTick = LX.oneTick(25, 100), fxEntry = 4800, fxLow1 = 4795.75, fxHigh1 = 4804.25;
			double fxDl = LX.atLeastOneTick(LX.firstStopDistance(fxEntry, fxLow1, fxHigh1, fxTick, true), fxTick), fxPl = LX.firstStopPrice(fxEntry, fxDl, true);
			double fxDs = LX.atLeastOneTick(LX.firstStopDistance(fxEntry, fxLow1, fxHigh1, fxTick, false), fxTick), fxPs = LX.firstStopPrice(fxEntry, fxDs, false);
			Check(fxDl == 4.5 && fxPl == 4795.5 && fxDs == 4.5 && fxPs == 4804.5 && fxPl != fxDl && fxPs != fxDs
				&& Math.Abs((fxEntry - LX.stopLossAmount(fxDl, 50, LXMode.PerContract, 2) / 50) - fxPl) < 1e-9
				&& Math.Abs((fxEntry + LX.stopLossAmount(fxDs, 50, LXMode.PerPosition, -2) / 2 / 50) - fxPs) < 1e-9,
				"MI-C10 fix 1 price vs distance: ES long stop price 4795.50 = entry - 4.5, short 4804.50 = entry + 4.5 (not the 4.5 distance); GenerateStopLoss amount lands on the same price [" + fxPl + ", " + fxPs + "]");
			string lxCalc = lxc.Substring(lxc.IndexOf("protected override void CalcBar()"), lxc.IndexOf("private void marketPositionChanged(") - lxc.IndexOf("protected override void CalcBar()"));
			string lxMpc = lxc.Substring(lxc.IndexOf("private void marketPositionChanged("), lxc.IndexOf("private void defaultProcessing()") - lxc.IndexOf("private void marketPositionChanged("));
			Check(lxCalc.Contains("m_lxTargetOrder.Send(m_targetPrice, ") && lxCalc.Contains("m_sxTargetOrder.Send(m_targetPrice, ")
				&& lxMpc.Contains("m_firstStopPrice = firstStopPrice(entryPrice, m_firstStopDistance, isLong);")
				&& lxMpc.Contains("m_targetPrice = targetPrice(entryPrice, m_firstStopDistance, isLong);")
				&& !lxc.Contains("m_firstStopPrice = this.EntryPrice()") && !lxc.Contains("atLeastOneTick(m_firstStopPrice")
				&& !System.Text.RegularExpressions.Regex.IsMatch(lxc, @"Send\(m_firstStopDistance") && !System.Text.RegularExpressions.Regex.IsMatch(lxc, @"stopLossAmount\(m_firstStopPrice"),
				"MI-C10b fix 1 wiring: the +1R limits get m_targetPrice (a price from entry and R), the -R stop is m_firstStopPrice = firstStopPrice(entry, R); only m_firstStopDistance feeds stopLossAmount");
			// (2) symmetry: mirrored bars give equal distances; long stop is one tick BELOW Low[1], short one tick ABOVE High[1]
			bool fxSym = true;
			foreach (double[] c in new[] { new[] { 4800, 4795.75, 4804.25 }, new[] { 4801.5, 4790, 4813 }, new[] { 4800.25, 4800, 4800.5 } })
			{
				double e = c[0], lo = c[1], hi = 2 * e - lo;   // hi mirrors lo about the entry
				double dl = LX.firstStopDistance(e, lo, hi, fxTick, true), ds = LX.firstStopDistance(e, lo, hi, fxTick, false);
				fxSym &= Math.Abs(dl - ds) < 1e-9 && Math.Abs(LX.firstStopPrice(e, dl, true) - (lo - fxTick)) < 1e-9 && Math.Abs(LX.firstStopPrice(e, ds, false) - (hi + fxTick)) < 1e-9
					&& LX.firstStopPrice(e, dl, true) < lo && LX.firstStopPrice(e, ds, false) > hi;
			}
			Check(fxSym, "MI-C11 fix 2 long/short symmetry: long first stop = Low[1] - tick (outside the low), short = High[1] + tick, equal distances on mirrored bars");
			// entry already beyond the level (gap through it): distance clamps to one tick, stop price still on the protective side of the entry
			double gDl = LX.atLeastOneTick(LX.firstStopDistance(4790, 4795.75, 4804.25, fxTick, true), fxTick), gDs = LX.atLeastOneTick(LX.firstStopDistance(4810, 4795.75, 4804.25, fxTick, false), fxTick);
			Check(gDl == 0.25 && LX.firstStopPrice(4790, gDl, true) == 4789.75 && gDs == 0.25 && LX.firstStopPrice(4810, gDs, false) == 4810.25,
				"MI-C11b fix 2 clamp symmetry: entry beyond Low[1] - tick / High[1] + tick clamps to one tick; long stop entry - tick, short entry + tick");
			// (3) orders re-sent every bar: drive exitOrdersThisBar bar by bar as CalcBar does (Default bars and the position-change bar alike)
			LX.State[] fxStates = { LX.State.FirstExit, LX.State.FirstExit, LX.State.FirstExit, LX.State.SecondExit, LX.State.SecondExit, LX.State.SecondExit, LX.State.None };
			int[] fxSides = { 1, 1, 1, 1, 1, 1, 0 };
			int fxFirstBars = 0, fxSecondBars = 0, fxFlatSends = 0;
			for (int b = 0; b < fxStates.Length; b++)
			{
				LX.State o = LX.exitOrdersThisBar(fxStates[b], 1, fxSides[b], fxSides[b] == 0 ? 0 : 2);
				if (o == LX.State.FirstExit) fxFirstBars++; else if (o == LX.State.SecondExit) fxSecondBars++; else if (fxSides[b] == 0 && o != LX.State.None) fxFlatSends++;
			}
			Check(fxFirstBars == 3 && fxSecondBars == 3 && fxFlatSends == 0 && LX.exitOrdersThisBar(LX.State.FirstExit, -1, -1, 2) == LX.State.FirstExit
				&& LX.exitOrdersThisBar(LX.State.FirstExit, 1, 0, 0) == LX.State.None && LX.exitOrdersThisBar(LX.State.SecondExit, 1, 0, 0) == LX.State.None,
				"MI-C12 fix 3 re-send: exit orders are planned on every bar in a position (3 FirstExit + 3 SecondExit of 6), long or short; none when flat (stale state)");
			int fxSw = lxCalc.IndexOf("switch (exitOrdersThisBar(m_state, m_armedSide, PositionSide, this.CurrentPosition.OpenLots))"), fxRsEnd = lxCalc.IndexOf("case CalculationReason.Default:");
			int fxFe = lxCalc.IndexOf("case State.FirstExit:"), fxSe = lxCalc.IndexOf("case State.SecondExit:"), fxGsl = lxCalc.IndexOf("GenerateStopLoss(");
			Check(fxSw > fxRsEnd && fxRsEnd > 0 && fxFe > fxSw && fxGsl > fxFe && fxGsl < fxSe
				&& !lxMpc.Contains("GenerateStopLoss(") && !lxMpc.Contains(".Send(") && !lxc.Contains("if (m_state == State.FirstExit) GenerateStopLoss"),
				"MI-C12b fix 3 wiring: CalcBar sends the stops and GenerateStopLoss (FirstExit case) after the CalcReason switch on every bar; marketPositionChanged sends nothing");

			// ---- #94 review: (4) order quantities, (5) one-lot positions, (6) backtest/playback position-change fallback ----
			// (4) Contracts.UserSpecified so Send's numLots is the order size; SecondExit passes the open lots explicitly
			Check(lxc.Contains("m_leStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, \"LXTrail\", EOrderAction.Sell, OrderExit.Total));")
				&& lxc.Contains("m_seStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, \"SXTrail\", EOrderAction.BuyToCover, OrderExit.Total));") && !lxc.Contains("Contracts.Default")
				&& lxc.Contains("m_lxTargetOrder = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, \"LXHalf\", EOrderAction.Sell, OrderExit.Total));")
				&& lxc.Contains("m_sxTargetOrder = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, \"SXHalf\", EOrderAction.BuyToCover, OrderExit.Total));")
				&& lxCalc.Contains("m_leStopOrder.Send(m_trailStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));")
				&& lxCalc.Contains("m_seStopOrder.Send(m_trailStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));")
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"\.Send\([^,()]*\)").Count == 0
				&& LX.remainingExitLots(3) == 3 && LX.remainingExitLots(-3) == 3 && LX.remainingExitLots(1) == 1,
				"MI-C13 quantities: target limits and stop orders are Contracts.UserSpecified + OrderExit.Total and every Send passes its lots (SecondExit = all open lots)");
			// (5) one lot: half exit is at least one lot (Math.Round(0.5) = 0 would send a zero-lot order)
			bool fxHalfOk = true;
			for (int n = 1; n <= 50; n++) { int h = LX.halfExitLots(n); fxHalfOk &= h >= 1 && h <= n && LX.halfExitLots(-n) == h; }
			Check(fxHalfOk && LX.halfExitLots(1) == 1 && LX.halfExitLots(2) == 1 && LX.halfExitLots(3) == 2 && LX.halfExitLots(4) == 2 && LX.halfExitLots(10) == 5
				&& lxCalc.Contains("m_lxTargetOrder.Send(m_targetPrice, halfExitLots(this.CurrentPosition.OpenLots));")
				&& lxCalc.Contains("m_sxTargetOrder.Send(m_targetPrice, halfExitLots(this.CurrentPosition.OpenLots));") && !lxc.Contains("Math.Round((double)this.CurrentPosition.OpenLots"),
				"MI-C14 one lot: halfExitLots is 1..openLots for 1..50 lots (1 lot -> 1, whole position), long or short; the FirstExit +1R limit sends it");
			// (6) fallback: drive positionTransitionDue as checkPositionTransition does (record the new position after a transition) and the
			// study's stage machine stageAfterPositionChange (flat -> None; opened/reversed by another signal -> re-armed FirstExit; FirstExit and fewer lots -> SecondExit)
			Func<bool[], int[], string> fxRun = (evt, pos) => {
				int prev = 0, fired = 0; LX.State st = LX.State.None; var trace = new System.Text.StringBuilder();
				for (int i = 0; i < pos.Length; i++)
				{
					if (LX.positionTransitionDue(prev, pos[i]))
					{
						st = LX.stageAfterPositionChange(st, prev, pos[i]);
						prev = pos[i]; fired++;
					}
					LX.State o = LX.exitOrdersThisBar(st, st == LX.State.None ? 0 : Math.Sign(prev), Math.Sign(pos[i]), Math.Abs(pos[i]));
					trace.Append(evt[i] ? "E" : "D").Append(o == LX.State.None ? "0" : o == LX.State.FirstExit ? "1" : "2");
				}
				return fired + ":" + trace;
			};
			// backtest/playback: Default calcs only, 0 -> +2 -> +1 -> 0
			string fxBt = fxRun(new[] { false, false, false, false, false, false, false }, new[] { 0, 2, 2, 1, 1, 0, 0 });
			// real time: each change is seen by the event AND by the next Default calc; and once by a Default calc before the event
			string fxRt = fxRun(new[] { false, true, false, false, true, false, true, false }, new[] { 0, 2, 2, 2, 1, 1, 0, 0 });
			string fxDe = fxRun(new[] { false, false, true, false }, new[] { 0, -2, -2, -2 });
			Check(fxBt == "3:D0D1D1D2D2D0D0" && fxRt == "3:D0E1D1D1E2D2E0D0" && fxDe == "1:D0D1E1D1",
				"MI-C15 fallback: Default-only calcs (backtest/playback) advance None -> FirstExit -> SecondExit -> None; event + Default on one change fires once [" + fxBt + " | " + fxRt + " | " + fxDe + "]");
			int fxCpt = lxc.IndexOf("private void checkPositionTransition()");
			string lxCpt = fxCpt < 0 ? "" : lxc.Substring(fxCpt, lxc.IndexOf("private void marketPositionChanged(") - fxCpt);
			Check(System.Text.RegularExpressions.Regex.Matches(lxCalc, @"checkPositionTransition\(\);").Count == 2
				&& lxCalc.IndexOf("case CalculationReason.MarketPositionChange:") < lxCalc.IndexOf("checkPositionTransition();")
				&& lxCalc.IndexOf("case CalculationReason.Default:") < lxCalc.LastIndexOf("checkPositionTransition();")
				&& lxCalc.LastIndexOf("checkPositionTransition();") < lxCalc.IndexOf("defaultProcessing();")
				&& lxCpt.Contains("int marketPosition = StrategyInfo.MarketPosition;") && lxCpt.Contains("if (!positionTransitionDue(m_previousMarketPosition, marketPosition)) return;")
				&& lxCpt.IndexOf("m_previousMarketPosition = marketPosition;") < lxCpt.IndexOf("marketPositionChanged(previousMarketPosition, marketPosition);")
				&& lxCpt.IndexOf("int previousMarketPosition = m_previousMarketPosition;") < lxCpt.IndexOf("m_previousMarketPosition = marketPosition;")
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"marketPositionChanged\(previousMarketPosition, marketPosition\);").Count == 1,
				"MI-C15b fallback wiring: both CalcReason paths call checkPositionTransition (Default before defaultProcessing); it compares StrategyInfo.MarketPosition, records it, then runs marketPositionChanged (its only caller)");

			// ---- #94 redesign: half off at +1R (limit), full GenerateStopLoss at -R, then the rest on a trailing roll stop that never loosens ----
			// error log before throw on the tick / BigPointValue guards
			int fxCatch = lxMpc.IndexOf("catch (InvalidOperationException ex)"), fxLog = lxMpc.IndexOf("Output.WriteLine(\"{0}\", \"Error: \" + ex.Message);"), fxRethrow = lxMpc.IndexOf("throw;");
			Check(fxCatch > 0 && fxLog > fxCatch && fxRethrow > fxLog && lxMpc.IndexOf("checkedTick(") < fxCatch && lxMpc.IndexOf("checkedBigPointValue(") < fxCatch,
				"MI-C16 guards: a tick / BigPointValue guard failure writes an Error line to Output, then rethrows the InvalidOperationException");
			// +1R limit price in both directions: ES entry 4800, Low[1] 4795.75 / High[1] 4804.25 -> R 4.5; long target 4804.50, short 4795.50
			double rT = LX.atLeastOneTick(LX.firstStopDistance(4800, 4795.75, 4804.25, 0.25, true), 0.25), rS = LX.atLeastOneTick(LX.firstStopDistance(4800, 4795.75, 4804.25, 0.25, false), 0.25);
			double tL = LX.targetPrice(4800, rT, true), tS = LX.targetPrice(4800, rS, false), sL = LX.firstStopPrice(4800, rT, true), sS = LX.firstStopPrice(4800, rS, false);
			double gR = LX.atLeastOneTick(LX.firstStopDistance(4790, 4795.75, 4804.25, 0.25, true), 0.25);
			Check(tL == 4804.5 && tS == 4795.5 && tL - 4800 == 4800 - sL && sS - 4800 == 4800 - tS && LX.targetPrice(4790, gR, true) == 4790.25 && LX.targetPrice(4810, 0.25, false) == 4809.75
				&& LX.halfExitLots(4) == 2 && LX.halfExitLots(-4) == 2 && LX.halfExitLots(6) == 3 && LX.halfExitLots(-7) == 4,
				"MI-C17 +1R limit: long entry + R (4804.50), short entry - R (4795.50), the mirror of the -R stop; gap clamp -> entry +/- one tick; half the lots long or short [" + tL + ", " + tS + "]");
			// 1 lot: the +1R limit is the whole position, so the trade goes FirstExit -> flat (None), never SecondExit
			LX.State oneLot = LX.stageAfterPositionChange(LX.State.None, 0, 1);
			LX.State oneLotOut = LX.stageAfterPositionChange(oneLot, 1, 0);
			LX.State oneLotS = LX.stageAfterPositionChange(LX.State.None, 0, -1);
			Check(LX.halfExitLots(1) == 1 && LX.halfExitLots(-1) == 1 && LX.halfExitLots(1) == LX.remainingExitLots(1) && oneLot == LX.State.FirstExit && oneLotOut == LX.State.None
				&& oneLotS == LX.State.FirstExit && LX.stageAfterPositionChange(oneLotS, -1, 0) == LX.State.None,
				"MI-C18 one lot: the +1R limit sends 1 lot (the whole position), long or short; filling it goes FirstExit -> None, not SecondExit");
			// the trailing stop never loosens: starts at the -R stop, follows the roll level up (long) / down (short) only; NaN keeps it
			double[] lvL = { 4790, 4797, 4796, 4801, double.NaN, 4800, 4803.25 }, expL = { 4795.5, 4797, 4797, 4801, 4801, 4801, 4803.25 };
			double[] lvS = { 4810, 4803, 4804, 4799, double.NaN, 4800, 4796.75 }, expS = { 4804.5, 4803, 4803, 4799, 4799, 4799, 4796.75 };
			double trL = 4795.5, trS = 4804.5; bool trOk = true;
			for (int i = 0; i < lvL.Length; i++)
			{
				double nL = LX.ratchetStop(trL, lvL[i], true), nS = LX.ratchetStop(trS, lvS[i], false);
				trOk &= nL >= trL && nS <= trS && nL == expL[i] && nS == expS[i];
				trL = nL; trS = nS;
			}
			int fxTr = lxMpc.IndexOf("m_trailStopPrice = m_firstStopPrice;");
			Check(trOk && fxTr > 0 && fxTr < lxMpc.IndexOf("updateSecondExit();") && lxc.Contains("m_trailStopPrice = ratchetStop(m_trailStopPrice, m_secondStopPrice, isLong);")
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"m_trailStopPrice = ").Count == 4,
				"MI-C19 trailing stop never loosens: starts at the -R stop, long only moves up, short only moves down, NaN level ignored; the only writes are the -R start, the ratchet and the resets (re-arm, flat)");
			// the state moves to SecondExit on a (partial) fill of the half; add-ons and further reductions keep the stage; reversal by another signal = re-arm
			Check(LX.stageAfterPositionChange(LX.State.FirstExit, 4, 2) == LX.State.SecondExit && LX.stageAfterPositionChange(LX.State.FirstExit, 4, 3) == LX.State.SecondExit
				&& LX.stageAfterPositionChange(LX.State.FirstExit, -4, -3) == LX.State.SecondExit && LX.stageAfterPositionChange(LX.State.FirstExit, 4, 6) == LX.State.FirstExit
				&& LX.stageAfterPositionChange(LX.State.SecondExit, 3, 1) == LX.State.SecondExit && LX.stageAfterPositionChange(LX.State.SecondExit, 3, 5) == LX.State.SecondExit
				&& LX.stageAfterPositionChange(LX.State.SecondExit, 3, 0) == LX.State.None && LX.stageAfterPositionChange(LX.State.FirstExit, 4, 0) == LX.State.None
				&& LX.stageAfterPositionChange(LX.State.FirstExit, 2, -2) == LX.State.FirstExit && LX.exitManagementRearmDue(LX.State.FirstExit, 2, -2)
				&& LX.stageAfterPositionChange(LX.State.SecondExit, -1, 1) == LX.State.FirstExit && LX.exitManagementRearmDue(LX.State.None, 0, 3) && !LX.exitManagementRearmDue(LX.State.FirstExit, 4, 2)
				&& !LX.exitManagementRearmDue(LX.State.FirstExit, 4, 6) && !LX.exitManagementRearmDue(LX.State.None, 2, 0)
				&& LX.stageAfterPositionChange(LX.State.FirstExit, int.MinValue, -5) == LX.State.SecondExit && LX.remainingExitLots(int.MinValue) == int.MaxValue && LX.halfExitLots(int.MinValue) > 0,
				"MI-C20 stages: FirstExit -> SecondExit when the same-side position gets smaller (partial fill too); add-on / further exits keep the stage; flat -> None; reversal by another signal -> re-armed FirstExit; int.MinValue safe");
			// one protective stop per stage: FirstExit = GenerateStopLoss only (plus the +1R limit), SecondExit = the trailing stop order only
			int fxSw2 = lxCalc.IndexOf("switch (exitOrdersThisBar(m_state, m_armedSide, PositionSide, this.CurrentPosition.OpenLots))");
			string fxFeSeg = lxCalc.Substring(lxCalc.IndexOf("case State.FirstExit:", fxSw2), lxCalc.IndexOf("case State.SecondExit:", fxSw2) - lxCalc.IndexOf("case State.FirstExit:", fxSw2));
			string fxSeSeg = lxCalc.Substring(lxCalc.IndexOf("case State.SecondExit:", fxSw2));
			Check(fxFeSeg.Contains("GenerateStopLoss(") && !fxFeSeg.Contains("m_leStopOrder") && !fxFeSeg.Contains("m_seStopOrder")
				&& fxSeSeg.Contains("m_leStopOrder.Send(") && fxSeSeg.Contains("m_seStopOrder.Send(") && !fxSeSeg.Contains("GenerateStopLoss(") && !fxSeSeg.Contains("TargetOrder")
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"GenerateStopLoss\(").Count == 1 && System.Text.RegularExpressions.Regex.Matches(lxc, @"m_[ls]eStopOrder\.Send\(").Count == 2
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"m_[ls]xTargetOrder\.Send\(").Count == 2,
				"MI-C21 one protective stop per stage: FirstExit sends the +1R limit and GenerateStopLoss (-R), no stop order; SecondExit sends only the trailing stop (GenerateStopLoss dropped)");

			// ---- #94 exit-only audit: no order can open, add to or reverse a position ----
			// source: the only orders are 4 Limit/Stop exits (Sell / BuyToCover) plus GenerateStopLoss; no market or entry-type orders;
			// Sell orders are sent only in the PositionSide > 0 branch and BuyToCover only in the else branch
			var fxCreates = System.Text.RegularExpressions.Regex.Matches(lxc, @"OrderCreator\.(\w+)\(");
			var fxActions = System.Text.RegularExpressions.Regex.Matches(lxc, @"EOrderAction\.(\w+)");
			bool fxExitOnly = fxCreates.Count == 4 && fxActions.Count == 4
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"OrderCreator\.(Limit|Stop)\(new SOrderParameters\(Contracts\.UserSpecified, ""\w+"", EOrderAction\.(Sell|BuyToCover), OrderExit\.Total\)\);").Count == 4
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"OrderExit\.Total").Count == 4 && !lxc.Contains("OrderExit.FromAll") && !lxc.Contains("OrderExit.FromEntry");
			foreach (System.Text.RegularExpressions.Match m in fxCreates) fxExitOnly &= m.Groups[1].Value == "Limit" || m.Groups[1].Value == "Stop";
			foreach (System.Text.RegularExpressions.Match m in fxActions) fxExitOnly &= m.Groups[1].Value == "Sell" || m.Groups[1].Value == "BuyToCover";
			Check(fxExitOnly && !lxc.Contains("SellShort") && !lxc.Contains("EOrderAction.Buy)") && !lxc.Contains("EOrderAction.Buy,") && !lxc.Contains("MarketNextBar") && !lxc.Contains("MarketThisBar")
				&& System.Text.RegularExpressions.Regex.Matches(lxCalc, @"if \(PositionSide > 0\)\s*m_l[xe]\w+\.Send\([^;]*;\s*else\s*m_s[xe]\w+\.Send\(").Count == 2
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"\.Send\(").Count == 4 && lxc.Contains("m_armedSide = isLong ? 1 : -1;") && lxc.Contains("m_armedSide = 0;"),
				"MI-C22 exit-only source audit: 4 orders, all Limit/Stop with Sell or BuyToCover (no Buy/SellShort/market); each OrderExit.Total (exactly the lots sent, not per entry); Sell only sent while long, BuyToCover only while short; armed side set on re-arm, cleared when flat");
			// lots: every exit is 1..open lots while a position is open, 0 when none are open (never more than the position, so never a reversal)
			bool fxLotsOk = LX.halfExitLots(0) == 0 && LX.remainingExitLots(0) == 0;
			for (int n = 1; n <= 200; n++) fxLotsOk &= LX.halfExitLots(n) >= 1 && LX.halfExitLots(n) <= n && LX.remainingExitLots(n) == n && LX.halfExitLots(-n) <= n && LX.remainingExitLots(-n) == n;
			fxLotsOk &= LX.halfExitLots(int.MaxValue) <= int.MaxValue && LX.halfExitLots(int.MaxValue) > 0;
			// orders: none when flat, with no open lots, or for a side the exits were not armed for (stale levels after an unseen reversal)
			fxLotsOk &= LX.exitOrdersThisBar(LX.State.FirstExit, 1, 0, 0) == LX.State.None && LX.exitOrdersThisBar(LX.State.FirstExit, 1, 1, 0) == LX.State.None
				&& LX.exitOrdersThisBar(LX.State.SecondExit, 1, -1, 3) == LX.State.None && LX.exitOrdersThisBar(LX.State.FirstExit, -1, 1, 3) == LX.State.None
				&& LX.exitOrdersThisBar(LX.State.FirstExit, 0, 1, 3) == LX.State.None && LX.exitOrdersThisBar(LX.State.SecondExit, -1, -1, 3) == LX.State.SecondExit;
			Check(fxLotsOk, "MI-C23 exit sizes and gating: half / remaining exits are 1..open lots (0 with none open); nothing is sent flat, with 0 lots, unarmed or for the opposite side");
			// bar-by-bar: other signals open, add to and reverse the position; at every bar each order this study would send is an exit for
			// the open side with lots <= open, so filling any one of them (or all, as MC caps exits at the position) leaves the same side or flat
			Func<int[], string> fxAudit = pos => {
				int prev = 0, armed = 0, bad = 0, sent = 0; LX.State st = LX.State.None;
				for (int i = 0; i < pos.Length; i++)
				{
					if (LX.positionTransitionDue(prev, pos[i]))
					{
						bool rearm = LX.exitManagementRearmDue(st, prev, pos[i]);
						st = LX.stageAfterPositionChange(st, prev, pos[i]);
						if (st == LX.State.None) armed = 0; else if (rearm) armed = Math.Sign(pos[i]);
						prev = pos[i];
					}
					int open = Math.Abs(pos[i]), side = Math.Sign(pos[i]);
					LX.State o = LX.exitOrdersThisBar(st, armed, side, open);
					var lots = new System.Collections.Generic.List<int>();
					if (o == LX.State.FirstExit) { lots.Add(LX.halfExitLots(open)); lots.Add(open); }   // +1R limit, GenerateStopLoss (full position)
					else if (o == LX.State.SecondExit) lots.Add(LX.remainingExitLots(open));            // trailing stop
					foreach (int l in lots)
					{
						sent++;
						bool closesLong = side > 0;                                                     // Sell while long, BuyToCover while short
						int after = closesLong ? pos[i] - l : pos[i] + l;
						// invariant: strictly toward flat, never through zero: 0 <= |after| < open, same side or flat
						if (side == 0 || l <= 0 || l > open || Math.Sign(after) == -side || Math.Abs(after) >= open || (after != 0 && Math.Sign(after) != side)) bad++;
					}
					if (side == 0 && lots.Count > 0) bad++;
				}
				return sent + ":" + bad;
			};
			string fxA = fxAudit(new[] { 0, 4, 4, 2, 2, 5, 5, -3, -3, -6, -6, -2, 0, 0 });   // open, half fills, add-on, reversal, add-on, partial, flat
			string fxB = fxAudit(new[] { 0, 1, 1, -1, -1, 1, 0 });                             // 1 lot reversed twice by another signal
			string fxC = fxAudit(new[] { 0, -2, -1, 3, 3, 1, 0 });                             // short half fills, reversed long in SecondExit
			Check(fxA.EndsWith(":0") && fxB.EndsWith(":0") && fxC.EndsWith(":0") && !fxA.StartsWith("0:") && !fxB.StartsWith("0:") && !fxC.StartsWith("0:")
				&& LX.stageAfterPositionChange(LX.State.FirstExit, 2, 5) == LX.State.FirstExit && LX.stageAfterPositionChange(LX.State.SecondExit, 2, -3) == LX.State.FirstExit,
				"MI-C24 exit-only bar-by-bar (add-on and reversal by another signal): every order is an exit for the open side, 1..open lots, none when flat; each strictly reduces toward flat, never through zero [" + fxA + " | " + fxB + " | " + fxC + "]");
			// multi-entry (add-ons by another signal): MC fills an exit over the entries. OrderExit.Total exits ONLY the lots sent, first to last
			// entry (PG L2584); the default FromAll would exit that many lots from EACH entry (PG L2563-2575: 5+1+2, Sell 2 -> 5). Model both:
			// with Total the lots exited == the lots intended <= open lots, for every order the study sends; FromAll would overshoot
			Func<int[], int, bool, int> fxFill = (entries, lots, total) => {
				int done = 0;
				foreach (int e in entries) { int take = total ? Math.Min(e, lots - done) : Math.Min(e, lots); if (take <= 0) { if (total) break; else continue; } done += take; }
				return done;
			};
			bool fxMultiOk = fxFill(new[] { 5, 1, 2 }, 2, false) == 5 && fxFill(new[] { 2, 1, 5 }, 5, true) == 5;   // the PG's own FromAll / Total examples
			int fxMultiCases = 0;
			foreach (int[] ent in new[] { new[] { 5, 1, 2 }, new[] { 1, 1, 1, 1 }, new[] { 2, 3 }, new[] { 1, 7 }, new[] { 4, 4, 4 }, new[] { 1, 1 }, new[] { 3 } })
			{
				int open = 0; foreach (int e in ent) open += e;
				foreach (int side in new[] { 1, -1 })
				{
					int half = LX.halfExitLots(side * open), rest = LX.remainingExitLots(side * open);
					foreach (int intended in new[] { half, rest })
					{
						int exited = fxFill(ent, intended, true);
						int after = side * (open - exited);
						fxMultiOk &= exited == intended && exited <= open && exited >= 1 && Math.Sign(after) != -side && Math.Abs(after) < open;
						fxMultiCases++;
					}
				}
			}
			int fxAddOnHalf = LX.halfExitLots(8);
			Check(fxMultiOk && fxMultiCases == 28 && fxAddOnHalf == 4 && fxFill(new[] { 5, 1, 2 }, fxAddOnHalf, true) == 4 && fxFill(new[] { 5, 1, 2 }, fxAddOnHalf, false) == 7
				&& System.Text.RegularExpressions.Regex.Matches(lxc, @"OrderExit\.Total\)\);").Count == 4,
				"MI-C24b multi-entry add-on: with OrderExit.Total every exit fills exactly the intended lots (<= open), strictly toward flat; e.g. entries 5+1+2, half = 4 exits 4 (FromAll would exit 4+1+2 = 7) [" + fxMultiCases + " cases]");
		}

		Console.WriteLine("OrderFlowTests: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}

	static OF.FootprintBar Footprint0(this OF.FlowEngine e) { return e.Closed(0); }
}
