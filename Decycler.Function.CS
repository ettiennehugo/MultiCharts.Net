using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
    /// <summary>
    /// Decycler filter as discussed by John Ehlers. Is essentially a high-pass filter that removes cycle components to isolate
    /// trend moves with minimal amount of lag. See Cycle Analytics for Traders p. 39
    /// </summary>
		public sealed class Decycler : FunctionSeries<System.Double>
		{
      //Constants:
      public const double DEFAULT_CUTOFF = 60.0;

      //Properties:
      public double Cutoff { get; set; }
      public ISeries<double> Price { get; set; }

      //Attributes:
      private double alpha;

      public Decycler(CStudyControl _master) : base(_master) { }
			public Decycler(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
        Cutoff = DEFAULT_CUTOFF;
			}

			protected override void StartCalc()
			{
        //cutoff frequency should be a positive number
        if (Cutoff <= 0)
          throw new Exception("Cutoff frequency can not be zero or negative.");

        alpha = Math.Cos(Math.PI / Cutoff) + Math.Sin(Math.PI / Cutoff) - 1 / Math.Cos(Math.PI / Cutoff);
			}

			protected override System.Double CalcBar()
			{
        double result;
        if (Bars.CurrentBar <= 3)
          result = Price[0];
        else
          result = (alpha / 2) * (Price[0] + Price[1]) + (1 - alpha) * this[1];

				return result;
			}
		}
	}
}