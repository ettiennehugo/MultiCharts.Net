using System;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  /// <summary>
  /// TODO:
	///   * Try to get the basic scanning working with this indicator.
  ///   * Search the other indicators and see how you can make the indicator display nicer data in the scanner.
  ///   * See whether you can adjust this criteria strategy so it would work with both the scanner and the chart, that is, if it's a scanner it will raise the criteria signal and when it's a
	///     chart it will actually plot on the chart where the criteria is met.
  /// </summary>
  public class _ScannerPercentChangeOverTime : IndicatorObject
  {
    public _ScannerPercentChangeOverTime(object _ctx) : base(_ctx) { }

    //types
    public enum ScannerDebugLog {
      All,
      CriteriaMet,
    };

    //inputs
    [Input] public double PercentageChange { get; set; }      //percentage increase/decrease in a stock
    [Input] public int BarCount { get; set; }                 //number of bars to check for the specific increase/decrease
    [Input] public double MinimumPrice { get; set; }          //minimum price for stock
    [Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
    [Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
    [Input] public bool Historical { get; set; }              //allow historical scanning so if ever the criteria is met the scanner will output true to allow chart analysis
    [Input] public bool Debug { get; set; }
    [Input] public ScannerDebugLog DebugMessageToLog { get; set; }

    //properties
    public bool CriteriaMet { get { return criteriaMet; } }
    public bool PercentChangeCriteria { get { return percentChangeCriteria; } }
    public bool PriceLiquidityCriteria { get { return priceLiquidityCriteria; } }
    public int NextBarToCheckCriteria { get { return nextBarToCheckCriteria; } }

    //attributes
    protected IPlotObject criteriaPlot;
    protected bool percentChangeCriteria;
    protected bool priceLiquidityCriteria;
    protected bool criteriaMet;
    protected bool historicalCriteria;
    protected int nextBarToCheckCriteria;

    protected override void Create()
    {
      //default inputs
      Debug = false;
      DebugMessageToLog = ScannerDebugLog.CriteriaMet;
      Historical = false;   //assume real time use
      PercentageChange = 100;
      BarCount = 252;
      MinimumPrice = 10.0;
      MinimumAverageVolume = 100000;
      AverageVolumeLength = 252;

      //create plots
      criteriaPlot = AddPlot(new PlotAttributes("Criteria", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc()
    {
      //check inputs
      if (PercentageChange <= 0) throw new Exception("Percentage change must be positive.");
      if (BarCount <= 0) throw new Exception("Bar count lookback must be positive.");
      if (MinimumPrice <= 0) throw new Exception("Minimum price must be positive.");
      if (MinimumAverageVolume <= 0) throw new Exception("Minimum average volume must be positive.");
      if (AverageVolumeLength <= 0) throw new Exception("Average volume length must be positive.");

      //default attributes
      criteriaMet = false;
      percentChangeCriteria = false;
      priceLiquidityCriteria = false;
      historicalCriteria = false;
      nextBarToCheckCriteria = 0;

      if (Debug) Output.Clear();
    }

    protected override void CalcBar()
    {
      //we don't want to recheck consecutive blocks of matches, we only check the criteria on a sliding window of bars where the criteria is checked once it was met previously or on the last bar
      criteriaPlot.Set(0);
      criteriaMet = false;
      if (Bars.CurrentBar >= nextBarToCheckCriteria || Bars.LastBarOnChart) {
        percentChangeCriteria = PublicFunctions.DoubleGreater(PercentageChange, 0.0) ?
          Bars.CurrentBar >= BarCount && !PublicFunctions.DoubleEquals(Bars.Close[BarCount - 1], 0.0) && PublicFunctions.DoubleGreater((Bars.Close[0] / Bars.Close[BarCount - 1]) - 1, PercentageChange / 100.0) :
          Bars.CurrentBar >= BarCount && !PublicFunctions.DoubleEquals(Bars.Close[BarCount - 1], 0.0) && PublicFunctions.DoubleLess((Bars.Close[0] / Bars.Close[BarCount - 1]) - 1, PercentageChange / 100.0);
        priceLiquidityCriteria = Bars.CurrentBar >= AverageVolumeLength && PublicFunctions.DoubleGreaterEquals(Bars.Close[0], MinimumPrice) && PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(AverageVolumeLength), MinimumAverageVolume);
        criteriaMet = percentChangeCriteria && priceLiquidityCriteria;
        historicalCriteria = Historical && !historicalCriteria ? criteriaMet : historicalCriteria;
        if (percentChangeCriteria && priceLiquidityCriteria) 
          nextBarToCheckCriteria = Bars.CurrentBar + BarCount;  //only check the criteria again once window has moved forward to we get consecutive moving blocks where criteria is met

        //log debug output
        if (Debug)
          if (DebugMessageToLog == ScannerDebugLog.All)
            Output.WriteLine("{0} - percentage change({1}), liquidity({2})", Bars.Time[0], percentChangeCriteria, priceLiquidityCriteria);
          else if (DebugMessageToLog == ScannerDebugLog.CriteriaMet && percentChangeCriteria && priceLiquidityCriteria)
            Output.WriteLine("{0} - percentage change({1}), liquidity({2})", Bars.Time[0], percentChangeCriteria, priceLiquidityCriteria);

        //calculate indicator value
        switch (Environment.CalcReason)
        {
          case CalculationReason.Default:
            if (percentChangeCriteria && priceLiquidityCriteria)
            {
              criteriaPlot.Set(1);
            }

            if (historicalCriteria && Bars.LastBarOnChart) criteriaPlot.Set(1);
            break;
        }
      }

    }
  }
}
