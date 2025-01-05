using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;
using System.IO;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Crable Narrow Range Bar Scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _CrableNRXScanner : _CustomSignal {
		//constants


		//inputs
		[Input] public int Length { get; set; }       //required length of the narrow range bars
		[Input] public int ScanLength { get; set; }   //scan length for the narrow range bars
		[Input] public double MinimumATR { get; set; }
		[Input] public bool Debug { get; set; }       //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._CrableNRX m_crableNRX;

		//interface methods
		public _CrableNRXScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = PowerLanguage.Function._CrableNRX.DEFAULT_LENGTH;
			ScanLength = PowerLanguage.Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			MinimumATR = PowerLanguage.Function._CrableNRX.DEFAULT_MINIMUM_ATR;
			Debug = false;
			m_crableNRX = new PowerLanguage.Function._CrableNRX(this);
		}

		protected override void StartCalc()
		{
			m_crableNRX.Instrument = Bars;
			m_crableNRX.Length = Length;
			m_crableNRX.ScanLength = ScanLength;
			m_crableNRX.MinimumATR = MinimumATR;
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_crableNRX[0])
			{
				if (Debug)
				{
					string trace = string.Format("CrableNRX, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("CrableNRX, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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