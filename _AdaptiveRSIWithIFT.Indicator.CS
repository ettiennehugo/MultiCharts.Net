using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _AdaptiveRSIWithIFT : IndicatorObject {
		//constants

		
		//types


		//properties


		//attribute
		private IPlotObject m_plot;

		//interface methods
		public _AdaptiveRSIWithIFT(object _ctx):base(_ctx){}

		protected override void Create() {
			// create variable objects, function objects, plot objects etc.
			m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc() {
			// assign inputs 
		}

		protected override void CalcBar(){
			// indicator logic 
			m_plot.Set(Bars.Close[0]);
		}

		//methods


	}
}