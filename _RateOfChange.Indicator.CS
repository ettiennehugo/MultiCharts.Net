using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _RateOfChange : IndicatorObject {
		public _RateOfChange(object _ctx):base(_ctx){}

    [Input] public int Length { get; set; }           //length to lookback for rate of change on price
    [Input] public int CumulativeLength { get; set; } //cumulative length lookback to draw cumulative changes over Length input, 0 = disable 

    private IPlotObject zeroPlot;
    private IPlotObject plot1;
    private IPlotObject plot2; 
    private IPlotObject plot3;
    private IPlotObject plot4;
    private IPlotObject plot5;

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

    protected override void Create() {
      Length = 1;
      CumulativeLength = 0;

      zeroPlot = AddPlot(new PlotAttributes("Zero", EPlotShapes.Line, Color.LightGray));
      plot1 = AddPlot(new PlotAttributes("1", EPlotShapes.Line, Color.Red));
      plot2 = AddPlot(new PlotAttributes("2", EPlotShapes.Line, Color.Yellow)); 
      plot3 = AddPlot(new PlotAttributes("3", EPlotShapes.Line, Color.Orange));
      plot4 = AddPlot(new PlotAttributes("4", EPlotShapes.Line, Color.Blue));
      plot5 = AddPlot(new PlotAttributes("5", EPlotShapes.Line, Color.Violet));

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

    protected override void StartCalc() {
      bars1 = Bars;
      try { bars2 = BarsOfData(2); } catch (Exception) { bars2 = null; }
      try { bars3 = BarsOfData(3); } catch (Exception) { bars3 = null; }
      try { bars4 = BarsOfData(4); } catch (Exception) { bars4 = null; }
      try { bars5 = BarsOfData(5); } catch (Exception) { bars5 = null; }
    }

    protected override void CalcBar(){
      calcRofCrof(bars1, rof1, crof1);
      calcRofCrof(bars2, rof2, crof2);
      calcRofCrof(bars3, rof3, crof3);
      calcRofCrof(bars4, rof4, crof4);
      calcRofCrof(bars5, rof5, crof5);

      zeroPlot.Set(0);
      plotRofCrof(bars1, plot1, rof1, crof1);
      plotRofCrof(bars2, plot2, rof2, crof2);
      plotRofCrof(bars3, plot3, rof3, crof3);
      plotRofCrof(bars4, plot4, rof4, crof4);
      plotRofCrof(bars5, plot5, rof5, crof5);
    }

    private void calcRofCrof(IInstrument bars, VariableSeries<double> rof, VariableSeries<double> crof)
    {
      //default values if no bars or current bar less than length
      rof.Value = 0;
      crof.Value = 0;

      //compute values if valid bars
      if (bars != null && bars.CurrentBar > Length)
      {
        rof.Value = bars.Close.RateOfChange(Length);
        crof.Value = 0;
        for (int i = 0; i < CumulativeLength; i++) crof.Value += rof[i];
        crof.Value = crof.Value == 0 ? rof.Value : crof.Value;
      }

    }

    private void plotRofCrof(IInstrument bars, IPlotObject plot, VariableSeries<double> rof, VariableSeries<double> crof) {
      if (bars != null)
        if (CumulativeLength != 0) plot.Set(crof.Value); else plot.Set(rof.Value);
      else
        plot.Set(0);
    }

  }
}