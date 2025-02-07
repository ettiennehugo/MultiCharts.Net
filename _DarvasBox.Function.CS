using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the current bar can be considered to be the last bar forming a Darvas Box of at least X-bars long.
		/// Computes an average upper and lower range based on the ATR and checks that all the bars' low/high fall within the range or
		/// that the low/high of a bar at least would fall within the range setup for the bar.
		/// Notes:
		/// * Looking at the Darvas Box you need to be prepared to go both long and short as the box breakout can go in either direction.
		/// * If only the long side is desired then you need to filter stocks that are already up (e.g. 30 to 100% in the past 6 months)
		///   then only look for long breakouts.
		/// </summary>
		public sealed class _DarvasBox : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 10;
			public const double DEFAULT_EPSILON = 0.5;   //50% of ATR within the average low/high prices would be included in the Darvas box

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int Length { get; set; }       //required length of the Darvas box
			public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" - larger values would be needed since as bars contract the ATR gets smaller

			//properties
			public double Low { get; private set; }  //low of the Darvas box
			public double High { get; private set; } //high of the Darvas box
			public DateTime FirstBar { get; private set; }  //first bar of the Darvas box
			public DateTime LastBar { get; private set; }   //last bar of the Darvas box

			//attributes
			private Function._ATR m_atr;

			//interface methods
			public _DarvasBox(CStudyControl _master) : base(_master) { }
			public _DarvasBox(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				Epsilon = DEFAULT_EPSILON;
				m_atr = new Function._ATR(this);
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new ArgumentException("Length must be a positive number");
				if (Epsilon <= 0.0d) throw new ArgumentException("Epsilon must be a positive number");
				m_atr.Instrument = Instrument;
				m_atr.Length = Length;
			}

			protected override System.Boolean CalcBar()
			{
				double epilonValue = m_atr.Value * Epsilon;

				//implement the high average and low average for the scan length of bars
				double highAverage = 0;
				double lowAverage = 0;

				for (int i = Length; i > 0; i--)
				{
					highAverage += Instrument.High[i];
					lowAverage += Instrument.Low[i];
				}

				highAverage /= Length;
				lowAverage /= Length;

				//setup the upper and lower ranges based on the ATR
				FirstBar = DateTime.MinValue;
				LastBar = DateTime.MinValue;
				High = highAverage + epilonValue;
				Low = lowAverage - epilonValue;

				for (int i = Length - 1; i >= 0; i--)
					if (Instrument.Low[i] < Low || Instrument.High[i] > High) return false;

				FirstBar = Instrument.Time[Length - 1];
				LastBar = Instrument.Time[0];

				return true;  //all bars meet the criteria for the Darvas box
			}

			//methods


		}
	}
}