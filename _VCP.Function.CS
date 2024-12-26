using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the last set of bars can be considered a volatility contraction pattern over a specified
		/// scan length of bars. Uses a very short and longer term super smoother and then based off the crosses made by
		/// these it looks at the following:
		///   * counts at least a given number of legs
		///   * leg lengths should be contracting
		///   * SS crossovers forming the legs should at least by a specific number of bars apart to avoid whipsaws due to price noise
		///   * [optional] legs should overlay one another by a prescribed minimum to be considerd a VCP
		/// </summary>
		public sealed class _VCP : FunctionSimple<System.Boolean>
		{
			//constants
			public const int MINIMUM_SCAN_LENGTH = 10;
			public const int DEFAULT_SCAN_LENGTH = 50;
			public const int MINIMUM_LEG_COUNT = 3;
			public const int MINIMUM_LEG_BAR_DELTA = 2;
			public const int DEFAULT_FAST_LENGTH = 10;
			public const int DEFAULT_SLOW_LENGTH = 12;
			public const double DEFAULT_MINIMUM_LEG_OVERLAY = 0.8;	//legs should have an 80% overlay to be considered a contraction

			//types

			
			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int ScanLength { get; set; }   //scan length for the VCP
			public int MinimumLegCount { get; set; }	//minimum number of required legs
			public int MinimumLegBarDelta { get; set; } //minimum number of bars between legs to avoid whipsaws
			public double MinimumLegOverlay { get; set; } //minimum required leg overlay to be considered a VCP, 0 to simply check for contaction without overlay

			//properties
			// properties to be used for rendering the VCP
			public DateTime FirstBar { get; private set; }
			public DateTime LastBar { get; private set; }
			public double Low { get; private set; }
			public double High { get; private set; }

			//attributes
			private Function._SuperSmoother m_fastSS;
			private Function._SuperSmoother m_slowSS;
			private List<Tuple<int, double, DateTime>> m_legs;    //data for bar index, SS cross value, and time of the cross

			//interface methods
			public _VCP(CStudyControl _master) : base(_master) { }
			public _VCP(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				ScanLength = DEFAULT_SCAN_LENGTH;
				MinimumLegCount = MINIMUM_LEG_COUNT;
				MinimumLegBarDelta = MINIMUM_LEG_BAR_DELTA;
				MinimumLegOverlay = DEFAULT_MINIMUM_LEG_OVERLAY;
				m_fastSS = new Function._SuperSmoother(this);
				m_slowSS = new Function._SuperSmoother(this);
				m_legs = new List<Tuple<int, double, DateTime>>();
			}

			protected override void StartCalc()
			{
				if (ScanLength < MINIMUM_SCAN_LENGTH) throw new Exception("Scan length must be greater than 10");
				if (MinimumLegCount < MINIMUM_LEG_COUNT) throw new Exception("Minimum leg count must at least be 3");
				if (MinimumLegBarDelta < 0) throw new Exception("Minimum leg bar delta must be zero or positive");
				if (MinimumLegOverlay < 0 || MinimumLegOverlay > 1.0d) throw new Exception("Minimum leg overlay is percentage");
				m_fastSS.Length = DEFAULT_FAST_LENGTH;
				m_fastSS.Price = Instrument.Close;
				m_slowSS.Length = DEFAULT_SLOW_LENGTH;
				m_slowSS.Price = Instrument.Close;
				m_legs.Clear();
			}

			protected override System.Boolean CalcBar()
			{
				//clear function state
				m_legs.Clear();
				FirstBar = DateTime.MinValue;
				LastBar = DateTime.MaxValue;
				High = double.PositiveInfinity;
				Low = double.NegativeInfinity;
				double vcpLow = double.PositiveInfinity;
				double vcpHigh = double.NegativeInfinity;

				//determine the set of legs based on the scan length and the minimum required leg count
				for (int i = 0; i < ScanLength; i++)
				{
					//determine whether we have a swing point
					if ((m_fastSS[i] < m_slowSS[i] && m_fastSS[i + 1] >= m_slowSS[i + 1]) ||
							(m_fastSS[i] > m_slowSS[i] && m_fastSS[i + 1] <= m_slowSS[i + 1]))
					{
						//filter swing points that are too close to one another due to price noise
						if (m_legs.Count == 0)
						{
							vcpLow = Math.Min(vcpLow, Instrument.Low[i]);
							vcpHigh = Math.Max(vcpHigh, Instrument.High[i]);
							m_legs.Add(new Tuple<int, double, DateTime>(Instrument.CurrentBar - i, m_fastSS[i], Instrument.Time[i]));
						}
						else if (Instrument.CurrentBar - m_legs[m_legs.Count - 1].Item1 >= MinimumLegBarDelta)
						{
							vcpLow = Math.Min(vcpLow, Instrument.Low[i]);
							vcpHigh = Math.Max(vcpHigh, Instrument.High[i]);
							m_legs.Add(new Tuple<int, double, DateTime>(Instrument.CurrentBar - i, m_fastSS[i], Instrument.Time[i]));
						}
					}

					//terminate when we have enough swing points for VCP analysis
					if (m_legs.Count >= (MinimumLegCount * 2 - 1)) break;
				}

				//determine leg lengths if we have enough legs in the scan distance, e.g. 3 legs
				//would require 5 points (3 * 2 - 1)
				if (m_legs.Count < (MinimumLegCount * 2 - 1)) return false;

				//determine whether leg lengths are contracting
				double previousLength = double.PositiveInfinity;
				double length = double.PositiveInfinity;
				double previousLow = double.PositiveInfinity;
				double previousHigh = double.PositiveInfinity;
				for (int i = m_legs.Count - 2; i >= 0; i--)
				{
					//determine the length of the leg
					length = Math.Abs(m_legs[i].Item2 - m_legs[i + 1].Item2);

					//once we have two legs' lengths computed we need to compare the lengths
					//to ensure they are contracting
					if (previousLength != double.PositiveInfinity && length > previousLength) return false;

					//check that the legs overlay one another by the prescibed percentage
					if (MinimumLegOverlay > 0.0d)
					{
						double currentLow = Math.Min(m_legs[i].Item2, m_legs[i + 1].Item2);
						double currentHigh = Math.Max(m_legs[i].Item2, m_legs[i + 1].Item2);
						double lowest = Math.Min(currentLow, previousLow);
						double highest = Math.Max(currentHigh, previousHigh);
						double lowHighDelta = highest - lowest;

						//if no overlay return false
						if (currentLow > highest || currentHigh < lowest) return false;

						if (!double.IsInfinity(lowHighDelta) && lowHighDelta != 0.0d)
						{
							double overlay = double.PositiveInfinity;
							if (currentLow > previousLow && currentHigh > previousHigh)
								overlay = (previousHigh - currentLow) / lowHighDelta;
							else
								overlay = (currentHigh - previousLow) / lowHighDelta;

							if (overlay < MinimumLegOverlay) return false;
						}
					}

					//save values for the next iteration
					previousLow = Math.Min(m_legs[i].Item2, m_legs[i + 1].Item2);
					previousHigh = Math.Max(m_legs[i].Item2, m_legs[i + 1].Item2);
					previousLength = length;
				}

				//set the output properties for the block of bars that are forming VCP
				FirstBar = m_legs[m_legs.Count - 1].Item3;
				LastBar = m_legs[0].Item3;
				Low = vcpLow;
				High = vcpHigh;

				return true;
			}

			//methods
			

		}
	}
}