using FileHelpers;
using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// RelativeStrength as used by William O'Neil to determine how strong an instrument is relative to the general market.
		/// </summary>
		public sealed class _RelativeStrength : FunctionSeries<System.Double>
		{
			//constants
			public const int LENGTH = 20;

			//properties
			public int Length { get; set; }
			public int SmoothingLength { get; set; }
			public IInstrument Instrument1 { get; set; }
			public IInstrument Instrument2 { get; set; }

			//attributes
			private Function.SuperSmoother m_superSmoother;
			private VariableSeries<double> m_rawRelativeStrength;   //relative strength without smoothing		

			//interface methods
			public _RelativeStrength(CStudyControl _master) : base(_master) { }
			public _RelativeStrength(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = LENGTH;
				m_rawRelativeStrength = new VariableSeries<double>(this);
				m_superSmoother = new SuperSmoother(this);
			}

			protected override void StartCalc()
			{
				m_superSmoother.Length = SmoothingLength > 0 ? SmoothingLength : SuperSmoother.DEFAULT_PERIOD;  //relative strength does allow no smoothing but supersmoother requires a smoothing larger than 0
				m_superSmoother.Price = m_rawRelativeStrength;
			}

			protected override System.Double CalcBar()
			{
				if (Instrument1.Status == EBarState.Close || Instrument2.Status == EBarState.Close)
				{
					m_rawRelativeStrength.Value = 0.0d;
					if (Instrument1.CurrentBar > Length && Instrument2.CurrentBar > Length)
					{
						double instrument2GrowthRate = Instrument2.Close[0] / Instrument2.Close[Length - 1];
						if (instrument2GrowthRate != 0.0d)
						{
							double instrument1GrowthRate = Instrument1.Close[0] / Instrument1.Close[Length - 1];
							m_rawRelativeStrength.Value = instrument1GrowthRate / instrument2GrowthRate;
						}
					}
				}

				return SmoothingLength > 0 ? m_superSmoother[0] : m_rawRelativeStrength[0];
			}

			//methods



		}
	}
}