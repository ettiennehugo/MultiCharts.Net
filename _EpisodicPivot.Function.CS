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
		/// </summary>
		public sealed class _EpisodicPivot : FunctionSimple<System.Boolean>
		{
			public const double DEFAULT_MINIMUM_PRICE = 5.0;
			public const int DEFAULT_GROWTH_DAYS = 120;
			public const double DEFAULT_GROWTH_PERCENT = 0.3;
			public const int DEFAULT_VOLUME_LENGTH = 20;
			public const int DEFAULT_AVERAGE_VOLUME = 150000;
			public const int DEFAULT_AVERAGE_DOLLAR_VOLUME = 20000000;
			public const double DEFAULT_MINIMUM_PERCENTAGE_CHANGE = 0.07;
			public const double DEFAULT_MAXIMUM_PERCENTAGE_CHANGE = 0.15;

			//inputs
			public IInstrument Instrument {  get; set; }
			public double MinimumPrice { get; set; }
			public int GrowthDays { get; set; }
			public double GrowthPercent { get; set; }
			public int VolumeLength { get; set; }
			public int AverageVolume { get; set; }
			public int AverageDollarVolume { get; set; }
			public double MinimumPercentageChange { get; set; }	//gap up must be at least this minimum
			public double MaximumPercentageChange { get; set; }	//gap up clipped to this maximum to avoid pump-and-dump stocks

			//properties


			//attributes

			
			//interface methods
			public _EpisodicPivot(CStudyControl _master) : base(_master) { }
			public _EpisodicPivot(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumPrice = DEFAULT_MINIMUM_PRICE;
				GrowthDays = DEFAULT_GROWTH_DAYS;
				GrowthPercent = DEFAULT_GROWTH_PERCENT;
				VolumeLength = DEFAULT_VOLUME_LENGTH;
				AverageVolume = DEFAULT_AVERAGE_VOLUME;
				AverageDollarVolume = DEFAULT_AVERAGE_DOLLAR_VOLUME;
				MinimumPercentageChange = DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
				MaximumPercentageChange = DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			}

			protected override void StartCalc()
			{
				if (MinimumPrice <= 0) throw new Exception("MinimumPrice must be larger than zero.");
				if (GrowthDays <= 0) throw new Exception("Growth days must be larger than zero.");
				//growth percentage can be anything both positive and negative
				if (VolumeLength <= 0) throw new Exception("VolumeLength must be larger than zero.");
				if (AverageVolume <= 0) throw new Exception("AverageVolume must be larger than zero.");
				if (AverageDollarVolume <= 0) throw new Exception("AverageDollarVolume must be larger than zero.");
				if (MinimumPercentageChange <= 0) throw new Exception("MinimumPercentageChange must be larger than zero.");
				if (MaximumPercentageChange <= 0) throw new Exception("MaximumPercentageChange must be larger than zero.");
				if (MinimumPercentageChange > MaximumPercentageChange) throw new Exception("MinimumPercentageChange must be smaller or equal to MaximumPercentageChange.");
			}

			protected override System.Boolean CalcBar()
			{				
				if (Instrument.CurrentBar < VolumeLength && Instrument.CurrentBar < GrowthDays) return false;
				if (Instrument.Close[0] < MinimumPrice) return false;
				if (((Instrument.Close[0] / Instrument.Close[GrowthDays]) - 1) < GrowthPercent) return false;
				if (Instrument.Volume.Average(VolumeLength) < AverageVolume) return false;
				
				double averageDollarVolume = 0.0;
				for (int i = 0; i < VolumeLength; i++) averageDollarVolume = Instrument.Close[i] * Instrument.Volume[i];
				averageDollarVolume /= VolumeLength;
				if (averageDollarVolume < AverageDollarVolume) return false;

				double instrumentGrowth = (Instrument.Close[0] / Instrument.Close[1]) - 1;
				if (instrumentGrowth < MinimumPercentageChange || instrumentGrowth > MaximumPercentageChange) return false;

				return true;
			}

			//methods


		}
	}
}