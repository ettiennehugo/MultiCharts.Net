using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	public class _AutocorrelationReversals : IndicatorObject
	{
		//constants


		//inputs
		[Input] public int Length { get; set; }
		[Input] public int LowPassLength { get; set; }
		[Input] public int HighPassLength { get; set; }

		//properties


		//attributes
		private AutocorrelationReversals m_autoCorrelationReversals;
		private IPlotObject PlotReversal;

		//interface methods
		public _AutocorrelationReversals(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//setup attribute defaults
			Length = AutocorrelationReversals.DEFAULT_LENGTH;
			LowPassLength = AutocorrelationReversals.DEFAULT_LOW_PASS_LENGTH;
			HighPassLength = AutocorrelationReversals.DEFAULT_HIGH_PASS_LENGTH;
			m_autoCorrelationReversals = new AutocorrelationReversals(this);

			//create plot components
			PlotReversal = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_autoCorrelationReversals.Length = Length;
			m_autoCorrelationReversals.LowPassLength = LowPassLength;
			m_autoCorrelationReversals.HighPassLength = HighPassLength;
		}

		protected override void CalcBar()
		{
			PlotReversal.Set(m_autoCorrelationReversals[0]? 1.0d : 0.0d);
		}

		//methods


	}
}