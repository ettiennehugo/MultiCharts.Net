using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{	
		public class _StochWithFisherTransform : IndicatorObject {
    //Inputs:
    [Input] public int StochLength { get; set; }
    [Input] public int SmoothLength1 { get; set; }
    [Input] public int SmoothLength2 { get; set; }
    [Input] public double OverBought { get; set; }
    [Input] public double OverSold { get; set; }

    //Attributes:
    private Fisher _fisherHighFC;
    private Fisher _fisherLowFC;
    private Fisher _fisherCloseFC;
    private Stochastic _stochasticFC;
    private VariableSeries<Double> _ofastk;
    private VariableSeries<Double> _ofastd;
    private VariableSeries<Double> _oslowk;
    private VariableSeries<Double> _oslowd;
    private IPlotObject plot1;
    private IPlotObject plot2;
    private IPlotObject plot3;
    private IPlotObject plot4;

    public _StochWithFisherTransform(object _ctx):base(_ctx) {
      StochLength = 14;
      SmoothLength1 = 3;
      SmoothLength2 = 3;
      OverBought = 80.0;
      OverSold = 20.0;
    }

    protected override void Create() {
      _fisherHighFC = new Fisher(this);
      _fisherLowFC = new Fisher(this);
      _fisherCloseFC = new Fisher(this);
      _stochasticFC = new Stochastic(this);
      _ofastk = new VariableSeries<double>(this);
      _ofastd = new VariableSeries<double>(this);
      _oslowk = new VariableSeries<double>(this);
      _oslowd = new VariableSeries<double>(this);

      plot1 = AddPlot(new PlotAttributes("SlowK", EPlotShapes.Line, Color.Yellow));
      plot2 = AddPlot(new PlotAttributes("SlowD", EPlotShapes.Line, Color.Blue));
      plot3 = AddPlot(new PlotAttributes("OverBought", EPlotShapes.Line, Color.Red));
      plot4 = AddPlot(new PlotAttributes("OverSold", EPlotShapes.Line, Color.Green));
    }

    protected override void StartCalc() {
      _fisherHighFC.Price = Bars.High;
	  _fisherHighFC.Length = StochLength;
      _fisherLowFC.Price = Bars.Low;
	  _fisherLowFC.Length = StochLength;
      _fisherCloseFC.Price = Bars.Close;
	  _fisherCloseFC.Length = StochLength;
      _stochasticFC.stochlength = StochLength;
      _stochasticFC.length1 = SmoothLength1;
      _stochasticFC.length2 = SmoothLength2;
      _stochasticFC.smoothingtype = 1;
      _stochasticFC.pricec = _fisherCloseFC;
      _stochasticFC.priceh = _fisherHighFC;
      _stochasticFC.pricel = _fisherLowFC;
      _stochasticFC.ofastk = _ofastk;
      _stochasticFC.ofastd = _ofastd;
      _stochasticFC.oslowk = _oslowk;
      _stochasticFC.oslowd = _oslowd;
    }

    protected override void CalcBar(){
      if (Bars.Status == EBarState.Close)
        _stochasticFC.Call();

      plot1.Set(_oslowk.Value);
      plot2.Set(_oslowd.Value);
      plot3.Set(OverBought);
      plot4.Set(OverSold);
		}
	}
}