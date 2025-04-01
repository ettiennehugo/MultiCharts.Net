using IronPython.Compiler.Ast;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Episodic pivot (gap up on earnings) strategy, it typically looks for stocks that have gapped up on earnings (or anything) and enters on a good momentum opening range bar.
		/// https://www.youtube.com/watch?v=YyNOhw2OLuI
		///	Required Feeds:
		///		1. Scanner data feed - typically daily data feed for the same instrument.
		/// Entry:
		///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
		///		* Stock must have a gap up day on the open after earnings.
		///		* Optionally - stock must be breaking out of a base and must not be overly extended.
		///		* Waits for a high volume opening range bar to form on the entry data feed.
		///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
		///	Nots:
		///		* This function needs to be used against specific sectors and market caps since what is considered an Episodic Pivot will differ based on that, e.g. a mega cap stock would not
		///		  most as much as a small cap stock.
		///		* For initial risk calculations the low of the bar Episodic Pivot bar can be used as an stop-loss.
		/// </summary>
		public sealed class _EpisodicPivot : FunctionSimple<System.Boolean>
		{
			public const int DEFAULT_GROWTH_DAYS = 120;
			public const double DEFAULT_GROWTH_PERCENT = 0.3;
			public const double DEFAULT_MINIMUM_PERCENTAGE_CHANGE = 0.07;
			public const double DEFAULT_MAXIMUM_PERCENTAGE_CHANGE = 0.15;

			//inputs
			public IInstrument Instrument { get; set; }
			public int GrowthDays { get; set; }
			public double GrowthPercent { get; set; }
			public double MinimumPercentageChange { get; set; } //gap up/down must be at least this minimum - this must always be a positive value, algoritm will flip it around if required
			public double MaximumPercentageChange { get; set; } //gap up/down clipped to this maximum to avoid pump-and-dump stocks - this must always be a positive value, algorithm will flip it around if required

			//properties


			//attributes


			//interface methods
			public _EpisodicPivot(CStudyControl _master) : base(_master) { }
			public _EpisodicPivot(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				GrowthDays = DEFAULT_GROWTH_DAYS;
				GrowthPercent = DEFAULT_GROWTH_PERCENT;
				MinimumPercentageChange = DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
				MaximumPercentageChange = DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			}

			protected override void StartCalc()
			{
				if (GrowthDays <= 0) throw new Exception("Growth days must be larger than zero.");
				//growth percentage can be anything both positive and negative
				if (MinimumPercentageChange <= 0) throw new Exception("MinimumPercentageChange must be larger than zero.");
				if (MaximumPercentageChange <= 0) throw new Exception("MaximumPercentageChange must be larger than zero.");
				if (MinimumPercentageChange > MaximumPercentageChange) throw new Exception("MinimumPercentageChange must be smaller or equal to MaximumPercentageChange.");
			}

			protected override System.Boolean CalcBar()
			{
				if (Instrument.CurrentBar < GrowthDays) return false;

				double lowest = Instrument.Close.Lowest(GrowthDays);
				double highest = Instrument.Close.Highest(GrowthDays);
				if (lowest == 0 || highest == 0 || Instrument.High[1] == 0) return false;

				//for long positions: growth should be >= GrowthPercent (positive value)
				//for short positions: decline should be <= GrowthPercent (negative value)
				bool validGrowthTrend = false;
				if (GrowthPercent >= 0)
					validGrowthTrend = (Instrument.Close[0] / lowest) - 1 >= GrowthPercent;
				else
					validGrowthTrend = (Instrument.Close[0] / highest) - 1 <= GrowthPercent;

				if (!validGrowthTrend) return false;

				// Calculate price change for potential entry
				double priceChange;
				if (GrowthPercent >= 0)
					//for long positions - gap up (current low above previous high)
					priceChange = (Instrument.Low[0] / Instrument.High[1]) - 1;
				else
					//for short positions - gap down (current high below previous low)
					priceChange = (Instrument.High[0] / Instrument.Low[1]) - 1;


				//for long positions: MinimumPercentageChange should be positive
				//for short positions: MinimumPercentageChange should be negative
				double effectiveMin = GrowthPercent >= 0 ? MinimumPercentageChange : -MaximumPercentageChange;
				double effectiveMax = GrowthPercent >= 0 ? MaximumPercentageChange : -MinimumPercentageChange;

				//check if the price change falls within our specified range
				if (priceChange < effectiveMin || priceChange > effectiveMax) return false;

				return true;
			}


			//methods


		}
	}
}