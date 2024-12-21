using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using PowerLanguage.Function;
using System.Drawing.Drawing2D;
using PriceAction;

namespace PowerLanguage.Indicator {

  public class BarText {
    public DateTime timestamp;
    public string text;
    public double low;
    public RectangleF location;

    public BarText() {
      location = new RectangleF();
    }
  }

  [SameAsSymbol]
  public class _PriceAction : IndicatorObject, IChartCustomDrawer {

    [Input] public int Size { get; set; }
    [Input] public int FractalLayers { get; set; }
    [Input] public int AtrLength { get; set; }
    [Input] public int VolumeLength { get; set; }
		[Input] public double FactorDoji { get; set; }
    [Input] public double FactorClose { get; set; }
    [Input] public double FactorStrong { get; set; }
    [Input] public double FactorEpsilon { get; set; }
    [Input] public Color TextColor { get; set; }
    [Input] public int TextFontSize { get; set; }
    [Input] public bool ShowEngulfing { get; set; }
    [Input] public bool ShowDoji { get; set; }
    [Input] public bool ShowPin { get; set; }
    [Input] public bool ShowInside { get; set; }
    [Input] public bool ShowOutside { get; set; }
    [Input] public bool ShowReversal { get; set; }
    [Input] public bool ShowStrong { get; set; }
    [Input] public bool ShowBreakout { get; set; }
    [Input] public bool ShowPivots { get; set; }
    [Input] public bool ShowDblTop { get; set; }
    [Input] public bool ShowHigherHigh { get; set; }
    [Input] public bool ShowLowerHigh { get; set; }
    [Input] public bool ShowDblBottom { get; set; }
    [Input] public bool ShowLowerLow { get; set; }
    [Input] public bool ShowHigherLow { get; set; }
    [Input] public bool ShowVolume { get; set; }

		public _PriceAction(object _ctx) : base(_ctx) {
      Size = PriceActionAnalysis.DefaultSize;
      FractalLayers = 3;
      AtrLength = PriceActionAnalysis.DefaultAtrLength;
			VolumeLength = PriceActionAnalysis.DefaultVolumeLength;
			FactorDoji = PriceActionAnalysis.DefaultFactorDoji;
      FactorClose = PriceActionAnalysis.DefaultFactorClose;
      FactorStrong = PriceActionAnalysis.DefaultFactorStrong;
      FactorEpsilon = PriceActionAnalysis.DefaultFactorEpsilon;
      TextColor = Color.LightGray;
      TextFontSize = 8;
      ShowEngulfing = false;
      ShowDoji = false;
      ShowPin = false;
      ShowInside = false;
      ShowOutside = false;
      ShowReversal = false;
      ShowStrong = false;
      ShowBreakout = false;
      ShowPivots = false;
      ShowDblTop = false;
      ShowHigherHigh = false;
      ShowLowerHigh = false;
      ShowDblBottom = false;
      ShowLowerLow = false;
      ShowHigherLow = false;
			ShowVolume = false;
		}

    private PriceActionAnalysis priceActionAnalysis;
    private SortedList<DateTime, BarText> barTexts;

    protected override void Create() {
      barTexts = new SortedList<DateTime, BarText>();
    }

    protected override void StartCalc() {
			ChartCustomDraw.Register(this);
			//we need to create the price action analysis here since it's dependent on the fractal layer count input variable
			priceActionAnalysis = new PriceActionAnalysis(FractalLayers);
      priceActionAnalysis.Size = Size;
      priceActionAnalysis.AtrLength = AtrLength;
			priceActionAnalysis.VolumeLength = VolumeLength;
			priceActionAnalysis.FactorDoji = FactorDoji;
      priceActionAnalysis.FactorClose = FactorClose;
      priceActionAnalysis.FactorStrong = FactorStrong;
      priceActionAnalysis.FactorEpsilon = FactorEpsilon;

      //clear the bar texts collection
      barTexts.Clear();
    }

    protected override void StopCalc() {
			ChartCustomDraw.Unregister(this);
			barTexts.Clear(); //clear the bar texts collection as the indicator is being removed/disabled
		}

    private DateTime testDate = new DateTime(2023, 8, 3);

		protected override void CalcBar() {
      bool priceActionChanged = false;

      //update the price action analysis if we have enough bars to analyse
      if (Bars.Status == EBarState.Close && Bars.CurrentBar > 2)
				priceActionChanged = priceActionAnalysis.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

			//update price action texts for bars analysed
			if (true == priceActionChanged) {
        //update the set of changed bars from the price action analysis                
        for (int index = 0; index < priceActionAnalysis.ChangedBars.Values.Count; index++) {
          PriceActionBar bar = priceActionAnalysis.ChangedBars.Values[index];

          if (false == barTexts.ContainsKey(bar.timestamp)) {
            BarText newText = new BarText();
            newText.timestamp = bar.timestamp;
            barTexts[bar.timestamp] = newText;
          }

          //update bar text
          BarText barText = barTexts[bar.timestamp];
          barText.low = bar.low;
          updateBarText(bar, barText);
        }

        //remove stale bar texts no longer needed
        while (barTexts.Count > Size)
          barTexts.RemoveAt(0);
      }
    }

    private void updateBarText(PriceActionBar bar, BarText text) {
      List<string> priceActionState = new List<string>();

      if (ShowEngulfing && bar.state.engulfing)
        priceActionState.Add("Engulfing");
      if (ShowDoji && bar.state.doji)
        priceActionState.Add("Doji");
      if (ShowPin && bar.state.highPin)
        priceActionState.Add("HighPin");
      if (ShowPin && bar.state.lowPin)
        priceActionState.Add("LowPin");
      if (ShowInside && bar.state.inside)
        priceActionState.Add("Inside");
      if (ShowOutside && bar.state.outside)
        priceActionState.Add("Outside");
      if (ShowReversal && bar.state.reversal)
        priceActionState.Add("Reversal");
      if (ShowStrong && bar.state.strong)
        priceActionState.Add("Strong");
      if (ShowBreakout && bar.state.breakout)
        priceActionState.Add("Breakout");

      if (ShowPivots) {
        //always display the pivot state of the highest fractal layer for which the pivot point is detected
        string pivotHigh = "";
        string pivotLow = "";

        for (int index = 0; index < priceActionAnalysis.LayerCount; index++) {
          //we output the fractal layer level and then the low/high count for the pivot point
          //NOTE: We only output the highest fractal layer on which a low/high pivot point is found so we set the pivot texts in this loop and then
          //      add them after the loop so that only the highest fractal pivot is displayed in the bar text.
          if (bar.state.pivotState[index].pivotHigh)
            pivotHigh = "FL" + (index + 1).ToString() + "H" + bar.state.pivotState[index].highCounter.ToString();
          if (bar.state.pivotState[index].pivotLow)
            pivotLow = "FL" + (index + 1).ToString() + "L" + bar.state.pivotState[index].lowCounter.ToString();
        }

        if (pivotHigh != "")
          priceActionState.Add(pivotHigh);
        if (pivotLow != "")
          priceActionState.Add(pivotLow);
      }

      if (ShowDblTop && bar.state.doubleTop)
        priceActionState.Add("DblTop");
      if (ShowHigherHigh && bar.state.higherHigh)
        priceActionState.Add("HH");
      if (ShowLowerHigh && bar.state.lowerHigh)
        priceActionState.Add("LH");
      if (ShowDblBottom && bar.state.doubleBottom)
        priceActionState.Add("DblBtm");
      if (ShowLowerLow && bar.state.lowerLow)
        priceActionState.Add("LL");
      if (ShowHigherLow && bar.state.higherLow)
        priceActionState.Add("HL");
      
      if (ShowVolume)
      {
        if (bar.state.volumePlus3SD)
          priceActionState.Add("VolumePlus3SD");
        else if (bar.state.volumePlus2SD)
          priceActionState.Add("VolumePlus2SD");
        else if (bar.state.volumePlus1SD)
          priceActionState.Add("VolumePlus1SD");
        else if (bar.state.volumeAboveAverage)
          priceActionState.Add("VolumeAboveAverage");
        else if (bar.state.volumeMinus3SD)
          priceActionState.Add("VolumeMinus3SD");
        else if (bar.state.volumeMinus2SD)
          priceActionState.Add("VolumeMinus2SD");
        else if (bar.state.volumeMinus1SD)
          priceActionState.Add("VolumeMinus1SD");
        else if (bar.state.volumeBelowAverage)
					priceActionState.Add("VolumeBelowAverage");
			}

      text.text = String.Join(", ", priceActionState);
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase) {
      //we only render text at the final drawing phase
      if (phase == EDrawPhases.Final) {
        using (Font textFont = new Font("Arial", TextFontSize, FontStyle.Regular))
        using (StringFormat textFormat = new StringFormat(StringFormatFlags.DirectionVertical | StringFormatFlags.NoWrap))
        using (Brush textBrush = new SolidBrush(TextColor)) {

          //determine the location for the text in the window client area and render the text if it
          //overlaps with the dirty rectangle area to be updated
          for (int index = 0; index < barTexts.Count; index++) {
            BarText barText = barTexts.Values[index];

            if (barText.text != "") {
              //update the location of the text in the window client area
              ChartPoint startChartPoint = new ChartPoint(barText.timestamp, barText.low);
              PointF startPoint = context.Environment.ChartPoint2Point(startChartPoint);
              SizeF textSize = context.graphics.MeasureString(barText.text, textFont);
              startPoint.X -= textSize.Height / 2;
              barText.location.X = startPoint.X;
              barText.location.Y = startPoint.Y;

              //NOTE: We render the text vertically so the width and height is swapped
              barText.location.Height = textSize.Width;
              barText.location.Width = textSize.Height;

              //render the text if it overlaps the dirty rectangle
              if (context.DirtyRect.IntersectsWith(barText.location))
                context.graphics.DrawString(barText.text, textFont, textBrush, barText.location, textFormat);
            }
          }
        }
      }
    }

  }
}