using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	/// <summary>
	/// RSI with a roofing filter to smooth out noise to make the RSI more reliable as described by
	/// Dr. John F. Ehlers in Cycle Analytics for Traders p. 78 and 87.
	/// </summary>
	public class _ModifiedRSI : IndicatorObject
	{
		//constants


		//inputs
		[Input] public int Length { get; set; }           //stochastic length
		[Input] public int LowPassLength { get; set; }    //roofing filter low pass length
		[Input] public int HighPassLength { get; set; }   //roofing filter high pass length

		//properties


		//attributes
		private Function.ModifiedRSI m_rsi;
		private IPlotObject m_plot;

		//interface methods
		public _ModifiedRSI(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			// create variable objects, function objects, plot objects etc.
			Length = Function.ModifiedRSI.DEFAULT_LENGTH;
			LowPassLength = Function.ModifiedRSI.DEFAULT_LP_LENGTH;
			HighPassLength = Function.ModifiedRSI.DEFAULT_HP_LENGTH;
			m_rsi = new Function.ModifiedRSI(this);
			m_plot = AddPlot(new PlotAttributes("MRSI", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_rsi.Length = Length;
			m_rsi.LowPassLength = LowPassLength;
			m_rsi.HighPassLength = HighPassLength;
		}

		protected override void CalcBar()
		{
			m_plot.Set(m_rsi[0]);
		}

		//methods


	}
}