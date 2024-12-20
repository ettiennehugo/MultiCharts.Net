using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Adptive based on the SuperSmoother filter as discussed by Dr John F. Ehlers in Cycle Analytics for Traders p. 31 (https://mesasoftware.com/).
		/// The filter is adaptive based on the efficiency ratio of the price data so as more noise enters the market the filter length is made
		/// longer to allow the price some room to move.
		/// </summary>
		public sealed class _AdaptiveSuperSmoother : FunctionSeries<System.Double>
		{
			//constants
			public const double SQRT_2 = 1.414213562373095d;
			public const int DEFAULT_FAST_PERIOD = 20;
			public const int DEFAULT_SLOW_PERIOD = 50;
			public const int DEFAULT_EFFICIENCY_RATIO_PERIOD = 10;

			//inputs
			public int FastPeriod { get; set; }
			public int SlowPeriod { get; set; }
			public int EfficiencyRatioPeriod { get; set; }
			public IInstrument Instrument { get; set; }

			//properties
			public double A1 { get { return m_a1; } }
			public double B1 { get { return m_b1; } }
			public double C1 { get { return m_c1; } }
			public double C2 { get { return m_c2; } }
			public double C3 { get { return m_c3; } }
			public double EfficiencyRatio { get; private set; }
			public double SmoothingPeriod { get; private set; }

			//attributes
			private double m_result;
			private double m_a1;
			private double m_b1;
			private double m_c1;
			private double m_c2;
			private double m_c3;
			private int m_periodDelta;

			//interface methods
			public _AdaptiveSuperSmoother(CStudyControl _master) : base(_master) { }
			public _AdaptiveSuperSmoother(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				FastPeriod = DEFAULT_FAST_PERIOD;
				SlowPeriod = DEFAULT_SLOW_PERIOD;
				EfficiencyRatioPeriod = DEFAULT_EFFICIENCY_RATIO_PERIOD;
			}

			protected override void StartCalc()
			{
				if (FastPeriod <= 0)
					throw new Exception("AdaptiveSuperSmoother requires a positive integer for FastPeriod.");
				if (SlowPeriod <= 0 || SlowPeriod <= FastPeriod)
					throw new Exception("AdaptiveSuperSmoother requires a positive integer for SlowPeriod and it needs to be larger than FastPeriod.");
				if (EfficiencyRatioPeriod <= 0)
					throw new Exception("AdaptiveSuperSmoother efficiency ratio period must be a positive integer.");
				m_periodDelta = SlowPeriod - FastPeriod;
			}

			protected override double CalcBar()
			{
				if (Instrument.Status == EBarState.Close)
				{
					if (Instrument.CurrentBar < SlowPeriod || Instrument.CurrentBar < EfficiencyRatioPeriod || Instrument.CurrentBar < 4) // 4 bars needed at least for supersmoother
						return Instrument.Close[0];
					else
					{
						//compute efficiency ratio to determine noise level
						EfficiencyRatio = 0;
						for (int i = 0; i < EfficiencyRatioPeriod; i++)
							EfficiencyRatio += Math.Abs(Instrument.Close[i] - Instrument.Close[i + 1]);
						EfficiencyRatio = Math.Abs(Instrument.Close[0] - Instrument.Close[EfficiencyRatioPeriod - 1]) / EfficiencyRatio;

						//compute the smoothing constant
						SmoothingPeriod = (EfficiencyRatio * m_periodDelta) + FastPeriod;

						//compute the coefficients of the SuperSmoother based on the current smoothing length
						m_result = 0;
						m_a1 = Math.Exp(-SQRT_2 * Math.PI / SmoothingPeriod);
						m_b1 = 2.0d * m_a1 * Math.Cos(SQRT_2 * Math.PI / SmoothingPeriod);
						m_c2 = m_b1;
						m_c3 = -m_a1 * m_a1;
						m_c1 = 1.0d - m_c2 - m_c3;
						m_result = m_c1 * (Instrument.Close[0] + Instrument.Close[1]) / 2.0d + m_c2 * this[1] + m_c3 * this[2];
					}
				}

				return m_result;
			}
			
			//methods


		}
	}
}