using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy
{

  public class _KeltnerChannelPA : SignalObject
  {
    //Inputs:
    [Input] public int LinRegLength { get; set; }
    [Input] public int ATRLength { get; set; }
    [Input] public double ScalingFactor { get; set; }
    [Input] public int StartTime { get; set; }
    [Input] public int WindowLength { get; set; }
    [Input] public int MMStartPosSize { get; set; }
    [Input] public double MMProfitDelta { get; set; }
    [Input] public double MMStartNetProfit { get; set; }    //Start net profit when strategy is restarted between system reboots.
    [Input] public bool EnableLong { get; set; }        //Enable long trades
    [Input] public bool EnableShort { get; set; }       //Enable short trades

    //Attributes:
    private IOrderPriced m_LE_DMM;
    private IOrderPriced m_SE_DMM;
    private IOrderPriced m_LE_SMM;
    private IOrderPriced m_SE_SMM;
    private IOrderPriced m_LX;
    private IOrderPriced m_SX;
    private LinearRegValue m_LinRegFC;
    private PriceActionAnalysis m_PriceActionAnalysis;
    private VariableSeries<Double> m_CenterLine;
    private VariableSeries<Double> m_UpperBand;
    private VariableSeries<Double> m_LowerBand;
    private TimeSpan m_StartTime;
    private TimeSpan m_EndTime;
    private double m_StopLoss;

    public _KeltnerChannelPA(object _ctx) : base(_ctx)
    {
      LinRegLength = 100;
      ATRLength = 20;
      ScalingFactor = 1.0;
      StartTime = 0;          //12:00 AM starting time for trading session       
      WindowLength = 1439;    //minutes for a full 24-hour trading day
      MMStartPosSize = 0;   //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;    //profit delta before another additional contract is added (0 - disable MM)	
      MMStartNetProfit = 0; //start net profit between restarts of the strategy
      EnableLong = true;    //per default enable long trades
      EnableShort = true;   //per default enable short trades
    }

    protected override void Create()
    {
      m_PriceActionAnalysis = new PriceActionAnalysis(3);
      m_LinRegFC = new LinearRegValue(this);
      m_CenterLine = new VariableSeries<Double>(this);
      m_UpperBand = new VariableSeries<Double>(this);
      m_LowerBand = new VariableSeries<Double>(this);

      m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KTCLE", EOrderAction.Buy));     //dynamic money management orders
      m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KTCSE", EOrderAction.SellShort));
      m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCLE", EOrderAction.Buy));       //static money management orders
      m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCSE", EOrderAction.SellShort));
      m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCLX", EOrderAction.Sell));      //exit orders
      m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCSX", EOrderAction.BuyToCover));

      m_StopLoss = 0;
    }

    protected override void StartCalc()
    {
      //set the start/end times for the time filter
      m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
      m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));

      //setup indicators used
      m_PriceActionAnalysis.Clear();
      m_PriceActionAnalysis.AtrLength = ATRLength;
      m_LinRegFC.price = Bars.Close;
      m_LinRegFC.length = LinRegLength;
      m_LinRegFC.tgtbar = 0;
    }

    protected override void CalcBar()
    {
      //update indicator values
      bool priceActionChanged = m_PriceActionAnalysis.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);
      m_CenterLine.Value = m_LinRegFC[0];

      double scaledAtr = ScalingFactor * this.AverageTrueRange(ATRLength);
      m_LowerBand.Value = m_CenterLine.Value - scaledAtr;
      m_UpperBand.Value = m_CenterLine.Value + scaledAtr;

      //manage signal once we have enough bars to compute indicators
      int longestLength = LinRegLength > ATRLength ? LinRegLength : ATRLength;
      if (Bars.CurrentBar >= longestLength)
      {
        //stop-loss/trailing-stop exit
        if (PositionSide != 0)
        {
          //see whether we have a pivot to improve our stop-loss otherwise keep a stop at the center line
          double stopLoss = 0;

          if (PositionSide > 0)
          {
            //determine the correct stop-loss to use	
            stopLoss = ((m_StopLoss != 0) && (m_StopLoss > m_CenterLine[0])) ? m_StopLoss : m_CenterLine[0];

            //check whether we have a pivot low to work with						
            if (true == priceActionChanged && true == m_PriceActionAnalysis.AnalysisState.pivotLow)
            {
              //get pivot bar and update stop-loss to it's low if it would lead to a price improvement
              PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;
              if (pivotBar.low > stopLoss)
              {
                stopLoss = pivotBar.low;
                m_StopLoss = pivotBar.low;
              }
            }

            m_LX.Send(stopLoss);

          }
          else if (PositionSide < 0)
          {
            //determine the correct stop-loss to use
            stopLoss = ((m_StopLoss != 0) && (m_StopLoss < m_CenterLine[0])) ? m_StopLoss : m_CenterLine[0];

            //check whether we have a pivot high to work with
            if (true == priceActionChanged && true == m_PriceActionAnalysis.AnalysisState.pivotHigh)
            {
              //get pivot bar and update stop-loss to it's low if it would lead to a price improvement
              PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;
              if (pivotBar.high < stopLoss)
              {
                stopLoss = pivotBar.high;
                m_StopLoss = pivotBar.high;
              }
            }

            m_SX.Send(stopLoss);
          }
        }
        else
        {
          //apply time filter for entries
          //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
          //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
          DateTime currentTime = Bars.Time.Value;

          if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||    //start/end time fall in the same trading day
              ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime))))
          {  //start/end time span over different trading days
             //determine flags used for entry position
            bool crossesOver = Bars.Close.CrossesOver(m_UpperBand, ExecInfo.MaxBarsBack);
            bool crossesUnder = Bars.Close.CrossesUnder(m_LowerBand, ExecInfo.MaxBarsBack);

            //check whether we're using dynamic or static position sizing
            if (MMStartPosSize > 0 && MMProfitDelta > 0)
            {
              //dynamic position sizing, compute new trade size
              //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
              double netProfit = MMStartNetProfit + NetProfit;
              netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negetive for square root calculation below
              int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

              if (crossesOver && EnableLong)
              {
                m_LE_DMM.Send(Bars.High[0], numLots);
                m_StopLoss = 0;
              }

              if (crossesUnder && EnableShort)
              {
                m_SE_DMM.Send(Bars.Low[0], numLots);
                m_StopLoss = 0;
              }
            }
            else
            {
              //static position sizing controlled by the user settings
              if (crossesOver && EnableLong)
              {
                m_LE_SMM.Send(Bars.High[0]);
                m_StopLoss = 0;
              }

              if (crossesUnder && EnableShort)
              {
                m_SE_SMM.Send(Bars.Low[0]);
                m_StopLoss = 0;
              }
            }

            //TODO: When entry order is filled you might have a single bar in which you don't have a stop-loss. Maybe try use the OnBrokerOrderFilled method.

          }
        }
      }
    }
  }
}