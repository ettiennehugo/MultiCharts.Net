using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Parabolic Short SE - this strategy is used to enter a short position when a lower high is formed after a parabolic move up.
	/// The strategy uses the price action analysis to determine the entry point and the exit point.
	/// </summary>
	public class _ParabolicShortSE : _CustomSignal
	{
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public const int DEFAULT_POST_LOWER_HIGH_LOOKBACK = 1; //lookback period for the high
		public int DEFAULT_FRACTAL_LAYERS = 3; //number of fractal layers to use for the price action analysis`
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.15; //per default don't enter positions larger than 15% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.0075; //use 0.75% of equity for risk based entry
		public const double DEFAULT_REQUIRED_CLOSE_PERCENTILE = 0.5; //required close percentile - this is used to determine the entry size based on the average trading volume 
		public const int DEFAULT_VOLUME_LENGTH = 20; //length of the volume for the entry size
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //do not exceed 20% of the average trading volume for the entry size

		//types


		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public int PostLowerHighLookback { get; set; }  //number of bars to look back after the lower high to determine the entry bar - this is used to determine the entry bar for the entry
		[Input] public int FractalLayers { get; set; } //number of fractal layers to use for the price action analysis
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume\
		[Input] public double RequiredClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool AddToPosition { get; set; } //add to position - this will add to the position if the price action analysis returns a lower high
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume
		[Input] public bool Debug { get; set; } //debug mode - this will print debug information to the output window

		//attribute
		private PriceActionAnalysis m_priceAction;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private IOrderPriced m_se;
		private IInstrument m_minuteTimeFrame;
		private IInstrument m_dailyTimeFrame;

		//interface methods
		public _ParabolicShortSE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			StartTime = DEFAULT_START_TIME;
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			EndTime = DEFAULT_END_TIME;
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			FractalLayers = DEFAULT_FRACTAL_LAYERS;
			RequiredClosePercentile = DEFAULT_REQUIRED_CLOSE_PERCENTILE;
			PostLowerHighLookback = DEFAULT_POST_LOWER_HIGH_LOOKBACK;
			AddToPosition = false;
			UseRoundLots = false;
			Debug = false;
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Se", EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			if (FractalLayers < 1) throw new Exception("FractalLayers must be larger than or equal to one.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (RequiredClosePercentile < 0 || RequiredClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			m_priceAction = new PriceActionAnalysis(FractalLayers);
			m_minuteTimeFrame = BarsOfData(1); //get the minute time frame for the stock
			m_dailyTimeFrame = BarsOfData(2); //get the daily time frame for the stock
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Environment.CalcReason != CalculationReason.Default || m_minuteTimeFrame.Status != EBarState.Close || m_minuteTimeFrame.CurrentBar < 2) return;
			bool priceActionChanged = m_priceAction.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);
			if (PositionSide != 0 && !AddToPosition) return; //do not enter if there is already a position
			if (m_minuteTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_minuteTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay) return; //exit if time filter not met
			if (!priceActionChanged || !m_priceAction.AnalysisState.lowerHigh) return; //do not enter if a lower high did not form yet

			//find the lower high bar and the last bar that would be the entry bar
			PriceActionBar entryBar = m_priceAction.Bars.Last().Value;
			PriceActionBar lowerHighBar = null;
			foreach (var bar in m_priceAction.ChangedBars)
			{
				if (bar.Value.state.lowerHigh)
				{
					lowerHighBar = bar.Value;
					break;
				}
			}

			if (lowerHighBar == null)
			{
				if (Debug) Output.WriteLine($"{GetType().Name}, {Bars.Info.Name}, {Bars.Time[0]}, PriceAction analysis returned that a lower high was found but could find the lower high bar");
				return; //do not enter if a lower high did not form yet
			}

			if (entryBar.index - lowerHighBar.index > PostLowerHighLookback) return; //do not enter when when the post lower high lookback period is not met

			//determine the close percentile of the entry bar
			double range = entryBar.high - entryBar.low;
			if (range == 0) return; //avoid division by zero
			double closeLowDelta = entryBar.close - entryBar.low;
			double closePercentile = closeLowDelta / range;
			if (closePercentile < RequiredClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//determine the risk based on the entry bar close and the day high
			double risk = IntradayHigh - entryBar.close;

			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_minuteTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int size = volumeBasedPositionSize(m_dailyTimeFrame, equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize, DEFAULT_VOLUME_LENGTH, MaximumVolumeBasedPercent, UseRoundLots);
			if (size == 0) return; //do not enter if the size is zero

			m_se.Send(entryBar.close, size);
		}

		//methods


	}
}
