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
			LowPassLength = Function.ModifiedStochastic.DEFAULT_LP_LENGTH;
			HighPassPeriod = Function.ModifiedStochastic.DEFAULT_HP_LENGTH;

			m_plot = AddPlot(new PlotAttributes("MSTCH", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_roofingFilter.LPLength = LowPassLength;
			m_roofingFilter.HPLength = HighPassPeriod;
			m_roofingFilter.Price = Bars.Close;

		}

		protected override void CalcBar()
		{
			 
			m_plot.Set(Bars.Close[0]);
		}

		//methods


	}
}