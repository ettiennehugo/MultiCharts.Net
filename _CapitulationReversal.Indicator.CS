using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _CapitulationReversal : IndicatorObject {

		//constants


		//inputs


		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._CapitulationReversal m_capitulationReversal;

		//interface methods
		public _CapitulationReversal(object _ctx) : base(_ctx) { }

		protected override void Create()
		{			
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_capitulationReversal = new Function._CapitulationReversal(this);
		}

		protected override void StartCalc()
		{

		}

		protected override void CalcBar()
		{
			if (m_capitulationReversal[0])
			{
				Output.WriteLine("CR - {0}", Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}