using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using System.Drawing.Drawing2D;

namespace PowerLanguage.Indicator {
  [SameAsSymbol]
  public class _Ichimoku : IndicatorObject, IChartCustomDrawer {
    //Inputs
    [Input] public int TenkanSenLength { get; set; }
    [Input] public int KijinSenLength { get; set; }
    [Input] public int ChikouSpanShift { get; set; }
    [Input] public int SenkouSpanBLength { get; set; }
    [Input] public int KumoShift { get; set; }
    [Input] public Color KumoBullishColor { get; set; }
    [Input] public Color KumoBearishColor { get; set; }
    [Input] public bool FillKumo { get; set; }

    public _Ichimoku(object _ctx) : base(_ctx) {
      TenkanSenLength = 9;
      KijinSenLength = 26;
      ChikouSpanShift = 26;
      SenkouSpanBLength = 52;
      KumoShift = 26;
      KumoBullishColor = Color.Green;
      KumoBearishColor = Color.Red;
      FillKumo = true;
    }

    private const int DefaultKumoAlpha = 60;
    private IPlotObject _tenkanSenPlot;
    private IPlotObject _kijinSenPlot;
    private IPlotObject _chikouSpanPlot;
    private IPlotObject _senkouSpanAPlot;
    private IPlotObject _senkouSpanBPlot;
    private SortedList<DateTime, ChartPoint> _senkouSpanA;
    private SortedList<DateTime, ChartPoint> _senkouSpanB;
    private long _barTickResolution;
    private int _longestLength = -1;

    protected override void Create() {
      //ChartCustomDraw.Register(this);

      _tenkanSenPlot = AddPlot(new PlotAttributes("TenkanSen", EPlotShapes.Line, Color.Blue));
      _kijinSenPlot = AddPlot(new PlotAttributes("KijinSen", EPlotShapes.Line, Color.Green));
      _chikouSpanPlot = AddPlot(new PlotAttributes("ChikouSpan", EPlotShapes.Line, Color.MediumPurple));
      _senkouSpanAPlot = AddPlot(new PlotAttributes("SenkouSpanA", EPlotShapes.Line, Color.LightGray));
      _senkouSpanBPlot = AddPlot(new PlotAttributes("SenkouSpanB", EPlotShapes.Line, Color.Gray));
      _senkouSpanA = new SortedList<DateTime, ChartPoint>();
      _senkouSpanB = new SortedList<DateTime, ChartPoint>();
      _barTickResolution = -1;
      _longestLength = -1;
    }

    //protected override void Destroy() {
    //    ChartCustomDraw.Unregister(this);
    //}

    protected override void StartCalc() {
      //calculate the bar tick resolution if not set yet
      _barTickResolution = Bars.Info.Resolution.Duration().Ticks;

      //calculate the longest indicator length we're working with
      _longestLength = TenkanSenLength;
      _longestLength = _longestLength > KijinSenLength ? _longestLength : KijinSenLength;
      _longestLength = _longestLength > ChikouSpanShift ? _longestLength : ChikouSpanShift;
      _longestLength = _longestLength > KumoShift ? _longestLength : KumoShift;
      _longestLength = _longestLength > SenkouSpanBLength ? _longestLength : SenkouSpanBLength;

      //correct the Kumo alpha blending
      KumoBullishColor = Color.FromArgb(DefaultKumoAlpha, KumoBullishColor.R, KumoBullishColor.G, KumoBullishColor.B);
      KumoBearishColor = Color.FromArgb(DefaultKumoAlpha, KumoBearishColor.R, KumoBearishColor.G, KumoBearishColor.B);
    }

    protected override void CalcBar() {
      //update indicator only if we have enough bars to correctly compute it
      if (Bars.CurrentBar > _longestLength && Bars.Status == EBarState.Close) {
        //calculate the indicator values
        double tenkanSen = (Bars.Close.Highest(TenkanSenLength) + Bars.Close.Lowest(TenkanSenLength)) / 2;
        double kijinSen = (Bars.Close.Highest(KijinSenLength) + Bars.Close.Lowest(KijinSenLength)) / 2;
        double chikouSpan = Bars.Close[0];
        double senkouSpanA = (tenkanSen + kijinSen) / 2;
        double senkouSpanB = (Bars.Close.Highest(SenkouSpanBLength) + Bars.Close.Lowest(SenkouSpanBLength)) / 2;

        DateTime kumoTimestamp = Bars.Time[0].AddTicks(_barTickResolution * KumoShift);
        _senkouSpanA[kumoTimestamp] = new ChartPoint(kumoTimestamp, senkouSpanA);
        _senkouSpanB[kumoTimestamp] = new ChartPoint(kumoTimestamp, senkouSpanB);

        //render all the plots for the indicator
        //NOTE: For the KumoShift to work correctly you have to set the Format Window->X Time Scale->Chart Shift parameter to be larger than
        //      the shift used for the Kumo cloud.
        _tenkanSenPlot.Set(tenkanSen);
        _kijinSenPlot.Set(kijinSen);
        _chikouSpanPlot.Set(ChikouSpanShift, chikouSpan);
        _senkouSpanAPlot.Set(-KumoShift, senkouSpanA);
        _senkouSpanBPlot.Set(-KumoShift, senkouSpanB);
      }
    }


    //TODO: 
    // * Custom drawing is unhooked from indicator.
    // * This custom rendering routine is not correct yet, it seems like on trading session boundaries the fill points are incorrectly computed causing the
    //   kumo cloud fill to be incorrect.

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase) {
      if (FillKumo && phase == EDrawPhases.Final) {
        using (Brush bullishBrush = new SolidBrush(KumoBullishColor))
        using (Brush bearishBrush = new SolidBrush(KumoBearishColor)) {
          Brush renderBrush = bullishBrush;       //default render brush to keep the compiler happy
          PointF prevSsaPoint = new PointF(-1, -1);
          PointF prevSsbPoint = new PointF(-1, -1);
          List<PointF> ssaCurve = new List<PointF>();
          List<PointF> ssbCurve = new List<PointF>();
          bool constructingPolygon = false;

          for (int index = 0; index < _senkouSpanA.Count; index++) {
            //convert chart points to window points
            PointF ssaPoint = context.Environment.ChartPoint2Point(_senkouSpanA.Values[index]);
            PointF ssbPoint = context.Environment.ChartPoint2Point(_senkouSpanB.Values[index]);

            //Algorithm
            // If we're constructing a figure
            //   Check whether we should end the figure, that is:
            //      When SSA/SSB is the same OR
            //      When SSA/SSB crosses over each other
            //        Compute the line intersection points formed by the SSA/SSB crossover.
            //      Render the figure polygon.
            //      Clear the SSA/SSB curve points.
            //      Add the computed end point for the polygon to the SSA curve since that's where the next polygon should start.
            //   If we should not end the polygon then just add the SSA/SSB points and render it.
            // If we're not constructing a polygon then start a new polygon if the SSA/SSB points differ.
            //   If the previous SSA/SSB points are the same then add that single point as a starting point for the
            //   figure with the current SSA/SSB points.
            //   If the previous SSA/SSB points are not the same then compute the mid-point between the previous SSA/SSB and
            //   the current points and use that point as the start of the figure before adding the current SSA/SSB points to
            //   the path.

            //check whether we're constructing a new figure
            if (true == constructingPolygon) {
              //check whether we must end the current figure
              if ((ssaPoint.Y == ssbPoint.Y) ||
                  (ssaPoint.Y > ssbPoint.Y && prevSsaPoint.Y <= prevSsbPoint.Y) ||
                  (ssaPoint.Y < ssbPoint.Y && prevSsaPoint.Y >= prevSsbPoint.Y)) {
                //figure must be closed, determine end point between the SSA/SSB curves and add points
                PointF endPoint;
                if (ssaPoint.Y == ssbPoint.Y)
                  endPoint = new PointF(ssaPoint.X, ssaPoint.Y);
                else {
                  //compute the intersection between the two line segments formed from the four SSA/SSB points
                  //see solving determinants for line intersection: https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
                  float x = (prevSsaPoint.X * ssaPoint.Y - prevSsaPoint.Y * ssaPoint.X) * (prevSsbPoint.X - ssbPoint.X) - (prevSsaPoint.X - ssaPoint.X) * (prevSsbPoint.X * ssbPoint.Y - prevSsbPoint.Y * ssbPoint.X);
                  float y = (prevSsaPoint.X * ssaPoint.Y - prevSsaPoint.Y * ssaPoint.X) * (prevSsbPoint.Y - ssbPoint.Y) - (prevSsaPoint.Y - ssaPoint.Y) * (prevSsbPoint.X * ssbPoint.Y - prevSsbPoint.Y * ssbPoint.X);
                  float divider = (prevSsaPoint.X - ssaPoint.X) * (prevSsbPoint.Y - ssbPoint.Y) - (prevSsaPoint.Y - ssaPoint.Y) * (prevSsbPoint.X - ssbPoint.X);
                  x = x / divider;
                  y = y / divider;
                  endPoint = new PointF(x, y);
                }

                ssaCurve.Add(ssaPoint);
                ssaCurve.Add(endPoint);
                ssbCurve.Add(ssbPoint);

                //construct total set of SSA/SSB points for the polygon, SSB curve points must be reversed so we can construct a closed polygon of points
                ssbCurve.Reverse();
                ssaCurve.AddRange(ssbCurve);

                PointF[] points = ssaCurve.ToArray();
                GraphicsPath path = new GraphicsPath(FillMode.Alternate);
                path.AddLines(points);

                //render the polygon if it intersects with the dirty region
                using (Region pathRegion = new Region(path)) {
                  if (pathRegion.IsVisible(context.DirtyRect)) {
                    pathRegion.Intersect(context.DirtyRect);
                    CompositingQuality previousQuality = context.graphics.CompositingQuality;
                    context.graphics.CompositingQuality = CompositingQuality.GammaCorrected;
                    context.graphics.FillRegion(renderBrush, pathRegion);
                    context.graphics.CompositingQuality = previousQuality;
                  }
                }

                //clear the curve lists
                ssaCurve.Clear();
                ssbCurve.Clear();

                //next polygon should start where previous polygon ended
                ssaCurve.Add(endPoint);

                //flag that we've finished a figure and the construction of another figure needs to be started
                constructingPolygon = false;

              } else {
                //figure continues, simply add current SSA/SSB points
                ssaCurve.Add(ssaPoint);
                ssbCurve.Add(ssbPoint);
              }
            }

            //check whether we need to detect starting a new figure
            //BOUNDARY CASE: We need to ignore the first bar in the series so that we can setup the previous SSA/SSB points first.
            if (false == constructingPolygon && 0 != index) {
              //start new figure if the SSA/SSB points differ
              if (ssaPoint.Y != ssbPoint.Y) {
                ssaCurve.Add(ssaPoint);
                ssbCurve.Add(ssbPoint);

                //setup the rendering brush for the cloud based on the SSA/SSB curve relationships
                if (ssaPoint.Y >= ssbPoint.Y)
                  renderBrush = bearishBrush;
                else
                  renderBrush = bullishBrush;

                //start accumulating points for the polygon to render
                constructingPolygon = true;
              }
            }

            //save previous curve points for analysis
            prevSsaPoint = ssaPoint;
            prevSsbPoint = ssbPoint;
          }

          //render last kumo polygon if we don't have an empty region
          if ((true == constructingPolygon) && (0 < ssaCurve.Count)) {
            //construct total set of SSA/SSB points for the polygon, SSB curve points must be reversed so we can construct a closed polygon of points
            ssbCurve.Reverse();
            ssaCurve.AddRange(ssbCurve);

            PointF[] points = ssaCurve.ToArray();
            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            path.AddLines(points);

            //render the polygon if it intersects with the dirty region
            using (Region pathRegion = new Region(path)) {
              if (pathRegion.IsVisible(context.DirtyRect)) {
                pathRegion.Intersect(context.DirtyRect);
                CompositingQuality previousQuality = context.graphics.CompositingQuality;
                context.graphics.CompositingQuality = CompositingQuality.GammaCorrected;
                context.graphics.FillRegion(renderBrush, pathRegion);
                context.graphics.CompositingQuality = previousQuality;
              }
            }
          }
        }
      }
    }
  }
}