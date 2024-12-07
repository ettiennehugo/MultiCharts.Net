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
		[Input] double DownPercent { get; set; }			//market down percent from the high in the past y-days
		[Input] double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down

		//properties


		//attributes
		PowerLanguage.Function._CapitulationReversal m_capitulationReversal;

		//interface methods
		public _CapitulationReversal(object _ctx) : base(_ctx) { }

		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = PowerLanguage.Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			ScanLength = PowerLanguage.Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			DownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_DOWN_PERCENT;
			GapDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;
		}

		protected override void StartCalc()
		{
			m_capitulationReversal.Instrument = BarsOfData(2); //Longer timeframe bars for the instrument, e.g. daily
			m_capitulationReversal.Market = BarsOfData(3); //Market data bars used to determine market pullback, e.g. daily
			m_capitulationReversal.MinimumGrowthPercent = MinimumGrowthPercent;
			m_capitulationReversal.GrowthDays = GrowthDays;
			m_capitulationReversal.ScanLength = ScanLength;
			m_capitulationReversal.DownPercent = DownPercent;
			m_capitulationReversal.GapDownPercent = GapDownPercent;
		}

		protected override void CalcBar()
		{
			//if we get a capitulation reversal signal then we can start to look for a high volume opening bar entry
			if (m_capitulationReversal[0]) base.CalcBar();
		}
	}
}