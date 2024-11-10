using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Buys the pullback on the strongest stocks (reversal trade) in the market when the market is overextended to the down side. Both the stocks and the
	/// market in general should be bullish.
	/// https://www.youtube.com/watch?v=WY_5BV3BmkA
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	///		3. Market data feed - typically daily data feed for the market (same resolution as 2).
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///   * Market must be bulish.
	///		* Market is currently down x-percent from the high in the past y-days and the stock is also down at least x-percent from the high in the past y-days.
	///		* Optionally - wait for an x-percent gap down on the open - 0 switch it off.
	///		* Waits for the opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _CapitulationReversal : _OpeningRangeBarSignal
	{
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }                //number of days to look back for the growth
		[Input] int ScanLength { get; set; }                //lookback length for the market high
		[Input] int MarketBullishLength { get; set; }       //length used to determine if the market is bullish
		[Input] double DownPercent { get; set; }						//market down percent from the high in the past y-days
		[Input] double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down

		//properties


		//attributes
		private IInstrument m_scannerTimeFrame;   //Capitulation reversal scanning timeframe, typically daily 
		private IInstrument m_marketTimeFrame;    //market timeframe, typically daily 

		//interface methods
		public _CapitulationReversal(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = 30;
			GrowthDays = 120;
			MarketBullishLength = 200;
			ScanLength = 20;
			DownPercent = 10.0d;
		}

		protected override void StartCalc()
		{
			if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
			if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
			if (ScanLength <= 0) throw new Exception("Scan length must be greater than zero.");
			if (DownPercent <= 0.0d || DownPercent >= 100.0d) throw new Exception("Down percent must be greater than zero but less than hundred percent.");
      //Bars is used as the entry time frame criteria
        	m_scannerTimeFrame = BarsOfData(2);
			m_marketTimeFrame = BarsOfData(3);
			MinimumGrowthPercent /= 100.0d;
			MinimumGrowthPercent += 1.0d;
			DownPercent = 1.0d - DownPercent / 100.0d;
			GapDownPercent /= 100.0d;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//scanner criteria
					if (m_scannerTimeFrame.CurrentBar <= GrowthDays) return; //not enough data
					if (m_scannerTimeFrame.Close[0] / m_scannerTimeFrame.Close[GrowthDays] < MinimumGrowthPercent) return;  //not enough growth
					if (m_marketTimeFrame.Close[0] < m_marketTimeFrame.Close.Average(200)) return; //market is not bullish
					if (m_marketTimeFrame.Close[0] > m_marketTimeFrame.High.Highest(ScanLength) * DownPercent) return; //market is not down by required percentage
					if (m_scannerTimeFrame.Close[0] > m_scannerTimeFrame.High.Highest(ScanLength) * DownPercent) return; //stock is not down by required percentage
					if (GapDownPercent > 0.0d && (m_scannerTimeFrame.Open[0] - m_scannerTimeFrame.Close[1]) / m_scannerTimeFrame.Close[1] > GapDownPercent) return; //gap down on the open

					//entry criteria
					base.CalcBar();
					break;
			}
		}
	}
}