using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
  namespace Function
  {
    /// <summary>
    /// Volume with average and levels of standard deviations to determine activity in the market.
    /// </summary>
    public sealed class VolumeWithSD : FunctionSeries<double>
    {
      //Constants:
      public const int DEFAULT_LENGTH = 20;
      public const double DEFAULT_LEVEL1 = 1.0;
      public const double DEFAULT_LEVEL2 = 2.0;
      public const double DEFAULT_LEVEL3 = 3.0;

      //Properties:
      public int Length { get; set; }     //Length to use for average and standard deviation.
      public ISeries<double> Volume { get; set; } //Volume around which values are calculated.
      public double Level1 { get; set; }    //Factor to use for first level of standard deviation.
      public double Level2 { get; set; }    //Factor to use for second level of standard deviation.
      public double Level3 { get; set; }	//Factor to use for third level of standard deviation.			
      public ISeries<double> Average { get { return averageSeries; } }
      public ISeries<double> AvgPlusLevel1 { get { return avgPlusLevel1; } }    //Average plus level 1 of standard deviation.
      public ISeries<double> AvgPlusLevel2 { get { return avgPlusLevel2; } }    //Average plus level 2 of standard deviation.
      public ISeries<double> AvgPlusLevel3 { get { return avgPlusLevel3; } }    //Average plus level 3 of standard deviation.
      public ISeries<double> AvgMinusLevel1 { get { return avgMinusLevel1; } }  //Average minus level 1 of standard deviation.
      public ISeries<double> AvgMinusLevel2 { get { return avgMinusLevel2; } }  //Average minus level 2 of standard deviation.
      public ISeries<double> AvgMinusLevel3 { get { return avgMinusLevel3; } }  //Average minus level 3 of standard deviation.

      //Attributes:
      private VariableSeries<double> averageSeries;
      private VariableSeries<double> avgPlusLevel1;
      private VariableSeries<double> avgPlusLevel2;
      private VariableSeries<double> avgPlusLevel3;
      private VariableSeries<double> avgMinusLevel1;
      private VariableSeries<double> avgMinusLevel2;
      private VariableSeries<double> avgMinusLevel3;

      public VolumeWithSD(CStudyControl _master) : base(_master) { }
      public VolumeWithSD(CStudyControl _master, int _ds) : base(_master, _ds) { }

      protected override void Create()
      {
        Length = DEFAULT_LENGTH;
        Level1 = DEFAULT_LEVEL1;
        Level2 = DEFAULT_LEVEL2;
        Level3 = DEFAULT_LEVEL3;
        averageSeries = new VariableSeries<double>(this);
        avgPlusLevel1 = new VariableSeries<double>(this);
        avgPlusLevel2 = new VariableSeries<double>(this);
        avgPlusLevel3 = new VariableSeries<double>(this);
        avgMinusLevel1 = new VariableSeries<double>(this);
        avgMinusLevel2 = new VariableSeries<double>(this);
        avgMinusLevel3 = new VariableSeries<double>(this);
      }

      protected override void StartCalc()
      {
        if (Volume == null) throw new InvalidOperationException("Volume series is not set.");
				if (Length < 1) throw new InvalidOperationException("Length must be greater than 0.");
			}

      protected override double CalcBar()
      {
				double volumeSum = 0.0d;
				for (int index = Length; index > 0; index--)
					volumeSum += Volume[index];
				
        double volumeAverage = volumeSum / Length;
				double volumeDeltaSum = 0.0d;
				for (int index = Length; index > 0; index--)
				{
					double volume = Volume[index];
					volumeDeltaSum += Math.Pow(volume - volumeAverage, 2);
				}

				double sigma = Math.Sqrt(volumeDeltaSum / Length);
				averageSeries.Value = volumeAverage;
        avgPlusLevel1.Value = volumeAverage + (Level1 * sigma);
        avgPlusLevel2.Value = volumeAverage + (Level2 * sigma);
        avgPlusLevel3.Value = volumeAverage + (Level3 * sigma);

        //average minus levels gets clipped to zero
        avgMinusLevel1.Value = volumeAverage - (Level1 * sigma);
        avgMinusLevel1.Value = avgMinusLevel1.Value >= 0 ? avgMinusLevel1.Value : 0;
        avgMinusLevel2.Value = volumeAverage - (Level2 * sigma);
        avgMinusLevel2.Value = avgMinusLevel2.Value >= 0 ? avgMinusLevel2.Value : 0;
        avgMinusLevel3.Value = volumeAverage - (Level3 * sigma);
        avgMinusLevel3.Value = avgMinusLevel3.Value >= 0 ? avgMinusLevel3.Value : 0;

        return volumeAverage;
      }
    }
  }
}