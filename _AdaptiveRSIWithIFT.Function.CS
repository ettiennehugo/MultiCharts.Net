namespace PowerLanguage.Function
{
	/// <summary>
	/// Adaptive RSI with IFT - this function calculates the Adaptive Relative Strength Index (RSI) using the Inverse Fisher Transform (IFT) as discussed in
	/// Cycle Analytics for Traders by John Ehlers p. 195.
	/// </summary>
	public sealed class _AdaptiveRSIWithIFT : FunctionSeries<System.Double>
	{
		//constants


		//types


		//properties


		//attribute


		//TODO - Finish AdaptiveRSI function and then implement this function as well with the inverse fisher transform (IFT) logic.


		//interface methods
		public _AdaptiveRSIWithIFT(CStudyControl _master) : base(_master) { }
		public _AdaptiveRSIWithIFT(CStudyControl _master, int _ds) : base(_master, _ds) { }

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
			// function logic
			return default(System.Double);
		}

		//methods


	}
}