using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// 
	/// </summary>
	public class _StopsAndTargets : SignalObject
	{
		//Inputs:
		[Input] public ESpecOrdersMode OrderMode { get; set; }    //order mode for stops/targets
		[Input] public double StopLossPct { get; set; }           //stop-loss percentage
		[Input] public double BreakEvenPct { get; set; }          //break even stop percentage
		[Input] public double TrailingStopActivationPct { get; set; } //percentage at which the trailing stop will be activated
		[Input] public double TrailingStopPct { get; set; }       //trailing stop percentage
		[Input] public double TargetPct { get; set; }             //exit target percentage

		//Attributes:

		public _StopsAndTargets(object _ctx) : base(_ctx) { }

		//protected override void Create() { }

		protected override void StartCalc()
		{
			if ((TrailingStopActivationPct == 0 && TrailingStopPct != 0) ||
					(TrailingStopActivationPct != 0 && TrailingStopPct == 0))
				throw new Exception("Trailing stop activation and trailing stop percentage should be used used together.");
		}

		protected override void CalcBar()
		{
			//TODO: Add breakeven stop and trail at price extreme high/low based on position direction.
			//  NOTE: The Convert.ToDouble( ) is required otherwise the doubles become intergers!!!
			CurSpecOrdersMode = OrderMode;

			if (this.PositionSide == 0)
				return;

			if (StopLossPct != 0)
			{
				double amount = 0.0;
				if (CurSpecOrdersMode == ESpecOrdersMode.PerContract)
					amount = this.EntryPrice() * (StopLossPct / 100);
				else
					amount = this.EntryPrice() * Math.Abs(this.PositionSide) * (StopLossPct / 100);
				GenerateStopLoss(amount);
			}

			if (BreakEvenPct != 0)
			{
				double amount = 0.0;
				if (CurSpecOrdersMode == ESpecOrdersMode.PerContract)
					amount = this.EntryPrice() * (BreakEvenPct / 100);
				else
					amount = this.EntryPrice() * Math.Abs(this.PositionSide) * (BreakEvenPct / 100);
				GenerateBreakEven(amount);
			}

			if (TrailingStopActivationPct != 0 && TrailingStopPct != 0)
			{
				double amount = 0.0;
				if (CurSpecOrdersMode == ESpecOrdersMode.PerContract)
					amount = this.EntryPrice() * (TrailingStopActivationPct / 100);
				else
					amount = this.EntryPrice() * Math.Abs(this.PositionSide) * (TrailingStopActivationPct / 100);
				GeneratePercentTrailing(amount, TrailingStopPct);
			}

			if (TargetPct != 0)
			{
				double amount = 0.0;
				if (CurSpecOrdersMode == ESpecOrdersMode.PerContract)
					amount = this.EntryPrice() * (TargetPct / 100);
				else
					amount = this.EntryPrice() * Math.Abs(this.PositionSide) * (TargetPct / 100);

				GenerateProfitTarget(amount);
			}
		}
	}
}
