using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;

namespace PowerLanguage.Strategy {
	public class _LowThresholdBreakout : _CustomSignal {
		//constants


		//inputs
		[Input] public EScanBullishBearish ScanBullishBearish { get; set; }  //market direction to scan
		[Input] public int TradeIntensityShortPeriod { get; set; }  //short period for the trade intensity filter
		[Input] public int TradeIntensityLongPeriod { get; set; }  //short period for the trade intensity filter
		[Input] public double TradeIntensityPercent { get; set; } //percentage for trade intensity
		[Input] public double PreviousDayIncreasePercent { get; set; }  //percentage increase for pervious trading day to limit how much price can move before the low threshold detection
		[Input] public double MinimumPrice { get; set; }          //minimum price for stock
		[Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
		[Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
		[Input] public bool Debug { get; set; }

		//properties


		//attributes
		private PowerLanguage.Function._LowThresholdBreakout m_lowThresholdBreakout;

		//interface methods
		public _LowThresholdBreakout(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			ScanBullishBearish = PowerLanguage.Function._LowThresholdBreakout.SCAN_BULLISH_BEARISH;
			TradeIntensityShortPeriod = PowerLanguage.Function._LowThresholdBreakout.TRADE_INTENSITY_SHORT_PERIOD;
			TradeIntensityLongPeriod = PowerLanguage.Function._LowThresholdBreakout.TRADE_INTENSITY_LONG_PERIOD;
			TradeIntensityPercent = PowerLanguage.Function._LowThresholdBreakout.TRADE_INTENSITY_PERCENT;
			PreviousDayIncreasePercent = PowerLanguage.Function._LowThresholdBreakout.PREVIOUS_DAY_INCREASE_PERCENT;
			MinimumPrice = PowerLanguage.Function._LowThresholdBreakout.MINIMUM_PRICE;
			MinimumAverageVolume = PowerLanguage.Function._LowThresholdBreakout.MINIMUM_AVERAGE_VOLUME;
			AverageVolumeLength = PowerLanguage.Function._LowThresholdBreakout.AVERAGE_VOLUME_LENGTH;
			Debug = false;
			m_lowThresholdBreakout = new PowerLanguage.Function._LowThresholdBreakout(this);
		}

		protected override void StartCalc()
		{
			m_lowThresholdBreakout.TradeIntensityShortPeriod = TradeIntensityShortPeriod;
			m_lowThresholdBreakout.TradeIntensityLongPeriod = TradeIntensityLongPeriod;
			m_lowThresholdBreakout.PreviousDayIncreasePercent = PreviousDayIncreasePercent;
			m_lowThresholdBreakout.MinimumPrice = MinimumPrice;
			m_lowThresholdBreakout.MinimumAverageVolume = MinimumAverageVolume;
			m_lowThresholdBreakout.AverageVolumeLength = AverageVolumeLength;
			m_lowThresholdBreakout.Historical = false;		//TODO remove this Historical parameter
			m_lowThresholdBreakout.Debug = Debug;
		}

		protected override void CalcBar()
		{

		}

		//methods


	}
}