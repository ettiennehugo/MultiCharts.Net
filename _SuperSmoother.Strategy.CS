using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.Indicator;

namespace PowerLanguage.Strategy
{
	public class _SuperSmoother : PortfolioSignalObject
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int Period { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		private PowerLanguage.Function._EfficiencyRatio m_efficiencyRatio;
		private SuperSmoother m_superSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;
		private IOrderMarket m_lx;
		private IOrderMarket m_sx;
		private double m_stopLossPercent;

		public _SuperSmoother(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			Period = DEFAULT_PERIOD;
			RiskBasedPositionSizing = true;
			MaxRiskEquityPerPosPercent = 1.0;		//make zero or less to use portfolio percentage
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_superSmoother = new SuperSmoother(this);
			m_efficiencyRatio = new PowerLanguage.Function._EfficiencyRatio(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SSLE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SSSE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SSLX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SSSX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			m_superSmoother.Length = Period;
			m_superSmoother.Price = Bars.Close;
			m_efficiencyRatio.Length = Period;
			m_efficiencyRatio.Instrument = Bars;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					CalcBarDefault();
					break;
			}
		}

		protected void CalcBarDefault()
		{
			//prioritize signals with a higher efficiency ratio in portfolio trading
			//lower efficiency ratio means the price moves more smoothly, so we need to use 1 - ER to prioritize these signals.
			if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = 1 - m_efficiencyRatio.Value;
			
			//process the entry/exit logic
			calcBarEntry();
			calcBarExit();
		}

		protected void calcBarEntry()
		{
			//determine whether we should send an entry order
			if (EnableLong && Bars.Close.CrossesOver(m_superSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? riskBasedPositionSize() : positionSize();
				m_stopLossPercent = Math.Abs(Bars.Close[0] - m_superSmoother[0]) / Bars.Close[0];
				m_le.Send(size);
			}
			else if (EnableShort && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? riskBasedPositionSize() : positionSize();
				m_stopLossPercent = Math.Abs(Bars.Close[0] - m_superSmoother[0]) / Bars.Close[0];
				m_se.Send(size);
			}
		}

		protected void calcBarExit()
		{
			
			//send standard exit orders
			if (this.PositionSide > 0 && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
			{
				if (RiskBasedPositionSizing)
				{
					CurSpecOrdersMode = ESpecOrdersMode.PerContract;
					GenerateStopLoss(this.EntryPrice() * m_stopLossPercent);
				}
				m_lx.Send();
			}
			else if (this.PositionSide < 0 && Bars.Close.CrossesOver(m_superSmoother[0], 1))
			{
				if (RiskBasedPositionSizing)
				{
					CurSpecOrdersMode = ESpecOrdersMode.PerContract;
					GenerateStopLoss(this.EntryPrice() * m_stopLossPercent);
				}
				m_sx.Send();
			}
		}

		private double equityAvailable()
		{
			double equityAvailable = 0.0d;

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					equityAvailable = (this.InitialCapital + this.NetProfit) * MaxRiskEquityPerPosPercent;
					break;
				case EApplicationCode.Portfolio:
					double percentEquity = MaxRiskEquityPerPosPercent > 0.0d ? MaxRiskEquityPerPosPercent : this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d;
					equityAvailable = (this.InitialCapital + this.Portfolio.NetProfit) * percentEquity;
					break;
			}

			return equityAvailable;
		}

		private int riskBasedPositionSize()
		{
			int result = 0;

			double positionCash = equityAvailable();
			if (positionCash > 0)
			{
				double risk;
				try
				{
					risk = checked(Math.Round(Math.Abs(Bars.Close[0] - m_superSmoother[0]), 2));
					if (risk != 0.0d) result = checked((int)Math.Truncate(positionCash / risk));
				}
				catch (OverflowException)
				{
					return 0;		//risk overflow exception occurred, ignore signal
				}
			}

			return result;
		}

		private int positionSize()
		{
			int result = 0;
			if (Bars.Close[0] == 0.0d) return result;
	
			double positionCash = equityAvailable();
			result = (int)Math.Truncate(positionCash / Bars.Close[0]);

			return result;
		}
	}
}