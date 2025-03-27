namespace PowerLanguage.Function
{
	/// <summary>
	/// Flags stocks that are experiencing a parabolic movement in price and is overly extended.
	/// </summary>
	public sealed class _ParabolicShort : FunctionSimple<System.Boolean>
	{
		//constants
		public const int DEFAULT_LENGTH = 10;             //10-bars will be scanned for growth
		public const double DEFAULT_GROWTH_PERCENT = 2.0; //need to be up at least a 100% (2.0) to be considered a spike

		//inputs
		public IInstrument Instrument { get; set; } //instrument price feed to use for the scan
		public int Length { get; set; }             //length over which the spike is measured
		public double GrowthPercent { get; set; }   //percentage growth over the length to be considered a spike

		//properties


		//attributes


		//interface methods
		public _ParabolicShort(CStudyControl _master) : base(_master) { }
		public _ParabolicShort(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_LENGTH;
			GrowthPercent = DEFAULT_GROWTH_PERCENT;
		}

		protected override void StartCalc() { }

		protected override System.Boolean CalcBar()
		{
			if (Environment.CalcReason != CalculationReason.Default) return false;
			if (Bars.CurrentBar < Length) return false;
			double closeAtLength = Bars.Close[Length];
			if (closeAtLength != 0.0d && (Instrument.Close[0] / closeAtLength) > GrowthPercent) return true;
			return false;
		}

		//methods


	}
}