using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the last set of bars can be considered a volatility contraction pattern over a specified
		/// scan length of bars.
		/// TBD - should we also determine whether the VCP is a rising flag or falling flag???
		/// </summary>
		public sealed class _VCP : FunctionSimple<System.Boolean>
		{
			//constants
			public const int MINIMUM_LENGTH = 5;
			public const int DEFAULT_LENGTH = 20;
			public const double DEFAULT_EPSILON = 0.01;   //1% of ATR would be considered equal in price value

			//types
			private enum PivotAnalysis: uint
			{
				None = 0,
				SwingHigh = 1,
				SwingLow = 2,
				MajorHigh = 4,
				MajorLow = 8,
				MinorHigh = 16,
				MinorLow = 32,
			}

			private struct PivotPoint
			{
				public double High;
				public double Low;
				public int Index;
				public PivotAnalysis Analysis;
			}

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int Length { get; set; }       //scan length for the VCP
			public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

			//properties
			public double HighestHigh { get; private set; }
			public double LowestLow { get; private set; }

			//attributes
			private Function._ATR m_atr;
			private List<PivotPoint> m_lowPivots;
			private List<PivotPoint> m_highPivots;

			//interface methods
			public _VCP(CStudyControl _master) : base(_master) { }
			public _VCP(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				Epsilon = DEFAULT_EPSILON;
				m_atr = new Function._ATR(this);
				m_lowPivots = new List<PivotPoint>();
				m_highPivots = new List<PivotPoint>();
			}

			protected override void StartCalc()
			{
				if (Length < MINIMUM_LENGTH) throw new Exception("Length must be greater than 5");
				m_atr.Instrument = Instrument;
				m_atr.Length = Length;
			}

			protected override System.Boolean CalcBar()
			{
				//initialize the highest high and lowest low
				HighestHigh = -1;
				LowestLow = -1;

				if (Instrument.CurrentBar < Length + 1) return false; //+1 since we want to test the bar before the first bar analysed 

				//determine pivot points for the scan length of bars
				m_lowPivots.Clear();
				m_highPivots.Clear();

				for (int i = Length; i > 1; i--)
				{
					//NOTE: An outside bar of a VCP contracting could be both the high and low pivot, we check 3 cases
					// 1. where the bar is both a low and high pivot
					// 2. where the bar is a high pivot
					// 3. where the bar is a low pivot
					if (Instrument.High[i] > Instrument.High[i - 1] && Instrument.High[i] > Instrument.High[i + 1] &&
							Instrument.Low[i] > Instrument.Low[i - 1] && Instrument.Low[i] > Instrument.Low[i + 1])
					{
						PivotPoint pivot = new PivotPoint();
						pivot.High = Instrument.High[i];
						pivot.Low = Instrument.Low[i];
						pivot.Index = i;
						pivot.Analysis = PivotAnalysis.MinorHigh | PivotAnalysis.MinorLow;
						m_lowPivots.Add(pivot);
						m_highPivots.Add(pivot);
					}
					else if (Instrument.High[i] > Instrument.High[i - 1] && Instrument.High[i] > Instrument.High[i + 1])
					{
						PivotPoint pivot = new PivotPoint();
						pivot.High = Instrument.High[i];
						pivot.Index = i;
						pivot.Analysis = PivotAnalysis.MinorHigh;
						m_highPivots.Add(pivot);
					}
					else if (Instrument.Low[i] > Instrument.Low[i - 1] && Instrument.Low[i] > Instrument.Low[i + 1])
					{
						PivotPoint pivot = new PivotPoint();
						pivot.Low = Instrument.Low[i];
						pivot.Index = i;
						pivot.Analysis = PivotAnalysis.MinorLow;
						m_lowPivots.Add(pivot);
					}
				}

				//the last bar should always be considered a pseudo pivot point since we'd
				//expect the VCP to contract into the last bar
//				PivotPoint pivot = new PivotPoint();
//				pivot.High = Instrument.High[0];
//				pivot.Low = Instrument.Low[0];
//				pivot.Index = 0;
//				m_lowPivots.Add(pivot);
//				m_highPivots.Add(pivot);

				//setup the highest high and lowest low
				LowestLow = m_lowPivots.Min(p => p.Low);
				HighestHigh = m_highPivots.Max(p => p.High);

				//analyse the pivots to determine the swing highs and lows


				//				//THESE loops should run for 0/1 to Lenght - 1 to detect the major highs and lows OR you need to reverse the lists
				//
				//
				//				for (int i = Length - 1; i > 1; i--)
				//				{
				//					//check if the pivot is a major high or low
				//					if (m_pivots[i].Analysis | PivotAnalysis.MinorHigh == PivotAnalysis.MinorHigh)
				//					{
				//						//check if the pivot is a major high
				//						if (m_pivots[i].High > m_pivots[i - 1].High && m_pivots[i].High > m_pivots[i + 1].High)
				//							m_pivots[i].Analysis |= PivotAnalysis.MajorHigh;
				//					}
				//
				//					if (m_pivots[i].Analysis | PivotAnalysis.MinorLow == PivotAnalysis.MinorLow)
				//					{
				//						//check if the pivot is a major low
				//						if (m_pivots[i].Low > m_pivots[i - 1].Low && m_pivots[i].Low > m_pivots[i + 1].Low)
				//							m_pivots[i].Analysis |= PivotAnalysis.MajorLow;
				//					}
				//				}
				//
				//
				//				//TODO
				//				// - WORK OUT A FEW STRATEGIES TO SCAN FOR DIFFERENT VARIATIONS OF VCP PATTERNS
				//        //   - rising flag
				//				//   - falling flag
				// 			  //   - symmetrical triangle
				//				//   - ascending triangle
				//				//   - descending triangle
				//				//   - pennant
				//				//   - wedge
				//				//   - cup and handle
				//				//	   - straight/rounded cup and normal handle
				//				//     - cup with a W cup and a normal handle
				//        //     - cup with a rounded cup and a W handle
				//        //     - cup with a W cup and a W handle
				//				// - start with a pivot and scan for the next extreme high or low based on Index
				//				// - remove pivots between the two extreme points to get the proper swing points
				//				// - need to keep the last bar in the scan as a pivot point
				//
				//
				//
				//				//check that the pivot points form some flag pattern
				//				double epsilon = (m_atr.Value * Epsilon) / 2;
				//
				//				//TODO
				//
				// function logic
				return default(System.Boolean);
			}

			//methods


		}
	}
}