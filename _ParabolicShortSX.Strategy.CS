﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Exit for the parabolic short strategy - it waits for a specific retracement of the original parabolic move, keeps a stop-loss at each lower high that forms
	/// and exits the position as soon as a higher low forms indicating signs of a reversal.
	/// Timeframes used:
	///		Primary: 1/5 minute chart for the entry
	///		Secondary: 30-minutes/hourly
	///		Dialy timeframe
	/// </summary>
	public class _ParabolicShortSX : _CustomSignal
	{
		//constants
		public int DEFAULT_FRACTAL_LAYERS = 3; //number of fractal layers to use for the price action analysis
		public const double DEFAULT_RETRACEMENT_EXIT_PERCENT = 0.5; //exit percentage - this is used to determine the exit percentage based on the entry date
		public const int DEFAULT_EXIT_DAYS_COUNT = 3; //number of days to exit the full position
		public const string DEFAULT_EXIT_TIME = "15:35"; //exit time for the full position

		//types
		public enum EExitDaysCountStrategy
		{
			CalendarDays,
			TradingDays
		};

		//properties
		[Input] public double RetracementExitPercent { get; set; } //exit percentage - this is used to determine the exit percentage based on the entry date
		[Input] public EExitDaysCountStrategy ExitDaysCountStrategy { get; set; } //exit days count strategy - this is used to determine the exit days count based on the entry date
		[Input] public int ExitDaysCount { get; set; } //full exit days count - this is used to determine the full exit days count based on the entry date
		[Input] public bool ExitFriday { get; set; } //exit on Fridays - this will exit the position on Fridays if required
		[Input] public string ExitTime { get; set; } //exit time - this will exit the position at the specified time
		[Input] public bool Debug { get; set; }      //debug mode - this will print debug information to the output window

		//attribute
		private PriceActionAnalysis m_priceAction;
		private IOrderMarket m_sxPartial;
		private IOrderMarket m_sxAll;
		private IOrderPriced m_sxStopLoss;
		private IInstrument m_primaryTimeFrame;
		private IInstrument m_secondaryTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private DateTime m_exitTime;
		private DateTime m_entryDate;
		private double m_stopLoss;
		private bool m_retracementExitFilled = false;

		//interface methods
		public _ParabolicShortSX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RetracementExitPercent = DEFAULT_RETRACEMENT_EXIT_PERCENT;
			ExitDaysCountStrategy = EExitDaysCountStrategy.CalendarDays;
			ExitDaysCount = DEFAULT_EXIT_DAYS_COUNT;
			ExitTime = DEFAULT_EXIT_TIME;
			m_exitTime = DateTime.ParseExact(DEFAULT_EXIT_TIME, "HH:mm", null);
			ExitFriday = true;
			Debug = false;
			m_sxPartial = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LxP", EOrderAction.Sell));
			m_sxAll = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LxA", EOrderAction.Sell));
			m_sxStopLoss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxSL", EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_priceAction = new PriceActionAnalysis(DEFAULT_FRACTAL_LAYERS);
			if (RetracementExitPercent <= 0 || RetracementExitPercent > 1) throw new Exception("RetracementExitPercent must be larger than zero and less/equal to one.");
			if (ExitDaysCount <= 0) throw new Exception("ExitDaysCount must be larger than zero.");
			m_exitTime = DateTime.ParseExact(ExitTime, "HH:mm", null);
			m_primaryTimeFrame = BarsOfData(1);
			m_secondaryTimeFrame = BarsOfData(2);
			m_dailyTimeFrame = BarsOfData(3);
			m_entryDate = DateTime.MinValue;
			m_stopLoss = -1;
		}

		protected override void CalcBar()
		{
			base.CalcBar();

			if (Environment.CalcReason == CalculationReason.OrderFilled)
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Retracement Order filled({CurrentPosition.OpenTrades.Count})");
				m_retracementExitFilled = true;
			}

			bool priceActionChanged = false;
			if (Environment.CalcReason == CalculationReason.Default && m_secondaryTimeFrame.Status == EBarState.Close)
				priceActionChanged = m_priceAction.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);
			if (PositionSide == 0)
			{
				m_retracementExitFilled = false;
				m_entryDate = DateTime.MinValue;
				m_stopLoss = -1;
				return;
			}

			//exit on Fridays if required
			if (ExitFriday && m_primaryTimeFrame.Time[0].DayOfWeek == DayOfWeek.Friday && m_primaryTimeFrame.Time[0].TimeOfDay >= m_exitTime.TimeOfDay)
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Exit Friday({m_exitTime.ToString("HH:mm")})");
				m_sxAll.Send();
				return;
			}

			//determine the entry date of the trade
			if (m_entryDate == DateTime.MinValue)
			{
				if (CurrentPosition.OpenTrades.Count > 0) m_entryDate = CurrentPosition.OpenTrades.Min(trade => trade.EntryOrder.Time);
				if (CurrentPosition.ClosedTrades.Count > 0) m_entryDate = CurrentPosition.ClosedTrades.Min(trade => trade.EntryOrder.Time);
			}
			int daysCount = ExitDaysCountStrategy == EExitDaysCountStrategy.CalendarDays ? calendarDayCount(m_entryDate, Bars.Time[0]) : tradingDayCount(m_entryDate, Bars.Time[0]);
			if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Entry date({m_entryDate.ToString("yyyy-MM-dd")}), DaysCount({daysCount})");

			//exit after a specific number of days
			if (m_primaryTimeFrame.Time[0].TimeOfDay >= m_exitTime.TimeOfDay && daysCount >= ExitDaysCount)
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Exit time({m_exitTime.ToString("HH:mm")})");
				m_sxAll.Send();
				return;
			}

			//maintain stop-loss based on the entry day and the formation of new lower highs
			double entryDayHigh = daysCount == 0 ? IntradayHigh : m_dailyTimeFrame.High[daysCount - 1];
			if (m_stopLoss == -1) m_stopLoss = entryDayHigh;
			if (priceActionChanged)
				foreach (var bar in m_priceAction.ChangedBars)
				{
					if (bar.Value.state.lowerHigh)
					{
						m_stopLoss = Math.Min(m_stopLoss, bar.Value.high);
						break;
					}
				}

			m_sxStopLoss.Send(m_stopLoss);

			//exit partial position if we reach the specific retracement of the spike
			if (!m_retracementExitFilled && entryDayHigh * RetracementExitPercent > m_primaryTimeFrame.Close[0])
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Partial exit({m_primaryTimeFrame.Close[0]}), EntryDayHigh({entryDayHigh}), RetracementExitPercent({RetracementExitPercent})");
				m_sxPartial.Send((int)Math.Ceiling(Math.Abs(StrategyInfo.MarketPosition) / (double)2));
				return;
			}

			//exit full position if the price action starts making higher lows
			if (priceActionChanged && m_priceAction.AnalysisState.higherLow)
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, Exit higher low({m_primaryTimeFrame.Close[0]})");
				m_sxAll.Send();
				return;
			}

			//TBD: Check if you add a parabolic SAR to trace the exit level. Think the tracking using the lower high and exit on specific days should be sufficient.

		}

		//methods


	}
}