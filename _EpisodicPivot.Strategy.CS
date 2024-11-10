using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Episodic pivot (gap up on earnings) strategy, it typically looks for stocks that have gapped up on earnings (or anything) and enters on a good momentum opening range bar.
	/// https://www.youtube.com/watch?v=YyNOhw2OLuI
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Stock must have a gap up day on the open after earnings.
	///		* Optionally - stock must be breaking out of a base and must not be overly extended.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _EpisodicPivot : _OpeningRangeBarSignal {
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }								 //number of days to look back for the growth
		[Input] double MinimumGapUpPercent { get; set; }     //minimum required gap up on the open
		[Input] double MaximumGapUpPercent { get; set; }     //maximum required gap up on the open - this is to avoid garbage stocks that are potentially up hundreds of percent
		[Input] double OverExtensionAtrFactor { get; set; }  //factor used to check whether the stock is overextended

		//properties


		//attributes
		private IInstrument m_scannerTimeFrame;


		//interface methods
		public _EpisodicPivot(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = 30;
			GrowthDays = 120;
			MinimumGapUpPercent = 10;
			MaximumGapUpPercent = 30;
			OverExtensionAtrFactor = 3.0d;    //must be within 3 ATR's of the moving average
		}

		protected override void StartCalc()
		{
			if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
			if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
			if (MinimumGapUpPercent <= 0) throw new Exception("Minimum gap up percent must be greater than zero.");
			if (MaximumGapUpPercent <= 0) throw new Exception("Maximum gap up percent must be greater than zero.");
			if (MinimumGapUpPercent >= MaximumGapUpPercent) throw new Exception("Minimum gap up percent must be less than the maximum gap up percent.");
			m_scannerTimeFrame = BarsOfData(2);
			MinimumGrowthPercent /= 100.0d;
			MinimumGrowthPercent += 1.0d;
			MaximumGapUpPercent /= 100.0d;
			MaximumGapUpPercent += 1.0d;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//scanner criteria
					if (m_scannerTimeFrame.CurrentBar <= GrowthDays) return; //not enough data
					if (m_scannerTimeFrame.Close[0] / m_scannerTimeFrame.Close[GrowthDays] > MinimumGrowthPercent) return;  //not enough growth
					double gapUpPercent = (m_scannerTimeFrame.Open[0] - m_scannerTimeFrame.Close[1]) / m_scannerTimeFrame.Close[1];
					if (gapUpPercent > MinimumGapUpPercent && gapUpPercent < MaximumGapUpPercent) return; //no minimum gap up on the open
					if (!isWithinAverageRange(m_scannerTimeFrame, 10, 20, OverExtensionAtrFactor)) return; //stock is overextended

					//entry criteria
					base.CalcBar();
					break;
			}
		}
	}
}