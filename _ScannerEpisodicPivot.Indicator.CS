using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  public class _ScannerEpisodicPivot : IndicatorObject
  {
    //inputs
    [Input] public double MinimumPrice { get; set; }
    [Input] public int VolumeLength { get; set; }
    [Input] public int AverageVolume { get; set; }
    [Input] public int AverageDollarVolume { get; set; }
    [Input] public double PercentageChange { get; set; }

    //attributes
    private IPlotObject criteriaPlot;
    private Function._ScannerEpisodicPivot scannerEpisodicPivot;

    public _ScannerEpisodicPivot(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      //default inputs
      MinimumPrice = Function._ScannerEpisodicPivot.MINIMUM_PRICE;
      VolumeLength = Function._ScannerEpisodicPivot.VOLUME_LENGTH;
      AverageVolume = Function._ScannerEpisodicPivot.AVERAGE_VOLUME;
      AverageDollarVolume = Function._ScannerEpisodicPivot.AVERAGE_DOLLAR_VOLUME;
      PercentageChange = Function._ScannerEpisodicPivot.PERCENTAGE_CHANGE;

      //create scanner function and plot
      scannerEpisodicPivot = new Function._ScannerEpisodicPivot(this);
      criteriaPlot = AddPlot(new PlotAttributes("Criteria", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc()
    {
      scannerEpisodicPivot.MinimumPrice = MinimumPrice;
      scannerEpisodicPivot.VolumeLength = VolumeLength;
      scannerEpisodicPivot.AverageVolume = AverageVolume;
      scannerEpisodicPivot.AverageDollarVolume = AverageDollarVolume;
      scannerEpisodicPivot.PercentageChange = PercentageChange;
    }

    protected override void CalcBar()
    {
      scannerEpisodicPivot.Call();

      if (Environment.CalcReason == CalculationReason.Default) 
      {
        if (scannerEpisodicPivot.Value)
          criteriaPlot.Set(1);
        else
          criteriaPlot.Set(0);
      }

    }

  }
}