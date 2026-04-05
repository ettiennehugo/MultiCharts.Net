using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Common base class for breakout classes. Mainly encapsulates the trade management and exit logic.
	/// </summary>
	public abstract class _BreakoutBase : _CustomSignal
	{
		//types
		public enum EStopLossPlacement
		{
			EntryDay,     //take the entry day low/high as the stop-loss, default
			EntryDayMid,  //take the entry day middle as the stop-loss, useful for trade entries toward the end of the day
			PreEntryDay,  //take the day before the entry day low/high as stop-loss
			XDay,         //take the X/lookback-day lowest/highest as the stop-loss
		};

		//constants
		public const int DEFAULT_LOOKBACK = 5;
		public const double DEFAULT_EXPANSION_FACTOR = 1.0;
		public const int DEFAULT_ABSOLUTE_SIZE = 0;
		public const double DEFAULT_MAX_EQUITY_PERCENT = 0.3;
		public const double DEFAULT_MAX_VOLUME_PERCENT = 0.2;
		public const double DEFAULT_MAX_RISK_BASED_PERCENT = 0.01;
		public const int DEFAULT_VOLUME_LENGTH = 20;
		public const EStopLossPlacement DEFAULT_STOP_LOSS_PLACEMENT = EStopLossPlacement.EntryDay;
		public const double DEFAULT_STOP_LOSS_BUFFER = 0.0;
		public const double DEFAULT_FIRST_PROFIT_EXIT = 0.08;
		public const double DEFAULT_FIRST_PROFIT_POSITION = 0.5;
		public const double DEFAULT_SECOND_PROFIT_EXIT = 0.15;
		public const double DEFAULT_SECOND_PROFIT_POSITION = 0.25;
		public const double DEFAULT_MAX_PROFIT_PERCENT = 0.2;
		public const double DEFAULT_BREAK_EVEN_PROFIT = 0.0;
		public const int DEFAULT_ENTRY_DAY_EXTREME_CHECK = 2;
		public const int DEFAULT_FLATTEN_IF_NOT_PROFITABLE = 2;
		public const bool DEFAULT_USE_ROUND_LOTS = false;

		//properties
		//Entry parameters
		[Input] public int LookBack { get; set; }               // look back number of days for % change to detect expansion
		[Input] public double ExpansionFactor { get; set; }     // additional expansion factor to use to control expansion detection sensitivity, default 1.0 (e.g. 1.25 would require a 25% additional move above average last LookBack days % change)

		//Position size (risk control) parameters
		[Input] public int AbsoluteSize { get; set; }           // [alternate A] Overrides the below position sizing parameters to take an absolute position size irrespective or risk/equity, this is the BIG HAMMER for position sizing use with caution, default 0 to disable, greater than 0 to enable.
		[Input] public double MaxEquityPercent { get; set; }    // [alternate B] Maximum account size that can be allocated to the positions, default 0.3 for 30% allocation.
		[Input] public double MaxRiskBasedPercent { get; set; } // [alternate B] Maximum risk percentage based on stop-loss placement and entry price (current price), default 0.01 for 1%.
		[Input] public double MaxVolumePercent { get; set; }    // [alternate B] Maximum average volume to trade, default 0.2 for 20%.
		[Input] public EStopLossPlacement StopLossPlacement { get; set; } // [alternate B] Price location to take as the stop-loss price, default is entry day low.
		[Input] public double StopLossBuffer { get; set; }      // [alternate B] Stop loss buffer to add/subtract to/from stop-loss price, default 0, e.g. 0.01 would place stop-loss 1 cent below stop loss for long position (1 cent above for short position).
		[Input] public bool UseRoundLots { get; set; }          // [alternate B] Use round lots for position size.

		//Exit parameters
		[Input] public double FirstProfitExit { get; set; }         // Percentage profit to exit first partial position, 0.08 for 8% profit, 0 to disable (let it run).
		[Input] public double FirstProfitPosition { get; set; }     // Percentage for first exit of initial position size to exit when first profit target is hit, 0.5 for 50%/half position.
		[Input] public double SecondProfitExit { get; set; }        // Percentage profit to exit second partial position, 0.15 for 15% profit, 0 to disable (let it run).
		[Input] public double SecondProfitPosition { get; set; }    // Percentage of initial position size to exit when second profit target is hit, 0.25 for 25%/quarter position.
		[Input] public double MaxProfitPercent { get; set; }        // Maximum profit to take if reached, default 0.2 for 20%, 0 to disable (let it run), will exit full position.
		[Input] public double BreakEvenProfit { get; set; }         // Move stop-loss to breakeven once a certain profit percentage is hit, default is 0 to disable it, e.g. 0.05 would move the stop-loss to breakeven once a 5% profit is shown
		[Input] public int EntryDayExtremeCheck { get; set; }       // Require move beyond entry day extreme (low/high) if not moved beyond that position will be flattened, default 2 will check each subsequent after entry day to remain above the entry day close.
		[Input] public int FlattenIfNotProfitable { get; set; }     // Flatten position on this trade day if not profitable, default 2 meaning will flatten position two days after entry if not profitable.

		//attribute
		private double m_stopLossPrice;
		private double m_projectedStopLossPrice;
		private bool m_longPosition;
		protected IInstrument m_tradingTimeframe;
		protected IInstrument m_dailyTimeframe;
		protected VariableSeries<double> m_averageRange;
		protected IOrderMarket m_entry;
		private IOrderPriced m_stopLossExit;
		private IOrderPriced m_firstExit;
		private IOrderPriced m_secondExit;
		private IOrderPriced m_exitMax;
		private IOrderMarket m_exitAll;

		//interface methods
		public _BreakoutBase(object _ctx, bool longPosition) : base(_ctx) { m_longPosition = longPosition; }

		protected override void Create()
		{
			base.Create();
			m_averageRange = new VariableSeries<double>(this);

			m_stopLossPrice = 0.0;
			LookBack = DEFAULT_LOOKBACK;
			ExpansionFactor = DEFAULT_EXPANSION_FACTOR;
			AbsoluteSize = DEFAULT_ABSOLUTE_SIZE;
			MaxEquityPercent = DEFAULT_MAX_EQUITY_PERCENT;
			MaxVolumePercent = DEFAULT_MAX_VOLUME_PERCENT;
			MaxRiskBasedPercent = DEFAULT_MAX_RISK_BASED_PERCENT;
			StopLossPlacement = DEFAULT_STOP_LOSS_PLACEMENT;
			StopLossBuffer = DEFAULT_STOP_LOSS_BUFFER;

			FirstProfitExit = DEFAULT_FIRST_PROFIT_EXIT;
			FirstProfitPosition = DEFAULT_FIRST_PROFIT_POSITION;
			SecondProfitExit = DEFAULT_SECOND_PROFIT_EXIT;
			SecondProfitPosition = DEFAULT_SECOND_PROFIT_POSITION;
			MaxProfitPercent = DEFAULT_MAX_PROFIT_PERCENT;
			BreakEvenProfit = DEFAULT_BREAK_EVEN_PROFIT;
			EntryDayExtremeCheck = DEFAULT_ENTRY_DAY_EXTREME_CHECK;
			FlattenIfNotProfitable = DEFAULT_FLATTEN_IF_NOT_PROFITABLE;
			UseRoundLots = DEFAULT_USE_ROUND_LOTS;

			if (m_longPosition)
			{
				m_entry = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LEBo", EOrderAction.Buy));
				m_stopLossExit = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LXSL", EOrderAction.Sell, OrderExit.FromAll));
				m_firstExit = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXBo1", EOrderAction.Sell, OrderExit.FromAll));
				m_secondExit = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LXBo2", EOrderAction.Sell, OrderExit.FromAll));
				m_exitMax = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LXBoMax", EOrderAction.Sell, OrderExit.FromAll));
				m_exitAll = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LXBoAll", EOrderAction.Sell, OrderExit.FromAll));
			}
			else
			{
				m_entry = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SEBo", EOrderAction.SellShort));
				m_stopLossExit = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SXSL", EOrderAction.BuyToCover, OrderExit.FromAll));
				m_firstExit = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXBo1", EOrderAction.BuyToCover, OrderExit.FromAll));
				m_secondExit = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "SXBo2", EOrderAction.BuyToCover, OrderExit.FromAll));
				m_exitMax = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "SXBoMax", EOrderAction.BuyToCover, OrderExit.FromAll));
				m_exitAll = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SXBoAll", EOrderAction.BuyToCover, OrderExit.FromAll));
			}
		}

		protected override void StartCalc()
		{
			base.StartCalc();

			//validate inputs
			if (LookBack <= 0) throw new ArgumentException("LookBack should be larger than zero.");
			if (ExpansionFactor <= 0) throw new ArgumentException("ExpansionFactor should be larger than zero.");
			if (AbsoluteSize < 0) throw new ArgumentException("AbsoluteSize should be larger or equal to zero.");
			if (MaxEquityPercent <= 0 || MaxEquityPercent > 1.0) throw new ArgumentException("MaxEquityPercent must be larger than 0.0 and less equal to 1.0.");
			if (MaxVolumePercent <= 0 || MaxVolumePercent > 1.0) throw new ArgumentException("MaxVolumePercent must be larger than 0.0 and less equal to 1.0.");
			if (MaxRiskBasedPercent <= 0 || MaxRiskBasedPercent > 1.0) throw new ArgumentException("MaxRiskBasedPercent must be larger than 0.0 and less equal to 1.0.");
			if (StopLossBuffer < 0) throw new ArgumentException("StopLossBuffer must be larger or equal to zero.");

			if (FirstProfitExit < 0 || FirstProfitExit > 1.0) throw new ArgumentException("First profit exit must be between 0.0 and 1.0 (0.0 to disable).");
			if (FirstProfitPosition < 0 || FirstProfitPosition > 1.0) throw new ArgumentException("First profit position size must be between 0.0 and 1.0 (0.0 to disable).");
			if (SecondProfitExit < 0 || SecondProfitExit > 1.0) throw new ArgumentException("Second profit exit must be between 0.0 and 1.0 (0.0 to disable)");
			if (SecondProfitPosition < 0 || SecondProfitPosition > 1.0) throw new ArgumentException("Second profit position size must be between 0.0 and 1.0 (0.0 to disable).");
			if (MaxProfitPercent < 0 || MaxProfitPercent > 1.0) throw new ArgumentException("MaxProfitPercent exit must be between 0.0 and 1.0 (0.0 to disable).");
			if (BreakEvenProfit < 0) throw new ArgumentException("BreakEvenProfit must be larger equal to zero (zero to disable).");

			if (EntryDayExtremeCheck < 0) throw new ArgumentException("Entry day extreme check must be greater or equal to zero.");
			if (FlattenIfNotProfitable < 0) throw new ArgumentException("Flatten if not profitable must be greater or equal to zero.");

			if (MaxProfitPercent == 0 && (FirstProfitPosition + SecondProfitPosition) < 1.0) throw new ArgumentException("If max profit percent is not set first and second exit positions must be equal to 1.0 to exit full position.");

			//initialize rest of the instance
			m_projectedStopLossPrice = 0.0;
			m_tradingTimeframe = Bars;
			m_dailyTimeframe = BarsOfData(2);
		}

		protected override void CalcBar()
		{
			base.CalcBar();

			//update general indicators
			updatePercentChange();

			if (this.PositionSide == 0)
			{
				//reset the stop-loss price
				m_stopLossPrice = 0.0;

				//update the projected stop-loss while no position
				updateProjectedStopLoss();

				//send entry
				sendEntry();
			}
			else
			{
				//setup stop-loss price
				if (m_stopLossPrice == 0.0) m_stopLossPrice = m_projectedStopLossPrice;

				//manage open trades
				sendStopLoss();
				sendBreakEvenStop();
				processExit(FirstProfitExit, FirstProfitPosition, m_firstExit);
				processExit(SecondProfitExit, SecondProfitPosition, m_secondExit);
				processExit(MaxProfitPercent, 1.0, m_exitMax);
				processEntryDayExtremeExit();
				flattenIfNotProfitable();
			}
		}

		//methods
		/// <summary>
		/// Subclasses need to implement this to get the entry logic going for long/short positions.
		/// </summary>
		protected abstract void sendEntry();

		void updatePercentChange()
		{
			if (m_dailyTimeframe.CurrentBar <= LookBack)
			{
				m_averageRange.Value = double.MaxValue; // neutral / no-entry
				return;
			}

			double sum = 0.0;
			for (int i = 0; i < LookBack; i++)
			{
				double rangePct = (m_dailyTimeframe.Open[i] > 0.0) ? Math.Abs(m_dailyTimeframe.Open[i] - m_dailyTimeframe.Close[i]) / m_dailyTimeframe.Open[i] : 0.0;
				sum += rangePct;
			}

			m_averageRange.Value = (sum / LookBack) * ExpansionFactor;
		}

		void updateProjectedStopLoss()
		{
			if (m_longPosition)
			{
				//determine stop-loss price with buffer
				m_projectedStopLossPrice = IntradayLow;

				switch (StopLossPlacement)
				{
					case EStopLossPlacement.EntryDayMid:
						m_projectedStopLossPrice = (IntradayOpen + IntradayClose) / 2;
						break;
					case EStopLossPlacement.PreEntryDay:
						m_projectedStopLossPrice = m_dailyTimeframe.Low[0];
						break;
					case EStopLossPlacement.XDay:
						m_projectedStopLossPrice = m_dailyTimeframe.Low.Lowest(LookBack);
						break;
				}

				m_projectedStopLossPrice -= StopLossBuffer;
			}
			else
			{
				//determine stop-loss price with buffer
				m_projectedStopLossPrice = IntradayHigh;

				switch (StopLossPlacement)
				{
					case EStopLossPlacement.EntryDayMid:
						m_projectedStopLossPrice = (IntradayOpen + IntradayClose) / 2;
						break;
					case EStopLossPlacement.PreEntryDay:
						m_projectedStopLossPrice = m_dailyTimeframe.High[0];
						break;
					case EStopLossPlacement.XDay:
						m_projectedStopLossPrice = m_dailyTimeframe.High.Highest(LookBack);
						break;
				}

				m_projectedStopLossPrice += StopLossBuffer;
			}
		}

		protected int positionSize()
		{
			if (AbsoluteSize != 0) return AbsoluteSize;

			int size = 0;
			double risk = Math.Abs(IntradayClose - m_projectedStopLossPrice);
			int equitySize = m_tradingTimeframe.CloseValue != 0 ? (int)Math.Truncate((equityAvailable() * MaxEquityPercent) / m_tradingTimeframe.CloseValue) : 0;
			int riskSize = riskBasedPositionSize(risk, MaxRiskBasedPercent, UseRoundLots);
			int volumeSize = volumeBasedPositionSize(m_dailyTimeframe, DEFAULT_VOLUME_LENGTH, MaxVolumePercent, UseRoundLots);
			size = (int)Math.Min(equitySize ,Math.Min(riskSize, volumeSize));
			trace(ETraceLevel.Verbose, $"Size {size} from risk {risk} - EquitySize({equitySize}) RiskSize({riskSize}) VolumeSize({volumeSize}).");

			return size;
		}

		private void sendStopLoss()
		{
			if (PositionSide != 0)
			{
				if (m_stopLossPrice != 0)
					m_stopLossExit.Send(m_stopLossPrice);
				else
					trace(ETraceLevel.Warning, "Position is being held without a stop-loss price.");
			}
		}

		private void sendBreakEvenStop()
		{
			if (BreakEvenProfit > 0.0)
			{
				this.CurSpecOrdersMode = ESpecOrdersMode.PerContract;
				double profit = StrategyInfo.AvgEntryPrice * BreakEvenProfit;
				trace(ETraceLevel.Info, $"Sending breakeven stop at {profit} per position.");
				GenerateBreakEven(profit);
			}
		}

		private void processExit(double profitPercent, double positionSize, IOrderPriced order)
		{
			if (profitPercent == 0.0 || positionSize == 0.0) return;

			IMarketPosition position = Positions[0];
			if (position.Value == 0) return;

			double entryPrice = StrategyInfo.AvgEntryPrice;
			if (entryPrice <= 0.0) return;

			//determine target price for long/short position and whether it was achieved
			double targetPrice = (position.Value > 0) ? entryPrice * (1 + profitPercent) : entryPrice * (1 - profitPercent);
			bool targetAchieved = (position.Value > 0 && m_tradingTimeframe.CloseValue >= targetPrice) ||
														(position.Value < 0 && m_tradingTimeframe.CloseValue <= targetPrice);
			trace(ETraceLevel.Verbose, $" Profit target {targetPrice} for order {order.ID} achieved at price {IntradayClose}? {targetAchieved}");

			if (targetAchieved)
			{
				//compute the total position size that was held by trade
				double openSize = 0.0;
				foreach (ITrade t in position.OpenTrades)
				{
					if (t.EntryOrder != null)
						openSize += Math.Abs(t.EntryOrder.Contracts);
				}

				double closedSize = 0.0;
				foreach (ITrade t in position.ClosedTrades)
				{
					if (t.EntryOrder != null)
						closedSize += Math.Abs(t.EntryOrder.Contracts);
				}

				double originalSize = openSize + closedSize;

				//safety fallback (in case the collections are empty on first bar)
				if (originalSize == 0.0)
					originalSize = Math.Abs(position.OpenLots);

				//positionSize is a PERCENTAGE of the ORIGINAL size
				int qtyToExit = (int)Math.Round(originalSize * positionSize);

				//never exit more than we currently have open
				int currentOpen = (int)Math.Abs(position.OpenLots);
				qtyToExit = Math.Max(0, Math.Min(qtyToExit, currentOpen));
				trace(ETraceLevel.Verbose, $"Original size {originalSize} (open {openSize} + closed {closedSize}) with current open {currentOpen} = quantity to exit {qtyToExit}.");

				if (qtyToExit > 0)
				{
					trace(ETraceLevel.Info, $"Profit target {profitPercent}% achieved → exit order {order.ID} sent at {targetPrice} for {qtyToExit} contracts.");
					order.Send(targetPrice, qtyToExit);
				}
			}
		}

		private void processEntryDayExtremeExit()
		{
			IMarketPosition position = Positions[0];
			if (position.Value == 0 || EntryDayExtremeCheck <= 0) return;

			DateTime entryDate = this.EntryTime(0);
			int daysCount = tradingDayCount(entryDate, m_tradingTimeframe.Time[0]);
			trace(ETraceLevel.Verbose, $"processEntryDayExtremeExit Entry date({entryDate.ToString("yyyy-MM-dd")}) DaysCount({daysCount})");

			if (daysCount >= EntryDayExtremeCheck)
			{
				double entryDayExtreme = position.Value > 0 ? m_dailyTimeframe.High[daysCount] : m_dailyTimeframe.Low[daysCount];
				bool exitPosition = position.Value > 0 ? m_tradingTimeframe.CloseValue <= entryDayExtreme : m_tradingTimeframe.CloseValue >= entryDayExtreme;
				trace(ETraceLevel.Verbose, $"processEntryDayExtremeExit EntryDayExtreme({entryDayExtreme}) CloseValue({m_tradingTimeframe.CloseValue}) ExitPosition({exitPosition})");

				if (exitPosition)
				{
					trace(ETraceLevel.Info, $"processEntryDayExtremeExit closing position with size {position.Value}");
					m_exitAll.Send();
				}
			}
		}

		private void flattenIfNotProfitable()
		{
			IMarketPosition position = Positions[0];
			if (position.Value == 0 || FlattenIfNotProfitable <= 0) return;

			DateTime entryDate = this.EntryTime(0);
			int daysCount = tradingDayCount(entryDate, m_tradingTimeframe.Time[0]);
			trace(ETraceLevel.Verbose, $"flattenIfNotProfitable Entry date({entryDate.ToString("yyyy-MM-dd")}) DaysCount({daysCount})");

			if (daysCount >= FlattenIfNotProfitable && position.Profit < 0.0)
			{
				trace(ETraceLevel.Info, $"flattenIfNotProfitable closing position with size {position.Value} and P/L {position.Profit}");
				m_exitAll.Send();
			}
		}
	}
}