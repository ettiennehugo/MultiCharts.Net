using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Flags stocks that are experiencing a short-term spike in price and is overly extended.
		/// </summary>
		public sealed class _ShortTheSpike : FunctionSimple<System.Boolean>
		{
			//constants


			//inputs


			//properties


			//attributes


			//interface methods
			public _ShortTheSpike(CStudyControl _master) : base(_master) { }
			public _ShortTheSpike(CStudyControl _master, int _ds) : base(_master, _ds) { }

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