using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _ShortTheSpike : IndicatorObject {

		//constants


		//inputs


		//properties


		//attributes
		private IPlotObject m_plot;

		//interface methods
		public _ShortTheSpike(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
		}
		protected override void StartCalc()
		{

		}

		protected override void CalcBar()
		{
			m_plot.Set(Bars.Close[0]);
		}

		//methods


	}
}