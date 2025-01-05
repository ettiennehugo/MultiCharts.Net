using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.IO;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Episodic Pivot Scanner signal that works with the high-volume opening bar money management signal.
	/// https://www.youtube.com/watch?v=YyNOhw2OLuI
	///	Required Feeds:
	///		1. Scanner data feed - typically daily data feed for the same instrument.
	/// </summary>
	public class _EpisodicPivotScanner : _CustomSignal {
		//constants


		//inputs
		[Input] public int GrowthDays { get; set; }
		[Input] public double GrowthPercent { get; set; }
		[Input] public double MinimumPercentageChange { get; set; } //gap up must be at least this minimum
		[Input] public double MaximumPercentageChange { get; set; } //gap up clipped to this maximum to avoid pump-and-dump stocks
		[Input] public bool Debug { get; set; } //debug flag to print out the values

		//properties


		//attributes
		private PowerLanguage.Function._EpisodicPivot m_episodicPivot;

		//interface methods
		public _EpisodicPivotScanner(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			GrowthDays = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_DAYS;
			GrowthPercent = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
			MinimumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			MaximumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			Debug = false;
			m_episodicPivot = new PowerLanguage.Function._EpisodicPivot(this);
		}

		protected override void StartCalc()
		{
			m_episodicPivot.Instrument = Bars;
			m_episodicPivot.GrowthDays = GrowthDays;
			m_episodicPivot.GrowthPercent = GrowthPercent;
			m_episodicPivot.MinimumPercentageChange = MinimumPercentageChange;
			m_episodicPivot.MaximumPercentageChange = MaximumPercentageChange;
		}

		protected override void CalcBar()
		{
			this.MyPortfolioData[_PMMS_HighVolumeOpeningRange.PARAMETER_SCAN_FOR_ENTRY] = false;

			if (Environment.CalcReason == CalculationReason.Default && m_episodicPivot[0])
			{
				if (Debug)
				{
					string trace = string.Format("EpisodicPivot, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
					if (MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE))
					{
						StreamWriter debugTrace = (StreamWriter)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE];
						lock (debugTrace) debugTrace.WriteLine(trace);
					}
					Output.WriteLine(trace);

					//string debugMessage = MyPortfolioData.ContainsKey(_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE) ? (string)MyPortfolioData[_PMMS_DebugTrace.PARAMETER_DEBUG_TRACE] : "";
					//if (debugMessage.Length > 0) debugMessage += _PMMS_DebugTrace.TRACE_SEPARATOR;
					//string trace = string.Format("EpisodicPivot, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
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