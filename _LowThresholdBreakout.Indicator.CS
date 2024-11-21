using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
  [SameAsSymbol(true)]
  public class _LowThresholdBreakout : IndicatorObject, IChartCustomDrawer
  {
    //types
    protected struct CriteriaMetArea
    {
      public ChartPoint high;
      public ChartPoint low;
      public ECurrentBarDirection barDirection;
    };

    //inputs
    [Input] public EScanBullishBearish ScanBullishBearish { get; set; }  //market direction to scan
    [Input] public int TradeIntensityShortPeriod { get; set; }  //short period for the trade intensity filter
    [Input] public int TradeIntensityLongPeriod { get; set; }  //short period for the trade intensity filter
    [Input] public double TradeIntensityPercent { get; set; } //percentage for trade intensity
    [Input] public double PreviousDayIncreasePercent { get; set; }  //percentage increase for pervious trading day to limit how much price can move before the low threshold detection
    [Input] public double MinimumPrice { get; set; }          //minimum price for stock
    [Input] public double MinimumAverageVolume { get; set; }  //minimum average volume for stock to trade at
    [Input] public int AverageVolumeLength { get; set; }      //length in number of bars used to compute average volume
    [Input] public Color BullishHighlightColor { get; set; }  //color used to highlight bullish momentum
    [Input] public Color BearishHighlightColor { get; set; }  //color used to highlight bearish momentum
    [Input] public bool Debug { get; set; }

    //attributes
    protected Function._LowThresholdBreakout m_lowThresholdBreakout;
    protected List<CriteriaMetArea> m_criteriaMetAreas;
    protected Color FONT_COLOR = Color.LightGray;
    protected float FRAME_LINE_WIDTH = 3.0f;
    protected float X_PADDING = 3.0f;
    protected float Y_PADDING = 3.0f;

		//interface methods
		public _LowThresholdBreakout(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);

      ScanBullishBearish = Function._LowThresholdBreakout.SCAN_BULLISH_BEARISH;
      TradeIntensityShortPeriod = Function._LowThresholdBreakout.TRADE_INTENSITY_SHORT_PERIOD;
      TradeIntensityLongPeriod = Function._LowThresholdBreakout.TRADE_INTENSITY_LONG_PERIOD;
      TradeIntensityPercent = Function._LowThresholdBreakout.TRADE_INTENSITY_PERCENT;
      PreviousDayIncreasePercent = Function._LowThresholdBreakout.PREVIOUS_DAY_INCREASE_PERCENT;
      MinimumPrice = Function._LowThresholdBreakout.MINIMUM_PRICE;
      MinimumAverageVolume = Function._LowThresholdBreakout.MINIMUM_AVERAGE_VOLUME;
      AverageVolumeLength = Function._LowThresholdBreakout.AVERAGE_VOLUME_LENGTH;
      m_lowThresholdBreakout = new Function._LowThresholdBreakout(this);
			Debug = Function._LowThresholdBreakout.DEBUG;

			//create additional attributes
			m_criteriaMetAreas = new List<CriteriaMetArea>();
    }

    protected override void StartCalc()
    {
      m_lowThresholdBreakout.TradeIntensityShortPeriod = TradeIntensityShortPeriod;
      m_lowThresholdBreakout.TradeIntensityLongPeriod = TradeIntensityLongPeriod;
      m_lowThresholdBreakout.PreviousDayIncreasePercent = PreviousDayIncreasePercent;
      m_lowThresholdBreakout.MinimumPrice = MinimumPrice;
      m_lowThresholdBreakout.MinimumAverageVolume = MinimumAverageVolume;
      m_lowThresholdBreakout.AverageVolumeLength = AverageVolumeLength;
      m_lowThresholdBreakout.Historical = true;
      m_lowThresholdBreakout.Debug = Debug;

      //clear set of criteria met areas
      m_criteriaMetAreas.Clear();
    }

    protected override void Destroy()
    {
      ChartCustomDraw.Unregister(this);
    }

    protected override void CalcBar()
    {
      m_lowThresholdBreakout.Call();  

      if (Environment.CalcReason == CalculationReason.Default && m_lowThresholdBreakout.CriteriaMet)
      {
        CriteriaMetArea criteriaMetArea = new CriteriaMetArea();
        criteriaMetArea.high.Time = Bars.Time[0];
        criteriaMetArea.high.Price = Bars.High[0];
        criteriaMetArea.low.Time = Bars.Time[0];
        criteriaMetArea.low.Price = Bars.Low[0];
        criteriaMetArea.barDirection = m_lowThresholdBreakout.CurrentBarDirection;
        m_criteriaMetAreas.Add(criteriaMetArea);
      }
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      if (phase == EDrawPhases.BeforeBGShapes)
      {

        using (Brush textBrush = new SolidBrush(FONT_COLOR))
        using (Brush bullishBrush = new SolidBrush(Color.FromArgb(64, BullishHighlightColor.R, BullishHighlightColor.G, BullishHighlightColor.B)))
        using (Pen bullishPen = new Pen(BullishHighlightColor, FRAME_LINE_WIDTH))
        using (Brush bearishBrush = new SolidBrush(Color.FromArgb(64, BearishHighlightColor.R, BearishHighlightColor.G, BearishHighlightColor.B)))
        using (Pen bearishPen = new Pen(BearishHighlightColor, FRAME_LINE_WIDTH))
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

            string momentumTypeStr = "LTBO";
            SizeF textSize = context.graphics.MeasureString(momentumTypeStr, context.StatusLineFont);
            RectangleF textRect = new RectangleF(rectangleF.X + (rectangleF.Width / 2) - (textSize.Width / 2), rectangleF.Y - (textSize.Height + Y_PADDING), textSize.Width, textSize.Height);

            if (context.DirtyRect.IntersectsWith(textRect) || context.DirtyRect.IntersectsWith(rectangleF))
            {
              Brush fillBrush = area.barDirection == ECurrentBarDirection.BullishLowThreshold ? bullishBrush : bearishBrush;
              Pen drawPen = area.barDirection == ECurrentBarDirection.BullishLowThreshold ? bullishPen : bearishPen;

              context.graphics.DrawString(momentumTypeStr, context.StatusLineFont, textBrush, textRect.Location);
              context.graphics.FillRectangle(fillBrush, rectangleF);
              context.graphics.DrawRectangle(drawPen, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
            }
          }
        }
      }
    }
  }
}