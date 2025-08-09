using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  [SameAsSymbol(true)]
  public class _EMA3 : IndicatorObject {
    public enum CandleStickPrice {
      Open,
      High,
      Low,
      Close,
    }

    [Input] public CandleStickPrice priceToUse { get; set; }
    [Input] public int length1 { get; set; }
    [Input] public int length2 { get; set; }
    [Input] public int length3 { get; set; }
    [Input] public int displace { get; set; }

    private IPlotObject _plot1;
    private IPlotObject _plot2;
    private IPlotObject _plot3;
    private XAverage _averageFC1;
    private XAverage _averageFC2;
    private XAverage _averageFC3;

    public _EMA3(object _ctx) : base(_ctx) {
      length1 = 9;
      length2 = 21;
      length3 = 49;
      priceToUse = CandleStickPrice.Close;
    }

    protected override void Create() {
      // create variable objects, function objects, plot objects etc.
      _averageFC1 = new XAverage(this);
      _averageFC2 = new XAverage(this);
      _averageFC3 = new XAverage(this);
      _plot1 = AddPlot(new PlotAttributes("EMA1", EPlotShapes.Line, Color.DarkGreen, Color.DarkGreen, 3, 0, true));
      _plot2 = AddPlot(new PlotAttributes("EMA2", EPlotShapes.Line, Color.Yellow, Color.Yellow, 3, 0, true));
      _plot3 = AddPlot(new PlotAttributes("EMA3", EPlotShapes.Line, Color.Orange, Color.Orange, 3, 0, true));
    }
    protected override void StartCalc() {
      switch (priceToUse) {
        case CandleStickPrice.Open:
          _averageFC1.Price = Bars.Open;
          _averageFC2.Price = Bars.Open;
          _averageFC3.Price = Bars.Open;
          break;
        case CandleStickPrice.High:
          _averageFC1.Price = Bars.High;
          _averageFC2.Price = Bars.High;
          _averageFC3.Price = Bars.High;
          break;
        case CandleStickPrice.Low:
          _averageFC1.Price = Bars.Low;
          _averageFC2.Price = Bars.Low;
          _averageFC3.Price = Bars.Low;
          break;
        case CandleStickPrice.Close:
          _averageFC1.Price = Bars.Close;
          _averageFC2.Price = Bars.Close;
          _averageFC3.Price = Bars.Close;
          break;
      }

      _averageFC1.Length = length1;
      _averageFC2.Length = length2;
      _averageFC3.Length = length3;
    }

    protected override void CalcBar() {
      _plot1.Set(displace, _averageFC1[0]);
      _plot2.Set(displace, _averageFC2[0]);
      _plot3.Set(displace, _averageFC3[0]);
    }
  }
}