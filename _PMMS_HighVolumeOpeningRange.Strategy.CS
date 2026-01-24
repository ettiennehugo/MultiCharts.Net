using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.TradeManager;
using System.Windows.Forms;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Portfolio money management strategy that uses a high volume opening range breakout strategy around the given opening range period.
	/// The portfolio signal is dependent on the associated strategies to perform the scanning for potential entries and will then manage the trade
	/// entry and fill in the specific entry direction, entry price, risk and stop-loss price levels.
	/// Required Feeds:
	///		None - the strategy will load the bar data needed only for the specific instruments signalled by the scanning strategies.
	///	Entry:
	///		
	/// </summary>
	public class _PMMS_HighVolumeOpeningRange : _CustomSignal {
		//constants
		public const string PARAMETER_SCAN_FOR_ENTRY = "HVORScanForEntry";	//strategies associated with this portfolio signal should set this bool to true to enable entry scanning
		public const string PARAMETER_ENTRY_DIRECTION = "HVOREntryDirection";	//direction to scan for entry, long vs short
		public const string PARAMETER_ENTRY_PRICE = "HVOREntryPrice";		//entry price for a position
		public const string PARAMETER_RISK = "HVORRisk";								//risk for a position
		public const string PARAMETER_STOP_LOSS = "HVORStopLoss";      	//stop-loss for a position
		public const string PARAMETER_STRATEGY_DATA = "HVORStrategyData";   //data stored per strategy for the portfolio signal
		public const int DEFAULT_BAR_SIZE = 5;						//use 5 minutes as the default bar size
		public const bool DEFAULT_INCLUDE_PRE_POST_MARKET = false;
		public const int DEFAULT_VOLUME_LENGTH = 450;								//5-days of 5-minute bars excluding the pre- and post-market sessions
		public const double DEFAULT_VOLUME_STD_DEV_FACTOR = 2.0;    //enter at 2 standard deviations above the average volume over the volume length
		public const double DEFAULT_MAX_VOLUME_BASED_POS_PERCENT = 0.2; //enter at maximum of 20% of the average volume over the volume length

		//types
		/// <summary>
		/// Records an entry direction for the strategy.
		/// </summary>
		public struct StrategyEntry
		{


			//TODO
			// - complete this structure where scanner strategies can set the entry direction, risk, stop-loss and entry price.
			// - in the StartCalc create an entry in a list in the dictionary for each of the strategies in the portfolio so that the strategy can update their associated signals.


		}

		/// <summary>
		/// Per strategy data stored to monitor entry position.
		/// </summary>
		public struct StrategyData
		{
//TODO - Init
//			public StrategyData() 
//			{
//				Bars = new Dictionary<DateTime, Bar>();
//			}

			public double EntryPrice;
			public double Risk;
			public double StopLoss;
			public double VolumeAverage;
			public double VolumeAveragePlusStdDev;
			public Dictionary<DateTime, Bar> Bars;
		}

		//inputs
		[Input] public int BarSize { get; set; }                //bar sizes to use for volume calculations and the opening range
		[Input] public bool IncludePrePostMarket { get; set; }  //include pre and post-market data in the volume calculations
		[Input] public int VolumeLength { get; set; }           //volume length to use for opening bar volume detection
		[Input] public double VolumeStdDevFactor { get; set; }  //volume standard deviation factor to use for high volume detection
		[Input] public double MaxVolumeBasedPosPercent { get; set; }  //maximum volume based position size as a percentage of equity

		//properties


		//attributes
		private IOrderPriced m_le;
		private IOrderPriced m_se;

		//interface methods
		public _PMMS_HighVolumeOpeningRange(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			BarSize = DEFAULT_BAR_SIZE;
			IncludePrePostMarket = DEFAULT_INCLUDE_PRE_POST_MARKET;
			VolumeLength = DEFAULT_VOLUME_LENGTH;
			VolumeStdDevFactor = DEFAULT_VOLUME_STD_DEV_FACTOR;
			MaxVolumeBasedPosPercent = DEFAULT_MAX_VOLUME_BASED_POS_PERCENT;
			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
		}
		protected override void StartCalc()
		{			
			base.StartCalc();
			if (MaxVolumeBasedPosPercent <= 0 || MaxVolumeBasedPosPercent >= 1) throw new Exception("Max volume based position percentage must be between 0 and 1.");
			if (Environment.ApplicationCode != EApplicationCode.Portfolio) ExecControl.Abort("Signal can be applied in portfolio only.");
		}

		protected override void CalcBar()
		{
			this.StrategiesDenyEntriesAll();	//strategies are not allowed the enter positions, this signal will enter positions
			
		//○ In the portfolio signal load the minute data only when the scanner gives a signal and check that an opening range bar - see p.70 of the programming guide
		//○ The portfolio signal will perform the measurement logic only on the opening bars of signals and attach the data when it is loaded
		//○ The portfolio signal when it opens a position it will fill in the expected entry, risk and stop - loss price for the position
		//○ An associated exit variation of half exit / half roll will manage the position based on the entry / risk / stop - loss

			for (int i = 0; i < PortfolioStrategies.Count; i++)
				if ((bool)PortfolioStrategies[i].PortfolioData[PARAMETER_SCAN_FOR_ENTRY])
					loadStrategyData(PortfolioStrategies[i]);
				else
					unloadStategyData(PortfolioStrategies[i]);
		}

		//methods
		void loadStrategyData(IPortfolioStrategy strategy)
		{
			


		}

		void unloadStategyData(IPortfolioStrategy strategy)
		{
		


		}
	}
}