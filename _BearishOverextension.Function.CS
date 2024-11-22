using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether a stock is in a bearish overextension with a gradual decline in price, we don't want to see massive
		/// volatility in the price that would make it hard to determine where the price is going.
		/// </summary>
		public sealed class _BearishOverextension : FunctionSimple<System.Boolean>
		{
			//constants


			//inputs


			//properties


			//attributes


			//interface methods
			public _BearishOverextension(CStudyControl _master) : base(_master) { }
			public _BearishOverextension(CStudyControl _master, int _ds) : base(_master, _ds) { }

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