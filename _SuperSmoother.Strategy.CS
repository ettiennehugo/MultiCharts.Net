using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;
using System.Net.Mime;

namespace PowerLanguage.Strategy
{
	public class _SuperSmoother : SignalObject
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int Period { get; set; }
		[Input] public bool ManageExit { get; set; }
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		private EfficiencyRatio m_efficiencyRatio;
		public double m_priority;
		private SuperSmoother m_superSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;
		private IOrderMarket m_lx;
		private IOrderMarket m_sx;

		public _SuperSmoother(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			Period = DEFAULT_PERIOD;
			ManageExit = true;
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_superSmoother = new SuperSmoother(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			m_superSmoother.Period = Period;
			m_superSmoother.Price = Bars.Close;
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
			//process the entry/exit logic
			calcBarEntry();
			if (ManageExit) calcBarExit();
		}

		protected void calcBarEntry()
		{
			//determine whether we should send an entry order
			if (EnableLong && Bars.Close.CrossesOver(m_superSmoother[0], 1))
				m_le.Send(positionSize());
			else if (EnableShort && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
				m_se.Send(positionSize());
		}

		protected void calcBarExit()
		{
			//send standard exit orders
			if (this.PositionSide > 0 && Bars.Close.CrossesUnder(m_superSmoother[0], 1))
				m_lx.Send();
			else if (this.PositionSide < 0 && Bars.Close.CrossesOver(m_superSmoother[0], 1))
				m_sx.Send();
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