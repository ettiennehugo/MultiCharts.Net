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
	///	• LineId - trend line id(error check this)
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
	///	• TBD [optional]
	///		AddToPosition - if the trend line is crossed and we are already in a position add to the position
	///  
	/// ExitLine
	/// 	• LineId - trend line id (error check this - allow entry and exit to be on the same trend line)
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

		
		//TODO 
		// - implement a base class Instruction with a method Tuple<bool, string> Validate() to perform run-time validation of parsed input.
		// - implement parsing
		// - implement general validation to validate the whole instruction collection
		
		
		public class EntryLine
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
			
			public EntryLine() {
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
		};

		public class ExitLine
		{
			public int LineId { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			public DateTime ValidUntil { get; set; }
			
			public ExitLine() {
				ExitPercent = 1.0;
				ExitType = EExitType.Stop;
				ValidUntil = DateTime.MaxValue;
			}
		};

		public class EntryZone
		{
			public int FirstLineId { get; set; }
			public int SecondLineId { get; set; }
			public EOrderAction EntryType { get; set; }
			public double EntryPercent { get; set; }
			public EEntryCriteria Criteria { get; set; }
			public bool AddToPosition { get; set; }
			public DateTime ValidUntil { get; set; }
			
			public EntryZone() {
				Criteria = EEntryCriteria.Close;
				AddToPosition = false;
				ValidUntil = DateTime.MaxValue;
			}
		};

		public class ExitZone
		{
			public int FirstLineId { get; set; }
			public int SecondLineId { get; set; }
			public EEntryCriteria Criteria { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			public DateTime ValidUntil { get; set; }
			
			public ExitZone() {
				Criteria = EEntryCriteria.Close;
				ExitPercent = 1.0;
				ExitType = EExitType.Stop;
				ValidUntil = DateTime.MaxValue;
			}
		};

		public class ExitStopLoss
		{
			public double LossPrice { get; set; }
			public double LossPercent { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			
			public ExitStopLoss() {
				LossPercent = -1.0;
				LossPrice = -1.0;
				ExitPercent = 1.0;
				ExitType = EExitType.Stop;
			}
		};

		public class ExitProfit
		{
			public double ProfitPercent { get; set; }
			public double ProfitPrice { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			
			public ExitProfit() {
				ProfitPercent = -1.0;
				ProfitPrice = -1.0;
				ExitPercent = 1.0;
				ExitType = EExitType.Stop;
			}
		};

		public class ExitDays
		{
			public int Count { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			
			public ExitDays() {
				Count = 0;
				ExitPercentage = 1.0;
				ExitType = EExitType.Stop;
			}
		};

		public class ExitEntryBarLowHigh
		{
			public double BufferPrice { get; set; }
			public double ExitPercent { get; set; }
			public EExitType ExitType { get; set; }
			
			public ExitEntryBarLowHigh() {
				BufferPrice = 0.0;
				ExitPercent = 1.0;
				ExitType = EExitType.Stop;
			}
		};

		//inputs
		[Input] public string Instructions { get; set; }
		[Input] public bool Debug { get; set; }

		//properties


		//attributes
		private IOrderPriced m_leLmt;
		private IOrderPriced m_lxLmt;
		private IOrderPriced m_leStpLmt;
		private IOrderPriced m_lxStpLmt;
		private IOrderPriced m_seLmt;
		private IOrderPriced m_sxLmt;
		private IOrderPriced m_seStpLmt;
		private IOrderPriced m_sxStpLmt;

		//interface methods
		public _TrendLine(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Debug = false;

			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LELmt", EOrderAction.Buy));
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SELmt", EOrderAction.SellShort));
			m_lx = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXLmt", EOrderAction.Sell));
			m_sx = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXLmt", EOrderAction.BuyToCover));
			m_leStpLmt = OrderCategory.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LEStpLmt", EOrderAction.Buy));
			m_seStpLmt = OrderCategory.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SEStpLmt", EOrderAction.SellShort));
			m_lxStpLmt = OrderCategory.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LXStpLmt", EOrderAction.Sell));
			m_sxStpLmt = OrderCategory.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SXStpLmt", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//try to parse the instructions



		}

		protected override void CalcBar()
		{





		}

		//methods

	}
}