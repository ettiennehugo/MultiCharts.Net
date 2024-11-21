using IronPython.Compiler.Ast;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Pullback gap trade or earnings gap trade where the stock is fairly down with a pull back to the average and then gaps up on earnings.
		/// 	• a better name would be an Earnings Gap Up Trade
	  ///   • screener - gap up > 10%
	  ///   • big volume near the open, best often trade their avg daily volume in the first 15 - 30 minutes
	  ///   • opening bar closes in its upper most 10% - 25% (or whatever percentage)
	  ///   • if earnings gap preferable high double digit or even triple digit earnings/revenue figures that beat expectations by a significant margin
		///     best if the stock is coming out of a base and not over-extended
		/// </summary>
		public sealed class _EpisodicPivot : FunctionSimple<System.Boolean>
		{
			public const double MINIMUM_PRICE = 5.0;
			public const int VOLUME_LENGTH = 20;
			public const int AVERAGE_VOLUME = 150000;
			public const int AVERAGE_DOLLAR_VOLUME = 20000000;
			public const double PERCENTAGE_CHANGE = 7.0;

			//inputs
			public double MinimumPrice { get; set; }
			public int VolumeLength { get; set; }
			public int AverageVolume { get; set; }
			public int AverageDollarVolume { get; set; }
			public double PercentageChange { get; set; }
			public bool Historical { get; set; }

			//properties
			public bool CriteriaMet { get { return m_criteriaMet; } }

			//attributes
			private double m_percentageChange;
			private bool m_historicalCriteria;
			private bool m_criteriaMet;

			//interface methods
			public _EpisodicPivot(CStudyControl _master) : base(_master) { }
			public _EpisodicPivot(CStudyControl _master, int _ds) : base(_master, _ds) { }

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

				m_percentageChange = PercentageChange / 100.0;
				m_historicalCriteria = false;
			}

			protected override System.Boolean CalcBar()
			{
				m_criteriaMet = false;

				if ((Historical || Bars.LastBarOnChart) && Bars.CurrentBar > VolumeLength)
				{
					//compute average dollar volume
					double averageDollarVolume = 0.0;
					for (int i = 0; i < VolumeLength; i++) averageDollarVolume = Bars.Close[i] * Bars.Volume[i];
					averageDollarVolume /= VolumeLength;

					m_criteriaMet = PublicFunctions.DoubleGreater(Bars.Close[0], MinimumPrice) &&
												PublicFunctions.DoubleGreaterEquals(Bars.Volume.Average(VolumeLength), AverageVolume) &&
												PublicFunctions.DoubleGreater((Bars.Close[0] / Bars.Close[1]) - 1, m_percentageChange) &&
												PublicFunctions.DoubleGreater(averageDollarVolume, AverageDollarVolume);
					m_historicalCriteria = Historical && !m_historicalCriteria ? m_criteriaMet : m_historicalCriteria;
				}

				return m_historicalCriteria;
			}

			//methods


		}
	}
}