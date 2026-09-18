/*
  VP_TwoTarget_Levels — MultiCharts .NET indicator
  ================================================
  Plots primary (Data1) and secondary (Data2) volume-profile magnets
  so you can see T1 / T2 before the signal fires.

  Same chart setup as VP_TwoTarget_Position:
    Data1 = 1/5/15/30 min, Volume Profile enabled
    Data2 = same symbol, longer TF, Volume Profile enabled
*/

using System;
using System.Drawing;
using PowerLanguage.VolumeProfile;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(true)]
    public class _VPTwoTargetLevels : IndicatorObject
    {
        [Input] public bool PlotPrimary { get; set; }
        [Input] public bool PlotSecondary { get; set; }

        private IPlotObject m_poc1, m_vah1, m_val1;
        private IPlotObject m_poc2, m_vah2, m_val2;

        public _VPTwoTargetLevels(object ctx) : base(ctx)
        {
            PlotPrimary = true;
            PlotSecondary = true;
        }

        protected override void Create()
        {
            m_poc1 = AddPlot(new PlotAttributes("Prim POC", EPlotShapes.Line, Color.Orange, Color.Black, 2, 0, true));
            m_vah1 = AddPlot(new PlotAttributes("Prim VAH", EPlotShapes.Line, Color.LimeGreen, Color.Black, 1, 0, true));
            m_val1 = AddPlot(new PlotAttributes("Prim VAL", EPlotShapes.Line, Color.IndianRed, Color.Black, 1, 0, true));
            m_poc2 = AddPlot(new PlotAttributes("Sec POC", EPlotShapes.Line, Color.Gold, Color.Black, 2, 0, true));
            m_vah2 = AddPlot(new PlotAttributes("Sec VAH", EPlotShapes.Line, Color.DarkGreen, Color.Black, 1, 0, true));
            m_val2 = AddPlot(new PlotAttributes("Sec VAL", EPlotShapes.Line, Color.Maroon, Color.Black, 1, 0, true));
        }

        protected override void CalcBar()
        {
            if (PlotPrimary)
                PlotStream(1, m_poc1, m_vah1, m_val1);
            if (PlotSecondary)
                PlotStream(2, m_poc2, m_vah2, m_val2);
        }

        private void PlotStream(int dataStream, IPlotObject poc, IPlotObject vah, IPlotObject val)
        {
            double p = 0, h = 0, l = 0;
            if (!Read(dataStream, out p, out h, out l)) return;
            if (p > 0) poc.Set(p);
            if (h > 0) vah.Set(h);
            if (l > 0) val.Set(l);
        }

        private bool Read(int dataStream, out double poc, out double vah, out double val)
        {
            poc = vah = val = 0;
            try
            {
                IProfilesCollection pack;
                int absBar;
                if (dataStream <= 1)
                {
                    pack = this.VolumeProfile;
                    absBar = Bars.FullSymbolData.Current - 1;
                }
                else
                {
                    if (!IsExist(dataStream)) return false;
                    pack = VolumeProfileByDataStream(dataStream);
                    absBar = BarsOfData(dataStream).FullSymbolData.Current - 1;
                }
                if (pack == null || absBar < 0) return false;
                IProfile vp = pack.ItemForBar(absBar);
                if (vp == null || vp.Empty) return false;

                Price a = vp.POCForBar(absBar);
                Price b = vp.HighVAForBar(absBar);
                Price c = vp.LowVAForBar(absBar);
                if (a != null) poc = a.Dbl;
                if (b != null) vah = b.Dbl;
                if (c != null) val = c.Dbl;
                return poc > 0 || vah > 0 || val > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
