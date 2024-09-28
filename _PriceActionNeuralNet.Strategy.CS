using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type:
  /// 
  /// Hypothesis:
  /// 
  /// 
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
  /// Notes:
  /// 	
  /// 
  /// </summary>
  public class _PriceActionNeuralNet : SignalObject {
    //Inputs:

    //TODO: Define additional inputs for the strategy.

    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Attributes:		
    protected double _PipValue;

    public _PriceActionNeuralNet(object _ctx) : base(_ctx) {
      //initialize additional inputs for strategy


      //TODO


      //initialize default inputs for strategy
      Debug = false;
    }

    protected override void Create() {
      //create indicators used


      //TODO


      //create the order types used


      //TODO


    }

    protected override void StartCalc() {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //initialize indicators and objects used


      //TODO


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
      if (Bars.Status == EBarState.Close) {
  
  
        //TODO

        
      }

      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void calcBarEntry() {

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
      if (PositionSide > 0) {


      }

      if (PositionSide < 0) {

      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
	  	orderFilled();
  	}

    private void CalcBarOrderFilled() {
      orderFilled();
    }
  }
}
