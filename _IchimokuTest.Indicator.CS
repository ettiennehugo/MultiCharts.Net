using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using Ichimoku;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Test indicator to display the Ichimoku analysis state detection flags.
	/// </summary>
	public class _IchimokuTest : IndicatorObject {
		public _IchimokuTest(object _ctx):base(_ctx){}

        private IPlotObject plot1;
        private IPlotObject plot2;
        private IPlotObject plot3;
        private IPlotObject plot4;
        private IPlotObject plot5;
        private IPlotObject plot6;
        private IPlotObject plot7;
        private IPlotObject plot8;
        private IPlotObject plot9;
        private IPlotObject plot10;
        private IchimokuAnalysis IchimokuAnalysis;

        protected override void Create() {
            // create variable objects, function objects, plot objects etc.
            //TEST BATCH 1:
            //plot1 = AddPlot(new PlotAttributes("PriceAboveKumo", EPlotShapes.Line, Color.AliceBlue));
            //plot2 = AddPlot(new PlotAttributes("PriceBelowKumo", EPlotShapes.Line, Color.AntiqueWhite));
            //plot3 = AddPlot(new PlotAttributes("PriceAboveTS", EPlotShapes.Line, Color.Aqua));
            //plot4 = AddPlot(new PlotAttributes("PriceBelowTS", EPlotShapes.Line, Color.Aquamarine));
            //plot5 = AddPlot(new PlotAttributes("PriceAboveKS", EPlotShapes.Line, Color.Azure));
            //plot6 = AddPlot(new PlotAttributes("PriceBelowKS", EPlotShapes.Line, Color.Beige));
            //plot7 = AddPlot(new PlotAttributes("PriceCloseToTS", EPlotShapes.Line, Color.BlanchedAlmond));
            //plot8 = AddPlot(new PlotAttributes("PriceCloseToKS", EPlotShapes.Line, Color.Blue));
            //plot9 = AddPlot(new PlotAttributes("TSAboveKS", EPlotShapes.Line, Color.Brown));
            //plot10 = AddPlot(new PlotAttributes("TSBelowKS", EPlotShapes.Line, Color.BurlyWood));

            //TEST BATCH 2:
            //plot1 = AddPlot(new PlotAttributes("ChikouAbovePrice", EPlotShapes.Line, Color.AliceBlue));
            //plot2 = AddPlot(new PlotAttributes("ChikouBelowPrice", EPlotShapes.Line, Color.AntiqueWhite));
            //plot3 = AddPlot(new PlotAttributes("ChikouAboveKumo", EPlotShapes.Line, Color.Aqua));
            //plot4 = AddPlot(new PlotAttributes("ChikouBelowKumo", EPlotShapes.Line, Color.Aquamarine));
            //plot5 = AddPlot(new PlotAttributes("ChikouBullishOpenSpace", EPlotShapes.Line, Color.Azure));
            //plot6 = AddPlot(new PlotAttributes("ChikouBearishOpenSpace", EPlotShapes.Line, Color.Beige));
            //plot7 = AddPlot(new PlotAttributes("TSMomentum", EPlotShapes.Line, Color.BlanchedAlmond));
            //plot8 = AddPlot(new PlotAttributes("KSMomentum", EPlotShapes.Line, Color.Blue));
            //plot9 = AddPlot(new PlotAttributes("CSMomentum", EPlotShapes.Line, Color.Brown));
            //plot10 = AddPlot(new PlotAttributes("CSAMomentum", EPlotShapes.Line, Color.BurlyWood));

            //TEST BATCH 3:
            plot1 = AddPlot(new PlotAttributes("CSBMomentum", EPlotShapes.Line, Color.AliceBlue));
            plot2 = AddPlot(new PlotAttributes("FSAMomentum", EPlotShapes.Line, Color.AntiqueWhite));
            plot3 = AddPlot(new PlotAttributes("FSBMomentum", EPlotShapes.Line, Color.Aqua));
            plot4 = AddPlot(new PlotAttributes("CSKumoBullish", EPlotShapes.Line, Color.Aquamarine));
            plot5 = AddPlot(new PlotAttributes("CSKumoBearish", EPlotShapes.Line, Color.Azure));
            plot6 = AddPlot(new PlotAttributes("CKumoBullish", EPlotShapes.Line, Color.Beige));
            plot7 = AddPlot(new PlotAttributes("CKumoBearish", EPlotShapes.Line, Color.BlanchedAlmond));
            plot8 = AddPlot(new PlotAttributes("FKumoBullish", EPlotShapes.Line, Color.Blue));
            plot9 = AddPlot(new PlotAttributes("FKumoBearish", EPlotShapes.Line, Color.Brown));
            plot10 = AddPlot(new PlotAttributes("CSKumoThick", EPlotShapes.Line, Color.BurlyWood));

            //TEST BATCH 4:
            //plot1 = AddPlot(new PlotAttributes("CSKumoThin", EPlotShapes.Line, Color.AliceBlue));
            //plot2 = AddPlot(new PlotAttributes("CKumoThick", EPlotShapes.Line, Color.AntiqueWhite));
            //plot3 = AddPlot(new PlotAttributes("CKumoThin", EPlotShapes.Line, Color.Aqua));
            //plot4 = AddPlot(new PlotAttributes("FKumoThick", EPlotShapes.Line, Color.Aquamarine));
            //plot5 = AddPlot(new PlotAttributes("FKumoThin", EPlotShapes.Line, Color.Azure));
            //plot6 = AddPlot(new PlotAttributes("PriceInKumo", EPlotShapes.Line, Color.Beige));
            //plot7 = AddPlot(new PlotAttributes("PriceInKumoShadow", EPlotShapes.Line, Color.BlanchedAlmond));
            //plot8 = AddPlot(new PlotAttributes("TSInKumo", EPlotShapes.Line, Color.Blue));
            //plot9 = AddPlot(new PlotAttributes("KSInKumo", EPlotShapes.Line, Color.Brown));
            //plot10 = AddPlot(new PlotAttributes("CSInKumo", EPlotShapes.Line, Color.BurlyWood));
        }

        protected override void StartCalc() {
            IchimokuAnalysis = new IchimokuAnalysis(10000, Bars.Info.Resolution.Duration());

            Output.Clear();

        }

		protected override void CalcBar() {
            //add the bar data to the Ichimoku analysis
            bool analysisReady = IchimokuAnalysis.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0]);

			
			//TEST CODE
			//IchimokuBar currentBar = IchimokuAnalysis.CurrentBarOrNull;
			//IchimokuBar chikouBar = IchimokuAnalysis.ChikouBarOrNull;
			//Output.WriteLine("Indicator: TS({0}) KS({1}) CS({2}) CSSA({3}) CSSB({4}) FSSA({5}) FSSB({6})", 
			//                  IchimokuAnalysis.TenkanSen, IchimokuAnalysis.KijinSen, IchimokuAnalysis.ChikouSpan, IchimokuAnalysis.CurrentSenkouSpanA, IchimokuAnalysis.CurrentSenkouSpanB,
			//				  IchimokuAnalysis.SenkouSpanA, IchimokuAnalysis.SenkouSpanB);
			//if (null != currentBar) Output.WriteLine("Current  : Time({0}) Close({1}) KumoTS({2}) FKumoTS({3}) CSSA({4}) CSSB({5})", currentBar.timestamp, currentBar.close, currentBar.currentKumoTimestamp, currentBar.futureKumoTimestamp, currentBar.currentSenkouSpanA, currentBar.currentSenkouSpanB);
			//if (null != chikouBar) Output.WriteLine("Chikou   : Time({0}) Close({1}) ChikouSpan({2}) CSSA({3}) CSSB({4})", chikouBar.timestamp, chikouBar.close, chikouBar.chikouSpan, chikouBar.currentSenkouSpanA, chikouBar.currentSenkouSpanB);
			//Output.WriteLine("-------------------------------------");
			//TEST CODE
			
			
            //first set of test methods
            //TEST BATCH 1:
            //if (analysisReady && IchimokuAnalysis.PriceAboveKumo) plot1.Set(2); else plot1.Set(1);
            //if (analysisReady && IchimokuAnalysis.PriceBelowKumo) plot2.Set(4); else plot2.Set(3);
            //if (analysisReady && IchimokuAnalysis.PriceAboveTenkanSen) plot3.Set(6); else plot3.Set(5);
            //if (analysisReady && IchimokuAnalysis.PriceBelowTenkanSen) plot4.Set(8); else plot4.Set(7);
            //if (analysisReady && IchimokuAnalysis.PriceAboveKijunSen) plot5.Set(10); else plot5.Set(9);
            //if (analysisReady && IchimokuAnalysis.PriceBelowKijunSen) plot6.Set(12); else plot6.Set(11);
            //if (analysisReady && IchimokuAnalysis.PriceCloseToTenkanSen) plot7.Set(14); else plot7.Set(13);
            //if (analysisReady && IchimokuAnalysis.PriceCloseToKijunSen) plot8.Set(16); else plot8.Set(15);
            //if (analysisReady && IchimokuAnalysis.TenkanSenAboveKijunSen) plot9.Set(18); else plot9.Set(17);
            //if (analysisReady && IchimokuAnalysis.TenkanSenBelowKijunSen) plot10.Set(20); else plot10.Set(19);

            //TEST BATCH 2:
            //if (analysisReady && IchimokuAnalysis.ChikouAbovePrice) plot1.Set(IchimokuAnalysis.DefaultChikouSpanShift, 2); else plot1.Set(IchimokuAnalysis.DefaultChikouSpanShift, 1);
            //if (analysisReady && IchimokuAnalysis.ChikouBelowPrice) plot2.Set(IchimokuAnalysis.DefaultChikouSpanShift, 4); else plot2.Set(IchimokuAnalysis.DefaultChikouSpanShift, 3);
            //if (analysisReady && IchimokuAnalysis.ChikouAboveKumo) plot3.Set(IchimokuAnalysis.DefaultChikouSpanShift, 6); else plot3.Set(IchimokuAnalysis.DefaultChikouSpanShift, 5);
            //if (analysisReady && IchimokuAnalysis.ChikouBelowKumo) plot4.Set(IchimokuAnalysis.DefaultChikouSpanShift, 8); else plot4.Set(IchimokuAnalysis.DefaultChikouSpanShift, 7);
            //if (analysisReady && IchimokuAnalysis.ChikouBullishOpenSpace) plot5.Set(IchimokuAnalysis.DefaultChikouSpanShift, 10); else plot5.Set(IchimokuAnalysis.DefaultChikouSpanShift, 9);
            //if (analysisReady && IchimokuAnalysis.ChikouBearishOpenSpace) plot6.Set(IchimokuAnalysis.DefaultChikouSpanShift, 12); else plot6.Set(IchimokuAnalysis.DefaultChikouSpanShift, 11);
            //if (analysisReady && 0 < IchimokuAnalysis.TenkanSenMomentum) plot7.Set(14); else plot7.Set(13);
            //if (analysisReady && 0 < IchimokuAnalysis.KijunSenMomentum) plot8.Set(16); else plot8.Set(15);
            //if (analysisReady && 0 < IchimokuAnalysis.ChikouSpanMomentum) plot9.Set(IchimokuAnalysis.DefaultChikouSpanShift, 18); else plot9.Set(17);
            //if (analysisReady && 0 < IchimokuAnalysis.CurrentSenkouAMomentum) plot10.Set(20); else plot10.Set(19);

            //TEST BATCH 3:
            if (analysisReady && 0 < IchimokuAnalysis.CurrentSenkouBMomentum) plot1.Set(2); else plot1.Set(1);
            if (analysisReady && 0 < IchimokuAnalysis.FutureSenkouAMomentum) plot2.Set(-IchimokuAnalysis.DefaultKumoShift, 4); else plot2.Set(-IchimokuAnalysis.DefaultKumoShift, 3);
            if (analysisReady && 0 < IchimokuAnalysis.FutureSenkouBMomentum) plot3.Set(-IchimokuAnalysis.DefaultKumoShift, 6); else plot3.Set(-IchimokuAnalysis.DefaultKumoShift, 5);
            if (analysisReady && IchimokuAnalysis.ChikouSpanKumoBullish) plot4.Set(IchimokuAnalysis.DefaultChikouSpanShift, 8); else plot4.Set(IchimokuAnalysis.DefaultChikouSpanShift, 7);
            if (analysisReady && IchimokuAnalysis.ChikouSpanKumoBearish) plot5.Set(IchimokuAnalysis.DefaultChikouSpanShift, 10); else plot5.Set(IchimokuAnalysis.DefaultChikouSpanShift, 9);
            if (analysisReady && IchimokuAnalysis.CurrentKumoBullish) plot6.Set(12); else plot6.Set(11);
            if (analysisReady && IchimokuAnalysis.CurrentKumoBearish) plot7.Set(14); else plot7.Set(13);
            if (analysisReady && IchimokuAnalysis.FutureKumoBullish) plot8.Set(-IchimokuAnalysis.DefaultKumoShift, 16); else plot8.Set(-IchimokuAnalysis.DefaultKumoShift, 15);
            if (analysisReady && IchimokuAnalysis.FutureKumoBearish) plot9.Set(-IchimokuAnalysis.DefaultKumoShift, 18); else plot9.Set(-IchimokuAnalysis.DefaultKumoShift, 17);
            if (analysisReady && IchimokuAnalysis.ChikouSpanKumoThick) plot10.Set(IchimokuAnalysis.DefaultChikouSpanShift, 20); else plot10.Set(IchimokuAnalysis.DefaultChikouSpanShift, 19);

            //TEST BATCH 4:
            //if (analysisReady && IchimokuAnalysis.ChikouKumoThin) plot1.Set(IchimokuAnalysis.DefaultChikouSpanShift, 2); else plot1.Set(IchimokuAnalysis.DefaultChikouSpanShift, 1);
            //if (analysisReady && IchimokuAnalysis.CurrentKumoThick) plot2.Set(4); else plot2.Set(3);
            //if (analysisReady && IchimokuAnalysis.CurrentKumoThin) plot3.Set(6); else plot3.Set(5);
            //if (analysisReady && IchimokuAnalysis.FutureKumoThick) plot4.Set(-IchimokuAnalysis.DefaultKumoShift, 8); else plot4.Set(-IchimokuAnalysis.DefaultKumoShift, 7);
            //if (analysisReady && IchimokuAnalysis.FutureKumoThin) plot5.Set(-IchimokuAnalysis.DefaultKumoShift, 10); else plot5.Set(-IchimokuAnalysis.DefaultKumoShift, 9);
            //if (analysisReady && IchimokuAnalysis.PriceInKumo) plot6.Set(12); else plot6.Set(11);
            //if (analysisReady && IchimokuAnalysis.PriceInKumoShadow) plot7.Set(14); else plot7.Set(13);
            //if (analysisReady && IchimokuAnalysis.TenkanSenInKumo) plot8.Set(16); else plot8.Set(15);
            //if (analysisReady && IchimokuAnalysis.KijunSenInKumo) plot9.Set(18); else plot9.Set(17);
            //if (analysisReady && IchimokuAnalysis.ChikouInKumo) plot10.Set(IchimokuAnalysis.DefaultChikouSpanShift, 20); else plot10.Set(IchimokuAnalysis.DefaultChikouSpanShift, 19);
        }
    }
}