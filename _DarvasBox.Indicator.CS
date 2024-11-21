using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _DarvasBox : IndicatorObject {

		//constants


		//inputs
		[Input] public int Length { get; set; }       //required length of the Darvas box
		[Input] public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._DarvasBox m_darvasBox;

		//interface methods
		public _DarvasBox(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = Function._DarvasBox.DEFAULT_LENGTH;
			Epsilon = Function._DarvasBox.DEFAULT_EPSILON;
			m_darvasBox = new Function._DarvasBox(this);
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
		}

		protected override void StartCalc()
		{
			m_darvasBox.Instrument = Bars;
			m_darvasBox.Length = Length;
			m_darvasBox.Epsilon = Epsilon;
		}

		protected override void CalcBar()
		{
			if (m_darvasBox[0])
			{
				Output.WriteLine("DB - {0}", Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}