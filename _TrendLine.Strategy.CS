using System;
using System.Globalization;
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
	///	• Direction - enum of long, short, default long
	///	• EquityPercent - percent of the ACCOUNT equity to enter into the position
	/// • [optional] Stategy - enum of Breakout, Fade, BreakoutOrFade - determines the strategy to use for the entry, defaults to Breakout
	///	• [optional] MaxVolumeBasedPercent - percent of the average volume to use as the maximum position size, default 0.2 (20%), set to 0.0 to disable.
	///	• [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	///	• [optional] ClosePercentile - (only used when Criteria == Close) double value between 0.0 and 1.0, e.g. 0.05 would mean that for a long trade the close price must be within 5% of the high of the bar
	///	                               while for short trades it would mean the close price must be within 5% of the low of the bar - default value is 0.5 (50%) meaning for long trades the close price must be
	///	                               in the top half of the bar and for short trades the close price must be in the bottom half of the bar
	///	• [optional] StartTime/EndTime - Start/end time filters on which the entry should be checked, e.g. 9:35 AM start to 10:00 AM end would look for an entry the first half hour of the regular stock trading day
	///	• [optional] ValidUntil - date/time until which the entry should hold, defaults to DateTime.Max (if this field is specified with the EndDate/EndTime fields the entry would become disabled which ever date/time comes first)
	///	• [optional] AddToPosition - if the trend line is crossed and we are already in a position add to the position
	///	• [optional] StopLossExitId - Creates a risk based entry in conjunction with the given exit stop loss Id (blank to disable risk based entry), e.g. when EquityPercent is 0.01 (1%) the position size would risk 1%
	///	                              of the account equity based on the entry price and the stop-loss price.
	///	• [optional] MaxRiskBasedEquityPercent - When a risk based entry is used (see StopLossExitId) the maximum percent of the account equity to use for the position as risk based position entries can become really big,
	///	                                         default is 1.0 (100%) of the account.
	///  
	/// ExitLine
	/// • Instruction - "ExitLine" 
	/// • LineId - trend line id
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	///	• [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	///	• [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
	/// 
	/// ExitMovingAverage
	/// • Instruction - "ExitMovingAverage" 
	/// • Length - length of the moving average.
	/// • MovingAverageType - enum Simple, Exponential, SuperSmoother
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max
	///
	/// ExitOverextension
	/// • Instruction - "ExitOverextension"
	/// • Length - length of the moving average
	/// • MovingAverageType - enum Simple, Exponential, SuperSmoother
	/// • AtrLength - length to compute the average true range
	/// • AtrMultiplier - factor to multiply the average true range to get a price level above/below the moving average 
	/// • [optional] Criteria - enum of Close, LowHigh - count the criteria met only if the bar closes over the trend line vs price just moving over the trend line, defaults to Close.
	/// • [optional] ExitPercent - percent of position to close, default 1.0 (100%)
	/// • [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop(TBD this could cause slippage)
	/// • [optional] ValidUntil - date/time until which the exit should hold, defaults to DateTime.Max 
	///
	/// ExitStopLoss
	/// • Instruction - "ExitStopLoss" 
	///	• [alternate] LossPercent - exit the position if the price moves against you by the specified percentage.
	///	• [alternate] LossPrice - exit the position if the price moves against you by a specified amount. 
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitProfit
	/// • Instruction - "ExitProfit" 
	///	• [alternate] ProfitPercent - exit the position one the price moved in your favor by this percentage
	///	• [alternate] ProfitPrice - exit the position one the price moved in your favor by this amount
	/// • [optional] ExitPercent - percent of INITIAL POSITION to close, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitDays
	/// • Instruction - "ExitDays" 
	/// • Count - number of days after which the position should be closed
	/// • [optional] Strategy - enum CalendarDays, TradingDays - whether to count calendar days or trading days, defaults to TradingDays
	///	• [optional] ExitPercent - percent of INITIAL POSITION to exit, default 1.0 (100%) 
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	/// 
	/// ExitEntryBarLowHigh
	/// • Instruction - "ExitEntryBarLowHigh" 
	///	• [optional] BufferPrice - buffer price to add/subtract from entry bar extreme
	///	• [optional] PositionPercent - percent of INITIAL POSITION to exit, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	///	
	/// ExitEntryDayLowHigh
	/// • Instruction - "ExitDayLowHigh"
	///	• [optional] BufferPrice - buffer price to add/subtract from entry day extreme
	///	• [optional] PositionPercent - percent of INITIAL POSITION to exit, default 1.0 (100%)
	///	• [optional] OrderType - enum Stop, StopLimit - whether to use a stop order or a stop-limit order for the exit, defaults to Stop (TBD this could cause slippage)
	///	• [optional] IncludePreMarket - include pre-market data when computing the entry day high/low, default false
	///	• [optional] IncludePostMarket - include post-market data when computing the entry day high/low, default false
	/// 
	/// Example JSON:
	/// [
	///	    {
	///	        "Instruction" : "EntryLine"
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
		public const int DEFAULT_VOLUME_PERIOD = 20;

		//types
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

		public abstract class Instruction
		{
			public _TrendLine m_studyControl;

			public string Name { get; set; }
			public string Id { get; set; }

			public Instruction(_TrendLine control) { m_studyControl = control; }

			public virtual bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "instruction") { Name = property.Value.ToString().ToLower(); return true; }
				if (name == "id") { Id = property.Value.ToString().ToLower(); return true; }
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
			/// Note: Market position is negative for short positions so we need to use the absolute value.
			/// </summary>
			protected int currentOpenPositionSize()
			{
				return Math.Abs(m_studyControl.StrategyInfo.MarketPosition);
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
		}

		public abstract class Entry : Instruction
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
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, "Entry - EquityPercent must be between 0 and 1");
				if (MaxVolumeBasedPercent < 0 || MaxVolumeBasedPercent > 1.0) return Tuple.Create(false, "Entry - MaxVolumeBasedPercent must be between 0 and 1");
				if (StopLossExitId != string.Empty && m_stopLossExit == null) return Tuple.Create(false, "Entry - ExitStopLossId priced exit not found");
				if (MaxRiskBasedEquityPercent <= 0 || MaxRiskBasedEquityPercent > 1.0) return Tuple.Create(false, "Entry - MaxRiskBasedEquityPercent must be between 0 and 1");
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
				double positionEquity = equityAvailable * EquityPercent;
				double entryPrice = price == 0.0 ? m_studyControl.Bars.Close[0] : price;
				int size = (int)Math.Truncate(positionEquity / entryPrice);

				if (price != 0.0 && m_stopLossExit != null)
				{
					double stopLossPrice = m_stopLossExit.EstimateExitPrice(Direction, Strategy, Criteria);
					if (stopLossPrice != 0.0)
					{
						if (positionEquity > 0)
						{
							double risk;
							try
							{
								risk = checked(Math.Round(Math.Abs(entryPrice - stopLossPrice), 2));
								if (risk != 0.0d) size = checked((int)Math.Truncate(positionEquity / risk));

								//clip the available equity based on the maximum allowable risk based equity percent if required
								if (MaxRiskBasedEquityPercent < 1.0)
								{
									double maxRiskBasedEquityAvailable = equityAvailable * MaxRiskBasedEquityPercent;
									equityAvailable = equityAvailable > maxRiskBasedEquityAvailable ? maxRiskBasedEquityAvailable : equityAvailable;
								}

								//clip position size to the account equity available since risk based entries can
								//result in very large position sizes
								int maxPositionSize = (int)Math.Truncate(equityAvailable / entryPrice);
								size = size > maxPositionSize ? maxPositionSize : size;
							}
							catch (OverflowException)
							{
								m_studyControl.Output.WriteLine("Entry - entrySize positionSize based on stop-loss raised an overflow exception");
							}
						}
					}
					else if (m_studyControl.Debug) m_studyControl.Output.WriteLine("Entry - StopLossPrice is zero, using equity based position size");
				}

				if (MaxVolumeBasedPercent != 0.0)
				{
					int maximumPositionBasedOnVolume = (int)Math.Truncate(m_studyControl.Bars.Volume.Average(_TrendLine.DEFAULT_VOLUME_PERIOD) * MaxVolumeBasedPercent);
					size = maximumPositionBasedOnVolume < size ? maximumPositionBasedOnVolume : size;
				}

				return size;
			}

			/// <summary>
			/// Sends an entry order at an optional price and specific size based on the entry type and direction.
			/// For market orders the order is sent immediately so subclasses needs to make sure they send market
			/// orders only when required.
			/// </summary>
			protected void sendOrder(double price = 0.0)
			{
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
		}

		public abstract class Exit : Instruction
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
				if (base.Parse(property)) return true;
				bool result = false;
				string name = property.Name.ToLower();
				if (name == "ordertype") { OrderType = property.Value.ToObject<EOrderType>(); result = true; }
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
				if (m_studyControl.PositionSide == 0) return 0;
				if (ExitPercent == 1.0) return currentOpenPositionSize();
				//NOTE: Since MC does not support partial order filling we need to round up to the nearest integer larger integer otherwise
				//      partial positions remain that are stopped out etc.
				int size = (int)Math.Ceiling(currentTotalPositionSize() * ExitPercent);
				return size;
			}

			protected void sendOrder(double price = 0.0)
			{
				if (m_studyControl.PositionSide > 0)
				{
					switch (OrderType)
					{
						case EOrderType.Market:
							m_orderLxMkt.Send(exitSize());
							break;
						case EOrderType.Stop:
						case EOrderType.Limit:
							m_orderLxPriced.Send(price, exitSize());
							break;
						case EOrderType.StopLimit:
							m_orderLxSL.Send(price, price, exitSize());
							break;
					}
				}
				else if (m_studyControl.PositionSide < 0)
				{
					switch (OrderType)
					{
						case EOrderType.Market:
							m_orderSxMkt.Send(exitSize());
							break;
						case EOrderType.Stop:
						case EOrderType.Limit:
							m_orderSxPriced.Send(price, exitSize());
							break;
						case EOrderType.StopLimit:
							m_orderSxSL.Send(price, price, exitSize());
							break;
					}
				}
			}
		}

		/// <summary>
		/// Base class for priced exits used for stop/stop-limit orders at a determined price.
		/// </summary>
		public abstract class ExitPriced : Exit
		{
			public ExitPriced(_TrendLine control) : base(control) { }

			public override Tuple<bool, string> IsValid()
			{
				if (OrderType == EOrderType.Market) return Tuple.Create(false, "ExitPriced - Market exit not supported");
				return base.IsValid();
			}

			public override void CalcBar()
			{
				if (m_studyControl.PositionSide == 0) return;
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

		public class EntryLine : Entry
		{
			public int LineId { get; set; }
			public double ClosePercentile { get; set; }
			public DateTime StartTime { get; set; }
			public DateTime EndTime { get; set; }

			private ITrendLineObject m_line;
			private DateTime m_startDate;
			private DateTime m_endDate;

			public EntryLine(_TrendLine control) : base(control)
			{
				LineId = -1;
				ClosePercentile = 0.5;
				StartTime = DateTime.MinValue;
				EndTime = DateTime.MaxValue;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "lineid") { LineId = property.Value.ToObject<int>(); return true; }
				if (name == "closepercentile") { ClosePercentile = property.Value.ToObject<double>(); return true; }
				if (name == "starttime") { StartTime = DateTime.ParseExact(property.Value.ToString(), "hh:mm:ss", CultureInfo.InvariantCulture); return true; }
				if (name == "endtime") { EndTime = DateTime.ParseExact(property.Value.ToString(), "hh:mm:ss", CultureInfo.InvariantCulture); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (m_line == null) return Tuple.Create(false, string.Format("EntryLine - trendline with id {0} not found"));
				if (EquityPercent <= 0 || EquityPercent > 1.0) return Tuple.Create(false, "EntryLine - EntryPercent must be between 0 and 1");
				if (ClosePercentile < 0 || ClosePercentile > 1.0) return Tuple.Create(false, "EntryLine - ClosePercentile must be between 0 and 1");
				if (StartTime > EndTime) return Tuple.Create(false, "EntryLine - StartTime must be before EndTime");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_line = getTrendLine(LineId);
				m_startDate = DateTime.MinValue;
				m_endDate = DateTime.MaxValue;

				if (m_line != null)
				{
					m_startDate = m_line.Begin.Time;
					m_endDate = m_line.End.Time;
				}
			}

			public override void CalcBar()
			{
				if (m_studyControl.Bars.CurrentBar < 2) return;
				if (m_studyControl.PositionSide != 0 && !AddToPosition) return;
				if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
				if (m_studyControl.Bars.Time[0].Date < m_startDate || m_studyControl.Bars.Time[0].Date > m_endDate) return;
				if (m_studyControl.Bars.Time[0].TimeOfDay < StartTime.TimeOfDay || m_studyControl.Bars.Time[0].TimeOfDay > EndTime.TimeOfDay) return;
				if (m_studyControl.Bars.Time[0] > ValidUntil) return;

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
						bool entryCriteriaMet = false;

						switch (Strategy)
						{
							case EEntryStrategy.Breakout:
								entryCriteriaMet = m_studyControl.Bars.Close[1] < linePrice && m_studyControl.Bars.Close[0] > linePrice;
								break;
							case EEntryStrategy.Fade:
								entryCriteriaMet = m_studyControl.Bars.Close[1] > linePrice && m_studyControl.Bars.Close[0] < linePrice;
								break;
							case EEntryStrategy.BreakoutOrFade:
								entryCriteriaMet = (m_studyControl.Bars.Close[1] < linePrice && m_studyControl.Bars.Close[0] > linePrice) || (m_studyControl.Bars.Close[1] > linePrice && m_studyControl.Bars.Close[0] < linePrice);
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
								if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Long entry at close {1}, {2}", Name, m_studyControl.Bars.Time[0], close);
								sendOrder(close);
							}
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Long entry at low/high {1}, {2}", Name, m_studyControl.Bars.Time[0], linePrice);
						sendOrder(linePrice);
						break;
				}
			}

			private void shortEntry()
			{
				double linePrice = m_line.PriceValue(m_studyControl.Bars.Time[0]);
				switch (Criteria)
				{
					case EEntryExitCriteria.Close:
						bool entryCriteriaMet = false;
						switch (Strategy)
						{
							case EEntryStrategy.Breakout:
								entryCriteriaMet = m_studyControl.Bars.Close[1] > linePrice && m_studyControl.Bars.Close[0] < linePrice;
								break;
							case EEntryStrategy.Fade:
								entryCriteriaMet = m_studyControl.Bars.Close[1] < linePrice && m_studyControl.Bars.Close[0] > linePrice;
								break;
							case EEntryStrategy.BreakoutOrFade:
								entryCriteriaMet = (m_studyControl.Bars.Close[1] < linePrice && m_studyControl.Bars.Close[0] > linePrice) || (m_studyControl.Bars.Close[1] > linePrice && m_studyControl.Bars.Close[0] < linePrice);
								break;
						}

						if (entryCriteriaMet)
						{
							double low = m_studyControl.Bars.Low[0];
							double close = m_studyControl.Bars.Close[0];
							double range = m_studyControl.Bars.High[0] - low;
							//this will skip trades for bars with no range, for short trades 1 - closePercentile is used to invert the percentiles so that
							//close prices closer to the bottom of the bar would be considered higher percentiles
							double closePercentile = range != 0 ? 1 - (close - low) / range : 0;

							if (closePercentile >= ClosePercentile)
							{
								if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Short entry at close {1}, {2}", Name, m_studyControl.Bars.Time[0], close);
								sendOrder(close);
							}
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Short entry at low/high {1}, {2}", Name, m_studyControl.Bars.Time[0], linePrice);
						sendOrder(linePrice);
						break;
				}
			}
		};

		public class ExitLine : Exit
		{
			public int LineId { get; set; }
			public EEntryExitCriteria Criteria { get; set; }

			private ITrendLineObject m_line;
			private DateTime m_startDate;
			private DateTime m_endDate;

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
				if (m_line == null) return Tuple.Create(false, string.Format("ExitLine - trendline with id {0} not found", LineId));
				if (OrderType == EOrderType.Market) return Tuple.Create(false, "ExitLine - Market exit not supported");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_line = getTrendLine(LineId);
				m_startDate = DateTime.MinValue;
				m_endDate = DateTime.MaxValue;

				if (m_line != null)
				{
					m_startDate = m_line.Begin.Time;
					m_endDate = m_line.End.Time;
				}
			}

			public override void CalcBar()
			{
				if (m_studyControl.PositionSide == 0) return;
				if (m_studyControl.Bars.CurrentBar < 2) return;
				if (m_studyControl.Bars.Time[0].Date < m_startDate || m_studyControl.Bars.Time[0].Date > m_endDate) return;
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
							if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Long exit on close at {1}, {2}", Name, m_studyControl.Bars.Time[0], linePrice);
							sendOrder(m_studyControl.Bars.Close[0]);
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Long exit on low at {1}, {2}", Name, m_studyControl.Bars.Time[0], linePrice);
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
							if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Short exit on close at {0}, {1}", Name, m_studyControl.Bars.Time[0], linePrice);
							sendOrder(m_studyControl.Bars.Close[0]);
						}
						break;
					case EEntryExitCriteria.LowHigh:
						if (m_studyControl.Debug) m_studyControl.Output.WriteLine("{0} - Short exit on high at {0}, {1}", Name, m_studyControl.Bars.Time[0], linePrice);
						sendOrder(linePrice);
						break;
				}
			}
		}

		public class ExitMovingAverage : ExitPriced
		{
			public int Length { get; set; }
			public EMovingAverageType MovingAverageType { get; set; }
			public EEntryExitCriteria Criteria { get; set; }

			protected PowerLanguage.Function.AverageFC m_averageSimple;
			protected PowerLanguage.Function.XAverage m_averageExponential;
			protected PowerLanguage.Function._SuperSmoother m_averageSuperSmoother;

			public ExitMovingAverage(_TrendLine control) : base(control)
			{
				Criteria = EEntryExitCriteria.Close;
				ExitPercent = 1.0;
				OrderType = EOrderType.Stop;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "length") { Length = property.Value.ToObject<int>(); return true; }
				if (name == "movingaveragetype") { MovingAverageType = property.Value.ToObject<EMovingAverageType>(); return true; }
				if (name == "criteria") { Criteria = property.Value.ToObject<EEntryExitCriteria>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (Length <= 0) return Tuple.Create(false, "ExitMovingAverage - Length must be greater than 0");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_averageSimple = null;
				m_averageExponential = null;
				m_averageSuperSmoother = null;

				switch (MovingAverageType)
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
				if (Criteria == EEntryExitCriteria.Close && m_studyControl.Bars.Status != EBarState.Close) return;
				base.CalcBar();
			}

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
				return exitPrice();
			}
				
			protected override double exitPrice()
			{
				double result = 0.0;
				switch (MovingAverageType)
				{
					case EMovingAverageType.Simple:
						result = m_averageSimple[0];
						break;
					case EMovingAverageType.Exponential:
						result = m_averageExponential[0];
						break;
					case EMovingAverageType.SuperSmoother:
						result = m_averageSuperSmoother[0];
						break;
				}

				//should never be reached
				if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - exitPrice - could not determine exit price");
				return result;
			}
		}

		public class ExitOverextension : ExitMovingAverage
		{
			public int AtrLength { get; set; }
			public double AtrMultiplier { get; set; }

			protected PowerLanguage.Function._ATR m_atr;

			public ExitOverextension(_TrendLine control) : base(control)
			{
				AtrLength = 20;
				AtrMultiplier = 3.0;
			}

			public override bool Parse(JProperty property)
			{
				string name = property.Name.ToLower();
				if (name == "atrlength") { AtrLength = property.Value.ToObject<int>(); return true; }
				if (name == "atrmultiplier") { AtrMultiplier = property.Value.ToObject<double>(); return true; }
				return base.Parse(property);
			}

			public override Tuple<bool, string> IsValid()
			{
				if (AtrLength <= 0) return Tuple.Create(false, "ExitOverextension - AtrLength must be greater than 0");
				if (AtrMultiplier <= 0) return Tuple.Create(false, "ExitOverextension - AtrFactor must be greater than 0");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_atr = m_studyControl.AllocateAtr();
				m_atr.Length = AtrLength;
			}

			protected override double exitPrice()
			{
				double result = 0.0;
				switch (MovingAverageType)
				{
					case EMovingAverageType.Simple:
						result = m_averageSimple[0];
						break;
					case EMovingAverageType.Exponential:
						result = m_averageExponential[0];
						break;
					case EMovingAverageType.SuperSmoother:
						result = m_averageSuperSmoother[0];
						break;
				}

				if (m_studyControl.PositionSide > 0)
					result += m_atr[0] * AtrMultiplier;
				else if (m_studyControl.PositionSide < 0)
					result -= m_atr[0] * AtrMultiplier;

				//should never be reached
				if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitMovingAverage - exitPrice - could not determine exit price");
				return result;
			}

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

			public override double EstimateExitPrice(EEntryDirection direction, EEntryStrategy strategy, EEntryExitCriteria criteria)
			{
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

				//should never be reached
				if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitStopLoss - EstimateExitPrice - could not determine exit price");
				return 0.0;
			}

			protected override double exitPrice()
			{
				double averageEntryPrice = currentPositionAverageEntryPrice();
				if (LossPrice != -1.0) return m_studyControl.PositionSide > 0 ? averageEntryPrice - LossPrice : averageEntryPrice + LossPrice;
				double priceDelta = averageEntryPrice * LossPercent;
				return m_studyControl.PositionSide > 0 ? averageEntryPrice - priceDelta : averageEntryPrice + priceDelta;
			}
		}

		public class ExitProfit : ExitPriced
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
				if (OrderType == EOrderType.Stop) return Tuple.Create(false, "ExitProfit - Stop exit not supported");
				if (ProfitPercent == -1.0 && ProfitPrice == -1.0) return Tuple.Create(false, "ExitProfit - ProfitPercent or ProfitPrice must be specified");
				if (ProfitPercent != -1.0 && ProfitPercent <= 0.0) return Tuple.Create(false, "ExitProfit - ProfitPercent must be a positive integer, e.g. 1.2 would mean 120% profit");
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
				if (m_studyControl.Debug) m_studyControl.Output.WriteLine("ExitProfit - EstimateExitPrice - could not determine exit price");
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
		public class ExitDays : Exit
		{
			public int Count { get; set; }
			public EExitDaysCountStrategy Strategy { get; set; }

			private bool m_exitOrderFilled;
			private int m_tradingDaysCount;
			private DateTime m_lastCheckedDate;

			public ExitDays(_TrendLine control) : base(control)
			{
				OrderType = EOrderType.Market;
				Strategy = EExitDaysCountStrategy.TradingDays;
				m_exitOrderFilled = false;
				m_tradingDaysCount = 0;
				m_lastCheckedDate = DateTime.MinValue;
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
				if (OrderType != EOrderType.Market) return Tuple.Create(false, "ExitDays - Exit only supports market orders");
				if (Count <= 0) return Tuple.Create(false, "ExitDays - Count must be larger than 0");
				return base.IsValid();
			}

			public override void StartCalc()
			{
				base.StartCalc();
				m_exitOrderFilled = false;
				m_tradingDaysCount = 0;
				m_lastCheckedDate = DateTime.MinValue;
			}

			public override void CalcBar()
			{
				if (m_studyControl.PositionSide == 0)
				{
					//reset exit order state when position goes flat
					m_exitOrderFilled = false;
					m_tradingDaysCount = 0;
					m_lastCheckedDate = DateTime.MinValue;
					return;
				}
				if (m_studyControl.Bars.Time[0] > ValidUntil) return;

				switch (m_studyControl.Environment.CalcReason)
				{
					case CalculationReason.OrderFilled:
						//check whether order was filled to ensure exit is only triggered once
						foreach (var item in m_studyControl.TradeManager.TradingData.Orders.Items)
						{
							if (item.OrderID == getOrderId() && item.LeftContracts == 0) m_exitOrderFilled = true;
							break;
						}
						break;
					case CalculationReason.Default:
						DateTime entryDate = m_studyControl.Positions[0].OpenTrades.Min(trade => trade.EntryOrder.Time);
						if (m_studyControl.Positions[0].ClosedTrades.Count > 0) entryDate = m_studyControl.Positions[0].ClosedTrades.Min(trade => trade.EntryOrder.Time);

						switch (Strategy)
						{
							case EExitDaysCountStrategy.CalendarDays:
								TimeSpan days = m_studyControl.Bars.Time[0] - entryDate;
								if (days.Days >= Count && !m_exitOrderFilled) sendOrder();
								break;
							case EExitDaysCountStrategy.TradingDays:
								//TBD - need to make sure this will work correctly when a position is adopted from the broker, if the strategy is not calculated
								//      all the way up to the current bar the trading days count will be off
								// https://www.multicharts.com/trading-software/index.php?title=Advanced._AutoTrading
								// https://www.multicharts.com/trading-software/index.php/Auto_Trading#Recalculate_on_Broker_Events
								if (m_lastCheckedDate == DateTime.MinValue)
								{
									m_tradingDaysCount = 1;
									m_lastCheckedDate = m_studyControl.Bars.Time[0];
									return;
								}

								if (m_studyControl.Bars.Time[0].Date != m_lastCheckedDate.Date)
								{
									m_lastCheckedDate = m_studyControl.Bars.Time[0];
									m_tradingDaysCount++;
								}

								if (m_tradingDaysCount >= Count && !m_exitOrderFilled) sendOrder();
								break;
						}

						break;
				}


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
				if (OrderType == EOrderType.Market) return Tuple.Create(false, "ExitEntryBarLowHigh - Market exit not supported");
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
		public class ExitEntryDayLowHigh : ExitPriced
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
				if (BufferPrice < 0) return Tuple.Create(false, "ExitEntryDayLowHigh - BufferPrice must be greater than or equal to 0 (instruction will invert the sign if required)");
				if (OrderType == EOrderType.Market) return Tuple.Create(false, "ExitEntryDayLowHigh - Market exit not supported");
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
								{ "exitoverextension", control => new _TrendLine.ExitOverextension(control) },
								{ "exitstoploss", control => new _TrendLine.ExitStopLoss(control) },
								{ "exitprofit", control => new _TrendLine.ExitProfit(control) },
								{ "exitdays", control => new _TrendLine.ExitDays(control) },
								{ "exitentrybarlowhigh", control => new _TrendLine.ExitEntryBarLowHigh(control) },
								{ "exitentrydaylowhigh", control => new _TrendLine.ExitEntryDayLowHigh(control) },
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
		public List<Instruction> InstructionsList { get; private set; }

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
		private List<PowerLanguage.Function._ATR> m_atr;
		private int m_atrAllocated;

		//interface methods
		public _TrendLine(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Debug = false;
			createOrders();
			createIndicators();
			InstructionsList = new List<Instruction>();
		}

		protected override void StartCalc()
		{
			resetOrders();
			resetIndicators();
			parseInstructions();
			foreach (var instruction in InstructionsList) instruction.StartCalc();
			validateInstructions();
		}

		protected override void StopCalc()
		{
			foreach (var instruction in InstructionsList) instruction.StopCalc();
		}

		protected override void CalcBar()
		{
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

		public PowerLanguage.Function._ATR AllocateAtr()
		{
			if (m_atrAllocated >= m_atr.Count) throw new InvalidOperationException("No more ATRs available");
			PowerLanguage.Function._ATR result = m_atr[m_atrAllocated];
			m_atrAllocated++;
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

				m_lxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LXMrk" + i, EOrderAction.Sell)));
				m_sxMrk.Add(OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SXMrk" + i, EOrderAction.BuyToCover)));
				m_lxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LXStp" + i, EOrderAction.Sell)));
				m_sxStp.Add(OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SXStp" + i, EOrderAction.BuyToCover)));
				m_lxLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXLmt" + i, EOrderAction.Sell)));
				m_sxLmt.Add(OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXLmt" + i, EOrderAction.BuyToCover)));
				m_lxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "LXStpLmt" + i, EOrderAction.Sell)));
				m_sxStpLmt.Add(OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SXStpLmt" + i, EOrderAction.BuyToCover)));
			}
		}

		private void createIndicators()
		{
			m_averageSimple = new List<PowerLanguage.Function.AverageFC>();
			m_averageExponential = new List<PowerLanguage.Function.XAverage>();
			m_averageSuperSmoothers = new List<PowerLanguage.Function._SuperSmoother>();
			m_atr = new List<PowerLanguage.Function._ATR>();

			for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
			{
				m_averageSimple.Add(new PowerLanguage.Function.AverageFC(this));
				m_averageExponential.Add(new PowerLanguage.Function.XAverage(this));
				m_averageSuperSmoothers.Add(new PowerLanguage.Function._SuperSmoother(this));
			}

			for (int i = 0; i < MAX_ATR; i++)
				m_atr.Add(new PowerLanguage.Function._ATR(this));
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

		private void resetIndicators()
		{
			m_averageSimpleAllocated = 0;
			m_averageExponentialAllocated = 0;
			m_averageSuperSmoothersAllocated = 0;
			m_atrAllocated = 0;

			for (int i = 0; i < MAX_MOVINGAVERAGES; i++)
			{
				m_averageSimple[i].price = Bars.Close;
				m_averageExponential[i].Price = Bars.Close;
				m_averageSuperSmoothers[i].Price = Bars.Close;
			}

			for (int i = 0; i < MAX_ATR; i++)
				m_atr[i].Instrument = Bars;
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
				if (instructionStr == "") throw new ArgumentException(string.Format("Instruction type not found - {0}", property.ToString()));
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