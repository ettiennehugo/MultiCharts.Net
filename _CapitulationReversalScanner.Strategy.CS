using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Capitulation Reversal Scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _CapitulationReversalScanner : _CustomSignal {
		//constants


		//inputs
		[Input] public double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public int GrowthDays { get; set; }                //number of days to look back for the growth
		[Input] public int ScanLength { get; set; }                //lookback length for the market high
		[Input] public double StockDownPercent { get; set; }       //stock down percent from the high in the past y-days
		[Input] public double MarketDownPercent { get; set; }      //market down percent from the high in the past y-days
		[Input] public double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down
		[Input] public bool Debug { get; set; }                    //debug flag to print out the values

		//properties


		//attributes
		PowerLanguage.Function._CapitulationReversal m_capitulationReversal;

		//interface methods
		public _CapitulationReversalScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumGrowthPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = PowerLanguage.Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			ScanLength = PowerLanguage.Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			StockDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_STOCK_DOWN_PERCENT;
			MarketDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MARKET_DOWN_PERCENT;
			GapDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;
			Debug = false;
			m_capitulationReversal = new PowerLanguage.Function._CapitulationReversal(this);
		}

		protected override void StartCalc()
		{
			m_capitulationReversal.Instrument = Bars;
			m_capitulationReversal.Market = BarsOfData(2);
			m_capitulationReversal.MinimumGrowthPercent = MinimumGrowthPercent;
			m_capitulationReversal.GrowthDays = GrowthDays;
			m_capitulationReversal.ScanLength = ScanLength;
			m_capitulationReversal.StockDownPercent = StockDownPercent;
			m_capitulationReversal.MarketDownPercent = MarketDownPercent;
			m_capitulationReversal.GapDownPercent = GapDownPercent;
		}

		protected override void CalcBar()
		{
			if (Environment.CalcReason == CalculationReason.Default && m_capitulationReversal[0])
			{
				if (Debug) Output.WriteLine("CapitulationReversal, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = true;
				this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_DIRECTION] = EMarketPositionSide.Long;
				//TODO fill values for the PMMS signal
				//_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY
				//_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_PRICE
				//_PMMS_HighVolumeOpeningRange.PARAMETER_RISK
				//_PMMS_HighVolumeOpeningRange.PARAMETER_STOP_LOSS
				//_PMMS_HighVolumeOpeningRange.PARAMETER_STRATEGY_DATA

			}
			else
				this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;
		}

		//methods


	}
}