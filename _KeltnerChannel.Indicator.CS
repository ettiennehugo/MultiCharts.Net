using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{

  /// <summary>
  /// Improved Keltner channel using a linear regression as the center line.
  /// </summary>

  [SameAsSymbol(true)]
  public class _KeltnerChannel : IndicatorObject
  {
    //Inputs:
    //[Input] public int LinRegLength { get; set; }

    [Input] public int FastLength { get; set; }
    [Input] public int SlowLength { get; set; }
    [Input] public int ATRLength { get; set; }
    [Input] public double ScalingFactor { get; set; }

    public _KeltnerChannel(object _ctx) : base(_ctx)
    {
      //LinRegLength = 100;
      FastLength = 2;
      SlowLength = 30;
      ATRLength = 20;
      ScalingFactor = 1.5;
      longestLength = -1;
    }

    private IPlotObject centerPlot;
    private IPlotObject upperPlot;
    private IPlotObject lowerPlot;
    private VariableSeries<double> centerLine;
    private VariableSeries<double> upperLine;
    private VariableSeries<double> lowerLine;
    //private LinearRegValue linRegFC;
    private PowerLanguage.Function._KAMA kama;
    private int longestLength;

    protected override void Create()
    {
      //linRegFC = new LinearRegValue(this);
      kama = new PowerLanguage.Function._KAMA(this);
      centerLine = new VariableSeries<double>(this);
      upperLine = new VariableSeries<double>(this);
      lowerLine = new VariableSeries<double>(this);
      centerPlot = AddPlot(new PlotAttributes("KCC", EPlotShapes.Line, Color.LightGray));
      upperPlot = AddPlot(new PlotAttributes("KCU", EPlotShapes.Line, Color.Yellow));
      lowerPlot = AddPlot(new PlotAttributes("KCL", EPlotShapes.Line, Color.Yellow));
    }
    protected override void StartCalc()
    {
      //linRegFC.price = Bars.Close;
      //linRegFC.length = LinRegLength;
      //linRegFC.tgtbar = 0;			

      kama.Price = Bars.Close;
      kama.FastLength = FastLength;
      kama.SlowLength = SlowLength;

      longestLength = ATRLength;
      longestLength = longestLength < SlowLength ? SlowLength : longestLength;
      longestLength = longestLength < FastLength ? FastLength : longestLength;
    }

    protected override void CalcBar()
    {
      centerLine.Value = kama[0];

      double scaledAtr = ScalingFactor * this.AverageTrueRange(ATRLength);
      lowerLine.Value = centerLine.Value - scaledAtr;
      upperLine.Value = centerLine.Value + scaledAtr;

      if (Bars.CurrentBar >= longestLength)
      {
        centerPlot.Set(centerLine.Value);
        upperPlot.Set(upperLine.Value);
        lowerPlot.Set(lowerLine.Value);
      }

    }
  }
}