using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Function
{
	/// <summary>
	/// High volume episodic pivot function to identify potential episodic pivot points based around volume spikes.
	/// </summary>
	public sealed class _EpisodicPivotHighVolume : FunctionSimple<System.Boolean>
	{
		//constants
		public const int DEFAULT_GROWTH_BARS = 120;
		public const double DEFAULT_GROWTH_PERCENT = 0.3;

		//types
		public enum EVolumeRequired
		{
			Disable,
			AboveAverage,     //volume required should be above average
			AboveAverageSD1,  //volume required should be above average + 1 standard deviation
			AboveAverageSD2,  //volume required should be above average + 2 standard deviations
			AboveAverageSD3   //volume required should be above average + 3 standard deviations
		};

		//properties
		public IInstrument Instrument { get; set; } //instrument to use for the calculation
		public int GrowthBars { get; set; }
		public double GrowthPercent { get; set; }
		public EVolumeRequired VolumeRequired { get; set; } //volume required to be above average or above average + x standard deviations 
		public int Length { get; set; }       //Length to use for average and standard deviation.
		public double Level1 { get; set; }    //Factor to use for first level of standard deviation.
		public double Level2 { get; set; }    //Factor to use for second level of standard deviation.
		public double Level3 { get; set; }    //Factor to use for third level of standard deviation.			

		//attribute
		private Function._VolumeWithSD m_volumeWithSD; //volume with standard deviation function`

		//interface methods
		public _EpisodicPivotHighVolume(CStudyControl _master) : base(_master) { }
		public _EpisodicPivotHighVolume(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			m_volumeWithSD = new Function._VolumeWithSD(this);
			Instrument = Bars;
			GrowthBars = DEFAULT_GROWTH_BARS;
			GrowthPercent = DEFAULT_GROWTH_PERCENT;
			VolumeRequired = EVolumeRequired.AboveAverage;
			Level1 = Function._VolumeWithSD.DEFAULT_LEVEL1;
			Level2 = Function._VolumeWithSD.DEFAULT_LEVEL2;
			Level3 = Function._VolumeWithSD.DEFAULT_LEVEL3;
		}

		protected override void StartCalc()
		{
			if (GrowthBars <= 0) throw new Exception("Growth bars must be larger than zero.");

			m_volumeWithSD.Instrument = Instrument;
			m_volumeWithSD.Length = Length;
			m_volumeWithSD.Level1 = Level1;
			m_volumeWithSD.Level2 = Level2;
			m_volumeWithSD.Level3 = Level3;
		}

		protected override System.Boolean CalcBar()
		{
			if (Instrument.CurrentBar < Length) return false;

			double lowest = Instrument.Close.Lowest(GrowthBars);
			double highest = Instrument.Close.Highest(GrowthBars);
			if (lowest == 0 || highest == 0 || Instrument.High[1] == 0) return false;

			//for long positions: growth should be >= GrowthPercent (positive value)
			//for short positions: decline should be <= GrowthPercent (negative value)
			bool validGrowthTrend = false;
			if (GrowthPercent >= 0)
				validGrowthTrend = (Instrument.Close[0] / lowest) - 1 >= GrowthPercent;
			else
				validGrowthTrend = (Instrument.Close[0] / highest) - 1 <= GrowthPercent;

			if (!validGrowthTrend) return false;

			switch (VolumeRequired)
			{
				case EVolumeRequired.AboveAverage:
					return Instrument.Volume[0] > m_volumeWithSD.Average[0];
				case EVolumeRequired.AboveAverageSD1:
					return Instrument.Volume[0] > m_volumeWithSD.AvgPlusLevel1[0];
				case EVolumeRequired.AboveAverageSD2:
					return Instrument.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0];
				case EVolumeRequired.AboveAverageSD3:
					return Instrument.Volume[0] > m_volumeWithSD.AvgPlusLevel3[0];
				default:
					return false;
			}
		}

		//methods


	}
}