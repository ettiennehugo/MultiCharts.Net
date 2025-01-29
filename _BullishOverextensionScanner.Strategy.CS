using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;
using System.IO;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Bullish overextension scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _BullishOverextensionScanner : _CustomSignal
	{
		//constants


		//inputs
		[Input] public double MinimumRisePercent { get; set; }
		[Input] public int RiseDays { get; set; }
		[Input] public int ATRLength { get; set; }
		[Input] public double MinimumOverextensionFactor { get; set; }
		[Input] public int FastSmoothingLength { get; set; }
		[Input] public int SlowSmoothingLength { get; set; }
		[Input] public int GradualInclineLength { get; set; }
		[Input] public bool Debug { get; set; }                  //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._BullishOverextension m_bullishOverextension;

		//interface methods
		public _BullishOverextensionScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumRisePercent = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			RiseDays = PowerLanguage.Function._BullishOverextension.DEFAULT_RISE_DAYS;
			ATRLength = PowerLanguage.Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualInclineLength = PowerLanguage.Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;
			Debug = false;
			m_bullishOverextension = new PowerLanguage.Function._BullishOverextension(this);

		}

		protected override void StartCalc()
		{
			m_bullishOverextension.Instrument = Bars;
			m_bullishOverextension.MinimumRisePercent = MinimumRisePercent;
			m_bullishOverextension.RiseDays = RiseDays;
			m_bullishOverextension.ATRLength = ATRLength;
			m_bullishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bullishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bullishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bullishOverextension.GradualInclineLength = GradualInclineLength;
		}

		protected override void CalcBar()
		{
			MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_bullishOverextension[0])
			{
				if (Debug)
				{
					string trace = string.Format("BullishOverextension, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("BullishOverextension, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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
	}
}