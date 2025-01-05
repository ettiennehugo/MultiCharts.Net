using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;
using System.IO;

namespace PowerLanguage.Strategy {
	public class _LowThresholdBreakoutScreener : _CustomSignal {
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
		public _LowThresholdBreakoutScreener(object _ctx) : base(_ctx) { }

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
			m_lowThresholdBreakout.Historical = false;    //TODO remove this Historical parameter
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_lowThresholdBreakout[0] && 
			    (m_lowThresholdBreakout.CurrentBarDirection == ECurrentBarDirection.BullishLowThreshold || m_lowThresholdBreakout.CurrentBarDirection == ECurrentBarDirection.Bullish))
			{
				if (Debug)
				{
					string trace = string.Format("LowThresholdBreakout, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("LowThresholdBreakout, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					//debugMessage += trace;
					//MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] = debugMessage;
					//Output.WriteLine(trace);
				}

				MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = true;
				MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_DIRECTION] = EMarketPositionSide.Long;
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