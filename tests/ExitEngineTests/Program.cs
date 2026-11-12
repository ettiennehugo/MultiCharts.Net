using System;
using System.Globalization;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for the GX engine in _GenericExit.Strategy.CS (specs/generic-exit-manager-spec.md rev 1.1 §8.3).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-8) { return Math.Abs(a - b) < eps; }
	static bool Has(System.Collections.Generic.List<string> log, string part) { return log.Any(s => s.Contains(part)); }

	static ExitMarket Mkt(double tick = 0.25, double bpv = 50, int floor = 14, double atr = 2)
	{
		return new ExitMarket { Tick = tick, Bpv = bpv, FloorTicks = floor, AtrStop = atr, AtrTrail = atr, BarMinutes = 1, BarKind = EGxBarKind.Minute, ResolutionLabel = "1 min" };
	}

	static ExitSession Eng(Action<ExitConfig> set = null, ExitMarket? mkt = null)
	{
		var e = new ExitSession();
		if (set != null) set(e.Config);
		e.Start(mkt ?? Mkt());
		return e;
	}

	static void Main()
	{
		Defaults();
		InitialStops();
		Disaster();
		Triggers();
		Rev06();
		T1QtyAndSkip();
		BreakEvenAndTrail();
		Precedence();
		Clock();
		Control();
		Idx();
		RAccount();
		RiskFields();
		OverBudget();
		Console.WriteLine("ExitEngineTests: " + s_pass.ToString(CultureInfo.InvariantCulture) + " passed, " + s_fail.ToString(CultureInfo.InvariantCulture) + " failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	static void Defaults()
	{
		var c = new ExitConfig();
		Check(c.InitialStop == EInitialStop.AtrMultiple && c.StopAtrMult == 1.5 && c.StopAtrLength == 14, "default AtrMultiple 1.5 x 14");
		Check(c.StopTicks == 0 && c.StopSwingBars == 5 && c.StopPct == 0 && c.FillBarDisasterStop, "default stop extras");
		Check(c.TargetMode == ETargetMode.ScaleOutThenRunner && c.T1Unit == ETriggerUnit.RMultiple && c.T1Value == 1 && c.T1ScalePct == 50, "default T1");
		Check(c.FinalTargetUnit == ETriggerUnit.RMultiple && c.FinalTargetValue == 0 && c.SingleLotMode == ESingleLotMode.ExitAtT1, "default final off, ExitAtT1");
		Check(c.BreakEven == EBreakEven.AfterT1 && c.BreakEvenUnit == ETriggerUnit.RMultiple && c.BreakEvenValue == 1 && c.BreakEvenOffsetTicks == 1, "default BE");
		Check(c.TrailMode == ETrailMode.Chandelier && c.TrailStart == ETrailStart.AfterT1 && c.TrailAtrMult == 3 && c.TrailAtrLength == 22, "default trail");
		Check(c.TrailStartUnit == ETriggerUnit.RMultiple && c.TrailStartValue == 1 && c.TrailBars == 2 && c.TrailEmaLength == 20, "default trail unit");
		Check(c.TimeStopBars == 0 && c.TimeStopMinutes == 0 && c.SessionExit == ESessionExit.FlatBy && c.FlatByHHMM == 1555 && c.MaxHoldDays == 0, "default clock");
		Check(c.NewsExitMinutes == 0 && c.RollExit == ERollExit.FlattenBeforeSplice && c.AddStopMode == EAddStopMode.RaiseToAddLow && c.ExitControl == EExitControl.None, "default news roll add control");
		Check(c.ControlExitBarsM1 == 30 && c.ControlExitBarsM5 == 12 && c.ControlExitBarsM15 == 8 && c.ControlExitBarsH1 == 6 && c.ControlExitBarsD1 == 5, "default control N");
		Check(c.InitialStopIdx == -1 && c.T1UnitIdx == -1 && c.FinalTargetUnitIdx == -1 && c.BreakEvenIdx == -1 && c.BreakEvenUnitIdx == -1 && c.TrailModeIdx == -1 && c.TrailStartIdx == -1 && c.TrailStartUnitIdx == -1, "default idx -1");
		Check(c.ControlSeed == 12345, "ControlSeed 12345");
	}

	static void InitialStops()
	{
		foreach (bool lng in new[] { true, false })
		{
			string side = lng ? "long" : "short";
			var es = Mkt();
			es.SignalLow = 4990; es.SignalHigh = 5010; es.SwingLow = 4980; es.SwingHigh = 5020; es.EntryStop = lng ? 4990 : 5010;
			var e = Eng(c => { c.Embedded = true; c.StopTicks = 20; c.StopPct = 1; }, es);
			double entry = 5000;
			e.Effective.InitialStop = EInitialStop.AtrMultiple;
			double atr = e.InitialStop(lng, entry, es);
			Check(Near(atr, lng ? 5000 - 3.5 : 5000 + 3.5), side + " AtrMultiple widened to ES floor 14 ticks (3pt raw < 3.5)");
			e.Effective.InitialStop = EInitialStop.FixedTicks;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4995 : 5005), side + " FixedTicks 20 = 5pt, outside the floor");
			e.Config.StopTicks = 8;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4996.5 : 5003.5), side + " FixedTicks 8 widened to 14");
			e.Effective.InitialStop = EInitialStop.SignalBarExtreme;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4989.75 : 5010.25), side + " SignalBarExtreme");
			e.Effective.InitialStop = EInitialStop.SwingExtreme;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4979.75 : 5020.25), side + " SwingExtreme");
			e.Effective.InitialStop = EInitialStop.Percent;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4950 : 5050), side + " Percent 1");
			e.Effective.InitialStop = EInitialStop.EntryProvided;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4990 : 5010), side + " EntryProvided kept when outside the floor");
			es.EntryStop = lng ? 4998 : 5002;
			Check(Near(e.InitialStop(lng, entry, es), lng ? 4996.5 : 5003.5), side + " EntryProvided widened to the floor");

			var nq = Mkt(0.25, 20, 19, 2);
			e.Effective.InitialStop = EInitialStop.AtrMultiple;
			Check(Near(e.InitialStop(lng, entry, nq), lng ? 5000 - 4.75 : 5000 + 4.75), side + " NQ floor 19 ticks widens a 3pt ATR stop");
		}
		var bad = Eng(c => { c.StopTicks = 0; c.InitialStop = EInitialStop.FixedTicks; });
		Check(bad.Effective.InitialStop == EInitialStop.AtrMultiple && Has(bad.Log, "Error: GX INITIAL-STOP invalid(FixedTicks StopTicks=0) using AtrMultiple"), "FixedTicks 0 falls back");
		Check(bad.Log.Count(s => s.Contains("INITIAL-STOP invalid")) == 1, "invalid stop logged once");
		bad.InitialStop(true, 5000, Mkt());
		Check(bad.Log.Count(s => s.Contains("INITIAL-STOP invalid")) == 1, "later stops do not log the error again");
		var pct = Eng(c => { c.InitialStop = EInitialStop.Percent; c.StopPct = 0; });
		Check(pct.Effective.InitialStop == EInitialStop.AtrMultiple && Has(pct.Log, "invalid(Percent StopPct=0)"), "Percent 0 falls back");
		var sw = Eng(c => { c.InitialStop = EInitialStop.SwingExtreme; c.StopSwingBars = 0; });
		Check(Has(sw.Log, "invalid(SwingExtreme StopSwingBars=0)"), "SwingExtreme 0 falls back");
		var wrap = Eng(c => { c.Embedded = false; c.InitialStop = EInitialStop.EntryProvided; });
		Check(wrap.Effective.InitialStop == EInitialStop.AtrMultiple && Has(wrap.Log, "invalid(EntryProvided EntryProvided=wrapper)"), "EntryProvided invalid in the wrapper");
		var emb = Eng(c => { c.Embedded = true; c.InitialStop = EInitialStop.EntryProvided; });
		Check(emb.Effective.InitialStop == EInitialStop.EntryProvided && !Has(emb.Log, "INITIAL-STOP invalid"), "EntryProvided valid when embedded");
	}

	static void Disaster()
	{
		foreach (bool lng in new[] { true, false })
		{
			var m = Mkt();
			m.SignalLow = 4990; m.SignalHigh = 5010; m.SwingLow = 4988; m.SwingHigh = 5012;
			var e = Eng(c => { c.StopTicks = 20; c.StopPct = 0.05; }, m);
			e.Effective.InitialStop = EInitialStop.AtrMultiple;
			Check(Near(e.DisasterDistance(5000, m), 3.5) && Near(e.DisasterDollars(5000, m), 175), (lng ? "long " : "short ") + "ATR disaster uses the floor, $175/contract");
			e.Effective.InitialStop = EInitialStop.FixedTicks;
			Check(Near(e.DisasterDistance(5000, m), 5), "FixedTicks disaster is 20 ticks");
			e.Config.StopTicks = 4;
			Check(Near(e.DisasterDistance(5000, m), 3.5), "FixedTicks disaster never under the floor");
			e.Config.StopTicks = 20;
			e.Effective.InitialStop = EInitialStop.Percent;
			Check(Near(e.DisasterDistance(5000, m), 3.5), "Percent 0.05 of 5000 is under the floor");
			e.Config.StopPct = 1;
			Check(Near(e.DisasterDistance(5000, m), 50), "Percent 1 disaster is 50 points");
			e.Effective.InitialStop = EInitialStop.SignalBarExtreme;
			double sig = Math.Max(Math.Abs(5000 - (4990 - 0.25)), Math.Abs((5010 + 0.25) - 5000));
			Check(Near(e.DisasterDistance(5000, m), sig), "SignalBarExtreme disaster is the larger side");
			e.Effective.InitialStop = EInitialStop.SwingExtreme;
			double swd = Math.Max(Math.Abs(5000 - (4988 - 0.25)), Math.Abs((5012 + 0.25) - 5000));
			Check(Near(e.DisasterDistance(5000, m), swd), "SwingExtreme disaster is the larger side");
			double prior = e.DisasterDistance(5000, m);
			Check(Near(ExitSession.HandoverStop(lng, 5002, 3.5), lng ? 4998.5 : 5005.5), (lng ? "long" : "short") + " handover stop is fill ∓ the prior flat distance");
			var p = new ExitPosition();
			Check(e.DisasterThisBar(p, true, 5000, m) && p.DisasterCalling && Near(p.DisasterDistance, prior), "disaster called while flat");
			Check(!e.DisasterThisBar(p, false, 5002, m) && !p.DisasterCalling, "disaster not called in a position");
			Check(e.DisasterThisBar(p, true, 5000, m) && p.DisasterCalling, "disaster called again once flat");
		}
		var emb = Eng(c => c.Embedded = true);
		Check(!emb.DisasterThisBar(new ExitPosition(), true, 5000, Mkt()) && Has(emb.Log, "GX START") && !Has(emb.Log, "FILLBAR"), "embedded sends no disaster stop and no FILLBAR line");
		var off = Eng(c => c.FillBarDisasterStop = false);
		Check(!off.DisasterThisBar(new ExitPosition(), true, 5000, Mkt()) && Has(off.Log, "FILLBAR-UNPROTECTED"), "FillBarDisasterStop off");
		var on = Eng();
		Check(Has(on.Log, "EXIT Info: GX FILLBAR-PROTECTION mode(DisasterStop) builtin(GenerateStopLoss PerContract) handover(next bar after fill)"), "FILLBAR-PROTECTION line");
		Check(on.NoteFillBarStop(true, true, 100, 80, 12) && on.FillBarStops == 1 && Has(on.Log, "FILLBAR-STOP bar(12) netChange($-20.00)"), "FILLBAR-STOP detected");
		Check(!on.NoteFillBarStop(false, true, 100, 80, 13) && on.FillBarStops == 1, "a seen position is not a fill-bar stop");
	}

	static void Triggers()
	{
		foreach (bool lng in new[] { true, false })
		{
			double entry = 5000, r = 4, tick = 0.25, bpv = 50;
			string err;
			double dR = ExitSession.TriggerDistance(ETriggerUnit.RMultiple, 1, entry, r, tick, bpv, out err);
			double dP = ExitSession.TriggerDistance(ETriggerUnit.PercentOfEntry, 0.5, entry, r, tick, bpv, out err);
			double dT = ExitSession.TriggerDistance(ETriggerUnit.Ticks, 6, entry, r, tick, bpv, out err);
			double dPt = ExitSession.TriggerDistance(ETriggerUnit.Points, 1.1, entry, r, tick, bpv, out err);
			double dC = ExitSession.TriggerDistance(ETriggerUnit.CurrencyPerContract, 250, entry, r, tick, bpv, out err);
			Check(Near(dR, 4) && Near(ExitSession.TriggerLevel(lng, entry, dR, tick), lng ? 5004 : 4996), (lng ? "L" : "S") + " RMultiple");
			Check(Near(dP, 25) && Near(ExitSession.TriggerLevel(lng, entry, dP, tick), lng ? 5025 : 4975), (lng ? "L" : "S") + " PercentOfEntry");
			Check(Near(dT, 1.5) && Near(ExitSession.TriggerLevel(lng, entry, dT, tick), lng ? 5001.5 : 4998.5), (lng ? "L" : "S") + " Ticks");
			Check(Near(ExitSession.TriggerLevel(lng, entry, dPt, tick), lng ? 5001.25 : 4998.75), (lng ? "L" : "S") + " Points 1.1 rounds away to 1.25");
			Check(Near(dC, 5) && Near(ExitSession.TriggerLevel(lng, entry, dC, tick), lng ? 5005 : 4995), (lng ? "L" : "S") + " CurrencyPerContract $250 = 5pt");
			Check(double.IsNaN(ExitSession.TriggerDistance(ETriggerUnit.RMultiple, 0, entry, r, tick, bpv, out err)) && err == "value<=0", (lng ? "L" : "S") + " value 0 is off");
		}
		var e = Eng(c => { c.T1Value = 0; });
		Check(!e.T1On && Has(e.Log, "Error: GX T1 value(0) <= 0: feature off"), "T1 value 0 logs Error and turns the feature off");
		var f = Eng(c => { c.FinalTargetValue = -1; });
		Check(!f.FinalOn && Has(f.Log, "Error: GX FINAL value(-1) <= 0: feature off"), "negative final target is off");
		var be = Eng(c => { c.BreakEven = EBreakEven.AtTrigger; c.BreakEvenValue = 0; });
		Check(be.BeDisabled && Has(be.Log, "Error: GX BE value(0) <= 0: feature off"), "BE value 0 is off");
		var tr = Eng(c => { c.TrailStart = ETrailStart.AtTrigger; c.TrailStartValue = 0; });
		Check(tr.TrailTriggerDisabled && Has(tr.Log, "Error: GX TRAIL value(0) <= 0: feature off"), "trail start value 0 is off");
	}

		static void Rev06()
	{
		var off = Eng();
		Check(!off.FinalOn && off.Config.FinalTargetValue == 0, "FinalTargetValue 0 leaves no final target");
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng(c => { c.TrailStart = ETrailStart.AtTrigger; c.TargetMode = ETargetMode.None; });
			var p = new ExitPosition();
			double stop = lng ? 4996 : 5004;
			e.Open(p, lng, 5000, 5000, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			var bar = Bar(lng, lng ? 5003.75 : 4996.25, lng ? 4996.5 : 5003.5, lng ? 5003.75 : 4996.25);
			var d0 = e.Manage(p, bar);
			Check(d0.Reason == null && !p.TrailOn, (lng ? "L" : "S") + " AtTrigger 1R not reached yet");
			var hit = Bar(lng, lng ? 5004 : 4996, lng ? 4996.5 : 5003.5, lng ? 5004 : 4996);
			var d1 = e.Manage(p, hit);
			Check(d1.Reason == null && p.TrailOn, (lng ? "L" : "S") + " TrailStart AtTrigger RMultiple 1 starts at ±1R");
		}
	}

	static void T1QtyAndSkip()
	{
		int[] qs = { 1, 2, 3, 5 };
		int[] pcts = { 0, 33, 50, 100 };
		foreach (int q in qs)
		{
			foreach (int pct in pcts)
			{
				int exitAt = ExitSession.T1Qty(q, pct, ESingleLotMode.ExitAtT1, ETargetMode.ScaleOutThenRunner);
				int runner = ExitSession.T1Qty(q, pct, ESingleLotMode.RunnerOnly, ETargetMode.ScaleOutThenRunner);
				int expect;
				if (pct == 0) expect = 0;
				else if (q == 1) expect = 1;
				else if (pct >= 100) expect = q;
				else
				{
					expect = (int)Math.Floor(q * (pct / 100.0));
					if (expect < 1) expect = 1;
					if (expect > q - 1) expect = q - 1;
				}
				Check(exitAt == expect, "T1 q=" + q + " pct=" + pct + " ExitAtT1 => " + expect + " got " + exitAt);
				Check(q == 1 ? runner == (pct == 0 ? 0 : 0) : runner == exitAt, "T1 q=" + q + " pct=" + pct + " RunnerOnly");
				if (q == 1 && pct > 0) Check(runner == 0 && exitAt == 1, "q=1 SingleLotMode");
			}
		}
		Check(ExitSession.T1Qty(4, 50, ESingleLotMode.ExitAtT1, ETargetMode.None) == 0, "TargetMode None has no T1");
		Check(ExitSession.T1Qty(4, 10, ESingleLotMode.ExitAtT1, ETargetMode.SingleTarget) == 4, "SingleTarget takes the whole position");

		var when = new DateTime(2024, 6, 3, 10, 0, 0);
		foreach (bool lng in new[] { true, false })
		{
			double planned = 5000;
			double plannedStop = lng ? 4996 : 5004;
			double t1 = lng ? 5004 : 4996;
			double fillShort = lng ? 5002 : 4998;
			double actualStop = lng ? 4999 : 5001;
			var e = Eng();
			var p = new ExitPosition();
			e.Open(p, lng, planned, plannedStop, fillShort, actualStop, 2, 0.25, 50, 100000, "LE", when, 0);
			Check(!p.T1Skipped && p.T1Qty == 1 && Near(p.T1Level, t1), (lng ? "L" : "S") + " fill short of the planned T1 keeps it");
			Check(Near(p.PlannedRPrice, 4) && Near(p.InitialRPrice, 3) && Near(p.T1Level, t1), (lng ? "L" : "S") + " planned T1 uses planned R, not the fill R");

			var past = Eng();
			var pp = new ExitPosition();
			double fillPast = lng ? 5006 : 4994;
			past.Open(pp, lng, planned, plannedStop, fillPast, actualStop, 2, 0.25, 50, 100000, "LE", when, 0);
			Check(pp.T1Skipped && pp.T1Qty == 0 && pp.TrailOn && Near(pp.T1Level, t1), (lng ? "L" : "S") + " fill at or beyond the planned T1 skips");
			Check(past.Log.Any(s => s.Contains("T1-SKIPPED side(" + (lng ? "+1" : "-1") + ")") && s.Contains("action(trail-all)") && s.Contains("qty(2)")), (lng ? "L" : "S") + " T1-SKIPPED info line");

			var fin = Eng(c => c.FinalTargetValue = 2);
			var pf = new ExitPosition();
			fin.Open(pf, lng, planned, plannedStop, fillPast, actualStop, 2, 0.25, 50, 100000, "LE", when, 0);
			Check(pf.T1Skipped && !pf.FinalSkipped && Near(pf.FinalLevel, lng ? 5008 : 4992), (lng ? "L" : "S") + " final target stays beyond the fill");

			var single = Eng(c => { c.TargetMode = ETargetMode.SingleTarget; c.FinalTargetValue = 1; });
			var ps = new ExitPosition();
			single.Open(ps, lng, planned, plannedStop, fillPast, actualStop, 2, 0.25, 50, 100000, "LE", when, 0);
			Check(ps.T1Skipped && ps.FinalSkipped && ps.TrailOn && Near(ps.FinalLevel, t1), (lng ? "L" : "S") + " SingleTarget passed at the fill trails all");
		}
		var plan = Eng();
		double pe, ps2;
		Check(plan.NotePlan(true, true, true, 5000, 4996, 10, 10, out pe, out ps2) && plan.PlanSource == "plan" && Near(pe, 5000) && Near(ps2, 4996), "valid plan keys => plan");
		Check(!Has(plan.Log, _ExitEngine.PlanFillInfo), "a valid plan does not log the fill fallback");
		Check(!plan.NotePlan(true, false, true, 5000, 4996, 10, 10, out pe, out ps2) && plan.PlanSource == "fill" && Has(plan.Log, _ExitEngine.PlanFillInfo), "missing key => fill");
		int nPlan = plan.Log.Count(s => s.Contains("T1-PLAN"));
		Check(!plan.NotePlan(true, true, true, double.NaN, 4996, 10, 10, out pe, out ps2) && plan.PlanSource == "fill", "non-finite => fill");
		Check(!plan.NotePlan(true, true, true, 5000, 5001, 10, 10, out pe, out ps2), "wrong side => fill");
		Check(!plan.NotePlan(false, true, true, 5000, 4996, 10, 10, out pe, out ps2), "short plan with the stop below entry => fill");
		Check(!plan.NotePlan(true, true, true, 5000, 4996, 12, 10, out pe, out ps2), "keys written after the fill bar => fill");
		Check(plan.Log.Count(s => s.Contains("T1-PLAN")) == nPlan, "fill fallback is logged once per run");
		Check(_ExitEngine.PlanKeysValid(false, true, true, 5000, 5004, 9, 10), "short plan written before the fill is valid");
		Check(!_ExitEngine.PlanKeysValid(true, true, true, double.PositiveInfinity, 4996, 10, 10), "infinity is not a plan");
		var m = Mkt();
		Check(Near(_ExitEngine.InitialStop(EInitialStop.AtrMultiple, true, 5000, m, new ExitConfig(), false), 4996.5), "static InitialStop matches the widened ATR stop");
	}

	static void BreakEvenAndTrail()
	{
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng();
			var p = new ExitPosition();
			double fill = 5000, stop = lng ? 4996 : 5004;
			e.Open(p, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			e.ScaleOut(p, 1, lng ? 5004 : 4996, 50);
			var bar = Bar(lng, lng ? 5004 : 4996.5, lng ? 4996.5 : 5003.5, lng ? 5004 : 4996.5);
			var d = e.Manage(p, bar);
			double be = ExitSession.BreakEvenStop(lng, fill, 1, 0.25);
			Check(p.BeDone && Near(p.Stop, be) && d.Reason == null, (lng ? "L" : "S") + " AfterT1 moves the stop to entry ± 1 tick");

			var tight = Eng();
			var pT = new ExitPosition();
			tight.Open(pT, lng, fill, fill, lng ? 5001 : 4999, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			tight.ScaleOut(pT, 1, lng ? 5004 : 4996, 50);
			tight.Manage(pT, bar);
			Check(Near(pT.Stop, lng ? 5001 : 4999), (lng ? "L" : "S") + " BE does not loosen a tighter stop");

			var fb = Eng(c => { c.T1ScalePct = 0; c.BreakEven = EBreakEven.AfterT1; });
			Check(Has(fb.Log, "GX BE AfterT1 fallback(AtTrigger)") && Has(fb.Log, "GX TRAIL AfterT1 fallback(AtTrigger)"), "fallback logged once for BE and trail");
			Check(fb.Log.Count(s => s.Contains("fallback(AtTrigger)")) == 2, "each fallback once");
			var pF = new ExitPosition();
			fb.Open(pF, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(pF.T1Qty == 0, "no T1 order when scale is 0");
			var early = Bar(lng, 5002, 4998, lng ? 5002 : 4998);
			fb.Manage(pF, early);
			Check(!pF.BeDone && !pF.TrailOn, (lng ? "L" : "S") + " fallback waits for +1R");
			var hit = Bar(lng, lng ? 5004 : 4996, lng ? 4996.5 : 5003.5, lng ? 5004 : 4996);
			fb.Manage(pF, hit);
			Check(pF.BeDone && pF.TrailOn, (lng ? "L" : "S") + " fallback AtTrigger fires at ±1R");

			var tr = Eng(c => { c.TrailStart = ETrailStart.Immediately; c.TrailAtrMult = 1; c.TargetMode = ETargetMode.None; c.BreakEven = EBreakEven.Off; });
			var pR = new ExitPosition();
			tr.Open(pR, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			double close = lng ? 5010 : 4990;
			double extreme = lng ? 5010.2 : 4989.8;
			var tbar = Bar(lng, extreme, lng ? 4996.5 : 5003.5, close);
			tbar.AtrTrail = 0.3;
			tr.Manage(pR, tbar);
			double candidate = lng ? pR.Extreme - 0.3 : pR.Extreme + 0.3;
			double capped = lng ? Math.Max(stop, Math.Min(candidate, close - 0.25)) : Math.Min(stop, Math.Max(candidate, close + 0.25));
			Check(candidate < close && candidate > close - 0.25 || !lng, (lng ? "L" : "S") + " fixture puts the candidate inside the last tick");
			if (!lng) Check(candidate > close && candidate < close + 0.25, "short fixture puts the candidate inside the last tick");
			Check(Near(pR.Stop, capped), (lng ? "L" : "S") + " trail capped one tick inside the close, got " + pR.Stop.ToString(CultureInfo.InvariantCulture));
			double held = pR.Stop;
			var back = Bar(lng, lng ? 5010 : 4990, lng ? 5010 : 4990, lng ? 5010.5 : 4989.9);
			back.AtrTrail = 8;
			var backDecision = tr.Manage(pR, back);
			Check(backDecision.Reason == null && (lng ? pR.Stop >= held - 1e-9 : pR.Stop <= held + 1e-9), (lng ? "L" : "S") + " trail is one-way");

			var thru = Eng(c => { c.TrailStart = ETrailStart.Immediately; c.TrailMode = ETrailMode.Ema; c.TargetMode = ETargetMode.None; c.BreakEven = EBreakEven.Off; });
			var pU = new ExitPosition();
			thru.Open(pU, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			var ubar = Bar(lng, lng ? 5001 : 4990, lng ? 5010 : 4999, lng ? 5000.25 : 4999.75);
			ubar.Ema = lng ? 5002 : 4998;
			var ud = thru.Manage(pU, ubar);
			Check(ud.TrailThrough && ud.Reason == null && pU.TrailThroughNext, (lng ? "L" : "S") + " candidate through the market arms TRAIL-THROUGH");
			var next = Bar(lng, lng ? 5001 : 4990, lng ? 5010 : 4999, lng ? 5000.5 : 4999.5);
			var nd = thru.Manage(pU, next);
			Check(nd.Reason == "TRAIL-THROUGH", (lng ? "L" : "S") + " next bar exits TRAIL-THROUGH");
		}
	}

	static ExitBar Bar(bool lng, double favorable, double adverse, double close)
	{
		return new ExitBar
		{
			High = lng ? favorable : Math.Max(adverse, close),
			Low = lng ? Math.Min(adverse, close) : favorable,
			Close = close,
			Tick = 0.25,
			Bpv = 50,
			AtrTrail = 2,
			TimeEt = new DateTime(2024, 6, 3, 10, 1, 0),
			BarMinutes = 1,
			MinutesToNews = double.NaN
		};
	}

	static void Precedence()
	{
		string all = ExitSession.Precedence(true, true, true, true, true, true, true, true, true, "T1");
		Check(all == "FLAT-BY-MM", "1 force close wins");
		Check(ExitSession.Precedence(false, true, true, true, true, true, true, true, true, "TARGET") == "STOP", "2 stop before target");
		Check(ExitSession.Precedence(false, false, true, true, true, true, true, true, true, "T1") == "T1", "3 target");
		Check(ExitSession.Precedence(false, false, false, true, true, true, true, true, true, "T1") == "NEWS", "4 news");
		Check(ExitSession.Precedence(false, false, false, false, true, true, true, true, true, "T1") == "FLATBY", "5 session");
		Check(ExitSession.Precedence(false, false, false, false, false, true, true, true, true, "T1") == "ROLL", "6 roll");
		Check(ExitSession.Precedence(false, false, false, false, false, false, true, true, true, "T1") == "INVALIDATE", "7 invalidate");
		Check(ExitSession.Precedence(false, false, false, false, false, false, false, true, true, "T1") == "TIME", "8 time");
		Check(ExitSession.Precedence(false, false, false, false, false, false, false, false, true, "T1") == "CONTROL", "9 control");
		Check(ExitSession.Precedence(false, false, false, false, false, false, false, false, false, "T1") == null, "none");

		foreach (bool lng in new[] { true, false })
		{
			var e = Eng(c => { c.Embedded = true; c.ExitControl = EExitControl.FixedBars; c.NewsExitMinutes = 30; });
			e.MappedBars = 2;
			var p = new ExitPosition();
			e.Open(p, lng, 5000, 5000, lng ? 4990 : 5010, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			p.ControlN = 1;
			var bar = Bar(lng, 5001, lng ? 4980 : 5020, 5000);
			bar.ForceClose = true;
			bar.Invalidate = true;
			bar.RollFlattenBar = true;
			bar.TimeEt = new DateTime(2024, 6, 3, 15, 55, 0);
			bar.MinutesToNews = 5;
			var d = e.Manage(p, bar);
			Check(d.Reason == "FLAT-BY-MM", (lng ? "L" : "S") + " Manage returns the first of several exits");
		}
	}

	static void Clock()
	{
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng(c => c.TimeStopBars = 3);
			var p = new ExitPosition();
			e.Open(p, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			ExitDecision d = new ExitDecision();
			for (int i = 0; i < 2; i++) d = e.Manage(p, Quiet(lng, new DateTime(2024, 6, 3, 10, i + 1, 0)));
			Check(d.Reason == null, (lng ? "L" : "S") + " time stop waits for 3 bars");
			d = e.Manage(p, Quiet(lng, new DateTime(2024, 6, 3, 10, 4, 0)));
			Check(d.Reason == "TIME", (lng ? "L" : "S") + " time stop on bar 3");

			var em = Eng(c => c.TimeStopMinutes = 30);
			var pm = new ExitPosition();
			em.Open(pm, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(em.Manage(pm, Quiet(lng, new DateTime(2024, 6, 3, 10, 29, 0))).Reason == null, (lng ? "L" : "S") + " 29 min is not the time stop");
			Check(em.Manage(pm, Quiet(lng, new DateTime(2024, 6, 3, 10, 30, 0))).Reason == "TIME", (lng ? "L" : "S") + " 30 min time stop");

			var es = Eng();
			var ps = new ExitPosition();
			es.Open(ps, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(es.Manage(ps, Quiet(lng, new DateTime(2024, 6, 3, 15, 54, 0))).Reason == null, (lng ? "L" : "S") + " 15:54 holds");
			Check(es.Manage(ps, Quiet(lng, new DateTime(2024, 6, 3, 15, 55, 0))).Reason == "FLATBY", (lng ? "L" : "S") + " FlatBy 15:55");

			var ee = Eng();
			var pe = new ExitPosition();
			ee.Open(pe, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			var early = Quiet(lng, new DateTime(2024, 6, 3, 13, 9, 0));
			early.EarlyClose = true; early.RthCloseHHMM = 1315;
			Check(ee.Manage(pe, early).Reason == null, (lng ? "L" : "S") + " early close waits until RthClose − 5 min");
			early.TimeEt = new DateTime(2024, 6, 3, 13, 10, 0);
			Check(ee.Manage(pe, early).Reason == "FLATBY", (lng ? "L" : "S") + " early close flattens at 13:10");

			var hol = Eng();
			var ph = new ExitPosition();
			hol.Open(ph, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			var hb = Quiet(lng, new DateTime(2024, 6, 3, 15, 55, 0));
			hb.HolidayClosed = true;
			Check(hol.Manage(ph, hb).Reason == null, (lng ? "L" : "S") + " a full-close holiday does not flatten");
		}
		var daily = Mkt();
		daily.BarKind = EGxBarKind.Day; daily.DailyOrLonger = true; daily.ResolutionLabel = "1 day"; daily.BarMinutes = 1440;
		var ed = Eng(null, daily);
		Check(Has(ed.Log, "Warning: GX SESSION FlatBy ignored on 1 day bars: HoldOvernight"), "daily FlatBy warning");
		var pd = new ExitPosition();
		ed.Open(pd, true, 5000, 5000, 4900, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 0, 0, 0), 0);
		var db = Quiet(true, new DateTime(2024, 6, 3, 15, 55, 0));
		Check(ed.Manage(pd, db).Reason == null, "daily FlatBy does not fire inside the bar");

		var hold = Eng(c => { c.SessionExit = ESessionExit.HoldOvernight; c.MaxHoldDays = 2; });
		var ph2 = new ExitPosition();
		hold.Open(ph2, false, 5000, 5000, 5100, 1, 0.25, 50, 100000, "SE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		Check(hold.Manage(ph2, Quiet(false, new DateTime(2024, 6, 4, 15, 55, 0))).Reason == null, "MaxHoldDays 2 holds on day 1");
		Check(hold.Manage(ph2, Quiet(false, new DateTime(2024, 6, 5, 10, 0, 0))).Reason == "TIME", "MaxHoldDays 2 exits on day 2");

		var news = Eng(c => c.NewsExitMinutes = 10);
		Check(!ExitSession.NewsWindow(0, 10, 1) && !ExitSession.NewsWindow(11, 10, 1) && ExitSession.NewsWindow(10, 10, 1) && !ExitSession.NewsWindow(double.NaN, 10, 1), "news window 0 < m < N+B");
		var pn = new ExitPosition();
		news.Open(pn, true, 5000, 5000, 4900, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		var nb = Quiet(true, new DateTime(2024, 6, 3, 10, 1, 0));
		nb.MinutesToNews = 10;
		Check(news.Manage(pn, nb).Reason == "NEWS", "news exit inside the window");

		var roll = Eng(c => c.SessionExit = ESessionExit.HoldOvernight);
		var pr = new ExitPosition();
		roll.Open(pr, true, 5000, 5000, 4900, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		var rb = Quiet(true, new DateTime(2024, 6, 3, 16, 0, 0));
		rb.RollFlattenBar = true;
		Check(roll.Manage(pr, rb).Reason == "ROLL", "roll flatten before the splice");
		var ign = Eng(c => { c.SessionExit = ESessionExit.HoldOvernight; c.RollExit = ERollExit.Ignore; });
		var pi = new ExitPosition();
		ign.Open(pi, false, 5000, 5000, 5100, 1, 0.25, 50, 100000, "SE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		var ib = Quiet(false, new DateTime(2024, 6, 3, 16, 0, 0));
		ib.RollFlattenBar = true;
		Check(ign.Manage(pi, ib).Reason == null, "Ignore does not flatten on the splice");
	}

	static ExitBar Quiet(bool lng, DateTime t)
	{
		var b = Bar(lng, 5000.25, lng ? 4990 : 5010, 5000);
		b.High = lng ? 5000.25 : 5000;
		b.Low = lng ? 5000 : 4999.75;
		b.Close = 5000;
		b.TimeEt = t;
		b.MinutesToNews = double.NaN;
		return b;
	}

	static void Control()
	{
		Check(Map(1, EGxBarKind.Minute, false) == 30, "1 min => 30");
		Check(Map(5, EGxBarKind.Minute, false) == 12, "5 min => 12");
		Check(Map(15, EGxBarKind.Minute, false) == 8, "15 min => 8");
		Check(Map(60, EGxBarKind.Minute, false) == 6, "60 min => 6");
		var day = Mkt(); day.BarKind = EGxBarKind.Day; day.BarMinutes = 1440; day.DailyOrLonger = true;
		string tf; bool warn;
		Check(ExitSession.ControlBars(new ExitConfig(), day, out tf, out warn) == 5 && tf == "D1" && !warn, "daily => 5");
		Check(Map(2, EGxBarKind.Minute, false) == 30, "2 min => 30");
		Check(Map(10, EGxBarKind.Minute, false) == 12, "10 min => 12");
		Check(Map(30, EGxBarKind.Minute, false) == 8, "30 min => 8");
		Check(Map(240, EGxBarKind.Minute, false) == 6, "240 min => 6");
		var week = Mkt(); week.BarKind = EGxBarKind.Week; week.DailyOrLonger = true;
		Check(ExitSession.ControlBars(new ExitConfig(), week, out tf, out warn) == 5 && tf == "D1", "weekly => 5");
		var sec = Mkt(); sec.BarKind = EGxBarKind.Second; sec.ResolutionLabel = "10 sec";
		var tick = Mkt(); tick.BarKind = EGxBarKind.Tick; tick.ResolutionLabel = "100 tick";
		var range = Mkt(); range.BarKind = EGxBarKind.Range; range.ResolutionLabel = "4 range";
		var es = Eng(null, sec);
		Check(es.MappedBars == 30 && Has(es.Log, "Warning: CONTROL-BARS no listed timeframe at or below 10 sec: using ControlExitBarsM1"), "seconds => 30 + Warning");
		Check(Eng(null, tick).MappedBars == 30 && Eng(null, range).MappedBars == 30, "tick and range => 30");
		var over = new ExitConfig(); over.ControlExitBarsM5 = 20;
		var m5 = Mkt(); m5.BarMinutes = 5;
		Check(ExitSession.ControlBars(over, m5, out tf, out warn) == 20 && tf == "M5", "ControlExitBarsM5 override wins");
		var m10 = Mkt(); m10.BarMinutes = 10;
		Check(ExitSession.ControlBars(over, m10, out tf, out warn) == 20, "10 min uses the 5 min override");
		var zero = new ExitConfig(); zero.ControlExitBarsM1 = 0;
		var m1 = Mkt();
		Check(ExitSession.ControlBars(zero, m1, out tf, out warn) == 0, "0 turns that timeframe off");

		foreach (bool lng in new[] { true, false })
		{
			var e = Eng(c => { c.ExitControl = EExitControl.FixedBars; });
			e.MappedBars = 3;
			var p = new ExitPosition();
			e.Open(p, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			p.ControlN = 3;
			Check(e.Manage(p, Quiet(lng, new DateTime(2024, 6, 3, 10, 1, 0))).Reason == null, (lng ? "L" : "S") + " FixedBars holds before N");
			Check(e.Manage(p, Quiet(lng, new DateTime(2024, 6, 3, 10, 2, 0))).Reason == null, (lng ? "L" : "S") + " FixedBars holds at N-1");
			var both = Quiet(lng, new DateTime(2024, 6, 3, 10, 3, 0));
			both.Low = lng ? 4800 : 4990;
			both.High = lng ? 5010 : 5200;
			Check(e.Manage(p, both).Reason == "STOP", (lng ? "L" : "S") + " disaster stop still beats FixedBars");

			var e2 = Eng(c => c.ExitControl = EExitControl.FixedBars);
			var p2 = new ExitPosition();
			e2.Open(p2, lng, 5000, 5000, lng ? 4900 : 5100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			p2.ControlN = 2;
			e2.Manage(p2, Quiet(lng, new DateTime(2024, 6, 3, 10, 1, 0)));
			Check(e2.Manage(p2, Quiet(lng, new DateTime(2024, 6, 3, 10, 2, 0))).Reason == "CONTROL", (lng ? "L" : "S") + " FixedBars exits after exactly N closed bars");
		}
		int a = ExitSession.RandomHold(12345, 12, 0);
		int b = ExitSession.RandomHold(12345, 12, 0);
		int other = ExitSession.RandomHold(12345, 12, 1);
		Check(a == b && a >= 1 && a <= 12, "RandomBars repeats for a seed");
		Check(a != other || ExitSession.RandomHold(12345, 12, 2) != a, "RandomBars moves with the trade index");
		var er = Eng(c => c.ExitControl = EExitControl.RandomBars);
		var pr = new ExitPosition();
		er.MappedBars = 12;
		er.Open(pr, true, 5000, 5000, 4900, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		var pr2 = new ExitPosition();
		er.Open(pr2, true, 5000, 5000, 4900, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		Check(pr.RandomHold == pr2.RandomHold && pr.RandomHold == a, "RandomBars on the engine matches the seed");
		Check(Has(er.Log, "CONTROL-BARS tf(1 min) mapped(M1) bars(30)"), "CONTROL-BARS line");
	}

	static int Map(int minutes, EGxBarKind kind, bool daily)
	{
		var m = Mkt(); m.BarMinutes = minutes; m.BarKind = kind; m.DailyOrLonger = daily;
		string tf; bool warn;
		return ExitSession.ControlBars(new ExitConfig(), m, out tf, out warn);
	}

	static void Idx()
	{
		Check((int)EInitialStop.EntryProvided == 0 && (int)EInitialStop.AtrMultiple == 1 && (int)EInitialStop.FixedTicks == 2 && (int)EInitialStop.SignalBarExtreme == 3 && (int)EInitialStop.SwingExtreme == 4 && (int)EInitialStop.Percent == 5, "EInitialStop order");
		Check((int)ETriggerUnit.RMultiple == 0 && (int)ETriggerUnit.PercentOfEntry == 1 && (int)ETriggerUnit.Ticks == 2 && (int)ETriggerUnit.Points == 3 && (int)ETriggerUnit.CurrencyPerContract == 4, "ETriggerUnit order");
		Check((int)EBreakEven.AfterT1 == 0 && (int)EBreakEven.AtTrigger == 1 && (int)EBreakEven.Off == 2, "EBreakEven order");
		Check((int)ETrailMode.Chandelier == 0 && (int)ETrailMode.Atr == 1 && (int)ETrailMode.BarExtreme == 2 && (int)ETrailMode.Ema == 3 && (int)ETrailMode.RollLevel == 4 && (int)ETrailMode.None == 5, "ETrailMode order");
		Check((int)ETrailStart.AfterT1 == 0 && (int)ETrailStart.AtTrigger == 1 && (int)ETrailStart.Immediately == 2, "ETrailStart order");

		var keep = Eng(c => c.T1Unit = ETriggerUnit.Ticks);
		Check(keep.Effective.T1Unit == ETriggerUnit.Ticks, "idx -1 keeps the enum");
		for (int i = 0; i < ExitEffective.InitialStops.Length; i++)
		{
			int n = i;
			var e = Eng(c => { c.Embedded = true; c.InitialStopIdx = n; c.StopTicks = 20; c.StopPct = 1; });
			Check(e.Effective.InitialStop == ExitEffective.InitialStops[n], "InitialStopIdx " + n);
		}
		for (int i = 0; i < ExitEffective.Units.Length; i++)
		{
			int n = i;
			var e = Eng(c => c.T1UnitIdx = n);
			Check(e.Effective.T1Unit == ExitEffective.Units[n], "T1UnitIdx " + n);
		}
		var lo = Eng(c => c.TrailModeIdx = -2);
		Check(lo.Effective.TrailMode == ETrailMode.Chandelier && Has(lo.Log, "Error: GX IDX-RANGE TrailModeIdx(-2) valid(-1..5) using TrailMode(Chandelier)"), "idx -2 uses the enum");
		var hi = Eng(c => c.BreakEvenIdx = 3);
		Check(hi.Effective.BreakEven == EBreakEven.AfterT1 && Has(hi.Log, "Error: GX IDX-RANGE BreakEvenIdx(3) valid(-1..2) using BreakEven(AfterT1)"), "idx N uses the enum");
		var badStop = Eng(c => c.InitialStopIdx = 0);
		Check(badStop.Effective.InitialStop == EInitialStop.AtrMultiple && Has(badStop.Log, "INITIAL-STOP invalid(EntryProvided"), "idx 0 EntryProvided in the wrapper falls back");

		string start = badStop.Log.First(s => s.Contains("GX START"));
		foreach (string name in new[] { "InitialStop", "T1Unit", "FinalTargetUnit", "BreakEven", "BreakEvenUnit", "TrailMode", "TrailStart", "TrailStartUnit" })
			Check(start.Contains(name + "(") && start.Contains(name + "Idx(") && start.Contains(name + "Eff("), "START prints " + name);
		Check(start.Contains("seed(12345)"), "START prints the seed");

		var swept = Eng(c => { c.T1Unit = ETriggerUnit.RMultiple; c.T1UnitIdx = 2; });
		var named = Eng(c => { c.T1Unit = ETriggerUnit.Ticks; c.T1UnitIdx = -1; });
		Check(swept.Effective.T1Unit == ETriggerUnit.Ticks && swept.Package == named.Package, "gxPackage matches a swept run and an enum-set run");
	}

	static void RAccount()
	{
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng(c => c.AddStopMode = EAddStopMode.KeepCurrent);
			var p = new ExitPosition();
			double fill = 100, stop = lng ? 96 : 104;
			e.Open(p, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(Near(p.InitialRDollars, 400) && Near(p.InitialRPrice, 4), (lng ? "L" : "S") + " initial R from the widened stop");
			double mark = lng ? 104 : 96;
			Check(Near(e.R(p, mark, 50), 1) && Near(e.ROrig(p, lng ? 400 : 400), 1), (lng ? "L" : "S") + " R and Rorig at +1R");

			double close = lng ? 104 : 96;
			double add = lng ? 105 : 95;
			double extreme = lng ? 103 : 97;
			e.OnAdd(p, add, 1, extreme, close, 50, 0.25);
			Check(p.AddCount == 1 && Near(p.ClosedSegmentR, 1), (lng ? "L" : "S") + " first segment closes at +1R");
			Check(Near(p.Stop, stop), (lng ? "L" : "S") + " KeepCurrent leaves the stop");
			double oneR = Math.Abs(close - stop) * 50 * 3;
			Check(Near(p.SegmentOneR, oneR), (lng ? "L" : "S") + " AddMinR uses the current segment 1R");
			Check(Near(p.InitialRPrice, 4), (lng ? "L" : "S") + " triggers stay on the initial R");

			var raised = Eng();
			var pr = new ExitPosition();
			raised.Open(pr, lng, fill, fill, stop, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			raised.OnAdd(pr, add, 1, extreme, close, 50, 0.25);
			double want = lng ? 103 - 0.25 : 97 + 0.25;
			Check(Near(pr.Stop, want), (lng ? "L" : "S") + " RaiseToAddLow tightens to the add extreme ± 1 tick");
			Check(Near(pr.SegmentOneR, Math.Abs(close - want) * 50 * 3), (lng ? "L" : "S") + " new segment 1R at the raised stop");
			var loose = Eng();
			var pl = new ExitPosition();
			loose.Open(pl, lng, fill, fill, lng ? 102 : 98, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			loose.OnAdd(pl, add, 1, lng ? 90 : 110, close, 50, 0.25);
			Check(Near(pl.Stop, lng ? 102 : 98), (lng ? "L" : "S") + " an add does not loosen the stop");
		}
	}

	static void RiskFields()
	{
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng();
			var p = new ExitPosition();
			e.Open(p, lng, 100, 100, lng ? 96 : 104, 2, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			var bar = Quiet(lng, new DateTime(2024, 6, 3, 10, 1, 0));
			bar.Low = lng ? 99 : 99;
			bar.High = lng ? 101 : 101;
			bar.Close = 100;
			e.Manage(p, bar);
			Check(Near(p.MaeTradeDollars, 100) && Near(p.MaeOpenDollars, 100), (lng ? "L" : "S") + " MAE 1 point on 2 contracts");
			e.ScaleOut(p, 1, lng ? 102 : 98, 50);
			var later = Quiet(lng, new DateTime(2024, 6, 3, 10, 2, 0));
			later.Low = lng ? 98 : 98;
			later.High = lng ? 102 : 102;
			later.Close = lng ? 99 : 101;
			e.Manage(p, later);
			Check(Near(p.MaeTradeDollars, 100), (lng ? "L" : "S") + " locked T1 profit offsets a later adverse mark");
			Check(p.MaeOpenDollars >= 100, (lng ? "L" : "S") + " open-only MAE ignores the partial");
			var exit = Quiet(lng, new DateTime(2024, 6, 3, 10, 3, 0));
			exit.IsExitBar = true;
			exit.ExitPrice = lng ? 99 : 101;
			exit.Low = lng ? 90 : 98;
			exit.High = lng ? 102 : 110;
			double before = p.MaeTradeDollars;
			e.MarkMae(p, lng ? 90 : 110, 50, true, exit.ExitPrice);
			Check(Near(p.MaeTradeDollars, before), (lng ? "L" : "S") + " exit bar is capped at the exit price");

			double plannedPct = ExitSession.PlannedRiskPct(p.InitialRDollars, p.EquityAtEntry);
			Check(Near(plannedPct, 0.4), (lng ? "L" : "S") + " plannedRiskPct 0.400");
			string line = e.CloseLine(p, "T1", lng ? 99 : 101, 50, 50, "bar");
			Check(line.Contains("plannedRiskPct=0.400") && line.Contains("plannedRiskPctPeak=0.400") && line.Contains("mae=$100.00") && line.Contains("maeScope=bar"), (lng ? "L" : "S") + " close line rounds at write time");
			Check(line.StartsWith("EXIT CLOSE "), "chart prefix");
		}
		var none = Eng();
		var pn = new ExitPosition();
		none.Open(pn, true, 100, 100, 100, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		pn.InitialRDollars = 0; pn.PeakRiskDollars = 0;
		string empty = none.CloseLine(pn, "STOP", 100, 50, 0, "bar");
		Check(empty.Contains("plannedRiskPct=") && empty.Contains("actualToPlanned=") && empty.Contains("riskFlag=NO-PLANNED-RISK"), "no planned risk leaves the percents empty");
		pn.EquityAtEntry = 0; pn.InitialRDollars = 100; pn.RiskFlag = "";
		string noEq = none.CloseLine(pn, "STOP", 100, 50, 10, "magnifier");
		Check(noEq.Contains("plannedRiskPct= plannedRiskPctPeak=") && noEq.Contains("realizedPnlPct=") && noEq.Contains("maeScope=magnifier"), "equity <= 0 leaves every percent empty");
		var pt = Eng(c => c.InPortfolio = true);
		var pp = new ExitPosition();
		pt.Open(pp, false, 100, 100, 104, 1, 0.25, 50, 50000, "SE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
		Check(pt.CloseLine(pp, "STOP", 100, 50, -200, "bar").StartsWith("PORT CLOSE "), "PT close prefix");
	}

	static void OverBudget()
	{
		foreach (bool lng in new[] { true, false })
		{
			var e = Eng();
			var p = new ExitPosition();
			e.Open(p, lng, 100, 100, lng ? 96 : 104, 2, 0.25, 50, 10000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(p.RiskFlag == "OVER-BUDGET" && Has(e.Log, "EXIT Warning: GX RISK-OVER-BUDGET " + e.Config.Id + " side=" + (lng ? "long" : "short")), (lng ? "L" : "S") + " over-budget warning");
			int n = e.Log.Count(s => s.Contains("RISK-OVER-BUDGET"));
			e.WarnOverBudget(p, 50);
			Check(e.Log.Count(s => s.Contains("RISK-OVER-BUDGET")) == n, (lng ? "L" : "S") + " same fill time and order name is deduped");
			p.EntryOrder = "LE2";
			Check(e.WarnOverBudget(p, 50), (lng ? "L" : "S") + " a different order name warns again");
			var quiet = Eng();
			var ok = new ExitPosition();
			quiet.Open(ok, lng, 100, 100, lng ? 99 : 101, 1, 0.25, 50, 100000, "LE", new DateTime(2024, 6, 3, 10, 0, 0), 0);
			Check(ok.RiskFlag == "" && !Has(quiet.Log, "RISK-OVER-BUDGET"), (lng ? "L" : "S") + " inside the budget stays quiet and is still open");
			Check(ok.Open, (lng ? "L" : "S") + " over-budget rule does not close an in-budget trade");
		}
		var pt = Eng(c => c.InPortfolio = true);
		var p2 = new ExitPosition();
		pt.Open(p2, true, 100, 100, 96, 2, 0.25, 50, 10000, "LE", new DateTime(2024, 6, 3, 11, 0, 0), 0);
		Check(Has(pt.Log, "PORT Warning: GX RISK-OVER-BUDGET"), "PT prefix");
		Check(p2.Open, "the position is kept");
	}
}
