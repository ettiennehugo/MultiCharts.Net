using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Stochastic with a roofing filter to smooth out noise to make the stochastic more reliable as described by
		/// Dr. John F. Ehlers in Cycle Analytics for Traders p. 78 and 83.
		/// </summary>
		public sealed class ModifiedStochastic : FunctionSeries<System.Double>
		{
			//constants
			public const int DEFAULT_LENGTH = 14;
			public const int DEFAULT_LP_LENGTH = Function.RoofingFilter.DEFAULT_LP_LENGTH;
			public const int DEFAULT_HP_LENGTH = Function.RoofingFilter.DEFAULT_HP_LENGTH;

			//inputs


			//properties
			public int Length { get; set; }		//stochastic length
			public int LowPassLength { get; set; }    //roofing filter low pass length
			public int HighPassLength { get; set; }   //roofing filter high pass length

			//attributes
			private Function.RoofingFilter m_roofingFilter;
			private double m_previousStochastic;

			//interface methods
			public ModifiedStochastic(CStudyControl _master) : base(_master) { }
			public ModifiedStochastic(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{				
				Length = DEFAULT_LENGTH;
				LowPassLength = Function.RoofingFilter.DEFAULT_LP_LENGTH;
				HighPassPeriod = Function.RoofingFilter.DEFAULT_HP_LENGTH;
				m_roofingFilter = new RoofingFilter(this);
			}

			protected override void StartCalc()
			{
				if (LowPassLength <= 0) throw new ArgumentException("Low pass length must be larger than 0.");
				if (HighPassLength <= 0) throw new ArgumentException("High pass length must be larger than 0.");
				if (LowPassLength > HighPassLength) throw new ArgumentException("Low pass length must be less than high pass length.");
				m_roofingFilter.LPLength = LowPassLength;
				m_roofingFilter.HPLength = HighPassLength;
				m_roofingFilter.Price = Bars.Close;
				m_previousStochastic = 0.0d;
			}

			protected override System.Double CalcBar()
			{
				double result = 0.0d;

				if (Bars.Status == EBarState.Close)
				{
					m_stochastic.Value = 0.0d;  //zero the stochastic value till we have enough bars to calculate it off the roofing filter output

					if (Bars.CurrentBar >= Length && Bars.CurrentBar >= LowPassLength)
					{
						double highest = m_roofingFilter.Highest(Length);
						double lowest = m_roofingFilter.Lowest(Length);
						double stochastic = (m_roofingFilter[0] - lowest) / (highest - lowest);
						result = m_roofingFilter.C1 * (stochastic - m_stochasticPrevious) / 2 + m_roofingFilter.C2 * this[1] + m_roofingFilter.C3 * this[2];
						m_previousStochastic = stochastic;
					}
				}

				return result;
			}

			//methods


		}
	}
}