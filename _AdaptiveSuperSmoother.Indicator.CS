using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	/// <summary>
	/// 
	/// </summary>
	[SameAsSymbol]
	public class _AdaptiveSuperSmoother : IndicatorObject
	{
		//constants


		//inputs
		[Input]public int FastPeriod { get; set; }
		[Input]public int SlowPeriod { get; set; }
		[Input]public int EfficiencyRatioPeriod { get; set; }

		//properties


		//attributes
		private IPlotObject m_plot;
		private Function._AdaptiveSuperSmoother m_adaptiveSuperSmoother;

		//interface methods
		public _AdaptiveSuperSmoother(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			FastPeriod = Function._AdaptiveSuperSmoother.DEFAULT_FAST_PERIOD;
			SlowPeriod = Function._AdaptiveSuperSmoother.DEFAULT_SLOW_PERIOD;
			EfficiencyRatioPeriod = Function._AdaptiveSuperSmoother.DEFAULT_EFFICIENCY_RATIO_PERIOD;
			m_adaptiveSuperSmoother = new Function._AdaptiveSuperSmoother(this);
			m_plot = AddPlot(new PlotAttributes("ASS", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_adaptiveSuperSmoother.Instrument = Bars;
			m_adaptiveSuperSmoother.FastPeriod = FastPeriod;
			m_adaptiveSuperSmoother.SlowPeriod = SlowPeriod;
			m_adaptiveSuperSmoother.EfficiencyRatioPeriod = EfficiencyRatioPeriod;
		}

		protected override void CalcBar()
		{
			m_plot.Set(m_adaptiveSuperSmoother[0]);
		}

		//methods


	}
}