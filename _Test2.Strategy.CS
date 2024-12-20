using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;
using System.Collections.Generic;

namespace PowerLanguage.Strategy 
{
	public class _Test2 : SignalObject
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int LowBreakoutPeriod { get; set; }		//only applies when we use short trading (no stop-loss based on this yet)
		[Input] public int HighBreakoutPeriod { get; set; }		//only applies when we use long trading (no stop-loss based on this yet)
		[Input] public int SuperSmootherPeriod { get; set; }	//used only to exit trades
		[Input] public int VolumePeriod { get; set; }
		//[Input] public int MarketPeriod { get; set; }
		//[Input] public int RelativeStrengthPeriod { get; set; }
		//[Input] public int RelativeStrengthSmoothingPeriod { get; set; }
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		//private EfficiencyRatio m_efficiencyRatio;
		//private RelativeStrength m_relativeStrength;
		public double m_priority;
		private PowerLanguage.Function._SuperSmoother m_instrumentSmoother;
		private VolumeWithSD m_volumeWithSD;
		//private IInstrument m_marketBars;
		//private _SuperSmoother m_marketSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;
		private IOrderMarket m_lx;
		private IOrderMarket m_sx;

		public _Test2(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			LowBreakoutPeriod = DEFAULT_PERIOD;
			HighBreakoutPeriod = DEFAULT_PERIOD;
			SuperSmootherPeriod = DEFAULT_PERIOD;
			VolumePeriod = DEFAULT_PERIOD;
			//MarketPeriod = DEFAULT_PERIOD;
			//RelativeStrengthPeriod = RelativeStrength.LENGTH;
			//RelativeStrengthSmoothingPeriod = RelativeStrength.LENGTH;
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_priority = 0.0d;
			//m_efficiencyRatio = new EfficiencyRatio(this);
			m_instrumentSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_volumeWithSD = new VolumeWithSD(this);
			//m_marketSmoother = new _SuperSmoother(this);
			//m_relativeStrength = new RelativeStrength(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			//m_efficiencyRatio.Length = InstrumentPeriod;
			//m_relativeStrength.Length = RelativeStrengthPeriod;
			//m_relativeStrength.SmoothingLength = RelativeStrengthSmoothingPeriod;
			//m_relativeStrength.Instrument1 = this.BarsOfData(1);
			//m_relativeStrength.Instrument2 = this.BarsOfData(2);
			m_instrumentSmoother.Length = SuperSmootherPeriod;
			m_instrumentSmoother.Price = Bars.Close;
			m_volumeWithSD.Length = VolumePeriod > 0 ? VolumePeriod : VolumeWithSD.DEFAULT_LENGTH;
			//m_marketSmoother.Length = MarketPeriod;
			//m_marketBars = this.BarsOfData(1);
			//m_marketSmoother.Price = m_marketBars.Close;
			m_volumeWithSD.Volume = Bars.Volume;

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
			//m_priority = 1 - m_efficiencyRatio.Value;
			//m_priority = m_relativeStrength.Value;

			//determine whether we should send an entry order
			if (EnableLong &&
				//m_marketBars.Close[0] > m_marketSmoother.Value &&	//market is bullish
				//m_relativeStrength[0] > 1.0d && //instrument is stronger than market
				(VolumePeriod == 0 || Bars.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0]) && //above average volume on breakout
				Bars.Close[0] > Bars.Close.Highest(HighBreakoutPeriod, 1))
				m_le.Send(positionSize());
			else if (EnableShort &&
					 //m_marketBars.Close[0] < m_marketSmoother.Value && //market is bearish
					 //m_relativeStrength[0] < 1.0d && //instrument is weaker than market
					 (VolumePeriod == 0 || Bars.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0]) && //above average volume breakout
					 Bars.Close[0] < Bars.Close.Lowest(LowBreakoutPeriod, 1))
				
				m_se.Send(positionSize());
		}

		protected void calcBarExit()
		{
			//send standard exit order
			if (this.PositionSide > 0 &&
					//(m_marketBars.Close.CrossesUnder(m_marketSmoother[0], 1) || Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1))	//test with market filter
					Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1) //no market filter - in general this is a better exit since the signal would turn bearish most likely before the market does
					)
				m_lx.Send();
			else if (this.PositionSide < 0 &&
							//(m_marketBars.Close.CrossesOver(m_marketSmoother[0], 1) || Bars.Close.CrossesOver(m_instrumentSmoother[0], 1))  //test with market filter
							Bars.Close.CrossesOver(m_instrumentSmoother[0], 1)  //no market filter - in general this is a better exit since the signal would turn bullish most likely before the market does
							)
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
