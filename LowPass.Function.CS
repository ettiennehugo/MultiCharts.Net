using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Implementation of a low-pass filter for a price series.
		/// 
		/// TODO: Implement different filters to see the effect it has on trading signals.
		/// 
		/// </summary>
		public sealed class LowPass : FunctionSimple<System.Double>
		{
			//[Input] public ISeries<double> Price { get; set; }
			//[Input] public int Length { get; set; }


			public LowPass(CStudyControl _master) : base(_master) { }
			public LowPass(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				// create variable objects and function objects
			}

			protected override void StartCalc()
			{
				// assign inputs
			}

			protected override System.Double CalcBar()
			{

				//IMPORTANT: Remember to check for Bars.Status == EBarState.Close in computation so that you do not compute
				//           the low-pass filter on every tick.

				// function logic
				return default(System.Double);
			}
		}
	}
}