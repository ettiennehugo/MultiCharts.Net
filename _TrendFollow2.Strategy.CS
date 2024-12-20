using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Trend following strategy using a supersmoother cross. Need to be paired with good StopsAndExits strategy to make good money.
	/// Needs multiple data stream inputs.
	/// </summary>
	public class _TrendFollow2 : _CustomSignal
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;
		public const int DEFAULT_SLOW_PERIOD = 200;

		//properties:
		[Input] public int FastPeriod { get; set; } //fast period of the instrument supersmoother
		[Input] public int SlowPeriod { get; set; } //slow period of the instrument supersmoother - used to determine bullishness or bearishness of instrument the trend
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public int VolumePeriod { get; set; }
		[Input] public double MaxVolumeBasedPosPercent { get; set; }
		[Input] public int MarketPeriod { get; set; }		//market period used to avoid entries that moves against the market trend.
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }
		[Input] public bool EnableExit { get; set; }	//handle exit for the positions, disable if some other exit strategy is used

		//attributes:
		private PowerLanguage.Function._EfficiencyRatio m_efficiencyRatio;
		private PowerLanguage.Function._SuperSmoother m_instrumentFastSmoother;
		private PowerLanguage.Function._SuperSmoother m_instrumentSlowSmoother;
		private PowerLanguage.Function.VolumeWithSD m_instrumentVolume;
		private IInstrument m_marketInstrument;
		private PowerLanguage.Function._SuperSmoother m_marketSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;
		private IOrderMarket m_lx;
		private IOrderMarket m_sx;

		public _TrendFollow2(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			FastPeriod = DEFAULT_PERIOD;
			SlowPeriod = DEFAULT_SLOW_PERIOD;
			RiskBasedPositionSizing = true;
			MaxRiskEquityPerPosPercent = 0.01;		//make zero or less to use portfolio percentage
			VolumePeriod = DEFAULT_PERIOD;
			MaxVolumeBasedPosPercent = 0.15;		//make zero or less to only use risk or portfolio percent
			MarketPeriod = DEFAULT_PERIOD;
			EnableLong = true;
			EnableShort = true;
			EnableExit = true;

			//create functions used
			m_instrumentFastSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_instrumentSlowSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_instrumentVolume = new VolumeWithSD(this);
			m_marketSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_efficiencyRatio = new PowerLanguage.Function._EfficiencyRatio(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF2LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF2SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF2LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF2SX", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			if (VolumePeriod <= 0) throw new Exception("VolumePeriod must be greater than zero.");
			if (FastPeriod >= SlowPeriod) throw new Exception("FastPeriod must be less than SlowPeriod.");
			if (MaxRiskEquityPerPosPercent > 1.0d) throw new Exception("MaxRiskEquityPerPosPercent must be less than 1.0.");
			if (MaxVolumeBasedPosPercent > 1.0d) throw new Exception("MaxVolumeBasedPosPercent must be less than 1.0.");
			m_marketInstrument = BarsOfData(2);
			m_instrumentFastSmoother.Length = FastPeriod;
			m_instrumentFastSmoother.Price = Bars.Close;
			m_instrumentSlowSmoother.Length = SlowPeriod;
			m_instrumentSlowSmoother.Price = Bars.Close;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Volume = Bars.Volume;
			m_marketSmoother.Length = MarketPeriod != 0 ? MarketPeriod : _SuperSmoother.DEFAULT_PERIOD;
			m_marketSmoother.Price = m_marketInstrument.Close;
			m_efficiencyRatio.Length = FastPeriod;
			m_efficiencyRatio.Instrument = Bars;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Volume = Bars.TrueVolume();
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
			
			calcBarEntry();
			if (EnableExit) calcBarExit();
		}
		
		protected void calcBarEntry()
		{
			//determine whether we should send an entry order
			if (EnableLong &&
				(MarketPeriod <= 0 || m_marketSmoother[0] < m_marketInstrument.Close[0]) &&	//market is bullish
				m_instrumentFastSmoother[0] > m_instrumentSlowSmoother[0] &&  //instrument is bullish
				Bars.Close.CrossesOver(m_instrumentFastSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentFastSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_le.Send(size);
			}
			else if (EnableShort && 
				     (MarketPeriod <= 0 || m_marketSmoother[0] > m_marketInstrument.Close[0]) &&	//market is bearish
						 m_instrumentFastSmoother[0] < m_instrumentSlowSmoother[0] &&  //instrument is bearish
						 Bars.Close.CrossesUnder(m_instrumentFastSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentFastSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_se.Send(size);
			}
		}

		protected void calcBarExit()
		{
			if (this.PositionSide > 0 && Bars.Close.CrossesUnder(m_instrumentFastSmoother[0], 1))
				m_lx.Send();
			else if (this.PositionSide < 0 && Bars.Close.CrossesOver(m_instrumentFastSmoother[0], 1))
				m_sx.Send();
		}
		
	}
}