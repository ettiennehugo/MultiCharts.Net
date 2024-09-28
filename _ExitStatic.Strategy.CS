using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Static exit that can be used to test the effectiveness of an entry.
  /// </summary>
  public class _ExitStatic : SignalObject
  {
    //Types:
    public enum EExitStrategy
    {
      FixedStopTarget,
      FixedBars,
      RandomExit,
    }

    //Inputs:
    [Input] public EExitStrategy ExitStrategy { get; set; }
    [Input] public double FixedStop { get; set; }     //Fixed stop to use for FixedStopTarget
    [Input] public double FixedTarget { get; set; }   //Fixed target to use for FixedStopTarget
    [Input] public int FixedBars { get; set; }        //Fixed number of bars to remain in position for FixedBars
    [Input] public int MaxRandomBars { get; set; }    //Maximum number of bars to use for RandomExit 

    //Attributes:
    protected IOrderMarket LX;
    protected IOrderMarket SX;
    protected int BarsSinceEntry;
    protected int RandomBars;
    protected Random RandomGenerator;

    public _ExitStatic(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      //set defaults for inputs
      ExitStrategy = EExitStrategy.FixedStopTarget;
      FixedStop = 500;
      FixedTarget = 1000;
      FixedBars = 10;
      MaxRandomBars = 10;

      //create the order types used
      LX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));
      SX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));

      RandomGenerator = new Random();
    }

    protected override void CalcBar()
    {
      if (PositionSide == 0)
      {
        //reset bar counters
        BarsSinceEntry = -1;
        RandomBars = -1;
      }
      else
      {
        //update bar counters
        BarsSinceEntry++;
        if (RandomBars == -1) RandomBars = (int)Math.Round(MaxRandomBars * RandomGenerator.NextDouble(), 0);

        //manage the position based on selected strategy
        switch (ExitStrategy)
        {
          case EExitStrategy.FixedStopTarget:
            CurSpecOrdersMode = ESpecOrdersMode.PerPosition;
            GenerateStopLoss(FixedStop);
            GenerateProfitTarget(FixedTarget);
            break;

          case EExitStrategy.FixedBars:
            //exit position if fixed number of bars were hit
            if (BarsSinceEntry == FixedBars)
              if (PositionSide > 0)
                LX.Send();
              else
                SX.Send();
            break;

          case EExitStrategy.RandomExit:
            //exit position if random number of bars were hit
            if (BarsSinceEntry == RandomBars)
              if (PositionSide > 0)
                LX.Send();
              else
                SX.Send();
            break;
        }
      }
    }
  }
}
