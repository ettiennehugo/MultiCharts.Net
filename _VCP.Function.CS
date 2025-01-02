using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Security.Policy;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the last set of bars can be considered a volatility contraction pattern over a specified scan length. 
		/// The algorithm is as follows:
		/// * Uses a KAMA with an ATR channel to determine intervals at which the price consoliation would pivot above/below the KAMA.
		/// * A big problem for this algorithm is to try and avoid whipsaws around the KAMA and thus incorrectly partitioning the pice
		///   data swings, an ATR channel upper/lower bounds are used to avoid whipsaw moves due to price moves.
		/// * A set of legs are generated based off the intervals, the legs are formed by the high/low points of the intervals.
		/// * IF the legs are contracting (i.e. the legs have a high overlay) then the pattern is considered a volatility contraction pattern.
		///		- A minimum percentage is introduced to scan for leg contractions, if the set of legs contracting out of the total number of legs if above this
		///		  percentage then the pattern is considered a volatility contraction pattern.
		///	NOTES:
		///	* The VCP could indicate both a build up to a breakout or a build up to a breakdown, the VCP itself does not indicate the direction. Use the VCP
		///	  with recent momentum indicators to determine the direction of the breakout/breakdown.
		/// </summary>
		public sealed class _VCP : FunctionSimple<System.Boolean>
		{
			//constants
			public const int MINIMUM_SCAN_LENGTH = 10;
			public const int DEFAULT_SCAN_LENGTH = 50;
			public const double DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS = 0.8;   //per default require 80% of the legs to contract to form the VCP - there might be some noise that might cause one or two legs to not contract
			public const int OVERSCAN_LENGTH = 20;   //we need some level of overscan before the start of the VCP to ensure we capture the full length of the first leg as best possible
			public const double DEFAULT_MINIMUM_ATR_DELTA = 0.5;  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
			public const int DEFAULT_FAST_LENGTH = 3;
			public const int DEFAULT_SLOW_LENGTH = 53;
			public const int DEFAULT_MINIMUM_REQUIRED_LEGS = 3;  //default minimum number of legs to potentially have a VCP

			//types
			/// <summary>
			/// Enumeration of the interval as the price moves above or below the KAMA and it's associated ATR channels.
			/// </summary>
			private enum PivotIntervalType
			{
				Above,
				Below
			}

			/// <summary>
			/// Pivot interval where the price data is moving above/below the KAMA.
			/// </summary>			
			private class PivotInterval
			{
				public PivotIntervalType Type;
				public DateTime OpenTime;
				public DateTime HighTime;
				public DateTime LowTime;
				public DateTime CloseTime;
				public int OpenBarNumber; //bar numbers represent the index of the bar relative to the current number
				public int HighBarNumber;
				public int LowBarNumber;
				public int CloseBarNumber;
				public int OpenBarIndex; //bar indices represent the index of the bar relative to the current bar index which would be zero at the time of computation
				public int HighBarIndex;
				public int LowBarIndex;
				public int CloseBarIndex;
				public double Open;
				public double Close;
				public double High;
				public double HighestClose;
				public double Low;
				public double LowestClose;
				public int BarCount { get { return CloseBarNumber - OpenBarNumber + 1; } }
			}

			//inputs
			public IInstrument Instrument { get; set; }  //instrument price feed to use for the scan
			public int FastLength { get; set; }          //fast length for the KAMA
			public int SlowLength { get; set; }          //slow length for the KAMA
			public double MinimumATRDelta { get; set; }  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
			public int ScanLength { get; set; }					 //scan length for the VCP
			public int MinimumRequiredLegs { get; set; } //minimum number of required legs to form the VCP
			public double MinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 

			//properties
			// properties to be used for rendering the VCP with the VCP itself representing a dynamically
			// constructed OHLC bar based on the pivot intervals
			public DateTime OpenTime { get; private set; }
			public DateTime CloseTime { get; private set; }
			public double Open { get; private set; }
			public double High { get; private set; }
			public double Low { get; private set; }
			public double Close { get; private set; }
			public List<Tuple<int, int, DateTime, double>> PivotPoints { get; private set; }  //pivot points forming VCP legs by bar number, bar index (at time of computation), time, and bar low/high
			public double PercentageContracingLegs { get; private set; }  //percentage of legs contracting

			//attributes
			private Function._KAMA m_kama;
			private Function._ATR m_atr;
			private List<PivotInterval> m_pivotIntervals;
			private List<Tuple<double, bool>> m_legDelta; //stores the percentage overlay with the previous leg and whether this leg is contracting relative to the previous leg

			//interface methods
			public _VCP(CStudyControl _master) : base(_master) { }
			public _VCP(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				FastLength = DEFAULT_FAST_LENGTH;
				SlowLength = DEFAULT_SLOW_LENGTH;
				MinimumATRDelta = DEFAULT_MINIMUM_ATR_DELTA;
				ScanLength = DEFAULT_SCAN_LENGTH;
				MinimumPercentageContracingLegs = DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;
				m_kama = new Function._KAMA(this);
				m_atr = new Function._ATR(this);
				PivotPoints = new List<Tuple<int, int, DateTime, double>>();
				m_pivotIntervals = new List<PivotInterval>();
				m_legDelta = new List<Tuple<double, bool>>();
			}

			protected override void StartCalc()
			{
				if (FastLength <= 0) throw new Exception("Fast length must be greater than 0");
				if (SlowLength <= 0) throw new Exception("Slow length must be greater than 0");
				if (FastLength >= SlowLength) throw new Exception("Fast length must be less than the slow length");
				if (MinimumATRDelta <= 0) throw new Exception("Minimum ATR delta must be greater than 0");
				if (ScanLength < MINIMUM_SCAN_LENGTH) throw new Exception("Maximum scan length must be greater than 10");
				if (MinimumPercentageContracingLegs < 0 || MinimumPercentageContracingLegs > 1.0d) throw new Exception("Minimum percentage of contracting legs must be between 0 and 1");
				m_kama.FastLength = FastLength;
				m_kama.SlowLength = SlowLength;
				m_kama.Price = Instrument.Close;
				m_atr.Instrument = Instrument; //we use the default ATR length
			}

			//DEBUG - Set date here to stop below for analysis on a specific date/time.
			static DateTime test = new DateTime(2023, 5, 18, 18, 30, 0);

			protected override System.Boolean CalcBar()
			{
				//clear function state
				PivotPoints.Clear();
				m_pivotIntervals.Clear();
				m_legDelta.Clear();
				OpenTime = DateTime.MinValue;
				CloseTime = DateTime.MaxValue;
				Open = Close = 0.0d;
				High = double.PositiveInfinity;
				Low = double.NegativeInfinity;

				//check that we have enough bars to process
				if (Instrument.CurrentBar < (ScanLength + OVERSCAN_LENGTH)) return false;

				//DEBUG - Set breakpoint here to stop at a specific date/time for analysis.
				if (Instrument.Time[0] == test)
					Output.Write("Debug");

				scanForIntervals();

				//we need at least a mininmum of intervals to construct a VCP of the required number of legs
				if (m_pivotIntervals.Count + 1 < MinimumRequiredLegs) return false; //+1 since we'll add the start and end points of the VCP forming additional legs

				extendFirstInterval();
				mergeIntervals();

				//after merging whipsaw intervals we still need at least a mininmum of intervals to construct
				//a VCP of the required number of legs
				if (m_pivotIntervals.Count + 1 < MinimumRequiredLegs) return false; //+1 since we'll add the start and end points of the VCP forming additional legs

				setupProperties();
				initializePivotPoints();
				if (PivotPoints.Count - 1 < MinimumRequiredLegs || !scanForContractionOfLegs()) return false;	//-1 because we need two points per line drawn

				return true;
			}

			//methods
			/// <summary>
			/// Performs a scan of the bars to determine the intervals where the price data move above/below the KAMA, following this the intervals that have not made a marked move
			/// above/below the KAMA based on an ATR deviation from the KAMA are merged into more prevailing intervals above/below the KAMA.
			/// </summary>
			private void scanForIntervals()
			{
				//partition the scan length into intervals formed of bars grouped by the KAMA cross points
				PivotInterval interval = new PivotInterval
				{
					Type = Instrument.Close[ScanLength - 1] > m_kama[ScanLength - 1] ? PivotIntervalType.Above : PivotIntervalType.Below,
					OpenBarNumber = Instrument.CurrentBar - ScanLength + 1,
					HighBarNumber = Instrument.CurrentBar - ScanLength + 1,
					LowBarNumber = Instrument.CurrentBar - ScanLength + 1,
					CloseBarNumber = Instrument.CurrentBar - ScanLength + 1,
					OpenBarIndex = ScanLength - 1,
					HighBarIndex = ScanLength - 1,
					LowBarIndex = ScanLength - 1,
					CloseBarIndex = ScanLength - 1,
					OpenTime = Instrument.Time[ScanLength - 1],
					HighTime = Instrument.Time[ScanLength - 1],
					LowTime = Instrument.Time[ScanLength - 1],
					CloseTime = Instrument.Time[ScanLength - 1],
					Open = Instrument.Open[ScanLength - 1],
					High = Instrument.High[ScanLength - 1],
					Low = Instrument.Low[ScanLength - 1],
					Close = Instrument.Close[ScanLength - 1],
					HighestClose = Instrument.Close[ScanLength - 1],
					LowestClose = Instrument.Close[ScanLength - 1]
				};

				//NOTE: We construct the intervals around the maximum scan length and will cull the intervals/legs not required up to the minimum
				//      scan length to still meet the number of required legs for the VCP.
				for (int i = ScanLength - 2; i >= 0; i--)    //-2 since we start with the first bar as an interval above
																														//determine whether price has crossed the SSMA
					if ((Instrument.Close[i] > m_kama[i] && Instrument.Close[i + 1] <= m_kama[i + 1]) ||
							(Instrument.Close[i] < m_kama[i] && Instrument.Close[i + 1] >= m_kama[i + 1]))
					{
						//close previous interval
						m_pivotIntervals.Add(interval);

						//start new interval
						interval = new PivotInterval
						{
							Type = Instrument.Close[i] > m_kama[i] ? PivotIntervalType.Above : PivotIntervalType.Below,
							OpenBarNumber = Instrument.CurrentBar - i,
							HighBarNumber = Instrument.CurrentBar - i,
							LowBarNumber = Instrument.CurrentBar - i,
							CloseBarNumber = Instrument.CurrentBar - i,
							OpenBarIndex = i,
							HighBarIndex = i,
							LowBarIndex = i,
							CloseBarIndex = i,
							OpenTime = Instrument.Time[i],
							HighTime = Instrument.Time[i],
							LowTime = Instrument.Time[i],
							CloseTime = Instrument.Time[i],
							Open = Instrument.Open[i],
							High = Instrument.High[i],
							Low = Instrument.Low[i],
							Close = Instrument.Close[i],
							HighestClose = Instrument.Close[i],
							LowestClose = Instrument.Close[i],
						};
					}
					else  //accumulate information for the current interval
					{
						interval.CloseBarNumber = Instrument.CurrentBar - i;
						interval.CloseBarIndex = i;
						interval.CloseTime = Instrument.Time[i];
						interval.Close = Instrument.Close[i];

						interval.HighestClose = Math.Max(interval.HighestClose, Instrument.Close[i]);
						if (interval.High < Instrument.High[i])
						{
							interval.High = Instrument.High[i];
							interval.HighBarNumber = Instrument.CurrentBar - i;
							interval.HighBarIndex = i;
							interval.HighTime = Instrument.Time[i];
						}

						interval.LowestClose = Math.Min(interval.LowestClose, Instrument.Close[i]);
						if (interval.Low > Instrument.Low[i])
						{
							interval.Low = Instrument.Low[i];
							interval.LowBarNumber = Instrument.CurrentBar - i;
							interval.LowBarIndex = i;
							interval.LowTime = Instrument.Time[i];
						}
					}

				//add last "incomplete" interval
				m_pivotIntervals.Add(interval);
			}

			/// <summary>
			/// Merge small intervals due to whipsaws into larger intervals.
			/// </summary>
			private void mergeIntervals()
			{
				//merge intervals that have not made a marked move above/below the KAMA based on an ATR deviation
				//from the KAMA - we reverse iterate the intervals since the last intervals would point to the most
				//recent price data
				for (int i = m_pivotIntervals.Count - 1; i > 0; i--)
				{
					if ((m_pivotIntervals[i].Type == PivotIntervalType.Above && m_pivotIntervals[i].HighestClose < m_kama[m_pivotIntervals[i].HighBarIndex] + (m_atr[m_pivotIntervals[i].HighBarIndex] * MinimumATRDelta)) ||
							(m_pivotIntervals[i].Type == PivotIntervalType.Below && m_pivotIntervals[i].LowestClose > m_kama[m_pivotIntervals[i].LowBarIndex] - (m_atr[m_pivotIntervals[i].LowBarIndex] * MinimumATRDelta)))
					{
						//merge interval into the previous one if it's not the last interval
						if (i > 0)
						{
							m_pivotIntervals[i - 1].CloseBarNumber = m_pivotIntervals[i].CloseBarNumber;
							m_pivotIntervals[i - 1].CloseBarIndex = m_pivotIntervals[i].CloseBarIndex;
							m_pivotIntervals[i - 1].CloseTime = m_pivotIntervals[i].CloseTime;
							m_pivotIntervals[i - 1].Close = m_pivotIntervals[i].Close;
							m_pivotIntervals[i - 1].HighestClose = Math.Max(m_pivotIntervals[i - 1].HighestClose, m_pivotIntervals[i].HighestClose);
							m_pivotIntervals[i - 1].LowestClose = Math.Min(m_pivotIntervals[i - 1].LowestClose, m_pivotIntervals[i].LowestClose);

							if (m_pivotIntervals[i].High > m_pivotIntervals[i - 1].High)
							{
								m_pivotIntervals[i - 1].High = m_pivotIntervals[i].High;
								m_pivotIntervals[i - 1].HighBarNumber = m_pivotIntervals[i].HighBarNumber;
								m_pivotIntervals[i - 1].HighBarIndex = m_pivotIntervals[i].HighBarIndex;
								m_pivotIntervals[i - 1].HighTime = m_pivotIntervals[i].HighTime;
							}

							if (m_pivotIntervals[i].Low < m_pivotIntervals[i - 1].Low)
							{
								m_pivotIntervals[i - 1].Low = m_pivotIntervals[i].Low;
								m_pivotIntervals[i - 1].LowBarNumber = m_pivotIntervals[i].LowBarNumber;
								m_pivotIntervals[i - 1].LowBarIndex = m_pivotIntervals[i].LowBarIndex;
								m_pivotIntervals[i - 1].LowTime = m_pivotIntervals[i].LowTime;
							}

							//remove the current interval
							m_pivotIntervals.RemoveAt(i);
						}
						else if (m_pivotIntervals.Count > 2)  //below RemoveAt will shrink the list and this merge only needs to happen if there are more than 2 intervals
						{
							//if it's the last interval (i == 0) we need to merge it into the previous interval
							m_pivotIntervals[1].CloseBarNumber = m_pivotIntervals[0].CloseBarNumber;
							m_pivotIntervals[1].CloseBarIndex = m_pivotIntervals[0].CloseBarIndex;
							m_pivotIntervals[1].CloseTime = m_pivotIntervals[0].CloseTime;
							m_pivotIntervals[1].Close = m_pivotIntervals[0].Close;
							m_pivotIntervals[1].HighestClose = Math.Max(m_pivotIntervals[1].HighestClose, m_pivotIntervals[0].HighestClose);
							m_pivotIntervals[1].LowestClose = Math.Min(m_pivotIntervals[1].LowestClose, m_pivotIntervals[0].LowestClose);

							if (m_pivotIntervals[0].High > m_pivotIntervals[1].High)
							{
								m_pivotIntervals[1].High = m_pivotIntervals[0].High;
								m_pivotIntervals[1].HighBarNumber = m_pivotIntervals[0].HighBarNumber;
								m_pivotIntervals[1].HighBarIndex = m_pivotIntervals[0].HighBarIndex;
								m_pivotIntervals[1].HighTime = m_pivotIntervals[0].HighTime;
							}

							if (m_pivotIntervals[0].Low < m_pivotIntervals[1].Low)
							{
								m_pivotIntervals[1].Low = m_pivotIntervals[0].Low;
								m_pivotIntervals[1].LowBarNumber = m_pivotIntervals[0].LowBarNumber;
								m_pivotIntervals[1].LowBarIndex = m_pivotIntervals[0].LowBarIndex;
								m_pivotIntervals[1].LowTime = m_pivotIntervals[0].LowTime;
							}

							//remove the current interval
							m_pivotIntervals.RemoveAt(i);
						}
					}
				}

				//now merge consecutive intervals that have the same type - this can happen when there are short whipsaw moves
				//where a single bar ended up below the KAMA with surrounding intervals above the KAMA
				for (int i = m_pivotIntervals.Count - 1; i > 0; i--)
				{
					if (m_pivotIntervals[i].Type == m_pivotIntervals[i - 1].Type)
					{
						m_pivotIntervals[i - 1].CloseBarNumber = m_pivotIntervals[i].CloseBarNumber;
						m_pivotIntervals[i - 1].CloseBarIndex = m_pivotIntervals[i].CloseBarIndex;
						m_pivotIntervals[i - 1].CloseTime = m_pivotIntervals[i].CloseTime;
						m_pivotIntervals[i - 1].Close = m_pivotIntervals[i].Close;
						m_pivotIntervals[i - 1].HighestClose = Math.Max(m_pivotIntervals[i - 1].HighestClose, m_pivotIntervals[i].HighestClose);
						m_pivotIntervals[i - 1].LowestClose = Math.Min(m_pivotIntervals[i - 1].LowestClose, m_pivotIntervals[i].LowestClose);

						if (m_pivotIntervals[i].High > m_pivotIntervals[i - 1].High)
						{
							m_pivotIntervals[i - 1].High = m_pivotIntervals[i].High;
							m_pivotIntervals[i - 1].HighBarNumber = m_pivotIntervals[i].HighBarNumber;
							m_pivotIntervals[i - 1].HighBarIndex = m_pivotIntervals[i].HighBarIndex;
							m_pivotIntervals[i - 1].HighTime = m_pivotIntervals[i].HighTime;
						}

						if (m_pivotIntervals[i].Low < m_pivotIntervals[i - 1].Low)
						{
							m_pivotIntervals[i - 1].Low = m_pivotIntervals[i].Low;
							m_pivotIntervals[i - 1].LowBarNumber = m_pivotIntervals[i].LowBarNumber;
							m_pivotIntervals[i - 1].LowBarIndex = m_pivotIntervals[i].LowBarIndex;
							m_pivotIntervals[i - 1].LowTime = m_pivotIntervals[i].LowTime;
						}

						//remove the current interval
						m_pivotIntervals.RemoveAt(i);
					}
				}
			}

			/// <summary>
			/// Scan the price data for an extended trend before the first interval to determine the starting point of the VCP.
			/// </summary>
			private void extendFirstInterval()
			{
				//overscan the bars before the first interval to determine the full extent of the first leg of the VCP				
				//NOTE: We need to do this before merging small intervals into larger ones since the first interval can end up
				//      containing a small number of bars initially but once extended contains enough bars to form it's own interval.
				for (int i = 0; i < OVERSCAN_LENGTH; i++)
				{
					int barIndex = ScanLength + i;
					if (m_pivotIntervals[0].Type == PivotIntervalType.Above)
					{
						//check for a trend that is moving lower at the start of the VCP above the KAMA
						if (Instrument.Low[barIndex] < m_pivotIntervals[0].Low)
						{
							m_pivotIntervals[0].OpenBarNumber = Instrument.CurrentBar - barIndex;
							m_pivotIntervals[0].OpenBarIndex = barIndex;
							m_pivotIntervals[0].OpenTime = Instrument.Time[barIndex];
							m_pivotIntervals[0].Open = Instrument.Open[barIndex];
							m_pivotIntervals[0].Low = Instrument.Low[barIndex];
							m_pivotIntervals[0].LowBarNumber = Instrument.CurrentBar - barIndex;
							m_pivotIntervals[0].LowBarIndex = barIndex;
							m_pivotIntervals[0].LowTime = Instrument.Time[barIndex];
						}
					}
					else
					{
						//check for a trend that is moving higher at the start of the VCP below the KAMA
						if (Instrument.High[barIndex] > m_pivotIntervals[0].High)
						{
							m_pivotIntervals[0].OpenBarNumber = Instrument.CurrentBar - barIndex;
							m_pivotIntervals[0].OpenBarIndex = barIndex;
							m_pivotIntervals[0].OpenTime = Instrument.Time[barIndex];
							m_pivotIntervals[0].Open = Instrument.Open[barIndex];
							m_pivotIntervals[0].High = Instrument.High[barIndex];
							m_pivotIntervals[0].HighBarNumber = Instrument.CurrentBar - barIndex;
							m_pivotIntervals[0].HighBarIndex = barIndex;
							m_pivotIntervals[0].HighTime = Instrument.Time[barIndex];
						}
					}
				}
			}

			/// <summary>
			/// Setup the legs for the VCP based on the determined intervals.
			/// </summary>
			private void initializePivotPoints()
			{
				for (int i = 0; i < m_pivotIntervals.Count; i++)
				{
					if (i == 0)
					{
						//create the starting point of the first leg (and thus the starting point of the VCP)
						if (m_pivotIntervals[i].Type == PivotIntervalType.Below)
							PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].HighBarNumber, m_pivotIntervals[i].HighBarIndex, m_pivotIntervals[i].HighTime, m_pivotIntervals[i].High));
						else
							PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].LowBarNumber, m_pivotIntervals[i].LowBarIndex, m_pivotIntervals[i].LowTime, m_pivotIntervals[i].Low));

						//create the end-point of the first leg IFF it is not the same as the opening bar created above which is used for the opening
						if (m_pivotIntervals[i].Type == PivotIntervalType.Below)
						{
							if (m_pivotIntervals[i].LowBarNumber != m_pivotIntervals[i].HighBarNumber)
								PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].LowBarNumber, m_pivotIntervals[i].LowBarIndex, m_pivotIntervals[i].LowTime, m_pivotIntervals[i].Low));
						}
						else
							if (m_pivotIntervals[i].HighBarNumber != m_pivotIntervals[i].LowBarNumber)
							  PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].HighBarNumber, m_pivotIntervals[i].HighBarIndex, m_pivotIntervals[i].HighTime, m_pivotIntervals[i].High));
					}
					else if (i == m_pivotIntervals.Count - 1)
					{
						//create the second to last pivot point from the start of the scan (bar index 0) IFF it is not the current bar that will be used as the close of the VCP
						if (m_pivotIntervals[i].Type == PivotIntervalType.Below)
						{
							if (m_pivotIntervals[i].LowBarNumber != Instrument.CurrentBar)
								PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].LowBarNumber, m_pivotIntervals[i].LowBarIndex, m_pivotIntervals[i].LowTime, m_pivotIntervals[i].Low));
						}
						else
							if (m_pivotIntervals[i].HighBarNumber != Instrument.CurrentBar)
							  PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].HighBarNumber, m_pivotIntervals[i].HighBarIndex, m_pivotIntervals[i].HighTime, m_pivotIntervals[i].High));

						//create the end-point of the last leg (and thus the end-point of the VCP stretching to the current bar being processed)
						PivotPoints.Add(new Tuple<int, int, DateTime, double>(Instrument.CurrentBar, 0, Instrument.Time[0], Instrument.Close[0]));
					}
					else
						//create the subsequent legs based on the previous leg
						PivotPoints.Add(new Tuple<int, int, DateTime, double>(m_pivotIntervals[i].Type == PivotIntervalType.Below ? m_pivotIntervals[i].LowBarNumber : m_pivotIntervals[i].HighBarNumber, m_pivotIntervals[i].Type == PivotIntervalType.Below ? m_pivotIntervals[i].LowBarIndex : m_pivotIntervals[i].HighBarIndex, m_pivotIntervals[i].Type == PivotIntervalType.Below ? m_pivotIntervals[i].LowTime : m_pivotIntervals[i].HighTime, m_pivotIntervals[i].Type == PivotIntervalType.Below ? m_pivotIntervals[i].Low : m_pivotIntervals[i].High));
				}
			}

			/// <summary>
			/// Sets up the external properties used to describe the VCP.
			/// </summary>
			private void setupProperties()
			{
				OpenTime = m_pivotIntervals[0].OpenTime;
				CloseTime = m_pivotIntervals[m_pivotIntervals.Count - 1].CloseTime;
				Open = m_pivotIntervals[0].Open;
				High = m_pivotIntervals.Max(p => p.High);
				Low = m_pivotIntervals.Min(p => p.Low);
				Close = m_pivotIntervals[m_pivotIntervals.Count - 1].Close;
			}

			/// <summary>
			/// Scan for a required set of legs that overlay and contract, if the number of legs contracting is above the MinimumPercentageContracingLegs percentage
			/// will return true otherwise false.
			/// </summary>
			private bool scanForContractionOfLegs()
			{
				//setup the list of leg delta's
				int contractionCount = 0;
				double previousLegHigh = double.PositiveInfinity;
				double previousLegLow = double.NegativeInfinity;
				double previousLegDelta = double.NegativeInfinity;

				for (int i = 0; i < PivotPoints.Count - 1; i++)
				{
					double currentLegHigh = Math.Max(PivotPoints[i + 1].Item4, PivotPoints[i].Item4);
					double currentLegLow = Math.Min(PivotPoints[i + 1].Item4, PivotPoints[i].Item4);
					double currentLegDelta = currentLegHigh - currentLegLow;

					if (i > 0)
					{
						bool contracting = currentLegDelta <= previousLegDelta;
						double overlapPercent = 1.0d;  //there will always be 100% overlap for legs that are contracting, assume this as the default

						//if the current leg is not contracting, determine the percentage overlap
						if (!contracting)
						{
							if (currentLegDelta == 0.0d || previousLegDelta == 0.0d)  //handle boundary case of zero lengths where we can't say anything
							{
								overlapPercent = 0.0d;
								contracting = false;
							}
							else if (currentLegDelta != previousLegDelta)
								overlapPercent = currentLegDelta > previousLegDelta ? previousLegDelta / currentLegDelta : currentLegDelta / previousLegDelta;
						}
						else
							contractionCount++;  //increment the overlay and contraction counts that meet our predefined criteria

						m_legDelta.Add(new Tuple<double, bool>(overlapPercent, contracting));
					}

					previousLegLow = currentLegLow;
					previousLegHigh = currentLegHigh;
					previousLegDelta = currentLegDelta;
				}

				//setup the output percentage of contracting legs
				PercentageContracingLegs = (double)contractionCount / (double)(PivotPoints.Count - 2);  //-2 since we have one less leg than the number of bars

				return PercentageContracingLegs >= MinimumPercentageContracingLegs;   //return true if the percentage of contracting legs is above the minimum required
			}
		}
	}
}