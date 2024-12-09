using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;

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
		[Input] public double MinimumPrice { get; set; }
		[Input] public int GrowthDays { get; set; }
		[Input] public double GrowthPercent { get; set; }
		[Input] public int VolumeLength { get; set; }
		[Input] public int AverageVolume { get; set; }
		[Input] public int AverageDollarVolume { get; set; }
		[Input] public double MinimumPercentageChange { get; set; }
		[Input] public double MaximumPercentageChange { get; set; }

		//properties


		//attributes
		private PowerLanguage.Function._EpisodicPivot m_episodicPivot;

		//interface methods
		public _EpisodicPivot(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			GrowthDays = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_DAYS;
			GrowthPercent = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
			MinimumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			MaximumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			m_episodicPivot = new PowerLanguage.Function._EpisodicPivot(this);
		}

		protected override void StartCalc()
		{
			m_episodicPivot.Instrument = Bars;
			m_episodicPivot.GrowthDays = GrowthDays;
			m_episodicPivot.GrowthPercent = GrowthPercent;
			m_episodicPivot.MinimumPercentageChange = MinimumPercentageChange;
			m_episodicPivot.MaximumPercentageChange = MaximumPercentageChange;
		}

		protected override void CalcBar()
		{
			if (m_episodicPivot[0]) base.CalcBar();
		}
	}
}