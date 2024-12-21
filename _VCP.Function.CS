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
			public const int MINIMUM_SWING_LENGTH = 3;
			public const int MINIMUM_SCAN_LENGTH = 5;
			public const int DEFAULT_SCAN_LENGTH = 50;
			public const double DEFAULT_EPSILON = 0.01;   //1% of ATR would be considered equal in price value

			//types
			public enum PivotAnalysis : uint
			{
				None = 0,
				SwingHigh = 1,
				SwingLow = 2,
				MajorHigh = 4,
				MajorLow = 8,
				MinorHigh = 16,
				MinorLow = 32,
				LastBar = 64,
			}

			public struct PivotPoint
			{
				public double High;
				public double Low;
				public int Index;
				public PivotAnalysis Analysis;
			}

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int ScanLength { get; set; }   //scan length for the VCP
			public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

			//properties
			public double HighestHigh { get; private set; }
			public double LowestLow { get; private set; }
			public List<PivotPoint> LowPivots { get { return m_lowPivots; } }
			public List<PivotPoint> HighPivots { get { return m_highPivots; } }


			//attributes
			private Function._ATR m_atr;
			private List<PivotPoint> m_lowPivots;
			private List<PivotPoint> m_highPivots;
			private List<PivotPoint> m_pivots;

			//interface methods
			public _VCP(CStudyControl _master) : base(_master) { }
			public _VCP(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				ScanLength = DEFAULT_SCAN_LENGTH;
				Epsilon = DEFAULT_EPSILON;
				m_atr = new Function._ATR(this);
				m_lowPivots = new List<PivotPoint>();
				m_highPivots = new List<PivotPoint>();
				m_pivots = new List<PivotPoint>();
			}

			protected override void StartCalc()
			{
				if (ScanLength < MINIMUM_SCAN_LENGTH) throw new Exception("Scan length must be greater than 5");
				m_atr.Instrument = Instrument;
				m_atr.Length = ScanLength;
			}

			protected override System.Boolean CalcBar()
			{
				//initialize the highest high and lowest low
				HighestHigh = -1;
				LowestLow = -1;

				if (Instrument.CurrentBar < ScanLength + 1) return false; //+1 since we want to test the bar before the first bar analysed 

				//determine pivot points for the scan length of bars
				m_lowPivots.Clear();
				m_highPivots.Clear();
				m_pivots.Clear();

				for (int i = ScanLength; i > 1; i--)
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
						m_pivots.Add(pivot);
					}
					else if (Instrument.High[i] > Instrument.High[i - 1] && Instrument.High[i] > Instrument.High[i + 1])
					{
						PivotPoint pivot = new PivotPoint();
						pivot.High = Instrument.High[i];
						pivot.Index = i;
						pivot.Analysis = PivotAnalysis.MinorHigh;
						m_highPivots.Add(pivot);
						m_pivots.Add(pivot);
					}
					else if (Instrument.Low[i] > Instrument.Low[i - 1] && Instrument.Low[i] > Instrument.Low[i + 1])
					{
						PivotPoint pivot = new PivotPoint();
						pivot.Low = Instrument.Low[i];
						pivot.Index = i;
						pivot.Analysis = PivotAnalysis.MinorLow;
						m_lowPivots.Add(pivot);
						m_pivots.Add(pivot);
					}
				}

				//the last bar should always be considered a pseudo pivot point since we'd
				//expect the VCP to contract into the last bar
				PivotPoint lastPivot = new PivotPoint();
				lastPivot.High = Instrument.High[0];
				lastPivot.Low = Instrument.Low[0];
				lastPivot.Index = 0;
				lastPivot.Analysis = PivotAnalysis.LastBar;
				m_lowPivots.Add(lastPivot);
				m_highPivots.Add(lastPivot);

				//setup the highest high and lowest low
				LowestLow = m_lowPivots.Min(p => p.Low);
				HighestHigh = m_highPivots.Max(p => p.High);


				////analyse the pivots to determine the swing highs and lows
				//// - we require that each swing low/high or high/low is at least 3 bars apart except for the last bar
				//int nextSwingIndex = 0;
				//foreach (PivotPoint pivot in m_pivots)
				//{
				//	if (pivot.Index < nextSwingIndex) continue; //skip this pivot point if it's already been analysed

				//	if ((pivot.Analysis & PivotAnalysis.MinorHigh) == PivotAnalysis.MinorHigh && (pivot.Analysis & PivotAnalysis.MinorLow) == PivotAnalysis.MinorLow)
				//	{
				//		//try to find a major high or low from the current pivot point
				//		PivotPoint? pivotPoint = findMajorPivot(pivot, PivotAnalysis.MajorHigh);
				//		if (pivotPoint == null)
				//		{
				//			pivotPoint = findMajorPivot(pivot, PivotAnalysis.MajorLow);
				//			pivotPoint.Value.Analysis |= PivotAnalysis.MajorLow;
				//		}
				//		else
				//			pivotPoint.Value.Analysis |= PivotAnalysis.MajorHigh;
				//		nextSwingIndex = pivotPoint.Index;
				//	}
				//	else if ((pivot.Analysis & PivotAnalysis.MinorHigh) == PivotAnalysis.MinorHigh)
				//	{
				//		//try to find a major high from the current pivot point
				//		PivotPoint? pivotPoint = findMajorPivot(pivot, PivotAnalysis.MajorLow);
				//		pivotPoint.Analysis |= PivotAnalysis.MajorLow;
				//		nextSwingIndex = pivotPoint.Index;
				//	}
				//	if ((pivot.Analysis & PivotAnalysis.MinorLow) == PivotAnalysis.MinorLow)
				//	{
				//		//try to find a major low from the current pivot point
				//		PivotPoint? pivotPoint = findMajorPivot(pivot, PivotAnalysis.MajorHigh);
				//		pivotPoint.Analysis |= PivotAnalysis.MajorHigh;
				//		nextSwingIndex = pivotPoint.Index;
				//	}
				//}

				////check that distances between the major swing points are contracting
				//double previousDistance = getStartDistance();
				//PivotPoint? previousMajorPivot = null;
				//foreach (PivotPoint pivotPoint in m_pivots)
				//{
				//  //check that the distance between the major swing points is contracting
				//	if ((pivotPoint.Analysis & PivotAnalysis.MajorHigh) == PivotAnalysis.MajorHigh)
				//	{
				//		//check that the distance between the major high and the last bar is contracting
				//		if (previousMajorPivot == null) previousMajorPivot = pivotPoint;
				//		else
				//		{
				//			double currentDistance = Math.Abs(pivotPoint.High - previousMajorPivot.Low);
				//			if (currentDistance > previousDistance)
				//				return false;
				//			previousDistance = currentDistance;
				//		}
				//	}
				//	else if ((pivotPoint.Analysis & PivotAnalysis.MajorLow) == PivotAnalysis.MajorLow)
				//	{
				//		//check that the distance between the major low and the last bar is contracting
				//		if (previousMajorPivot == null) previousMajorPivot = pivotPoint;
				//		else
				//		{
				//			double currentDistance = Math.Abs(previousMajorPivot.High - pivotPoint.Low);
				//			if (currentDistance > previousDistance)
				//				return false;
				//			previousDistance = currentDistance;
				//		}
				//	}
				//}


				return true;
			}

			//methods
			private double getStartDistance()
			{
				if (m_pivots.Count < 2) return 0.0d; //need at least 2 pivots to determine the distance between them

				double distance = 0;
				PivotPoint firstPivot = m_pivots[0];
				PivotPoint secondPivot = m_pivots[1];

				if ((firstPivot.Analysis & PivotAnalysis.MajorHigh) == PivotAnalysis.MajorHigh)
					distance = Math.Abs(firstPivot.High - secondPivot.Low);
				else if ((firstPivot.Analysis & PivotAnalysis.MajorLow) == PivotAnalysis.MajorLow)
					distance = Math.Abs(firstPivot.Low - secondPivot.High);

				return distance;
			}

			private PivotPoint? findMajorPivot(PivotPoint pivot, PivotAnalysis analysis)
			{
				PivotPoint? pivotPoint = null;
//				bool searchLeg = false;
//
//				foreach (PivotPoint otherPivot in m_pivots)
//				{
//					//if (!searchLeg && otherPivot != pivot) continue;
//
//					searchLeg = true;
//					if (analysis == PivotAnalysis.MajorHigh)
//					{
//
//						//TODO - how to properly scan for a leg high pivot point
//
//					}
//					else if (analysis == PivotAnalysis.MajorLow)
//					{
//
//						//TODO - how to properly scan for a leg low pivot point
//
//					}
//
//					if (pivotPoint != null) break;
//				}

				return pivotPoint;
			}


		}
	}
}