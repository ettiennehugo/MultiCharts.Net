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
using Newtonsoft.Json.Linq;
using System.Runtime.Remoting.Messaging;

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
    /// • Instruction - "EntryLine" 
    ///	• LineId - trend line id 
    ///	• Direction - enum of Long, Short
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
    /// • Instruction - "ExitLine" 
    /// • LineId - trend line id
	/// • [optional] ExitPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
    ///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    /// 
    /// ExitMovingAverage
    /// • Instruction - "ExitMovingAverage" 
	/// • Length - length of the moving average.
    /// • MovingAverageType - enum Simple, Exponential, SuperSmoother
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    ///
    /// EntryZone - TBD
    /// • Instruction - "EntryZone" 
    ///	• FirstLineId - first trend line used to define the zone
    ///	• SecondLineId - second trend line used to define the zone
    ///	• Direction - enum of Long, Short
    ///	• EquityPercent - percent of the ACCOUNT size to enter into the position
    ///	• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
    ///	• [optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
    /// 
    /// ExitZone - TBD
    /// • Instruction - "ExitZone" 
    ///	• FirstLineId - first trend line used to define the zone
    ///	• SecondLineId - second trend line used to define the zone
    ///	• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
	/// • [optional] ExitPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    ///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    ///
    /// ExitStopLoss
    /// • Instruction - "ExitStopLoss" 
    ///	• [alternate] LossPercent - exit the position if the price moves against you by the specified percentage.
    ///	• [alternate] LossPrice - exit the position if the price moves against you by a specified amount. 
	/// • [optional] ExitPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitProfit
    /// • Instruction - "ExitProfit" 
    ///	• [alternate] ProfitPercent - exit the position one the price moved in your favor by this percentage
    ///	• [alternate] ProfitPrice - exit the position one the price moved in your favor by this amount
	/// • [optional] ExitPercent - percent of POSITION to close, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitDays
    /// • Instruction - "ExitDays" 
    /// • Count - number of days after which the position should be closed
    ///	• [optional] ExitPercent - percent of POSITION to exit, default 1.0 (100%) 
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitEntryBarLowHigh
    /// • Instruction - "ExitEntryBarLowHigh" 
    ///	• [optional] BufferPrice - buffer price to add/subtract from entry bar extreme
    ///	• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%)
    ///	• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    ///		
    /// Example JSON:
    /// [
    ///	    {
    ///	        "Instruction" : "EntryLine"
    ///			"LineId" : 1,
    ///			"EntryType" : "Long", 
    ///			"EquityPercent" : 0.01,
    ///			"BufferPrice" : 0.05,
    ///			"ClosePercentile" : 0.75,
    ///			"StartTime" : "9:35 AM",
    ///			"EndTime" : "9:35 AM"
    ///		},
    ///		{
    ///	        "Instruction" : "EntryLine"
    ///			"LineId" : 2,
    ///			"EntryType" : "Short", 
    ///			"EquityPercent" : 0.01,
    ///			"BufferPrice" : 0.05,
    ///			"ClosePercentile" : 0.75,
    ///			"StartTime" : "9:35 AM",
    ///			"EndTime" : "9:35 AM"
    ///		},
    ///		{
    ///	        "Instruction" : "EntryLine"
    ///			"LineId" : 3,
    ///			"PositionPercent" : 0.5,
    ///		},
    ///		{
    ///	        "Instruction" : "ExitProfit"
    ///			"Percent" : 0.5
    ///		}
    ///		{
    ///		    "Instruction" : "ExitEntryBarLowHigh",
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
        public enum EEntryDirection
        {
            Long,
            Short
        };

        public enum EOrderType
        {
            Market,
            Stop,
            Limit,
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

            public string Name { get; set; }

            public Instruction(_TrendLine control) { m_studyControl = control; }

            public virtual bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "instruction") { Name = property.Value.ToString().ToLower(); return true; }
                return false;
            }

            public virtual void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    //this exception is added to perform strict parsing of the input JSON since classes to catch typo's that would not
                    //be caught due to parameter defaults provided, e.g. an entry has parameter EquityPercent that defaults to 1.0, if the
                    //user has a typo on the entry property EqutyPercent set to 0.05 without strict validation the entry would enter at 1.0 (100%)
                    //and not the intended 0.05 (5%)
                    if (!Parse((JProperty)child)) throw new ArgumentException(string.Format("Failed to process property - {0}", child.Path));
                }
            }

            public virtual Tuple<bool, string> IsValid()
            {
                return Tuple.Create(true, string.Empty);
            }

            public virtual void StartCalc() { }
            public virtual void StopCalc() { }
            public abstract void CalcBar();

            protected ITrendLineObject getTrendLine(int id)
            {
                foreach (ITrendLineObject line in m_studyControl.DrwTrendLine.GetTrendLineObjects(EDrawingSource.AnyTechOrManual))
                    if (line.ID == id) return line;
                return null;
            }

            /// <summary>
            /// Determine the current open position size.
            /// </summary>
            protected int currentOpenPositionSize()
            {
                return m_studyControl.StrategyInfo.MarketPosition;
            }

            /// <summary>
            /// Determine the current total position size based on both the open and closed trades of the position.
            /// </summary>
            protected int currentTotalPositionSize()
            {
                if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0;
                int size = 0;
                foreach (ITrade trade in m_studyControl.Positions[0].ClosedTrades)
                    size += trade.ExitOrder.Contracts;
                foreach (ITrade trade in m_studyControl.Positions[0].OpenTrades)
                    size += trade.EntryOrder.Contracts;
                return size;
            }

            /// <summary>
            /// Determines the cost basis of the current position.
            /// </summary>
            protected double currentPositionCostBasis()
            {
                if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0.0;
                double costBasis = 0.0;
                foreach (ITrade trade in m_studyControl.Positions[0].ClosedTrades)
                    costBasis += trade.EntryOrder.Price * trade.EntryOrder.Contracts;
                foreach (ITrade trade in m_studyControl.Positions[0].OpenTrades)
                    costBasis += trade.EntryOrder.Price * trade.EntryOrder.Contracts;
                return costBasis;
            }

            /// <summary>
            /// Determine the average entry price of the current position.
            /// </summary>
            protected double currentPositionAverageEntryPrice()
            {
                return m_studyControl.StrategyInfo.AvgEntryPrice;
            }

            protected double currentPositionPL()
            {
                if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0.0;
                return m_studyControl.Positions[0].Profit;
            }

            protected double currentPositionPLPercent()
            {
                if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0.0;
                double costBasis = currentPositionCostBasis();
                double pl = currentPositionPL();
                return costBasis != 0 ? pl / costBasis : 0;
            }
        }



        //TODO - Finish work around the supported entries and start testing the parsing/validation of each of the instructions.



        public abstract class Entry : Instruction
        {
            public EEntryDirection Direction { get; set; }
            public EOrderType EntryType { get; set; }
            public EEntryExitCriteria Criteria { get; set; }
            public double EquityPercent { get; set; }
            public bool AddToPosition { get; set; }
            public DateTime ValidUntil { get; set; }

            protected IOrderMarket m_orderLeMkt;
            protected IOrderMarket m_orderSeMkt;
            protected IOrderPriced m_orserLeStp;
            protected IOrderPriced m_orderSeStp;
            protected IOrderPriced m_orderLeLmt;
            protected IOrderPriced m_orderSeLmt;
            protected IOrderStopLimit m_orderLeStpLmt;
            protected IOrderStopLimit m_orderSeStpLmt;

            public Entry(_TrendLine control) : base(control)
            {
                Direction = EEntryDirection.Long;
                EntryType = EOrderType.Market;
                EquityPercent = 1.0;
                Criteria = EEntryExitCriteria.Close;
                AddToPosition = false;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "direction") { Direction = property.Value.ToObject<EEntryDirection>(); return true; }
                if (name == "entrytype") { EntryType = property.Value.ToObject<EOrderType>(); return true; }
                if (name == "equitypercent") { EquityPercent = property.Value.ToObject<double>(); return true; }
                if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
                if (name == "addtoposition") { AddToPosition = property.Value.ToObject<bool>(); return true; }
                if (name == "validuntil") { ValidUntil = property.Value.ToObject<DateTime>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, "Entry - EquityPercent must be between 0 and 1");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_orderLeMkt = null;
                m_orderSeMkt = null;
                m_orserLeStp = null;
                m_orderSeStp = null;
                m_orderLeLmt = null;
                m_orderSeLmt = null;
                m_orderLeStpLmt = null;
                m_orderSeStpLmt = null;

                switch (EntryType)
                {
                    case EOrderType.Market:
                        switch (Direction)
                        {
                            case EEntryDirection.Long:
                                m_orderLeMkt = m_studyControl.AllocateLeMrk();
                                break;
                            case EEntryDirection.Short:
                                m_orderSeMkt = m_studyControl.AllocateSeMrk();
                                break;
                        }
                        break;
                    case EOrderType.Stop:
                        switch (Direction)
                        {
                            case EEntryDirection.Long:
                                m_orserLeStp = m_studyControl.AllocateLeStp();
                                break;
                            case EEntryDirection.Short:
                                m_orderSeStp = m_studyControl.AllocateSeStp();
                                break;
                        }
                        break;
                    case EOrderType.Limit:
                        switch (Direction)
                        {
                            case EEntryDirection.Long:
                                m_orderLeLmt = m_studyControl.AllocateLeLmt();
                                break;
                            case EEntryDirection.Short:
                                m_orderSeLmt = m_studyControl.AllocateSeLmt();
                                break;
                        }
                        break;
                    case EOrderType.StopLimit:
                        switch (Direction)
                        {
                            case EEntryDirection.Long:
                                m_orderLeStpLmt = m_studyControl.AllocateLeStpLmt();
                                break;
                            case EEntryDirection.Short:
                                m_orderSeStpLmt = m_studyControl.AllocateSeStpLmt();
                                break;
                        }
                        break;
                }
            }

        }

        public abstract class Exit : Instruction
        {
            public EOrderType ExitType { get; set; }
            public double ExitPercent { get; set; }
            public DateTime ValidUntil { get; set; }

            protected IOrderPriced m_orderLx;
            protected IOrderPriced m_orderSx;
            protected IOrderStopLimit m_orderLxSL;
            protected IOrderStopLimit m_orderSxSL;

            public Exit(_TrendLine control) : base(control)
            {
                ExitType = EOrderType.Stop;
                ExitPercent = 1.0;
                ValidUntil = DateTime.MaxValue;
            }

            public override bool Parse(JProperty property)
            {
                if (base.Parse(property)) return true;
                bool result = false;
                string name = property.Name.ToLower();
                if (name == "exittype") { ExitType = property.Value.ToObject<EOrderType>(); result = true; }
                if (name == "exitpercent") { ExitPercent = property.Value.ToObject<double>(); result = true; }
                if (name == "validuntil") { ValidUntil = property.Value.ToObject<DateTime>(); result = true; }
                return result;
            }

            public override Tuple<bool, string> IsValid()
            {
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "Exit - ExitPercent must be between 0 and 1");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                m_orderLx = null;
                m_orderSx = null;
                m_orderLxSL = null;
                m_orderSxSL = null;

                switch (ExitType)
                {
                    case EOrderType.Stop:
                        m_orderLx = m_studyControl.AllocateLxStp();
                        m_orderSx = m_studyControl.AllocateSxStp();
                        break;
                    case EOrderType.Limit:
                        m_orderLx = m_studyControl.AllocateLxLmt();
                        m_orderSx = m_studyControl.AllocateSxLmt();
                        break;
                    case EOrderType.StopLimit:
                        m_orderLxSL = m_studyControl.AllocateLxStpLmt();
                        m_orderSxSL = m_studyControl.AllocateSxStpLmt();
                        break;
                }
            }

            protected int exitSize()
            {
                if (m_studyControl.PositionSide == 0) return 0;
                if (ExitPercent == 1.0) return currentOpenPositionSize();
                int size = (int)Math.Truncate(currentTotalPositionSize() * ExitPercent);
                return size;
            }
        }

        public class EntryLine : Entry
        {
            public int LineId { get; set; }
            public double BufferPrice { get; set; }
            public double ClosePercentile { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            private ITrendLineObject m_line;
            private IOrderPriced m_order;

            public EntryLine(_TrendLine control) : base(control)
            {
                BufferPrice = 0.0;
                ClosePercentile = 0.05;
                StartDate = DateTime.MinValue;
                EndDate = DateTime.MaxValue;
                StartTime = DateTime.MinValue;
                EndTime = DateTime.MaxValue;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "lineid") { LineId = property.Value.ToObject<int>(); return true; }
                if (name == "bufferprice") { BufferPrice = property.Value.ToObject<double>(); return true; }
                if (name == "closepercentile") { ClosePercentile = property.Value.ToObject<double>(); return true; }
                if (name == "startdate") { StartDate = property.Value.ToObject<DateTime>(); return true; }
                if (name == "enddate") { EndDate = property.Value.ToObject<DateTime>(); return true; }
                if (name == "starttime") { StartTime = property.Value.ToObject<DateTime>(); return true; }
                if (name == "endtime") { EndTime = property.Value.ToObject<DateTime>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_line == null) return Tuple.Create(false, string.Format("EntryLine - trendline with id {0} not found"));
                if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, "EntryLine - EntryPercent must be between 0 and 1");
                if (ClosePercentile < 0 || ClosePercentile > 1.0) return Tuple.Create(false, "EntryLine - ClosePercentile must be between 0 and 1");
                if (StartDate > EndDate) return Tuple.Create(false, "EntryLine - StartDate must be before EndDate");
                if (StartTime > EndTime) return Tuple.Create(false, "EntryLine - StartTime must be before EndTime");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                m_line = getTrendLine(LineId);
                if (Direction == EEntryDirection.Long)
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

                if (Direction == EEntryDirection.Long) 
                    longEntry();
                else 
                    shortEntry();
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
                        m_order.Send(entryPrice, m_studyControl.positionSize(m_studyControl.Bars, EquityPercent));
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
                        m_order.Send(entryPrice, m_studyControl.positionSize(m_studyControl.Bars, EquityPercent));
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

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "lineid") { LineId = property.Value.ToObject<int>(); return true; }
                if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_line == null) return Tuple.Create(false, string.Format("ExitLine - trendline with id {0} not found"));
                if (ExitType == EOrderType.Market) return Tuple.Create(false, "ExitLine - Market exit not supported");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_line = getTrendLine(LineId);
            }

            public override void CalcBar()
            {
                if (m_studyControl.PositionSide == 0) return;
                if (m_studyControl.Bars.CurrentBar < 2) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;
                if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
                if (m_studyControl.PositionSide > 0)
                    exitLong();
                else if (m_studyControl.PositionSide < 0)
                    exitShort();
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
                            if (ExitType == EOrderType.Stop)
                                m_orderLx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EOrderType.StopLimit)
                                m_orderLxSL.Send(m_studyControl.Bars.Close[0], m_studyControl.Bars.Close[0], positionSize);
                        }
                        break;
                    case EEntryExitCriteria.LowHigh:
                        if (m_studyControl.Bars.Low[0] < trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Long exit on low at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EOrderType.Stop)
                                m_orderLx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EOrderType.StopLimit)
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
                            if (ExitType == EOrderType.Stop)
                                m_orderSx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EOrderType.StopLimit)
                                m_orderSxSL.Send(m_studyControl.Bars.Close[0], m_studyControl.Bars.Close[0], positionSize);
                        }
                        break;
                    case EEntryExitCriteria.LowHigh:
                        if (m_studyControl.Bars.High[0] > trendLineValue)
                        {
                            if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitLine - Short exit on high at {0}, {1}", m_studyControl.Bars.Time[0], trendLineValue);
                            if (ExitType == EOrderType.Stop)
                                m_orderSx.Send(m_studyControl.Bars.Close[0], positionSize);
                            else if (ExitType == EOrderType.StopLimit)
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
                ExitType = EOrderType.Stop;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "length") { Length = property.Value.ToObject<int>(); return true; }
                if (name == "movingaveragetype") { Type = property.Value.ToObject<EMovingAverageType>(); return true; }
                if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (Length <= 0) return Tuple.Create(false, "ExitMovingAverage - Length must be greater than 0");
                if (ExitType == EOrderType.Market) return Tuple.Create(false, "ExitMovingAverage - Market exit not supported");
                return base.IsValid();
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

            private void exitAverage(ISeries<double> values)
            {
                int positionSize = m_studyControl.positionSize(m_studyControl.Bars, ExitPercent);
                if (m_studyControl.PositionSide > 0)
                {
                    if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - Long exit at {0}, {1}", m_studyControl.Bars.Time[0], values[0]);
                    if (ExitType == EOrderType.Stop)
                        m_orderLx.Send(values[0], positionSize);
                    else if (ExitType == EOrderType.StopLimit)
                        m_orderLxSL.Send(values[0], values[0], positionSize);
                }
                else if (m_studyControl.PositionSide < 0)
                {
                    if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - Short exit at {0}, {1}", m_studyControl.Bars.Time[0], values[0]);
                    if (ExitType == EOrderType.Stop)
                        m_orderSx.Send(values[0], positionSize);
                    else if (ExitType == EOrderType.StopLimit)
                        m_orderSxSL.Send(values[0], values[0], positionSize);
                }
            }
        }

        public class EntryZone : Entry
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public EntryZone(_TrendLine control) : base(control)
            {
                Direction = EEntryDirection.Long;
                Criteria = EEntryExitCriteria.Close;
                ValidUntil = DateTime.MaxValue;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "firstlineid") { FirstLineId = property.Value.ToObject<int>(); return true; }
                if (name == "secondlineid") { SecondLineId = property.Value.ToObject<int>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_firstLine == null) return Tuple.Create(false, string.Format("EntryZone - trendline with id {0} not found", FirstLineId));
                if (m_secondLine == null) return Tuple.Create(false, string.Format("EntryZone - trendline with id {0} not found", SecondLineId));
                return base.IsValid();
            }

            public override void StartCalc()
            {
                m_firstLine = getTrendLine(FirstLineId);
                m_secondLine = getTrendLine(SecondLineId);
            }

            public override void CalcBar()
            {
                //TODO
                throw new NotImplementedException();
            }
        };

        public class ExitZone : Exit
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }
            public EEntryExitCriteria Criteria { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public ExitZone(_TrendLine control) : base(control)
            {
                Criteria = EEntryExitCriteria.Close;
                ValidUntil = DateTime.MaxValue;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "firstlineid") { FirstLineId = property.Value.ToObject<int>(); return true; }
                if (name == "secondlineid") { SecondLineId = property.Value.ToObject<int>(); return true; }
                if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_firstLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", FirstLineId));
                if (m_secondLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", SecondLineId));
                if (ExitType == EOrderType.Market) return Tuple.Create(false, "ExitZone - Market exit not supported");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                m_firstLine = getTrendLine(FirstLineId);
                m_secondLine = getTrendLine(SecondLineId);
            }

            public override void CalcBar()
            {
                //TODO
                throw new NotImplementedException();
            }
        };

        /// <summary>
        /// Base class for priced exits used for stop/stop-limit orders at a determined price.
        /// </summary>
        public abstract class ExitPriced : Exit
        {
            public ExitPriced(_TrendLine control) : base(control) { }

            public override Tuple<bool, string> IsValid()
            {
                if (ExitType == EOrderType.Market) return Tuple.Create(false, "ExitPriced - Market exit not supported");
                return base.IsValid();
            }

            public override void CalcBar()
            {
                if (m_studyControl.PositionSide == 0) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;
                double price = exitPrice();
                if (m_studyControl.PositionSide > 0)
                {
                    switch (ExitType)
                    {
                        case EOrderType.Stop:
                            m_orderLx.Send(price, exitSize());
                            break;
                        case EOrderType.StopLimit:
                            m_orderLxSL.Send(price, price, exitSize());
                            break;
                    }
                }
                else if (m_studyControl.PositionSide < 0)
                {
                    switch (ExitType)
                    {
                        case EOrderType.Stop:
                            m_orderSx.Send(price, exitSize());
                            break;
                        case EOrderType.StopLimit:
                            m_orderSxSL.Send(price, price, exitSize());
                            break;
                    }
                }
            }

            /// <summary>
            /// Subclasses need to calculate the exit price per contract to exit the position.
            /// </summary>
            abstract protected double exitPrice();
        }

        public class ExitStopLoss : ExitPriced
        {
            public double LossPrice { get; set; }
            public double LossPercent { get; set; }

            public ExitStopLoss(_TrendLine control) : base(control)
            {
                LossPercent = -1.0;
                LossPrice = -1.0;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "losspercent") { LossPercent = property.Value.ToObject<double>(); return true; }
                if (name == "lossprice") { LossPrice = property.Value.ToObject<double>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (LossPercent == -1.0 && LossPrice == -1.0) return Tuple.Create(false, "ExitStopLoss - LossPercent or LossPrice must be specified");
                if (LossPrice != -1.0 && LossPrice <= 0) return Tuple.Create(false, "ExitStopLoss - LossPrice must be a positive number (will be negated by strategy)");
                if (LossPercent != -1.0 && (LossPercent <= 0.0 || LossPercent > 1.0)) return Tuple.Create(false, "ExitStopLoss - LossPercent must be between 0 and 1");
                return base.IsValid();
            }

            protected override double exitPrice()
            {
                double averageEntryPrice = currentPositionAverageEntryPrice();
                if (LossPrice != -1.0) return averageEntryPrice - LossPrice;
                return averageEntryPrice - (averageEntryPrice * (1 - LossPercent));
            }
        };

        public class ExitProfit : ExitPriced
        {
            public double ProfitPercent { get; set; }
            public double ProfitPrice { get; set; }

            public ExitProfit(_TrendLine control) : base(control)
            {
                ProfitPercent = -1.0;
                ProfitPrice = -1.0;
            }

            public override bool Parse(JProperty property)
            {
                string name = property.Name.ToLower();
                if (name == "profitpercent") { ProfitPercent = property.Value.ToObject<double>(); return true; }
                if (name == "profitprice") { ProfitPrice = property.Value.ToObject<double>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (ProfitPercent == -1.0 && ProfitPrice == -1.0) return Tuple.Create(false, "ExitProfit - ProfitPercent or ProfitPrice must be specified");
                if (ProfitPercent != -1.0 && (ProfitPercent <= 0.0 || ProfitPercent > 1.0)) return Tuple.Create(false, "ExitProfit - ProfitPercent must be between 0 and 1");
                return base.IsValid();
            }

            protected override double exitPrice()
            {
                double averageEntryPrice = currentPositionAverageEntryPrice();
                if (ProfitPrice != -1.0) return averageEntryPrice + ProfitPrice;
                return averageEntryPrice * (1 + ProfitPercent);
            }
        };

        /// <summary>
        /// Exit at the market close after a certain number of days.
        /// </summary>
        public class ExitDays : Exit
        {
            public int Count { get; set; }

            protected new IOrderMarket m_orderLx;
            protected new IOrderMarket m_orderSx;

            public ExitDays(_TrendLine control) : base(control)
            {
                Count = 1;
            }

            public override bool Parse(JProperty property)
            {
                if (property.Name.ToLower() == "count") { Count = property.Value.ToObject<int>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (Count <= 0) return Tuple.Create(false, "ExitDays - Count must be larger than 0");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_orderLx = m_studyControl.AllocateLxMrk();
                m_orderSx = m_studyControl.AllocateSxMrk();
            }

            public override void CalcBar()
            {
                if (m_studyControl.PositionSide == 0) return;
                if (m_studyControl.Bars.Time[0] > ValidUntil) return;

                DateTime entryDate = m_studyControl.Positions[0].OpenTrades.Min(trade => trade.EntryOrder.Time);
                entryDate = m_studyControl.Positions[0].ClosedTrades.Min(trade => trade.EntryOrder.Time);
                TimeSpan days = m_studyControl.Bars.Time[0] - entryDate;

                if (days.Days > Count)
                    if (m_studyControl.PositionSide > 0)
                    {
                        if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitDays - Long exit at {0}", m_studyControl.Bars.Time[0]);
                        m_orderLx.Send(exitSize());
                    }
                    else if (m_studyControl.PositionSide < 0)
                    {
                        if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitDays - Short exit at {0}", m_studyControl.Bars.Time[0]);
                        m_orderSx.Send(exitSize());
                    }
            }
        };

        public class ExitEntryBarLowHigh : ExitPriced
        {
            public double BufferPrice { get; set; }

            private double m_stopLossPrice;

            public ExitEntryBarLowHigh(_TrendLine control) : base(control)
            {
                BufferPrice = 0.0;
            }

            public override bool Parse(JProperty property)
            {
                if (property.Name.ToLower() == "bufferprice") { BufferPrice = property.Value.ToObject<double>(); return true; }
                return base.Parse(property);
            }

            public override Tuple<bool, string> IsValid()
            {
                if (BufferPrice < 0) return Tuple.Create(false, "ExitEntryBarLowHigh - BufferPrice must be greater than or equal to 0 (instruction will invert the sign if required)");
                if (ExitType == EOrderType.Market) return Tuple.Create(false, "ExitEntryBarLowHigh - Market exit not supported");
                return base.IsValid();
            }

            public override void StartCalc()
            {
                base.StartCalc();
                m_stopLossPrice = -1.0;
            }

            public override void CalcBar()
            {
                if (m_studyControl.PositionSide == 0) 
                    m_stopLossPrice = -1.0;     //reset stop-loss price when we position went flat
                else if (m_stopLossPrice == -1.0)
                {
                    if (m_studyControl.PositionSide > 0)
                        m_stopLossPrice = m_studyControl.Bars.Low[0] - BufferPrice;
                    else if (m_studyControl.PositionSide < 0)
                        m_stopLossPrice = m_studyControl.Bars.High[0] + BufferPrice;
                }

                base.CalcBar();
            }

            protected override double exitPrice()
            {
                return m_stopLossPrice;
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
        private List<IOrderMarket> m_leMrk;
        private int m_leMrkAllocated;
        private List<IOrderMarket> m_seMrk;
        private int m_seMrkAllocated;
        private List<IOrderPriced> m_leStp;
        private int m_leStpAllocated;
        private List<IOrderPriced> m_seStp;
        private int m_seStpAllocated;
        private List<IOrderPriced> m_leLmt;
        private int m_leLmtAllocated;
        private List<IOrderPriced> m_seLmt;
        private int m_seLmtAllocated;
        private List<IOrderStopLimit> m_seStpLmt;
        private int m_seStpLmtAllocated;
        private List<IOrderStopLimit> m_sxStpLmt;
        private int m_sxStpLmtAllocated;

        private List<IOrderMarket> m_lxMrk;
        private int m_lxMrkAllocated;
        private List<IOrderMarket> m_sxMrk;
        private int m_sxMrkAllocated;
        private List<IOrderPriced> m_lxStp;
        private int m_lxStpAllocated;
        private List<IOrderPriced> m_sxStp;
        private int m_sxStpAllocated;
        private List<IOrderPriced> m_lxLmt;
        private int m_lxLmtAllocated;
        private List<IOrderPriced> m_sxLmt;
        private int m_sxLmtAllocated;
        private List<IOrderStopLimit> m_leStpLmt;
        private int m_leStpLmtAllocated;
        private List<IOrderStopLimit> m_lxStpLmt;
        private int m_lxStpLmtAllocated;

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
            parseInstructions();
            foreach (var instruction in m_instructions) instruction.StartCalc();
            validateInstructions();
        }

        protected override void StopCalc()
        {
            foreach (var instruction in m_instructions) instruction.StopCalc();
        }

        protected override void CalcBar()
        {
            foreach (var instruction in m_instructions) instruction.CalcBar();
        }

        public IOrderMarket AllocateLeMrk()
        {
            if (m_leMrkAllocated >= m_leMrk.Count) throw new InvalidOperationException("No more Long Market entry orders available");
            IOrderMarket result = m_leMrk[m_leMrkAllocated];
            m_leMrkAllocated++;
            return result;
        }

        public IOrderMarket AllocateSeMrk()
        {
            if (m_seMrkAllocated >= m_seMrk.Count) throw new InvalidOperationException("No more Short Market entry orders available");
            IOrderMarket result = m_seMrk[m_seMrkAllocated];
            m_seMrkAllocated++;
            return result;
        }

        public IOrderPriced AllocateLeStp()
        {
            if (m_leStpAllocated >= m_leStp.Count) throw new InvalidOperationException("No more Long Stop orders available");
            IOrderPriced result = m_leStp[m_leStpAllocated];
            m_leStpAllocated++;
            return result;
        }

        public IOrderPriced AllocateSeStp()
        {
            if (m_seStpAllocated >= m_seStp.Count) throw new InvalidOperationException("No more Short Stop orders available");
            IOrderPriced result = m_seStp[m_seStpAllocated];
            m_seStpAllocated++;
            return result;
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

        public IOrderMarket AllocateLxMrk()
        {
            if (m_lxMrkAllocated >= m_lxMrk.Count) throw new InvalidOperationException("No more Long Market exit orders available");
            IOrderMarket result = m_lxMrk[m_lxMrkAllocated];
            m_lxMrkAllocated++;
            return result;
        }

        public IOrderMarket AllocateSxMrk()
        {
            if (m_sxMrkAllocated >= m_sxMrk.Count) throw new InvalidOperationException("No more Short Market exit orders available");
            IOrderMarket result = m_sxMrk[m_sxMrkAllocated];
            m_sxMrkAllocated++;
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

        public IOrderPriced AllocateLxLmt()
        {
            if (m_lxLmtAllocated >= m_lxLmt.Count) throw new InvalidOperationException("No more Long Exit Limit orders available");
            IOrderPriced result = m_lxLmt[m_lxLmtAllocated];
            m_lxLmtAllocated++;
            return result;
        }

        public IOrderPriced AllocateSxLmt()
        {
            if (m_sxLmtAllocated >= m_sxLmt.Count) throw new InvalidOperationException("No more Short Exit Limit orders available");
            IOrderPriced result = m_sxLmt[m_sxLmtAllocated];
            m_sxLmtAllocated++;
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
            m_leMrk = new List<IOrderMarket>();
            m_seMrk = new List<IOrderMarket>();
            m_leStp = new List<IOrderPriced>();
            m_seStp = new List<IOrderPriced>();
            m_leLmt = new List<IOrderPriced>();
            m_seLmt = new List<IOrderPriced>();
            m_leStpLmt = new List<IOrderStopLimit>();
            m_seStpLmt = new List<IOrderStopLimit>();

            m_lxMrk = new List<IOrderMarket>();
            m_sxMrk = new List<IOrderMarket>();
            m_lxStp = new List<IOrderPriced>();
            m_sxStp = new List<IOrderPriced>();
            m_lxLmt = new List<IOrderPriced>();
            m_sxLmt = new List<IOrderPriced>();
            m_lxStpLmt = new List<IOrderStopLimit>();
            m_sxStpLmt = new List<IOrderStopLimit>();

            for (int i = 0; i < MAX_INSTRUCTIONS; i++)
            {
                m_leMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LEMrk" + i, EOrderAction.Buy)));
                m_seMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SEMrk" + i, EOrderAction.SellShort)));
                m_leStp.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LEStp" + i, EOrderAction.Buy)));
                m_seStp.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SEStp" + i, EOrderAction.SellShort)));
                m_leLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LELmt" + i, EOrderAction.Buy)));
                m_seLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SELmt" + i, EOrderAction.SellShort)));
                m_leStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LEStpLmt" + i, EOrderAction.Buy)));
                m_seStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SEStpLmt" + i, EOrderAction.SellShort)));

                m_lxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LXMrk" + i, EOrderAction.Sell)));
                m_sxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SXMrk" + i, EOrderAction.BuyToCover)));
                m_lxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LXStp" + i, EOrderAction.Sell)));
                m_sxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SXStp" + i, EOrderAction.BuyToCover)));
                m_lxLmt.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LXLmt" + i, EOrderAction.Sell)));
                m_sxLmt.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SXLmt" + i, EOrderAction.BuyToCover)));
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
            m_leMrkAllocated = 0;
            m_seMrkAllocated = 0;
            m_leStpAllocated = 0;
            m_seStpAllocated = 0;
            m_leLmtAllocated = 0;
            m_seLmtAllocated = 0;
            m_leStpLmtAllocated = 0;
            m_seLmtAllocated = 0;

            m_lxMrkAllocated = 0;
            m_sxMrkAllocated = 0;
            m_lxStpAllocated = 0;
            m_sxStpAllocated = 0;
            m_lxLmtAllocated = 0;
            m_sxLmtAllocated = 0;
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

        private void parseInstructions()
        {
            m_instructions.Clear();
            JArray instructions = (JArray)JToken.Parse(Instructions);

            foreach (var property in instructions)
            {
                var objectProperty = (JObject)property;
                string instructionStr = "";
                foreach (var prop in objectProperty.Properties())
                {
                    if (prop.Name.ToLower() == "instruction")
                    {
                        instructionStr = prop.Value.ToString().ToLower();
                        break;
                    }
                }

                if (instructionStr == "") throw new ArgumentException("Instruction type not found");
                var instruction = InstructionFactory.CreateInstruction(instructionStr, this);
                instruction.Parse(property);
                m_instructions.Add(instruction);
            }
        }

        private void validateInstructions()
        {
            foreach (var instruction in m_instructions)
            {
                var result = instruction.IsValid();
                if (!result.Item1) throw new ArgumentException(result.Item2);
            }
        }
    }
}