using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;
using PowerLanguage.VolumeProfile;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  /// Imbalances between the short term and longer term price-at-value would cause the price to revert to the longer term price-at-value average.
  /// 
  /// Ideal Instruments and Timeframes:
  /// Easier instruments to trade are USD/CAD, AUD/USD and GBP/USD (once Brexit is done).
  /// 
  /// Indicators: 
  /// Price action and volume
  /// 
  /// Entry Rules - Long:
  /// 
  /// 
  /// Entry Rules - Short:
  /// 
  /// 
  /// Exit Rules - Long:
  /// 
  /// 
  /// Exit Rules - Short:
  /// 
  /// 
  /// Money Management:
  /// 	Fixed ratio position sizing based on profits.
  /// 
  /// TODO:
  ///   - Use two sets of data based on the same timeframe with different volume profile setups, as soon as the shorter POC differs more than say 10-pips with the longer volume analysis POC and a price
  ///     action reversal occurs then enter a trade for 10-pips profit with a stop-loss of say 5-pips.
  ///   - IMPORTANT: Use price action with volume analysis in conjunction with the volume profile at specific price levels to determine whether a reversal is going to take place. 
  ///     
  ///   
  ///   - Implement alerts for the volume with SD indicator to notify you when average, avg + SD, avg + 2SD, avg + 3SD is broken.
  ///   - Try to at least build an indicator that can alert you when there is a confluence between the price movement between different timeframes, e.g M5, H1 and D when price is starting to get close to support/resistance levels.
  ///   - Implement a volume strategy that uses multiple price-at-volume levels on a small timeframe, compute the center of "gravity" between them and then enter trades when the market starts to diviate far away from this center point
  ///     of gravity. Can potentially first implement an indicator for this to be able to see the center of gravity between then price levels. - DOES NOT WORK!!! When the price is trending is results in losing trades.
  /// 
  /// Notes:
  ///   Couple of things to try/look at:
  ///     - Setup the volume profile and volume with SD to reflect a longer timeframe. The price would tend to swing toward the POC and other high volume areas where the price would be considered to be reasonable.
  ///       E.g. say you're trading the 5-minute timeframe you could setup your volume-at-price profile to spread accross the last 1-hour, 4-hours or day.
  ///       - Watch out for large movements in the market like economic events.
  ///       - Check for candles at extremes with low volume-at-price where the asset would be overpriced or underpriced, it will most likely move towards the POC.
  ///       - Check for drop in volume as the price reaches extremes and for candles with long wicks indicating movement in the opposite direction with higher volume to support the signal.
  ///       - You might have to check the previous couple of volume profiles as well to check where areas of large volume-at-price were this would be especially important if the new volume at price profile is just starting out.
  ///         This might be an input parameter, e.g. look back over the last 5 volume-at-price profiles.
  ///     - In general you have to build some measurement tool to guage the sentiment in the market to determine when to enter a trade with a specific risk and where to get out of the market.
  ///       - Don't look at specific candles or even candle patterns while it can be useful a single candle does not tell the whole story but only contributes to it.
  ///       - On intraday 1-hour charts you have to compare volume within sessions, e.g. Asia session is lesser traded than the London session - maybe normalize the volume in some way so that volume even
  ///         over sessions can be compared.
  ///       - It might be useful to look at the economic calendar and trade around the economic events to look for trends and reversals OR you can trade in between the economic events looking for range bound markets.
  ///         - Market makers usually buy/sell before the releases to position themselves for news, see Forex VPA p.133-134. Small candles moving on rising volume indicate market makers positioning themselves.
  ///       
  ///     - Large candles with high volume followed by smaller candles with similar volume typically indicates that the market is going to reverse, see Forex PVA p. 115 - 116. Similar volume must cause similar
  ///       price moves.
  ///     - Look for price levels where the candle spreads are small (below average) with high volume, this indicates buy/sell zones.
  ///     - Look for large candles with RELATIVELY LOW volume, this is typically market makers pushing the market in a direction only to reverse direction in order to trap traders in losing positions. 
  ///       - You need to look at the candle spread and volume, the effort must match the move, a large up candle can have above average volume BUT the volume might still be too small to merit the move!!!
  ///     - Look for small candles with high volume, this would signal that market tension is building that might result in a strong breakout as the market is wound up and then that tension
  ///       is released.
  ///       - Additionally, the direction in which the candles closes might give an indication of the most likely direction the market will take, e.g. bullish doji signals a bullish move to come.
  ///     - Reversals:
  ///       - Rising volume with a failure to move the market is a good sign of a reversal in the opposite direction.
  ///       - Movement of the market with the candle spreads narrowing and the volume drying up quickly is also a sign of a market that is soon going to reverse.
  ///     - Consider computing a metric in order to measure how much movement should be expected per unit volume, this should enable you to quickly identify candles that are anomalous. Maybe something
  ///       along the lines of VWAP but try more advanced interpretation.
  ///       - It's important to compare volume on intraday timeframes with volume within specific sessions, e.g. volume in the London session and USA session would be much deeper than volume in the
  ///         Far East/Asia session.
  ///         - For intraday trading you might have to implement time filters to or objects to hold the data specific to the session or maybe even the overlaps between sessions.
  ///       - Compare volume and movement on subsequent candles, similar volume should result in similar price movements, small spread candles with high volume is typically a sign that the market
  ///         might reverse.
  ///       - Movement in a specific direction with falling volume is a strong indication that the market is going to reverse, e.g. bearish candles with falling volume indicates bullish movement ahead.
  ///       - IMPORTANT: See p. 49 in Forex Volume Price Analysis, you must compare similar candles with each other in terms of spread and then compare the volume of these candles to determine whether
  ///         there are candles with price movement that are anomalous.
  ///     - Might be best to implement analysis as a set of heuristics, the more heuristics that are met the higher the likelihood of a successfull trade. Potential heuristics to build in:
  ///       - Entry: Look for candles that indicate reversals like small spread candles on high volume indicating exhaustion of a move up or down.
  ///       - Entry: Large candles with high volume on news events, will need economic calendar on this, fade the price levels that were significant previously and use a specific risk to exit - see M15 chart in Forex VPA p.105.
  ///       - Entry: Breakout of support levels with high volume on the breakout candles - see M15 chart of Forex VPA p.105 
  ///       - Entry: Attempt for market to move higher or lower by starting on high volume but the subsequent candles have smaller spread and volume quickly falls off, enter in the opposite direction of the attempted move.
  ///       - Continuation/Entry: Market consolidates after a trend on a price level but the candle spreads are small and the volume is also below average, this typically indicates that a further move down can be expected, see Forex VPA p.105
  ///       - Continuation/Entry: Candle with large volume and large high or low wick, this is typically an attempt to rally that failed indicating the market is most likely going to move lower/higher. 
  ///     
  ///     
  /// </summary>
  public class _VolumePriceAnalysis1 : SignalObject {
    //Inputs:

    //TODO: Define additional inputs for the strategy.
    [Input] public int DeltaPricePips { get; set; }             //Delta between price-at-value at which the strategy would start looking for entries.
    [Input] public int PriceActionFractalLayers { get; set; }   //Fractal layers to instantiate for price action analysis.
    //[Input] public int VolumeLength { get; set; }               //Length for volume average and standard deviation computation
    [Input] public int StopLossBufferPips { get; set; }                 //buffer pips for the stop-loss
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMPercentRiskPerTrade { get; set; }   //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; }       //default trade size for static position sizing
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int DEFAULT_DELTA_PRICE_PIPS = 10;
    const int DEFAULT_FRACTAL_LAYERS = 1;
    const int DEFAULT_VOLUME_LENGTH = 20;
    const int DEFAULT_STOP_LOSS_BUFFER_PIPS = 1;
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Attributes:		
    private double _PipValue;
    private double _DeltaPriceValue;
    private double _StopLossPrice;
    private double _TakeProfitPrice;
    private double _Risk;
    private bool _CalculateActualRisk;
    private IInstrument _ShortTermBars;
    private IInstrument _LongTermBars;
    private PriceActionAnalysis _ShortTermPriceAction;
    private PriceActionAnalysis _LongTermPriceAction;
    private IProfilesCollection _ShortTermVolumeProfiles;
    private IProfilesCollection _LongTermVolumeProfiles;
    private double _ShortTermPOC;
    private double _LongTermPOC;
    private IOrderMarket _LE;
    private IOrderMarket _SE;
    private IOrderPriced _LXSL;
    private IOrderPriced _SXSL;
    private IOrderPriced _LXTP;
    private IOrderPriced _SXTP;
    private RiskManager _RiskManager;

    public _VolumePriceAnalysis1(object _ctx) : base(_ctx) { }

    protected override void Create() {
      //initialize additional inputs for strategy
      DeltaPricePips = DEFAULT_DELTA_PRICE_PIPS;
      PriceActionFractalLayers = DEFAULT_FRACTAL_LAYERS;
      //VolumeLength = DEFAULT_VOLUME_LENGTH;
      StopLossBufferPips = DEFAULT_STOP_LOSS_BUFFER_PIPS;
      MMStartNetProfit = 0.0;
      MMStaticPositionSize = 100000;
      MMStartPosSize = 0;
      MMPercentRiskPerTrade = 1.0;
      MMProfitDelta = 0.0;
      MMStartNetProfit = 0.0;

      //initialize default inputs for strategy
      Debug = false;

      //create orders used
      _LE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _SE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      _LXSL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LXSL", EOrderAction.Sell));
      _SXSL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SXSL", EOrderAction.BuyToCover));
      _LXTP = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LXTP", EOrderAction.Sell));
      _SXTP = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "SXTP", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;

      //initialize the risk manager
      _RiskManager = new RiskManager(this);
      _RiskManager.RiskPercentage = MMPercentRiskPerTrade;

      if (MMStartPosSize > 0 && MMProfitDelta > 0) {
        FixedRatioSizing fixedRatioSizing = new FixedRatioSizing(this);
        fixedRatioSizing.StartNetProfit = MMStartPosSize;
        fixedRatioSizing.BasePositionSize = MMStartPosSize;
        fixedRatioSizing.ProfitDelta = MMProfitDelta;
        _RiskManager.PositionSizing = fixedRatioSizing;
      } else {
        StaticSizing staticSizing = new StaticSizing(this);
        staticSizing.PositionSize = MMStaticPositionSize;
        _RiskManager.PositionSizing = staticSizing;
      }

      //initialize indicators and objects used
      _ShortTermBars = BarsOfData(1);
      _LongTermBars = BarsOfData(2);
      _ShortTermVolumeProfiles = VolumeProfileByDataStream(1);
      _LongTermVolumeProfiles = VolumeProfileByDataStream(2);
      _ShortTermPriceAction = new PriceActionAnalysis(PriceActionFractalLayers);
      _LongTermPriceAction = new PriceActionAnalysis(PriceActionFractalLayers);
      //_ShortTermPriceAction.VolumeLength = VolumeLength;
      //_LongTermPriceAction.VolumeLength = VolumeLength;
      _ShortTermPOC = 0.0;
      _LongTermPOC = 0.0;
      _DeltaPriceValue = DeltaPricePips * _PipValue;
    }

    //protected override void StopCalc() {
    //	//cleanup/deinitialize strategy components
    //	
    //}

    protected override void CalcBar() {
      //update the price action analysis
      if (_ShortTermBars.Status == EBarState.Close)
        _ShortTermPriceAction.Analyse(_ShortTermBars.Time[0], _ShortTermBars.Open[0], _ShortTermBars.High[0], _ShortTermBars.Low[0], _ShortTermBars.Close[0], _ShortTermBars.Volume[0]);

      if (_LongTermBars.Status == EBarState.Close)
        _LongTermPriceAction.Analyse(_LongTermBars.Time[0], _LongTermBars.Open[0], _LongTermBars.High[0], _LongTermBars.Low[0], _LongTermBars.Close[0], _LongTermBars.Volume[0]);

      //analyse the entry/exit
      switch (Environment.CalcReason) {
        case CalculationReason.Default:
          CalcBarDefault();
          break;
        case CalculationReason.OrderFilled:
          CalcBarOrderFilled();
          break;
      }
    }

    private void CalcBarOrderFilled() {
      if (PositionSide > 0) {
        _LXSL.Send(_StopLossPrice);
        _LXTP.Send(_TakeProfitPrice);
      }

      if (PositionSide < 0) {
        _SXSL.Send(_StopLossPrice);
        _SXTP.Send(_TakeProfitPrice);
      }
    }

    private void CalcBarDefault() {
      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void calcBarEntry() {
      //reset stop-loss and risk values
      _StopLossPrice = 0.0;
      _TakeProfitPrice = 0.0;
      _Risk = 0.0;
      _CalculateActualRisk = false;

      //determine the current delta between the two price feeds
      //NOTE: The volume profile is null when volume profile is being computed and POC is null if it was not yet established.
      IProfile shortTermVolumeProfile = _ShortTermVolumeProfiles.ItemForBar(_ShortTermBars.CurrentBar);
      IProfile longTermVolumeProfile = _LongTermVolumeProfiles.ItemForBar(_LongTermBars.CurrentBar);
      double currentDelta = 0.0;

      if (shortTermVolumeProfile != null && !shortTermVolumeProfile.Empty &&
          longTermVolumeProfile != null && !longTermVolumeProfile.Empty) {
        Price shortTermPOCPrice = shortTermVolumeProfile.POCForBar(_ShortTermBars.CurrentBar);
        Price longTermPOCPrice = longTermVolumeProfile.POCForBar(_LongTermBars.CurrentBar);

        if (shortTermPOCPrice != null && longTermPOCPrice != null) {
          _ShortTermPOC = shortTermPOCPrice.Dbl;
          _LongTermPOC = longTermPOCPrice.Dbl;
          currentDelta = _ShortTermPOC - _LongTermPOC;
        }
      }



      ////DEBUG: Log current delta and whether position signal will be processed.
      //if (EnableLong && currentDelta < -_DeltaPriceValue)
      //  Output.WriteLine("LONG {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta);
      //else if (EnableShort && currentDelta > _DeltaPriceValue)
      //  Output.WriteLine("SHORT {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta);
      //else {
      //  double shortVPPOC = shortTermVolumeProfile != null && shortTermVolumeProfile.POC != null ? shortTermVolumeProfile.POC.Price.Dbl : 0.0;
      //  double longVPPOC = longTermVolumeProfile != null && longTermVolumeProfile.POC != null ? longTermVolumeProfile.POC.Price.Dbl : 0.0;
      //  Output.WriteLine("{0} CurrentDelta({1}) ShortPOC({2}) LongPOC({3})", _ShortTermBars.Time[0], currentDelta, shortVPPOC, longVPPOC);
      //}



      //enter long trades if we have a large deviation from the longer term POC
      if (currentDelta < -_DeltaPriceValue) {
        //Implement entry when reversal patterns occur on the following patterns:
        // - Doji with high volume at top.
        // - Shooting star pattern with high volume.
        // - Higher low
        // - Double bottom
        bool enterLong = false;
        PriceActionAnalysisState state = _ShortTermPriceAction.AnalysisState;
        string entryTrigger = "";

        //doji with high volume
        if (!enterLong && state.doji) {
          PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
          enterLong = currentBar.state.doji; // && currentBar.state.volumeHigh;

          if (enterLong) {
            _StopLossPrice = currentBar.low - StopLossBufferPips * _PipValue;
            _TakeProfitPrice = _LongTermPOC;
            _Risk = currentBar.close - _StopLossPrice;
            entryTrigger = "doji";
          }
        }

        ////shooting star pattern with high volume
        //if (!enterLong && state.lowPin) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  enterLong = currentBar.state.lowPin && currentBar.state.volumeHigh;

        //  if (enterLong) {
        //    _StopLossPrice = currentBar.low - StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = currentBar.close - _StopLossPrice;
        //    entryTrigger = "shoorting star";
        //  }
        //}

        ////Higher low
        //if (!enterLong && state.higherLow) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  PriceActionBar currentMin1 = _ShortTermPriceAction.CurrentMin1;
        //  enterLong = currentMin1.state.higherLow && currentMin1.state.volumeHigh;

        //  if (enterLong) {
        //    _StopLossPrice = currentMin1.low - StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = currentBar.close - _StopLossPrice;
        //    entryTrigger = "higher low";
        //  }
        //}

        ////Double bottom
        //if (!enterLong && state.doubleBottom) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  PriceActionBar currentMin1 = _ShortTermPriceAction.CurrentMin1;
        //  enterLong = currentMin1.state.doubleBottom && currentMin1.state.volumeHigh;

        //  if (enterLong) {
        //    _StopLossPrice = currentMin1.low - StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = currentBar.close - _StopLossPrice;
        //    entryTrigger = "double bottom";
        //  }
        //}

        //send long order if required
        if (enterLong) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze(_Risk);

          if (riskAnalysis.Allowed) {
            _LE.Send(riskAnalysis.Size);
            _CalculateActualRisk = true;

            //TODO: Log entry type determined above that triggered the trade.
            if (Debug)
              Output.WriteLine("LE Order Sent {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3}) InitialRisk({4}) Size({5}) Trigger({6})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta.ToString("0.00000"), _Risk.ToString("0.00000"), riskAnalysis.Size, entryTrigger);

          } else if (Debug)
            Output.WriteLine("LE Risk Not Allowed {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3}) InitialRisk({4}) Size({5}) Trigger({6})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta.ToString("0.00000"), _Risk.ToString("0.00000"), riskAnalysis.Size, entryTrigger);
        }
      }

      //enter short trades if we have a large deviation from the longer term POC
      if (currentDelta > _DeltaPriceValue) {
        //Implement entry when reversal patterns occur on the following patterns:
        // - Doji with high volume at bottom
        // - Hammer pattern with high volume
        // - Lower high
        // - Double top
        bool enterShort = false;
        PriceActionAnalysisState state = _ShortTermPriceAction.AnalysisState;
        string entryTrigger = "";

        //doji with high volume
        if (!enterShort && state.doji) {
          PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
          enterShort = currentBar.state.doji; // && currentBar.state.volumeHigh;

          if (enterShort) {
            _StopLossPrice = currentBar.high + StopLossBufferPips * _PipValue;
            _TakeProfitPrice = _LongTermPOC;
            _Risk = _StopLossPrice - currentBar.close;
            entryTrigger = "doji";
          }
        }

        ////hammer pattern with high volume
        //if (!enterShort && state.highPin) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  enterShort = currentBar.state.highPin && currentBar.state.volumeHigh;

        //  if (enterShort) {
        //    _StopLossPrice = currentBar.high + StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = _StopLossPrice - currentBar.close;
        //    entryTrigger = "hammer";
        //  }
        //}

        ////lower high
        //if (!enterShort && state.lowerHigh) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  PriceActionBar currentMin1 = _ShortTermPriceAction.CurrentMin1;
        //  enterShort = currentMin1.state.lowerHigh && currentMin1.state.volumeHigh;

        //  if (enterShort) {
        //    _StopLossPrice = currentMin1.high + StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = _StopLossPrice - currentBar.close;
        //    entryTrigger = "lower high";
        //  }
        //}

        ////double top
        //if (!enterShort && state.doubleTop) {
        //  PriceActionBar currentBar = _ShortTermPriceAction.CurrentBar;
        //  PriceActionBar currentMin1 = _ShortTermPriceAction.CurrentMin1;
        //  enterShort = currentMin1.state.doubleTop && currentMin1.state.volumeHigh;

        //  if (enterShort) {
        //    _StopLossPrice = currentMin1.high + StopLossBufferPips * _PipValue;
        //    _TakeProfitPrice = _LongTermPOC;
        //    _Risk = _StopLossPrice - currentBar.close;
        //    entryTrigger = "double top";
        //  }
        //}

        //send long order if required
        if (enterShort) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze(_Risk);

          if (riskAnalysis.Allowed) {
            _SE.Send(riskAnalysis.Size);
            _CalculateActualRisk = true;

            //TODO: Log entry type determined above that triggered the trade.
            if (Debug)
              Output.WriteLine("SE Order Sent {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3}) InitialRisk({4}) Size({5}) Trigger({6})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta.ToString("0.00000"), _Risk.ToString("0.00000"), riskAnalysis.Size, entryTrigger);

          } else if (Debug)
            Output.WriteLine("SE Risk Not Allowed {0}: ShortTermPOC({1}) LongTermPOC({2}) Delta({3}) InitialRisk({4}) Size({5}) Trigger({6})", _ShortTermBars.Time[0], shortTermVolumeProfile.POC.Price.Dbl, longTermVolumeProfile.POC.Price.Dbl, currentDelta.ToString("0.00000"), _Risk.ToString("0.00000"), riskAnalysis.Size, entryTrigger);
        }
      }
    }

    private void calcBarExit() {
      //manage long position
      if (PositionSide > 0) {
        //calculate actual risk if required
        if (_CalculateActualRisk) {
          _Risk = StrategyInfo.AvgEntryPrice - _StopLossPrice;
          _CalculateActualRisk = false;

          if (Debug)
            Output.WriteLine("LE risk adjust {0}: EntryPrice({1}) Risk({2}) StopLoss({3})", _ShortTermBars.Time[0], StrategyInfo.AvgEntryPrice, _Risk.ToString("0.00000"), _StopLossPrice);
        }

        //TODO: Implement more sophisticated exits
        _LXSL.Send(_StopLossPrice);
        _LXTP.Send(_TakeProfitPrice);



        //if (Debug)
        //  Output.WriteLine("Static LE_MM {0}: EntryPrice({1}) StopLoss({2}) TakeProfit({3}) ActualRisk({4})) ActualProfit({5})", Bars.Time[0], StrategyInfo.AvgEntryPrice, _SLPrice, _TakeProfit, _Risk, _Profit);


      }

      //manage short position
      if (PositionSide < 0) {
        //calculate actual risk if required
        if (_CalculateActualRisk) {
          _Risk = _StopLossPrice - StrategyInfo.AvgEntryPrice;
          _CalculateActualRisk = false;

          if (Debug)
            Output.WriteLine("SE risk adjust {0}: EntryPrice({1}) Risk({2}) StopLoss({3})", _ShortTermBars.Time[0], StrategyInfo.AvgEntryPrice, _Risk.ToString("0.00000"), _StopLossPrice);
        }


        //TODO: Implement more sophisticated exits
        _SXSL.Send(_StopLossPrice);
        _SXTP.Send(_TakeProfitPrice);


        //if (Debug)
        //  Output.WriteLine("Static SE_MM {0}: EntryPrice({1}) StopLoss({2}) TakeProfit({3}) ActualRisk({4}) ActualProfit({5})", Bars.Time[0], StrategyInfo.AvgEntryPrice, _SLPrice, _TakeProfit, _Risk, _Profit);


      }
    }
  }
}
