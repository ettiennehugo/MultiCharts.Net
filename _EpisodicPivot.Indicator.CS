using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  public class _EpisodicPivot : IndicatorObject, IChartCustomDrawer
  {
    //types
    protected struct CriteriaMetArea
    {
      public ChartPoint high;
      public ChartPoint low;
    };

    //inputs
    [Input] public double MinimumPrice { get; set; }
    [Input] public int VolumeLength { get; set; }
    [Input] public int AverageVolume { get; set; }
    [Input] public int AverageDollarVolume { get; set; }
    [Input] public double PercentageChange { get; set; }
    [Input] public Color HighlightColor { get; set; }

    //attributes
    private Function._ScannerEpisodicPivot scannerEpisodicPivot;
    protected List<CriteriaMetArea> criteriaMetAreas;
    protected float FRAME_LINE_WIDTH = 3.0f;
    protected float X_PADDING = 3.0f;
    protected float Y_PADDING = 3.0f;

    public _EpisodicPivot(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);

      //default inputs
      MinimumPrice = Function._ScannerEpisodicPivot.MINIMUM_PRICE;
      VolumeLength = Function._ScannerEpisodicPivot.VOLUME_LENGTH;
      AverageVolume = Function._ScannerEpisodicPivot.AVERAGE_VOLUME;
      AverageDollarVolume = Function._ScannerEpisodicPivot.AVERAGE_DOLLAR_VOLUME;
      PercentageChange = Function._ScannerEpisodicPivot.PERCENTAGE_CHANGE;
      HighlightColor = Color.LightGray;

      //create additional attributes
      criteriaMetAreas = new List<CriteriaMetArea>();

      //create scanner function
      scannerEpisodicPivot = new Function._ScannerEpisodicPivot(this);
    }

    protected override void Destroy()
    {
      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc()
    {
      //set inputs
      scannerEpisodicPivot.MinimumPrice = MinimumPrice;
      scannerEpisodicPivot.VolumeLength = VolumeLength;
      scannerEpisodicPivot.AverageVolume = AverageVolume;
      scannerEpisodicPivot.AverageDollarVolume = AverageDollarVolume;
      scannerEpisodicPivot.PercentageChange = PercentageChange;
      scannerEpisodicPivot.Historical = true;

      //clear set of criteria met areas
      criteriaMetAreas.Clear();
    }

    protected override void CalcBar()
    {
      scannerEpisodicPivot.Call();

      if (Environment.CalcReason == CalculationReason.Default && scannerEpisodicPivot.CriteriaMet)
      {
        CriteriaMetArea criteriaMetArea = new CriteriaMetArea();
        criteriaMetArea.high.Time = Bars.Time[0];
        criteriaMetArea.high.Price = Bars.High[0];
        criteriaMetArea.low.Time = Bars.Time[0];
        criteriaMetArea.low.Price = Bars.Low[0];
        criteriaMetAreas.Add(criteriaMetArea);
      }
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      if (phase == EDrawPhases.BeforeBGShapes)
      {
        using (Brush brush = new SolidBrush(Color.FromArgb(64, HighlightColor.R, HighlightColor.G, HighlightColor.B)))
        using (Pen pen = new Pen(HighlightColor, FRAME_LINE_WIDTH))
        {
          foreach (CriteriaMetArea area in criteriaMetAreas)
          {
            //compute area for rectangle around price movement
            PointF startPoint = context.Environment.ChartPoint2Point(area.high);
            startPoint.X -= (float)context.Environment.ChartBarCloseDistance / 2.0f;
            startPoint.X -= X_PADDING + context.Environment.ChartBarCloseDistance / 2.0f;
            startPoint.Y -= Y_PADDING;
            PointF endPoint = context.Environment.ChartPoint2Point(area.low);
            endPoint.X += context.Environment.ChartBarCloseDistance / 2.0f;
            endPoint.X += (X_PADDING * 2) + context.Environment.ChartBarCloseDistance;
            endPoint.Y += Y_PADDING;
            RectangleF rectangleF = new RectangleF(startPoint.X, startPoint.Y, Math.Abs(startPoint.X - endPoint.X), Math.Abs(startPoint.Y - endPoint.Y));

            if (context.DirtyRect.IntersectsWith(rectangleF))
            {
              context.graphics.FillRectangle(brush, rectangleF);
              context.graphics.DrawRectangle(pen, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
            }
          }
        }
      }
    }
  }
}