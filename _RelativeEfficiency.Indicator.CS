using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	/// <summary>
	/// Plots the relative efficiency of two instruments applied to the chart.
	/// </summary>
	public class _RelativeEfficiency : IndicatorObject
	{
		//constants
		public const int LENGTH = 20;

		//inputs
		[Input] public int Length { get; set; }
		[Input] public int SmoothingLength { get; set; }

		//attributes
		private IPlotObject m_relativeEfficiencyPlot;
		private Function._RelativeEfficiency m_relativeEfficiency;

		public _RelativeEfficiency(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = LENGTH;
			SmoothingLength = 0;
			m_relativeEfficiency = new Function._RelativeEfficiency(this);
			m_relativeEfficiencyPlot = AddPlot(new PlotAttributes("RE", EPlotShapes.Line, Color.LightGray));
		}

		protected override void StartCalc()
		{
			m_relativeEfficiency.Length = Length;
			m_relativeEfficiency.SmoothingLength = SmoothingLength;
			m_relativeEfficiency.Instrument1 = BarsOfData(1);
			m_relativeEfficiency.Instrument2 = BarsOfData(2);
		}

		protected override void CalcBar()
		{
			m_relativeEfficiencyPlot.Set(m_relativeEfficiency.Value);
		}
	}
}