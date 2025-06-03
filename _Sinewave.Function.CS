using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Computes a sinewave value based the price series and a given duration. Uses a single pole high-pass filter with super smoother, see 
	/// Cycle Analytics for Traders by Dr John Ehlers p. 159 (https://mesasoftware.com/).
	/// </summary>
	/// <parameter>Duration - Controls the maximum length of an indicated position by setting the critical period for the high-pass filter.</parameter>
	public sealed class _SineWave : FunctionSimple<System.Double>
	{
		//Constants:
		public const int DEFAULT_DURATION = 40;
		public const int DEFAULT_SMOOTHING_LENGTH = 10;

		//Properties:
		[Input] public int Duration { get; set; }

		//Attributes:
		private double alpha1;
		private double a1;
		private double b1;
		private double c1;
		private double c2;
		private double c3;
		private double highPass0;
		private double highPass1;
		private double wave;
		private double power;
		private VariableSeries<double> smoother;

		public _SineWave(CStudyControl _master) : base(_master) { }
		public _SineWave(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Duration = DEFAULT_DURATION;
			smoother = new VariableSeries<double>(this);
		}

		protected override void StartCalc()
		{
			if (Duration <= 0)
				throw new Exception("Sinewave duration must be larger than zero.");

			alpha1 = (1 - Math.Sin(2 * Math.PI / Duration)) / Math.Cos(2 * Math.PI / Duration);

			a1 = Math.Exp(-1.414 * Math.PI) / DEFAULT_SMOOTHING_LENGTH;
			b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / DEFAULT_SMOOTHING_LENGTH);
			c2 = b1;
			c3 = -a1 * a1;
			c1 = 1 - c2 - c3;
		}

		protected override System.Double CalcBar()
		{
			double result;

			//apply single pole high-pass filter
			highPass0 = 0.5 * (1 + alpha1) * (Bars.Close[0] - Bars.Close[1]) + alpha1 * highPass1;
			smoother.Value = c1 * (highPass0 + highPass1) / 2 + c2 * smoother[1] + c3 * smoother[2];

			//save look back memory values for next bar computation
			highPass1 = highPass0;

			//3-bar average of wave amplitude and power
			wave = (smoother[0] + smoother[1] + smoother[2]) / 3;
			power = (smoother[0] * smoother[0] + smoother[1] * smoother[1] + smoother[2] * smoother[2]) / 3;

			//normalize the average wave to square root of the average power
			try
			{
				result = wave / Math.Sqrt(power);
			}
			catch (ArithmeticException)
			{
				result = 0.0;   //TBD: Will this result in strange behavior???
			}

			return result;
		}
	}
}