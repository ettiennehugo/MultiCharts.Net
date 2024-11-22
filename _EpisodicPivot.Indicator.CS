using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
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
		[Input] public int GrowthDays { get; set; }
		[Input] public double GrowthPercent { get; set; }
		[Input] public int VolumeLength { get; set; }
    [Input] public int AverageVolume { get; set; }
    [Input] public int AverageDollarVolume { get; set; }
    [Input] public double MinimumPercentageChange { get; set; }
    [Input] public double MaximumPercentageChange { get; set; }
    [Input] public Color HighlightColor { get; set; }

    //attributes
    private Function._EpisodicPivot m_episodicPivot;
    protected List<CriteriaMetArea> m_criteriaMetAreas;
    protected float FRAME_LINE_WIDTH = 3.0f;
    protected float X_PADDING = 3.0f;
    protected float Y_PADDING = 3.0f;

    public _EpisodicPivot(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);
      
      MinimumPrice = Function._EpisodicPivot.DEFAULT_MINIMUM_PRICE;
      GrowthDays = Function._EpisodicPivot.DEFAULT_GROWTH_DAYS;
      GrowthPercent = Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
      VolumeLength = Function._EpisodicPivot.DEFAULT_VOLUME_LENGTH;
      AverageVolume = Function._EpisodicPivot.DEFAULT_AVERAGE_VOLUME;
      AverageDollarVolume = Function._EpisodicPivot.DEFAULT_AVERAGE_DOLLAR_VOLUME;
      MinimumPercentageChange = Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
      MaximumPercentageChange = Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
      HighlightColor = Color.LightGray;

      m_criteriaMetAreas = new List<CriteriaMetArea>();
      m_episodicPivot = new Function._EpisodicPivot(this);
    }

    protected override void Destroy()
    {
      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc()
    {
      //set inputs
      m_episodicPivot.Instrument = Bars;
			m_episodicPivot.MinimumPrice = MinimumPrice;
      m_episodicPivot.GrowthDays = GrowthDays;
      m_episodicPivot.GrowthPercent = GrowthPercent;
      m_episodicPivot.VolumeLength = VolumeLength;
      m_episodicPivot.AverageVolume = AverageVolume;
      m_episodicPivot.AverageDollarVolume = AverageDollarVolume;
      m_episodicPivot.MinimumPercentageChange = MinimumPercentageChange;
      m_episodicPivot.MaximumPercentageChange = MaximumPercentageChange;

      //clear set of criteria met areas
      m_criteriaMetAreas.Clear();
    }

    protected override void CalcBar()
    {
      //m_episodicPivot.Call(); - TBD is this required???

      if (m_episodicPivot[0])
      {
        CriteriaMetArea criteriaMetArea = new CriteriaMetArea();
        criteriaMetArea.high.Time = Bars.Time[0];
        criteriaMetArea.high.Price = Bars.High[0];
        criteriaMetArea.low.Time = Bars.Time[0];
        criteriaMetArea.low.Price = Bars.Low[0];
        m_criteriaMetAreas.Add(criteriaMetArea);
      }
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      if (phase == EDrawPhases.BeforeBGShapes)
      {
        using (Brush brush = new SolidBrush(Color.FromArgb(64, HighlightColor.R, HighlightColor.G, HighlightColor.B)))
        using (Pen pen = new Pen(HighlightColor, FRAME_LINE_WIDTH))
        {
          foreach (CriteriaMetArea area in m_criteriaMetAreas)
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