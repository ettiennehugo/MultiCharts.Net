using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
  namespace Function
  {
		/// <summary>
		/// SuperSmoother filter as discussed by Dr John F. Ehlers in Cycle Analytics for Traders p. 31 (https://mesasoftware.com/).
		/// </summary>
		public sealed class SuperSmoother : FunctionSeries<System.Double>
    {
      //Constants:
      public const int DEFAULT_PERIOD = 20;
      public const double SQRT_2 = 1.414213562373095d;

      //Properties:
      public int Period { get; set; }
      public ISeries<double> Price { get; set; }
      public double A1 { get { return m_a1; } }
      public double B1 { get { return m_b1; } }
      public double C1 { get { return m_c1; } }
      public double C2 { get { return m_c2; } }
      public double C3 { get { return m_c3; } }

      //Attributes:
      private double m_result;
      private double m_a1;
      private double m_b1;
      private double m_c1;
      private double m_c2;
      private double m_c3;

      public SuperSmoother(CStudyControl _master) : base(_master) { }
      public SuperSmoother(CStudyControl _master, int _ds) : base(_master, _ds) { }

      protected override void Create()
      {
        Period = DEFAULT_PERIOD;
      }

      protected override void StartCalc()
      {
        if (Period <= 0)
          throw new Exception("Supersmoother requires a positive integer for the period.");

        //compute the coefficients of the SuperSmoother based on given period
        m_result = 0;
        m_a1 = Math.Exp(-SQRT_2 * Math.PI / Period);
        m_b1 = 2.0d * m_a1 * Math.Cos(SQRT_2 * Math.PI / Period);
        m_c2 = m_b1;
        m_c3 = -m_a1 * m_a1;
        m_c1 = 1.0d - m_c2 - m_c3;
      }

      protected override double CalcBar()
      {
      if (Bars.CurrentBar < 4)
        m_result = Price[0];
      else
        m_result = m_c1 * (Price[0] + Price[1]) / 2.0d + m_c2 * this[1] + m_c3 * this[2];

        return m_result;
      }
    }
  }
}