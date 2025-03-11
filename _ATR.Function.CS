using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Average True Range calculation - https://en.wikipedia.org/wiki/Average_true_range
		/// </summary>
		public sealed class _ATR : FunctionSeries<System.Double>
		{
			//constants
			public const int DEFAULT_LENGTH = 14;

			//inputs
			public IInstrument Instrument { get; set; } //data feed to use for the ATR calculation
			public int Length { get; set; }             //length of the ATR calculation

			//properties


			//attributes


			//interface methods
			public _ATR(CStudyControl _master) : base(_master) { }
			public _ATR(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
			}

			protected override void StartCalc() 
			{
				if (Length <= 0) throw new Exception("Length must be greater than zero");
			}

			protected override System.Double CalcBar()
			{
				if (Instrument.CurrentBar == Length)
				{
					//compute the first value for the average true range
					double sum = 0.0;
					for (int i = 0; i < Length; i++)
					{
						double tr = Math.Max(Instrument.High[i] - Instrument.Low[i], Math.Max(Math.Abs(Instrument.High[i] - Instrument.Close[i + 1]), Math.Abs(Instrument.Low[i] - Instrument.Close[i + 1])));
						sum += tr;
					}

					return sum / Length;
				}
				else
				{
					//average true range is a smoothed value of the true range using exponential smoothing
					double tr = Math.Max(Instrument.High[0] - Instrument.Low[0], Math.Max(Math.Abs(Instrument.High[0] - Instrument.Close[1]), Math.Abs(Instrument.Low[0] - Instrument.Close[1])));
					return (this[1] * (Length - 1) + tr) / Length;
				}
			}

			//methods



		}
	}
}