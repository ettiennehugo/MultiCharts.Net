using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _SuperSmootherMulti : IndicatorObject {
		//constants

		
		//types


		//properties
		[Input] public int Length1 { get; set; }
		[Input] public int Length2 { get; set; }
		[Input] public int Length3 { get; set; }
		[Input] public int Length4 { get; set; }
		[Input] public int Length5 { get; set; }

		//attribute
		private IPlotObject m_plot1;
		private IPlotObject m_plot2;
		private IPlotObject m_plot3;
		private IPlotObject m_plot4;
		private IPlotObject m_plot5;
		private Function._SuperSmoother m_ss1;
		private Function._SuperSmoother m_ss2;
		private Function._SuperSmoother m_ss3;
		private Function._SuperSmoother m_ss4;
		private Function._SuperSmoother m_ss5;


		//interface methods
		public _SuperSmootherMulti(object _ctx):base(_ctx){}

		protected override void Create() {
			Length1 = 10;
			Length2 = 20;
			Length3 = 50;
			Length4 = 100;
			Length5 = 200;

			m_ss1 = new Function._SuperSmoother(this);
			m_ss2 = new Function._SuperSmoother(this);
			m_ss3 = new Function._SuperSmoother(this);
			m_ss4 = new Function._SuperSmoother(this);
			m_ss5 = new Function._SuperSmoother(this);

			m_plot1 = AddPlot(new PlotAttributes("SS1", EPlotShapes.Line, Color.Green, Color.Green, 3, 0, true));
			m_plot2 = AddPlot(new PlotAttributes("SS2", EPlotShapes.Line, Color.Yellow, Color.Yellow, 3, 0, true));
			m_plot3 = AddPlot(new PlotAttributes("SS3", EPlotShapes.Line, Color.Orange, Color.Orange, 3, 0, true));
			m_plot4 = AddPlot(new PlotAttributes("SS4", EPlotShapes.Line, Color.Red, Color.Red, 3, 0, true));
			m_plot5 = AddPlot(new PlotAttributes("SS5", EPlotShapes.Line, Color.DarkRed, Color.DarkRed, 3, 0, true));
		}

		protected override void StartCalc() 
		{
			m_ss1.Length = Length1;
			m_ss2.Length = Length2;
			m_ss3.Length = Length3;
			m_ss4.Length = Length4;
			m_ss5.Length = Length5;

			m_ss1.Instrument = Bars;
			m_ss2.Instrument = Bars;
			m_ss3.Instrument = Bars;
			m_ss4.Instrument = Bars;
			m_ss5.Instrument = Bars;
		}

		protected override void CalcBar()
		{
			m_plot1.Set(m_ss1[0]);
			m_plot2.Set(m_ss2[0]);
			m_plot3.Set(m_ss3[0]);
			m_plot4.Set(m_ss4[0]);
			m_plot5.Set(m_ss5[0]);
		}

		//methods


	}
}