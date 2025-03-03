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
	public class _TrendFollow1 : _CustomSignal
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;
		public const int DEFAULT_SLOW_PERIOD = 200;
		public const double DEFAULT_GROWTH_PERCENT = 0.3;	//percentage that stock needs to be up over a given growth period
		public const double DEFAULT_DECLINE_PERCENT = -0.3; //percentage that stock needs to be down over a given decline period
		public const int DEFAULT_GROWTH_PERIOD = 120;
		public const int DEFAULT_DECLINE_PERIOD = 120;

		//properties:
		[Input] public int FastPeriod { get; set; } //fast period of the instrument supersmoother
		[Input] public int SlowPeriod { get; set; } //slow period of the instrument supersmoother - used to determine bullishness or bearishness of instrument the trend
		[Input] public double GrowthPercent { get; set; } // growth percentage required before we consider going long
		[Input] public int GrowthPeriod { get; set; }	//growth period to check for momentum on the stock
		[Input] public double DeclinePercent { get; set; } // decline percentage required before we consider going long
		[Input] public int DeclinePeriod { get; set; }	// decline period to check for momentum on the stock		
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public int VolumePeriod { get; set; }
		[Input] public double MaxVolumeBasedPosPercent { get; set; }
		[Input] public int MarketPeriod { get; set; }		//market period used to avoid entries that moves against the market trend.
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		private PowerLanguage.Function._EfficiencyRatio m_efficiencyRatio;
		private PowerLanguage.Function._SuperSmoother m_instrumentFastSmoother;
		private PowerLanguage.Function._SuperSmoother m_instrumentSlowSmoother;
		private PowerLanguage.Function._VolumeWithSD m_instrumentVolume;
		private IInstrument m_marketInstrument;
		private PowerLanguage.Function._SuperSmoother m_marketSmoother;
		private IOrderPriced m_le;
		private IOrderPriced m_se;

		public _TrendFollow1(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			FastPeriod = DEFAULT_PERIOD;
			SlowPeriod = DEFAULT_SLOW_PERIOD;
			GrowthPercent = DEFAULT_GROWTH_PERCENT;
			GrowthPeriod = DEFAULT_GROWTH_PERIOD; 
			DeclinePercent = DEFAULT_DECLINE_PERCENT;
			DeclinePeriod = DEFAULT_DECLINE_PERIOD; 
			RiskBasedPositionSizing = true;
			MaxRiskEquityPerPosPercent = 0.01;		//make zero or less to use portfolio percentage
			VolumePeriod = DEFAULT_PERIOD;
			MaxVolumeBasedPosPercent = 0.15;		//make zero or less to only use risk or portfolio percent
			MarketPeriod = DEFAULT_PERIOD;
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_instrumentFastSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_instrumentSlowSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_instrumentVolume = new _VolumeWithSD(this);
			m_marketSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_efficiencyRatio = new PowerLanguage.Function._EfficiencyRatio(this);

			//create order types
			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TF1LE", EOrderAction.Buy));
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TF1SE", EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			if (VolumePeriod <= 0) throw new Exception("VolumePeriod must be greater than zero.");
			if (FastPeriod >= SlowPeriod) throw new Exception("FastPeriod must be less than SlowPeriod.");
			if (MaxRiskEquityPerPosPercent > 1.0d) throw new Exception("MaxRiskEquityPerPosPercent must be less than 1.0.");
			if (MaxVolumeBasedPosPercent > 1.0d) throw new Exception("MaxVolumeBasedPosPercent must be less than 1.0.");
			if (GrowthPercent < 0) throw new Exception("GrowthPercent should be zero (off) or more than zero.");
			if (GrowthPeriod < 0) throw new Exception("GrowthPeriod should be zero (off) or more than zero.");
			if (DeclinePercent > 0) throw new Exception("DeclinePercent should be zero (off) or less than zero.");
			if (DeclinePeriod < 0) throw new Exception("DeclinePeriod should be zero (off) or more than zero.");
			m_marketInstrument = BarsOfData(2);
			m_instrumentFastSmoother.Length = FastPeriod;
			m_instrumentFastSmoother.Instrument = Bars;
			m_instrumentSlowSmoother.Length = SlowPeriod;
			m_instrumentSlowSmoother.Instrument = Bars;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Instrument = Bars;
			m_marketSmoother.Length = MarketPeriod != 0 ? MarketPeriod : _SuperSmoother.DEFAULT_PERIOD;
			m_marketSmoother.Instrument = m_marketInstrument;
			m_efficiencyRatio.Length = FastPeriod;
			m_efficiencyRatio.Instrument = Bars;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Instrument = Bars;
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
			
			//process the entry logic
			//determine whether we should send an entry order
			if (EnableLong &&
				(MarketPeriod <= 0 || m_marketSmoother[0] < m_marketInstrument.Close[0]) &&	//market is bullish
				m_instrumentFastSmoother[0] > m_instrumentSlowSmoother[0] &&  //instrument is bullish
				Bars.Close.CrossesOver(m_instrumentFastSmoother[0], 1) &&
				(GrowthPercent == 0.0d || GrowthPeriod == 0.0d || (Bars.CurrentBar > GrowthPeriod && Bars.Close[GrowthPeriod] != 0.0d && (Bars.Close[0] / Bars.Close[GrowthPeriod]) - 1 >= GrowthPercent)))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentFastSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_le.Send(Bars.Close[0], size);
			}
			else if (EnableShort && 
				     (MarketPeriod <= 0 || m_marketSmoother[0] > m_marketInstrument.Close[0]) &&	//market is bearish
					 m_instrumentFastSmoother[0] < m_instrumentSlowSmoother[0] &&  //instrument is bearish
					 Bars.Close.CrossesUnder(m_instrumentFastSmoother[0], 1) &&
					 (DeclinePercent == 0.0d || DeclinePeriod == 0.0d || (Bars.CurrentBar > DeclinePeriod && Bars.Close[DeclinePeriod] != 0.0d && 1 - (Bars.Close[0] / Bars.Close[DeclinePeriod]) <= DeclinePercent)))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentFastSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_se.Send(Bars.Close[0], size);
			}
		}
	}
}