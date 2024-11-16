using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		public sealed class _HighVolumeSpike : FunctionSimple<System.Boolean>
		{
			public _HighVolumeSpike(CStudyControl _master) : base(_master) { }
			public _HighVolumeSpike(CStudyControl _master, int _ds) : base(_master, _ds) { }

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
		}
	}
}