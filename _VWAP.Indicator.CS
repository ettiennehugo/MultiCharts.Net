using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  [SameAsSymbol(true)]
	public class _VWAP : IndicatorObject {
		public _VWAP(object _ctx):base(_ctx){}
		
    //Inputs:
    [Input]public int Length { get; set; }

    //Attributes:
    private IPlotObject _vwapPlot;
    private VWAP _vwap;
		
    protected override void Create() {
      // create variable objects, function objects, plot objects etc.
      Length = VWAP.DEFAULT_LENGTH;
			_vwapPlot = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
      _vwap = new VWAP(this);
		}

		protected override void StartCalc() {
      _vwap.Length = Length;
      _vwap.Price = Bars.Close;
      _vwap.Volume = Bars.Volume;
		}

		protected override void CalcBar() {
      if (Bars.Status == EBarState.Close)	_vwapPlot.Set(_vwap.Value);
		}
	}
}