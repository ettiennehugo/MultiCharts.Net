using System;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Scanner function to determine whether an apisodic pivot is busy forming for a short position.
		/// </summary>
		public sealed class _ScannerEpisodicPivot : FunctionSimple<System.Boolean>
		{
			//constants
			public const double MINIMUM_PRICE = 5.0;
			public const int VOLUME_LENGTH = 20;
			public const int AVERAGE_VOLUME = 150000;
			public const int AVERAGE_DOLLAR_VOLUME = 20000000;
			public const double PERCENTAGE_CHANGE = 7.0;

			//inputs
			[Input] public double MinimumPrice { get; set; }
			[Input] public int VolumeLength { get; set; }
			[Input] public int AverageVolume { get; set; }
			[Input] public int AverageDollarVolume { get; set; }
			[Input] public double PercentageChange { get; set; }
			[Input] public bool Historical { get; set; }

			//properties
			public bool CriteriaMet { get { return criteriaMet; } }

			//attributes
			private double percentageChange;
			private bool historicalCriteria;
			private bool criteriaMet;

			public _ScannerEpisodicPivot(CStudyControl _master) : base(_master) { }
			public _ScannerEpisodicPivot(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumPrice = MINIMUM_PRICE;
				VolumeLength = VOLUME_LENGTH;
				AverageVolume = AVERAGE_VOLUME;
				AverageDollarVolume = AVERAGE_DOLLAR_VOLUME;
				PercentageChange = PERCENTAGE_CHANGE;
				Historical = false;
			}

			protected override void StartCalc()
			{
				if (MinimumPrice <= 0) throw new Exception("MinimumPrice must be larger than zero.");
				if (VolumeLength <= 0) throw new Exception("VolumeLength must be larger than zero.");
				if (AverageVolume <= 0) throw new Exception("AverageVolume must be larger than zero.");
				if (AverageDollarVolume <= 0) throw new Exception("AverageDollarVolume must be larger than zero.");
				if (PercentageChange <= 0) throw new Exception("PercentageChange must be larger than zero.");

				percentageChange = PercentageChange / 100.0;
				historicalCriteria = false;
			}

			protected override System.Boolean CalcBar()
			{
				criteriaMet = false;

				if ((Historical || Bars.LastBarOnChart) && Bars.CurrentBar > VolumeLength)
				{
					//compute average dollar volume
					double averageDollarVolume = 0.0;
					for (int i = 0; i < VolumeLength; i++) averageDollarVolume = Bars.Close[i] * Bars.Volume[i];
					averageDollarVolume /= VolumeLength;

					criteriaMet = PublicFunctions.DoubleGreater(Bars.Close[0], MinimumPrice) &&
												PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(VolumeLength), AverageVolume) &&
												PublicFunctions.DoubleGreater((Bars.Close[0] / Bars.Close[1]) - 1, percentageChange) &&
												PublicFunctions.DoubleGreater(averageDollarVolume, AverageDollarVolume);
					historicalCriteria = Historical && !historicalCriteria ? criteriaMet : historicalCriteria;
				}

				return historicalCriteria;
			}
		}
	}
}