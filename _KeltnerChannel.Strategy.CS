using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  public class _KeltnerChannel : SignalObject
  {
    //Inputs:
    [Input] public int LinRegLength { get; set; }
    [Input] public int ATRLength { get; set; }
    [Input] public double ScalingFactor { get; set; }
    [Input] public int StartTime { get; set; }
    [Input] public int WindowLength { get; set; }
    [Input] public double StopLossPct { get; set; }
    [Input] public double TrailingStopPct { get; set; }
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
    private VariableSeries<Double> m_CenterLine;
    private VariableSeries<Double> m_UpperBand;
    private VariableSeries<Double> m_LowerBand;
    private TimeSpan m_StartTime;
    private TimeSpan m_EndTime;

    public _KeltnerChannel(object _ctx) : base(_ctx)
    {
      LinRegLength = 100;
      ATRLength = 20;
      ScalingFactor = 1.5;
      StartTime = 0;          //12:00 AM starting time for trading session       
      WindowLength = 1439;    //minutes for a full 24-hour trading day
      StopLossPct = 0;        //stop loss (0 = disable)
      TrailingStopPct = 0;    //trailing stop (0 = disable)
      MMStartPosSize = 0;   //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;    //profit delta before another additional contract is added (0 - disable MM)	
      MMStartNetProfit = 0; //start net profit between restarts of the strategy
      EnableLong = true;    //per default enable long trades
      EnableShort = true;   //per default enable short trades
    }

    protected override void Create()
    {
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
    }

    protected override void StartCalc()
    {
      //set the start/end times for the time filter
      m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
      m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));

      //update linear regression and moving average
      m_LinRegFC.price = Bars.Close;
      m_LinRegFC.length = LinRegLength;
      m_LinRegFC.tgtbar = 0;
    }

    protected override void CalcBar()
    {
      //update indicator values
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
          CurSpecOrdersMode = ESpecOrdersMode.PerContract;
          if (StopLossPct != 0) GenerateStopLoss(this.EntryPrice() * (StopLossPct / 100));
          if (TrailingStopPct != 0) GeneratePercentTrailing(this.EntryPrice() * (TrailingStopPct / 100), TrailingStopPct);

          //always have an exit order at the moving average if no stop loss is specified
          if (PositionSide > 0)
            m_LX.Send(m_CenterLine[0]);
          else if (PositionSide < 0)
            m_SX.Send(m_CenterLine[0]);
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
              netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negative for square root calculation below
              int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

              if (crossesOver && EnableLong) m_LE_DMM.Send(Bars.High[0], numLots);
              if (crossesUnder && EnableShort) m_SE_DMM.Send(Bars.Low[0], numLots);
            }
            else
            {
              //static position sizing controlled by the user settings
              if (crossesOver && EnableLong) m_LE_SMM.Send(Bars.High[0]);
              if (crossesUnder && EnableShort) m_SE_SMM.Send(Bars.Low[0]);
            }
          }
        }
      }
    }
  }
}