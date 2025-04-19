using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;

namespace PowerLanguage.Strategy
{
	public class _AdaptiveSuperSmootherCross : _CustomSignal
	{
		//constants
		public const double DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT = 0.01d;

		//inputs
		[Input] public int FastMinPeriod { get; set; }
		[Input] public int FastMaxPeriod { get; set; }
		[Input] public int SlowMinPeriod { get; set; }
		[Input] public int SlowMaxPeriod { get; set; }
		[Input] public int EfficiencyRatioPeriod { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }
		[Input] public bool EnableExit { get; set; }

		//properties
		private PowerLanguage.Function._AdaptiveSuperSmoother m_fastAdaptiveSuperSmoother;
		private PowerLanguage.Function._AdaptiveSuperSmoother m_slowAdaptiveSuperSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_lx;
		private IOrderMarket m_se;
		private IOrderMarket m_sx;

		//attributes


		//interface methods
		public _AdaptiveSuperSmootherCross(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			FastMinPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_FAST_PERIOD;
			FastMaxPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_SLOW_PERIOD;
			SlowMinPeriod = FastMaxPeriod + 1;
			SlowMaxPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_SLOW_PERIOD * 2;
			EfficiencyRatioPeriod = PowerLanguage.Function._AdaptiveSuperSmoother.DEFAULT_EFFICIENCY_RATIO_PERIOD;
			RiskBasedPositionSizing = false;
			MaxRiskEquityPerPosPercent = DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT;
			EnableLong = true;
			EnableShort = true;
			EnableExit = true;
			m_fastAdaptiveSuperSmoother = new PowerLanguage.Function._AdaptiveSuperSmoother(this);
			m_slowAdaptiveSuperSmoother = new PowerLanguage.Function._AdaptiveSuperSmoother(this);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "AssLE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AssLX", EOrderAction.Sell));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "AssLE", EOrderAction.SellShort));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "AssLX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			m_fastAdaptiveSuperSmoother.Instrument = Bars;
			m_fastAdaptiveSuperSmoother.FastPeriod = FastMinPeriod;
			m_fastAdaptiveSuperSmoother.SlowPeriod = FastMaxPeriod;
			m_fastAdaptiveSuperSmoother.EfficiencyRatioPeriod = EfficiencyRatioPeriod;
			m_slowAdaptiveSuperSmoother.Instrument = Bars;
			m_slowAdaptiveSuperSmoother.FastPeriod = SlowMinPeriod;
			m_slowAdaptiveSuperSmoother.SlowPeriod = SlowMaxPeriod;
			m_slowAdaptiveSuperSmoother.EfficiencyRatioPeriod = EfficiencyRatioPeriod;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//higher efficiency ratio means the price moves more smoothly
					if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = m_fastAdaptiveSuperSmoother.EfficiencyRatio;

					if (PositionSide == 0)
					{
						if (EnableLong && m_fastAdaptiveSuperSmoother.CrossesOver(m_slowAdaptiveSuperSmoother, 1))
						{
							int size = RiskBasedPositionSizing ? stopLossBasedPositionSize(Bars, m_fastAdaptiveSuperSmoother[0], MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent, false);
							m_le.Send(size);
						}

						if (EnableShort && m_fastAdaptiveSuperSmoother.CrossesUnder(m_slowAdaptiveSuperSmoother, 1))
						{
							int size = RiskBasedPositionSizing ? stopLossBasedPositionSize(Bars, m_fastAdaptiveSuperSmoother[0], MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent, false);
							m_se.Send(size);
						}
					}  
					else if (EnableExit)
					{
						if (PositionSide > 0 && m_fastAdaptiveSuperSmoother.CrossesUnder(m_slowAdaptiveSuperSmoother, 1))
							m_lx.Send();

						if (PositionSide < 0 && m_fastAdaptiveSuperSmoother.CrossesOver(m_slowAdaptiveSuperSmoother, 1))
							m_sx.Send();
					}
					break;
				case CalculationReason.OrderFilled:
					if (EnableExit)
					{
						if (PositionSide > 0 && m_fastAdaptiveSuperSmoother.CrossesUnder(m_slowAdaptiveSuperSmoother, 1))
							m_lx.Send();

						if (PositionSide < 0 && m_fastAdaptiveSuperSmoother.CrossesOver(m_slowAdaptiveSuperSmoother, 1))
							m_sx.Send();
					}
					break;
			}

		}

		//methods



	}
}