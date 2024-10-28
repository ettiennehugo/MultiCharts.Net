using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy {

  /// <summary>
  /// Price action trading signal based around low/high pivot points.
  /// </summary>

  //TBD: Set this flag to true to always set intrabar order generation for the strategy, otherwise the user have to set it manually.
  //     see programming guide p.49
  //[IOGMode(IOGMode.Enable)]
  public class _PriceActionPivots : SignalObject {
    [Input] public int FastMALength { get; set; }
    [Input] public int SlowMALength { get; set; }
    [Input] public int ATRLength { get; set; }
    [Input] public int StartTime { get; set; }
    [Input] public int WindowLength { get; set; }
    [Input] public bool EnableLong { get; set; }
    [Input] public bool EnableShort { get; set; }

    private IOrderMarket m_LE_SMM;
    private IOrderMarket m_SE_SMM;
    private IOrderPriced m_LX;
    private IOrderPriced m_SX;
    private XAverage m_FastXAverage;
    private XAverage m_SlowXAverage;
    private VariableSeries<Double> m_FastMA;
    private VariableSeries<Double> m_SlowMA;
    private double m_FastMAMomentum;
    private double m_SlowMAMomentum;
    private PriceActionAnalysis m_PriceActionAnalysis;
    private double m_StopLossPrice;
    private bool m_UpdateStopLoss;

    public _PriceActionPivots(object _ctx) : base(_ctx) {
      FastMALength = 21;
      SlowMALength = 55;
      ATRLength = PriceActionAnalysis.DefaultAtrLength;
      EnableLong = true;
      EnableShort = true;
    }

    protected override void Create() {
      //create indicators used for signal
      m_PriceActionAnalysis = new PriceActionAnalysis(3);
      m_FastXAverage = new XAverage(this);
      m_SlowXAverage = new XAverage(this);
      m_FastMA = new VariableSeries<Double>(this);
      m_SlowMA = new VariableSeries<Double>(this);
      m_LE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "PAPVLE", EOrderAction.Buy));     //static money management orders
      m_SE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "PAPVSE", EOrderAction.SellShort));
      m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PAPVLX", EOrderAction.Sell));                       //exit orders
      m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PAPVSX", EOrderAction.BuyToCover));
      m_StopLossPrice = 0;
      m_UpdateStopLoss = false;
    }

    protected override void StartCalc() {
      //setup indicators used
      m_PriceActionAnalysis.Clear();
      m_PriceActionAnalysis.AtrLength = ATRLength;
      m_FastXAverage.Price = Bars.Close;
      m_FastXAverage.Length = FastMALength;
      m_SlowXAverage.Price = Bars.Close;
      m_SlowXAverage.Length = SlowMALength;
    }

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

    protected void CalcBarDefault() {
      bool priceActionChanged = false;

      //add bar to price action data
      priceActionChanged = m_PriceActionAnalysis.Analyse(Bars.Time[0], Bars.CurrentBar, Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

      //update stop-loss price if required
      if (priceActionChanged && (m_PriceActionAnalysis.AnalysisState.pivotLow || m_PriceActionAnalysis.AnalysisState.pivotHigh)) {
        //get the pivot bar to adjust stop-loss
        PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;

        if (PositionSide > 0 && pivotBar.state.pivotState[0].pivotLow && m_StopLossPrice < pivotBar.low)
          m_StopLossPrice = pivotBar.low;
        if (PositionSide < 0 && pivotBar.state.pivotState[0].pivotHigh && m_StopLossPrice > pivotBar.high)
          m_StopLossPrice = pivotBar.high;
      }

      //send stop-losses order if required
      if (PositionSide != 0) {
        if (PositionSide > 0)
          m_LX.Send(m_StopLossPrice);
        if (PositionSide < 0)
          m_SX.Send(m_StopLossPrice);
      }

      if (Bars.CurrentBar > SlowMALength) {
        //update moving averages from new price data
        m_FastMA.Value = m_FastXAverage[0];
        m_SlowMA.Value = m_SlowXAverage[0];
        m_FastMAMomentum = m_FastMA.Momentum(1);
        m_SlowMAMomentum = m_SlowMA.Momentum(1);

        //process entries if we're flat and we have a pivot point
        if (PositionSide == 0 && priceActionChanged && (m_PriceActionAnalysis.AnalysisState.pivotLow || m_PriceActionAnalysis.AnalysisState.pivotHigh)) {
          //get the bar around which the pivot low/high was formed
          //NOTE: We always work with the first fractal layer so currentMin1 bar will be the detected pivot point.
          PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;

          //determine entry for long/short trades
          bool openLongTrade = false;
          bool openShortTrade = false;


          //TBD: Medium rules for entry that drop the momentum test and just use the MA.


          // * LONG: FastMA is above SlowMA and FastMAMomentum is positive and SlowMAMomentum is positive and we have a new pivot low that formed on a non-outside bar
          //         on low L1 or L2 => enter at the market and place a stop-loss at the low of the entry bar.
          openLongTrade = (m_FastMA.Value > m_SlowMA.Value) && (m_FastMAMomentum > 0) && (m_SlowMAMomentum > 0) && (false == pivotBar.state.outside) &&
                (true == pivotBar.state.pivotState[0].pivotLow) && ((1 == pivotBar.state.pivotState[0].lowCounter) || (2 == pivotBar.state.pivotState[0].lowCounter));

          // * SHORT: FastMA is below SlowMA and FastMAMomentum is negative and SlowMAMomentum is negative and we have a new pivot high that formed on a non-outside bar
          //          on high H1 or H2 => enter at the market and place a stop-loss at the high of the entry bar.
          if (!openLongTrade)
            openShortTrade = (m_FastMA.Value < m_SlowMA.Value) && (m_FastMAMomentum < 0) && (m_SlowMAMomentum < 0) && (false == pivotBar.state.outside) &&
            (true == pivotBar.state.pivotState[0].pivotHigh) && ((1 == pivotBar.state.pivotState[0].highCounter) || (2 == pivotBar.state.pivotState[0].highCounter));

          //static position sizing controlled by the user settings
          if (EnableLong && openLongTrade) {
            m_LE_SMM.Send(positionSize());
            m_StopLossPrice = pivotBar.low;
          }

          if (EnableShort && openShortTrade) {
            m_SE_SMM.Send(positionSize());
            m_StopLossPrice = pivotBar.high;
          }
        }
      }
    }

    /// <summary>
    /// Catches recalulation if order filled reason is received.
    /// </summary>
    protected void CalcBarOrderFilled() {
      if (PositionSide > 0)
        m_LX.Send(m_StopLossPrice);
      if (PositionSide < 0)
        m_SX.Send(m_StopLossPrice);
    }

    /// <summary>
    /// Live trading handling of broker strategy order filled.
    /// </summary>
    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
      if (PositionSide > 0)
        m_LX.Send(m_StopLossPrice);
      if (PositionSide < 0)
        m_SX.Send(m_StopLossPrice);
    }

		private int positionSize()
		{
			double equityAvailable = 0.0d;
			int result = 0;
			if (Bars.Close[0] == 0.0d) return result;

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					equityAvailable = this.InitialCapital + this.NetProfit;
					break;
				case EApplicationCode.Portfolio:
					equityAvailable = (this.InitialCapital + this.Portfolio.NetProfit) * (this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d);
					break;
			}

			if (equityAvailable > 0) result = (int)Math.Truncate(equityAvailable / Bars.Close[0]);

			return result;
		}
	}
}