﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	public class _AdaptiveSuperSmootherSE : _CustomSignal
	{
		//constants
		public const double DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT = 0.01d;

		//inputs
		[Input] public int FastPeriod { get; set; }
		[Input] public int SlowPeriod { get; set; }
		[Input] public int EfficiencyRatioPeriod { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public bool EnableExit { get; set; }

		//properties


		//attributes
		private PowerLanguage.Function._AdaptiveSuperSmoother m_adaptiveSuperSmoother;
		private IOrderMarket m_se;
		private IOrderMarket m_sx;

		//interface methods
		public _AdaptiveSuperSmootherSE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			FastPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_FAST_PERIOD;
			SlowPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_SLOW_PERIOD;
			EfficiencyRatioPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_EFFICIENCY_RATIO_PERIOD;
			RiskBasedPositionSizing = false;
			MaxRiskEquityPerPosPercent = DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT;
			EnableExit = true;
			m_adaptiveSuperSmoother = new PowerLanguage.Function._AdaptiveSuperSmoother(this);
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "AssSE", EOrderAction.SellShort));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AssSX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			m_adaptiveSuperSmoother.Instrument = Bars;
			m_adaptiveSuperSmoother.FastPeriod = FastPeriod;
			m_adaptiveSuperSmoother.SlowPeriod = SlowPeriod;
			m_adaptiveSuperSmoother.EfficiencyRatioPeriod = EfficiencyRatioPeriod;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//higher efficiency ratio means the price moves more smoothly
					if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = m_adaptiveSuperSmoother.EfficiencyRatio;

					if (PositionSide == 0 && Bars.Close.CrossesUnder(m_adaptiveSuperSmoother[0], 1))
					{
						int size = RiskBasedPositionSizing ? stopLossBasedPositionSize(Bars, m_adaptiveSuperSmoother[0], MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent, false);
						m_se.Send(size);
					}

					if (PositionSide < 0 && EnableExit && Bars.Close.CrossesOver(m_adaptiveSuperSmoother[0], 1))
						m_sx.Send();
					break;
				case CalculationReason.OrderFilled:
					if (PositionSide < 0 && EnableExit && Bars.Close.CrossesOver(m_adaptiveSuperSmoother[0], 1))
						m_sx.Send();
					break;

			}
		}

		//methods


	}
}