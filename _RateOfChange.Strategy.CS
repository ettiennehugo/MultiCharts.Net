using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Plots the rate of change between a set of instruments. This INDICATOR is implemented as a strategy since the standard indicator
	/// objects are not allowed access to multiple data streams.
  /// </summary>
  public class _RateOfChange : SignalObject, IChartCustomDrawer
  {
    //Inputs:
    [Input] public int Length { get; set; }           //length to lookback for rate of change on price
    [Input] public int CumulativeLength { get; set; } //cumulative length lookback to draw cumulative changes over Length input, 0 = disable 
    [Input] public Color ZeroColor { get; set; }
    [Input] public Color Color1 { get; set; }
    [Input] public Color Color2 { get; set; }
    [Input] public Color Color3 { get; set; }
    [Input] public Color Color4 { get; set; }
    [Input] public Color Color5 { get; set; }

    //Attributes:    
    private IInstrument bars1;
    private IInstrument bars2;
    private IInstrument bars3;
    private IInstrument bars4;
    private IInstrument bars5;

    private VariableSeries<double> rof1;    //rate of change
    private VariableSeries<double> crof1;   //cumulative rate of change
    private VariableSeries<double> rof2;
    private VariableSeries<double> crof2;
    private VariableSeries<double> rof3;
    private VariableSeries<double> crof3;
    private VariableSeries<double> rof4;
    private VariableSeries<double> crof4;
    private VariableSeries<double> rof5;
    private VariableSeries<double> crof5;

    public _RateOfChange(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);

      //set defaults for inputs
      Length = 1;
      CumulativeLength = 0;
      ZeroColor = Color.LightGray;
      Color1 = Color.Red;
      Color2 = Color.Violet;
      Color3 = Color.Orange;
      Color4 = Color.Indigo;
      Color5 = Color.Yellow;

      //create the data series for the rate of change and cumulative rate of change
      rof1 = new VariableSeries<double>(this);
      crof1 = new VariableSeries<double>(this);
      rof2 = new VariableSeries<double>(this);
      crof2 = new VariableSeries<double>(this);
      rof3 = new VariableSeries<double>(this);
      crof3 = new VariableSeries<double>(this);
      rof4 = new VariableSeries<double>(this);
      crof4 = new VariableSeries<double>(this);
      rof5 = new VariableSeries<double>(this);
      crof5 = new VariableSeries<double>(this);
    }

    protected override void Destroy()
    {
      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc()
    {
      //get the set of data feeds applied to the chart
      bars1 = Bars;
      try { bars2 = BarsOfData(2); } catch (Exception) { bars2 = null; }
      try { bars3 = BarsOfData(3); } catch (Exception) { bars3 = null; }
      try { bars4 = BarsOfData(4); } catch (Exception) { bars4 = null; }
      try { bars5 = BarsOfData(5); } catch (Exception) { bars5 = null; }
    }

    protected override void CalcBar()
    {
      calcRofCrof(bars1, rof1, crof1);
      calcRofCrof(bars2, rof2, crof2);
      calcRofCrof(bars3, rof3, crof3);
      calcRofCrof(bars4, rof4, crof4);
      calcRofCrof(bars5, rof5, crof5);
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      if (phase == EDrawPhases.Final)
      {
        //draw zero line
        using (Pen pen = new Pen(ZeroColor)) {
          ChartPoint startCP = new ChartPoint(bars1.Time[1], 0);
          ChartPoint endCP = new ChartPoint(bars1.Time[0], 0);
          PointF start = context.Environment.ChartPoint2Point(startCP);
          PointF end = context.Environment.ChartPoint2Point(endCP);
          if (context.DirtyRect.Contains(start) || context.DirtyRect.Contains(end)) context.graphics.DrawLine(pen, start, end);
        }

        //draw rates of change
        drawRofCrof(context, Color1, bars1, rof1, crof1);
        drawRofCrof(context, Color2, bars2, rof2, crof2);
        drawRofCrof(context, Color3, bars3, rof3, crof3);
        drawRofCrof(context, Color4, bars4, rof4, crof4);
        drawRofCrof(context, Color5, bars5, rof5, crof5);
      }
    }

    private void calcRofCrof(IInstrument bars, VariableSeries<double> rof, VariableSeries<double> crof)
    {
      if (bars != null)
      {
        rof.Value = bars.Close.RateOfChange(Length);
        crof.Value = 0;
        for (int i = 0; i < CumulativeLength; i++) crof.Value += rof[i];
        crof.Value = crof.Value == 0 ? rof.Value : crof.Value;
      }
    }

    private void drawRofCrof(DrawContext context, Color color, IInstrument bars, VariableSeries<double> rof, VariableSeries<double> crof)
    {
      if (bars != null && bars.CurrentBar >= 2)
      {
        using (Pen pen = new Pen(color))
        {
          //render rate of change vs cumulative rate of change
          ChartPoint startCP;
          ChartPoint endCP;

          if (CumulativeLength != 0)
          {
            startCP = new ChartPoint(bars.Time[1], crof[1]);
            endCP = new ChartPoint(bars.Time[0], crof[0]);
          }
          else
          {
            startCP = new ChartPoint(bars.Time[1], rof[1]);
            endCP = new ChartPoint(bars.Time[0], rof[0]);
          }

          PointF start = context.Environment.ChartPoint2Point(startCP);
          PointF end = context.Environment.ChartPoint2Point(endCP);
          if (context.DirtyRect.Contains(start) || context.DirtyRect.Contains(end)) context.graphics.DrawLine(pen, start, end);
        }
      }
    }
  }
}
