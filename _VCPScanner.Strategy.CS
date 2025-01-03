using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy {
	public class _VCPScanner : _CustomSignal {
		//constants


		//inputs
		[Input] public double MinimumGrowthPercent { get; set; } //minimum required growth in the past x-days
		[Input] public int GrowthDays { get; set; }          //number of days to look back for the growth
		[Input] public int FastLength { get; set; }          //fast length for the KAMA
		[Input] public int SlowLength { get; set; }          //slow length for the KAMA
		[Input] public double MinimumATRDelta { get; set; }  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
		[Input] public int ScanLength { get; set; }          //scan length for the VCP
		[Input] public int MinimumRequiredLegs { get; set; } //minimum number of required legs to form the VCP
		[Input] public double MinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 
		[Input] public bool Debug { get; set; }							 //debug flag to print out the values

		//properties


		//attributes
		private double m_growthPercent;
		private PowerLanguage.Function._VCP m_vcp;

		//interface methods
		public _VCPScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumGrowthPercent = PowerLanguage.Strategy._VCP.DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = PowerLanguage.Strategy._VCP.DEFAULT_GROWTH_DAYS;
			FastLength = PowerLanguage.Function._VCP.DEFAULT_FAST_LENGTH;
			SlowLength = PowerLanguage.Function._VCP.DEFAULT_SLOW_LENGTH;
			MinimumATRDelta = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_ATR_DELTA;
			ScanLength = PowerLanguage.Function._VCP.DEFAULT_SCAN_LENGTH;
			MinimumRequiredLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_REQUIRED_LEGS;
			MinimumPercentageContracingLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;
			Debug = false;
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
			m_growthPercent = 1.0d + MinimumGrowthPercent;	//only checking positive growth
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Bars.CurrentBar < GrowthDays) return;

			double growthPercent = Bars.Close[GrowthDays - 1] != 0.0d ? (Bars.Close[0] - Bars.Close[GrowthDays - 1]) / Bars.Close[GrowthDays - 1] : 0.0d;
			if (Environment.CalcReason == CalculationReason.Default && m_growthPercent < growthPercent && m_vcp[0])
			{
				if (Debug) Output.WriteLine("VCP, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = true;
				this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_DIRECTION] = EMarketPositionSide.Long;

				//TODO fill values for the PMMS signal
				//_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY
				//_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_PRICE
				//_PMMS_HighVolumeOpeningRange.PARAMETER_RISK
				//_PMMS_HighVolumeOpeningRange.PARAMETER_STOP_LOSS
				//_PMMS_HighVolumeOpeningRange.PARAMETER_STRATEGY_DATA

			}
		}

		//methods


	}
}