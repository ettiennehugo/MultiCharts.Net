using PowerLanguage.Indicator;
using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
    /// <summary>
    /// Rules:
    ///   * Up/down move of 4% (includes short positions) - Dollar scan can be used to scan large/mega cap stocks for moves.
    ///   * Volume today > Volume yesterday
    ///   * Volume > 100 000
    ///   * Candle closes close to it's high (for bullish stocks) and low (for bearish stocks)
    ///   * Ignore extended breakouts (will most likely pullback) - Bullish - candles are not up for a number of consecutive days/Bearish - candles are not down for a number of consecutive days.
    ///   * Days before the breakout is a low range day - small range, can be positive or negative.
    ///   * TBD See how you can measure this: Check for orderly moves where there's smooth buying and selling leading up/down to the breakout - not gaps and price movement all over the place, it moves up/down and then consolidates before the breakout.
    ///	
    ///   TODO - complete the momentum criteria. https://www.youtube.com/watch?v=1EZ1R9nK77Y&t=88s
    ///     * See whether you can implement a low-threshold scan as well in this since it will typically return stocks in this list.
    ///     * Low threshold breakout scanner rules
    ///       - Minimum volume over last 3-days > 100000 - liquidity filter on volume
    ///       - Close > $1 - liquidity filter on price
    ///       - Avg.Close last 7 days / Avg.Close last 65 days > 1.05 (105% up)  - Pradeep calls this a trade intensity filter (or TI65 since it's based off 65 day long term), it essentially checks that the short term recent closes are stronger than the more medium term closes
    ///       - Close > Open - bullish candle
    ///       - Close / Close one day back > Close one day back / Close two days back
    ///       - Close one day back / Close two days back < 1.02 (102%) - we did not see a sudden price move
    /// </summary>
    public sealed class _ScannerMomentum : FunctionSimple<System.Boolean>
		{
      //constants
      public const EScanBullishBearish SCAN_BULLISH_BEARISH = EScanBullishBearish.BullishAndBearish;
      public const double PERCENTAGE_CHANGE = 4.0;
      public const double DOLLAR_CHANGE = 0.0;
      public const double PERCENTAGE_CLOSE_FACTOR = 5.0;
      public const int CONSECUTIVE_DAY_FILTER = 3;
      public const double MINIMUM_PRICE = 10.0;
      public const double MINIMUM_AVERAGE_VOLUME = 100000;
      public const int AVERAGE_VOLUME_LENGTH = 126;
      public const bool HISTORICAL = false;
      public const bool DEBUG = false;

      //input
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

      //read-only properties
      public bool CriteriaMet { get { return m_criteriaMet; } }
      public bool MomentumCriteria { get { return m_momentumCriteriaMet; } }
      public bool MinimumPriceCriteria { get { return m_minimumPriceCriteria; } }
      public bool VolumeCriteria { get { return VolumeCriteria; } }
      public bool PriceLiquidityCriteria { get { return m_priceLiquidityCriteria; } }
      public ECurrentBarDirection CurrentBarDirection { get { return m_currentBarDirection; } }

      //attributes
      private bool m_criteriaMet;
      private bool m_momentumCriteriaMet;
      private bool m_volumeCriteria;
      private bool m_minimumPriceCriteria;
      private bool m_priceLiquidityCriteria;
      private bool m_historicalCriteria;
      private ECurrentBarDirection m_currentBarDirection;
      private double m_percentageChange;
      private double m_percentageCloseFactor;
      private int m_firstBarToCheck;

      public _ScannerMomentum(CStudyControl _master) : base(_master) { }
			public _ScannerMomentum(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
        //default inputs
        Debug = DEBUG;
        ScanBullishBearish = SCAN_BULLISH_BEARISH;
        PercentageChange = PERCENTAGE_CHANGE;
        DollarChange = DOLLAR_CHANGE;
        PercentageCloseFactor = PERCENTAGE_CLOSE_FACTOR;
        ConsecutiveDayFilter = CONSECUTIVE_DAY_FILTER;
        MinimumPrice = MINIMUM_PRICE;
        MinimumAverageVolume = MINIMUM_AVERAGE_VOLUME;
        AverageVolumeLength = AVERAGE_VOLUME_LENGTH;
      }

      protected override void StartCalc()
			{
        //validate inputs
        if (PercentageChange < 0) throw new Exception("Percentage change must be positive or zero.");
        if (DollarChange < 0) throw new Exception("Dollar change must be positive or zero.");
        if (MinimumPrice <= 0) throw new Exception("Minimum price must be positive.");
        if (MinimumAverageVolume <= 0) throw new Exception("Minimum average volume must be positive.");
        if (ConsecutiveDayFilter <= 0) throw new Exception("Consecutive day filter must be positive.");
        if (AverageVolumeLength <= 0) throw new Exception("Average volume length must be positive.");

        //default get properties
        m_criteriaMet = false;
        m_momentumCriteriaMet = false;
        m_minimumPriceCriteria = false;
        m_volumeCriteria = false;
        m_priceLiquidityCriteria = false;
        m_historicalCriteria = false;
        m_currentBarDirection = ECurrentBarDirection.None;

        //compute general values based off inputs
        m_percentageCloseFactor = PercentageCloseFactor / 100;
        m_percentageChange = PercentageChange / 100;

        //determine the first bar to check
        m_firstBarToCheck = ConsecutiveDayFilter >= AverageVolumeLength ? ConsecutiveDayFilter : AverageVolumeLength;
      }

      protected override System.Boolean CalcBar()
			{
        //compute the momentum - we either compute the momentum just on the lastbar or all the bars if historical computation is needed
        if (Historical || Bars.LastBarOnChart)
        {
          //default criteria
          m_criteriaMet = false;
          m_momentumCriteriaMet = false;
          m_volumeCriteria = false;
          m_minimumPriceCriteria = false;
          m_priceLiquidityCriteria = false;
          m_currentBarDirection = ECurrentBarDirection.None;

          if (Bars.CurrentBar > m_firstBarToCheck && Environment.CalcReason == CalculationReason.Default)
          {
            double barRange = Bars.High[0] - Bars.Low[0];
            bool strongBullishCloseCriteria = false;
            bool strongBearishCloseCriteria = false;
            bool percentageChangeCriteria = false;
            bool dollarChangeCriteria = false;
            bool consecutiveBarCriteria = false;    //consecutive bullish/bearish bars
            double currentBarPercentageChange = (Bars.Close[0] / Bars.Close[1]) - 1;
            double dollarChange = Bars.Close[0] - Bars.Close[1];
            m_minimumPriceCriteria = PublicFunctions.DoubleGreater(Bars.Close[0], MinimumPrice);
            m_volumeCriteria = Bars.Volume[0] > Bars.Volume[1] && PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(AverageVolumeLength), MinimumAverageVolume);

            //determine the momentum criteria
            if (ScanBullishBearish == EScanBullishBearish.BullishAndBearish || ScanBullishBearish == EScanBullishBearish.Bullish)
            {
              strongBullishCloseCriteria = PublicFunctions.DoubleGreaterEquals(barRange * m_percentageCloseFactor, Bars.High[0] - Bars.Close[0]);
              percentageChangeCriteria = !PublicFunctions.DoubleEquals(PercentageChange, 0.0) && PublicFunctions.DoubleGreaterEquals(currentBarPercentageChange, m_percentageChange);
              dollarChangeCriteria = !PublicFunctions.DoubleEquals(DollarChange, 0.0) && PublicFunctions.DoubleGreaterEquals(dollarChange, DollarChange);

              int consecutiveBarCount = 0;
              for (int index = ConsecutiveDayFilter; index >= 0; index--)
                if (Bars.Close[index] > Bars.Open[index]) consecutiveBarCount++;
              consecutiveBarCriteria = consecutiveBarCount < ConsecutiveDayFilter;
              
              m_momentumCriteriaMet = (percentageChangeCriteria || dollarChangeCriteria) && strongBullishCloseCriteria && consecutiveBarCriteria;

              if (m_momentumCriteriaMet) m_currentBarDirection = ECurrentBarDirection.Bullish;
            }

            //NOTE: The function would tend to be baised to detecting bullish momentum.
            if ((ScanBullishBearish == EScanBullishBearish.BullishAndBearish && !m_momentumCriteriaMet) || ScanBullishBearish == EScanBullishBearish.Bearish)
            {
              strongBearishCloseCriteria = PublicFunctions.DoubleGreaterEquals(barRange * m_percentageCloseFactor, Bars.Close[0] - Bars.Low[0]);
              percentageChangeCriteria = !PublicFunctions.DoubleEquals(PercentageChange, 0.0) && PublicFunctions.DoubleLessEquals(currentBarPercentageChange, -m_percentageChange);
              dollarChangeCriteria = !PublicFunctions.DoubleEquals(DollarChange, 0.0) && PublicFunctions.DoubleLessEquals(dollarChange, -DollarChange);

              int consecutiveBarCount = 0;
              for (int index = ConsecutiveDayFilter; index >= 0; index--)
                if (Bars.Close[index] < Bars.Open[index]) consecutiveBarCount++;
              consecutiveBarCriteria = consecutiveBarCount < ConsecutiveDayFilter;

              m_momentumCriteriaMet = (percentageChangeCriteria || dollarChangeCriteria) && strongBearishCloseCriteria && consecutiveBarCriteria;

              if (m_momentumCriteriaMet) m_currentBarDirection = ECurrentBarDirection.Bearish;
            }

            //determine general criteria and historical criteria
            m_criteriaMet = m_momentumCriteriaMet && m_volumeCriteria && m_minimumPriceCriteria;

            //output debug information
            if (Debug)
              Output.WriteLine("Index({0}) DateTime({1}) - criteria({2}) momentumCriteria({3}) volumeCriteria({4}) minimumPrice({5}) percentageChange({6}) dollarChange({7}) consecutiveDays({8}) strongBullishCloseCriteria({9}) strongBearishCloseCriteria({10})", Bars.CurrentBar, Bars.Time[0], m_criteriaMet, m_momentumCriteriaMet, m_volumeCriteria, m_minimumPriceCriteria, percentageChangeCriteria, dollarChangeCriteria, consecutiveBarCriteria, strongBullishCloseCriteria, strongBearishCloseCriteria);
          }
        }

        //determine historical criteria
        m_historicalCriteria = Historical && !m_historicalCriteria ? m_criteriaMet : m_historicalCriteria;

        return m_historicalCriteria;
      }
    }
	}
}