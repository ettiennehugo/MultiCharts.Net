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
    /// 	• LineId - trend line id
    ///		• [optional] PositionPercent - percent of POSITION to close, default 1.0 (100%)
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
    ///		• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    /// 
    /// EntryZone
    ///		• FirstLineId - first trend line used to define the zone
    ///		• SecondLineId - second trend line used to define the zone
    ///		• EntryType - enum of Long, Short
    ///		• EquityPercent - percent of the ACCOUNT size to enter into the position
    ///		• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
    ///		• TBD[optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
    /// 
    /// ExitZone
    ///		• FirstLineId - first trend line used to define the zone
    ///		• SecondLineId - second trend line used to define the zone
    ///		• [optional] Criteria - enum of Close, LowHigh count the entry met when the bar closes in the zone vs whether the price just entered the zone via the high/low 
    ///		• [optional] PositionPercent - percent of POSITION to close, default 1.0 (100%)
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    ///		• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
    ///
    /// ExitStopLoss
    ///		• Percent - exit the position if the price moves against you by the specified percentage.
    ///		• [optional] PositionPercent - percent of the POSITION to exit, default 1.0 (100%)
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitProfit
    ///		• Percent - exit the position one the price moved in your favor by this percentage
    ///		• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%)
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitDays
    /// 	• Count - number of days after which the position should be closed
    ///		• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%) 
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
    /// 
    /// ExitEntryBarLowHigh
    ///		• [optional] BufferPrice - buffer price to add/subtract from entry bar extreme
    ///		• [optional] PositionPercent - percent of POSITION to exit, default 1.0 (100%)
    ///		• [optional] ExitType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
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

        public enum EEntryCriteria
        {
            Close,
            LowHigh
        };

        public abstract class Instruction
        {
            public _TrendLine m_studyControl;

            public Instruction(_TrendLine control) { m_studyControl = control; }
            public abstract void Parse(JToken token);
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

        public class EntryLine : Instruction
        {
            public int LineId { get; set; }
            public EEntryType EntryType { get; set; }
            public double EntryPercent { get; set; }
            public EEntryCriteria Criteria { get; set; }
            public bool EntryPercentRoundingUp { get; set; }
            public double BufferPrice { get; set; }
            public double ClosePercentile { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public DateTime ValidUntil { get; set; }
            public bool AddToPosition { get; set; }

            private ITrendLineObject m_line;

            public EntryLine(_TrendLine control) : base(control)
            {
                EntryPercent = 1.0;
                Criteria = EEntryCriteria.Close;
                EntryPercentRoundingUp = false;
                BufferPrice = 0.0;
                ClosePercentile = 0.05;
                StartDate = DateTime.MinValue;
                EndDate = DateTime.MaxValue;
                StartTime = DateTime.MinValue;
                EndTime = DateTime.MaxValue;
                AddToPosition = false;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "entrytype") EntryType = prop.Value.ToObject<EEntryType>();
                    if (prop.Name.ToLower() == "equitypercent") EntryPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "criteria") Criteria = prop.Value.ToObject<EEntryCriteria>();
                    if (prop.Name.ToLower() == "entrypercentroundingup") EntryPercentRoundingUp = prop.Value.ToObject<bool>();
                    if (prop.Name.ToLower() == "bufferprice") BufferPrice = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "closepercentile") ClosePercentile = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "startdate") StartDate = prop.Value.ToObject<DateTime>();
                    if (prop.Name.ToLower() == "enddate") EndDate = prop.Value.ToObject<DateTime>();
                    if (prop.Name.ToLower() == "starttime") StartTime = prop.Value.ToObject<DateTime>();
                    if (prop.Name.ToLower() == "endtime") EndTime = prop.Value.ToObject<DateTime>();
                    if (prop.Name.ToLower() == "validuntil") ValidUntil = prop.Value.ToObject<DateTime>();
                    if (prop.Name.ToLower() == "addtoposition") AddToPosition = prop.Value.ToObject<bool>();
                }
            }

            public override void StartCalc()
            {
                m_line = getTrendLine(LineId);
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
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
        };

        public class ExitLine : Instruction
        {
            public int LineId { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }
            public DateTime ValidUntil { get; set; }

            private ITrendLineObject m_line;

            public ExitLine(_TrendLine control) : base(control)
            {
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
                ValidUntil = DateTime.MaxValue;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "lineid") LineId = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                    if (prop.Name.ToLower() == "validuntil") ValidUntil = prop.Value.ToObject<DateTime>();
                }
            }

            public override void StartCalc()
            {
                m_line = getTrendLine(LineId);
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (m_line == null) return Tuple.Create(false, string.Format("ExitLine - trendline with id {0} not found"));
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "ExitLine - ExitPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class EntryZone : Instruction
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }
            public EOrderAction EntryType { get; set; }
            public double EntryPercent { get; set; }
            public EEntryCriteria Criteria { get; set; }
            public bool AddToPosition { get; set; }
            public DateTime ValidUntil { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public EntryZone(_TrendLine control) : base(control)
            {
                Criteria = EEntryCriteria.Close;
                AddToPosition = false;
                ValidUntil = DateTime.MaxValue;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "firstlineid") FirstLineId = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "secondlineid") SecondLineId = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "entrytype") EntryType = prop.Value.ToObject<EOrderAction>();
                    if (prop.Name.ToLower() == "entrypercent") EntryPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "criteria") Criteria = prop.Value.ToObject<EEntryCriteria>();
                    if (prop.Name.ToLower() == "addtoposition") AddToPosition = prop.Value.ToObject<bool>();
                    if (prop.Name.ToLower() == "validuntil") ValidUntil = prop.Value.ToObject<DateTime>();
                }
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

        public class ExitZone : Instruction
        {
            public int FirstLineId { get; set; }
            public int SecondLineId { get; set; }
            public EEntryCriteria Criteria { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }
            public DateTime ValidUntil { get; set; }

            private ITrendLineObject m_firstLine;
            private ITrendLineObject m_secondLine;

            public ExitZone(_TrendLine control) : base(control)
            {
                Criteria = EEntryCriteria.Close;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
                ValidUntil = DateTime.MaxValue;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "firstlineid") FirstLineId = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "secondlineid") SecondLineId = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "criteria") Criteria = prop.Value.ToObject<EEntryCriteria>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                    if (prop.Name.ToLower() == "validuntil") ValidUntil = prop.Value.ToObject<DateTime>();
                }
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
                if (m_firstLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", FirstLineId));
                if (m_secondLine == null) return Tuple.Create(false, string.Format("ExitZone - trendline with id {0} not found", SecondLineId));
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "ExitZone - ExitPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitStopLoss : Instruction
        {
            public double LossPrice { get; set; }
            public double LossPercent { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }

            public ExitStopLoss(_TrendLine control) : base(control)
            {
                LossPercent = -1.0;
                LossPrice = -1.0;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "losspercent") LossPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "lossprice") LossPrice = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                }
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (LossPercent == -1.0 && LossPrice == -1.0) return Tuple.Create(false, "ExitStopLoss - LossPercent or LossPrice must be specified");
                if (LossPercent != -1.0 && (LossPercent <= 0.0 || LossPercent > 1.0)) return Tuple.Create(false, "ExitStopLoss - LossPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitProfit : Instruction
        {
            public double ProfitPercent { get; set; }
            public double ProfitPrice { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }

            public ExitProfit(_TrendLine control) : base(control)
            {
                ProfitPercent = -1.0;
                ProfitPrice = -1.0;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "profitpercent") ProfitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "profitprice") ProfitPrice = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                }
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

        public class ExitDays : Instruction
        {
            public int Count { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }

            public ExitDays(_TrendLine control) : base(control)
            {
                Count = 0;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "count") Count = prop.Value.ToObject<int>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                }
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (Count <= 0) return Tuple.Create(false, "ExitDays - Count must be greater than 0");
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "ExitDays - ExitPercent must be between 0 and 1");
                return Tuple.Create(true, string.Empty);
            }
        };

        public class ExitEntryBarLowHigh : Instruction
        {
            public double BufferPrice { get; set; }
            public double ExitPercent { get; set; }
            public EExitType ExitType { get; set; }

            public ExitEntryBarLowHigh(_TrendLine control) : base(control)
            {
                BufferPrice = 0.0;
                ExitPercent = 1.0;
                ExitType = EExitType.Stop;
            }

            public override void Parse(JToken token)
            {
                foreach (var child in token.Children())
                {
                    if (child.Type != JTokenType.Property) continue;
                    var prop = (JProperty)child;
                    if (prop.Name.ToLower() == "bufferprice") BufferPrice = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exitpercent") ExitPercent = prop.Value.ToObject<double>();
                    if (prop.Name.ToLower() == "exittype") ExitType = prop.Value.ToObject<EExitType>();
                }
            }

            public override void CalcBar()
            {
                throw new NotImplementedException();
            }

            public override Tuple<bool, string> IsValid()
            {
                if (BufferPrice < 0) return Tuple.Create(false, "ExitEntryBarLowHigh - BufferPrice must be greater than or equal to 0");
                if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, "ExitEntryBarLowHigh - ExitPercent must be between 0 and 1");
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
        public IOrderPriced m_leLmt;
        public IOrderPriced m_lxLmt;
        public IOrderStopLimit m_leStpLmt;
        public IOrderStopLimit m_lxStpLmt;
        public IOrderPriced m_seLmt;
        public IOrderPriced m_sxLmt;
        public IOrderStopLimit m_seStpLmt;
        public IOrderStopLimit m_sxStpLmt;
        protected List<Instruction> m_instructions;

        //interface methods
        public _TrendLine(object _ctx) : base(_ctx) { }

        protected override void Create()
        {
            Debug = false;

            m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LELmt", EOrderAction.Buy));
            m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SELmt", EOrderAction.SellShort));
            m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXLmt", EOrderAction.Sell));
            m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXLmt", EOrderAction.BuyToCover));
            m_leStpLmt = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LEStpLmt", EOrderAction.Buy));
            m_seStpLmt = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SEStpLmt", EOrderAction.SellShort));
            m_lxStpLmt = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LXStpLmt", EOrderAction.Sell));
            m_sxStpLmt = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SXStpLmt", EOrderAction.BuyToCover));
            m_instructions = new List<Instruction>();
        }

        protected override void StartCalc()
        {
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

        //methods
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