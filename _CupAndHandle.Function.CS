using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Function to determine whether the last set of bars can be considered a cup and handle pattern.
		/// </summary>
		public sealed class _CupAndHandle : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 100;

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan
			public int Length { get; set; }							//scan length for the cup-and-handle pattern

			//properties


			//attributes


			//interface methods
			public _CupAndHandle(CStudyControl _master) : base(_master) { }
			public _CupAndHandle(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				// create variable objects and function objects
			}

			protected override void StartCalc()
			{
				// assign inputs
			}

			protected override System.Boolean CalcBar()
			{
				// function logic
				return default(System.Boolean);
			}

			//methods


		}
	}
}