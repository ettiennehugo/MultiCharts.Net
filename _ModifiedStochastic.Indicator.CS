using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	/// <summary>
	/// Stochastic with a roofing filter to smooth out noise to make the stochastic more reliable as described by
	/// Dr. John F. Ehlers in Cycle Analytics for Traders p. 78 and 83.
	/// </summary>
	public class _ModifiedStochastic : IndicatorObject
	{
		//constants


		//inputs
		[Input] public int Length { get; set; }
		[Input] public int LowPassLength { get; set; }		//roofing filter low pass length
		[Input] public int HighPassLength { get; set; }		//roofing filter high pass length

		//properties


		//attributes
		private Function.ModifiedStochastic m_stochastic;
		private IPlotObject m_plot;

		//interface methods
		public _ModifiedStochastic(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = Function.ModifiedStochastic.DEFAULT_LENGTH;
			LowPassLength = Function.ModifiedStochastic.DEFAULT_LOW_PASS_LENGTH;
			HighPassLength = Function.ModifiedStochastic.DEFAULT_HIGH_PASS_LENGTH;
			m_stochastic = new Function.ModifiedStochastic(this);
			m_plot = AddPlot(new PlotAttributes("MSTCH", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_stochastic.Length = Length;
			m_stochastic.LowPassLength = LowPassLength;
			m_stochastic.HighPassLength = HighPassLength;
		}

		protected override void CalcBar()
		{
			m_plot.Set(m_stochastic[0]);
		}

		//methods


	}
}