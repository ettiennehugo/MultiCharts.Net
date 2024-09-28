using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using StructualTargetPatterns;
using FileHelpers;

//TODO:
// * This indicator is not very useful since it will render corridors found irrespective of whether one of those corridors would result in an entry or a trade,
//   so you get all these corridors rendered because you're never in a trade that would result in focus on a specific corridor.
//   * Maybe add some code so that the corridors are not rendered if a fill would occur on a corridor. 
// * Add arrows to the direction of the corridor on the first fractal bar that starts the corridor as used in the book.

namespace PowerLanguage.Indicator {
  [SameAsSymbol]
  public class _StructuralTargetPatterns : IndicatorObject, IChartCustomDrawer {
    //Constants:
    //NOTE: These defaults are based on the M5 EUR/USD chart.
    public const int DEFAULT_ENTRY_BUFFER_PIPS = 2;
    public const int DEFAULT_EXIT_BUFFER_PIPS = 0;
    public const int DEFAULT_MIN_CORRIDOR_PIPS = 2;              
    public const int DEFAULT_MAX_CORRIDOR_PIPS = 13;
    public const string DEFAULT_EVENT_FILE = "C:\\Share\\Trading\\MultiCharts\\Structural Target Patterns\\EURUSD April 2018.csv";
    public const EEventVolatility DEFAULT_EVENT_VOLATILITY = EEventVolatility.Medium;

    //Types:
    /// <summary>
    /// Storage structure for historical corridors that formed.
    /// </summary>
    protected struct HistoricalCorridor {
      public double high;
      public double low;
      public double entryPrice;
      public double stopLossPrice;
      public double target210;
      public double exitTargetPrice;
      public DateTime highBarTime;
      public DateTime lowBarTime;
      public CorridorDirection direction;
    }

    public enum EEventVolatility
    {
      None,
      Low,
      Medium,
      High
    }

    /// <summary>
    /// Expected record structure for the CSV file.
    /// </summary>
    [DelimitedRecord(","), IgnoreFirst(), IgnoreEmptyLines()]
    protected class FileEconomicEvent
    {
      public string startDateTime;
      public string currency;
      public string description;
      public string country;
      public string volatility;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double actual;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double previous;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double consensus;
    }

    /// <summary>
    /// Run-time economic event data used for trading.
    /// </summary>
    protected class EconomicEvent
    {
      public DateTime startDateTime;
      public string currency;
      public string description;
      public string country;
      public EEventVolatility volatility;
      public double actual;
      public double previous;
      public double consensus;
    }

    //Properties:
    [Input]public int EntryBufferPips { get; set; }
    [Input]public int ExitBufferPips { get; set; }
    [Input] public int MinCorridorPips { get; set; }             //Minimum size to accept as a corridor
    [Input] public int MaxCorridorPips { get; set; }             //Maximum size to accept as a corridor
    [Input] public string EconomicEventFile { get; set; }        //File containing upcoming economic events.
    [Input] public EEventVolatility EconomicEventVolatility { get; set; } //Least event volatility before which trading should be stopped.
    [Input]public Color EntryColor { get; set; }
    [Input]public Color ExitColor { get; set; }
    [Input]public Color Target210Color { get; set; }
    [Input]public Color ExitTargetColor { get; set; }
    [Input]public Color TradeColor { get; set; }
    [Input] public Color EventTextColor { get; set; }
    [Input] public int EventTextFontSize { get; set; }

    //Attributes:
    protected Corridor _corridor;
    protected List<HistoricalCorridor> _historicalCorridors;
    protected int _corridorId;
    private List<EconomicEvent> _economicEvents;

    public _StructuralTargetPatterns(object _ctx) : base(_ctx) { }

    protected override void Create() {
      ChartCustomDraw.Register(this);
      EntryBufferPips = DEFAULT_ENTRY_BUFFER_PIPS;
      ExitBufferPips = DEFAULT_EXIT_BUFFER_PIPS;
      MinCorridorPips = DEFAULT_MIN_CORRIDOR_PIPS;
      MaxCorridorPips = DEFAULT_MAX_CORRIDOR_PIPS;
      EconomicEventFile = DEFAULT_EVENT_FILE;
      EconomicEventVolatility = EEventVolatility.Medium;
      EntryColor = Color.LightGreen;
      ExitColor = Color.LightSalmon;
      Target210Color = Color.LightGray;
      ExitTargetColor = Color.Green;
      TradeColor = Color.LightGray;
      EventTextColor = Color.AntiqueWhite;
      EventTextFontSize = 8;
      _historicalCorridors = new List<HistoricalCorridor>();
      _economicEvents = new List<EconomicEvent>();
    }

    protected override void Destroy() {
      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc() {
      //clear the list of historical corridors found
      _historicalCorridors.Clear();
      _corridorId = 1;
      _corridor = null;

      //load the economic events from the event file
      _economicEvents.Clear();

      FileHelperEngine<FileEconomicEvent> parser = new FileHelperEngine<FileEconomicEvent>();
      FileEconomicEvent[] lines = parser.ReadFile(EconomicEventFile);

      foreach (FileEconomicEvent line in lines)
      {
        EconomicEvent economicEvent = new EconomicEvent();
        economicEvent.volatility = (EEventVolatility)Enum.Parse(typeof(EEventVolatility), line.volatility);

        //filter events with less expected volatility than volatility used to disable trading
        if (economicEvent.volatility < EconomicEventVolatility) continue;

        economicEvent.startDateTime = DateTime.Parse(line.startDateTime);
        economicEvent.currency = line.currency;
        economicEvent.description = line.description;
        economicEvent.country = line.country;
        economicEvent.actual = line.actual;
        economicEvent.previous = line.previous;
        economicEvent.consensus = line.consensus;
        _economicEvents.Add(economicEvent);
      }
    }

    protected override void CalcBar() {
      //create a new corridor if we previously found a corridor
      if (_corridor == null) {
        _corridor = new Corridor(_corridorId.ToString(), this);
        _corridorId++;
        _corridor.EntryBufferPips = EntryBufferPips;
        _corridor.ExitBufferPips = ExitBufferPips;
        _corridor.MinCorridorPips = MinCorridorPips;
        _corridor.MaxCorridorPips = MaxCorridorPips;
        _corridor.LastScanBar = Bars.CurrentBar > 1 ? Bars.Time[1] : Bars.Time[0];
      }


      //DEBUG
      //if (Bars.Time[0] == DateTime.Parse("04/01/2018 18:25") || 
      //    Bars.Time[0] == DateTime.Parse("04/01/2018 20:10")) {
      //  Output.WriteLine("DEBUG");
      //}


      //allow the corridor to scan for pivots
      _corridor.CalcBar();

      //add the corridor to the list of historical corridors if found and
      //start analyzing next corridor
      if (_corridor.Found) {
        //save historical entry for corridor that was found
        HistoricalCorridor h = new HistoricalCorridor();
        h.direction = _corridor.Direction;
        h.low = _corridor.LowPivotPrice;
        h.high = _corridor.HighPivotPrice;
        h.entryPrice = _corridor.EntryPrice;
        h.stopLossPrice = _corridor.StopLossPrice;
        h.target210 = _corridor.PriceLevel210;
        h.exitTargetPrice = _corridor.ExitTargetPrice;
        
        if (h.direction == CorridorDirection.Bullish) {
          //bullish corridor has first pivot as the low and second pivot as the high
          h.highBarTime = _corridor.FirstPivotBarTime;
          h.lowBarTime = _corridor.SecondPivotBarTime;
        } else {
          //bearish corridor has first pivot as the high and second pivot as the low
          h.lowBarTime = _corridor.FirstPivotBarTime;
          h.highBarTime = _corridor.SecondPivotBarTime;
        }

        _historicalCorridors.Add(h);

        //clear corridor instance so that it's initialized for next bar
        _corridor = null;
      }
    }

    /// <summary>
    /// Render the set of historical corridors found that within the dirty rectangle to be rendered.
    /// </summary>
    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase) {  
      if (phase == EDrawPhases.Final) {
        using (Pen entryPen = new Pen(EntryColor, 3))
        using (Pen exitPen = new Pen(ExitColor, 3))
        using (Pen entryPricePen = new Pen(EntryColor, 1))
        using (Pen stopLossPricePen = new Pen(ExitColor, 1))
        using (Pen target210Pen = new Pen(Target210Color, 1))
        using (Pen exitTargetPen = new Pen(ExitTargetColor, 3))
        using (Pen tradePen = new Pen(TradeColor, 1))
        using (Font textFont = new Font("Arial", EventTextFontSize, FontStyle.Regular))
        using (StringFormat textFormat = new StringFormat(StringFormatFlags.DirectionVertical | StringFormatFlags.NoWrap))
        using (Brush textBrush = new SolidBrush(EventTextColor)) {
            //draw entry/exit prices with dashed styles
            entryPricePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
          stopLossPricePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

          //render the necessary historical corridors
          TimeSpan ts = Bars.Info.Resolution.Duration();

          foreach (HistoricalCorridor h in _historicalCorridors) {
            //determine entry and exit points for historical bar lines
            ChartPoint entryStartCP;
            ChartPoint entryEndCP;
            ChartPoint entryPriceStartCP;
            ChartPoint entryPriceEndCP;
            ChartPoint exitStartCP;
            ChartPoint exitEndCP;
            ChartPoint stopLossPriceStartCP;
            ChartPoint stopLossPriceEndCP;
            ChartPoint target210PriceStartCP;
            ChartPoint target210PriceEndCP;
            ChartPoint exitTargetPriceStartCP;
            ChartPoint exitTargetPriceEndCP;

            DateTime startTime;
            DateTime endTime;

            if (h.direction == CorridorDirection.Bullish) {
              //high pivot bar formed first and then second pivot bar formed at the low
              //draw line from high bar date to low bar date
              startTime = h.highBarTime - ts;
              endTime = h.lowBarTime + ts;

              entryStartCP = new ChartPoint(startTime, h.high);
              entryEndCP = new ChartPoint(endTime, h.high);
              entryPriceStartCP = new ChartPoint(startTime, h.entryPrice);
              entryPriceEndCP = new ChartPoint(endTime, h.entryPrice);

              exitStartCP = new ChartPoint(startTime, h.low);
              exitEndCP = new ChartPoint(endTime, h.low);
              stopLossPriceStartCP = new ChartPoint(startTime, h.stopLossPrice);
              stopLossPriceEndCP = new ChartPoint(endTime, h.stopLossPrice);

            } else {
              //low pivot bar formed first and then the second pivot bar formed at the high
              //draw line from low bar date to high bar date
              startTime = h.lowBarTime - ts;
              endTime = h.highBarTime + ts;

              entryStartCP = new ChartPoint(startTime, h.low);
              entryEndCP = new ChartPoint(endTime, h.low);
              entryPriceStartCP = new ChartPoint(startTime, h.entryPrice);
              entryPriceEndCP = new ChartPoint(endTime, h.entryPrice);

              exitStartCP = new ChartPoint(startTime, h.high);
              exitEndCP = new ChartPoint(endTime, h.high);
              stopLossPriceStartCP = new ChartPoint(startTime, h.stopLossPrice);
              stopLossPriceEndCP = new ChartPoint(endTime, h.stopLossPrice);
            }

            //set target 210% and exit target prices
            target210PriceStartCP = new ChartPoint(startTime, h.target210);
            target210PriceEndCP = new ChartPoint(endTime, h.target210);
            exitTargetPriceStartCP = new ChartPoint(startTime, h.exitTargetPrice);
            exitTargetPriceEndCP = new ChartPoint(endTime, h.exitTargetPrice);

            //get points on the screen
            PointF entryStartP = context.Environment.ChartPoint2Point(entryStartCP);
            PointF entryEndP = context.Environment.ChartPoint2Point(entryEndCP);
            PointF exitStartP = context.Environment.ChartPoint2Point(exitStartCP);
            PointF exitEndP = context.Environment.ChartPoint2Point(exitEndCP);

            PointF entryPriceStartP = context.Environment.ChartPoint2Point(entryPriceStartCP);
            PointF entryPriceEndP = context.Environment.ChartPoint2Point(entryPriceEndCP);
            PointF stopLossPriceStartP = context.Environment.ChartPoint2Point(stopLossPriceStartCP);
            PointF stopLossPriceEndP = context.Environment.ChartPoint2Point(stopLossPriceEndCP);

            PointF target210PriceStartP = context.Environment.ChartPoint2Point(target210PriceStartCP);
            PointF target210PriceEndP = context.Environment.ChartPoint2Point(target210PriceEndCP);
            PointF exitTargetPriceStartP = context.Environment.ChartPoint2Point(exitTargetPriceStartCP);
            PointF exitTargetPriceEndP = context.Environment.ChartPoint2Point(exitTargetPriceEndCP);

            //render corridor lines if required
            if (context.DirtyRect.Contains(entryStartP) || context.DirtyRect.Contains(entryEndP))
              context.graphics.DrawLine(entryPen, entryStartP, entryEndP);

            if (context.DirtyRect.Contains(exitStartP) || context.DirtyRect.Contains(exitEndP))
              context.graphics.DrawLine(exitPen, exitStartP, exitEndP);

            //render target 210% and exit target lines if they are in the dirty rectangle
            if (context.DirtyRect.Contains(target210PriceStartP) || context.DirtyRect.Contains(target210PriceEndP))
              context.graphics.DrawLine(target210Pen, target210PriceStartP, target210PriceEndP);

            if (context.DirtyRect.Contains(exitTargetPriceStartP) || context.DirtyRect.Contains(exitTargetPriceEndP))
              context.graphics.DrawLine(exitTargetPen, exitTargetPriceStartP, exitTargetPriceEndP);

            //render entry/stop-loss price lines if they are not at the same level as the corridor price lines
            if ((entryStartP.Y != entryPriceStartP.Y) && (context.DirtyRect.Contains(entryPriceStartP) || context.DirtyRect.Contains(entryPriceEndP)))
              context.graphics.DrawLine(entryPricePen, entryPriceStartP, entryPriceEndP);

            if ((exitEndP.Y != stopLossPriceStartP.Y) && (context.DirtyRect.Contains(stopLossPriceStartP) || context.DirtyRect.Contains(stopLossPriceEndP)))
              context.graphics.DrawLine(stopLossPricePen, stopLossPriceStartP, stopLossPriceEndP);

            //render trade connector line to connect all levels together
            if (context.DirtyRect.Contains(stopLossPriceStartP) || context.DirtyRect.Contains(exitTargetPriceStartP))
              context.graphics.DrawLine(tradePen, stopLossPriceStartP, exitTargetPriceStartP);
          }

          //render economic event text
          string eventText =  "";
          DateTime eventDateTime = _economicEvents.Count > 0 ? _economicEvents[0].startDateTime : DateTime.MinValue;
          foreach (EconomicEvent e in _economicEvents) {
            //render events if this new event no longer fall on the time of the previous event
            if (e.startDateTime != eventDateTime && eventText.Length > 0) {
              //find the bar where the event text should be rendered
              foreach (Bar b in context.Environment.Bars.All)
                if (b.Time == eventDateTime) {
                  //update the location of the text in the window client area
                  RectangleF textRect = new RectangleF();
                  ChartPoint startChartPoint = new ChartPoint(eventDateTime, b.Low);
                  PointF startPoint = context.Environment.ChartPoint2Point(startChartPoint);
                  SizeF textSize = context.graphics.MeasureString(eventText, textFont);
                  startPoint.X -= textSize.Height / 2;
                  textRect.Location = startPoint;

                  //NOTE: We render the text vertically so the width and height is swapped
                  textRect.Height = textSize.Width;
                  textRect.Width = textSize.Height;

                  //render the text if it overlaps the dirty rectangle
                  if (context.DirtyRect.IntersectsWith(textRect))
                    context.graphics.DrawString(eventText, textFont, textBrush, textRect.Location, textFormat);

                  break;  //terminate search loop for bar
                }  

              //clear the rendered event text and start accumulating new values for text
              eventText = "";
              eventDateTime = e.startDateTime;
            }

            //accumulate events that fall on the same date/time
            if (e.startDateTime == eventDateTime) {
              if (eventText == "") 
                eventText = e.description;
              else
                eventText += ", " + e.description;
            }
          }

        }
      }
    }
  }
}