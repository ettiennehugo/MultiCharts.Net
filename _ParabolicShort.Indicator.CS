using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
	public class _ParabolicShort : IndicatorObject
	{
		//constants


		//types


		//properties
		[Input] public int Length { get; set; }             //length over which the spike is measured
		[Input] public double GrowthPercent { get; set; }   //percentage growth over the length to be considered a spike
		[Input] public bool Debug { get; set; }             //debug flag to print out the values

		//attribute
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._ParabolicShort m_parabolicShort;

		//interface methods
		public _ParabolicShort(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = PowerLanguage.Function._ParabolicShort.DEFAULT_LENGTH;
			GrowthPercent = PowerLanguage.Function._ParabolicShort.DEFAULT_GROWTH_PERCENT;
			Debug = false;
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_parabolicShort = new PowerLanguage.Function._ParabolicShort(this);
		}

		protected override void StartCalc()
		{
			m_parabolicShort.Instrument = Bars;
			m_parabolicShort.Length = Length;
			m_parabolicShort.GrowthPercent = GrowthPercent;
		}

		protected override void CalcBar()
		{
			if (m_parabolicShort[0])
			{
				if (Debug) Output.WriteLine("Parabolic Short Indicator, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}