using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class RelativeStrength : IndicatorObject {
		//constants


		//inputs


		//properties
		[Input] public int Length { get; set; }
		[Input] public int SmoothingLength { get; set; }

		//attributes
		private Function.RelativeStrength m_relativeStrength;
		private Function.SuperSmoother m_superSmoother;
		private IPlotObject m_plot;
		private IPlotObject m_plotOne;

		//interface methods
		public RelativeStrength(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = Function.RelativeStrength.LENGTH;
			SmoothingLength = Function.SuperSmoother.DEFAULT_PERIOD;
			m_relativeStrength = new Function.RelativeStrength(this);
			m_superSmoother = new Function.SuperSmoother(this);
			m_plot = AddPlot(new PlotAttributes("RS", EPlotShapes.Line, Color.Red));
			m_plotOne = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.LightGray));
		}

		protected override void StartCalc()
		{
			m_relativeStrength.Length = Length;
			m_relativeStrength.Instrument1 = BarsOfData(1);
			m_relativeStrength.Instrument2 = BarsOfData(2);
			m_superSmoother.Period = SmoothingLength;
			m_superSmoother.Price = m_relativeStrength;
		}

		protected override void CalcBar()
		{
			if (SmoothingLength != 0)
				m_plot.Set(m_superSmoother[0]);
			else
				m_plot.Set(m_relativeStrength[0]);
			m_plotOne.Set(1.0);
		}
	}
}