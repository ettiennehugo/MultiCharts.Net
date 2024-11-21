using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _CrableNRX : IndicatorObject {

		//constants


		//inputs
		[Input] public int Length { get; set; }
		[Input] public int ScanLength { get; set; }

		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._CrableNRX m_crableNRX;

		//interface methods
		public _CrableNRX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Length = Function._CrableNRX.DEFAULT_LENGTH;
			ScanLength = Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_crableNRX = new Function._CrableNRX(this);
		}

		protected override void StartCalc()
		{
			m_crableNRX.Instrument = Bars;
			m_crableNRX.Length = Length;
			m_crableNRX.ScanLength = ScanLength;
		}

		protected override void CalcBar()
		{
			if (m_crableNRX[0])
			{
				Output.WriteLine("NRX - {0}", Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods



	}
}