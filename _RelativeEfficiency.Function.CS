using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		public sealed class _RelativeEfficiency : FunctionSeries<System.Double>
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
			private bool m_initRatios;
			private List<Tuple<Double, Double, Double>> m_ratioTuples1;
			private List<Tuple<Double, Double, Double>> m_ratioTuples2;

			private VariableSeries<double> m_relativeEfficiency;   //relative efficiency without smoothing		

			//interface methods
			public _RelativeEfficiency(CStudyControl _master) : base(_master) { }
			public _RelativeEfficiency(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = LENGTH;
				SmoothingLength = 0;
				m_relativeEfficiency = new VariableSeries<double>(this);
				m_superSmoother = new SuperSmoother(this);
				m_ratioTuples1 = new List<Tuple<Double, Double, Double>>();
				m_ratioTuples2 = new List<Tuple<Double, Double, Double>>();
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new Exception("Length must be positive.");
				m_superSmoother.Length = SmoothingLength > 0 ? SmoothingLength : SuperSmoother.DEFAULT_PERIOD;  //relative efficiency does allow no smoothing but supersmoother requires a smoothing larger than 0
				m_superSmoother.Price = m_relativeEfficiency;
				m_initRatios = true;
				m_ratioTuples1.Clear();
				m_ratioTuples2.Clear();
			}

			protected override System.Double CalcBar()
			{
				//with two input data streams we need to only compute the relative efficiency once
				//the current bar for the two data streams align (the CalcBar can be run at with different
				//indices for the current bar which would result in incorrect computations).
				if (Instrument1.CurrentBar == Instrument2.CurrentBar)
				{
					m_relativeEfficiency.Value = 0.0d;
					double efficiencyRatio1 = calculateEfficiencyRatio(Instrument1, m_ratioTuples1);
					double efficiencyRatio2 = calculateEfficiencyRatio(Instrument2, m_ratioTuples2);
					m_initRatios = false;

					if (efficiencyRatio2 != 0.0d)
						m_relativeEfficiency.Value = efficiencyRatio1 / efficiencyRatio2;
				}

				return SmoothingLength > 0 ? m_superSmoother[0] : m_relativeEfficiency[0];
			}

			//methods
			private double calculateEfficiencyRatio(IInstrument instrument, List<Tuple<Double, Double, Double>> ratioTuples)
			{
				double result = 0.0;

				if (m_initRatios)
				{
					//initialize the data list used to incrementally compute the efficiency ratio
					for (int i = Length - 1; i > 0; i--)
						ratioTuples.Add(new Tuple<Double, Double, Double>(instrument.Open[i], instrument.Close[i], Math.Abs(instrument.Close[i] - instrument.Close[i - 1])));
				}
				else
				{
					//remove stale oldest entry in the list and add latest entry for the current bar
					ratioTuples.RemoveAt(0);
					ratioTuples.Add(new Tuple<Double, Double, Double>(instrument.Open[0], instrument.Close[0], Math.Abs(instrument.Close[0] - instrument.Close[1])));
				}

				//compute the efficiency ratio
				double start = ratioTuples[0].Item1;
				double end = -1.0;
				double totalPriceMove = -1.0;

				foreach (var ratio in ratioTuples)
				{
					end = ratio.Item2;
					totalPriceMove += ratio.Item3;
				}

				// 1 == very efficient vs 0 == very inefficient
				if (totalPriceMove != 0.0)
					result = Math.Abs(end - start) / totalPriceMove;

				return result;
			}
		}
	}
}