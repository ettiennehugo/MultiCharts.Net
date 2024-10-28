using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Automatic Gain Control as discussed in Cycle Analytics for Traders by Dr. John F. Ehlers p. 54 (https://mesasoftware.com/).
		/// An automatic gain control provide a consistent indicator appearance independently from the range of input price swings. 
		/// The process of the automatic gain control divides the current price by the absolute value of the recent maximum swing so that
		/// the normalized waveform as a maximum of 1 and a minimum of -1. The specific implementation is a fast-attack slow-decay 
		/// automatic gain control.
		/// </summary>
		public sealed class AutomaticGainControl : FunctionSeries<System.Double>
		{
      //Constants:
      public const double DEFAULT_DECAY_FACTOR = 0.991;
			
			//Properties:
			public ISeries<double> Input;			//price or input value to standardize
			public double DecayFactor { get; set; }	//decay factor from input to output for signal
			
			//Attributes:
			private double MaxPower;
			
			
			public AutomaticGainControl(CStudyControl _master) : base(_master) { }
			public AutomaticGainControl(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create() {
				DecayFactor = DEFAULT_DECAY_FACTOR;
			}

			protected override void StartCalc() {
				//validate inputs
				if (Input == null) throw new Exception("Automatic gain control requires an input time series.");
				if (DecayFactor <=0 || DecayFactor >= 1) throw new Exception("Decay factor for automatic gain control must be between 0 and 1 (exclusive).");
				
				//initialize function state
				MaxPower = 0;
			}

			protected override System.Double CalcBar() {
        MaxPower = DecayFactor * MaxPower;
				double result = Input[0];
				if (Math.Abs(Input[0]) > MaxPower)
          MaxPower = Math.Abs(Input[0]);
				if (MaxPower != 0) result = result / MaxPower;
				return (result);
			}
		}
	}
}