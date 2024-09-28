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
		public sealed class RelativeStrength : FunctionSeries<System.Double>
		{
			//constants
			public const int LENGTH = 20;

			//properties
			public int Length { get; set; }
			public IInstrument Instrument1 { get; set; }
			public IInstrument Instrument2 { get; set; }

			//attributes
			VariableSeries<double> m_instrument1GrowthRate;
			VariableSeries<double> m_instrument2GrowthRate;

			//interface methods
			public RelativeStrength(CStudyControl _master) : base(_master) { }
			public RelativeStrength(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = LENGTH;
				m_instrument1GrowthRate = new VariableSeries<double>(this);
				m_instrument2GrowthRate = new VariableSeries<double>(this);
			}

			protected override void StartCalc() { }

			protected override System.Double CalcBar()
			{
				if (Instrument1.CurrentBar < Length || Instrument2.CurrentBar < Length)
					return 0;

				m_instrument1GrowthRate.Value = Instrument1.Close[0] / Instrument1.Close[Length];
				m_instrument2GrowthRate.Value = Instrument2.Close[0] / Instrument2.Close[Length];
				
				//TBD: Should you apply some level of smoothing here.
				
				return m_instrument1GrowthRate[0] / m_instrument2GrowthRate[0];
			}

			//methods



		}
	}
}