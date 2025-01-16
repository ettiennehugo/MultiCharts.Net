using System;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Collections.Generic;
using PowerLanguage.Indicator;
using PowerLanguage.TradeManager;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using IronPython.Runtime;
using Newtonsoft.Json.Linq;

namespace PowerLanguage.Strategy
{
    /// <summary>
    /// Manages entry and exit signals based on a trendlines.
    /// 
    /// Supports the following:
    /// - Takes a set of instructions defined in JSON tied to the trendlines defined on the chart to trade against the price levels defined by the trendlines.
    /// - Allows using two trendlines together to define entry/exit zones.
    /// - Allows stop-loss and take-profit orders for trade management.
    /// 
    /// Instructions:
    /// EntryLine
    ///	• LineId - trend line id 
    ///	• EntryType - enum of Long, Short
    ///	• EquityPercent - percent of the ACCOUNT equity to enter into the position
    ///	• [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
    ///	• [optional] EntryPercentRoundingUp - (boolean) round the entry position up to the next whole number, default false
    ///	• [optional] BufferPrice - price to add/subtract from the trend line, only enter the position when you have a break above/below the trendline with the buffer price included in the price calculation, default 0.0
    ///	• [optional] ClosePercentile - (only used when Criteria == Close) double value between 0.0 and 1.0, e.g. 0.05 would mean the closing price must not more than 5% from the high for a long entry, vice versa for low/short entries
    ///		○ raise a warning when this value is below 0.5, meaning the user wants to price to close on the opposite side of the bar
    ///	• [optional] StartDate/EndDate - Start/end dates to check for an entry
    ///	• [optional] StartTime/EndTime - Start/end time filters on which the entry should be checked, e.g. 9:35 AM start to 10:00 AM end would look for an entry the first half hour of the regular stock trading day
    ///	• [optional] ValidUntil - date/time until which the entry should hold, defaults to DateTime.Max (if this field is specified with the EndDate/EndTime fields the entry would become disabled which ever date/time comes first)
    ///	• [optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
    ///  
    /// ExitLine
    /// • LineId - trend line id
    ///	• [optional] PositionPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
    ///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    /// 
    /// ExitMovingAverage
	/// • Length - length of the moving average.
    /// • Type - enum Simple, Exponential, SuperSmoother
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    ///
    /// EntryZone
    ///	• FirstLineId - first trend line used to define the zone
    ///	• SecondLineId - second trend line used to define the zone
    ///	• EntryType - enum of Long, Short
    ///	• EquityPercent - percent of the ACCOUNT size to enter into the position
    ///	• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
    ///	• [optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
    /// 
    /// ExitZone
    ///	• FirstLineId - first trend line used to define the zone
    ///	• SecondLineId - second trend line used to define the zone
    ///	• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
    ///	• [optional] PositionPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    ///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    ///
    /// ExitStopLoss
    ///	• Percent - exit the position if the price moves against you by the specified percentage.
    ///	• [optional] PositionPercent - percent of the POSITION to exit, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitProfit
    ///	• Percent - exit the position one the price moved in your favor by this percentage
    ///	• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitDays
    /// • Count - number of days after which the position should be closed
    ///	• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%) 
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitEntryBarLowHigh
    ///	• [optional] BufferPrice - buffer price to add/subtract from entry bar extreme
    ///	• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    ///		
    /// Example JSON:
    /// [
    ///		"EntryLine" : {
    ///			"LineId" : 1,
    ///			"EntryType" : "Long", 
    ///			"EquityPercent" : 0.01,
    ///			"BufferPrice" : 0.05,
    ///			"ClosePercentile" : 0.75,
    ///			"StartTime" : "9:35 AM",
    ///			"EndTime" : "9:35 AM"
    ///		},
    ///		"EntryLine" : {
    ///			"LineId" : 2,
    ///			"EntryType" : "Short", 
    ///			"EquityPercent" : 0.01,
    ///			"BufferPrice" : 0.05,
    ///			"ClosePercentile" : 0.75,
    ///			"StartTime" : "9:35 AM",
    ///			"EndTime" : "9:35 AM"
    ///		},
    ///		"ExitLine" : {
    ///			"LineId" : 3,
    ///			"PositionPercent" : 0.5,
    ///		},
    ///		"ExitProfit" : {
    ///			"Percent" : 0.5
    ///		}
    ///		"ExitEntryBarLowHigh" : {
    ///			"BufferPrice" : 0.05,
    ///		}
    /// ]
    /// </summary>
    public class _TrendLine : _CustomSignal
    {
        //constants
        /// <summary>
        /// Maximum number of long, short type entries/exit supported and the number of moving averages supported.
        /// </summary>
        public const int MAX_INSTRUCTIONS = 10;
        public const int MAX_MOVINGAVERAGES = 5;

        //types
        public enum EEntryType
        {
            Long,
            Short
        };

        public enum EExitType
        {
            Stop,
            StopLimit
        };

        /// <summary>
        /// Determines when the entry/exit should execute based on the price crossing the trendline.
        /// </summary>
        public enum EEntryExitCriteria
        {
            Close,
            LowHigh
        };

        public enum EMovingAverageType
        {
            Simple,
            Exponential,
            SuperSmoother
        };

        public abstract class Instruction
        {
            public _TrendLine m_studyControl;

            public Instruction(_TrendLine control) { m_studyControl = control; }

            public abstract void Parse(JProperty property);
            public virtual void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    Parse((JProperty)child);
                }
            }

            public virtual void StartCalc() { }
            public virtual void StopCalc() { }
            public abstract void CalcBar();
            public abstract Tuple<bool, string> IsValid();

            protected ITrendLineObject getTrendLine(int id)
            {
                foreach (ITrendLineObject line in m_studyControl.DrwTrendLine.GetTrendLineObjects(EDrawingSource.AnyTechOrManual))
                    if (line.ID == id) return line;
                return null;
            }
        }

        public abstract class Exit : Instruction
        {
            public EExitType ExitType { get; set; }
            public double ExitPercent { get; set; }
            public DateTime ValidUntil { get; set; }

            protected IOrderPriced m_orderLx;
            protected IOrderPriced m_orderSx;
            protected IOrderStopLimit m_orderLxSL;
            protected IOrderStopLimit m_orderSxSL;

            public Exit(_TrendLine control) : base(control)
            {
                ExitType = EExitType.Stop;
                ExitPercent = 1.0;
                ValidUntil = DateTime.MaxValue;
            }

            public override void StartCalc()
            {
                m_orderLx = null;
                m_orderSx = null;
                m_orderLxSL = null;
                m_orderSxSL = null;

                switch (ExitType)
                {
                    case EExitType.Stop:
                        m_orderLx = m_studyControl.AllocateLxStp();
                        m_orderSx = m_studyControl.AllocateSxStp();
                        break;
                    case EExitType.StopLimit:
                        m_orderLxSL = m_studyControl.AllocateLxStpLmt();
                        m_orderSxSL = m_studyControl.AllocateSxStpLmt();
                        break;
                }
            }

            public override void Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "exittype") ExitType = property.Value.ToObject<EExitType>();
                if (name == "exitpercent") ExitPercent = property.Value.ToObject<double>();
                if (name == "validuntil") ValidUntil = property.Value.ToObject<DateTime>();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "Exit - ExitPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        }

        public class EntryLine : Instruction
        {
            public int LineId { get; set; }
            public EEntryType EntryType { get; set; }
            public double EntryPercent { get; set; }
            public EEntryExitCriteria Criteria { get; set; }
            public double BufferPrice { get; set; }
            public double ClosePercentile { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public DateTime ValidUntil { get; set; }
            public bool AddToPosition { get; set; }

            private ITrendLineObject m_line;
            private IOrderPriced m_order;

            public EntryLine(_TrendLine control) : base(control)
            {
                EntryPercent = 1.0;
                Criteria = EEntryExitCriteria.Close;
                BufferPrice = 0.0;
                ClosePercentile = 0.05;
                StartDate = DateTime.MinValue;
                EndDate = DateTime.MaxValue;
                StartTime = DateTime.MinValue;
                EndTime = DateTime.MaxValue;
                AddToPosition = false;
            }

            public override void Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "entrytype") EntryType = property.Value.ToObject<EEntryType>();
                if (name == "equitypercent") EntryPercent = property.Value.ToObject<double>();
                if (name == "criteria") Criteria = property.Value.ToObject<EEntryExitCriteria>();
                if (name == "bufferprice") BufferPrice = property.Value.ToObject<double>();
                if (name == "closepercentile") ClosePercentile = property.Value.ToObject<double>();
                if (name == "startdate") StartDate = property.Value.ToObject<DateTime>();
                if (name == "enddate") EndDate = property.Value.ToObject<DateTime>();
                if (name == "starttime") StartTime = property.Value.ToObject<DateTime>();
                if (name == "endtime") EndTime = property.Value.ToObject<DateTime>();
                if (name == "validuntil") ValidUntil = property.Value.ToObject<DateTime>();
                if (name == "addtoposition") AddToPosition = property.Value.ToObject<bool>();
            }

            public override void StartCalc()
            {
                m_line = getTrendLine(LineId);
                if (EntryType == EEntryType.Long)
                    m_order = m_studyControl.AllocateLeLmt();
                else
                    m_order = m_studyControl.AllocateSeLmt();
            }

            public override void CalcBar()
            {
                if (m_studyControl.Bars.CurrentBar < 2) return;
                if (m_studyControl.PositionSide != 0 && !AddToPosition) return;
                if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
                if (m_studyControl.Bars.Time[0].Date < StartDate || m_studyControl.Bars.Time[0].Date > EndDate) return;
                if (m_studyControl.Bars.Time[0].TimeOfDay < StartTime.TimeOfDay || m_studyControl.Bars.Time[0].TimeOfDay > EndTime.TimeOfDay) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;

                if (EntryType == EEntryType.Long) 
                    longEntry();
                else 
                    shortEntry();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_line == null) return Tuple.Create(false, string.Format("EntryLine - trendline with id {0} not found"));
                if (EntryPercent <= 0 || EntryPercent > 1.0) return Tuple.Create(false, "EntryLine - EntryPercent must be between 0 and 1");
                if (ClosePercentile < 0 || ClosePercentile > 1.0) return Tuple.Create(false, "EntryLine - ClosePercentile must be between 0 and 1");
                if (StartDate > EndDate) return Tuple.Create(false, "EntryLine - StartDate must be before EndDate");
                if (StartTime > EndTime) return Tuple.Create(false, "EntryLine - StartTime must be before EndTime");
                return Tuple.Create(true, string.Empty);
            }

            private void longEntry()
            {
                if (m_studyControl.Bars.Close[1] < (m_line.PriceValue(m_studyControl.Bars.Time[1]) + BufferPrice) &&
                    m_studyControl.Bars.Close[0] > (m_line.PriceValue(m_studyControl.Bars.Time[0]) + BufferPrice))
                {
                    bool closePercentileCheck = true;
                    double entryPrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
                    if (Criteria == EEntryExitCriteria.Close)
                    {
                        double low = m_studyControl.Bars.Low[0];
                        double close = m_studyControl.Bars.Close[0];
                        double range = m_studyControl.Bars.High[0] - low;
                        double closePercentile = range != 0 ? (close - low) / range : 0;    //this will skip trades for bars with no range
                        closePercentileCheck = closePercentile > ClosePercentile;
                        entryPrice = close;
                    }

                    if (closePercentileCheck)
                    {
                        if (m_studyControl.Debug) m_studyControl.Output.WriteLine("EntryLine - Long entry at {0}, {1}", m_studyControl.Bars.Time[0], m_line.PriceValue(m_studyControl.Bars.Time[0]));
                        m_order.Send(entryPrice, m_studyControl.positionSize(m_studyControl.Bars, EntryPercent));
                    }
                }
            }

            private void shortEntry()
            {
                if (m_studyControl.Bars.Close[1] > (m_line.PriceValue(m_studyControl.Bars.Time[1]) - BufferPrice) &&
                    m_studyControl.Bars.Close[0] < (m_line.PriceValue(m_studyControl.Bars.Time[0]) - BufferPrice))
                {
                    bool closePercentileCheck = true;
                    double entryPrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
                    if (Criteria == EEntryExitCriteria.Close)
                    {
                        double high = m_studyControl.Bars.High[0];
                        double close = m_studyControl.Bars.Close[0];
                        double range = high - m_studyControl.Bars.Low[0];
                        double closePercentile = range != 0 ? 1 - (high - close) / range : 0;    //this will skip trades for bars with no range, 1 - to invert the percentile check
                        closePercentileCheck = closePercentile > ClosePercentile;
                        entryPrice = close;
                    }

                    if (closePercentileCheck)
                    {
                        if (m_studyControl.Debug) m_studyControl.Output.WriteLine("EntryLine - Short entry at {0}, {1}", m_studyControl.Bars.Time[0], m_line.PriceValue(m_studyControl.Bars.Time[0]));
                        m_order.Send(entryPrice, m_studyControl.positionSize(m_studyControl.Bars, EntryPercent));
                    }
                }
            }
        };

        public class ExitLine : Exit
        {
            public int LineId { get; set; }
            public EEntryExitCriteria Criteria { get; set; }

            private ITrendLineObject m_line;

            public ExitLine(_TrendLine control) : base(control)
            {
                Criteria = EEntryExitCriteria.Close;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (name == "lineid") LineId = property.Value.ToObject<int>();
                if (name == "criteria") Criteria = property.Value.ToObject<EEntryExitCriteria>();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_line = getTrendLine(LineId);
            }

            public override void CalcBar()
            {
                if (m_studyControl.Bars.CurrentBar < 2) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;
                if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
                if (m_studyControl.PositionSide > 0)
                    exitLong();
                else if (m_studyControl.PositionSide < 0)
                    exitShort();
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (m_line == null) return Tuple.Create(false, string.Format("ExitLine - trendline with id {0} not found"));
                return Tuple.Create(true, string.Empty);
            }

            private void exitLong()
            {
                double trendLineValue = m_line.PriceValue(m_studyControl.Bars.Time[0]);
                int positionSize = m_studyControl.positionSize(m_studyControl.Bars, ExitPercent);
                switch (Criteria)
                {
                    case EEntryExitCriteria.Close:
                        if (m_studyControl.Bars.Close[0] < trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Long exit on close at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EExitType.Stop)
                                m_orderLx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EExitType.StopLimit)
                                m_orderLxSL.Send(m_studyControl.Bars.Close[0], m_studyControl.Bars.Close[0], positionSize);
                        }
                        break;
                    case EEntryExitCriteria.LowHigh:
                        if (m_studyControl.Bars.Low[0] < trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Long exit on low at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EExitType.Stop)
                                m_orderLx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EExitType.StopLimit)
                                m_orderLxSL.Send(m_studyControl.Bars.Low[0], m_studyControl.Bars.Low[0], positionSize);
                        }
                        break;
                }
            }

            private void exitShort()
            {
                double trendLineValue = m_line.PriceValue(m_studyControl.Bars.Time[0]);
                int positionSize = m_studyControl.positionSize(m_studyControl.Bars, ExitPercent);
                switch (Criteria)
                {
                    case EEntryExitCriteria.Close:
                        if (m_studyControl.Bars.Close[0] > trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Short exit on close at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EExitType.Stop)
                                m_orderSx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EExitType.StopLimit)
                                m_orderSxSL.Send(m_studyControl.Bars.Close[0], m_studyControl.Bars.Close[0], positionSize);
                        }
                        break;
                    case EEntryExitCriteria.LowHigh:
                        if (m_studyControl.Bars.High[0] > trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Short exit on high at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EExitType.Stop)
                                m_orderSx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EExitType.StopLimit)
                                m_orderSxSL.Send(m_studyControl.Bars.High[0], m_studyControl.Bars.High[0], positionSize);
                        }
                        break;
                }
            }
        }

        public class ExitMovingAverage : Exit
        {
            public int Length { get; set; }
            public EMovingAverageType Type { get; set; }
            public EEntryExitCriteria Criteria { get; set; }

            private PowerLanguage.Function.AverageFC m_averageSimple;
            private PowerLanguage.Function.XAverage m_averageExponential;
            private PowerLanguage.Function._SuperSmoother m_averageSuperSmoother;

            public ExitMovingAverage(_TrendLine control) : base(control)
            {
                Criteria = EEntryExitCriteria.Close;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (name == "length") Length = property.Value.ToObject<int>();
                if (name == "type") Type = property.Value.ToObject<EMovingAverageType>();
                if (name == "criteria") Criteria = property.Value.ToObject<EEntryExitCriteria>();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_averageSimple = null;
                m_averageExponential = null;
                m_averageSuperSmoother = null;

                switch (Type)
                {
                    case EMovingAverageType.Simple:
                        m_averageSimple = m_studyControl.AllocateSimpleAverage();
                        m_averageSimple.length = Length;
                        break;
                    case EMovingAverageType.Exponential:
                        m_averageExponential = m_studyControl.AllocateExponentialAverage();
                        m_averageExponential.Length = Length;
                        break;
                    case EMovingAverageType.SuperSmoother:
                        m_averageSuperSmoother = m_studyControl.AllocateSuperSmoother();
                        m_averageSuperSmoother.Length = Length;
                        break;
                }
            }

            public override void CalcBar()
            {
                if (m_studyControl.PositionSide == 0) return;
                if (m_studyControl.Bars.CurrentBar < Length) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;
                if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;

                switch (Type)
                {
                    case EMovingAverageType.Simple:
                        exitAverage(m_averageSimple);
                        break;
                    case EMovingAverageType.Exponential:
                        exitAverage(m_averageExponential);
                        break;
                    case EMovingAverageType.SuperSmoother:
                        exitAverage(m_averageSuperSmoother);
                        break;
                }
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (Length <= 0) return Tuple.Create(false, "ExitMovingAverage - Length must be greater than 0");
                return Tuple.Create(true, string.Empty);
            }

            private void exitAverage(ISeries<double> values)
            {
                int positionSize = m_studyControl.positionSize(m_studyControl.Bars, ExitPercent);
                if (m_studyControl.PositionSide > 0)
                {
                    if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - Long exit at {0}, {1}", m_studyControl.Bars.Time[0], values[0]);
                    if (ExitType == EExitType.Stop)
                        m_orderLx.Send(values[0], positionSize);
                    else if (ExitType == EExitType.StopLimit)
                        m_orderLxSL.Send(values[0], values[0], positionSize);
                }
                else if (m_studyControl.PositionSide < 0)
                {
                    if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - Short exit at {0}, {1}", m_studyControl.Bars.Time[0], values[0]);
                    if (ExitType == EExitType.Stop)
                        m_orderSx.Send(values[0], positionSize);
                    else if (ExitType == EExitType.StopLimit)
                        m_orderSxSL.Send(values[0], values[0], positionSize);
                }
            }
        }

        public class EntryZone : Instruction
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }
            public EOrderAction EntryType { get; set; }
            public double EntryPercent { get; set; }
            public EEntryExitCriteria Criteria { get; set; }
            public bool AddToPosition { get; set; }
            public DateTime ValidUntil { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public EntryZone(_TrendLine control) : base(control)
            {
                Criteria = EEntryExitCriteria.Close;
                AddToPosition = false;
                ValidUntil = DateTime.MaxValue;
            }

            public override void Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "firstlineid") FirstLineId = property.Value.ToObject<int>();
                if (name == "secondlineid") SecondLineId = property.Value.ToObject<int>();
                if (name == "entrytype") EntryType = property.Value.ToObject<EOrderAction>();
                if (name == "entrypercent") EntryPercent = property.Value.ToObject<double>();
                if (name == "criteria") Criteria = property.Value.ToObject<EEntryExitCriteria>();
                if (name == "addtoposition") AddToPosition = property.Value.ToObject<bool>();
                if (name == "validuntil") ValidUntil = property.Value.ToObject<DateTime>();
            }

            public override void StartCalc()
            {
                m_firstLine = getTrendLine(FirstLineId);
                m_secondLine = getTrendLine(SecondLineId);
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_firstLine == null) return Tuple.Create(false, string.Format("EntryZone - trendline with id {0} not found", FirstLineId));
                if (m_secondLine == null) return Tuple.Create(false, string.Format("EntryZone - trendline with id {0} not found", SecondLineId));
                if (EntryPercent <= 0 || EntryPercent > 1.0) return Tuple.Create(false, "EntryZone - EntryPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitZone : Exit
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }
            public EEntryExitCriteria Criteria { get; set; }
            public DateTime ValidUntil { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public ExitZone(_TrendLine control) : base(control)
            {
                Criteria = EEntryExitCriteria.Close;
                ValidUntil = DateTime.MaxValue;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (name == "firstlineid") FirstLineId = property.Value.ToObject<int>();
                if (name == "secondlineid") SecondLineId = property.Value.ToObject<int>();
                if (name == "criteria") Criteria = property.Value.ToObject<EEntryExitCriteria>();
                if (name == "validuntil") ValidUntil = property.Value.ToObject<DateTime>();
            }

            public override void StartCalc()
            {
                m_firstLine = getTrendLine(FirstLineId);
                m_secondLine = getTrendLine(SecondLineId);
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (m_firstLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", FirstLineId));
                if (m_secondLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", SecondLineId));
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitStopLoss : Exit
        {
            public double LossPrice { get; set; }
            public double LossPercent { get; set; }

            public ExitStopLoss(_TrendLine control) : base(control)
            {
                LossPercent = -1.0;
                LossPrice = -1.0;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (name == "losspercent") LossPercent = property.Value.ToObject<double>();
                if (name == "lossprice") LossPrice = property.Value.ToObject<double>();
            }

            public override void StartCalc()
            {
                m_orderLx = null;
                m_orderSx = null;
                m_orderLxSL = null;
                m_orderSxSL = null;

                switch (ExitType)
                {
                    case EExitType.Stop:
                        m_orderLx = m_studyControl.AllocateLxStp();
                        m_orderSx = m_studyControl.AllocateSxStp();
                        break;
                    case EExitType.StopLimit:
                        m_orderLxSL = m_studyControl.AllocateLxStpLmt();
                        m_orderSxSL = m_studyControl.AllocateSxStpLmt();
                        break;
                }
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (LossPercent == -1.0 && LossPrice == -1.0) return Tuple.Create(false, "ExitStopLoss - LossPercent or LossPrice must be specified");
                if (LossPercent != -1.0 && (LossPercent <= 0.0 || LossPercent > 1.0)) return Tuple.Create(false, "ExitStopLoss - LossPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitProfit : Exit
        {
            public double ProfitPercent { get; set; }
            public double ProfitPrice { get; set; }

            public ExitProfit(_TrendLine control) : base(control)
            {
                ProfitPercent = -1.0;
                ProfitPrice = -1.0;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (name == "profitpercent") ProfitPercent = property.Value.ToObject<double>();
                if (name == "profitprice") ProfitPrice = property.Value.ToObject<double>();
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (ProfitPercent == -1.0 && ProfitPrice == -1.0) return Tuple.Create(false, "ExitProfit - ProfitPercent or ProfitPrice must be specified");
                if (ProfitPercent != -1.0 && (ProfitPercent <= 0.0 || ProfitPercent > 1.0)) return Tuple.Create(false, "ExitProfit - ProfitPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitDays : Exit
        {
            public int Count { get; set; }

            public ExitDays(_TrendLine control) : base(control)
            {
                Count = 1;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (property.Name.ToLower() == "count") Count = property.Value.ToObject<int>();
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (Count <= 0) return Tuple.Create(false, "ExitDays - Count must be greater than 0");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitEntryBarLowHigh : Exit
        {
            public double BufferPrice { get; set; }

            public ExitEntryBarLowHigh(_TrendLine control) : base(control)
            {
                BufferPrice = 0.0;
            }

            public override void Parse(JProperty property)
            {
                base.Parse(property);
                string name = property.Name.ToLower();
                if (property.Name.ToLower() == "bufferprice") BufferPrice = property.Value.ToObject<double>();
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                Tuple<bool, string> baseResult = base.IsValid();
                if (!baseResult.Item1) return baseResult;
                if (BufferPrice < 0) return Tuple.Create(false, "ExitEntryBarLowHigh - BufferPrice must be greater than or equal to 0");
                return Tuple.Create(true, string.Empty);
            }
        };

        /// <summary>
        /// InstructionFactory used to create instructions based on some specific instruction name and associated type.
        /// </summary>
        public static class InstructionFactory
        {
            private static readonly Dictionary<string, Func<_TrendLine, Instruction>> m_instructionCreators = new Dictionary<string, Func<_TrendLine, Instruction>>
            {
                { "entryline", control => new _TrendLine.EntryLine(control) },
                { "exitline", control => new _TrendLine.ExitLine(control) },
                { "exitmovingaverage", control => new _TrendLine.ExitMovingAverage(control) },
                { "entryzone", control => new _TrendLine.EntryZone(control) },
                { "exitzone", control => new _TrendLine.ExitZone(control) },
                { "exitstoploss", control => new _TrendLine.ExitStopLoss(control) },
                { "exitprofit", control => new _TrendLine.ExitProfit(control) },
                { "exitdays", control => new _TrendLine.ExitDays(control) },
                { "exitentrybarlowhigh", control => new _TrendLine.ExitEntryBarLowHigh(control) }
            };

            public static Instruction CreateInstruction(string type, _TrendLine control)
            {
                Func<_TrendLine, Instruction> creator;
                if (m_instructionCreators.TryGetValue(type.ToLower(), out creator))
                    return creator(control);
                throw new ArgumentException(string.Format("Unsupported instruction type: {0}", type));
            }
        }

        //inputs
        [Input] public string Instructions { get; set; }
        [Input] public bool Debug { get; set; }

        //properties


        //attributes
        private List<IOrderPriced> m_leLmt;
        private int m_leLmtAllocated;
        private List<IOrderPriced> m_lxStp;
        private int m_lxStpAllocated;
        private List<IOrderStopLimit> m_leStpLmt;
        private int m_leStpLmtAllocated;
        private List<IOrderStopLimit> m_lxStpLmt;
        private int m_lxStpLmtAllocated;
        private List<IOrderPriced> m_seLmt;
        private int m_seLmtAllocated;
        private List<IOrderPriced> m_sxStp;
        private int m_sxStpAllocated;
        private List<IOrderStopLimit> m_seStpLmt;
        private int m_seStpLmtAllocated;
        private List<IOrderStopLimit> m_sxStpLmt;
        private int m_sxStpLmtAllocated;

        private List<PowerLanguage.Function.AverageFC> m_averageSimple;
        private int m_averageSimpleAllocated;
        private List<PowerLanguage.Function.XAverage> m_averageExponential;
        private int m_averageExponentialAllocated;
        private List<PowerLanguage.Function._SuperSmoother> m_averageSuperSmoothers;
        private int m_averageSuperSmoothersAllocated;
        private List<Instruction> m_instructions;

        //interface methods
        public _TrendLine(object _ctx) : base(_ctx) { }

        protected override void Create()
        {
            Debug = false;
            createOrders();
            createMovingAverages();
            m_instructions = new List<Instruction>();
        }

        protected override void StartCalc()
        {
            resetOrders();
            resetMovingAverages();
            parseAndValidateInstructions();
            foreach (var instruction in m_instructions) instruction.StartCalc();
        }

        protected override void StopCalc()
        {
            foreach (var instruction in m_instructions) instruction.StopCalc();
        }

        protected override void CalcBar()
        {
            foreach (var instruction in m_instructions) instruction.CalcBar();
        }

        public IOrderPriced AllocateLeLmt()
        {
            if (m_leLmtAllocated >= m_leLmt.Count) throw new InvalidOperationException("No more Long Limit orders available");
            IOrderPriced result = m_leLmt[m_leLmtAllocated];
            m_leLmtAllocated++;
            return result;
        }

        public IOrderPriced AllocateSeLmt()
        {
            if (m_seLmtAllocated >= m_seLmt.Count) throw new InvalidOperationException("No more Short Limit orders available");
            IOrderPriced result = m_seLmt[m_seLmtAllocated];
            m_seLmtAllocated++;
            return result;
        }

        public IOrderPriced AllocateLxStp()
        {
            if (m_lxStpAllocated >= m_lxStp.Count) throw new InvalidOperationException("No more Long Exit Stop orders available");
            IOrderPriced result = m_lxStp[m_lxStpAllocated];
            m_lxStpAllocated++;
            return result;
        }

        public IOrderPriced AllocateSxStp()
        {
            if (m_sxStpAllocated >= m_sxStp.Count) throw new InvalidOperationException("No more Short Exit Stop orders available");
            IOrderPriced result = m_sxStp[m_sxStpAllocated];
            m_sxStpAllocated++;
            return result;
        }

        public IOrderStopLimit AllocateLeStpLmt()
        {
            if (m_leStpLmtAllocated >= m_leStpLmt.Count) throw new InvalidOperationException("No more Long Stop Limit orders available");
            IOrderStopLimit result = m_leStpLmt[m_leStpLmtAllocated];
            m_leStpLmtAllocated++;
            return result;
        }

        public IOrderStopLimit AllocateSeStpLmt()
        {
            if (m_seStpLmtAllocated >= m_seStpLmt.Count) throw new InvalidOperationException("No more Short Stop Limit orders available");
            IOrderStopLimit result = m_seStpLmt[m_seStpLmtAllocated];
            m_seStpLmtAllocated++;
            return result;
        }

        public IOrderStopLimit AllocateLxStpLmt()
        {
            if (m_lxStpAllocated >= m_lxStpLmt.Count) throw new InvalidOperationException("No more Long Exit Stop Limit orders available");
            IOrderStopLimit result = m_lxStpLmt[m_lxStpAllocated];
            m_lxStpAllocated++;
            return result;
        }

        public IOrderStopLimit AllocateSxStpLmt()
        {
            if (m_sxStpLmtAllocated >= m_sxStpLmt.Count) throw new InvalidOperationException("No more Short Exit Stop Limit orders available");
            IOrderStopLimit result = m_sxStpLmt[m_sxStpLmtAllocated];
            m_sxStpLmtAllocated++;
            return result;
        }

        public PowerLanguage.Function.AverageFC AllocateSimpleAverage()
        {
            if (m_averageSimpleAllocated >= m_averageSimple.Count) throw new InvalidOperationException("No more Simple Moving Averages available");
            PowerLanguage.Function.AverageFC result = m_averageSimple[m_averageSimpleAllocated];
            m_averageSimpleAllocated++;
            return result;
        }

        public PowerLanguage.Function.XAverage AllocateExponentialAverage()
        {
            if (m_averageExponentialAllocated >= m_averageExponential.Count) throw new InvalidOperationException("No more Exponential Moving Averages available");
            PowerLanguage.Function.XAverage result = m_averageExponential[m_averageExponentialAllocated];
            m_averageExponentialAllocated++;
            return result;
        }

        public PowerLanguage.Function._SuperSmoother AllocateSuperSmoother()
        {
            if (m_averageSuperSmoothersAllocated >= m_averageSuperSmoothers.Count) throw new InvalidOperationException("No more Super Smoothers available");
            PowerLanguage.Function._SuperSmoother result = m_averageSuperSmoothers[m_averageSuperSmoothersAllocated];
            m_averageSuperSmoothersAllocated++;
            return result;
        }

        //methods
        private void createOrders()
        {
            m_leLmt = new List<IOrderPriced>();
            m_lxStp = new List<IOrderPriced>();
            m_leStpLmt = new List<IOrderStopLimit>();
            m_lxStpLmt = new List<IOrderStopLimit>();
            m_seLmt = new List<IOrderPriced>();
            m_sxStp = new List<IOrderPriced>();
            m_seStpLmt = new List<IOrderStopLimit>();
            m_sxStpLmt = new List<IOrderStopLimit>();

            for (int i = 0; i < MAX_INSTRUCTIONS; i++)
            {
                m_leLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LELmt" + i, EOrderAction.Buy)));
                m_seLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SELmt" + i, EOrderAction.SellShort)));
                m_lxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LXStp" + i, EOrderAction.Sell)));
                m_sxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SXStp" + i, EOrderAction.BuyToCover)));
                m_leStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LEStpLmt" + i, EOrderAction.Buy)));
                m_seStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SEStpLmt" + i, EOrderAction.SellShort)));
                m_lxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LXStpLmt" + i, EOrderAction.Sell)));
                m_sxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SXStpLmt" + i, EOrderAction.BuyToCover)));
            }
        }

        private void createMovingAverages()
        {
            m_averageSimple = new List<PowerLanguage.Function.AverageFC>();
            m_averageExponential = new List<PowerLanguage.Function.XAverage>();
            m_averageSuperSmoothers = new List<PowerLanguage.Function._SuperSmoother>();

            for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
            {
                m_averageSimple.Add(new PowerLanguage.Function.AverageFC(this));
                m_averageExponential.Add(new PowerLanguage.Function.XAverage(this));
                m_averageSuperSmoothers.Add(new PowerLanguage.Function._SuperSmoother(this));
            }
        }

        private void resetOrders()
        {
            m_leLmtAllocated = 0;
            m_seLmtAllocated = 0;
            m_lxStpAllocated = 0;
            m_sxStpAllocated = 0;
            m_leStpLmtAllocated = 0;
            m_seLmtAllocated = 0;
            m_lxStpLmtAllocated = 0;
            m_sxStpLmtAllocated = 0;
        }

        private void resetMovingAverages()
        {
            m_averageSimpleAllocated = 0;
            m_averageExponentialAllocated = 0;
            m_averageSuperSmoothersAllocated = 0;

            for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
            {
                m_averageSimple[i].price = Bars.Close;
                m_averageExponential[i].Price = Bars.Close;
                m_averageSuperSmoothers[i].Price = Bars.Close;
            }
        }

        private void parseAndValidateInstructions()
        {
            m_instructions.Clear();
            JObject root = JObject.Parse(Instructions);
            foreach (var property in root.Properties())
            {
                var instruction = InstructionFactory.CreateInstruction(property.Name, this);
                instruction.Parse(property.Value);
                m_instructions.Add(instruction);
            }

            foreach (var instruction in m_instructions)
            {
                var result = instruction.IsValid();
                if (!result.Item1) throw new ArgumentException(result.Item2);
            }
        }
    }
}