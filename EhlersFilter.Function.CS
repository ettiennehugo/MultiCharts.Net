using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Implements a basic Ehlers Filter as discussed by Dr. John Ehlers (https://mesasoftware.com/). 
		/// The idea is that the filter responds better than a moving average while being irresponsive when
		/// the market is not moving thus ignoring noise.
		/// </summary>
		public sealed class EhlersFilter : FunctionSimple<System.Double>
		{
			//Constants:
			public const int DEFAULT_LENGTH = 15;
			public const ECoefficientStrategy DEFAULT_COEFFICIENT_STRATEGY = ECoefficientStrategy.Momentum;
			public const int DEFAULT_MOMENTUM_LENGTH = 5;

			//Types:

			//TODO: Implement different strategies to determine the coefficients.

			public enum ECoefficientStrategy
			{
				Momentum,
			}

			//Inputs:
			public IInstrument Instrument { get; set; }            //Price on which filter should operate.
			public int Length { get; set; }                       //Number of price entries to process for the filter.
			public ECoefficientStrategy CoeffientStrategy;        //Strategy used to compute the coefficients for the filter.
			public int MomentumLength;                            //Length of momentum when momentum strategy is used.

			//Attributes:
			private List<double> _coefficients;
			private int _rampUpLength;
			private double _result;

			public EhlersFilter(CStudyControl _master) : base(_master) { }
			public EhlersFilter(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				_result = 0.0;
				Length = DEFAULT_LENGTH;
				CoeffientStrategy = DEFAULT_COEFFICIENT_STRATEGY;
				MomentumLength = DEFAULT_MOMENTUM_LENGTH;
			}

			protected override void StartCalc()
			{
				//create the set of coefficients to use
				_coefficients = new List<double>();
				_rampUpLength = Length;

				switch (CoeffientStrategy)
				{
					case ECoefficientStrategy.Momentum:
						_rampUpLength += MomentumLength;
						break;
				}
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close)
				{
					//compute the coefficients based on the selected strategy
					_coefficients.Clear();
					switch (CoeffientStrategy)
					{
						case ECoefficientStrategy.Momentum:
							momentumCoefficients();
							break;
					}

					//compute the Ehlers filter value
					double numerator = 0.0;
					double denominator = 0.0;

					for (int i = 0; i < Length; i++)
					{
						//double price = (Instrument.High[i] + Instrument.Low[i] + Instrument.Close[i]) / 3;
						double price = (Instrument.High[i] + Instrument.Low[i]) / 2;
						numerator = numerator + _coefficients[i] * price;
						denominator = denominator + _coefficients[i];
					}

					_result = numerator / denominator;
				}

				return _result;
			}

			/// <summary>
			/// Sets up the momentum coefficients for the momentum strategy.
			/// </summary>
			private void momentumCoefficients()
			{
				for (int i = 0; i < Length; i++)
					_coefficients.Add(Math.Abs(Instrument.Close[i] - Instrument.Close[i + MomentumLength]));
			}
		}
	}
}