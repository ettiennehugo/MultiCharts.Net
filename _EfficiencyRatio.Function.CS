using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Computes Perry Kaufman's efficiency ratio for the price movement over a period of time. It uses the start price to the end price movement divided
		/// over the total movement in price (high to low on each bar) over the period.
		/// </summary>
		public sealed class _EfficiencyRatio : FunctionSimple<System.Double>
		{
			//constants
			public const int LENGTH = 20;

			//inputs
			public int Length { get; set; }
			public IInstrument Instrument { get; set; }

			//attributes
			private bool m_initRatios;
			private List<Tuple<Double, Double, Double>> m_ratioTuples;

			public _EfficiencyRatio(CStudyControl _master) : base(_master) { }
			public _EfficiencyRatio(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = LENGTH;
				m_ratioTuples = new List<Tuple<Double, Double, Double>>();
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new Exception("Length must be positive.");
				m_initRatios = true;
				m_ratioTuples.Clear();
			}

			protected override System.Double CalcBar()
			{
				Double result = 0.0;

				if (Instrument.Status == EBarState.Close && Instrument.CurrentBar > Length)
				{
					if (m_initRatios)
					{
						//initialize the data list used to incrementally compute the efficiency ratio
						for (int i = Length - 1; i > 0; i--)
							m_ratioTuples.Add(new Tuple<Double, Double, Double>(Instrument.Open[i], Instrument.Close[i], Math.Abs(Instrument.Close[i] - Instrument.Close[i - 1])));
						m_initRatios = false;
					}
					else
					{
						//remove stale oldest entry in the list and add latest entry for the current bar
						m_ratioTuples.RemoveAt(0);
						m_ratioTuples.Add(new Tuple<Double, Double, Double>(Instrument.Open[0], Instrument.Close[0], Math.Abs(Instrument.Close[0] - Instrument.Close[1])));
					}

					//compute the efficiency ratio
					Double start = m_ratioTuples[0].Item1;
					Double end = -1.0;
					Double totalPriceMove = -1.0;

					foreach (var ratio in m_ratioTuples)
					{
						end = ratio.Item2;
						totalPriceMove += ratio.Item3;
					}

					// 1 == very efficient vs 0 == very inefficient
					if (totalPriceMove != 0.0)
						result = Math.Abs(end - start) / totalPriceMove;
				}

				return result;
			}

			//methods


		}
	}
}