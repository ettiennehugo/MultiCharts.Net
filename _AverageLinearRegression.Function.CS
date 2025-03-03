using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Linear regression average value, Multicharts version most likely do not work correctly on Multiple data feeds.
	/// </summary>
	public sealed class _AverageLinearRegression : FunctionSimple<System.Double>
	{
		//constants


		//types


		//properties
		public IInstrument Instrument { get; set; } //data feed to use for the average
		public int Length { get; set; }             //length of the average calculation

		//attribute


		//interface methods
		public _AverageLinearRegression(CStudyControl _master) : base(_master) { }
		public _AverageLinearRegression(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			// create variable objects and function objects
		}

		protected override void StartCalc()
		{
			// assign inputs

			//TODO - finish this and remove the LinReg usage in the strategy explorer.

		}

		protected override System.Double CalcBar()
		{
			// function logic
			return default(System.Double);
		}

		//methods


	}
}