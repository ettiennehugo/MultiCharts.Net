using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Bearish overextension scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _BearishOverextensionScanner : _CustomSignal
	{
		//constants


		//inputs
		[Input] public double MinimumDropPercent { get; set; }   //minimum required drop in the past x-days
		[Input] public int DropDays { get; set; }                //number of days to look back for the drop
		[Input] public int ATRLength { get; set; }               //length of the ATR to determine the overextension
		[Input] public double MinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
		[Input] public int FastSmoothingLength { get; set; }     //length of smoother to determine more current price move
		[Input] public int SlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
		[Input] public int GradualDeclineLength { get; set; }    //length of scan to check for gradual decline
		[Input] public bool Debug { get; set; }                  //debug flag to print out the values

		//properties


		//attributes
		PowerLanguage.Function._BearishOverextension m_bearishOverextension;

		//interface methods
		public _BearishOverextensionScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumDropPercent = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			DropDays = PowerLanguage.Function._BearishOverextension.DEFAULT_DROP_DAYS;
			ATRLength = PowerLanguage.Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualDeclineLength = PowerLanguage.Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;
			Debug = false;
			m_bearishOverextension = new PowerLanguage.Function._BearishOverextension(this);
		}

		protected override void StartCalc()
		{
			m_bearishOverextension.Instrument = Bars;
			m_bearishOverextension.MinimumDropPercent = MinimumDropPercent;
			m_bearishOverextension.DropDays = DropDays;
			m_bearishOverextension.ATRLength = ATRLength;
			m_bearishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bearishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bearishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bearishOverextension.GradualDeclineLength = GradualDeclineLength;
		}

		protected override void CalcBar()
		{
			if (Environment.CalcReason == CalculationReason.Default && m_bearishOverextension[0])
			{
				if (Debug) Output.WriteLine("BearishOverextension, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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