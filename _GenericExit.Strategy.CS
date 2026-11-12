using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// GX-1 (specs/generic-exit-manager-spec.md rev 1.1 §8.1). Pure C#: no MultiCharts types.
	// Trade management only. The engine never opens a position and never reverses one. It takes an existing position to flat.
	// T1 and the final target are planned from the planned entry and the planned R. T1-SKIPPED compares the actual fill with that planned T1.
	// Idx companions are resolved here. GX-3 puts the eight Idx Inputs on _GenericExit and on hosts (owner 2026-11-12 07:31 ET). GX-1 adds no [Input].

	public enum EInitialStop { EntryProvided, AtrMultiple, FixedTicks, SignalBarExtreme, SwingExtreme, Percent }
	public enum ETargetMode { ScaleOutThenRunner, SingleTarget, None }
	public enum ESingleLotMode { ExitAtT1, RunnerOnly }
	public enum ETriggerUnit { RMultiple, PercentOfEntry, Ticks, Points, CurrencyPerContract }
	public enum EBreakEven { AfterT1, AtTrigger, Off }
	public enum ETrailMode { Chandelier, Atr, BarExtreme, Ema, RollLevel, None }
	public enum ETrailStart { AfterT1, AtTrigger, Immediately }
	public enum ESessionExit { FlatBy, HoldOvernight }
	public enum ERollExit { FlattenBeforeSplice, Ignore }
	public enum EExitControl { None, FixedBars, RandomBars }
	public enum EAddStopMode { RaiseToAddLow, KeepCurrent }
	public enum EGxBarKind { Minute, Day, Week, Month, Second, Tick, Range }

	public sealed class ExitConfig
	{
		public string Id = "GX";
		public bool Embedded;
		public bool InPortfolio;
		public int ControlSeed = 12345;

		public EInitialStop InitialStop = EInitialStop.AtrMultiple;
		public double StopAtrMult = 1.5;
		public int StopAtrLength = 14;
		public int StopTicks;
		public int StopSwingBars = 5;
		public double StopPct;
		public bool FillBarDisasterStop = true;

		public ETargetMode TargetMode = ETargetMode.ScaleOutThenRunner;
		public ETriggerUnit T1Unit = ETriggerUnit.RMultiple;
		public double T1Value = 1.0;
		public int T1ScalePct = 50;
		public ETriggerUnit FinalTargetUnit = ETriggerUnit.RMultiple;
		public double FinalTargetValue;
		public ESingleLotMode SingleLotMode = ESingleLotMode.ExitAtT1;

		public EBreakEven BreakEven = EBreakEven.AfterT1;
		public ETriggerUnit BreakEvenUnit = ETriggerUnit.RMultiple;
		public double BreakEvenValue = 1.0;
		public int BreakEvenOffsetTicks = 1;

		public ETrailMode TrailMode = ETrailMode.Chandelier;
		public ETrailStart TrailStart = ETrailStart.AfterT1;
		public ETriggerUnit TrailStartUnit = ETriggerUnit.RMultiple;
		public double TrailStartValue = 1.0;
		public double TrailAtrMult = 3.0;
		public int TrailAtrLength = 22;
		public int TrailBars = 2;
		public int TrailEmaLength = 20;

		public int TimeStopBars;
		public int TimeStopMinutes;
		public ESessionExit SessionExit = ESessionExit.FlatBy;
		public int FlatByHHMM = 1555;
		public int MaxHoldDays;

		public int NewsExitMinutes;
		public ERollExit RollExit = ERollExit.FlattenBeforeSplice;
		public EAddStopMode AddStopMode = EAddStopMode.RaiseToAddLow;
		public EExitControl ExitControl = EExitControl.None;
		public int ControlExitBarsM1 = 30;
		public int ControlExitBarsM5 = 12;
		public int ControlExitBarsM15 = 8;
		public int ControlExitBarsH1 = 6;
		public int ControlExitBarsD1 = 5;

		public int InitialStopIdx = -1;
		public int T1UnitIdx = -1;
		public int FinalTargetUnitIdx = -1;
		public int BreakEvenIdx = -1;
		public int BreakEvenUnitIdx = -1;
		public int TrailModeIdx = -1;
		public int TrailStartIdx = -1;
		public int TrailStartUnitIdx = -1;
	}

	public struct ExitMarket
	{
		public double Tick;
		public double Bpv;
		public int FloorTicks;
		public double AtrStop;
		public double AtrTrail;
		public double Ema;
		public double SignalLow;
		public double SignalHigh;
		public double SwingLow;
		public double SwingHigh;
		public double EntryStop;
		public int BarMinutes;
		public EGxBarKind BarKind;
		public bool DailyOrLonger;
		public string ResolutionLabel;
	}

	public struct ExitLot
	{
		public double Fill;
		public int Qty;
	}

	public sealed class ExitPosition
	{
		public bool Open;
		public bool Long = true;
		public double PlannedEntry;
		public double Fill;
		public double Stop;
		public double InitialStop;
		public double T1Level = double.NaN;
		public double FinalLevel = double.NaN;
		public int Qty;
		public int OpenQty;
		public int T1Qty;
		public bool T1Skipped;
		public bool T1Filled;
		public bool FinalSkipped;
		public bool TrailOn;
		public bool BeDone;
		public bool GapThroughStop;
		public bool TrailThroughNext;
		public double Extreme;
		public bool ExtremeSet;
		public int ClosedBars;
		public DateTime FillTime = DateTime.MinValue;
		public string EntryOrder = "";
		public double DisasterDistance;
		public bool DisasterCalling;
		public double EquityAtEntry;
		public double MaeTradeDollars;
		public double MaeOpenDollars;
		public double RealizedDollars;
		public double SegmentRealized;
		public int AddCount;
		public double InitialRDollars;
		public double InitialRPrice;
		public double PeakRiskDollars;
		public int SegmentQty;
		public double SegmentOneR;
		public double ClosedSegmentR;
		public string RiskFlag = "";
		public string T1PlanSource = "";
		public double PlannedStop;
		public double PlannedRPrice;
		public int ControlN;
		public int RandomHold;
		public int TradeIndex;
		public readonly List<ExitLot> Lots = new List<ExitLot>();
	}

	public struct ExitBar
	{
		public double High;
		public double Low;
		public double Close;
		public double Tick;
		public double Bpv;
		public double AtrTrail;
		public double Ema;
		public double TrailLow;
		public double TrailHigh;
		public double PriorDayLow;
		public double PriorDayHigh;
		public bool HavePriorDay;
		public double EntryBarLow;
		public double EntryBarHigh;
		public DateTime TimeEt;
		public bool EarlyClose;
		public int RthCloseHHMM;
		public bool HolidayClosed;
		public double MinutesToNews;
		public int BarMinutes;
		public bool RollFlattenBar;
		public bool Invalidate;
		public bool ForceClose;
		public bool IsExitBar;
		public double ExitPrice;
	}

	public struct ExitDecision
	{
		public string Reason;
		public double Stop;
		public bool StopChanged;
		public bool SendT1;
		public int T1Qty;
		public bool TrailThrough;
	}

	/// <summary>One run of the exit state machine. Hosts send orders. This type decides levels, quantities and the one exit reason.</summary>
	public sealed class ExitSession
	{
		public ExitConfig Config = new ExitConfig();
		public readonly List<string> Log = new List<string>();
		public ExitEffective Effective;
		public string Package;
		public int FillBarStops;
		public bool PlanFallbackLogged;
		public string PlanSource = "";
		public bool T1On = true;
		public bool FinalOn;
		public bool BeUsesTrigger;
		public bool BeDisabled;
		public bool TrailUsesTrigger;
		public bool TrailTriggerDisabled;
		public int MappedBars;
		public string MappedTf;
		readonly HashSet<string> _budgetKeys = new HashSet<string>();

		public void Start(ExitMarket mkt)
		{
			Log.Clear();
			_budgetKeys.Clear();
			FillBarStops = 0;
			PlanFallbackLogged = false;
			Effective = ExitEffective.Resolve(Config, Log);
			string bad = InvalidReason(Effective.InitialStop, Config, Config.Embedded);
			if (bad != null)
			{
				Log.Add(Config.Id + " Error: GX INITIAL-STOP invalid(" + bad + ") using AtrMultiple");
				Effective.InitialStop = EInitialStop.AtrMultiple;
			}
			ApplyFeatureGates();
			NoteFallbacks();
			Package = ExitEffective.Hash(Effective, Config);
			bool warn;
			MappedBars = ControlBars(Config, mkt, out MappedTf, out warn);
			Log.Add(Config.Id + " Info: CONTROL-BARS tf(" + (mkt.ResolutionLabel ?? "") + ") mapped(" + MappedTf + ") bars(" + MappedBars.ToString(CultureInfo.InvariantCulture) + ")");
			if (warn)
				Log.Add(Config.Id + " Warning: CONTROL-BARS no listed timeframe at or below " + (mkt.ResolutionLabel ?? "") + ": using ControlExitBarsM1");
			if (Config.SessionExit == ESessionExit.FlatBy && (mkt.DailyOrLonger || mkt.BarKind == EGxBarKind.Day || mkt.BarKind == EGxBarKind.Week || mkt.BarKind == EGxBarKind.Month))
				Log.Add(Config.Id + " Warning: GX SESSION FlatBy ignored on " + (mkt.ResolutionLabel ?? "daily") + " bars: HoldOvernight");
			Log.Add(StartLine(Config, Effective, Package));
			if (!Config.Embedded)
			{
				if (Config.FillBarDisasterStop)
					Log.Add("EXIT Info: GX FILLBAR-PROTECTION mode(DisasterStop) builtin(GenerateStopLoss PerContract) handover(next bar after fill)");
				else
					Log.Add("EXIT Warning: GX FILLBAR-UNPROTECTED the wrapper's stop starts on the bar after the fill (E2); set FillBarDisasterStop = true or embed the engine");
			}
		}

		void ApplyFeatureGates()
		{
			T1On = Effective.TargetMode == ETargetMode.ScaleOutThenRunner && Config.T1ScalePct > 0;
			if (T1On && !(Config.T1Value > 0))
			{
				Log.Add(Config.Id + " Error: GX T1 value(" + Num(Config.T1Value) + ") <= 0: feature off");
				T1On = false;
			}
			FinalOn = Config.FinalTargetValue > 0 || Effective.TargetMode == ETargetMode.SingleTarget;
			if (Effective.TargetMode == ETargetMode.SingleTarget && !(Config.FinalTargetValue > 0))
			{
				Log.Add(Config.Id + " Error: GX FINAL value(" + Num(Config.FinalTargetValue) + ") <= 0: feature off");
				FinalOn = false;
			}
			else if (Config.FinalTargetValue < 0)
			{
				Log.Add(Config.Id + " Error: GX FINAL value(" + Num(Config.FinalTargetValue) + ") <= 0: feature off");
				FinalOn = false;
			}
			else if (!(Config.FinalTargetValue > 0)) FinalOn = false;

			bool beTrigger = Effective.BreakEven == EBreakEven.AtTrigger
				|| (Effective.BreakEven == EBreakEven.AfterT1 && !ScaleOutHasT1());
			BeUsesTrigger = beTrigger && Effective.BreakEven != EBreakEven.Off;
			BeDisabled = Effective.BreakEven == EBreakEven.Off;
			if (BeUsesTrigger && !(Config.BreakEvenValue > 0))
			{
				Log.Add(Config.Id + " Error: GX BE value(" + Num(Config.BreakEvenValue) + ") <= 0: feature off");
				BeUsesTrigger = false;
				BeDisabled = true;
			}
			bool trailTrigger = Effective.TrailStart == ETrailStart.AtTrigger
				|| (Effective.TrailStart == ETrailStart.AfterT1 && !ScaleOutHasT1());
			TrailUsesTrigger = trailTrigger;
			TrailTriggerDisabled = false;
			if (Effective.TrailMode == ETrailMode.None) TrailUsesTrigger = false;
			if (TrailUsesTrigger && !(Config.TrailStartValue > 0))
			{
				Log.Add(Config.Id + " Error: GX TRAIL value(" + Num(Config.TrailStartValue) + ") <= 0: feature off");
				TrailUsesTrigger = false;
				TrailTriggerDisabled = true;
			}
		}

		bool ScaleOutHasT1()
		{
			return Effective.TargetMode == ETargetMode.ScaleOutThenRunner && Config.T1ScalePct > 0 && Config.T1Value > 0;
		}

		void NoteFallbacks()
		{
			if (Effective.BreakEven == EBreakEven.AfterT1 && !ScaleOutHasT1())
				Log.Add(Config.Id + " Info: GX BE AfterT1 fallback(AtTrigger) unit(" + Effective.BreakEvenUnit + ") value(" + Num(Config.BreakEvenValue) + ")");
			if (Effective.TrailStart == ETrailStart.AfterT1 && Effective.TrailMode != ETrailMode.None && !ScaleOutHasT1())
				Log.Add(Config.Id + " Info: GX TRAIL AfterT1 fallback(AtTrigger) unit(" + Effective.TrailStartUnit + ") value(" + Num(Config.TrailStartValue) + ")");
		}

		public static string StartLine(ExitConfig c, ExitEffective e, string pkg)
		{
			return c.Id + " Info: GX START pkg(" + pkg + ") seed(" + c.ControlSeed.ToString(CultureInfo.InvariantCulture) + ")"
				+ Pair("InitialStop", c.InitialStop, c.InitialStopIdx, e.InitialStop)
				+ Pair("T1Unit", c.T1Unit, c.T1UnitIdx, e.T1Unit)
				+ Pair("FinalTargetUnit", c.FinalTargetUnit, c.FinalTargetUnitIdx, e.FinalTargetUnit)
				+ Pair("BreakEven", c.BreakEven, c.BreakEvenIdx, e.BreakEven)
				+ Pair("BreakEvenUnit", c.BreakEvenUnit, c.BreakEvenUnitIdx, e.BreakEvenUnit)
				+ Pair("TrailMode", c.TrailMode, c.TrailModeIdx, e.TrailMode)
				+ Pair("TrailStart", c.TrailStart, c.TrailStartIdx, e.TrailStart)
				+ Pair("TrailStartUnit", c.TrailStartUnit, c.TrailStartUnitIdx, e.TrailStartUnit);
		}

		static string Pair(string name, object input, int idx, object eff)
		{
			return " " + name + "(" + input + ") " + name + "Idx(" + idx.ToString(CultureInfo.InvariantCulture) + ") " + name + "Eff(" + eff + ")";
		}

		public static double Widen(double entry, double stop, double floorDistance)
		{
			double use = Math.Max(Math.Abs(entry - stop), floorDistance > 0 ? floorDistance : 0);
			return stop < entry ? entry - use : entry + use;
		}

		public static double FloorDistance(double tick, int floorTicks) { return floorTicks > 0 && tick > 0 ? floorTicks * tick : 0; }

		public static string InvalidReason(EInitialStop mode, ExitConfig c, bool entryProvidedOk)
		{
			if (mode == EInitialStop.FixedTicks && c.StopTicks <= 0) return "FixedTicks StopTicks=" + c.StopTicks.ToString(CultureInfo.InvariantCulture);
			if (mode == EInitialStop.Percent && !(c.StopPct > 0)) return "Percent StopPct=" + Num(c.StopPct);
			if (mode == EInitialStop.SwingExtreme && c.StopSwingBars <= 0) return "SwingExtreme StopSwingBars=" + c.StopSwingBars.ToString(CultureInfo.InvariantCulture);
			if (mode == EInitialStop.EntryProvided && !entryProvidedOk) return "EntryProvided EntryProvided=wrapper";
			return null;
		}

		public static double RawStop(EInitialStop mode, bool longSide, double entry, ExitMarket mkt, ExitConfig c)
		{
			double tick = mkt.Tick > 0 ? mkt.Tick : 0.25;
			if (mode == EInitialStop.EntryProvided) return mkt.EntryStop;
			if (mode == EInitialStop.FixedTicks) return longSide ? entry - c.StopTicks * tick : entry + c.StopTicks * tick;
			if (mode == EInitialStop.SignalBarExtreme) return longSide ? mkt.SignalLow - tick : mkt.SignalHigh + tick;
			if (mode == EInitialStop.SwingExtreme) return longSide ? mkt.SwingLow - tick : mkt.SwingHigh + tick;
			if (mode == EInitialStop.Percent) return longSide ? entry * (1.0 - c.StopPct / 100.0) : entry * (1.0 + c.StopPct / 100.0);
			return longSide ? entry - c.StopAtrMult * mkt.AtrStop : entry + c.StopAtrMult * mkt.AtrStop;
		}

		public double InitialStop(bool longSide, double entry, ExitMarket mkt)
		{
			EInitialStop mode = Effective.InitialStop;
			double raw = RawStop(mode, longSide, entry, mkt, Config);
			return Widen(entry, raw, FloorDistance(mkt.Tick, mkt.FloorTicks));
		}

		/// <summary>§4.12 price distance from a flat bar's close. Level modes use the larger of the two sides. Never below the cost floor.</summary>
		public double DisasterDistance(double close, ExitMarket mkt)
		{
			EInitialStop mode = Effective.InitialStop;
			if (InvalidReason(mode, Config, false) != null) mode = EInitialStop.AtrMultiple;
			double floor = FloorDistance(mkt.Tick, mkt.FloorTicks);
			double dist;
			if (mode == EInitialStop.SignalBarExtreme || mode == EInitialStop.SwingExtreme)
			{
				double lo = RawStop(mode, true, close, mkt, Config);
				double hi = RawStop(mode, false, close, mkt, Config);
				dist = Math.Max(Math.Abs(close - lo), Math.Abs(hi - close));
			}
			else
			{
				double stop = RawStop(mode, true, close, mkt, Config);
				dist = Math.Abs(close - stop);
			}
			if (dist < floor) dist = floor;
			return dist;
		}

		public double DisasterDollars(double close, ExitMarket mkt)
		{
			return DisasterDistance(close, mkt) * (mkt.Bpv > 0 ? mkt.Bpv : 0);
		}

		public bool DisasterThisBar(ExitPosition p, bool flat, double close, ExitMarket mkt)
		{
			if (Config.Embedded || !Config.FillBarDisasterStop) { p.DisasterCalling = false; return false; }
			if (!flat) { p.DisasterCalling = false; return false; }
			p.DisasterDistance = DisasterDistance(close, mkt);
			p.DisasterCalling = true;
			return true;
		}

		public static double HandoverStop(bool longSide, double fill, double priorDisasterDistance)
		{
			return longSide ? fill - priorDisasterDistance : fill + priorDisasterDistance;
		}

		public bool NoteFillBarStop(bool prevFlat, bool nowFlat, double prevNet, double nowNet, int bar)
		{
			if (!(prevFlat && nowFlat) || nowNet == prevNet) return false;
			FillBarStops++;
			Log.Add("EXIT Info: GX FILLBAR-STOP bar(" + bar.ToString(CultureInfo.InvariantCulture) + ") netChange($" + (nowNet - prevNet).ToString("0.00", CultureInfo.InvariantCulture) + ") - closed on its fill bar by the built-in disaster stop, not seen by the wrapper");
			return true;
		}

		public static double RoundAway(double entry, double level, double tick)
		{
			if (!(tick > 0) || double.IsNaN(level) || double.IsNaN(entry)) return level;
			double dist = Math.Abs(level - entry);
			if (dist < tick * 1e-9) return entry;
			double steps = dist / tick;
			double whole = Math.Round(steps);
			if (Math.Abs(steps - whole) < 1e-8) return entry + Math.Sign(level - entry) * whole * tick;
			double away = Math.Ceiling(steps - 1e-12);
			return entry + Math.Sign(level - entry) * away * tick;
		}

		public static double TriggerDistance(ETriggerUnit unit, double value, double entry, double initialRPrice, double tick, double bpv, out string error)
		{
			error = null;
			if (!(value > 0)) { error = "value<=0"; return double.NaN; }
			double d;
			if (unit == ETriggerUnit.RMultiple) d = value * initialRPrice;
			else if (unit == ETriggerUnit.PercentOfEntry) d = Math.Abs(entry) * value / 100.0;
			else if (unit == ETriggerUnit.Ticks) d = value * tick;
			else if (unit == ETriggerUnit.Points) d = value;
			else d = bpv > 0 ? value / bpv : double.NaN;
			if (double.IsNaN(d) || d <= 0) { error = "value<=0"; return double.NaN; }
			return d;
		}

		public static double TriggerLevel(bool longSide, double entry, double distance, double tick)
		{
			double raw = longSide ? entry + distance : entry - distance;
			return RoundAway(entry, raw, tick);
		}

		public static int T1Qty(int q, int scalePct, ESingleLotMode single, ETargetMode mode)
		{
			if (q <= 0 || mode == ETargetMode.None) return 0;
			if (mode == ETargetMode.SingleTarget) return q;
			if (scalePct <= 0) return 0;
			if (q == 1) return single == ESingleLotMode.ExitAtT1 ? 1 : 0;
			if (scalePct >= 100) return q;
			int raw = (int)Math.Floor(q * (scalePct / 100.0));
			if (raw < 1) raw = 1;
			if (raw > q - 1) raw = q - 1;
			return raw;
		}

		/// <summary>T1-SKIPPED: the actual fill is at or beyond the planned T1 (long: fill ≥ t1; short: fill ≤ t1).</summary>
		public static bool SkipT1(bool longSide, double fill, double t1Level)
		{
			if (double.IsNaN(t1Level)) return false;
			return longSide ? fill >= t1Level : fill <= t1Level;
		}

		public double PlanTrigger(bool longSide, ETriggerUnit unit, double value, double anchor, double initialRPrice, double tick, double bpv, out string error)
		{
			double d = TriggerDistance(unit, value, anchor, initialRPrice, tick, bpv, out error);
			if (double.IsNaN(d)) return double.NaN;
			return TriggerLevel(longSide, anchor, d, tick);
		}

		public void Open(ExitPosition p, bool longSide, double plannedEntry, double fill, double initialStop, int qty, double tick, double bpv, double equity, string orderName, DateTime fillTime, int tradeIndex)
		{
			Open(p, longSide, plannedEntry, initialStop, fill, initialStop, qty, tick, bpv, equity, orderName, fillTime, tradeIndex);
		}

		/// <summary>plannedStop is the planned widened stop. T1 and the final target use |plannedEntry − plannedStop|. The live stop and BE/trail R use the fill and initialStop.</summary>
		public void Open(ExitPosition p, bool longSide, double plannedEntry, double plannedStop, double fill, double initialStop, int qty, double tick, double bpv, double equity, string orderName, DateTime fillTime, int tradeIndex)
		{
			p.Open = true; p.Long = longSide; p.PlannedEntry = plannedEntry; p.PlannedStop = plannedStop; p.Fill = fill;
			p.Stop = initialStop; p.InitialStop = initialStop; p.Qty = qty; p.OpenQty = qty;
			p.FillTime = fillTime; p.EntryOrder = orderName ?? ""; p.ClosedBars = 0; p.TradeIndex = tradeIndex;
			p.Extreme = longSide ? fill : fill; p.ExtremeSet = true; p.EquityAtEntry = equity;
			p.DisasterCalling = false; p.BeDone = false; p.T1Filled = false; p.TrailOn = false; p.T1Skipped = false; p.FinalSkipped = false;
			p.GapThroughStop = longSide ? fill <= initialStop : fill >= initialStop;
			p.TrailThroughNext = false; p.AddCount = 0; p.RealizedDollars = 0; p.SegmentRealized = 0; p.ClosedSegmentR = 0;
			p.MaeTradeDollars = 0; p.MaeOpenDollars = 0; p.RiskFlag = "";
			p.Lots.Clear(); p.Lots.Add(new ExitLot { Fill = fill, Qty = qty });
			p.SegmentQty = qty;
			p.PlannedRPrice = Math.Abs(plannedEntry - plannedStop);
			p.InitialRPrice = Math.Abs(fill - initialStop);
			p.InitialRDollars = p.InitialRPrice * bpv * qty;
			p.SegmentOneR = p.InitialRDollars;
			p.PeakRiskDollars = p.InitialRDollars;
			p.ControlN = MappedBars;
			p.RandomHold = Config.ExitControl == EExitControl.RandomBars ? RandomHold(Config.ControlSeed, MappedBars, tradeIndex) : MappedBars;
			string err;
			p.T1Level = double.NaN; p.T1Qty = 0;
			if (T1On)
			{
				p.T1Level = PlanTrigger(longSide, Effective.T1Unit, Config.T1Value, plannedEntry, p.PlannedRPrice, tick, bpv, out err);
				if (!double.IsNaN(p.T1Level) && SkipT1(longSide, fill, p.T1Level))
				{
					p.T1Skipped = true; p.TrailOn = true; p.T1Qty = 0;
					Log.Add(T1SkippedLine(longSide, fill, p.T1Level, tick, qty));
				}
				else p.T1Qty = T1Qty(qty, Config.T1ScalePct, Effective.SingleLotMode, Effective.TargetMode);
			}
			p.FinalLevel = double.NaN;
			if (FinalOn)
			{
				p.FinalLevel = PlanTrigger(longSide, Effective.FinalTargetUnit, Config.FinalTargetValue, plannedEntry, p.PlannedRPrice, tick, bpv, out err);
				if (Effective.TargetMode == ETargetMode.SingleTarget && !double.IsNaN(p.FinalLevel) && SkipT1(longSide, fill, p.FinalLevel))
				{
					p.FinalSkipped = true; p.T1Skipped = true; p.TrailOn = true;
					Log.Add(T1SkippedLine(longSide, fill, p.FinalLevel, tick, qty));
				}
			}
			if (Effective.TrailStart == ETrailStart.Immediately && Effective.TrailMode != ETrailMode.None) p.TrailOn = true;
			WarnOverBudget(p, bpv);
		}

		/// <summary>Wrapper plan keys. Valid ⇒ source plan. Otherwise source fill, and the Info line once per run.</summary>
		public bool NotePlan(bool longSide, bool hasEntry, bool hasStop, double entry, double stop, int writtenBar, int fillBar, out double planEntry, out double planStop)
		{
			if (_ExitEngine.PlanKeysValid(longSide, hasEntry, hasStop, entry, stop, writtenBar, fillBar))
			{
				planEntry = entry; planStop = stop; PlanSource = "plan";
				return true;
			}
			planEntry = double.NaN; planStop = double.NaN; PlanSource = "fill";
			if (!PlanFallbackLogged)
			{
				PlanFallbackLogged = true;
				Log.Add(_ExitEngine.PlanFillInfo);
			}
			return false;
		}

		public string BudgetKey(ExitPosition p)
		{
			return Config.Id + "|" + p.FillTime.ToString("o", CultureInfo.InvariantCulture) + "|" + (p.EntryOrder ?? "");
		}

		public bool WarnOverBudget(ExitPosition p, double bpv)
		{
			double risk = Math.Abs(p.Fill - p.InitialStop) * bpv * Math.Abs(p.Qty);
			if (!(risk > 0.01 * p.EquityAtEntry)) return false;
			if (!_budgetKeys.Add(BudgetKey(p))) return false;
			p.RiskFlag = "OVER-BUDGET";
			string line = "GX RISK-OVER-BUDGET " + Config.Id + " side=" + (p.Long ? "long" : "short") + " qty=" + p.Qty.ToString(CultureInfo.InvariantCulture)
				+ " fill=" + Px(p.Fill) + " stop=" + Px(p.InitialStop) + " risk=$" + Money(risk)
				+ " budget=$" + Money(0.01 * p.EquityAtEntry) + " (1% of equity $" + Money(p.EquityAtEntry) + ") - position kept and managed normally";
			Log.Add((Config.InPortfolio ? "PORT Warning: " : "EXIT Warning: ") + line);
			return true;
		}

		public string T1SkippedLine(bool longSide, double fill, double t1, double tick, int qty)
		{
			double past = tick > 0 ? Math.Abs(fill - t1) / tick : 0;
			return Config.Id + " Info: T1-SKIPPED side(" + (longSide ? "+1" : "-1") + ") fill(" + Px(fill) + ") t1(" + Px(t1) + ") past(" + past.ToString("0", CultureInfo.InvariantCulture) + ") qty(" + qty.ToString(CultureInfo.InvariantCulture) + ") action(trail-all)";
		}

		/// <summary>Apply a caller-supplied T1 price. This is the §9 one-argument check.</summary>
		public bool ApplySuppliedT1(ExitPosition p, double t1Level, double tick)
		{
			p.T1Level = t1Level;
			if (!SkipT1(p.Long, p.Fill, t1Level)) return false;
			p.T1Skipped = true; p.T1Qty = 0; p.TrailOn = true;
			Log.Add(T1SkippedLine(p.Long, p.Fill, t1Level, tick, p.Qty));
			return true;
		}

		public static string Precedence(bool forceClose, bool stop, bool target, bool news, bool session, bool roll, bool invalidate, bool time, bool control, string targetReason)
		{
			if (forceClose) return "FLAT-BY-MM";
			if (stop) return "STOP";
			if (target) return string.IsNullOrEmpty(targetReason) ? "T1" : targetReason;
			if (news) return "NEWS";
			if (session) return "FLATBY";
			if (roll) return "ROLL";
			if (invalidate) return "INVALIDATE";
			if (time) return "TIME";
			if (control) return "CONTROL";
			return null;
		}

		public static int ControlBars(ExitConfig c, ExitMarket mkt, out string mapped, out bool warn)
		{
			warn = false;
			mapped = "M1";
			if (mkt.BarKind == EGxBarKind.Second || mkt.BarKind == EGxBarKind.Tick || mkt.BarKind == EGxBarKind.Range)
			{
				warn = true; mapped = "M1"; return c.ControlExitBarsM1;
			}
			if (mkt.BarKind == EGxBarKind.Week || mkt.BarKind == EGxBarKind.Month) { mapped = "D1"; return c.ControlExitBarsD1; }
			if (mkt.BarKind == EGxBarKind.Day || mkt.DailyOrLonger) { mapped = "D1"; return c.ControlExitBarsD1; }
			int s = mkt.BarMinutes;
			if (s < 5) { mapped = "M1"; return c.ControlExitBarsM1; }
			if (s < 15) { mapped = "M5"; return c.ControlExitBarsM5; }
			if (s < 60) { mapped = "M15"; return c.ControlExitBarsM15; }
			if (s < 1440) { mapped = "H1"; return c.ControlExitBarsH1; }
			mapped = "D1"; return c.ControlExitBarsD1;
		}

		public static int RandomHold(int seed, int n, int tradeIndex)
		{
			if (n <= 1) return n <= 0 ? 0 : 1;
			uint s = unchecked((uint)seed);
			for (int i = 0; i <= tradeIndex; i++) s = unchecked(s * 1664525u + 1013904223u);
			return (int)(s % (uint)n) + 1;
		}

		public static int Hhmm(DateTime t) { return t.Hour * 100 + t.Minute; }

		public static int MinusMinutes(int hhmm, int minutes)
		{
			int total = (hhmm / 100) * 60 + (hhmm % 100) - minutes;
			if (total < 0) total = 0;
			return (total / 60) * 100 + (total % 60);
		}

		public static bool NewsWindow(double minutesTo, int newsMinutes, int barMinutes)
		{
			if (newsMinutes <= 0 || double.IsNaN(minutesTo)) return false;
			return minutesTo > 0 && minutesTo < newsMinutes + barMinutes;
		}

		public bool SessionFlatten(ExitBar bar)
		{
			if (Config.SessionExit != ESessionExit.FlatBy) return false;
			if (bar.HolidayClosed) return false;
			if (MappedTf == "D1") return false;
			int cut = Config.FlatByHHMM;
			if (bar.EarlyClose && bar.RthCloseHHMM > 0) cut = MinusMinutes(bar.RthCloseHHMM, 5);
			return Hhmm(bar.TimeEt) >= cut;
		}

		public bool TimeHit(ExitPosition p, DateTime barEt)
		{
			if (Config.TimeStopBars > 0 && p.ClosedBars >= Config.TimeStopBars) return true;
			if (Config.TimeStopMinutes > 0 && p.FillTime != DateTime.MinValue)
			{
				double mins = (barEt - p.FillTime).TotalMinutes;
				if (mins >= Config.TimeStopMinutes) return true;
			}
			if (Config.MaxHoldDays > 0 && p.FillTime != DateTime.MinValue)
			{
				if ((barEt.Date - p.FillTime.Date).TotalDays >= Config.MaxHoldDays) return true;
			}
			return false;
		}

		public bool ControlHit(ExitPosition p)
		{
			if (Config.ExitControl == EExitControl.None || p.ControlN <= 0) return false;
			int n = Config.ExitControl == EExitControl.RandomBars ? p.RandomHold : p.ControlN;
			return n > 0 && p.ClosedBars >= n;
		}

		public static double Tighten(bool longSide, double oldStop, double candidate, double close, double tick, out bool through)
		{
			through = false;
			if (longSide)
			{
				if (candidate >= close) { through = true; return oldStop; }
				double capped = Math.Min(candidate, close - tick);
				return Math.Max(oldStop, capped);
			}
			if (candidate <= close) { through = true; return oldStop; }
			double cappedS = Math.Max(candidate, close + tick);
			return Math.Min(oldStop, cappedS);
		}

		public static double BreakEvenStop(bool longSide, double entry, int offsetTicks, double tick)
		{
			return longSide ? entry + offsetTicks * tick : entry - offsetTicks * tick;
		}

		public double TrailCandidate(ExitPosition p, ExitBar bar)
		{
			double tick = bar.Tick;
			if (Effective.TrailMode == ETrailMode.Chandelier)
				return p.Long ? p.Extreme - Config.TrailAtrMult * bar.AtrTrail : p.Extreme + Config.TrailAtrMult * bar.AtrTrail;
			if (Effective.TrailMode == ETrailMode.Atr)
				return p.Long ? bar.Close - Config.TrailAtrMult * bar.AtrTrail : bar.Close + Config.TrailAtrMult * bar.AtrTrail;
			if (Effective.TrailMode == ETrailMode.BarExtreme)
				return p.Long ? bar.TrailLow - tick : bar.TrailHigh + tick;
			if (Effective.TrailMode == ETrailMode.Ema)
				return p.Long ? bar.Ema - tick : bar.Ema + tick;
			if (Effective.TrailMode == ETrailMode.RollLevel)
			{
				if (bar.HavePriorDay) return p.Long ? bar.PriorDayLow - tick : bar.PriorDayHigh + tick;
				return p.Long ? bar.EntryBarLow - tick : bar.EntryBarHigh + tick;
			}
			return p.Stop;
		}

		public double OpenPnl(ExitPosition p, double mark, double bpv)
		{
			double pnl = 0;
			for (int i = 0; i < p.Lots.Count; i++)
			{
				ExitLot lot = p.Lots[i];
				pnl += (p.Long ? mark - lot.Fill : lot.Fill - mark) * bpv * lot.Qty;
			}
			return pnl;
		}

		public void MarkMae(ExitPosition p, double adverse, double bpv, bool capAtExit, double exitPrice)
		{
			double mark = capAtExit ? exitPrice : adverse;
			double trade = p.RealizedDollars + OpenPnl(p, mark, bpv);
			double adverseDl = trade < 0 ? -trade : 0;
			if (adverseDl > p.MaeTradeDollars) p.MaeTradeDollars = adverseDl;
			double openOnly = OpenPnl(p, mark, bpv);
			double openAdv = openOnly < 0 ? -openOnly : 0;
			if (openAdv > p.MaeOpenDollars) p.MaeOpenDollars = openAdv;
		}

		public void ScaleOut(ExitPosition p, int qtyOut, double price, double bpv)
		{
			if (qtyOut > p.OpenQty) qtyOut = p.OpenQty;
			int left = qtyOut;
			for (int i = 0; i < p.Lots.Count && left > 0; i++)
			{
				ExitLot lot = p.Lots[i];
				int take = lot.Qty < left ? lot.Qty : left;
				double pnl = (p.Long ? price - lot.Fill : lot.Fill - price) * bpv * take;
				p.RealizedDollars += pnl;
				p.SegmentRealized += pnl;
				lot.Qty -= take;
				p.Lots[i] = lot;
				left -= take;
			}
			for (int i = p.Lots.Count - 1; i >= 0; i--) if (p.Lots[i].Qty <= 0) p.Lots.RemoveAt(i);
			p.OpenQty -= qtyOut;
			p.T1Filled = true;
			if (p.OpenQty <= 0) p.Open = false;
		}

		public double R(ExitPosition p, double mark, double bpv)
		{
			double seg = p.SegmentRealized + OpenPnl(p, mark, bpv);
			double cur = p.SegmentOneR > 0 ? seg / p.SegmentOneR : 0;
			return p.ClosedSegmentR + cur;
		}

		public double ROrig(ExitPosition p, double totalPnl)
		{
			return p.InitialRDollars > 0 ? totalPnl / p.InitialRDollars : double.NaN;
		}

		public double CurrentSegmentOneR(ExitPosition p) { return p.SegmentOneR; }

		public double OnAdd(ExitPosition p, double addPrice, int addQty, double addExtreme, double close, double bpv, double tick)
		{
			double open = OpenPnl(p, close, bpv);
			double seg = p.SegmentRealized + open;
			if (p.SegmentOneR > 0) p.ClosedSegmentR += seg / p.SegmentOneR;
			p.SegmentRealized = 0;
			p.AddCount++;
			p.Lots.Add(new ExitLot { Fill = addPrice, Qty = addQty });
			p.OpenQty += addQty;
			p.SegmentQty = p.OpenQty;
			if (Config.AddStopMode == EAddStopMode.RaiseToAddLow)
			{
				double candidate = p.Long ? addExtreme - tick : addExtreme + tick;
				bool through;
				p.Stop = Tighten(p.Long, p.Stop, candidate, close, tick, out through);
			}
			p.SegmentOneR = Math.Abs(close - p.Stop) * bpv * p.SegmentQty;
			if (p.SegmentOneR > p.PeakRiskDollars) p.PeakRiskDollars = p.SegmentOneR;
			return p.Stop;
		}

		bool Reached(ExitPosition p, double level, double close)
		{
			if (double.IsNaN(level)) return false;
			return p.Long ? close >= level : close <= level;
		}

		public ExitDecision Manage(ExitPosition p, ExitBar bar)
		{
			var d = new ExitDecision();
			d.Stop = p.Stop;
			if (!p.Open) return d;
			p.ClosedBars++;
			if (p.Long) { if (!p.ExtremeSet || bar.High > p.Extreme) p.Extreme = bar.High; }
			else if (!p.ExtremeSet || bar.Low < p.Extreme) p.Extreme = bar.Low;
			p.ExtremeSet = true;
			double adverse = p.Long ? bar.Low : bar.High;
			if (bar.IsExitBar) MarkMae(p, adverse, bar.Bpv, true, bar.ExitPrice);
			else MarkMae(p, adverse, bar.Bpv, false, 0);

			bool stop = p.GapThroughStop || p.TrailThroughNext || (p.Long ? bar.Low <= p.Stop : bar.High >= p.Stop);
			string stopReason = (p.TrailThroughNext && !p.GapThroughStop && !(p.Long ? bar.Low <= p.Stop : bar.High >= p.Stop)) ? "TRAIL-THROUGH" : "STOP";
			if (p.TrailThroughNext && !(p.Long ? bar.Low <= p.Stop : bar.High >= p.Stop) && !p.GapThroughStop) stopReason = "TRAIL-THROUGH";
			bool t1Hit = T1On && !p.T1Skipped && p.T1Qty > 0 && !p.T1Filled && (p.Long ? bar.High >= p.T1Level : bar.Low <= p.T1Level);
			bool finalHit = FinalOn && !p.FinalSkipped && !double.IsNaN(p.FinalLevel) && (p.Long ? bar.High >= p.FinalLevel : bar.Low <= p.FinalLevel);
			bool target = t1Hit || finalHit;
			string targetReason = t1Hit ? "T1" : "TARGET";
			bool news = NewsWindow(bar.MinutesToNews, Config.NewsExitMinutes, bar.BarMinutes);
			bool session = SessionFlatten(bar);
			bool roll = Effective.RollExit == ERollExit.FlattenBeforeSplice && bar.RollFlattenBar;
			bool invalidate = Config.Embedded && bar.Invalidate;
			bool time = TimeHit(p, bar.TimeEt);
			bool control = ControlHit(p);
			string reason = Precedence(bar.ForceClose, stop, target, news, session, roll, invalidate, time, control, targetReason);
			if (reason == "STOP" && stopReason == "TRAIL-THROUGH") reason = "TRAIL-THROUGH";
			if (reason != null)
			{
				d.Reason = reason;
				d.Stop = p.Stop;
				p.Open = false;
				return d;
			}
			if (!BeDisabled && !p.BeDone)
			{
				bool fire = false;
				if (Effective.BreakEven == EBreakEven.AfterT1 && ScaleOutHasT1()) fire = p.T1Filled || p.T1Skipped;
				else if (BeUsesTrigger)
				{
					string err;
					double lvl = PlanTrigger(p.Long, Effective.BreakEvenUnit, Config.BreakEvenValue, p.Fill, p.InitialRPrice, bar.Tick, bar.Bpv, out err);
					fire = Reached(p, lvl, bar.Close);
				}
				if (fire)
				{
					double be = BreakEvenStop(p.Long, p.Fill, Config.BreakEvenOffsetTicks, bar.Tick);
					double next = p.Long ? Math.Max(p.Stop, be) : Math.Min(p.Stop, be);
					if (next != p.Stop) { p.Stop = next; d.StopChanged = true; }
					p.BeDone = true;
				}
			}
			bool startTrail = p.TrailOn;
			if (!startTrail && Effective.TrailMode != ETrailMode.None && !TrailTriggerDisabled)
			{
				if (Effective.TrailStart == ETrailStart.AfterT1 && ScaleOutHasT1()) startTrail = p.T1Filled || p.T1Skipped;
				else if (Effective.TrailStart == ETrailStart.Immediately) startTrail = true;
				else if (TrailUsesTrigger)
				{
					string err;
					double lvl = PlanTrigger(p.Long, Effective.TrailStartUnit, Config.TrailStartValue, p.Fill, p.InitialRPrice, bar.Tick, bar.Bpv, out err);
					startTrail = Reached(p, lvl, bar.Close);
				}
			}
			if (startTrail && Effective.TrailMode != ETrailMode.None)
			{
				p.TrailOn = true;
				double candidate = TrailCandidate(p, bar);
				bool through;
				double next = Tighten(p.Long, p.Stop, candidate, bar.Close, bar.Tick, out through);
				if (through)
				{
					p.TrailThroughNext = true;
					d.TrailThrough = true;
				}
				else if (next != p.Stop)
				{
					p.Stop = next;
					d.StopChanged = true;
				}
			}
			d.Stop = p.Stop;
			d.SendT1 = T1On && !p.T1Skipped && !p.T1Filled && p.T1Qty > 0;
			d.T1Qty = d.SendT1 ? p.T1Qty : 0;
			return d;
		}

		public static string Money(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
		public static string Pct3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
		public static string Ratio2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
		public static string Px(double v) { return v.ToString("0.########", CultureInfo.InvariantCulture); }
		public static string Num(double v) { return v.ToString("0.########", CultureInfo.InvariantCulture); }

		public static double PlannedRiskPct(double plannedDollars, double equity)
		{
			if (!(equity > 0) || !(plannedDollars > 0)) return double.NaN;
			return plannedDollars / equity * 100.0;
		}

		public string CloseLine(ExitPosition p, string reason, double mark, double bpv, double totalPnl, string maeScope)
		{
			double planned = p.InitialRDollars;
			double plannedPct = PlannedRiskPct(planned, p.EquityAtEntry);
			double peakPct = PlannedRiskPct(p.PeakRiskDollars, p.EquityAtEntry);
			bool equityOk = p.EquityAtEntry > 0;
			double actualPct = equityOk ? p.MaeTradeDollars / p.EquityAtEntry * 100.0 : double.NaN;
			double ratio = planned > 0 ? p.MaeTradeDollars / planned : double.NaN;
			double realPct = equityOk ? totalPnl / p.EquityAtEntry * 100.0 : double.NaN;
			string flag = p.RiskFlag ?? "";
			if (!(planned > 0)) flag = flag.Length == 0 ? "NO-PLANNED-RISK" : flag + (flag.Contains("NO-PLANNED-RISK") ? "" : " NO-PLANNED-RISK");
			string pp = double.IsNaN(plannedPct) ? "" : Pct3(plannedPct);
			string pk = double.IsNaN(peakPct) ? "" : Pct3(peakPct);
			string ap = !equityOk || double.IsNaN(actualPct) ? "" : Pct3(actualPct);
			string ra = double.IsNaN(ratio) ? "" : Ratio2(ratio);
			string rp = !equityOk || double.IsNaN(realPct) ? "" : Pct3(realPct);
			double r = R(p, mark, bpv);
			double ro = ROrig(p, totalPnl);
			string prefix = Config.InPortfolio ? "PORT CLOSE " : "EXIT CLOSE ";
			return prefix + Config.Id + " side=" + (p.Long ? "long" : "short") + " qty=" + p.Qty.ToString(CultureInfo.InvariantCulture) + "+" + p.AddCount.ToString(CultureInfo.InvariantCulture)
				+ " reason=" + reason + " R=" + Ratio2(r) + " Rorig=" + (double.IsNaN(ro) ? "" : Ratio2(ro))
				+ " plannedRiskPct=" + pp + " plannedRiskPctPeak=" + pk + " actualRiskPct=" + ap + " actualToPlanned=" + ra
				+ " realizedPnlPct=" + rp + " mae=$" + Money(p.MaeTradeDollars) + " maeScope=" + maeScope
				+ " equity=$" + Money(p.EquityAtEntry) + (flag.Length > 0 ? " riskFlag=" + flag : "");
		}
	}

	public struct ExitEffective
	{
		public EInitialStop InitialStop;
		public ETriggerUnit T1Unit;
		public ETriggerUnit FinalTargetUnit;
		public EBreakEven BreakEven;
		public ETriggerUnit BreakEvenUnit;
		public ETrailMode TrailMode;
		public ETrailStart TrailStart;
		public ETriggerUnit TrailStartUnit;
		public ETargetMode TargetMode;
		public ESingleLotMode SingleLotMode;
		public ESessionExit SessionExit;
		public ERollExit RollExit;
		public EExitControl ExitControl;
		public EAddStopMode AddStopMode;

		public static ExitEffective Resolve(ExitConfig c, List<string> log)
		{
			var e = new ExitEffective();
			e.InitialStop = Idx(c.InitialStopIdx, c.InitialStop, InitialStops, "InitialStop", log, c.Id);
			e.T1Unit = Idx(c.T1UnitIdx, c.T1Unit, Units, "T1Unit", log, c.Id);
			e.FinalTargetUnit = Idx(c.FinalTargetUnitIdx, c.FinalTargetUnit, Units, "FinalTargetUnit", log, c.Id);
			e.BreakEven = Idx(c.BreakEvenIdx, c.BreakEven, BreakEvens, "BreakEven", log, c.Id);
			e.BreakEvenUnit = Idx(c.BreakEvenUnitIdx, c.BreakEvenUnit, Units, "BreakEvenUnit", log, c.Id);
			e.TrailMode = Idx(c.TrailModeIdx, c.TrailMode, Trails, "TrailMode", log, c.Id);
			e.TrailStart = Idx(c.TrailStartIdx, c.TrailStart, TrailStarts, "TrailStart", log, c.Id);
			e.TrailStartUnit = Idx(c.TrailStartUnitIdx, c.TrailStartUnit, Units, "TrailStartUnit", log, c.Id);
			e.TargetMode = c.TargetMode; e.SingleLotMode = c.SingleLotMode; e.SessionExit = c.SessionExit;
			e.RollExit = c.RollExit; e.ExitControl = c.ExitControl; e.AddStopMode = c.AddStopMode;
			return e;
		}

		public static readonly EInitialStop[] InitialStops = { EInitialStop.EntryProvided, EInitialStop.AtrMultiple, EInitialStop.FixedTicks, EInitialStop.SignalBarExtreme, EInitialStop.SwingExtreme, EInitialStop.Percent };
		public static readonly ETriggerUnit[] Units = { ETriggerUnit.RMultiple, ETriggerUnit.PercentOfEntry, ETriggerUnit.Ticks, ETriggerUnit.Points, ETriggerUnit.CurrencyPerContract };
		public static readonly EBreakEven[] BreakEvens = { EBreakEven.AfterT1, EBreakEven.AtTrigger, EBreakEven.Off };
		public static readonly ETrailMode[] Trails = { ETrailMode.Chandelier, ETrailMode.Atr, ETrailMode.BarExtreme, ETrailMode.Ema, ETrailMode.RollLevel, ETrailMode.None };
		public static readonly ETrailStart[] TrailStarts = { ETrailStart.AfterT1, ETrailStart.AtTrigger, ETrailStart.Immediately };

		public static T Idx<T>(int idx, T enumValue, T[] order, string name, List<string> log, string id)
		{
			int n = order.Length;
			if (idx == -1) return enumValue;
			if (idx >= 0 && idx < n) return order[idx];
			if (log != null) log.Add(id + " Error: GX IDX-RANGE " + name + "Idx(" + idx.ToString(CultureInfo.InvariantCulture) + ") valid(-1.." + (n - 1).ToString(CultureInfo.InvariantCulture) + ") using " + name + "(" + enumValue + ")");
			return enumValue;
		}

		public static string Hash(ExitEffective e, ExitConfig c)
		{
			var sb = new StringBuilder();
			sb.Append(e.InitialStop).Append('|').Append(c.StopAtrMult.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.StopAtrLength).Append('|').Append(c.StopTicks).Append('|').Append(c.StopSwingBars).Append('|').Append(c.StopPct.ToString("R", CultureInfo.InvariantCulture)).Append('|');
			sb.Append(e.TargetMode).Append('|').Append(e.T1Unit).Append('|').Append(c.T1Value.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.T1ScalePct).Append('|');
			sb.Append(e.FinalTargetUnit).Append('|').Append(c.FinalTargetValue.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(e.SingleLotMode).Append('|');
			sb.Append(e.BreakEven).Append('|').Append(e.BreakEvenUnit).Append('|').Append(c.BreakEvenValue.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.BreakEvenOffsetTicks).Append('|');
			sb.Append(e.TrailMode).Append('|').Append(e.TrailStart).Append('|').Append(e.TrailStartUnit).Append('|').Append(c.TrailStartValue.ToString("R", CultureInfo.InvariantCulture)).Append('|');
			sb.Append(c.TrailAtrMult.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.TrailAtrLength).Append('|').Append(c.TrailBars).Append('|').Append(c.TrailEmaLength).Append('|');
			sb.Append(e.SessionExit).Append('|').Append(c.FlatByHHMM).Append('|').Append(c.MaxHoldDays).Append('|').Append(e.RollExit).Append('|').Append(e.ExitControl).Append('|');
			sb.Append(c.ControlExitBarsM1).Append('|').Append(c.ControlExitBarsM5).Append('|').Append(c.ControlExitBarsM15).Append('|').Append(c.ControlExitBarsH1).Append('|').Append(c.ControlExitBarsD1).Append('|');
			sb.Append(c.TimeStopBars).Append('|').Append(c.TimeStopMinutes).Append('|').Append(c.NewsExitMinutes).Append('|').Append(e.AddStopMode).Append('|').Append(c.FillBarDisasterStop ? 1 : 0);
			ulong h = 14695981039346656037UL;
			unchecked { foreach (byte b in Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; } }
			return h.ToString("x16", CultureInfo.InvariantCulture);
		}
	}

	/// <summary>Static entry points hosts call. The state machine is <see cref="ExitSession"/>.</summary>
	public static class _ExitEngine
	{
		public const string PlanFillInfo = "EXIT Info: GX T1-PLAN source(fill) - entry did not publish pm.gxPlanEntry/pm.gxPlanStop";

		public static bool PlanKeysValid(bool longSide, bool hasEntry, bool hasStop, double entry, double stop, int writtenBar, int fillBar)
		{
			if (!hasEntry || !hasStop) return false;
			if (double.IsNaN(entry) || double.IsInfinity(entry) || double.IsNaN(stop) || double.IsInfinity(stop)) return false;
			if (longSide ? !(stop < entry) : !(stop > entry)) return false;
			if (writtenBar > fillBar) return false;
			return true;
		}

		/// <summary>Same widened stop ExitSession.InitialStop returns after Start has resolved the mode.</summary>
		public static double InitialStop(EInitialStop mode, bool longSide, double entry, ExitMarket mkt, ExitConfig c, bool entryProvidedOk)
		{
			if (ExitSession.InvalidReason(mode, c, entryProvidedOk) != null) mode = EInitialStop.AtrMultiple;
			return ExitSession.Widen(entry, ExitSession.RawStop(mode, longSide, entry, mkt, c), ExitSession.FloorDistance(mkt.Tick, mkt.FloorTicks));
		}
	}
}

#if !GX_OFFMC
namespace PowerLanguage.Strategy
{
	/// <summary>
	/// GX-1 placeholder (specs/generic-exit-manager-spec.md rev 1.1 §8.1).
	/// GX-3 replaces this body with the wrapper. No Inputs and no orders.
	/// The shared engine is PowerLanguage.Function._ExitEngine in this file.
	/// </summary>
	public class _GenericExit : _CustomSignal
	{
		public _GenericExit(object _ctx) : base(_ctx) { }

		protected override void CalcBar()
		{
		}
	}
}
#endif
