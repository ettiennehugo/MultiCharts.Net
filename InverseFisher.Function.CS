using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage {
  namespace Function {
    /// <summary>
    /// Calculates the Inverse Fisher Transform of a price data value. This transform typically needs to be applied to the outputs of oscillators to transform
    /// the indicator into a bipolar distribution where the results cluster near the extremes of +1 and -1. See Trading Systems and Methods 5th Edition p. 520 or
    /// https://www.mql5.com/en/articles/303
    /// </summary>
    public sealed class InverseFisher : FunctionSimple<System.Double> {
      public ISeries<double> Price { get; set; }
      public int Length { get; set; } //length used to determine low and high values to normalize the incoming price data

      public InverseFisher(CStudyControl _master) : base(_master) { }
      public InverseFisher(CStudyControl _master, int _ds) : base(_master, _ds) { }

      protected override System.Double CalcBar() {
        return ((Math.Exp(2 * Price[0]) - 1.0) / (Math.Exp(2 * Price[0]) + 1));
      }
    }
  }
}