using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Implementation of Kaufman's Adaptive Moving Average. See Trading Systems and Methods, Perry Kaufman, Edition 5, p. 779.
		/// 
		/// Notes:
		///   * Using efficiency ratio lengths around 10 works best, using larger values will only scale the efficiency ratio smaller but
		///     will not add any value to the function as a whole (this is under the assumption that you're working on the daily timeframe).
		///   * Keep the slow length 30 but change the fast length to be larger than 2 if you want to make the trendline less sensitive.
		/// 
		/// TODO:
		///   * See that "Practical Matters" section on p.782, there are two filters that need to be added to resolve some issues.
		///     - Filter 1: Need to add a filter that would keep the smoothing constant the same if the price moves only by a very small amount every say 5-bars.
		///     - Filter 2: When minor price penetrations of the KAMA occurs and price moves around it you want it to move sideways and not change direction, a
		///                 filter must be added that would keep the trend line constant if it only changes by say 0.1 standard deviations of the trend line values
		///                 so far.
		/// 
		/// </summary>		
		public sealed class KAMA : FunctionSeries<System.Double>
		{
			//Constants:
			const int DEFAULT_FAST_LENGTH = 2;
			const int DEFAULT_SLOW_LENGTH = 30;
			const int DEFAULT_ER_LENGTH = 10;
			const int DEFAULT_MOMENTUM_LENGTH = 1;

			//Attributes:
			public ISeries<double> Price { get; set; }
			public int FastLength { get; set; }
			public int SlowLength { get; set; }
			public int EfficiencyRatioLength { get; set; }    //advisable length is 8-10 bars
			public int MomentumLength { get; set; }
			public double EfficiencyRatio { get; private set; }
			public double SmoothingConstant { get; private set; }
			public double Momentum { get; private set; }
			private double fastSmoothing;
			private double slowSmoothing;
			private double result;

			public KAMA(CStudyControl _master) : base(_master) { }
			public KAMA(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				FastLength = DEFAULT_FAST_LENGTH;
				SlowLength = DEFAULT_SLOW_LENGTH;
				EfficiencyRatioLength = DEFAULT_ER_LENGTH;
				MomentumLength = DEFAULT_MOMENTUM_LENGTH;
			}

			protected override void StartCalc()
			{
				//validate input values
				if (FastLength <= 0)
					throw new Exception("KAMA fast length must be a positive integer.");
				if (SlowLength <= 0)
					throw new Exception("KAMA slow length must be a positive integer.");
				if (FastLength >= SlowLength)
					throw new Exception("KAMA fast length must be less than slow length.");
				if (EfficiencyRatioLength <= 0)
					throw new Exception("KAMA efficiency ratio length must be a positive integer.");

				//compute the fast and slow smoothing values once-off
				fastSmoothing = 2 / (double)(FastLength + 1);
				slowSmoothing = 2 / (double)(SlowLength + 1);
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close)
					//check whether we have enough bars to compute the function, we need at least efficienty ratio length bars
					//to compute an output
					if (Bars.CurrentBar < EfficiencyRatioLength)
						result = Price[0];
					else
					{
						//compute the efficiency ratio of the price movement
						EfficiencyRatio = 0;
						for (int i = 0; i < EfficiencyRatioLength; i++)
							EfficiencyRatio += Math.Abs(Price[i] - Price[i + 1]);
						EfficiencyRatio = Math.Abs(Price[0] - Price[EfficiencyRatioLength - 1]) / EfficiencyRatio;

						//compute the smoothing constant
						SmoothingConstant = EfficiencyRatio * (fastSmoothing - slowSmoothing) + slowSmoothing;
						SmoothingConstant = SmoothingConstant * SmoothingConstant;  //previous result must be squared

						//compute the new KAMA value
						double previous = this[1];
						result = previous + (SmoothingConstant * (Price[0] - previous));

						//update the momentum value
						Momentum = result - this[MomentumLength];
					}

				return result;
			}
		}
	}
}