using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	public class _RelativeStrength : IndicatorObject
	{
		//constants


		//inputs


		//properties
		[Input] public int Length { get; set; }
		[Input] public int SmoothingLength { get; set; }

		//attributes
		private Function._RelativeStrength m_relativeStrength;
		private IPlotObject m_plot;
		private IPlotObject m_plotOne;

		//interface methods
		public _RelativeStrength(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = Function._RelativeStrength.LENGTH;
			SmoothingLength = Function._SuperSmoother.DEFAULT_PERIOD;
			m_relativeStrength = new Function._RelativeStrength(this);
			m_plot = AddPlot(new PlotAttributes("RS", EPlotShapes.Line, Color.Red));
			m_plotOne = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.LightGray));
		}

		protected override void StartCalc()
		{
			m_relativeStrength.Length = Length;
			m_relativeStrength.SmoothingLength = SmoothingLength;
			m_relativeStrength.Instrument1 = BarsOfData(1);
			m_relativeStrength.Instrument2 = BarsOfData(2);
		}

		protected override void CalcBar()
		{
			m_plot.Set(m_relativeStrength[0]);
			m_plotOne.Set(1.0);
		}
	}
}