using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using IronPython.Compiler.Ast;
using System.Windows.Forms;
using PowerLanguage.Function;
using PowerLanguage.TradeManager;
//using System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace PowerLanguage.Indicator
{
  public class _ScannerMomentum : IndicatorObject
  {
    //inputs
    [Input] public EScanBullishBearish ScanBullishBearish { get; set; }  //market direction to scan
    [Input] public double PercentageChange { get; set; }      //percentage increase/decrease in a stock price on the current/historic day - use for nano/micro/small/mid cap stocks
    [Input] public double DollarChange { get; set; }          //dollar increase/decrease in a stock price on the current/historic day - use for large/mega cap stocks
    [Input] public double PercentageCloseFactor { get; set; } //factor used to determine how far the price should close from the low/high to meet momentum criteria
    [Input] public int ConsecutiveDayFilter { get; set; }      //filter out stocks that are up/down by consecutive bars, must be less than this number
    [Input] public double MinimumPrice { get; set; }          //minimum price for stock
    [Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
    [Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
    [Input] public bool Historical { get; set; }              //allow historical scanning so if ever the criteria is met the scanner will output true to allow chart analysis
    [Input] public bool Debug { get; set; }

    //attributes
    protected Function._ScannerMomentum scannerMomentum;
    protected IPlotObject criteriaPlot;

    public _ScannerMomentum(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      //default inputs
      ScanBullishBearish = Function._ScannerMomentum.SCAN_BULLISH_BEARISH;
      PercentageChange = Function._ScannerMomentum.PERCENTAGE_CHANGE;
      DollarChange = Function._ScannerMomentum.DOLLAR_CHANGE;
      PercentageCloseFactor = Function._ScannerMomentum.PERCENTAGE_CLOSE_FACTOR;
      ConsecutiveDayFilter = Function._ScannerMomentum.CONSECUTIVE_DAY_FILTER;
      MinimumPrice = Function._ScannerMomentum.MINIMUM_PRICE;
      MinimumAverageVolume = Function._ScannerMomentum.MINIMUM_AVERAGE_VOLUME;
      AverageVolumeLength = Function._ScannerMomentum.AVERAGE_VOLUME_LENGTH;
      Historical = true;
      Debug = Function._ScannerMomentum.DEBUG;

      //create scanning function
      scannerMomentum = new Function._ScannerMomentum(this);
      criteriaPlot = AddPlot(new PlotAttributes("Criteria", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc()
    {
      // assign inputs 
      scannerMomentum.ScanBullishBearish = ScanBullishBearish;
      scannerMomentum.PercentageChange = PercentageChange;
      scannerMomentum.DollarChange = DollarChange;
      scannerMomentum.PercentageCloseFactor = PercentageCloseFactor;
      scannerMomentum.ConsecutiveDayFilter = ConsecutiveDayFilter;
      scannerMomentum.MinimumPrice = MinimumPrice;
      scannerMomentum.MinimumAverageVolume = MinimumAverageVolume;
      scannerMomentum.AverageVolumeLength = AverageVolumeLength;
      scannerMomentum.Historical = Historical;
      scannerMomentum.Debug = Debug;
    }

    protected override void CalcBar()
    {
      scannerMomentum.Call();

      if (Environment.CalcReason == CalculationReason.Default) 
      {
          if (scannerMomentum.Value)
            criteriaPlot.Set(1.0);
          else
            criteriaPlot.Set(0.0);
      }
    }
  }
}