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
  /// Specific ultra high volume bars with given price action merit an entry in that specific direction.
  /// 
  /// Ideal Instruments and Timeframes:
  /// 
  /// 
  /// 
  /// Indicators: 
  /// 
  /// 
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
  /// Notes:
  /// 	Default location of this file it: C:\ProgramData\TS Support\MultiCharts .NET64\StudyServer\Techniques
  /// 
  /// </summary>
  public class _VolumePriceAnalysis2 : SignalObject {
    //Inputs:
    [Input] public int PriceActionFractalLayers { get; set; }   //Fractal layers to instantiate for price action analysis.
    [Input] public int VolumeLength { get; set; }               //Length for volume average and standard deviation computation
    [Input] public int BufferPips { get; set; }                 //buffer pips for the stop-loss
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMPercentRiskPerTrade { get; set; }   //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; }       //default trade size for static position sizing
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex
    const int DEFAULT_FRACTAL_LAYERS = 1;
    const int DEFAULT_VOLUME_LENGTH = 20;
    const int DEFAULT_BUFFER_PIPS = 3;

    //Attributes:		
    private IOrderMarket _LE_DMM;
    private IOrderMarket _SE_DMM;
    private IOrderPriced _LX_SL;
    private IOrderPriced _SX_SL;
    private IOrderPriced _LX_TP_LMT;
    private IOrderPriced _SX_TP_LMT;
    private double _Risk;
    private double _PipValue;
    private RiskManager _RiskManager;
    private PriceActionAnalysis _PriceAction;

    public _VolumePriceAnalysis2(object _ctx) : base(_ctx) {
      //initialize additional inputs for strategy
      PriceActionFractalLayers = DEFAULT_FRACTAL_LAYERS;
      VolumeLength = DEFAULT_VOLUME_LENGTH;
      BufferPips = DEFAULT_BUFFER_PIPS;

      //initialize default inputs for strategy
      MMStartPosSize = 0;               //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;                //profit delta before another additional contract is added
      MMPercentRiskPerTrade = 0.01;     //percentage risk per trade to take
      MMStaticPositionSize = 100000;    //setup one lot size for static position sizing
      MMStartNetProfit = 0;             //start net profit between restarts of the strategy
      Debug = false;
    }

    protected override void Create() {
      //create indicators used
      _PriceAction = new PriceActionAnalysis(PriceActionFractalLayers);

      //create the order types used
      _LE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _SE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      _LX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLLX", EOrderAction.Sell));              //exit orders
      _SX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLSX", EOrderAction.BuyToCover));
      _LX_TP_LMT = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "TPLX", EOrderAction.Sell));
      _SX_TP_LMT = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "TPSX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //clear the debugging console
      if (Debug)
        Output.Clear();

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
      _PriceAction.Clear();

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;
    }

    //protected override void StopCalc() {
    //	//cleanup/deinitialize strategy components
    //	
    //}

    protected override void CalcBar() {
      switch (Environment.CalcReason) {
        case CalculationReason.Default:
          CalcBarDefault();
          break;
        case CalculationReason.OrderFilled:
          CalcBarOrderFilled();
          break;
      }
    }

    private void CalcBarDefault() {
      //process signal entries and adjust stop-losses
      //NOTE: We only update the signals when the bar is closed but we process the entry/exit signals
      //      so that intrabar order generation can be used to get exact back testing on smaller timeframe
      //      bars during back testing.
      if (Bars.Status == EBarState.Close)
        _PriceAction.Analyse(Bars.Time[0], Bars.CurrentBar, Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void calcBarEntry() {
      bool enterLong = false;
      bool enterShort = false;

      enterLong = false;

      if (enterLong) {




      }

      enterShort = false;

      if (enterShort) {




      }

      //enter trade if signal was generated and risk is allowed
      if (enterLong) {
        RiskAnalysis riskAnalysis = _RiskManager.Analyze(_Risk);
        if (riskAnalysis.Allowed)
          _LE_DMM.Send(riskAnalysis.Size);
      }

      if (enterShort) {
        RiskAnalysis riskAnalysis = _RiskManager.Analyze(_Risk);
        if (riskAnalysis.Allowed)
          _SE_DMM.Send(riskAnalysis.Size);
      }
    }

    private void calcBarExit() {
      //manage long position
      if (PositionSide > 0) {



      }

      //manage short position
      if (PositionSide < 0) {


      }
    }

    private void orderFilled() {
      //send initial stop-loss when an entry order is filled
      //if (PositionSide > 0)

      //if (PositionSide < 0)

    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
      orderFilled();
    }

    private void CalcBarOrderFilled() {
      orderFilled();
    }
  }
}
