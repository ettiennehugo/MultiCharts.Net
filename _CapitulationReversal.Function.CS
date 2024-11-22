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
		///   * Market must be bulish.
		///		* Market is currently down x-percent from the high in the past y-days and the stock is also down at least x-percent from the high in the past y-days.
		///		* Optionally - wait for an x-percent gap down on the open - 0 switch it off.
		///		* Waits for the opening range bar to form on the entry data feed.
		///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
		/// </summary>
		public sealed class _CapitulationReversal : FunctionSimple<System.Boolean>
		{
			//constants
			public const double DEFAULT_MINIMUM_GROWTH_PERCENT = 0.3;	//30% growth required
			public const int DEFAULT_GROWTH_DAYS = 120;
			public const int DEFAULT_SCAN_LENGTH = 20;
			public const int DEFAULT_MARKET_BULLISH_LENGTH = 200;
			public const double DEFAULT_DOWN_PERCENT = 0.1d;          //stock/market down 10% from the high
			public const double DEFAULT_GAP_DOWN_PERCENT = 0.07d;			//stock gap down on entry day by at least 7%

			//properties
			public double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
			public int GrowthDays { get; set; }                //number of days to look back for the growth
			public int ScanLength { get; set; }                //lookback length for the market high
			public int MarketBullishLength { get; set; }       //length used to determine if the market is bullish
			public double DownPercent { get; set; }            //market down percent from the high in the past y-days
			public double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down
			public IInstrument Instrument { get; set; }        //larger timeframe instrument bar data, e.g. daily, to determine pullback state
			public IInstrument Market { get; set; }            //market instrument bar data, e.g. daily, to determine market pullback state

			//attributes


			//interface methods
			public _CapitulationReversal(CStudyControl _master) : base(_master) { }
			public _CapitulationReversal(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumGrowthPercent = DEFAULT_MINIMUM_GROWTH_PERCENT;
				GrowthDays = DEFAULT_GROWTH_DAYS;
				MarketBullishLength = DEFAULT_MARKET_BULLISH_LENGTH;
				ScanLength = DEFAULT_SCAN_LENGTH;
				DownPercent = DEFAULT_DOWN_PERCENT;
				GapDownPercent = DEFAULT_GAP_DOWN_PERCENT;
			}

			protected override void StartCalc()
			{
				if (MinimumGrowthPercent <= 0) throw new Exception("Minimum growth percent must be greater than zero.");
				if (GrowthDays <= 0) throw new Exception("Growth days must be greater than zero.");
				if (ScanLength <= 0) throw new Exception("Scan length must be greater than zero.");
				if (DownPercent <= 0.0d || DownPercent >= 1.0d) throw new Exception("Down percent must be greater than zero but less than 1.0.");
				if (GapDownPercent < 0.0d || GapDownPercent >= 1.0d) throw new Exception("Gap Down percent must be greater or equal to zero but less than 1.0.");
			}

			protected override System.Boolean CalcBar()
			{
				switch (Environment.CalcReason)
				{
					case CalculationReason.Default:
						//scanner criteria
						if (Instrument.CurrentBar <= GrowthDays) return false; //not enough data
						if (Instrument.Close[0] / Instrument.Close[GrowthDays] < MinimumGrowthPercent) return false;  //not enough growth
						if (Market.Close[0] < Market.Close.Average(MarketBullishLength)) return false; //market is not bullish
						if (Market.Close[0] > Market.High.Highest(ScanLength) * DownPercent) return false; //market is not down by required percentage
						if (Instrument.Close[0] > Instrument.High.Highest(ScanLength) * DownPercent) return false; //stock is not down by required percentage
						if (GapDownPercent > 0.0d && (Instrument.Open[0] - Instrument.Close[1]) / Instrument.Close[1] > GapDownPercent) return false; //gap down on the open
						return true;  //all criteria met - strategies need to decide on entry criteria based on this signal
				}
				
				return false;
			}

			//methods


		}
	}
}