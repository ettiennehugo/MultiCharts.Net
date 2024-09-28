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
      private SuperSmoother average;
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
        average = new SuperSmoother(this);
      }

      protected override void StartCalc()
      {
        if (Volume == null) Volume = Bars.TrueVolume();
        average.Period = Length;
        average.Price = Volume;
      }

      protected override double CalcBar()
      {
        double sd = Volume.StandardDeviationCustom(Length, 1);
        averageSeries.Value = average.Value;
        avgPlusLevel1.Value = average.Value + Level1 * sd;
        avgPlusLevel2.Value = average.Value + Level2 * sd;
        avgPlusLevel3.Value = average.Value + Level3 * sd;

        //average minus levels gets clipped to zero
        avgMinusLevel1.Value = average.Value - Level1 * sd;
        avgMinusLevel1.Value = avgMinusLevel1.Value >= 0 ? avgMinusLevel1.Value : 0;
        avgMinusLevel2.Value = average.Value - Level2 * sd;
        avgMinusLevel2.Value = avgMinusLevel2.Value >= 0 ? avgMinusLevel2.Value : 0;
        avgMinusLevel3.Value = average.Value - Level3 * sd;
        avgMinusLevel3.Value = avgMinusLevel3.Value >= 0 ? avgMinusLevel3.Value : 0;

        return average.Value;
      }
    }
  }
}