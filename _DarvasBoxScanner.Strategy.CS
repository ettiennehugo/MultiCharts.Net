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
	/// Darvas Box Scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _DarvasBoxScanner : _CustomSignal
	{
		//constants


		//inputs
		[Input] public int Length { get; set; }       //required length of the Darvas box
		[Input] public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 
		[Input] public bool Debug { get; set; }       //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._DarvasBox m_darvasBox;

		//interface methods
		public _DarvasBoxScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = PowerLanguage.Function._DarvasBox.DEFAULT_LENGTH;
			Epsilon = PowerLanguage.Function._DarvasBox.DEFAULT_EPSILON;
			Debug = false;
			m_darvasBox = new PowerLanguage.Function._DarvasBox(this);
		}

		protected override void StartCalc()
		{
			m_darvasBox.Instrument = Bars;
			m_darvasBox.Length = Length;
			m_darvasBox.Epsilon = Epsilon;
		}

		protected override void CalcBar()
		{
			MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_darvasBox[0])
			{
				if (Debug)
				{
					string trace = string.Format("DarvasBox, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("DarvasBox, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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