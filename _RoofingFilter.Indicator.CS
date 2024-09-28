using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  /// <summary>
  /// Roofing filter with zero mean capability as discussed by John F. Ehlers in Cycle Analytics for Traders p. 78 - 82
  /// </summary>
	public class _RoofingFilter : IndicatorObject {
    //Properties:
    [Input] public int LPLength { get; set; }
    [Input] public int HPLength { get; set; }

    //Attributes:
    private RoofingFilter m_roofingFilter;
    private IPlotObject m_plotRoofingFilter;
    private IPlotObject m_plotZero;

    public _RoofingFilter(object _ctx) : base(_ctx) { }

    protected override void Create() {
      LPLength = RoofingFilter.DEFAULT_LP_LENGTH;
      HPLength = RoofingFilter.DEFAULT_HP_LENGTH;
      m_roofingFilter = new RoofingFilter(this);
      m_plotRoofingFilter = AddPlot(new PlotAttributes("RFLT", EPlotShapes.Line, Color.Red));
      m_plotZero = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.LightGray));
    }

    protected override void StartCalc() {
      m_roofingFilter.LPLength = LPLength;
      m_roofingFilter.HPLength = HPLength;
      m_roofingFilter.Price = Bars.Close;
    }

    protected override void CalcBar() {
      m_plotZero.Set(0);
      m_plotRoofingFilter.Set(m_roofingFilter[0]);
    }
  }
}