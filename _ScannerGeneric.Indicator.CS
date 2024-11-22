using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _ScannerGeneric : IndicatorObject {
		//constants


		//inputs
		[Input] public int NRXLength { get; set; }           //NRX length
		[Input] public int NRXScanLength { get; set; }       //Scan length
		[Input] public int DarvasLength { get; set; }        //Darvas length
		[Input] public double DarvasEpsilon { get; set; }    //Darvas epsilon measure

		//properties


		//attributes
		private IPlotObject m_plot;
		private Function._CrableNRX m_crableNRX;
		private Function._DarvasBox m_darvaBox;
		//private Function._CapitulationReversal m_capitulationReversal;

		//interface methods
		public _ScannerGeneric(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			NRXLength = Function._CrableNRX.DEFAULT_LENGTH;
			NRXScanLength = Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			DarvasLength = Function._DarvasBox.DEFAULT_LENGTH;
			DarvasEpsilon = Function._DarvasBox.DEFAULT_EPSILON;
			m_crableNRX = new Function._CrableNRX(this);
			m_darvaBox = new Function._DarvasBox(this);
			m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc()
		{
			m_crableNRX.Instrument = Bars;
			m_crableNRX.Length = NRXLength > 0 ? NRXLength : Function._CrableNRX.DEFAULT_LENGTH;
			m_crableNRX.ScanLength = NRXScanLength > 0 ? NRXScanLength : Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			m_darvaBox.Instrument = Bars;
			m_darvaBox.Length = DarvasLength > 0 ? DarvasLength : Function._DarvasBox.DEFAULT_LENGTH;
			m_darvaBox.Epsilon = DarvasEpsilon > 0 ? DarvasEpsilon : Function._DarvasBox.DEFAULT_EPSILON;
		}

		protected override void CalcBar()
		{
			m_plot.Set((NRXLength > 0 && m_crableNRX[0]) || (DarvasLength > 0 && m_darvaBox[0]) ? 1 : 0);
		}

		//methods


	}
}