using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Volume with average and levels of standard deviations to determine activity in the market.
	/// </summary>
	public sealed class _VolumeWithSD : FunctionSeries<double>
	{
		//Constants:
		public const int DEFAULT_LENGTH = 20;
		public const double DEFAULT_LEVEL1 = 1.0;
		public const double DEFAULT_LEVEL2 = 2.0;
		public const double DEFAULT_LEVEL3 = 3.0;

		//Properties:
		public int Length { get; set; }     //Length to use for average and standard deviation.
		public IInstrument Instrument { get; set; } //instrument around which the calculations are computed.
		public double Level1 { get; set; }    //Factor to use for first level of standard deviation.
		public double Level2 { get; set; }    //Factor to use for second level of standard deviation.
		public double Level3 { get; set; }  //Factor to use for third level of standard deviation.			
		public ISeries<double> Average { get { return m_averageSeries; } }
		public ISeries<double> AverageDollarVolume { get { return m_averageDollarVolumeSeries; } }  //average dollar volume over the period
		public ISeries<double> Sigma { get { return m_sigmaSeries; } }              //Sigma of the volume data.
		public ISeries<double> AvgPlusLevel1 { get { return m_avgPlusLevel1; } }    //Average plus level 1 of standard deviation.
		public ISeries<double> AvgPlusLevel2 { get { return m_avgPlusLevel2; } }    //Average plus level 2 of standard deviation.
		public ISeries<double> AvgPlusLevel3 { get { return m_avgPlusLevel3; } }    //Average plus level 3 of standard deviation.
		public ISeries<double> AvgMinusLevel1 { get { return m_avgMinusLevel1; } }  //Average minus level 1 of standard deviation.
		public ISeries<double> AvgMinusLevel2 { get { return m_avgMinusLevel2; } }  //Average minus level 2 of standard deviation.
		public ISeries<double> AvgMinusLevel3 { get { return m_avgMinusLevel3; } }  //Average minus level 3 of standard deviation.

		//Attributes:
		private VariableSeries<double> m_averageSeries;
		private VariableSeries<double> m_averageDollarVolumeSeries;
		private VariableSeries<double> m_sigmaSeries;
		private VariableSeries<double> m_avgPlusLevel1;
		private VariableSeries<double> m_avgPlusLevel2;
		private VariableSeries<double> m_avgPlusLevel3;
		private VariableSeries<double> m_avgMinusLevel1;
		private VariableSeries<double> m_avgMinusLevel2;
		private VariableSeries<double> m_avgMinusLevel3;

		public _VolumeWithSD(CStudyControl _master) : base(_master) { }
		public _VolumeWithSD(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_LENGTH;
			Level1 = DEFAULT_LEVEL1;
			Level2 = DEFAULT_LEVEL2;
			Level3 = DEFAULT_LEVEL3;
			m_averageSeries = new VariableSeries<double>(this);
			m_averageDollarVolumeSeries = new VariableSeries<double>(this);
			m_sigmaSeries = new VariableSeries<double>(this);
			m_avgPlusLevel1 = new VariableSeries<double>(this);
			m_avgPlusLevel2 = new VariableSeries<double>(this);
			m_avgPlusLevel3 = new VariableSeries<double>(this);
			m_avgMinusLevel1 = new VariableSeries<double>(this);
			m_avgMinusLevel2 = new VariableSeries<double>(this);
			m_avgMinusLevel3 = new VariableSeries<double>(this);
		}

		protected override void StartCalc()
		{
			if (Level1 > Level2) throw new ArgumentException("Level1 must be less than Level2.");
			if (Level2 > Level3) throw new ArgumentException("Level2 must be less than Level3.");
			if (Length < 1) throw new ArgumentException("Length must be greater than 0.");
		}

		protected override double CalcBar()
		{
			double volumeSum = 0.0d;
			double dollarVolumeSum = 0.0d;
			for (int index = Length; index > 0; index--)
			{
				volumeSum += Instrument.Volume[index];
				dollarVolumeSum += Instrument.Close[index] * Instrument.Volume[index];
			}

			double volumeAverage = volumeSum / Length;
			double dollarVolumeAverage = dollarVolumeSum / Length;
			double volumeDeltaSum = 0.0d;
			for (int index = Length; index > 0; index--)
			{
				double volume = Instrument.Volume[index];
				volumeDeltaSum += Math.Pow(volume - volumeAverage, 2);
			}

			double sigma = Math.Sqrt(volumeDeltaSum / Length);
			m_averageSeries.Value = volumeAverage;
			m_averageDollarVolumeSeries.Value = dollarVolumeAverage;
			m_sigmaSeries.Value = sigma;
			m_avgPlusLevel1.Value = volumeAverage + (Level1 * sigma);
			m_avgPlusLevel2.Value = volumeAverage + (Level2 * sigma);
			m_avgPlusLevel3.Value = volumeAverage + (Level3 * sigma);

			//average minus levels gets clipped to zero
			m_avgMinusLevel1.Value = volumeAverage - (Level1 * sigma);
			m_avgMinusLevel1.Value = m_avgMinusLevel1.Value >= 0 ? m_avgMinusLevel1.Value : 0;
			m_avgMinusLevel2.Value = volumeAverage - (Level2 * sigma);
			m_avgMinusLevel2.Value = m_avgMinusLevel2.Value >= 0 ? m_avgMinusLevel2.Value : 0;
			m_avgMinusLevel3.Value = volumeAverage - (Level3 * sigma);
			m_avgMinusLevel3.Value = m_avgMinusLevel3.Value >= 0 ? m_avgMinusLevel3.Value : 0;

			return volumeAverage;
		}
	}
}