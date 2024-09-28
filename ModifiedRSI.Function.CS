using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// RSI with a roofing filter to smooth out noise to make the RSI more reliable as described by
		/// Dr. John F. Ehlers in Cycle Analytics for Traders p. 78 and 87.
		/// </summary>
		public sealed class ModifiedRSI : FunctionSeries<System.Double>
		{
			//constants
			public const int DEFAULT_LENGTH = 14;
			public const int DEFAULT_LP_LENGTH = Function.RoofingFilter.DEFAULT_LP_LENGTH;
			public const int DEFAULT_HP_LENGTH = Function.RoofingFilter.DEFAULT_HP_LENGTH;

			//inputs


			//properties
			public int Length { get; set; }						//stochastic length
			public int LowPassLength { get; set; }    //roofing filter low pass length
			public int HighPassLength { get; set; }   //roofing filter high pass length

			//attributes
			private Function.RoofingFilter m_roofingFilter;
			private double m_previousClosesUp;
			private double m_previousDenominator;

			//interface methods
			public ModifiedRSI(CStudyControl _master) : base(_master) { }
			public ModifiedRSI(CStudyControl _master, int _ds) : base(_master, _ds) { }

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
				m_previousClosesUp = 0.0d;
				m_previousDenominator = 0.0d;
			}

			protected override System.Double CalcBar()
			{
				double result = 0.0d;

				if (Bars.Status == EBarState.Close && Bars.CurrentBar >= Length && Bars.CurrentBar >= LowPassLength)
				{
					double closesUp = 0;
					double closesDown = 0;
					for (int i = 0; i < Length - 1; i++)
					{
						double rfCurrent = m_roofingFilter[i];
						double rfPrevious = m_roofingFilter[i + 1];
						if (rfCurrent > rfPrevious) closesUp += rfCurrent + rfPrevious;
						else if (rfCurrent < rfPrevious) closesDown += rfCurrent + rfPrevious;
						double denominator = closesUp + closesDown;
						if (denominator != 0.0d && m_previousDenominator != 0.0d) result = m_roofingFilter.C1 * (closesUp / denominator + m_previousClosesUp / m_previousDenominator) / 2 + m_roofingFilter.C2 * this[1] + m_roofingFilter.C3 * this[2];
						m_previousClosesUp = closesUp;
						m_previousDenominator = denominator;
					}
				}

				return result;
			}

			//methods


		}
	}
}