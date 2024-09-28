using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	/// <summary>
	/// Plots Perry Kaufman's efficiency ratio for the stock price movement. 
	/// </summary>
	public class _EfficiencyRatio : IndicatorObject
	{
		//constants
		public const int LENGTH = 20;

		//inputs
		[Input] public int Length { get; set; }

		//attributes
		private IPlotObject m_efficiencyRatioPlot;
		private EfficiencyRatio m_efficiencyRatio;

		public _EfficiencyRatio(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = LENGTH;
			m_efficiencyRatio = new EfficiencyRatio(this);
			m_efficiencyRatioPlot = AddPlot(new PlotAttributes("ER", EPlotShapes.Line, Color.LightGray));
		}

		protected override void StartCalc()
		{
			m_efficiencyRatio.Length = Length;
		}

		protected override void CalcBar()
		{
			m_efficiencyRatioPlot.Set(m_efficiencyRatio.Value);
		}
	}
}