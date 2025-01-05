using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.IO;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Short the Spike Scanner signal that works with the high-volume opening bar money management signal.
	/// </summary>
	public class _ShortTheSpikeScanner : _CustomSignal {
		//constants


		//inputs
		[Input] public int Length { get; set; }             //length over which the spike is measured
		[Input] public double GrowthPercent { get; set; }   //percentage growth over the length to be considered a spike
		[Input] public bool Debug { get; set; }             //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._ShortTheSpike m_shortTheSpike;

		//interface methods
		public _ShortTheSpikeScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = PowerLanguage.Function._ShortTheSpike.DEFAULT_LENGTH;
			GrowthPercent = PowerLanguage.Function._ShortTheSpike.DEFAULT_GROWTH_PERCENT;
			m_shortTheSpike = new PowerLanguage.Function._ShortTheSpike(this);
		}

		protected override void StartCalc()
		{
			m_shortTheSpike.Instrument = Bars;
			m_shortTheSpike.Length = Length;
			m_shortTheSpike.GrowthPercent = GrowthPercent;
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_shortTheSpike[0])
			{
				if (Debug)
				{
					string trace = string.Format("ShortTheSpike, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("ShortTheSpike, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					//debugMessage += trace;
					//MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] = debugMessage;
					//Output.WriteLine(trace);
				}

				MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = true;
				MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_ENTRY_DIRECTION] = EMarketPositionSide.Short;
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