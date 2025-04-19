using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.Indicator;

namespace PowerLanguage.Strategy
{
	public class _SuperSmoother : _CustomSignal
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;
		public const double DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT = 0.01d;

		//properties:
		[Input] public int Period { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		private PowerLanguage.Function._EfficiencyRatio m_efficiencyRatio;
		private PowerLanguage.Function._SuperSmoother m_superSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;
		private IOrderMarket m_lx;
		private IOrderMarket m_sx;

		public _SuperSmoother(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			Period = DEFAULT_PERIOD;
			RiskBasedPositionSizing = false;
			MaxRiskEquityPerPosPercent = DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT;
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_superSmoother = new PowerLanguage.Function._SuperSmoother(this);
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
			m_superSmoother.Instrument = Bars;
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
			//higher efficiency ratio means the price moves more smoothly
			if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = m_efficiencyRatio.Value;
			
			//process the entry/exit logic
			calcBarEntry();	
			calcBarExit();
		}

		protected void calcBarEntry()
		{
			//determine whether we should send an entry order

			//TODO - calculating the stop-loss off the delta of Close and SuperSmoother[0] results in HUGE positions for the risk based entries.

			if (EnableLong && Bars.Close.CrossesOver(m_superSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? stopLossBasedPositionSize(Bars, m_superSmoother[0], MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent, false);
				m_le.Send(size);
			}
			else if (EnableShort && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? stopLossBasedPositionSize(Bars, m_superSmoother[0], MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent, false);
				m_se.Send(size);
			}
		}

		protected void calcBarExit()
		{
			if (this.PositionSide > 0 && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
				m_lx.Send();
			else if (this.PositionSide < 0 && Bars.Close.CrossesOver(m_superSmoother[0], 1))
				m_sx.Send();
		}
	}
}