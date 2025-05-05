using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Drawing;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Manages entry and exit signals based on trendlines.
	/// 
	/// Supports the following:
	/// - Takes a set of instructions defined in JSON tied to the trendlines defined on the chart to trade against the price levels defined by the trendlines.
	/// - Allows using two trendlines together to define entry/exit zones.
	/// - Allows stop-loss and take-profit orders for trade management.
	/// 
	/// Instructions:
	/// EntryLine
	/// • Instruction - "EntryLine" 
	///	• [alternate] Line start/end
	///			LineStartTime - start date/time of the line
	///			LineStartPrice - start price of the line
	///			LineEndTime - end date/time of the line
	///			LineEndPrice - end price of the line
	///	• [alternate] LineId - trend line id 
	///	• Direction - enum of long, short, default long
	///	• EquityPercent - percent of the ACCOUNT equity to enter into the position
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	/// • [optional] Stategy - enum of Breakout, Fade, BreakoutOrFade - determines the strategy to use for the entry, defaults to Breakout
	///	• [optional] MaxVolumeBasedPercent - percent of the average volume to use as the maximum position size, default 0.2 (20%), set to 0.0 to disable.
	///	• [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	///	• [optional] ClosePercentile - (only used when Criteria == Close) double value between 0.0 and 1.0, e.g. 0.05 would mean that for a long trade the close price must be within 5% of the high of the bar
	///	                               while for short trades it would mean the close price must be within 5% of the low of the bar - default value is 0.5 (50%) meaning for long trades the close price must be
	///	                               in the top half of the bar and for short trades the close price must be in the bottom half of the bar
	///	• [optional] TimeFilters - Set of start/end time/entry count filters on which the entry should be checked, e.g. 9:35 AM start to 10:00 AM end would look for a single entry (when EntryCount is 1 the default value) the first half hour of the regular stock trading day
	///	• [optional] ValidUntil - date/time until which the entry should hold, defaults to DateTime.Max (if this field is specified with the EndDate/EndTime fields the entry would become disabled which ever date/time comes first)
	///	• [optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
	///	• [optional] StopLossExitId - Creates a risk based entry in conjunction with the given exit stop loss Id (blank to disable risk based entry), e.g. when EquityPercent is 0.01 (1%) the position size would risk 1%
	///	                              of the account equity based on the entry price and the stop-loss price.
	///	• [optional] MaxRiskBasedEquityPercent - When a risk based entry is used (see StopLossExitId) the maximum percent of the account equity to use for the position as risk based position entries can become really big,
	///	                                         default is 1.0 (100%) of the account.
	///	• [optional] VolumeDataFeed - integer for the data feed to use to compute the volume, default is 1
	///	• [optional] VolumeLength - length of the volume moving average, default is 20	
	///	• [optional] VolumeRequired - current volume required above the average volume to consider for a high volume entry, default is Disable
	/// • [optional] UseRoundLots - use round lot orders (multiples of 100) to facilitate better market fills for larger accounts, default false 
	/// 
	/// ExitLine
	/// • Instruction - "ExitLine" 
	///	• [alternate] Line start/end
	///			LineStartTime - start date/time of the line
	///			LineStartPrice - start price of the line
	///			LineEndTime - end date/time of the line
	///			LineEndPrice - end price of the line
	///	• [alternate] LineId - trend line id 
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	///	• [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
	/// 
	/// ExitMovingAverage
	/// • Instruction - "ExitMovingAverage" 
	/// • Length - length of the moving average.
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	/// • [optional] MovingAverageType - enum Simple, Exponential, SuperSmoother, default is Simple
	/// • [optional] DataFeed - integer for the data feed to use to compute the moving average, default is 1 
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
	///
	/// ExitOverextension
	/// • Instruction - "ExitOverextension"
	/// • Length - length of the moving average
	/// • [optional] ZScoreThreshold - Standard deviation threshold to use for the overextension, default 2.0, e.g. 2.0 would mean that the price is 2 standard deviations away from the moving average
	/// • [optional] StdDevLength - Standard deviation length to use for the overextension, default 100 (assuming 5-minute bars)
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	/// • [optional] MovingAverageType - enum Simple, Exponential, SuperSmoother, default is Simple
	/// • [optional] DataFeed - integer for the data feed to use to compute the moving average and ATR, default is 1 
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max 
	///
	/// ExitStopLoss
	/// • Instruction - "ExitStopLoss" 
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	///	• [alternate] LossPercent - exit the position if the price moves against you by the specified percentage.
	///	• [alternate] LossPrice - exit the position if the price moves against you by a specified amount. 
	///	• [alternate] Price - exit the position at the specific price value.
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitProfit
	/// • Instruction - "ExitProfit" 
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	///	• [alternate] ProfitPercent - exit the position one the price moved in your favor by this percentage
	///	• [alternate] ProfitPrice - exit the position one the price moved in your favor by this amount
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitDays
	/// • Instruction - "ExitDays" 
	/// • Count - number of days after which the position should be closed
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	/// • [optional] Strategy - enum CalendarDays, TradingDays - whether to count calendar days or trading days, defaults to TradingDays
	///	• [optional] ExitPercent - percent of INITIAL POSITION to exit, default 1.0 (100%) 
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitEntryBarLowHigh
	/// • Instruction - "ExitEntryBarLowHigh" 
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	///	• [optional] BufferPrice - buffer price to add/subtract from entry bar extreme
	///	• [optional] PositionPercent - percent of INITIAL POSITION to exit, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	///	
	/// ExitEntryDayLowHigh
	/// • Instruction - "ExitDayLowHigh"
	/// • [optional] Id - identifier for the instruction to reference it in other instructions	
	///	• [optional] BufferPrice - buffer price to add/subtract from entry day extreme
	///	• [optional] PositionPercent - percent of INITIAL POSITION to exit, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	///	• [optional] IncludePreMarket - include pre-market data when computing the entry day high/low, default false
	///	• [optional] IncludePostMarket - include post-market data when computing the entry day high/low, default false
	/// 
	/// ExitTrailingStop
	/// • Instruction - "ExitTrailingStop"
	/// • TrailingType - enum of FixedAmount, Percentage, ATR - determines the type of trailing stop to use, default FixedAmount
	/// • [alternate] ActivationProfitPercent - profit percentage to activate the trailing stop
	/// • [alternate] ActivationProfitPrice - profit price to activate the trailing stop 
	/// • [optional] Criteria - enum of Close, LowHigh - activate trailing stop based on bar close or bar low/high, defaults to Close.
	/// • [alternate on TrailingType] TrailingValue - for FixedAmount type this is a dollar amount and for Percentage this is a percent value, e.g. 0.15 for 15%
	/// • [alternate on TrailingType] AtrLength - for ATR type this is the length of the ATR to use with TrailingType is set to ATR
	/// 
	/// ExitBreakeven
	/// • Instruction - "ExitBreakeven", always exits the whole position.
	/// • [alternate] ActivationProfitPercent - profit percentage to activate the trailing stop
	/// • [alternate] ActivationProfitAmount - profit amount to activate the trailing stop 
	/// 
	/// Example JSON:
	/// [
	///	    {
	///     "Instruction" : "EntryLine"
	///			"LineId" : 1,
	///			"Direction" : "Long", 
	///			"EquityPercent" : 0.01,
	///			"BufferPrice" : 0.05,
	///			"ClosePercentile" : 0.75,
	///			"StartTime" : "9:35 AM",
	///			"EndTime" : "9:35 AM"
	///		},
	///		{
	///	        "Instruction" : "EntryLine"
	///			"LineId" : 2,
	///			"Direction" : "Short", 
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
	///			"ProfitPrice": 5.0
	///		},
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
		/// Maximum number of long, short type entries/exit supported, number of moving averages supported and default volume length period.
		/// </summary>
		public const int MAX_INSTRUCTIONS = 10;
		public const int MAX_MOVINGAVERAGES = 5;
		public const int MAX_ATR = 5;
		public const int MAX_VOLUME_WITH_SD = 5;
		public const int DEFAULT_VOLUME_PERIOD = 20;

		//types
		public struct TimeFilter
		{
			public DateTime StartTime;
			public DateTime EndTime;
			public int Count; //homw many times can the entry/exit occur during the time filter
		};

		public enum EEntryDirection
		{
			Long,
			Short,
		};

		public enum EEntryStrategy
		{
			Breakout,
			Fade,
			BreakoutOrFade
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
			SuperSmoother,
		};

		public enum EExitDaysCountStrategy
		{
			CalendarDays,
			TradingDays
		};

		public enum EVolumeRequired
		{
			Disable,
			AboveAverage,     //volume required should be above average
			AboveAverageSD1,  //volume required should be above average + 1 standard deviation
			AboveAverageSD2,  //volume required should be above average + 2 standard deviations
			AboveAverageSD3   //volume required should be above average + 3 standard deviations
		};

		internal abstract class Instruction
		{
			public _TrendLine m_studyControl;

			public string Name { get; private set; }
			public string Code { get; private set; }	//snippet of code that created the instruction (mainly for debugging purposes)
			public string Id { get; private set; }
			public int Index { get; }
			public string Symbol { get { return m_studyControl.Bars.Info.Name; } }			
			public string InstructionTypeName { get { return this.GetType().Name; } }
			public List<TimeFilter> TimeFilters { get; set; }
			private bool m_previousBarPositionWasFlat;
			private int m_entryCount;

			public Instruction(_TrendLine control)
			{
				m_studyControl = control;
				Name = string.Empty;
				Index = m_studyControl.InstructionsList.Count + 1;
				Id = Index.ToString();
				TimeFilters = new List<TimeFilter>();
				Code = string.Empty;
				m_entryCount = 0;
				m_previousBarPositionWasFlat = true;
			}

			public virtual bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "instruction") { Name = property.Value.ToString().ToLower(); return true; }
				if (name == "id") { Id = property.Value.ToString().ToLower(); return true; }

				if (name == "timefilters")
				{
					TimeFilters = property.Value.ToObject<List<TimeFilter>>();
					for (int i = 0; i < TimeFilters.Count; i++)
					{
						var tempFilter = TimeFilters[i];
						if (tempFilter.Count == 0)
						{
							tempFilter.Count = 1;
							TimeFilters[i] = tempFilter;
						}
					}
					return true;
				}

				return false;
			}

			public virtual void Parse(JToken token)
			{
				Code = token.ToString();
				foreach (var child in token.Children())
				{
					if (child.Type != JTokenType.Property) continue;
					//this exception is added to perform strict parsing of the input JSON since classes to catch typo's that would not
					//be caught due to parameter defaults provided, e.g. an entry has parameter EquityPercent that defaults to 1.0, if the
					//user has a typo on the entry property EqutyPercent set to 0.05 without strict validation the entry would enter at 1.0 (100%)
					//and not the intended 0.05 (5%)
					if (!Parse((JProperty)child)) throw new ArgumentException($"Failed to process property - {child.Path}");
				}
			}

			public virtual Tuple<bool, string> IsValid()
			{
				if (Name == string.Empty) throw new ArgumentException($"{InstructionTypeName} - Instrument name not specified");

				for (int i = 0; i < TimeFilters.Count; i++)
				{
					if (TimeFilters[i].StartTime == TimeFilters[i].EndTime)
						return Tuple.Create(false, $"{InstructionTypeName} - TimeFilter[{i}] StartTime and EndTime can not be the same");
					if (TimeFilters[i].StartTime > TimeFilters[i].EndTime)
						return Tuple.Create(false, $"{InstructionTypeName} - TimeFilter[{i}] StartTime must be before EndTime");
					if (TimeFilters[i].Count < 1)
						return Tuple.Create(false, $"{InstructionTypeName} - TimeFilter[{i}] Count must be greater than 0");
				}

				for (int i = 0; i < TimeFilters.Count; i++)
				{
					for (int j = i + 1; j < TimeFilters.Count; j++)
					{
						if (TimeFilters[i].StartTime < TimeFilters[j].EndTime && TimeFilters[j].StartTime < TimeFilters[i].EndTime)
							return Tuple.Create(false, $"{InstructionTypeName} - TimeFilter[{i}] and TimeFilter[{j}] overlap");
					}
				}

				return Tuple.Create(true, string.Empty);
			}

			protected virtual bool Active
			{
				get {
					bool timeFilterValid = TimeFilters.Count == 0;  //if no time filters specified the entry is valid all the time
					bool timeFilterMatch = false;
					foreach (TimeFilter timeFilter in TimeFilters)
						if (m_studyControl.Bars.Time[0].TimeOfDay >= timeFilter.StartTime.TimeOfDay && m_studyControl.Bars.Time[0].TimeOfDay <= timeFilter.EndTime.TimeOfDay)
						{
							timeFilterMatch = true; //time filter is valid if the time of day is between the start and end time
							timeFilterValid = m_entryCount < timeFilter.Count; //time filter is only valid if the number of entries for the time filter is not exceeded
							break;
						}
					if (!timeFilterMatch) m_entryCount = 0;

					return TimeFilters.Count == 0 || timeFilterMatch;
				}
			}

			public virtual void StartCalc() { }
			public virtual void StopCalc() { }

			public virtual void CalcBar()
			{
				if (m_studyControl.PositionSide != 0 && m_previousBarPositionWasFlat) m_entryCount++;
				m_previousBarPositionWasFlat = m_studyControl.PositionSide == 0;
			}

			protected ITrendLineObject getTrendLine(int id)
			{
				foreach (ITrendLineObject line in m_studyControl.DrwTrendLine.GetTrendLineObjects(EDrawingSource.AnyTechOrManual))
					if (line.ID == id) return line;
				return null;
			}

			/// <summary>
			/// Determine the current open position size.
			/// Note: Market position is negative for short positions so we need to use the absolute value.
			/// </summary>
			protected int currentOpenPositionSize()
			{
				return Math.Abs(m_studyControl.StrategyInfo.MarketPosition);
			}

			/// <summary>
			/// Determine the current open cost basis of the position.
			/// </summary>
			protected double currentOpenCostBasis()
			{
				if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0.0;
				double costBasis = 0.0;
				foreach (ITrade trade in m_studyControl.Positions[0].OpenTrades)
					costBasis += trade.EntryOrder.Price * trade.EntryOrder.Contracts;
				return costBasis;
			}

			/// <summary>
			/// Determine the current total position size based on both the open and closed trades of the position.
			/// Note: Market position is negative for short positions so we need to use the absolute value.
			/// </summary>
			protected int currentTotalPositionSize()
			{
				if (m_studyControl.StrategyInfo.MarketPosition == 0) return 0;
				int size = 0;
				foreach (ITrade trade in m_studyControl.Positions[0].ClosedTrades)
					size += trade.ExitOrder.Contracts;
				foreach (ITrade trade in m_studyControl.Positions[0].OpenTrades)
					size += trade.EntryOrder.Contracts;
				return Math.Abs(size);
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

			/// <summary>
			/// Scans back from the current bar to the previous day and returns the lowest low including the previous day.
			/// </summary>
			protected double recentLow(int daysBack = 1)
			{
				double low = m_studyControl.IntradayLow;
				DateTime lowDate = m_studyControl.IntradayLowTime;
				int daysProcessed = 0;
				DateTime startDate = m_studyControl.Bars.Time[0].Date;
				DateTime lastDate = startDate;

				for (int i = 0; i < m_studyControl.Bars.CurrentBar; i++)
				{
					if (m_studyControl.Bars.Time[i].Date != lastDate)
					{
						daysProcessed++;
						lastDate = m_studyControl.Bars.Time[i].Date;
					}

					if (m_studyControl.Bars.Time[i].Date != startDate && low > m_studyControl.Bars.Low[i])					
						low = Math.Min(low, m_studyControl.Bars.Low[i]);

					if (daysProcessed > daysBack)
						break;
				}

				return low;
			}

			/// <summary>
			/// Scan back from the current bar to the previous day and returns the highest high including the previous day.
			/// </summary>
			protected double recentHigh(int daysBack = 1)
			{
				double high = m_studyControl.IntradayHigh;
				int daysProcessed = 0;
				DateTime startDate = m_studyControl.Bars.Time[0].Date;
				DateTime lastDate = startDate;

				for (int i = 0; i < m_studyControl.Bars.CurrentBar; i++)
				{
					if (m_studyControl.Bars.Time[i].Date != lastDate)
					{
						daysProcessed++;
						lastDate = m_studyControl.Bars.Time[i].Date;
					}

					if (m_studyControl.Bars.Time[i].Date != startDate)
						high = Math.Max(high, m_studyControl.Bars.High[i]);

					if (daysProcessed > daysBack)
						break;
				}

				return high;
			}
		}

		internal abstract class Entry : Instruction
		{
			public EEntryDirection Direction { get; set; }
			public EEntryStrategy Strategy { get; set; }
			public EOrderType OrderType { get; set; }
			public EEntryExitCriteria Criteria { get; set; }
			public double EquityPercent { get; set; }
			public double MaxVolumeBasedPercent { get; set; }
			public bool AddToPosition { get; set; }
			public DateTime ValidUntil { get; set; }
			public string StopLossExitId { get; set; }
			public double MaxRiskBasedEquityPercent { get; set; }
			public bool UseRoundLots { get; set; }

			protected IOrderMarket m_orderLeMkt;
			protected IOrderMarket m_orderSeMkt;
			protected IOrderPriced m_orderLePriced;     //use for stop and limit orders
			protected IOrderPriced m_orderSePriced;     //use for stop and limit orders
			protected IOrderStopLimit m_orderLeStpLmt;
			protected IOrderStopLimit m_orderSeStpLmt;
			protected EEntryDirection m_autoTradeDirection;   //direction to take when the entry direction is set to auto, subclasses need to fill this in and it should never be auto since it will result in a noop
			protected ExitPriced m_stopLossExit;

			public Entry(_TrendLine control) : base(control)
			{
				Direction = EEntryDirection.Long;
				Strategy = EEntryStrategy.Breakout;
				m_autoTradeDirection = EEntryDirection.Long;
				OrderType = EOrderType.Market;
				EquityPercent = 1.0;
				MaxVolumeBasedPercent = 0.2;
				Criteria = EEntryExitCriteria.Close;
				AddToPosition = false;
				ValidUntil = DateTime.MaxValue;
				StopLossExitId = string.Empty;
				m_stopLossExit = null;
				MaxRiskBasedEquityPercent = 1.0;
				UseRoundLots = false;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "direction") { Direction = property.Value.ToObject<EEntryDirection>(); return true; }
				if (name == "strategy") { Strategy = property.Value.ToObject<EEntryStrategy>(); return true; }
				if (name == "ordertype") { OrderType = property.Value.ToObject<EOrderType>(); return true; }
				if (name == "equitypercent") { EquityPercent = property.Value.ToObject<double>(); return true; }
				if (name == "maxvolumebasedpercent") { MaxVolumeBasedPercent = property.Value.ToObject<double>(); return true; }
				if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
				if (name == "addtoposition") { AddToPosition = property.Value.ToObject<bool>(); return true; }
				if (name == "validuntil") { ValidUntil = property.Value.ToObject<DateTime>(); return true; }
				if (name == "stoplossexitid") { StopLossExitId = property.Value.ToString().ToLower(); return true; }
				if (name == "maxriskbasedequitypercent") { MaxRiskBasedEquityPercent = property.Value.ToObject<double>(); return true; }
				if (name == "useroundlots") { UseRoundLots = property.Value.ToObject<bool>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - EquityPercent must be between 0 and 1");
				if (MaxVolumeBasedPercent < 0 || MaxVolumeBasedPercent > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - MaxVolumeBasedPercent must be between 0 and 1");
				if (StopLossExitId != string.Empty && m_stopLossExit == null) return Tuple.Create(false, $"{InstructionTypeName} - ExitStopLossId priced exit not found");
				if (MaxRiskBasedEquityPercent <= 0 || MaxRiskBasedEquityPercent > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - MaxRiskBasedEquityPercent must be between 0 and 1");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_orderLeMkt = null;
				m_orderSeMkt = null;
				m_orderLePriced = null;
				m_orderSePriced = null;
				m_orderLeStpLmt = null;
				m_orderSeStpLmt = null;
				m_stopLossExit = null;

				//allocate orders
				switch (OrderType)
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
								m_orderLePriced = m_studyControl.AllocateLeStp();
								break;
							case EEntryDirection.Short:
								m_orderSePriced = m_studyControl.AllocateSeStp();
								break;
						}
						break;
					case EOrderType.Limit:
						switch (Direction)
						{
							case EEntryDirection.Long:
								m_orderLePriced = m_studyControl.AllocateLeLmt();
								break;
							case EEntryDirection.Short:
								m_orderSePriced = m_studyControl.AllocateSeLmt();
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

				//try to find the exit stop loss exit
				if (StopLossExitId != string.Empty)
				{
					foreach (Instruction instruction in m_studyControl.InstructionsList)
						if (instruction is ExitPriced && instruction.Id == StopLossExitId)
						{
							m_stopLossExit = (ExitPriced)instruction;
							break;
						}
				}
			}

			/// <summary>
			/// Computes the size of the position based on the equity available, equity percent per position, entry price and exit stop-loss price (if specified).
			/// </summary>
			protected virtual int entrySize(double price = 0.0)
			{
				double equityAvailable = m_studyControl.equityAvailable();
				double maxPositionEquity = equityAvailable * EquityPercent - currentOpenCostBasis();
				double entryPrice = price == 0.0 ? m_studyControl.Bars.Close[0] : price;
				int equityPercentSize = (int)Math.Truncate(maxPositionEquity / entryPrice);
				int size = equityPercentSize;

				if (price != 0.0 && m_stopLossExit != null)
				{
					double stopLossPrice = m_stopLossExit.EstimateExitPrice(Direction, Strategy, Criteria);
					if (stopLossPrice != 0.0)
					{
						if (maxPositionEquity > 0)
						{
							double risk;
							try
							{
								risk = checked(Math.Round(Math.Abs(entryPrice - stopLossPrice), 2));
								int riskAdjustedSize = equityPercentSize;
								if (risk != 0.0d) riskAdjustedSize = checked((int)Math.Truncate(maxPositionEquity / risk));

								//clip the available equity based on the maximum allowable risk based equity percent if required
								if (MaxRiskBasedEquityPercent < 1.0)
								{
									double maxRiskBasedEquityAvailable = equityAvailable * MaxRiskBasedEquityPercent;
									equityAvailable = equityAvailable > maxRiskBasedEquityAvailable ? maxRiskBasedEquityAvailable : equityAvailable;
								}

								//clip position size to the account equity available since risk based entries can
								//result in very large position sizes, we pick the lowest number between the
								//equityPercentSize, riskAdjustedSize and maxPositionSize
								int maxPositionSize = (int)Math.Truncate(equityAvailable / risk);
								size = equityPercentSize > riskAdjustedSize ? (maxPositionSize > riskAdjustedSize ? riskAdjustedSize : maxPositionSize) : (maxPositionSize > equityPercentSize ? equityPercentSize : maxPositionSize);
							}
							catch (OverflowException)
							{
								m_studyControl.Output.WriteLine($"{Symbol} - {InstructionTypeName} - entrySize positionSize based on stop-loss raised an overflow exception: EntryPrice({entryPrice})");
							}
						}
					}
					else m_studyControl.trace(ETraceLevel.Error, "StopLossPrice is zero, using equity based position size");
				}

				//clip position size to the available volume of the insturment being traded if required
				if (MaxVolumeBasedPercent != 0.0)
				{
					int maximumPositionBasedOnVolume = (int)Math.Truncate(m_studyControl.Bars.Volume.Average(_TrendLine.DEFAULT_VOLUME_PERIOD) * MaxVolumeBasedPercent);
					size = maximumPositionBasedOnVolume < size ? maximumPositionBasedOnVolume : size;
				}

				if (UseRoundLots) size = (int)(Math.Round(size / 100.0) * 100.0);
				return size;
			}

			/// <summary>
			/// Sends an entry order at an optional price and specific size based on the entry type and direction.
			/// For market orders the order is sent immediately so subclasses needs to make sure they send market
			/// orders only when required.
			/// </summary>
			protected void sendOrder(double price = 0.0)
			{
				if (!Active)
				{
					m_studyControl.trace(ETraceLevel.Verbose, $"{InstructionTypeName} - Entry is not active");
					return;
				}

				switch (Direction)
				{
					case EEntryDirection.Long:
						switch (OrderType)
						{
							case EOrderType.Market:
								m_orderLeMkt.Send(entrySize(price));
								break;
							case EOrderType.Stop:
							case EOrderType.Limit:
								m_orderLePriced.Send(price, entrySize(price));
								break;
							case EOrderType.StopLimit:
								m_orderLeStpLmt.Send(price, price, entrySize(price));
								break;
						}
						break;
					case EEntryDirection.Short:
						switch (OrderType)
						{
							case EOrderType.Market:
								m_orderSeMkt.Send(entrySize(price));
								break;
							case EOrderType.Stop:
							case EOrderType.Limit:
								m_orderSePriced.Send(price, entrySize(price));
								break;
							case EOrderType.StopLimit:
								m_orderSeStpLmt.Send(price, price, entrySize(price));
								break;
						}
						break;
				}
			}

			/// <summary>
			/// Given the set of defined exits, check if the current price will be stopped out.
			/// </summary>
			protected bool willBeStoppedOut(EEntryDirection direction, double price)
			{
				bool willBeStoppedOut = false;
				foreach (var instruction in m_studyControl.InstructionsList)
				{
					if (instruction is ExitPriced)
					{
						ExitPriced exitPriced = (ExitPriced)instruction;
						switch (direction)
						{
							case EEntryDirection.Long:
								willBeStoppedOut = exitPriced.EstimateExitPrice(direction, Strategy, Criteria) >= price;
								break;
							case EEntryDirection.Short:
								willBeStoppedOut = exitPriced.EstimateExitPrice(direction, Strategy, Criteria) <= price;
								break;
						}

						if (willBeStoppedOut) break;
					}
				}

				if (willBeStoppedOut) m_studyControl.trace(ETraceLevel.Verbose, $"Entry will be stopped out at {price}");
				return willBeStoppedOut;
			}
		}

		internal abstract class Exit : Instruction
		{
			public EOrderType OrderType { get; set; }
			public double ExitPercent { get; set; }
			public DateTime ValidUntil { get; set; }

			protected IOrderMarket m_orderLxMkt;
			protected IOrderMarket m_orderSxMkt;
			protected IOrderPriced m_orderLxPriced;
			protected IOrderPriced m_orderSxPriced;
			protected IOrderStopLimit m_orderLxSL;
			protected IOrderStopLimit m_orderSxSL;

			public Exit(_TrendLine control) : base(control)
			{
				OrderType = EOrderType.Stop;
				ExitPercent = 1.0;
				ValidUntil = DateTime.MaxValue;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "ordertype") { OrderType = property.Value.ToObject<EOrderType>(); return true; }
				if (name == "exitpercent") { ExitPercent = property.Value.ToObject<double>(); return true; }
				if (name == "validuntil") { ValidUntil = property.Value.ToObject<DateTime>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (ExitPercent <= 0 || ExitPercent > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - ExitPercent must be between 0 and 1");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_orderLxMkt = null;
				m_orderSxMkt = null;
				m_orderLxPriced = null;
				m_orderSxPriced = null;
				m_orderLxSL = null;
				m_orderSxSL = null;

				switch (OrderType)
				{
					case EOrderType.Market:
						m_orderLxMkt = m_studyControl.AllocateLxMrk();
						m_orderSxMkt = m_studyControl.AllocateSxMrk();
						break;
					case EOrderType.Stop:
						m_orderLxPriced = m_studyControl.AllocateLxStp();
						m_orderSxPriced = m_studyControl.AllocateSxStp();
						break;
					case EOrderType.Limit:
						m_orderLxPriced = m_studyControl.AllocateLxLmt();
						m_orderSxPriced = m_studyControl.AllocateSxLmt();
						break;
					case EOrderType.StopLimit:
						m_orderLxSL = m_studyControl.AllocateLxStpLmt();
						m_orderSxSL = m_studyControl.AllocateSxStpLmt();
						break;
				}
			}

			protected int exitSize()
			{
				if (ExitPercent == 1.0) return currentOpenPositionSize();
				//NOTE: Since MC does not support partial order filling we need to round up to the nearest larger integer otherwise
				//      partial positions remain that are stopped out etc.
				int size = (int)Math.Ceiling(currentOpenPositionSize() * ExitPercent);
				return size;
			}

			protected void sendOrder(double price = 0.0)
			{
				if (!Active)
				{
					m_studyControl.trace(ETraceLevel.Verbose, $"{InstructionTypeName} - Exit is not active");
					return;
				}

				int size = exitSize();
				if (m_studyControl.PositionSide > 0)
				{
					switch (OrderType)
					{
						case EOrderType.Market:
							m_studyControl.trace(ETraceLevel.Info, $"long - sending exit market order for {size} contracts");
							m_orderLxMkt.Send(size);
							break;
						case EOrderType.Stop:
						case EOrderType.Limit:
							if (price == 0.0)
							{
								m_studyControl.trace(ETraceLevel.Error, $"long - price is zero for stop/limit order exiting");
								return;
							}
							m_studyControl.trace(ETraceLevel.Info, $"long - sending exit {OrderType} order for {size} contracts at price {price}");
							m_orderLxPriced.Send(price, size);
							break;
						case EOrderType.StopLimit:
							if (price == 0.0)
							{
								m_studyControl.trace(ETraceLevel.Error, $"long - price is zero for stop-limit order exiting");
								return;
							}
							m_studyControl.trace(ETraceLevel.Info, $"long - sending exit stop-limit order for {size} contracts at price {price}");
							m_orderLxSL.Send(price, price, size);
							break;
					}
				}
				else if (m_studyControl.PositionSide < 0)
				{
					switch (OrderType)
					{
						case EOrderType.Market:
							m_studyControl.trace(ETraceLevel.Info, $"short - sending exit market order for {size} contracts");
							m_orderSxMkt.Send(size);
							break;
						case EOrderType.Stop:
						case EOrderType.Limit:
							if (price == 0.0)
							{
								m_studyControl.trace(ETraceLevel.Error, $"short - price is zero for stop/limit order exiting");
								return;
							}
							m_studyControl.trace(ETraceLevel.Info, $"short - sending exit {OrderType} order for {size} contracts at price {price}");
							m_orderSxPriced.Send(price, size);
							break;
						case EOrderType.StopLimit:
							if (price == 0.0)
							{
								m_studyControl.trace(ETraceLevel.Error, $"short - price is zero for stop-limit order exiting");
								return;
							}
							m_studyControl.trace(ETraceLevel.Info, $"short - sending exit stop-limit order for {size} contracts at price {price}");
							m_orderSxSL.Send(price, price, size);
							break;
					}
				}
			}

			protected override bool Active
			{
				get { return base.Active && m_studyControl.PositionSide != 0; }
			}
		}

		/// <summary>
		/// Base class for priced exits used for stop/stop-limit orders at a determined price.
		/// </summary>
		internal abstract class ExitPriced : Exit
		{
			public ExitPriced(_TrendLine control) : base(control) { }

			public override Tuple<bool, string> IsValid()
			{
				if (OrderType == EOrderType.Market) return Tuple.Create(false, $"{InstructionTypeName} - Market exit not supported");
				return base.IsValid();
			}

			public override void CalcBar()
			{
				base.CalcBar();
				if (m_studyControl.Bars.Time[0] > ValidUntil) return;
				sendOrder(exitPrice());
			}

			/// <summary>
			/// Estimate the exit price per contract for the exit if an entry is made based on direction and strategy.
			/// </summary>
			public abstract double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria);

			/// <summary>
			/// Subclasses need to calculate the exit price per contract to exit the position.
			/// </summary>
			protected abstract double exitPrice();
		}

		internal class EntryLine : Entry
		{
			public int LineId { get; set; }
			public DateTime LineStartTime { get; set; }
			public double LineStartPrice { get; set; }
			public DateTime LineEndTime { get; set; }
			public double LineEndPrice { get; set; }
			public double ClosePercentile { get; set; }
			public int VolumeDataFeed { get; set; }
			public int VolumeLength { get; set; }
			public EVolumeRequired VolumeRequired { get; set; }
			private ITrendLineObject m_line;
			private PowerLanguage.Function._VolumeWithSD m_volume;
			private IInstrument m_volumeInstrument;

			public EntryLine(_TrendLine control) : base(control)
			{
				LineId = -1;
				ClosePercentile = 0.5;
				LineStartTime = DateTime.MinValue;
				LineStartPrice = 0.0;
				LineEndTime = DateTime.MinValue;
				LineEndPrice = 0.0;
				VolumeDataFeed = 1;
				VolumeLength = 20;
				VolumeRequired = EVolumeRequired.Disable;
				m_line = null;
				m_volume = null;
				m_volumeInstrument = null;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "lineid") { LineId = property.Value.ToObject<int>(); return true; }
				if (name == "closepercentile") { ClosePercentile = property.Value.ToObject<double>(); return true; }
				if (name == "linestarttime") { LineStartTime = DateTime.ParseExact(property.Value.ToString(), "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture); return true; }
				if (name == "linestartprice") { LineStartPrice = property.Value.ToObject<double>(); return true; }
				if (name == "lineendtime") { LineEndTime = DateTime.ParseExact(property.Value.ToString(), "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture); return true; }
				if (name == "lineendprice") { LineEndPrice = property.Value.ToObject<double>(); return true; }
				if (name == "volumedatafeed") { VolumeDataFeed = property.Value.ToObject<int>(); return true; }
				if (name == "volumelength") { VolumeLength = property.Value.ToObject<int>(); return true; }
				if (name == "volumerequired") { VolumeRequired = property.Value.ToObject<EVolumeRequired>(); return true; }

				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (LineId != 0 && m_line == null)
					return Tuple.Create(false, $"{InstructionTypeName} - trendline with id {LineId} not found");
				else if (LineStartTime == DateTime.MinValue || LineEndTime == DateTime.MinValue || LineStartPrice == 0.0 || LineEndPrice == 0.0)
					return Tuple.Create(false, $"{InstructionTypeName} - LineId is not specified and LineStartTime/LineStartPrice and LineEndTime/LineEndPrice is not specified");
				if (LineStartTime != DateTime.MinValue && LineEndTime != DateTime.MinValue && LineStartTime == LineEndTime)
					return Tuple.Create(false, $"{InstructionTypeName} - LineStartTime and LineEndTime can not be the same");
				if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - EntryPercent must be between 0 and 1");
				if (ClosePercentile < 0 || ClosePercentile > 1.0) return Tuple.Create(false, $"{InstructionTypeName} - ClosePercentile must be between 0 and 1");
				if (VolumeRequired != EVolumeRequired.Disable && VolumeDataFeed < 1) return Tuple.Create(false, $"{InstructionTypeName} - VolumeDataFeed must be greater than 0");
				if (VolumeRequired != EVolumeRequired.Disable && VolumeLength < 1) return Tuple.Create(false, $"{InstructionTypeName} - VolumeLength must be greater than 0");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();

				if (VolumeRequired >= 0)
				{
					m_volume = m_studyControl.AllocateVolumeWithSD();
					m_volumeInstrument = m_studyControl.BarsOfData(VolumeDataFeed);
					m_volume.Instrument = m_volumeInstrument;
					m_volume.Length = VolumeLength;
				}

				if (LineId > 0)
				{
					m_line = getTrendLine(LineId);
					LineStartTime = m_line.Begin.Time;
					LineStartPrice = m_line.Begin.Price;
					LineEndTime = m_line.End.Time;
					LineEndPrice = m_line.End.Price;
				}
				else
					m_line = m_studyControl.DrwTrendLine.Create(new ChartPoint(LineStartTime, LineStartPrice), new ChartPoint(LineEndTime, LineEndPrice), 1);
			}

			public override void CalcBar()
			{
				base.CalcBar();
				if (m_studyControl.Bars.CurrentBar < 2) return;
				if (m_studyControl.PositionSide != 0 && !AddToPosition) return;
				if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
				if (m_studyControl.Bars.Time[0] < LineStartTime || m_studyControl.Bars.Time[0] > LineEndTime) return;
				if (m_studyControl.Bars.Time[0] > ValidUntil) return;

				switch (VolumeRequired)
				{
					case EVolumeRequired.Disable:
						break;  //nothing to do when disabled
					case EVolumeRequired.AboveAverage:
						if (m_volume.Average[0] >= m_volumeInstrument.Volume[0]) return;
						break;
					case EVolumeRequired.AboveAverageSD1:
						if (m_volume.AvgPlusLevel1[0] >= m_volumeInstrument.Volume[0]) return;
						break;
					case EVolumeRequired.AboveAverageSD2:
						if (m_volume.AvgPlusLevel2[0] >= m_volumeInstrument.Volume[0]) return;
						break;
					case EVolumeRequired.AboveAverageSD3:
						if (m_volume.AvgPlusLevel3[0] >= m_volumeInstrument.Volume[0]) return;
						break;
				}

				switch (Direction)
				{
					case EEntryDirection.Long:
						longEntry();
						break;
					case EEntryDirection.Short:
						shortEntry();
						break;
				}
			}

			private void longEntry()
			{
				double linePrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
				switch (Criteria)
				{
					case EEntryExitCriteria.Close:
						//need to consider both breakout and fade entries
						//NOTE: We use the previous day low for breakout and fade entries since gap ups can cause all bars for the current day to be above the line.
						bool entryCriteriaMet = false;
						double recentLow = this.recentLow();

						switch (Strategy)
						{
							case EEntryStrategy.Breakout:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Long, m_studyControl.Bars.Close[0]) && recentLow < linePrice && m_studyControl.Bars.Close[0] > linePrice;
								break;
							case EEntryStrategy.Fade:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Long, m_studyControl.Bars.Close[0]) && recentLow > linePrice && m_studyControl.Bars.Close[0] < linePrice;
								break;
							case EEntryStrategy.BreakoutOrFade:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Long, m_studyControl.Bars.Close[0]) && ((recentLow < linePrice && m_studyControl.Bars.Close[0] > linePrice) || (recentLow > linePrice && m_studyControl.Bars.Close[0] < linePrice));
								break;
						}

						if (entryCriteriaMet)
						{
							double low = m_studyControl.Bars.Low[0];
							double close = m_studyControl.Bars.Close[0];
							double range = m_studyControl.Bars.High[0] - low;
							double closePercentile = range != 0 ? (close - low) / range : 0;    //this will skip trades for bars with no range

							if (closePercentile >= ClosePercentile)
							{
								m_studyControl.trace(ETraceLevel.Info, $"Long entry at close {close}, closePercentile {closePercentile}");
								sendOrder(close);
							}
							else m_studyControl.trace(ETraceLevel.Verbose, $"Ignoring long entry at close {close} since closePercentile {closePercentile} is not above required {ClosePercentile}");
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (!willBeStoppedOut(EEntryDirection.Long, linePrice))
						{
							m_studyControl.trace(ETraceLevel.Info, $"Long entry at low/high linePrice {linePrice}");
							sendOrder(linePrice);
						}
						break;
				}
			}

			/// <summary>
			/// Enter a short entry based on the trendline and most recent high, if the last position was held after the most recent high a new
			/// high needs to be made before a new entry is allowed.
			/// </summary>
			private void shortEntry()
			{
				double linePrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
				switch (Criteria)
				{
					case EEntryExitCriteria.Close:
						bool entryCriteriaMet = false;
						double recentHigh = this.recentHigh();

						switch (Strategy)
						{
							case EEntryStrategy.Breakout:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Short, m_studyControl.Bars.Close[0]) && recentHigh > linePrice && m_studyControl.Bars.Close[0] < linePrice;
								break;
							case EEntryStrategy.Fade:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Short, m_studyControl.Bars.Close[0]) && recentHigh < linePrice && m_studyControl.Bars.Close[0] > linePrice;
								break;
							case EEntryStrategy.BreakoutOrFade:
								entryCriteriaMet = !willBeStoppedOut(EEntryDirection.Short, m_studyControl.Bars.Close[0]) && ((recentHigh < linePrice && m_studyControl.Bars.Close[0] > linePrice) || (recentHigh > linePrice && m_studyControl.Bars.Close[0] < linePrice));
								break;
						}

						if (entryCriteriaMet)
						{
							double high = m_studyControl.Bars.High[0];
							double close = m_studyControl.Bars.Close[0];
							double range = high - m_studyControl.Bars.Low[0];
							//for short trades, invert the percentile so a close near the low = high percentile (e.g., bottom 25% close -> 0.75 or higher percentile)
							double closePercentile = range != 0 ? (high - close) / range : 0;

							if (closePercentile >= ClosePercentile)  // e.g., ClosePercentile = 0.75 means close in bottom 25% of range
							{
								m_studyControl.trace(ETraceLevel.Info, $"Short entry at close {close}");
								sendOrder(close);
							}
							else m_studyControl.trace(ETraceLevel.Verbose, $"Ignoring short entry at close {close} since closePercentile {closePercentile} is not above required {ClosePercentile}");
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (!willBeStoppedOut(EEntryDirection.Short, linePrice))
						{
							m_studyControl.trace(ETraceLevel.Info, $"Short entry at low/high linePrice {linePrice}");
							sendOrder(linePrice);
						}
						break;
				}
			}
		};

		internal class ExitLine : Exit
		{
			public int LineId { get; set; }
			public EEntryExitCriteria Criteria { get; set; }
			public DateTime LineStartTime { get; set; }
			public double LineStartPrice { get; set; }
			public DateTime LineEndTime { get; set; }
			public double LineEndPrice { get; set; }
			private ITrendLineObject m_line;

			public ExitLine(_TrendLine control) : base(control)
			{
				Criteria = EEntryExitCriteria.Close;
				LineStartTime = DateTime.MinValue;
				LineStartPrice = 0.0;
				LineEndTime = DateTime.MinValue;
				LineEndPrice = 0.0;
				m_line = null;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "lineid") { LineId = property.Value.ToObject<int>(); return true; }
				if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
				if (name == "linestarttime") { LineStartTime = DateTime.ParseExact(property.Value.ToString(), "yyyy/MM/dd hh:mm:ss", CultureInfo.InvariantCulture); return true; }
				if (name == "linestartprice") { LineStartPrice = property.Value.ToObject<double>(); return true; }
				if (name == "lineendtime") { LineEndTime = DateTime.ParseExact(property.Value.ToString(), "yyyy/MM/dd hh:mm:ss", CultureInfo.InvariantCulture); return true; }
				if (name == "lineendprice") { LineEndPrice = property.Value.ToObject<double>(); return true; }

				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (LineId != 0 && m_line == null)
					return Tuple.Create(false, $"{InstructionTypeName} - trendline with id {LineId} not found");
				else if (LineStartTime == DateTime.MinValue || LineEndTime == DateTime.MinValue || LineStartPrice == 0.0 || LineEndPrice == 0.0)
					return Tuple.Create(false, $"{InstructionTypeName} - LineId is not specified and LineStartTime/LineStartPrice and LineEndTime/LineEndPrice is not specified");
				if (LineStartTime != DateTime.MinValue && LineEndTime != DateTime.MinValue && LineStartTime == LineEndTime)
					return Tuple.Create(false, $"{InstructionTypeName} - LineStartTime and LineEndTime can not be the same");
				if (OrderType == EOrderType.Market) return Tuple.Create(false, $"{InstructionTypeName} - Market exit not supported");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				if (LineId > 0)
				{
					m_line = getTrendLine(LineId);
					LineStartTime = m_line.Begin.Time;
					LineStartPrice = m_line.Begin.Price;
					LineEndTime = m_line.End.Time;
					LineEndPrice = m_line.End.Price;
				}
				else
					m_line = m_studyControl.DrwTrendLine.Create(new ChartPoint(LineStartTime, LineStartPrice), new ChartPoint(LineEndTime, LineEndPrice), 1);
			}

			public override void CalcBar()
			{
				base.CalcBar();

				if (m_studyControl.PositionSide == 0) return;
				if (m_studyControl.Bars.CurrentBar < 2) return;
				if (m_studyControl.Bars.Time[0].Date < LineStartTime || m_studyControl.Bars.Time[0].Date > LineEndTime) return;
				if (m_studyControl.Bars.Time[0] > ValidUntil) return;
				if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;

				if (m_studyControl.PositionSide > 0)
					exitLong();
				else if (m_studyControl.PositionSide < 0)
					exitShort();
			}

			private void exitLong()
			{
				double linePrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
				switch (Criteria)
				{
					case EEntryExitCriteria.Close:
						if (m_studyControl.Bars.Close[0] < linePrice)
						{
							m_studyControl.trace(ETraceLevel.Info, $"Long exit on close at close {m_studyControl.Bars.Close[0]} above linePrice {linePrice}");
							sendOrder(m_studyControl.Bars.Close[0]);
						}
						break;
					case EEntryExitCriteria.LowHigh:
						m_studyControl.trace(ETraceLevel.Info, $"Long exit on low at low/high on linePrice {linePrice}");
						sendOrder(linePrice);
						break;
				}
			}

			private void exitShort()
			{
				double linePrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
				switch (Criteria)
				{
					case EEntryExitCriteria.Close:
						if (m_studyControl.Bars.Close[0] > linePrice)
						{
							m_studyControl.trace(ETraceLevel.Info, $"Short exit on close at close {m_studyControl.Bars.Close[0]} below linePrice {linePrice}");
							sendOrder(m_studyControl.Bars.Close[0]);
						}
						break;
					case EEntryExitCriteria.LowHigh:
						m_studyControl.trace(ETraceLevel.Info, $"Short exit on high at low/high on linePrice {linePrice}");
						sendOrder(linePrice);
						break;
				}
			}
		}

		internal class ExitMovingAverage : ExitPriced
		{
			public int Length { get; set; }
			public EMovingAverageType MovingAverageType { get; set; }
			public EEntryExitCriteria Criteria { get; set; }
			public int DataFeed { get; set; }

			protected PowerLanguage.Function._AverageSimple m_averageSimple;
			protected PowerLanguage.Function._AverageExponential m_averageExponential;
			protected PowerLanguage.Function._SuperSmoother m_averageSuperSmoother;
			protected IInstrument m_instrument;

			public ExitMovingAverage(_TrendLine control) : base(control)
			{
				MovingAverageType = EMovingAverageType.Simple;
				Criteria = EEntryExitCriteria.Close;
				OrderType = EOrderType.Stop;
				DataFeed = 1;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "length") { Length = property.Value.ToObject<int>(); return true; }
				if (name == "movingaveragetype") { MovingAverageType = property.Value.ToObject<EMovingAverageType>(); return true; }
				if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
				if (name == "datafeed") { DataFeed = property.Value.ToObject<int>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (Length <= 0) return Tuple.Create(false, $"{InstructionTypeName} - Length must be greater than 0");
				if (DataFeed <= 0) return Tuple.Create(false, $"{InstructionTypeName} - DataFeed must be greater than 0");
				if (DataFeed > m_studyControl.MaxDataStream) return Tuple.Create(false, $"{InstructionTypeName} - DataFeed must be less than {m_studyControl.MaxDataStream}");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_averageSimple = null;
				m_averageExponential = null;
				m_averageSuperSmoother = null;
				m_instrument = m_studyControl.BarsOfData(DataFeed);

				switch (MovingAverageType)
				{
					case EMovingAverageType.Simple:
						m_averageSimple = m_studyControl.AllocateSimpleAverage();
						m_averageSimple.Instrument = m_instrument;
						m_averageSimple.Length = Length;
						break;
					case EMovingAverageType.Exponential:
						m_averageExponential = m_studyControl.AllocateExponentialAverage();
						m_averageExponential.Instrument = m_instrument;
						m_averageExponential.Length = Length;
						break;
					case EMovingAverageType.SuperSmoother:
						m_averageSuperSmoother = m_studyControl.AllocateSuperSmoother();
						m_averageSuperSmoother.Instrument = m_instrument;
						m_averageSuperSmoother.Length = Length;
						break;
				}
			}

			public override void CalcBar()
			{
				if (m_studyControl.Bars.CurrentBar < Length) return;

				if (Criteria == EEntryExitCriteria.Close) {
					if (m_studyControl.Bars.Status != EBarState.Close) return;
					if (m_studyControl.PositionSide > 0 && m_studyControl.Bars.Close[0] > exitPrice()) return;
					if (m_studyControl.PositionSide < 0 && m_studyControl.Bars.Close[0] < exitPrice()) return;
				}

				base.CalcBar();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				return exitPrice();
			}

			protected override double exitPrice()
			{
				switch (MovingAverageType)
				{
					case EMovingAverageType.Simple:
						return m_averageSimple[0];
					case EMovingAverageType.Exponential:
						return m_averageExponential[0];
					case EMovingAverageType.SuperSmoother:
						return m_averageSuperSmoother[0];
				}

				//should never be reached
				m_studyControl.trace(ETraceLevel.Error, $"exitPrice - could not determine exit price {MovingAverageType}");
				return 0.0;
			}
		}

		internal class ExitOverextension : ExitMovingAverage
		{
			public double ZScoreThreshold { get; set; }
			public int StdDevLength { get; set; }

			public ExitOverextension(_TrendLine control) : base(control)
			{
				ZScoreThreshold = 3.0;
				StdDevLength = 200;
				OrderType = EOrderType.Limit;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "zscorethreshold") { ZScoreThreshold = property.Value.ToObject<double>(); return true; }
				if (name == "stddevlength") { StdDevLength = property.Value.ToObject<int>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (OrderType == EOrderType.Market || OrderType == EOrderType.Stop) return Tuple.Create(false, $"{InstructionTypeName} - Market/stop exit not supported, requires limit type orders");
				if (ZScoreThreshold <= 0) return Tuple.Create(false, $"{InstructionTypeName} - ZScoreThreshold must be greater than 0");
				if (StdDevLength <= 0) return Tuple.Create(false, $"{InstructionTypeName} - StdDevLength must be greater than 0");
				return base.IsValid();
			}

			protected override bool Active
			{
				get
				{
					//NOTE: We completely override the base implementation as it only activates when the price starts falling on the wrong side
					//      of the moving average.
					if (base.Active) 
					{
						double stdDev = m_studyControl.Bars.Close.StandardDeviationCustom(StdDevLength, DataFeed);
						if (stdDev == 0.0) return false; //no standard deviation, disable exit
						double ma = base.exitPrice();
						double zScore = (m_studyControl.Bars.Close[0] - ma) / stdDev;
						m_studyControl.trace(ETraceLevel.Info, $"overextension values - MA {ma} - StdDev {stdDev} - zScore {zScore} - zThreshold {ZScoreThreshold}");
						if (m_studyControl.PositionSide > 0 && zScore > ZScoreThreshold)
						{
							m_studyControl.trace(ETraceLevel.Info, $"long position overextension detected");
							return true;
						}
						if (m_studyControl.PositionSide < 0 && zScore < -ZScoreThreshold)
						{
							m_studyControl.trace(ETraceLevel.Info, $"short position overextension detected");
							return true;
						}
					}

					return false;
				}
			}

			protected override double exitPrice()
			{
				double ma = base.exitPrice();
				double stdDev = m_studyControl.Bars.Close.StandardDeviationCustom(StdDevLength, DataFeed);
				if (stdDev == 0.0) return 0.0; //this will skip the exit and log a warning, most likely not enough data to compute the exit
				double zScore = (m_studyControl.Bars.Close[0] - ma) / stdDev;
				if (m_studyControl.PositionSide > 0 && zScore > ZScoreThreshold)
					return m_studyControl.Bars.Close[0];
				else if (m_studyControl.PositionSide < 0 && zScore < -ZScoreThreshold)
					return m_studyControl.Bars.Close[0];
				return 0.0; //this will skip the exit and log a warning, since the price is not overextended
			}
		}

		internal class ExitStopLoss : ExitPriced
		{
			public double LossPercent { get; set; }
			public double LossPrice { get; set; }
			public double Price { get; set; }

			public ExitStopLoss(_TrendLine control) : base(control)
			{
				LossPercent = -1.0;
				LossPrice = -1.0;
				Price = -1.0;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "losspercent") { LossPercent = property.Value.ToObject<double>(); return true; }
				if (name == "lossprice") { LossPrice = property.Value.ToObject<double>(); return true; }
				if (name == "price") { Price = property.Value.ToObject<double>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (LossPercent == -1.0 && LossPrice == -1.0 && Price == -1.0) return Tuple.Create(false, $"{InstructionTypeName} - LossPercent, LossPrice or Price must be specified");
				if (LossPercent != -1.0 && (LossPercent <= 0.0 || LossPercent > 1.0)) return Tuple.Create(false, $"{InstructionTypeName} - LossPercent must be between 0 and 1");
				if (LossPrice != -1.0 && LossPrice <= 0) return Tuple.Create(false, $"{InstructionTypeName} - LossPrice must be a positive number (will be negated by strategy)");
				if (Price != -1.0 && Price <= 0) return Tuple.Create(false, $"{InstructionTypeName} - Price must be a positive number (will be negated by strategy)");
				return base.IsValid();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				if (LossPercent != -1)
				{
					switch (direction)
					{
						case EEntryDirection.Long:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] * (1 - LossPercent);
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.High[0] * (1 - LossPercent);
							}
							break;
						case EEntryDirection.Short:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] * (1 + LossPercent);
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.Low[0] * (1 + LossPercent);
							}
							break;
					}
				}

				if (LossPrice != -1)
				{
					switch (direction)
					{
						case EEntryDirection.Long:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] - LossPrice;
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.High[0] - LossPrice;
							}
							break;
						case EEntryDirection.Short:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] + LossPrice;
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.Low[0] + LossPrice;
							}
							break;
					}
				}

				if (Price != -1) return Price;

				//should never be reached
				m_studyControl.trace(ETraceLevel.Error, "EstimateExitPrice - could not determine exit price");
				return 0.0;
			}

			protected override double exitPrice()
			{
				double averageEntryPrice = currentPositionAverageEntryPrice();
				if (LossPercent != -1.0) return m_studyControl.PositionSide > 0 ? averageEntryPrice - averageEntryPrice * LossPercent : averageEntryPrice + averageEntryPrice * LossPercent;
				if (LossPrice != -1.0) return m_studyControl.PositionSide > 0 ? averageEntryPrice - LossPrice : averageEntryPrice + LossPrice;
				return Price;
			}
		}

		internal class ExitProfit : ExitPriced
		{
			public double ProfitPercent { get; set; }
			public double ProfitPrice { get; set; }

			public ExitProfit(_TrendLine control) : base(control)
			{
				ProfitPercent = -1.0;
				ProfitPrice = -1.0;
				OrderType = EOrderType.Limit;
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
				if (OrderType == EOrderType.Stop) return Tuple.Create(false, $"{InstructionTypeName} - Stop exit not supported");
				if (ProfitPercent == -1.0 && ProfitPrice == -1.0) return Tuple.Create(false, $"{InstructionTypeName} - ProfitPercent or ProfitPrice must be specified");
				if (ProfitPercent != -1.0 && ProfitPercent <= 0.0) return Tuple.Create(false, $"{InstructionTypeName} - ProfitPercent must be a positive integer, e.g. 1.2 would mean 120% profit");
				return base.IsValid();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				if (ProfitPrice != -1.0)
				{
					switch (direction)
					{
						case EEntryDirection.Long:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] + ProfitPrice;
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.High[0] + ProfitPrice;
							}
							break;
						case EEntryDirection.Short:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] - ProfitPrice;
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.Low[0] - ProfitPrice;
							}
							break;
					}
				}
				else if (ProfitPercent != -1.0)
				{
					switch (direction)
					{
						case EEntryDirection.Long:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] * (1 + ProfitPercent);
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.High[0] * (1 + ProfitPercent);
							}
							break;
						case EEntryDirection.Short:
							switch (criteria)
							{
								case EEntryExitCriteria.Close:
									return m_studyControl.Bars.Close[0] * (1 - ProfitPercent);
								case EEntryExitCriteria.LowHigh:
									return m_studyControl.Bars.Low[0] * (1 - ProfitPercent);
							}
							break;
					}
				}

				//should never be reached
				m_studyControl.trace(ETraceLevel.Error, "EstimateExitPrice - could not determine exit price");
				return 0.0;
			}

			protected override double exitPrice()
			{
				double averageEntryPrice = currentPositionAverageEntryPrice();
				if (ProfitPrice != -1.0)
				{
					double exitPrice = m_studyControl.PositionSide > 0 ? averageEntryPrice + ProfitPrice : averageEntryPrice - ProfitPrice;
					return exitPrice > 0 ? exitPrice : 0;
				}
				return m_studyControl.PositionSide > 0 ? averageEntryPrice * (1 + ProfitPercent) : averageEntryPrice * (1 - ProfitPercent);
			}
		}

		/// <summary>
		/// Exit at the market close after a certain number of days.
		/// </summary>
		internal class ExitDays : Exit
		{
			public int Count { get; set; }
			public EExitDaysCountStrategy Strategy { get; set; }

			private bool m_exitOrderFilled;

			public ExitDays(_TrendLine control) : base(control)
			{
				OrderType = EOrderType.Market;
				Strategy = EExitDaysCountStrategy.TradingDays;
				m_exitOrderFilled = false;
				Count = 1;
			}

			public override bool Parse(JProperty property)
			{
				if (property.Name.ToLower() == "count") { Count = property.Value.ToObject<int>(); return true; }
				if (property.Name.ToLower() == "strategy") { Strategy = property.Value.ToObject<EExitDaysCountStrategy>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (OrderType != EOrderType.Market) return Tuple.Create(false, $"{InstructionTypeName} - Exit only supports market orders");
				if (Count <= 0) return Tuple.Create(false, $"{InstructionTypeName} - Count must be larger than 0");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_exitOrderFilled = false;
			}

			public override void CalcBar()
			{
				base.CalcBar();
				if (m_studyControl.PositionSide == 0)
				{
					//reset exit order state when position goes flat
					m_exitOrderFilled = false;
					return;
				}

				if (m_studyControl.Bars.Time[0] > ValidUntil) return;

				switch (m_studyControl.Environment.CalcReason)
				{
					case CalculationReason.OrderFilled:
						//check whether order was filled to ensure exit is only triggered once
						foreach (var item in m_studyControl.TradeManager.TradingData.Orders.Items)
						{
							if (item.OrderID == getOrderId() && item.LeftContracts == 0)
							{
								m_exitOrderFilled = true;
								m_studyControl.trace(ETraceLevel.Verbose, $"exitOrderFilled - OrderId {item.OrderID} - LeftContracts {item.LeftContracts}");
								break;
							}
						}
						break;
					case CalculationReason.Default:
						DateTime entryDate = m_studyControl.Positions[0].OpenTrades.Min(trade => trade.EntryOrder.Time);
						if (m_studyControl.Positions[0].ClosedTrades.Count > 0) entryDate = m_studyControl.Positions[0].ClosedTrades.Min(trade => trade.EntryOrder.Time);
						m_studyControl.trace(ETraceLevel.Verbose, $"entryDate {entryDate} - currentDateTime {m_studyControl.Bars.Time[0]} - Count {Count} - Strategy {Strategy} - exitOrderFilled {m_exitOrderFilled}");

						switch (Strategy)
						{
							case EExitDaysCountStrategy.CalendarDays:
								if (calendarDayCount(entryDate, m_studyControl.Bars.Time[0]) >= Count && !m_exitOrderFilled) sendOrder();
								break;
							case EExitDaysCountStrategy.TradingDays:
								if (tradingDayCount(entryDate, m_studyControl.Bars.Time[0]) >= Count && !m_exitOrderFilled) sendOrder();
								break;
						}

						break;
				}
			}

			private int calendarDayCount(DateTime entryDate, DateTime currentDateTime)
			{
				TimeSpan days = currentDateTime - entryDate;
				return days.Days;
			}

			private int tradingDayCount(DateTime entryDate, DateTime currentDateTime)
			{
				DateTime date = currentDateTime;
				int count = 0;

				if (m_studyControl.Bars.Info.Category != ESymbolCategory.Stock) m_studyControl.trace(ETraceLevel.Warning, $"WARNING - {InstructionTypeName} used with non-stock object using TradingDays specification calculated around stock market calendar.");

				while (date.Date > entryDate.Date)
				{
					//NOTE: This will not work correctly for non-US markets and non-equities that trade over the weekends, e.g. Forex.
					if (!m_studyControl.Holidays.Contains(date) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) count++;
					date = date.AddDays(-1);
				}

				return count;
			}

			private int getOrderId()
			{
				if (m_studyControl.PositionSide > 0)
					return m_orderLxMkt.ID;
				else if (m_studyControl.PositionSide < 0)
					return m_orderSxMkt.ID;
				return -1;
			}
		}

		//TBD - Check whether this should also support moving the stop-loss if new positions are added to an existing position. This will
		//      need to introduce a new boolean parameter, e.g. AdjustStopLossOnNewPosition, to recompute the stop-loss if a new position
		//      is added to the existing position.
		internal class ExitEntryBarLowHigh : ExitPriced
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
				if (BufferPrice < 0) return Tuple.Create(false, $"{InstructionTypeName} - BufferPrice must be greater than or equal to 0 (instruction will invert the sign if required)");
				if (OrderType == EOrderType.Market) return Tuple.Create(false, $"{InstructionTypeName} - Market exit not supported");
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
				else if (m_studyControl.Environment.CalcReason == CalculationReason.Default &&
								 m_studyControl.Bars.Status == EBarState.Close &&
								 m_stopLossPrice == -1.0)   //only calculate stop-loss price on close bars otherwise we could set the value incorrectly on internal bar price lows
				{
					if (m_studyControl.PositionSide > 0)
						m_stopLossPrice = m_studyControl.Bars.Low[0] - BufferPrice;
					else if (m_studyControl.PositionSide < 0)
						m_stopLossPrice = m_studyControl.Bars.High[0] + BufferPrice;
				}

				base.CalcBar();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				switch (direction)
				{
					case EEntryDirection.Long:
						return m_studyControl.Bars.Low[0] - BufferPrice;
					case EEntryDirection.Short:
						return m_studyControl.Bars.High[0] + BufferPrice;
				}

				//should not be reached
				return 0.0;
			}

			protected override double exitPrice()
			{
				return m_stopLossPrice;
			}
		}

		//TBD - Check whether this should also support moving the stop-loss if new positions are added to an existing position. This will
		//      need to introduce a new boolean parameter, e.g. AdjustStopLossOnNewPosition, to recompute the stop-loss if a new position
		//      is added to the existing position.
		internal class ExitEntryDayLowHigh : ExitPriced
		{
			public static TimeSpan DEFAULT_MARKET_OPEN_TIME = new TimeSpan(9, 30, 0);
			public static TimeSpan DEFAULT_MARKET_CLOSE_TIME = new TimeSpan(16, 0, 0);
			public double BufferPrice { get; set; }
			public bool IncludePreMarket { get; set; }
			public bool IncludePostMarket { get; set; }
			private double m_stopLossPrice;

			public ExitEntryDayLowHigh(_TrendLine control) : base(control)
			{
				BufferPrice = 0.0;
				IncludePreMarket = false;
				IncludePostMarket = false;
			}

			public override bool Parse(JProperty property)
			{
				if (property.Name.ToLower() == "bufferprice") { BufferPrice = property.Value.ToObject<double>(); return true; }
				if (property.Name.ToLower() == "includepremarket") { IncludePreMarket = property.Value.ToObject<bool>(); return true; }
				if (property.Name.ToLower() == "includepostmarket") { IncludePostMarket = property.Value.ToObject<bool>(); return true; }

				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (BufferPrice < 0) return Tuple.Create(false, $"{InstructionTypeName} - BufferPrice must be greater than or equal to 0 (instruction will invert the sign if required)");
				if (OrderType == EOrderType.Market) return Tuple.Create(false, $"{InstructionTypeName} - Market exit not supported");
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
					m_stopLossPrice = -1.0;     //reset stop-loss price when position went flat
				else if (m_studyControl.Environment.CalcReason == CalculationReason.Default &&
								 m_studyControl.Bars.Status == EBarState.Close &&
								 m_stopLossPrice == -1.0)   //only calculate stop-loss price on close bars otherwise we could set the value incorrectly on internal bar price lows
				{
					if (m_studyControl.PositionSide > 0)
						m_stopLossPrice = getDayLow() - BufferPrice;
					else if (m_studyControl.PositionSide < 0)
						m_stopLossPrice = getDayHigh() + BufferPrice;
				}

				base.CalcBar();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				switch (direction)
				{
					case EEntryDirection.Long:
						return getDayLow() - BufferPrice;
					case EEntryDirection.Short:
						return getDayHigh() + BufferPrice;
				}

				//should not be reached
				return 0.0;
			}

			public double getDayLow()
			{
				DateTime startBarDate = m_studyControl.Bars.Time[0].Date;
				int index = 1;
				double result = m_studyControl.Bars.Low[0];
				while (m_studyControl.Bars.Time[index].Date == startBarDate.Date && index < m_studyControl.Bars.CurrentBar)
				{
					if (!IncludePreMarket && m_studyControl.Bars.Time[index].TimeOfDay <= DEFAULT_MARKET_OPEN_TIME) break; //no more values to check
					if (!IncludePostMarket && m_studyControl.Bars.Time[index].TimeOfDay >= DEFAULT_MARKET_CLOSE_TIME)
					{
						index++;
						continue; //skip values after market close
					}
					result = m_studyControl.Bars.Low[index] < result ? m_studyControl.Bars.Low[index] : result;
					index++;
				}

				return result;
			}

			public double getDayHigh()
			{
				DateTime startBarDate = m_studyControl.Bars.Time[0].Date;
				int index = 1;
				double result = m_studyControl.Bars.High[0];
				while (m_studyControl.Bars.Time[index].Date == startBarDate.Date && index < m_studyControl.Bars.CurrentBar)
				{
					if (!IncludePreMarket && m_studyControl.Bars.Time[index].TimeOfDay <= DEFAULT_MARKET_OPEN_TIME) break; //no more values to check
					if (!IncludePostMarket && m_studyControl.Bars.Time[index].TimeOfDay >= DEFAULT_MARKET_CLOSE_TIME)
					{
						index++;
						continue; //skip values after market close
					}
					result = m_studyControl.Bars.High[index] > result ? m_studyControl.Bars.High[index] : result;
					index++;
				}

				return result;
			}

			protected override double exitPrice()
			{
				return m_stopLossPrice;
			}
		}

		internal class ExitTrailingStop : ExitPriced
		{
			public enum ETrailingType { FixedAmount, Percentage, ATR }
			public ETrailingType TrailingType { get; set; }
			public EEntryExitCriteria Criteria { get; set; }
			public double ActivationProfitPercent { get; set; }
			public double ActivationProfitAmount { get; set; }
			public double TrailingValue { get; set; }
			public int AtrLength { get; set; }
			private double m_trailPrice;
			private PowerLanguage.Function._ATR m_atr;
			private bool m_active;

			public ExitTrailingStop(_TrendLine control) : base(control)
			{
				Criteria = EEntryExitCriteria.Close;
				ActivationProfitPercent = 0.0;
				ActivationProfitAmount = 0.0;
				TrailingType = ETrailingType.FixedAmount;
				TrailingValue = 1.0;
				AtrLength = 14;
				m_trailPrice = 0.0;
				m_active = false;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "activationprofitpercent") { ActivationProfitPercent = property.Value.ToObject<double>(); return true; }
				if (name == "activationprofitamount") { ActivationProfitAmount = property.Value.ToObject<double>(); return true; }
				if (name == "trailingtype") { TrailingType = property.Value.ToObject<ETrailingType>(); return true; }
				if (name == "trailingvalue") { TrailingValue = property.Value.ToObject<double>(); return true; }
				if (name == "atrlength") { AtrLength = property.Value.ToObject<int>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (ActivationProfitPercent <= 0 && ActivationProfitAmount <= 0) return Tuple.Create(false, $"{this.GetType().Name} - ActivationProfitPercent or ActivationProfitAmount must be set to zero or a positive number");
				if (TrailingValue <= 0) return Tuple.Create(false, $"{this.GetType().Name} - TrailingValue must be positive");
				if (TrailingType == ETrailingType.ATR && AtrLength <= 0) return Tuple.Create(false, $"{this.GetType().Name} - AtrLength must be positive");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_trailPrice = 0.0;
				m_active = false;
				if (TrailingType == ETrailingType.ATR)
				{
					m_atr = m_studyControl.AllocateAtr();
					m_atr.Instrument = m_studyControl.Bars;
					m_atr.Length = AtrLength;
				}
			}

			public override void CalcBar()
			{
				if (m_studyControl.PositionSide == 0)
				{
					m_trailPrice = 0.0;
					m_active = false;
				}
				else
				{
					double trailValue = TrailingValue;
					if (TrailingType == ETrailingType.ATR) trailValue = m_atr[0] * TrailingValue;
					else
						if (TrailingType == ETrailingType.Percentage) trailValue = currentPositionAverageEntryPrice() * TrailingValue;

					if (m_studyControl.PositionSide > 0)
					{
						double newTrail = m_studyControl.Bars.High[0] - trailValue;
						m_trailPrice = Math.Max(m_trailPrice, newTrail);
					}
					else if (m_studyControl.PositionSide < 0)
					{
						double newTrail = m_studyControl.Bars.Low[0] + trailValue;
						m_trailPrice = m_trailPrice == 0.0 ? newTrail : Math.Min(m_trailPrice, newTrail);
					}
				}

				base.CalcBar();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				double entryPrice = criteria == EEntryExitCriteria.Close ? m_studyControl.Bars.Close[0] : m_studyControl.Bars.High[0];
				return direction == EEntryDirection.Long ? entryPrice - TrailingValue : entryPrice + TrailingValue;
			}

			protected override double exitPrice()
			{
				return m_trailPrice;
			}

			protected override bool Active
			{
				get
				{
					// Update activation state if it was not activated before
					if (!m_active)
					{
						double entryPrice = currentPositionAverageEntryPrice();
						double activationPrice = entryPrice;
						if (ActivationProfitAmount > 0)
							activationPrice = m_studyControl.PositionSide > 0 ? entryPrice + ActivationProfitAmount : entryPrice - ActivationProfitAmount;
						else
							activationPrice = m_studyControl.PositionSide > 0 ? entryPrice * (1.0 + ActivationProfitPercent) : entryPrice * (1.0 - ActivationProfitPercent);

						if (Criteria == EEntryExitCriteria.Close)
							m_active = m_studyControl.PositionSide > 0 ? m_studyControl.Bars.Close[0] >= activationPrice : m_studyControl.Bars.Close[0] <= activationPrice;
						else
							m_active = m_studyControl.PositionSide > 0 ? m_studyControl.Bars.High[0] >= activationPrice : m_studyControl.Bars.Low[0] <= activationPrice;
					}

					return base.Active && m_active;
				}
			}
		}

		internal class ExitBreakeven : Instruction
		{
			public double ActivationProfitPercent { get; set; }
			public double ActivationProfitAmount { get; set; }

			public ExitBreakeven(_TrendLine control) : base(control)
			{
				ActivationProfitPercent = 0.0;
				ActivationProfitAmount = 0.0;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "activationprofitpercent") { ActivationProfitPercent = property.Value.ToObject<double>(); return true; }
				if (name == "activationprofitamount") { ActivationProfitAmount = property.Value.ToObject<double>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (ActivationProfitPercent <= 0 && ActivationProfitAmount <= 0) return Tuple.Create(false, $"{this.GetType().Name} - ActivationProfitPercent or ActivationProfitPrice must be set to zero or a positive number");
				return base.IsValid();
			}

			public override void CalcBar()
			{
				base.CalcBar();
				if (m_studyControl.PositionSide == 0) return;
				m_studyControl.CurSpecOrdersMode = ESpecOrdersMode.PerContract;
				double profit = ActivationProfitAmount;
				if (ActivationProfitPercent > 0)
					profit = m_studyControl.StrategyInfo.AvgEntryPrice * ActivationProfitPercent;
				m_studyControl.trace(ETraceLevel.Verbose, $"avgEntryPrice {m_studyControl.StrategyInfo.AvgEntryPrice} - profit {profit}");
				m_studyControl.GenerateBreakEven(profit);
			}
		}

		/// <summary>
		/// InstructionFactory used to create instructions based on some specific instruction name and associated type.
		/// </summary>
		internal static class InstructionFactory
		{
			private static readonly Dictionary<string, Func<_TrendLine, Instruction>> m_instructionCreators = new Dictionary<string, Func<_TrendLine, Instruction>>
			{
					{ "entryline", control => new _TrendLine.EntryLine(control) },
					{ "exitline", control => new _TrendLine.ExitLine(control) },
					{ "exitmovingaverage", control => new _TrendLine.ExitMovingAverage(control) },
					{ "exitoverextension", control => new _TrendLine.ExitOverextension(control) },
					{ "exitstoploss", control => new _TrendLine.ExitStopLoss(control) },
					{ "exitprofit", control => new _TrendLine.ExitProfit(control) },
					{ "exitdays", control => new _TrendLine.ExitDays(control) },
					{ "exitentrybarlowhigh", control => new _TrendLine.ExitEntryBarLowHigh(control) },
					{ "exitentrydaylowhigh", control => new _TrendLine.ExitEntryDayLowHigh(control) },
					{ "exittrailingstop", control => new _TrendLine.ExitTrailingStop(control) },
					{ "exitbreakeven", control => new _TrendLine.ExitBreakeven(control) },
			};

			public static Instruction CreateInstruction(string type, _TrendLine control)
			{
				Func<_TrendLine, Instruction> creator;
				if (m_instructionCreators.TryGetValue(type.ToLower(), out creator))
					return creator(control);
				throw new ArgumentException($"Unsupported instruction type: {type}");
			}
		}

		//inputs
		[Input] public string Instructions { get; set; }

		//properties
		internal List<Instruction> InstructionsList { get; private set; }
		public List<DateTime> Holidays { get { return m_holidays.Holidays; } }

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

		private List<PowerLanguage.Function._AverageSimple> m_averageSimple;
		private int m_averageSimpleAllocated;
		private List<PowerLanguage.Function._AverageExponential> m_averageExponential;
		private int m_averageExponentialAllocated;
		private List<PowerLanguage.Function._SuperSmoother> m_averageSuperSmoothers;
		private int m_averageSuperSmoothersAllocated;
		private List<PowerLanguage.Function._ATR> m_atr;
		private int m_atrAllocated;
		private List<PowerLanguage.Function._VolumeWithSD> m_volumeWithSD;
		private int m_volumeWithSDAllocated;
		private PowerLanguage.Function.USHolidays m_holidays;

		//interface methods
		public _TrendLine(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			TraceLevel = ETraceLevel.None;
			createOrders();
			createIndicators();
			InstructionsList = new List<Instruction>();
			m_holidays = new PowerLanguage.Function.USHolidays(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			resetOrders();
			resetIndicators();
			parseInstructions();
			foreach (var instruction in InstructionsList) instruction.StartCalc();
			validateInstructions();
		}

		protected override void StopCalc()
		{
			foreach (var instruction in InstructionsList) instruction.StopCalc();
			base.StopCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			evaluateIndicators();
			foreach (var instruction in InstructionsList) instruction.CalcBar();
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

		public PowerLanguage.Function._AverageSimple AllocateSimpleAverage()
		{
			if (m_averageSimpleAllocated >= m_averageSimple.Count) throw new InvalidOperationException("No more Simple Moving Averages available");
			PowerLanguage.Function._AverageSimple result = m_averageSimple[m_averageSimpleAllocated];
			m_averageSimpleAllocated++;
			return result;
		}

		public PowerLanguage.Function._AverageExponential AllocateExponentialAverage()
		{
			if (m_averageExponentialAllocated >= m_averageExponential.Count) throw new InvalidOperationException("No more Exponential Moving Averages available");
			PowerLanguage.Function._AverageExponential result = m_averageExponential[m_averageExponentialAllocated];
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

		public PowerLanguage.Function._ATR AllocateAtr()
		{
			if (m_atrAllocated >= m_atr.Count) throw new InvalidOperationException("No more ATRs available");
			PowerLanguage.Function._ATR result = m_atr[m_atrAllocated];
			m_atrAllocated++;
			return result;
		}

		public PowerLanguage.Function._VolumeWithSD AllocateVolumeWithSD()
		{
			if (m_volumeWithSDAllocated >= m_volumeWithSD.Count) throw new InvalidOperationException("No more Volume with SDs available");
			PowerLanguage.Function._VolumeWithSD result = m_volumeWithSD[m_volumeWithSDAllocated];
			m_volumeWithSDAllocated++;
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
				m_leStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LEStp" + i, EOrderAction.Buy)));
				m_seStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SEStp" + i, EOrderAction.SellShort)));
				m_leLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LELmt" + i, EOrderAction.Buy)));
				m_seLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SELmt" + i, EOrderAction.SellShort)));
				m_leStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LEStpLmt" + i, EOrderAction.Buy)));
				m_seStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SEStpLmt" + i, EOrderAction.SellShort)));

				m_lxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LXMrk" + i, EOrderAction.Sell, OrderExit.Total)));
				m_sxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SXMrk" + i, EOrderAction.BuyToCover, OrderExit.Total)));
				m_lxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LXStp" + i, EOrderAction.Sell, OrderExit.Total)));
				m_sxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SXStp" + i, EOrderAction.BuyToCover, OrderExit.Total)));
				m_lxLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXLmt" + i, EOrderAction.Sell, OrderExit.Total)));
				m_sxLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXLmt" + i, EOrderAction.BuyToCover, OrderExit.Total)));
				m_lxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LXStpLmt" + i, EOrderAction.Sell, OrderExit.Total)));
				m_sxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SXStpLmt" + i, EOrderAction.BuyToCover, OrderExit.Total)));
			}
		}

		private void createIndicators()
		{
			m_averageSimple = new List<PowerLanguage.Function._AverageSimple>();
			m_averageExponential = new List<PowerLanguage.Function._AverageExponential>();
			m_averageSuperSmoothers = new List<PowerLanguage.Function._SuperSmoother>();
			m_atr = new List<PowerLanguage.Function._ATR>();
			m_volumeWithSD = new List<PowerLanguage.Function._VolumeWithSD>();

			for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
			{
				m_averageSimple.Add(new PowerLanguage.Function._AverageSimple(this));
				m_averageExponential.Add(new PowerLanguage.Function._AverageExponential(this));
				m_averageSuperSmoothers.Add(new PowerLanguage.Function._SuperSmoother(this));
			}

			for (int i = 0; i < MAX_ATR; i++)
				m_atr.Add(new PowerLanguage.Function._ATR(this));

			for (int i = 0; i < MAX_VOLUME_WITH_SD; i++)
				m_volumeWithSD.Add(new PowerLanguage.Function._VolumeWithSD(this));
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
			m_seStpLmtAllocated = 0;

			m_lxMrkAllocated = 0;
			m_sxMrkAllocated = 0;
			m_lxStpAllocated = 0;
			m_sxStpAllocated = 0;
			m_lxLmtAllocated = 0;
			m_sxLmtAllocated = 0;
			m_lxStpLmtAllocated = 0;
			m_sxStpLmtAllocated = 0;
		}

		private void resetIndicators()
		{
			m_averageSimpleAllocated = 0;
			m_averageExponentialAllocated = 0;
			m_averageSuperSmoothersAllocated = 0;
			m_atrAllocated = 0;
			m_volumeWithSDAllocated = 0;

			for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
			{
				m_averageSimple[i].Instrument = Bars;
				m_averageSimple[i].Length = 10;
				m_averageExponential[i].Instrument = Bars;
				m_averageExponential[i].Length = 10;
				m_averageSuperSmoothers[i].Instrument = Bars;
				m_averageSuperSmoothers[i].Length = 10;
			}

			for (int i = 0; i < MAX_ATR; i++)
			{
				m_atr[i].Instrument = Bars;
				m_atr[i].Length = 14;
			}

			for (int i = 0; i < MAX_VOLUME_WITH_SD; i++)
			{
				m_volumeWithSD[i].Instrument = Bars;
				m_volumeWithSD[i].Length = 20;
			}
		}

		//NOTE: Not all instructions access the values of the indicators, so we need to evaluate them all to ensure that the CalcBar is called for all
		//      of them otherwise the initially computed values are incorrect.
		private void evaluateIndicators()
		{
			for (int i = 0; i < m_averageSimpleAllocated; i++)
				m_averageSimple[i].Call();

			for (int i = 0; i < m_averageExponentialAllocated; i++)
				m_averageExponential[i].Call();

			for (int i = 0; i < m_averageSuperSmoothersAllocated; i++)
				m_averageSuperSmoothers[i].Call();

			for (int i = 0; i < m_atrAllocated; i++)
				m_atr[i].Call();
		}

		private void parseInstructions()
		{
			InstructionsList.Clear();
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
				if (instructionStr == "") throw new ArgumentException($"Instruction type not found - {property.ToString()}");
				var instruction = InstructionFactory.CreateInstruction(instructionStr, this);
				instruction.Parse(property);
				InstructionsList.Add(instruction);
			}
		}

		private void validateInstructions()
		{
			foreach (var instruction in InstructionsList)
			{
				var result = instruction.IsValid();
				if (!result.Item1) throw new ArgumentException(result.Item2);
			}
		}
	}
}