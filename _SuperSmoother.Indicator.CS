using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  [SameAsSymbol]
  public class _SuperSmoother : IndicatorObject
  {
    //Properties:
    [Input] public int Period { get; set; }

    //Attributes:
    private IPlotObject m_plot;
    private Function._SuperSmoother m_superSmoother;

    public _SuperSmoother(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      Period = 10;
      m_superSmoother = new Function._SuperSmoother(this);
      m_plot = AddPlot(new PlotAttributes("SS", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc()
    {
      m_superSmoother.Length = Period;
      m_superSmoother.Price = Bars.Close;
    }

    protected override void CalcBar()
    {
      m_plot.Set(m_superSmoother[0]);
    }
  }
}