using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage;
using PowerLanguage.Indicator;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(true)]
    public class _RangeExpansionStatistics : IndicatorObject
    {
        public _RangeExpansionStatistics(object _ctx) : base(_ctx) { }

        [Input] public int LongLength { get; set; } = 50;        // NEW
        [Input] public int ShortLength { get; set; } = 5;        // NEW
        [Input] public double ExpansionFactor { get; set; } = 1.0; // default 1.0 as requested
        [Input] public bool OnlyBullish { get; set; } = true;
        [Input] public int ForwardLookback { get; set; } = 5;

        private List<double> m_highFromOpenPct = new List<double>();
        private List<double> m_openToClosePct  = new List<double>();
        private List<double> m_openToLowPct    = new List<double>();
        private List<double> m_dayRangePct     = new List<double>();
        private List<int>    m_reBarNumbers    = new List<int>();

        private VariableSeries<double> m_longAvgRange;
        private VariableSeries<double> m_shortAvgRange;

        protected override void Create()
        {
            m_longAvgRange  = new VariableSeries<double>(this);
            m_shortAvgRange = new VariableSeries<double>(this);
        }

        protected override void StartCalc()
        {
            m_highFromOpenPct.Clear();
            m_openToClosePct.Clear();
            m_openToLowPct.Clear();
            m_dayRangePct.Clear();
            m_reBarNumbers.Clear();
        }

        protected override void CalcBar()
        {
            double curRange = Bars.High[0] - Bars.Low[0];
            double open     = Bars.Open[0];

            int maxLen = Math.Max(LongLength, ShortLength);
            if (Bars.CurrentBar <= maxLen)
            {
                m_longAvgRange.Value  = curRange;
                m_shortAvgRange.Value = curRange;
                return;
            }

            //use previous values (no look-ahead)
            double longAvgPrev  = m_longAvgRange[1];
            double shortAvgPrev = m_shortAvgRange[1];

            bool isContraction = shortAvgPrev < longAvgPrev;
            bool isExpansion   = curRange > (shortAvgPrev * ExpansionFactor);
            bool isBullishDay  = Bars.Close[0] > open;

            if (isContraction && isExpansion && (!OnlyBullish || isBullishDay) && open > 0)
            {
                m_highFromOpenPct.Add((Bars.High[0] - open) / open * 100);
                m_openToClosePct.Add ((Bars.Close[0] - open) / open * 100);
                m_openToLowPct.Add   ((open - Bars.Low[0])   / open * 100);
                m_dayRangePct.Add    (curRange / open * 100);
                m_reBarNumbers.Add(Bars.CurrentBar);
            }

            //update averages AFTER detection
            m_longAvgRange.Value  = (longAvgPrev * (LongLength - 1) + curRange) / LongLength;
            m_shortAvgRange.Value = (shortAvgPrev * (ShortLength - 1) + curRange) / ShortLength;

            //output on last bar
            if (Bars.LastBarOnChart && m_reBarNumbers.Count >= 5)
            {
                double avgHighOpen = m_highFromOpenPct.Average();
                double avgMove     = m_openToClosePct.Average();
                double avgDip      = m_openToLowPct.Average();
                double avgRangePct = m_dayRangePct.Average();
                double pctOfDays   = (m_reBarNumbers.Count / (double)Bars.CurrentBar) * 100;

                string header =
                    "=== RANGE EXPANSION STATS (Contraction + Expansion) ===\n" +
                    $"Symbol: {Bars.Info.Name}\n" +
                    $"LongLength: {LongLength} | ShortLength: {ShortLength} | ExpansionFactor: {ExpansionFactor}x | OnlyBullish: {OnlyBullish}\n" +
                    $"RE Days Detected: {m_reBarNumbers.Count} ({pctOfDays:F1}% of bars)\n\n" +
                    $"Avg High from Open     : {avgHighOpen:F2}%\n" +
                    $"Avg Open → Close Move  : {avgMove:F2}%\n" +
                    $"Avg Open → Low (dip)   : {avgDip:F2}%\n" +
                    $"Avg Day Range (H-L)    : {avgRangePct:F2}%\n\n";

                Output.WriteLine(header);

                //forward move distribution of the price after a range expansion
                var bucketCounts = new Dictionary<int, int>();
                int totalForDist = 0;

                foreach (int reBarNum in m_reBarNumbers)
                {
                    int barsAgo = Bars.CurrentBar - reBarNum;
                    if (barsAgo < ForwardLookback) continue;

                    double entry = Bars.Close[barsAgo];
                    double maxFutureHigh = 0;

                    for (int i = 1; i <= ForwardLookback; i++)
                    {
                        int idx = barsAgo - i;
                        if (idx < 0) break;
                        maxFutureHigh = Math.Max(maxFutureHigh, Bars.High[idx]);
                    }

                    if (maxFutureHigh <= 0 || entry <= 0) continue;

                    double maxGainPct = (maxFutureHigh / entry - 1.0) * 100.0;
                    totalForDist++;

                    int bucket;
                    if (maxGainPct < 4)        bucket = -1;
                    else if (maxGainPct > 50)  bucket = 999;
                    else                       bucket = (int)(Math.Floor(maxGainPct / 2) * 2);

                    if (!bucketCounts.ContainsKey(bucket)) bucketCounts[bucket] = 0;
                    bucketCounts[bucket]++;
                }

                if (totalForDist == 0)
                {
                    Output.WriteLine("Not enough RE days with sufficient forward data.");
                    return;
                }

                string distHeader =
                    $"FORWARD MOVE DISTRIBUTION (max % gain in next {ForwardLookback} bars after RE)\n" +
                    $"RE days with enough future data: {totalForDist}\n" +
                    "Move Bucket     Count     Probability\n" +
                    "------------------------------------";

                Output.WriteLine(distHeader);

                if (bucketCounts.ContainsKey(-1))
                {
                    double p = bucketCounts[-1] / (double)totalForDist * 100;
                    Output.WriteLine(string.Format("< 4%          {0,5}      {1,6:F1}%", bucketCounts[-1], p));
                }

                for (int low = 4; low <= 48; low += 2)
                {
                    int c = bucketCounts.ContainsKey(low) ? bucketCounts[low] : 0;
                    double p = c / (double)totalForDist * 100;
                    Output.WriteLine(string.Format("{0,2}-{1,2}%         {2,5}      {3,6:F1}%", low, low + 2, c, p));
                }

                if (bucketCounts.ContainsKey(999))
                {
                    double p = bucketCounts[999] / (double)totalForDist * 100;
                    Output.WriteLine(string.Format(">50%          {0,5}      {1,6:F1}%", bucketCounts[999], p));
                }

                Output.WriteLine("\nNew detection: Short-term avg range < Long-term avg range AND today's range > Short avg × ExpansionFactor");
                Output.WriteLine("This captures contraction-then-expansion setups favored by Bonde, Qullamaggie & Minervini.");
            }
        }
    }
}