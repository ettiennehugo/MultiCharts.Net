using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Volatility contraction pattern strategy the price action is contracting into narrower bar ranges and then breaks out of that range.
	/// This is typically the pattern traded by Mark Minervini.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Stock must be contracting into a narrower range in the past x-days.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range
	/// </summary>
	public class _VCP : _OpeningRangeBarSignal {
		//constants
		public const double DEFAULT_MINIMUM_GROWTH_PERCENT = 0.3;
		public const int DEFAULT_GROWTH_DAYS = 60;

		//inputs
		[Input] public double MinimumGrowthPercent { get; set; } //minimum required growth in the past x-days
		[Input] public int GrowthDays { get; set; }          //number of days to look back for the growth
		[Input] public int FastLength { get; set; }          //fast length for the KAMA
		[Input] public int SlowLength { get; set; }          //slow length for the KAMA
		[Input] public double MinimumATRDelta { get; set; }  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
		[Input] public int ScanLength { get; set; }          //scan length for the VCP
		[Input] public int MinimumRequiredLegs { get; set; } //minimum number of required legs to form the VCP
		[Input] public double MinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 

		//properties


		//attributes
		private double m_growthPercent;
		private PowerLanguage.Function._VCP m_vcp;

		//interface methods
		public _VCP(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = DEFAULT_GROWTH_DAYS;
			FastLength = PowerLanguage.Function._VCP.DEFAULT_FAST_LENGTH;
			SlowLength = PowerLanguage.Function._VCP.DEFAULT_SLOW_LENGTH;
			MinimumATRDelta = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_ATR_DELTA;
			ScanLength = PowerLanguage.Function._VCP.DEFAULT_SCAN_LENGTH;
			MinimumRequiredLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_REQUIRED_LEGS;
			MinimumPercentageContracingLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;
			m_vcp = new PowerLanguage.Function._VCP(this);
		}

		protected override void StartCalc()
		{
			m_vcp.Instrument = BarsOfData(2);
			m_vcp.FastLength = FastLength;
			m_vcp.SlowLength = SlowLength;
			m_vcp.MinimumATRDelta = MinimumATRDelta;
			m_vcp.ScanLength = ScanLength;
			m_vcp.MinimumRequiredLegs = MinimumRequiredLegs;
			m_vcp.MinimumPercentageContracingLegs = MinimumPercentageContracingLegs;
			m_growthPercent = 1.0d + MinimumGrowthPercent;  //only checking positive growth
		}

		protected override void CalcBar()
		{
			if (Bars.CurrentBar < GrowthDays) return;
			double growthPercent = Bars.Close[GrowthDays - 1] != 0.0d ? (Bars.Close[0] - Bars.Close[GrowthDays - 1]) / Bars.Close[GrowthDays - 1] : 0.0d;
			if (m_growthPercent < growthPercent && m_vcp[0]) base.CalcBar();
		}
	}
}