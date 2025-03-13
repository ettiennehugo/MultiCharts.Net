using System;
using System.Drawing;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _AverageExponential : IndicatorObject {
		//constants


		//types


		//properties
		[Input] public int Length { get; set; } = 10;
		[Input] public int Displace { get; set; } = 0;

		//attribute
		private IPlotObject m_plot;
		private Function._AverageExponential m_function;
		private VariableSeries<double> m_series;

		//interface methods
		public _AverageExponential(object _ctx):base(_ctx){}

		protected override void Create() {
			// create variable objects, function objects, plot objects etc.
			m_plot = AddPlot(new PlotAttributes("EMA", EPlotShapes.Line, Color.Red));
			m_function = new Function._AverageExponential(this);
			m_series = new VariableSeries<double>(this);
		}

		protected override void StartCalc() {
			if (Displace < 0) throw new ArgumentOutOfRangeException("Displace must be greater or equal to zero");
			m_function.Instrument = Bars;
			m_function.Length = Length;
		}

		protected override void CalcBar(){
			m_series.Value = m_function[0];
			m_plot.Set(Displace, m_series.Value);
		}

		//methods


	}
}