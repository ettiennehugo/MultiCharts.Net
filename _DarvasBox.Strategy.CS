using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Darvas Box breakout strategy, it typically looks for x-number of bars that is consolidating within an upper and lower range and then breaks out of that range.
	/// The bar lows/highs must fall within a lower range and upper range based off an average upper and lower prices.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Stock must have consolidated for at least x-bars within a Darvas box.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _DarvasBox :	PortfolioSignalObject {
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }          //number of days to look back for the growth
		[Input] int MinimumLength { get; set; }			  //minimum required length of the Darvas box
		[Input] double AtrRangePercent { get; set; }  //percentage of the ATR to use for the range		

		//properties


		//attributes
		private IInstrument m_entryInstrument;
		private IInstrument m_secondInstrument;
		private IOrderMarket m_le;
		private double m_atrPercent;
		private double m_upperPriceAverage;
		private double m_lowerPriceAverage;
		private double m_upperRangeLow;
		private double m_upperRangeHigh;
		private double m_lowerRangeLow;
		private double m_lowerRangeHigh;

		//interface methods
		public _DarvasBox(object _ctx) : base(_ctx) { }


		//methods
		protected override void Create()
		{
			MinimumGrowthPercent = 30;    //minimum required growth in the past growth-days
			GrowthDays = 120;							//number of days to look back for the growth
			MinimumLength = 10;   //require at least 5 bars to form a Darvas box
			AtrRangePercent = 5;  //use 5% of the ATR for the range
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			m_entryInstrument = BarsOfData(1);
			m_secondInstrument = BarsOfData(2);
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					calcDefault();
					break;
			}
		}

		private void calcDefault()
		{

			/// TODO - develop a Darvas box FUNCTION.


			var m_atrPercent = this.AverageTrueRange(20);			





		}

	}
}