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
	///   * Stock must have shown some minimal momentum in the past, typically 30% or more up in the past 1,3 or 6 months.
	///	  * Stock must have consolidated for at least x-bars within a Darvas box.
	///	  * Waits for a high volume opening range bar to form on the entry data feed.
	///	  * IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _DarvasBox : _QullamaggieBase {
		//constants
		public const double DEFAULT_MINIMUM_GROWTH_PERCENT = 0.3;
		public const string DEFAULT_GROWTH_LOOKBACKS = "20,60,120"; //1, 3, 6 months trading days (20- trading days per month)

		//inputs
		[Input] public double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public string GrowthDays { get; set; }          		//number of days to look back for the growth
		[Input] public int DarvasBoxLength { get; set; }			  	//minimum required length of the Darvas box
		[Input] public double AtrEpsilon { get; set; }  			//percentage of the ATR to use for the range close factor in Darvas box computations

		//attributes
		private PowerLanguage.Function._DarvasBox m_darvasBox;
		private int[] m_growthDays;
		private int m_maxGrowthDays;

		//interface methods
		public _DarvasBox(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			base.Create();
			MinimumGrowthPercent = DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = DEFAULT_GROWTH_LOOKBACKS;
			DarvasBoxLength = PowerLanguage.Function._DarvasBox.DEFAULT_LENGTH;
			AtrEpsilon = PowerLanguage.Function._DarvasBox.DEFAULT_EPSILON;
			m_darvasBox = new PowerLanguage.Function._DarvasBox(this);
		}

		protected override void StartCalc()
		{
			if (MinimumGrowthPercent <= 0) throw new ArgumentException("Minimum growth percent must be greater than zero.");
			m_growthDays = parseLookbackList(GrowthDays);
			if (m_growthDays.Length == 0) throw new ArgumentException("No growth days specified.");
			foreach (var growthDays in m_growthDays)
				if (growthDays <= 0) throw new ArgumentException("Growth days must be greater than zero.");
			m_maxGrowthDays = m_growthDays.Max();

			//Bars is used as the entry time frame criteria
			m_darvasBox.Instrument = BarsOfData(2);
			m_darvasBox.Length = DarvasBoxLength;
			m_darvasBox.Epsilon = AtrEpsilon;

			base.StartCalc();
		}

		protected override bool calcEntry()
		{
			if (!base.calcEntry()) return false; //base class filtering not met
			if (m_volumeInstrument.CurrentBar <= m_maxGrowthDays) return false; //not enough data

			bool minimumGrowth = false;
			foreach (var growthDays in m_growthDays)
				if (growthPercent(m_volumeInstrument, growthDays) > MinimumGrowthPercent)
				{
					minimumGrowth = true;
					break;
				}

			if (!minimumGrowth) return false; //not enough growth
			if (!m_darvasBox[1]) return false; //no Darvas Box detection on daily timeframe one day back
			if (!qullamaggieFilter()) return false; //Qullamaggie filter not met
			if (Bars.Close[0] < m_darvasBox.High) return false; //not break out of the Darvas box

			//allow base class filtering and potential entry
			if (Debug) Output.WriteLine("Darvas Box detected on {0}, {1} at close {2}", Bars.Info.Name, Bars.Time[0], Bars.Close[0]);
			return true;
		}
	}
}