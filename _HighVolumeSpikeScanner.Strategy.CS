using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;
using System.IO;

namespace PowerLanguage.Strategy
{
	public class _HighVolumeSpikeScanner : _CustomSignal
	{
		//constants


		//inputs
		[Input] public int SpikeLength { get; set; }          //length to look for spike
		[Input] public double GrowthPercentage { get; set; }  //percentage growth to consider as spike
		[Input] public int VolumeLength { get; set; }         //length to determine for volume average
		[Input] public double VolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike
		[Input] public bool Debug { get; set; }              //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._HighVolumeSpike m_highVolumeSpike;

		//interface methods
		public _HighVolumeSpikeScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			SpikeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_LENGTH;
			GrowthPercentage = PowerLanguage.Function._HighVolumeSpike.DEFAULT_GROWTH_PERCENTAGE;
			VolumeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_LENGTH;
			VolumeSigmaMultiplier = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_SIGMA_MULTIPLIER;
			m_highVolumeSpike = new PowerLanguage.Function._HighVolumeSpike(this);
			Debug = false;
		}

		protected override void StartCalc()
		{
			m_highVolumeSpike.Instrument = Bars;
			m_highVolumeSpike.SpikeLength = SpikeLength;
			m_highVolumeSpike.GrowthPercentage = GrowthPercentage;
			m_highVolumeSpike.VolumeLength = VolumeLength;
			m_highVolumeSpike.VolumeSigmaMultiplier = VolumeSigmaMultiplier;
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_highVolumeSpike[0])
			{
				if (Debug)
				{
					string trace = string.Format("HighVolumeSpike, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("HighVolumeSpike, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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