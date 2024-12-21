using System;
using System.IO;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using PriceAction;
using System.Collections.Generic;
using FileHelpers;

namespace PowerLanguage.Indicator
{
  /// <summary>
  /// Generates training data for a neural network based on some criteria. Multiple strategies can be used to generate the data
  /// including strategies to actually manage a trade once the system is in a trade.
  /// </summary>
  /// 
  /// THINGS TO TRY:
  ///   - Price action with Volume
  ///   - Daily range and daily change as input parameters with volume
  /// 
  /// TODO:
  ///   - Output the data to a CNTK text format file as well, looks as follows with the "features" being the input and the "labels" the desired output:
  ///     |features 1 0 0 0 1 |labels 1 0 0 0 0 0
  ///     |features 1 0 0 0 1 |labels 1 0 0 0 0 0
  ///     |features 1 0 0 0 1 |labels 1 0 0 0 0 0
  ///     |features 1 0 0 0 1 |labels 1 0 0 0 0 0
  /// 
  ///   - There are instances where the output signal is generated for long and short directions this should not occur and needs to be corrected.
  ///   - The pip lengths typically flags long streaks of bars when the market is trending, find the first bar in such a streak and then only flag it, e.g. market moves
  ///     20-pips long then a lot of the bars would be flagged as 20-pips, then down to 15-pips, then 10-pips until bars are reached on the sub-10-pip level.
  ///     - Maybe resolve this by resetting all the long flags for the previous bar computed if the new bar also have to long set, that way the "long" signal would propagate its
  ///       way to the most extreme value that it could get.
  /// 
  [SameAsSymbol(true)]
  public class _GenerateNNTrainingData : IndicatorObject
  {
    //------------------------------------------------------------------------
    //CONSTANTS:
    public const int AVERAGE_LOOKBACK = 5;

    //------------------------------------------------------------------------
    //TYPES:

    /// <summary>
    /// Output structure used for neural network training.
    /// </summary>
    [IgnoreFirst(1), DelimitedRecord(",")]
    public class CSVData
    {
      //declare the header for the csv file
      public const string HEADER_TEXT = "timestamp," +
                                        "open,high,low,close,volume," +
                                        "bullish,bearish,engulfing,doji,highPin,lowPin,inside,outside,reversal,strong,breakout,small,large," +
                                        "volumeUp,volumeDown,volumeAboveAverage,volumeHigh,volumeVeryHigh,volumeUltraHigh,volumeBelowAverage,volumeLow,volumeVeryLow,volumeUltraLow," +
                                        "pivotHigh,pivotLow,lowerHigh,higherHigh,higherLow,lowerLow" + //,pivotTimestamp," +
                                        //"doubleTop,doubleTopTimestamp,doubleBottom,doubleBottomTimestamp," +
                                        "long1,long2,long3,short1,short2,short3";

      //INPUTS: hyperparameters for neural network
      [FieldConverter(ConverterKind.Date, "MMddyyyyHHmm")] public DateTime timestamp;
      public double open;
      public double high;
      public double low;
      public double close;
      public double volume;
      public int bullish;
      public int bearish;
      public int engulfing;
      public int doji;
      public int highPin;
      public int lowPin;
      public int inside;
      public int outside;
      public int reversal;
      public int strong;
      public int breakout;
      public int small;
      public int large;
      public int volumeUp;
      public int volumeDown;
      public int volumeAboveAverage;
      public int volumeHigh;
      public int volumeVeryHigh;
      public int volumeUltraHigh;
      public int volumeBelowAverage;
      public int volumeLow;
      public int volumeVeryLow;
      public int volumeUltraLow;

      public int pivotHigh;
      public int pivotLow;
      public int lowerHigh;
      public int higherHigh;
      public int higherLow;
      public int lowerLow;
      //[FieldConverter(ConverterKind.Date, "MMddyyyyHHmm")] public DateTime pivotTimestamp;    //timestamp for ALL of the above pivot point flags.

      //public int doubleTop;
      //[FieldConverter(ConverterKind.Date, "MMddyyyyHHmm")] public DateTime doubleTopTimestamp;
      //public int doubleBottom;
      //[FieldConverter(ConverterKind.Date, "MMddyyyyHHmm")] public DateTime doubleBottomTimestamp;

      //OUTPUTS: To be modelled by the neural network.
      public int long1;
      public int long2;
      public int long3;
      public int short1;
      public int short2;
      public int short3;

      public CSVData()
      {
        timestamp = DateTime.MinValue;
        //pivotTimestamp = DateTime.MinValue;
        //doubleTopTimestamp = DateTime.MinValue;
        //doubleBottomTimestamp = DateTime.MinValue;
      }

      //public bool IsPivot() {
      //  return (pivotHigh == 1 || pivotLow == 1);
      //}
    }

    /// <summary>
    /// CTF data used by the CNTK framework.
    /// </summary>
    public class CTFData
    {
      /// <summary>
      /// Copy down the basic attributes from a CSV data object.
      /// </summary>
      public CSVData BasicAttributes {
        set {
          timestamp = value.timestamp;
          open = value.open;
          high = value.high;
          low = value.low;
          close = value.close;
          volume = value.volume;
          bullish = value.bullish;
          bearish = value.bearish;
          engulfing = value.engulfing;
          doji = value.doji;
          highPin = value.highPin;
          lowPin = value.lowPin;
          inside = value.inside;
          outside = value.outside;
          reversal = value.reversal;
          strong = value.strong;
          breakout = value.breakout;
          small = value.small;
          large = value.large;
          volumeUp = value.volumeUp;
          volumeDown = value.volumeDown;
          volumeAboveAverage = value.volumeAboveAverage;
          volumeHigh = value.volumeHigh;
          volumeVeryHigh = value.volumeVeryHigh;
          volumeUltraHigh = value.volumeUltraHigh;
          volumeBelowAverage = value.volumeBelowAverage;
          volumeLow = value.volumeLow;
          volumeVeryLow = value.volumeVeryLow;
          volumeUltraLow = value.volumeUltraLow;

          long1 = value.long1;
          long2 = value.long2;
          long3 = value.long3;
          short1 = value.short1;
          short2 = value.short2;
          short3 = value.short3;
        }
      }

      public CSVData PivotAttributes {
        set {
          pivotHigh = value.pivotHigh;
          pivotLow = value.pivotLow;
          lowerHigh = value.lowerHigh;
          higherHigh = value.higherHigh;
          higherLow = value.higherLow;
          lowerLow = value.lowerLow;
        }
      }

      public DateTime timestamp;
      public double open;
      public double high;
      public double low;
      public double close;
      public double volume;
      public int bullish;
      public int bearish;
      public int engulfing;
      public int doji;
      public int highPin;
      public int lowPin;
      public int inside;
      public int outside;
      public int reversal;
      public int strong;
      public int breakout;
      public int small;
      public int large;
      public int volumeUp;
      public int volumeDown;
      public int volumeAboveAverage;
      public int volumeHigh;
      public int volumeVeryHigh;
      public int volumeUltraHigh;
      public int volumeBelowAverage;
      public int volumeLow;
      public int volumeVeryLow;
      public int volumeUltraLow;

      public int pivotHigh;
      public int pivotLow;
      public int lowerHigh;
      public int higherHigh;
      public int higherLow;
      public int lowerLow;
      //public int doubleTop;
      //public int doubleBottom;       

      public int long1;
      public int long2;
      public int long3;
      public int short1;
      public int short2;
      public int short3;

      public CTFData()
      {
        timestamp = DateTime.MinValue;
      }

      public override string ToString()
      {
        string result = "";

        //pack timestamp as a comment
        result += "|# ";
        result += timestamp.ToString("MMddyyyyHHmm") + " ";

        //pack all the price value components used as inputs
        result += "|inputs" + " ";

        //TODO: Floating point data needs to be scaled in the range of 0 to 1 for signmoid input gate or -1 to +1 for tanh input gate and can not be fed into the neural network "as-is".

        //result += open.ToString() + " ";
        //result += high.ToString() + " ";
        //result += low.ToString() + " ";
        //result += close.ToString() + " ";
        //result += volume.ToString() + " ";

        //pack all the price action components used as inputs        
        result += bullish.ToString() + " ";
        result += bearish.ToString() + " ";
        result += engulfing.ToString() + " ";
        result += doji.ToString() + " ";
        result += highPin.ToString() + " ";
        result += lowPin.ToString() + " ";
        result += inside.ToString() + " ";
        result += outside.ToString() + " ";
        result += reversal.ToString() + " ";
        result += strong.ToString() + " ";
        result += breakout.ToString() + " ";
        result += small.ToString() + " ";
        result += large.ToString() + " ";
        result += volumeUp.ToString() + " ";
        result += volumeDown.ToString() + " ";
        result += volumeAboveAverage.ToString() + " ";
        result += volumeHigh.ToString() + " ";
        result += volumeVeryHigh.ToString() + " ";
        result += volumeUltraHigh.ToString() + " ";
        result += volumeBelowAverage.ToString() + " ";
        result += volumeLow.ToString() + " ";
        result += volumeVeryLow.ToString() + " ";
        result += volumeUltraLow.ToString() + " ";

        result += pivotHigh.ToString() + " ";
        result += pivotLow.ToString() + " ";
        result += lowerHigh.ToString() + " ";
        result += higherHigh.ToString() + " ";
        result += higherLow.ToString() + " ";
        result += lowerLow.ToString() + " ";
        //result += doubleTop.ToString() + " ";
        //result += doubleBottom.ToString() + " ";

        //pack all the projected profits for outputs
        result += "|outputs ";
        result += long1.ToString() + " ";
        result += long2.ToString() + " ";
        result += long3.ToString() + " ";
        result += short1.ToString() + " ";
        result += short2.ToString() + " ";
        result += short3.ToString() + " ";

        return result;
      }
    }

    //------------------------------------------------------------------------
    //INPUTS:
    public _GenerateNNTrainingData(object _ctx) : base(_ctx) { }
    [Input] public bool Plot { get; set; }           //Load file previously produced and simply plot it.
    [Input] public string CSVFilename { get; set; }  //CSV file used for input/output of the neural net training data.
    [Input] public string CTFFilename { get; set; }  //CTF file used for output of the CNTK neural net training data.
    [Input] public int ProfitLevel1 { get; set; }    //First profit level to scan for.
    [Input] public int ProfitLevel2 { get; set; }    //Second profit level to scan for.
    [Input] public int ProfitLevel3 { get; set; }    //Third profit level to scan for.
    [Input] public double BufferSpread { get; set; } //Expected worst case spread to take into account for price moves.
    [Input] public int ATRLength { get; set; }       //ATR length used by price action.
    [Input] public int VolumeLength { get; set; }    //Length to compute volume averages.

    //------------------------------------------------------------------------
    //ATTRIBUTES:
    private SortedList<DateTime, CSVData> _CSVData;
    private PriceActionAnalysis _priceAction;
    private double _pipValue;
    private double _bufferSpread;
    private IPlotObject _long1;
    private IPlotObject _long2;
    private IPlotObject _long3;
    private IPlotObject _short1;
    private IPlotObject _short2;
    private IPlotObject _short3;

    //------------------------------------------------------------------------
    //METHODS:
    protected override void Create()
    {
      Plot = false;
      CSVFilename = "";
      CTFFilename = "";
      ProfitLevel1 = 10;
      ProfitLevel2 = 15;
      ProfitLevel3 = 20;
      BufferSpread = 1.5;
      ATRLength = 20;
      VolumeLength = 20;

      _long1 = AddPlot(new PlotAttributes("L1", EPlotShapes.Point, Color.FromArgb(0, 100, 0)));
      _long2 = AddPlot(new PlotAttributes("L2", EPlotShapes.Point, Color.FromArgb(0, 175, 0)));
      _long3 = AddPlot(new PlotAttributes("L3", EPlotShapes.Point, Color.FromArgb(0, 255, 0)));
      _short1 = AddPlot(new PlotAttributes("S1", EPlotShapes.Point, Color.FromArgb(100, 0, 0)));
      _short2 = AddPlot(new PlotAttributes("S2", EPlotShapes.Point, Color.FromArgb(175, 0, 0)));
      _short3 = AddPlot(new PlotAttributes("S3", EPlotShapes.Point, Color.FromArgb(255, 0, 0)));
    }

    protected override void StartCalc()
    {
      _CSVData = new SortedList<DateTime, CSVData>();

      //compute spread and pip values based on instrument price scale
      _pipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;
      _bufferSpread = BufferSpread * _pipValue;

      //check whether we need to produce training data or simply plot it
      if (!Plot)
      {
        //setup components used to compute the input/output parameters for the training data
        _priceAction = new PriceActionAnalysis(1);
        _priceAction.AtrLength = ATRLength;
      }
      else
      {
        FileHelperEngine<CSVData> engine = new FileHelperEngine<CSVData>();
        CSVData[] input = engine.ReadFile(CSVFilename);

        _CSVData.Clear();

        for (int i = 0; i < input.Count(); i++)
        {
          CSVData data = input[i];
          _CSVData.Add(data.timestamp, data);
        }
      }
    }
    protected override void CalcBar()
    {
      if (!Plot)
      {
        //add new entry in the file data for the bar being processed
        CSVData newTrainingBar = new CSVData();
        newTrainingBar.timestamp = Bars.Time[0];
        newTrainingBar.open = Bars.Open[0];
        newTrainingBar.high = Bars.High[0];
        newTrainingBar.low = Bars.Low[0];
        newTrainingBar.close = Bars.Close[0];
        newTrainingBar.volume = Bars.Volume[0];

        newTrainingBar.volumeUp = Bars.CurrentBar > 2 && Bars.Volume[0] > Bars.Volume[1] ? 1 : 0;
        newTrainingBar.volumeDown = Bars.CurrentBar > 2 && Bars.Volume[0] < Bars.Volume[1] ? 1 : 0;

        _CSVData.Add(Bars.Time[0], newTrainingBar);

        //allow the price action to analyze the bar
        if (_priceAction.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]))
        {
          //update the set of bars that were modified
          foreach (KeyValuePair<DateTime, PriceActionBar> bar in _priceAction.ChangedBars)
          {
            CSVData trainingBar = _CSVData[bar.Key];

            //set trivial price action flags
            trainingBar.bullish = bar.Value.state.bull ? 1 : 0;
            trainingBar.bearish = bar.Value.state.bear ? 1 : 0;
            trainingBar.engulfing = bar.Value.state.engulfing ? 1 : 0;
            trainingBar.doji = bar.Value.state.doji ? 1 : 0;
            trainingBar.highPin = bar.Value.state.highPin ? 1 : 0;
            trainingBar.lowPin = bar.Value.state.lowPin ? 1 : 0;
            trainingBar.inside = bar.Value.state.inside ? 1 : 0;
            trainingBar.outside = bar.Value.state.outside ? 1 : 0;
            trainingBar.reversal = bar.Value.state.reversal ? 1 : 0;
            trainingBar.strong = bar.Value.state.strong ? 1 : 0;
            trainingBar.breakout = bar.Value.state.breakout ? 1 : 0;
//            trainingBar.small = bar.Value.state.small ? 1 : 0;
//            trainingBar.large = bar.Value.state.large ? 1 : 0;
//            trainingBar.volumeAboveAverage = bar.Value.state.volumeAboveAverage ? 1 : 0;
//            trainingBar.volumeHigh = bar.Value.state.volumeHigh ? 1 : 0;
//            trainingBar.volumeVeryHigh = bar.Value.state.volumeVeryHigh ? 1 : 0;
//            trainingBar.volumeUltraHigh = bar.Value.state.volumeUltraHigh ? 1 : 0;
//            trainingBar.volumeBelowAverage = bar.Value.state.volumeBelowAverage ? 1 : 0;
//            trainingBar.volumeLow = bar.Value.state.volumeLow ? 1 : 0;
//            trainingBar.volumeVeryLow = bar.Value.state.volumeVeryLow ? 1 : 0;
//            trainingBar.volumeUltraLow = bar.Value.state.volumeUltraLow ? 1 : 0;

            //set pivot points and their associated timestamp when detected
            trainingBar.pivotHigh = bar.Value.state.pivotState[0].pivotHigh ? 1 : 0;
            trainingBar.pivotLow = bar.Value.state.pivotState[0].pivotLow ? 1 : 0;
            trainingBar.lowerHigh = bar.Value.state.lowerHigh ? 1 : 0;
            trainingBar.higherHigh = bar.Value.state.higherHigh ? 1 : 0;
            trainingBar.higherLow = bar.Value.state.higherLow ? 1 : 0;
            trainingBar.lowerLow = bar.Value.state.lowerLow ? 1 : 0;

            //if (trainingBar.IsPivot() && trainingBar.pivotTimestamp == DateTime.MinValue)
            //  trainingBar.pivotTimestamp = Bars.Time[0];

            ////set the double top/bottom with their associated timestamp when detected
            //trainingBar.doubleTop = bar.Value.state.doubleTop ? 1 : 0;
            //if (trainingBar.doubleTop == 1 && trainingBar.doubleTopTimestamp == DateTime.MinValue)
            //  trainingBar.doubleTopTimestamp = Bars.Time[0];

            //trainingBar.doubleBottom = bar.Value.state.doubleBottom ? 1 : 0;
            //if (trainingBar.doubleBottom == 1 && trainingBar.doubleBottomTimestamp == DateTime.MinValue)
            //  trainingBar.doubleBottomTimestamp = Bars.Time[0];

          }

          //check if we have a pivot low/high then scan for pip changes to update the output values for different bars
          if (_priceAction.AnalysisState.pivotLow)
          {
            //scan for potential shorts where the newly discovered pivot low would be the EXIT, so we need to find a bar that is higher than the current bar in it's high
            //price that would result in the desired pip move, the following conditions need to hold:
            // 1. A bar exists that is higher in price than the current pivot low bar by the required number of pips for a 10-, 15- or 20-pip bearish profit.
            // 2. The higher priced bar and the low pivot bar would allow a trade without the price moving against us, so price never moves beyond the high of the high priced bar and the low of the
            //    pivot low bar.
            double low = _priceAction.CurrentMin1.low;
            double delta1Pips = low + (ProfitLevel1 * _pipValue) + (2 * _bufferSpread);
            double delta2Pips = low + (ProfitLevel2 * _pipValue) + (2 * _bufferSpread);
            double delta3Pips = low + (ProfitLevel3 * _pipValue) + (2 * _bufferSpread);

            //reverse iterate the price action bars and flag bars that would result in different profit margins from the pivot low
            for (int i = _priceAction.Bars.Count - 1; i >= 0; i--)
            {
              PriceActionBar bar = _priceAction.Bars.Values[i];
              //see whether we need to terminate the loop because we found a bar that would result in a losing trade
              // APPLY: 2. The higher priced bar and the low pivot bar would allow a trade without the price moving against us, so price never moves beyond the high of the high priced bar and the low of the
              //           pivot low bar. NOTE: We currently apply only the break below the pivot low.
              if (bar.low < low)
                break;

              // APPLY: 1. A bar exists that is higher in price than the current pivot low bar by the required number of pips for a 10-, 15- or 20-pip bearish profit.
              //NOTE: We only allow one of the flags to be set on the bar per scan
              CSVData data = null;
              if (bar.high > delta3Pips)
              {
                data = _CSVData[bar.timestamp];
                data.short3 = 1;
                data.short2 = 0;
                data.short1 = 0;
                //terminate loop as soon as we can achieve a 20-pip profit
                break;
              }
              else if (bar.high > delta2Pips)
              {
                data = _CSVData[bar.timestamp];
                data.short2 = 1;
                data.short1 = 0;
              }
              else if (bar.high > delta1Pips)
              {
                data = _CSVData[bar.timestamp];
                data.short1 = 1;
              }
            }
          }
          else if (_priceAction.AnalysisState.pivotHigh)
          {
            //scan for potential longs where the newly discovered pivot high would be the EXIT, so we need to find a bar that is lower than the current bar in it's low
            //price that would result in the desired pip move, the following conditions need to hold:
            // 1. A bar exists that is lower in price than the current pivot high bar by the required number of pips for a 10-, 15- or 20-pip bullish profit.
            // 2. The lower priced bar and the high pivot bar would allow a trade without the price moving against us, so price never moves beyond the low of the low priced bar and the high of the  
            //    pivot high bar.
            double high = _priceAction.CurrentMin1.high;
            double delta1Pips = high - (ProfitLevel1 * _pipValue) - (2 * _bufferSpread);
            double delta2Pips = high - (ProfitLevel2 * _pipValue) - (2 * _bufferSpread);
            double delta3Pips = high - (ProfitLevel3 * _pipValue) - (2 * _bufferSpread);

            //reverse iterate the price action bars and flag bars that would result in different profit margins from the pivot high
            for (int i = _priceAction.Bars.Count - 1; i >= 0; i--)
            {
              PriceActionBar bar = _priceAction.Bars.Values[i];

              //see whether we need to terminate the loop because we found a bar that would result in a losing trade
              //APPLY: 2. The lower priced bar and the high pivot bar would allow a trade without the price moving against us, so price never moves beyond the low of the low priced bar and the high of the  
              //          pivot high bar. NOTE: We currently apply only the break above the pivot high.
              if (bar.high > high)
                break;

              // APPLY: 1. A bar exists that is lower in price than the current pivot high bar by the required number of pips for a 10-, 15- or 20-pip bullish profit.
              //check whether the bar would result in a 5-pip profit (including the buffer spread value)
              CSVData data = null;
              if (bar.low < delta3Pips)
              {
                data = _CSVData[bar.timestamp];
                data.long3 = 1;
                data.long2 = 0;
                data.long1 = 0;
                //terminate loop as soon as we can achieve a 20-pip profit
                break;
              }
              else if (bar.low < delta2Pips)
              {
                data = _CSVData[bar.timestamp];
                data.long2 = 1;
                data.long1 = 0;
              }
              else if (bar.low < delta1Pips)
              {
                data = _CSVData[bar.timestamp];
                data.long1 = 1;
              }
            }
          }
        }

        if (Bars.LastBarOnChart)
        {
          //write output file if this is the last bar on the chart
          FileHelperEngine<CSVData> engine = new FileHelperEngine<CSVData>();
          List<CSVData> csv_output = new List<CSVData>(_CSVData.Count);
          SortedList<DateTime, CTFData> ctf_output = new SortedList<DateTime, CTFData>(_CSVData.Count);

          //correct noisy bars that could have both long and short poistions set, we average the long/short signals over the bars
          //and correct a bar based on the predominant direction given by the average
          for (int i = 0; i < _CSVData.Count; i++)
          {
            //compute averages
            int avgCount = i >= AVERAGE_LOOKBACK ? AVERAGE_LOOKBACK : i;
            int longAvg = 0;
            int shortAvg = 0;
            CSVData bar = null;

            for (int avgIndex = 0; avgIndex < avgCount; avgIndex++)
            {
              int barIndex = i - avgIndex;
              bar = _CSVData.Values[barIndex];
              longAvg += bar.long1 + bar.long2 + bar.long3;
              shortAvg += bar.short1 + bar.short2 + bar.short3;
            }

            //get the bar we'll process for this iteration
            bar = _CSVData.Values[i];

            //update the output values for the bar long/short indicator
            int barDirectionSum = bar.long1 + bar.long2 + bar.long3 + bar.short1 + bar.short2 + bar.short3;
            if (barDirectionSum > 1)
            {
              //make sure long/short diretion is mutually exclusive
              if (longAvg > shortAvg)
              {
                //look back is predominantly long
                bar.short1 = 0;
                bar.short2 = 0;
                bar.short3 = 0;
              }
              else if (longAvg < shortAvg)
              {
                //look back is predominantly short
                bar.long1 = 0;
                bar.long2 = 0;
                bar.long3 = 0;
              }
              else
              {
                //look back is indeterminate, reset all directions to avoid noise
                bar.long1 = 0;
                bar.long2 = 0;
                bar.long3 = 0;
                bar.short1 = 0;
                bar.short2 = 0;
                bar.short3 = 0;
              }

              //set the highest pip profit for the bar in a specific direction to make sure we have
              //a one-hot output
              if (bar.long3 == 1)
              {
                bar.long2 = 0;
                bar.long1 = 0;
              }
              else if (bar.long2 == 1)
              {
                bar.long1 = 0;
              }

              if (bar.short3 == 1)
              {
                bar.short2 = 0;
                bar.short1 = 0;
              }
              else if (bar.short2 == 1)
              {
                bar.short1 = 0;
              }
            }
          }

          //displace the pivot flags to the correct bars where they will be detected, there is a TWO bar lag period to
          //detect the pivot point so we process the bars in reverse
          for (int i = _CSVData.Count - 1; i > 2; i--)
          {
            CSVData bar = _CSVData.Values[i];
            CSVData potentialPivotBar = _CSVData.Values[i - 2];
            if (potentialPivotBar.pivotHigh == 1)
            {
              bar.pivotHigh = potentialPivotBar.pivotHigh;
              bar.higherHigh = potentialPivotBar.higherHigh;
              bar.lowerHigh = potentialPivotBar.lowerHigh;
              potentialPivotBar.pivotHigh = 0;
              potentialPivotBar.higherHigh = 0;
              potentialPivotBar.lowerHigh = 0;
            }
            else if (potentialPivotBar.pivotLow == 1) 
            {
              bar.pivotLow = potentialPivotBar.pivotLow;
              bar.lowerLow = potentialPivotBar.lowerLow;
              bar.higherLow = potentialPivotBar.higherLow;
              potentialPivotBar.pivotLow = 0;
              potentialPivotBar.lowerLow = 0;
              potentialPivotBar.higherLow = 0;
            }
          }

          //split the CSV data up into the relevant output files
          CTFData ctf_data = null;
          foreach (KeyValuePair<DateTime, CSVData> data in _CSVData)
          {
            csv_output.Add(data.Value);

            ctf_data = new CTFData();
            ctf_data.BasicAttributes = data.Value;
            ctf_data.PivotAttributes = data.Value;
            ctf_output.Add(ctf_data.timestamp, ctf_data);
          }

          _CSVData.Clear();

          //output the CSV file data
          engine.HeaderText = CSVData.HEADER_TEXT;
          engine.WriteFile(CSVFilename, csv_output);

          //output the CTF file data
          using (StreamWriter file = new StreamWriter(CTFFilename))
          {
            foreach (KeyValuePair<DateTime, CTFData> data in ctf_output)
              file.WriteLine(data.Value.ToString());
            file.Close();
          }
        }
      }
      else
      {
        //try to find a bar in the loaded data and plot it on the chart
        CSVData data = null;
        if (_CSVData.TryGetValue(Bars.Time[0], out data))
        {
          if (data.long1 == 1)
            _long1.Set(0, Bars.Low[0]);
          if (data.long2 == 1)
            _long2.Set(0, Bars.Low[0]);
          if (data.long3 == 1)
            _long3.Set(0, Bars.Low[0]);

          if (data.short1 == 1)
            _short1.Set(0, Bars.High[0]);
          if (data.short2 == 1)
            _short2.Set(0, Bars.High[0]);
          if (data.short3 == 1)
            _short3.Set(0, Bars.High[0]);
        }
      }
    }
  }
}