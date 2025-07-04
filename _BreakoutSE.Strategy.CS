﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  public class _BreakoutSE : _CustomSignal
  {
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int LowBreakoutPeriod { get; set; }		//only applies when we use short trading (no stop-loss based on this yet)
		[Input] public int SuperSmootherPeriod { get; set; }	//used only to exit trades
		[Input] public int VolumePeriod { get; set; }
		//[Input] public int MarketPeriod { get; set; }
		//[Input] public int RelativeStrengthPeriod { get; set; }
		//[Input] public int RelativeStrengthSmoothingPeriod { get; set; }

		//attributes:
		private _EfficiencyRatio m_efficiencyRatio;
		//private RelativeStrength m_relativeStrength;
		public double m_priority;
		private PowerLanguage.Function._SuperSmoother m_instrumentSmoother;
		private _VolumeWithSD m_volumeWithSD;
		private IInstrument m_marketBars;
		private _SuperSmoother m_marketSmoother;
		private IOrderMarket m_se;
		private IOrderMarket m_sx;

		public _BreakoutSE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			LowBreakoutPeriod = DEFAULT_PERIOD;
			SuperSmootherPeriod = DEFAULT_PERIOD;
			VolumePeriod = DEFAULT_PERIOD;
			//MarketPeriod = DEFAULT_PERIOD;
			//RelativeStrengthPeriod = RelativeStrength.LENGTH;
			//RelativeStrengthSmoothingPeriod = RelativeStrength.LENGTH;

			//create functions used
			m_priority = 0.0d;
			m_efficiencyRatio = new _EfficiencyRatio(this);
			m_instrumentSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_volumeWithSD = new _VolumeWithSD(this);
			//m_marketSmoother = new _SuperSmoother(this);
			//m_relativeStrength = new RelativeStrength(this);

			//create order types
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			m_efficiencyRatio.Length = LowBreakoutPeriod;
			//m_relativeStrength.Length = RelativeStrengthPeriod;
			//m_relativeStrength.SmoothingLength = RelativeStrengthSmoothingPeriod;
			//m_relativeStrength.Instrument1 = this.BarsOfData(1);
			//m_relativeStrength.Instrument2 = this.BarsOfData(2);
			m_instrumentSmoother.Length = SuperSmootherPeriod;
			m_instrumentSmoother.Instrument = Bars;
			m_volumeWithSD.Length = VolumePeriod > 0 ? VolumePeriod : _VolumeWithSD.DEFAULT_LENGTH;
			m_volumeWithSD.Instrument = Bars;
			//m_marketSmoother.Length = MarketPeriod;
			//m_marketBars = this.BarsOfData(1);
			//m_marketSmoother.Price = m_marketBars.Close;

			if (Environment.ApplicationCode == EApplicationCode.Portfolio) this.Portfolio.PortfolioEntriesPriority = new Lambda<double>(delegate { return m_priority; });
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
			calcBarExit();
		}

		protected void calcBarEntry()
		{
			//prioritize signals with a higher efficiency ratio in portfolio trading
			//lower efficiency ratio means the price moves more smoothly, so we need to use 1 - ER to prioritize the signals.
			if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = 1 - m_efficiencyRatio.Value;
			//m_priority = m_relativeStrength.Value;

			//determine whether we should send an entry order
			if (//m_marketBars.Close[0] < m_marketSmoother.Value && //market is bearish
				//m_relativeStrength[0] < 1.0d && //instrument is weaker than market
				(VolumePeriod == 0 || Bars.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0]) && //above average volume breakout
				Bars.Close[0] < Bars.Low.Lowest(LowBreakoutPeriod, 1))

				//TODO - Cap the position size to about 5-10% of the daily volume, this strategy does give a lot of entries that would not be able to be filled in real life.
				m_se.Send(positionSize(Bars));
		}

		protected void calcBarExit()
		{
			if (this.PositionSide < 0 &&
				//(m_marketBars.Close.CrossesOver(m_marketSmoother[0], 1) || Bars.Close.CrossesOver(m_instrumentSmoother[0], 1))  //test with market filter
				Bars.Close.CrossesOver(m_instrumentSmoother[0], 1)  //no market filter - in general this is a better exit since the signal would turn bullish most likely before the market does
				)
				m_sx.Send();
		}
  }
}