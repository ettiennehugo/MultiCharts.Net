using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Based off the narrow range day breakout strategy by Toby Crable - https://www.youtube.com/watch?v=Hym5yqy1QGY
	/// A X-bar narrow range is defined as the narrowest X-day range relative to any X-day range within the previous Y-market days, typical values for X is
	/// 2, 4 and 7 and the default for Y is 20.
	/// Requires two data feeds, the entry data feed based off the minute timeframe since it uses an opening bar range to determine the risk and a secondary data
	/// feed on the daily timeframe used to determine the narrow range days.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Current X-bar lookback we have a range tightening and it is the smallest range in the last narraw range period.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _CrableNRX : _OpeningRangeBarSignal 
	{
		//constants


		//inputs
		//_OpeningRangeBarSignal inputs
		[Input] public int VolumeLength { get; set; }           //volume length to use for opening bar volume detection
		[Input] public double EntryVolumePercent { get; set; }  //volume standard deviation factor to use for high volume detection
		[Input] public double MaxVolumeBasedPosPercent { get; set; }  //maximum volume based position size as a percentage of equity

		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }								 //number of days to look back for the growth
		[Input] public int NarrowRangeDays { get; set; }     //X-days is the narrowest X-day range relative to any X-day range within the previous narrow range period-market days
		[Input] public int NarrowRangePeriod { get; set; }   //period to scan for narrow range days
		[Input] public double MinimumATR { get; set;}        //minimum required price movement to avoid stocks that are collapsing in price
		[Input] double AtrEpsilon { get; set; }              //percentage of the ATR to use for the range close factor in Darvas box computations

		//properties


		//attributes
		private PowerLanguage.Function._CrableNRX m_crableNRX;

		//interface methods
		IInstrument m_scannerTimeFrame;
		public _CrableNRX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			MinimumGrowthPercent = 30;
			GrowthDays = 120;
			NarrowRangeDays = 7;
			NarrowRangePeriod = 20;
			MinimumATR = 0.2;
			m_crableNRX = new PowerLanguage.Function._CrableNRX(this);
		}

		protected override void StartCalc()
		{
			_VolumeLength = VolumeLength;
			_EntryVolumePercent = EntryVolumePercent;
			_MaxVolumeBasedPosPercent = MaxVolumeBasedPosPercent;
			_EntryDataFeed = 1;
			_VolumeDataFeed = 2;
			base.StartCalc();
			if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
			if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
			//Bars is used as the entry time frame criteria
			m_scannerTimeFrame = BarsOfData(2);
			m_crableNRX.Instrument = m_scannerTimeFrame;
			m_crableNRX.ScanLength = NarrowRangePeriod;
			m_crableNRX.Length = NarrowRangeDays;
			m_crableNRX.MinimumATR = MinimumATR;
			MinimumGrowthPercent /= 100.0d;
			MinimumGrowthPercent += 1.0d;
		}

		protected override void CalcBar()
		{			
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//scanner criteria
					if (m_scannerTimeFrame.CurrentBar <= GrowthDays) return; //not enough data
					if (m_scannerTimeFrame.Close[0] / m_scannerTimeFrame.Close[GrowthDays] < MinimumGrowthPercent) return;  //not enough growth
					if (!m_crableNRX[0]) return; //no Crable narrow range bars detection on daily timeframe

					//entry criteria
					base.CalcBar();
					break;
			}
		}

		//methods



	}
}