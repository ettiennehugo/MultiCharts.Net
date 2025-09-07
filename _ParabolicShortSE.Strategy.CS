using System;
using System.Linq;
using PriceAction;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Parabolic Short SE - this strategy is used to enter a short position when a lower high is formed after a parabolic move up.
	/// The strategy uses the price action analysis to determine the entry point and the exit point.
	/// Timeframes used:
	///		Primary: 1/5 minute chart for the entry
	///		Secondary: 15-minute - used for price action analysis (longer timeframes would cause too make late entries)
	///		Dialy timeframe
	/// </summary>
	public class _ParabolicShortSE : _CustomSignal
	{
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public int DEFAULT_FRACTAL_LAYERS = 3; //number of fractal layers to use for the price action analysis
		public const double DEFAULT_FACTOR_CLOSE = 0.05; //factor close - user a more finely tuned factor close to determine lower highs, this uses 5% of the ATR as parabolic shorts can have huge bar moves
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.15; //per default don't enter positions larger than 15% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.0075; //use 0.75% of equity for risk based entry
		public const double DEFAULT_CLOSE_PERCENTILE = 0.5; //required close percentile - this is used to determine the entry size based on the average trading volume 
		public const double DEFAULT_VOLUME_PERCENTILE = 0.5; //required volume percentile - this is used to determine the entry size based on the average trading volume
		public const int DEFAULT_VOLUME_LENGTH = 20; //length of the volume period to use for the volume based entry size
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //maximum volume based percentage - this is used to determine the entry size based on the average trading volume

		//types


		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume
		[Input] public double ClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public double VolumePercentile { get; set; } //required volume percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool AddToPosition { get; set; } //add to position - this will add to the position if the price action analysis returns a lower high
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume

		//attribute
		private PriceActionAnalysis m_priceAction;
		private PowerLanguage.Function._VolumePercentile m_volumePercentile;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private IOrderPriced m_se;
		private IInstrument m_primaryTimeFrame;
		private IInstrument m_secondaryTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private bool m_lowerHighDetected;

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
			ClosePercentile = DEFAULT_CLOSE_PERCENTILE;
			VolumePercentile = DEFAULT_VOLUME_PERCENTILE;
			AddToPosition = false;
			UseRoundLots = false;
			m_volumePercentile = new PowerLanguage.Function._VolumePercentile(this);
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Se", EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (ClosePercentile < 0 || ClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			m_priceAction = new PriceActionAnalysis(DEFAULT_FRACTAL_LAYERS);
			m_priceAction.FactorClose = DEFAULT_FACTOR_CLOSE;
			m_primaryTimeFrame = BarsOfData(1);
			m_secondaryTimeFrame = BarsOfData(2);
			m_dailyTimeFrame = BarsOfData(3);
			m_volumePercentile.Instrument = m_primaryTimeFrame;
			m_lowerHighDetected = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();

			//always perform price action analsis on the secondary timeframe close
			if (m_secondaryTimeFrame.Status == EBarState.Close)
			{
				m_priceAction.Analyse(m_secondaryTimeFrame.Time[0], m_secondaryTimeFrame.Open[0], m_secondaryTimeFrame.High[0], m_secondaryTimeFrame.Low[0], m_secondaryTimeFrame.Close[0], m_secondaryTimeFrame.Volume[0]);
				m_lowerHighDetected = m_priceAction.AnalysisState.lowerHigh;
			}

			if (m_primaryTimeFrame.Status != EBarState.Close || m_primaryTimeFrame.CurrentBar < 2) return;
			if (PositionSide != 0 && !AddToPosition) return; //do not enter if there is already a position
			if (m_primaryTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_primaryTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay) return; //exit if time filter not met

			//perform price action analysis on secondary timeframe close 
			if (!m_lowerHighDetected) return; //only enter when lower high is formed on secondary timeframe

			//find the lower high bar - we need to reverse iterate to find the most recent lower high for analysis
			PriceActionBar lowerHighBar = null;
			foreach (var bar in m_priceAction.ChangedBars.Reverse())
				if (bar.Value.state.lowerHigh)
				{
					lowerHighBar = bar.Value;
					break;
				}

			if (lowerHighBar == null)
			{
				trace(ETraceLevel.Error, $"PriceAction analysis returned that a lower high was found but could find the lower high bar");
				return; //do not enter if a lower high did not form yet
			}

			//exit if bar does not have significant volume
			if (VolumePercentile > 0 && m_volumePercentile[0] < VolumePercentile)
			{
				trace(ETraceLevel.Verbose, $"Volume percentile {m_volumePercentile[0]} is below the required volume percentile {VolumePercentile} not entering.");
				return;
			}

			//compare the current entry timeframe bar against the secondary timeframe used for price action analysis
			if (m_primaryTimeFrame.High[0] > lowerHighBar.high) 
			{
				trace(ETraceLevel.Verbose, $"Entry timeframe high {m_primaryTimeFrame.High[0]} breaking above lower high {lowerHighBar.high} at {m_primaryTimeFrame.Time[0].ToString("yyyyMMdd-HHmmss.fff")} skipping short entry");
				return;
			}

			//determine the close percentile of the entry bar
			double range = m_primaryTimeFrame.High[0] - m_primaryTimeFrame.Low[0];
			if (range == 0) return; //avoid division by zero
			double closeHighDelta = m_primaryTimeFrame.High[0] - m_primaryTimeFrame.Close[0];	//we need bearish bars to close "far" from the high to get a solid closing percentile
			double closePercentile = closeHighDelta / range;
			if (closePercentile < ClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//determine the risk based on the entry bar close and the day high
			double risk = IntradayHigh - m_primaryTimeFrame.Close[0];

			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_primaryTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int volumeBasedSize = volumeBasedPositionSize(m_dailyTimeFrame, DEFAULT_VOLUME_LENGTH, MaximumVolumeBasedPercent, UseRoundLots);
			int size = equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize;
			size = size < volumeBasedSize ? size : volumeBasedSize;			
			if (size == 0) return; //do not enter if the size is zero

			trace(ETraceLevel.Info, $"Short entry at {m_primaryTimeFrame.Close[0]} Size {size} Risk {risk} IntradayHigh {IntradayHigh} EntryBarClose {m_primaryTimeFrame.Close[0]}");
			m_se.Send(m_primaryTimeFrame.Close[0], size);
		}

		//methods


	}
}
