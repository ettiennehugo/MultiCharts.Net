using System;
using System.Drawing;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _AverageSimple : IndicatorObject {
		//constants


		//types


		//properties
		[Input] public int Length { get; set; }
		[Input] public int Displace { get; set; }

		//attribute
		private IPlotObject m_plot;
		private Function._AverageSimple m_function;
		private VariableSeries<double> m_series;

		//interface methods
		public _AverageSimple(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			// create variable objects, function objects, plot objects etc.
			Length = 10;
			Displace = 0;
			m_plot = AddPlot(new PlotAttributes("SMA", EPlotShapes.Line, Color.Red));
			m_function = new Function._AverageSimple(this);
			m_series = new VariableSeries<double>(this);
		}

		protected override void StartCalc()
		{
			if (Displace < 0) throw new ArgumentOutOfRangeException("Displace must be greater or equal to zero");
			m_function.Instrument = Bars;
			m_function.Length = Length;
		}

		protected override void CalcBar()
		{
			m_series.Value = m_function[0];
			m_plot.Set(Displace, m_series.Value);
		}

		//methods


	}
}