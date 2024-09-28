using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Microsoft.Scripting.Debugging.CompilerServices;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  public class _ScannerLowThresholdBreakout : IndicatorObject
  {
    //inputs
    [Input] public EScanBullishBearish ScanBullishBearish { get; set; }  //market direction to scan
    [Input] public int TradeIntensityShortPeriod { get; set; }  //short period for the trade intensity filter
    [Input] public int TradeIntensityLongPeriod { get; set; }  //short period for the trade intensity filter
    [Input] public double TradeIntensityPercent { get; set; } //percentage for trade intensity
    [Input] public double PreviousDayIncreasePercent { get; set; }  //percentage increase for pervious trading day to limit how much price can move before the low threshold detection
    [Input] public double MinimumPrice { get; set; }          //minimum price for stock
    [Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
    [Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
    [Input] public bool Historical { get; set; }              //allow historical scanning so if ever the criteria is met the scanner will output true to allow chart analysis
    [Input] public bool Debug { get; set; }

    //attributes
    protected IPlotObject criteriaPlot;
    protected Function._ScannerLowThresholdBreakout scannerLowThresholdBreakout;

    public _ScannerLowThresholdBreakout(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ScanBullishBearish = Function._ScannerLowThresholdBreakout.SCAN_BULLISH_BEARISH;
      TradeIntensityShortPeriod = Function._ScannerLowThresholdBreakout.TRADE_INTENSITY_SHORT_PERIOD;
      TradeIntensityLongPeriod = Function._ScannerLowThresholdBreakout.TRADE_INTENSITY_LONG_PERIOD;
      TradeIntensityPercent = Function._ScannerLowThresholdBreakout.TRADE_INTENSITY_PERCENT;
      PreviousDayIncreasePercent = Function._ScannerLowThresholdBreakout.PREVIOUS_DAY_INCREASE_PERCENT;
      MinimumPrice = Function._ScannerLowThresholdBreakout.MINIMUM_PRICE;
      MinimumAverageVolume = Function._ScannerLowThresholdBreakout.MINIMUM_AVERAGE_VOLUME;
      AverageVolumeLength = Function._ScannerLowThresholdBreakout.AVERAGE_VOLUME_LENGTH;
      Historical = Function._ScannerLowThresholdBreakout.HISTORICAL;
      Debug = Function._ScannerLowThresholdBreakout.DEBUG;

      scannerLowThresholdBreakout = new Function._ScannerLowThresholdBreakout(this);
      criteriaPlot = AddPlot(new PlotAttributes("Criteria", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc()
    {
      scannerLowThresholdBreakout.TradeIntensityShortPeriod = TradeIntensityShortPeriod;
      scannerLowThresholdBreakout.TradeIntensityLongPeriod = TradeIntensityLongPeriod;
      scannerLowThresholdBreakout.PreviousDayIncreasePercent = PreviousDayIncreasePercent;
      scannerLowThresholdBreakout.MinimumPrice = MinimumPrice;
      scannerLowThresholdBreakout.MinimumAverageVolume = MinimumAverageVolume;
      scannerLowThresholdBreakout.AverageVolumeLength = AverageVolumeLength;
      scannerLowThresholdBreakout.Historical = Historical;
      scannerLowThresholdBreakout.Debug = Debug;
    }

    protected override void CalcBar()
    {
      if (scannerLowThresholdBreakout.Value)
        criteriaPlot.Set(1);
      else 
        criteriaPlot.Set(0);
    }
  }
}