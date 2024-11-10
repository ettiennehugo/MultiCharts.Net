using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Darvas Box breakout strategy, it typically looks for x-number of bars that is consolidating within an upper and lower range and then breaks out of that range.
	/// The bar lows/highs must fall within a lower range and upper range based off an average upper and lower prices.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30% or more up in the past 6 months.
	///	  * Stock must have consolidated for at least x-bars within a Darvas box.
	///	  * Waits for a high volume opening range bar to form on the entry data feed.
	///	  * IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _DarvasBox : _OpeningRangeBarSignal {
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }          		//number of days to look back for the growth
		[Input] int MinimumLength { get; set; }			  	//minimum required length of the Darvas box
		[Input] double AtrEpsilon { get; set; }  			//percentage of the ATR to use for the range close factor in Darvas box computations
		
		//attributes
		private IInstrument m_scannerTimeFrame; 	//Darvas box scanning timeframe, typically daily 
		private PowerLanguage.Function._DarvasBox m_darvasBox;
		
		//interface methods
		public _DarvasBox(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = 30;
			GrowthDays = 120;
			MinimumLength = 10;
			AtrEpsilon = 0.05;
			m_darvasBox = new PowerLanguage.Function._DarvasBox(this);
		}

		protected override void StartCalc()
		{
			if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
			if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
			//Bars is used as the entry time frame criteria
			m_scannerTimeFrame = BarsOfData(2);
			m_darvasBox.Instrument = m_scannerTimeFrame;
			m_darvasBox.Length = MinimumLength;
			m_darvasBox.Epsilon = AtrEpsilon;
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
					if (!m_darvasBox[0]) return; //no Darvas Box detection on daily timeframe

					//entry criteria
					base.CalcBar();
					break;
			}
		}
	}
}