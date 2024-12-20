using PowerLanguage.Indicator;
using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Buys the pullback on the strongest stocks (reversal trade) in the market when the market is overextended to the down side. Both the stocks and the
		/// market in general should be bullish.
		/// https://www.youtube.com/watch?v=WY_5BV3BmkA
		///	Required Feeds:
		///		1. Entry data feed - typically some minute data feed.
		///		2. Scanner data feed - typically daily data feed for the same instrument.
		///		3. Market data feed - typically daily data feed for the market (same resolution as 2).
		/// Entry:
		///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
		///		* Market is currently down x-percent from the high in the past y-days and the stock is also down at least x-percent from the high in the past y-days.
		///		* Optionally - wait for an x-percent gap down on the open - 0 switch it off.
		///		* Waits for the opening range bar to form on the entry data feed.
		///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
		///	Notes:
		///	  * The market and the stock should be bullish in general but we can not use it as a filter as we want to try and buy pullbacks that will most of the time
		///	    coincide with down periods in the market. Therefore when using this indicator in a strategy the stop-loss would be important to ensure entries on an actual
		///	    bearish market.
		/// </summary>
		public sealed class _CapitulationReversal : FunctionSimple<System.Boolean>
		{
			//constants
			public const double DEFAULT_MINIMUM_GROWTH_PERCENT = 0.3; //30% growth required
			public const int DEFAULT_GROWTH_DAYS = 60;
			public const int DEFAULT_SCAN_LENGTH = 40;
			public const double DEFAULT_STOCK_DOWN_PERCENT = 0.1d;  //market down 10% from the high
			public const double DEFAULT_MARKET_DOWN_PERCENT = 0.08d;  //market down 8% from the high
			public const double DEFAULT_GAP_DOWN_PERCENT = 0.07d;   //stock gap down on entry day by at least 7%
			public const int DEFAULT_FAST_SSMA = 10;          //additional supersmoother cross filter to check that stock is not declining
			public const int DEFAULT_SLOW_SSMA = 100;         //additional supercsmoother filter to check that stock is not declining

			//properties
			public double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
			public int GrowthDays { get; set; }                //number of days to look back for the growth
			public int ScanLength { get; set; }                //lookback length for the market high
			public double StockDownPercent { get; set; }       //market down percent from the high in the past y-days
			public double MarketDownPercent { get; set; }      //market down percent from the high in the past y-days
			public double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down
			public IInstrument Instrument { get; set; }        //larger timeframe instrument bar data, e.g. daily, to determine pullback state
			public IInstrument Market { get; set; }            //market instrument bar data, e.g. daily, to determine market pullback state

			//attributes
			private double m_stockDownPercent;
			private double m_marketDownPercent;
			private Function._SuperSmoother m_fastSSMA;
			private Function._SuperSmoother m_slowSSMA;

			//interface methods
			public _CapitulationReversal(CStudyControl _master) : base(_master) { }
			public _CapitulationReversal(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumGrowthPercent = DEFAULT_MINIMUM_GROWTH_PERCENT;
				GrowthDays = DEFAULT_GROWTH_DAYS;
				ScanLength = DEFAULT_SCAN_LENGTH;
				StockDownPercent = DEFAULT_STOCK_DOWN_PERCENT;
				MarketDownPercent = DEFAULT_MARKET_DOWN_PERCENT;
				GapDownPercent = DEFAULT_GAP_DOWN_PERCENT;
				m_fastSSMA = new Function._SuperSmoother(this);
				m_slowSSMA = new Function._SuperSmoother(this);
			}

			protected override void StartCalc()
			{
				if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
				if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
				if (ScanLength <= 0) throw new Exception("Scan length must be greater than zero.");
				if (StockDownPercent <= 0.0d || StockDownPercent >= 1.0d) throw new Exception("Stock down percent must be greater than zero but less than 1.0.");
				if (MarketDownPercent <= 0.0d || MarketDownPercent >= 1.0d) throw new Exception("Market down percent must be greater than zero but less than 1.0.");
				if (GapDownPercent < 0.0d || GapDownPercent >= 1.0d) throw new Exception("Gap Down percent must be greater or equal to zero but less than 1.0.");
				m_stockDownPercent = 1 - StockDownPercent;
				m_marketDownPercent = 1 - MarketDownPercent;
				m_fastSSMA.Price = Market.Close;
				m_fastSSMA.Length = DEFAULT_FAST_SSMA;
				m_slowSSMA.Price = Market.Close;
				m_slowSSMA.Length = DEFAULT_SLOW_SSMA;
			}

			protected override System.Boolean CalcBar()
			{
				if (Environment.CalcReason == CalculationReason.Default)
				{
					//scanner criteria
					if (Instrument.CurrentBar <= GrowthDays || Instrument.CurrentBar <= ScanLength /*|| Instrument.CurrentBar <= DEFAULT_FAST_SSMA || Instrument.CurrentBar <= DEFAULT_SLOW_SSMA*/) return false; //not enough data

					//NOTE: We need to get the highest close over the scan length as the growth percentage as the pullback will "eat" into that growth
					if ((Instrument.Close.Highest(ScanLength) / Instrument.Close[GrowthDays - 1]) - 1 < MinimumGrowthPercent) return false;  //not enough growth

					if (m_fastSSMA[0] < m_slowSSMA[0]) return false;  //market is not bullish
					//if (Instrument.Close[0] > m_fastSSMA[0] || Instrument.Close[0] < m_slowSSMA[0]) return false; //bar should be between the fast and slow SSMA
					if (Market.Close[0] > Market.High.Highest(ScanLength) * m_marketDownPercent) return false; //market is not down by required percentage
					if (Instrument.Close[0] > Instrument.High.Highest(ScanLength) * m_stockDownPercent) return false; //stock is not down by required percentage
					if (GapDownPercent > 0.0d)
					{
						double gapPercent = (Instrument.Open[0] - Instrument.Close[1]) / Instrument.Close[1];
						if (gapPercent >= 0.0d || Math.Abs(gapPercent) < GapDownPercent) return false; //gap down on the open is not negative or not large enough
					}
					return true;  //all criteria met - strategies need to decide on entry criteria based on this signal
				}

				return false;
			}

			//methods


		}
	}
}