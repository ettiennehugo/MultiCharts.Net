using System;
using System.Drawing;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  public class _ADX : IndicatorObject
  {
    private DirMovement _dirMovement;
    private IPlotObject _plot1;
    private IPlotObject _plot2;
    private IPlotObject _plot3;
    private IPlotObject _plot4;
    private IPlotObject _plot5;
    private IPlotObject _plot6;


    public _ADX(object ctx) :
        base(ctx)
    {
      length = 14;
      trend_level = 20;
      overbought_level = 40;
    }

    [Input]
    public int length { get; set; }
    [Input]
    public int trend_level { get; set; }
    [Input]
    public int overbought_level { get; set; }

    protected override void Create()
    {
      _dirMovement = new DirMovement(this);
      _plot1 = AddPlot(new PlotAttributes("ADX", 0, Color.Cyan, Color.Empty, 0, EPlotStyle.Solid, true));
      _plot2 = AddPlot(new PlotAttributes("ADXR", 0, Color.Magenta, Color.Empty, 0, EPlotStyle.Solid, true));
      _plot3 = AddPlot(new PlotAttributes("DMI+", 0, Color.Green, Color.Empty, 0, EPlotStyle.Dash, true));
      _plot4 = AddPlot(new PlotAttributes("DMI-", 0, Color.Red, Color.Empty, 0, EPlotStyle.Dash, true));
      _plot5 = AddPlot(new PlotAttributes("Trend", 0, Color.LightGray, Color.Empty, 0, EPlotStyle.Solid, true));
      _plot6 = AddPlot(new PlotAttributes("Overbought", 0, Color.LightGray, Color.Empty, 0, EPlotStyle.Solid, true));
    }

    protected override void StartCalc()
    {
      _dirMovement.PriceH = Bars.High;
      _dirMovement.PriceL = Bars.Low;
      _dirMovement.PriceC = Bars.Close;
      _dirMovement.Length = length;
    }

    protected override void CalcBar()
    {
      _plot1.Set(0, _dirMovement.ADX.Value);
      _plot2.Set(0, _dirMovement.ADXR.Value);
      _plot3.Set(0, _dirMovement.DMIPlus.Value);
      _plot4.Set(0, _dirMovement.DMIMinus.Value);
      _plot5.Set(0, trend_level);
      _plot6.Set(0, overbought_level);

      if (PublicFunctions.DoubleGreater(_dirMovement.ADXR.Value, _dirMovement.ADXR[1])
          && PublicFunctions.DoubleLessEquals(_dirMovement.ADXR[1], _dirMovement.ADXR[2]))
      {
        Alerts.Alert("ADXR turning up");
      }
      else
      {
        if (PublicFunctions.DoubleLess(_dirMovement.ADXR.Value, _dirMovement.ADXR[1])
            && PublicFunctions.DoubleGreaterEquals(_dirMovement.ADXR[1], _dirMovement.ADXR[2]))
        {
          Alerts.Alert("ADXR turning down");
        }
      }
    }
  }
}