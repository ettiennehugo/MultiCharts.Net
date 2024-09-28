using FileHelpers;
using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{

		//General types shared among indicators
		public enum EScanBullishBearish
		{
			BullishAndBearish,
			Bullish,
			Bearish,
		}

		public enum ECurrentBarDirection
		{
			None,   //bullish/bearish criteria not met
			Bullish,
			Bearish,
			BullishLowThreshold,
			BearishLowThreshold,
		}

		/// <summary>
		/// Rules:
		/// * Minimum volume over 3-days > 100 000
		/// * Close > $1
		/// * Bullish:
		///   - 7MA / 65MA > 1.05 (5% up) - trade intensity filter
		///   - Close > Open
		///   - Close[0] / Close[1] > Close[1] / Close[2] - saw a price increase
		///   - Close[1] / Close[2] < 1.02 (2% increase) - did not see a huge price increase before
		/// * Bearish:
		///   - 7MA / 65MA < 0.95 (5% down) - trade intensity filter
		///   - Close < Open
		///   - Close[0] / Close[1] < Close[1] / Close[2] - saw a price increase
		///   - Close[1] / Close[2] < 0.98 (2% decrease) - did not see a huge price decrease before
		///
		/// TODO:
		/// 
		/// </summary>
		public sealed class _ScannerLowThresholdBreakout : FunctionSimple<System.Boolean>
		{
			//constants
			public const EScanBullishBearish SCAN_BULLISH_BEARISH = EScanBullishBearish.BullishAndBearish;
			public const int TRADE_INTENSITY_SHORT_PERIOD = 7;
			public const int TRADE_INTENSITY_LONG_PERIOD = 65;
			public const double TRADE_INTENSITY_PERCENT = 5.0;
			public const double PREVIOUS_DAY_INCREASE_PERCENT = 2.0;
			public const double MINIMUM_PRICE = 10.0;
			public const int MINIMUM_AVERAGE_VOLUME = 100000;
			public const int AVERAGE_VOLUME_LENGTH = 3;
			public const bool HISTORICAL = false;
			public const bool DEBUG = false;

			//inputs
			[Input] public EScanBullishBearish ScanBullishBearish { get; set; }  //market direction to scan
			[Input] public int TradeIntensityShortPeriod { get; set; }  //short period for the trade intensity filter
			[Input] public int TradeIntensityLongPeriod { get; set; }  //short period for the trade intensity filter
			[Input] public double TradeIntensityPercent { get; set; } //percentage for trade intensity
			[Input] public double PreviousDayIncreasePercent { get; set; }  //percentage increase for pervious trading day to limit how much price can move before the low threshold detection
			[Input] public double MinimumPrice { get; set; }          //minimum price for stock
			[Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
			[Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
			[Input] public bool Historical { get; set; }              //allow historical scanning so if ever the criteria is met the scanner will output true to allow chart analysis
			[Input] public bool Debug { get; set; }

			//properties
			public bool CriteriaMet { get { return m_criteriaMet; } }
			public bool LowThresholdCriteria { get { return m_lowThresholdCriteria; } }
			public bool MinimumPriceCriteria { get { return m_minimumPriceCriteria; } }
			public bool VolumeCriteria { get { return m_volumeCriteria; } }
			public bool PriceLiquidityCriteria { get { return m_priceLiquidityCriteria; } }
			public ECurrentBarDirection CurrentBarDirection { get { return m_currentBarDirection; } }

			//attributes
			private AverageFC m_shortMA;
			private AverageFC m_longMA;
			private bool m_criteriaMet;
			private bool m_lowThresholdCriteria;
			private bool m_minimumPriceCriteria;
			private bool m_volumeCriteria;
			private bool m_priceLiquidityCriteria;
			private bool m_historicalCriteria;
			private ECurrentBarDirection m_currentBarDirection;
			private double m_tradeIntensityPercent;
			private double m_bullishTradeIntensity;
			private double m_bearishTradeIntensity;
			private double m_previousDayIncreasePercent;
			private double m_bullishPreviousDayDecreasePercent;
			private double m_bearishPreviousDayDecreasePercent;
			private int m_firstBarToCheck;

			public _ScannerLowThresholdBreakout(CStudyControl _master) : base(_master) { }
			public _ScannerLowThresholdBreakout(CStudyControl _master, int _ds) : base(_master, _ds) { }


			protected override void Create()
			{
				//default inputs
				ScanBullishBearish = SCAN_BULLISH_BEARISH;
				TradeIntensityShortPeriod = TRADE_INTENSITY_SHORT_PERIOD;
				TradeIntensityLongPeriod = TRADE_INTENSITY_LONG_PERIOD;
				TradeIntensityPercent = TRADE_INTENSITY_PERCENT;
				PreviousDayIncreasePercent = PREVIOUS_DAY_INCREASE_PERCENT;
				MinimumPrice = MINIMUM_PRICE;
				MinimumAverageVolume = MINIMUM_AVERAGE_VOLUME;
				AverageVolumeLength = AVERAGE_VOLUME_LENGTH;
				Historical = HISTORICAL;
				Debug = DEBUG;

				//create indicators, plots
				m_shortMA = new AverageFC(this);
				m_longMA = new AverageFC(this);
			}

			protected override void StartCalc()
			{
				//validate inputs
				if (TradeIntensityShortPeriod <= 0) throw new Exception("Trade intensity short period must be positive.");
				if (TradeIntensityLongPeriod <= 0) throw new Exception("Trade intensity long period must be positive.");
				if (TradeIntensityShortPeriod > TradeIntensityLongPeriod) throw new Exception("Trade intensity short period must be smaller than long period.");
				if (TradeIntensityPercent <= 0) throw new Exception("Trade intensity percentage must be positive.");
				if (PreviousDayIncreasePercent <= 0) throw new Exception("Previous day increase percent must be positive.");
				if (MinimumPrice <= 0) throw new Exception("Minimum price must be positive.");
				if (MinimumAverageVolume <= 0) throw new Exception("Minimum average volume must be positive.");
				if (AverageVolumeLength <= 0) throw new Exception("Average volume length must be positive.");

				//default get properties
				m_criteriaMet = false;
				m_lowThresholdCriteria = false;
				m_minimumPriceCriteria = false;
				m_volumeCriteria = false;
				m_priceLiquidityCriteria = false;
				m_historicalCriteria = false;
				m_currentBarDirection = ECurrentBarDirection.None;

				//compute general values based off inputs
				m_tradeIntensityPercent = TradeIntensityPercent / 100.0;
				m_bullishTradeIntensity = 1.0 + m_tradeIntensityPercent;
				m_bearishTradeIntensity = 1.0 - m_tradeIntensityPercent;
				m_previousDayIncreasePercent = PreviousDayIncreasePercent / 100.0;
				m_bullishPreviousDayDecreasePercent = 1.0 + m_previousDayIncreasePercent;
				m_bearishPreviousDayDecreasePercent = 1.0 - m_previousDayIncreasePercent;

				//setup indicators
				m_shortMA.price = Bars.Close;
				m_shortMA.length = TradeIntensityShortPeriod;
				m_longMA.price = Bars.Close;
				m_longMA.length = TradeIntensityLongPeriod;

				//determine the first bar to check
				m_firstBarToCheck = TradeIntensityLongPeriod >= AverageVolumeLength ? TradeIntensityLongPeriod : AverageVolumeLength;
				m_firstBarToCheck = m_firstBarToCheck < 2 ? 2 : m_firstBarToCheck;  //need at least two bars based on calculations below

				//clear debug output
				if (Debug) Output.Clear();
			}

			protected override System.Boolean CalcBar()
			{
				if (Bars.CurrentBar > m_firstBarToCheck && Environment.CalcReason == CalculationReason.Default)
				{
					if (Historical || Bars.LastBarOnChart)
					{
						//default criteria
						m_criteriaMet = false;
						m_lowThresholdCriteria = false;
						m_minimumPriceCriteria = false;
						m_volumeCriteria = false;
						m_priceLiquidityCriteria = false;
						m_currentBarDirection = ECurrentBarDirection.None;

						m_minimumPriceCriteria = PublicFunctions.DoubleGreater(Bars.Close[0], MinimumPrice);
						m_volumeCriteria = PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(AverageVolumeLength), MinimumAverageVolume);

						if (ScanBullishBearish == EScanBullishBearish.BullishAndBearish || ScanBullishBearish == EScanBullishBearish.Bullish)
						{
							/// * Bullish:
							///   - 7MA / 65MA > 1.05 (5% up) - trade intensity filter
							///   - Close > Open
							///   - Close[0] / Close[1] > Close[1] / Close[2] - saw a price increase
							///   - Close[1] / Close[2] < 1.02 (2% increase) - did not see a huge price increase before
							m_lowThresholdCriteria = PublicFunctions.DoubleGreater(Bars.Close[0], Bars.Open[0]) && PublicFunctions.DoubleGreater(m_shortMA.Value / m_longMA.Value, m_bullishTradeIntensity) &&
																		 PublicFunctions.DoubleGreater(Bars.Close[0] / Bars.Close[1], Bars.Close[1] / Bars.Close[2]) && PublicFunctions.DoubleGreater(Bars.Close[1] / Bars.Close[2], m_bullishPreviousDayDecreasePercent);
							if (m_lowThresholdCriteria) m_currentBarDirection = ECurrentBarDirection.BullishLowThreshold;
						}

						if (ScanBullishBearish == EScanBullishBearish.BullishAndBearish || ScanBullishBearish == EScanBullishBearish.Bearish)
						{
							/// * Bearish:
							///   - 7MA / 65MA < 0.95 (5% down) - trade intensity filter
							///   - Close < Open
							///   - Close[0] / Close[1] < Close[1] / Close[2] - saw a price increase
							///   - Close[1] / Close[2] < 0.98 (2% decrease) - did not see a huge price decrease before
							bool bullishDirectionNotMet = !m_lowThresholdCriteria;
							m_lowThresholdCriteria |= PublicFunctions.DoubleLess(Bars.Close[0], Bars.Open[0]) && PublicFunctions.DoubleLess(m_shortMA.Value / m_longMA.Value, m_bearishTradeIntensity) &&
																			PublicFunctions.DoubleLess(Bars.Close[0] / Bars.Close[1], Bars.Close[1] / Bars.Close[2]) && PublicFunctions.DoubleLess(Bars.Close[1] / Bars.Close[2], m_bearishPreviousDayDecreasePercent);
							if (m_lowThresholdCriteria && bullishDirectionNotMet) m_currentBarDirection = ECurrentBarDirection.BearishLowThreshold;
						}

						//determine liquidity criteria
						m_priceLiquidityCriteria = PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(AverageVolumeLength), MinimumAverageVolume);

						//determine general criteria and historical criteria
						m_criteriaMet = m_lowThresholdCriteria && m_volumeCriteria && m_minimumPriceCriteria && m_priceLiquidityCriteria;

						//output debug information
						if (Debug)
							Output.WriteLine("{0} - lowThresholdCriteria({1}) liquidityCriteria({2}) volumeCriteria({3}) minimumPriceCriteria({4})", Bars.Time[0], m_lowThresholdCriteria, m_priceLiquidityCriteria, m_volumeCriteria, m_minimumPriceCriteria);
					}

					//set the historical criteria flag
					m_historicalCriteria = Historical && !m_historicalCriteria ? m_criteriaMet : m_historicalCriteria;
				}

				// function logic
				return m_historicalCriteria;
			}
		}
	}
}