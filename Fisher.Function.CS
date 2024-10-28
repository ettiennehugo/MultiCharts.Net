using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage {
  namespace Function {
    /// <summary>
    /// Returns the Fisher transform of a given price series. This transformation normalizes the price series so that it has a more normal distribution of values. 
    /// Oscillators can typically be improved by first feeding the price data trough a Fisher transform before feeding the transformed price series to the oscillator. 
    /// This will result in the oscillator being more resilient to price extremes due to noise in the price data. See discussion in Trading Systems and Methods 5th 
    /// Edition p. 518 or https://www.mql5.com/en/articles/303
    /// </summary>
    public sealed class Fisher : FunctionSimple<System.Double> {
      public ISeries<double> Price { get; set; }
      public int Length { get; set; }	//length used to determine low and high values to normalize the incoming price data

      private double _previousDistribution;
      private double _result;

      public Fisher(CStudyControl _master) : base(_master) { }
      public Fisher(CStudyControl _master, int _ds) : base(_master, _ds) { }

      protected override void StartCalc() {
        Length = 10;
        _result = 0.0;
        _previousDistribution = 0.0;
      }

      protected override System.Double CalcBar() {
        //normalize the prices into a range of (-1, 1) to have a standard distribution of prices - this is required by the Fisher transform function
        double MinL = Price.Lowest(Length);
        double MaxH = Price.Highest(Length);
        double distribution = 0.5 * 2 * ((Price.Value - MinL) / (MaxH - MinL) - 0.5) + 0.5 * _previousDistribution; //TBD: This previousDistribution looks suspiciously wrong!!!!
        distribution = distribution > 0.999 ? 0.999 : distribution;
        distribution = distribution < -0.999 ? -0.999 : distribution;
        _previousDistribution = distribution;
        _result = 0.5 * Math.Log((1 + distribution) / (1 - distribution));

        return _result;
      }
    }
  }
}