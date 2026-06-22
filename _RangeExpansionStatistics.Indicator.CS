using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage;
using PowerLanguage.Indicator;

namespace PowerLanguage.Indicator
{
    public enum ETradeDirection
    {
        BullishAndBearish,
        Bullish,
        Bearish
    }

    [SameAsSymbol(true)]
    public class _RangeExpansionStatistics : IndicatorObject
    {
        public _RangeExpansionStatistics(object _ctx) : base(_ctx) { }

        [Input] public int LongLength { get; set; } = 63;   //Longer term average to compare shorter term average against.
        [Input] public int ShortLength { get; set; } = 5;   //Shorter term average to determine whether a range contraction is occuring.
        [Input] public double ExpansionFactor { get; set; } = 1.0;  //Expansion factor required against a day being measured for range expansion detection, e.g. 1.2 would mean a 20% expansion over the shorter term average range.
        [Input] public ETradeDirection TradeDirection { get; set; } = ETradeDirection.BullishAndBearish;  //Output statistics for bullish vs bearish vs both.
        [Input] public int ForwardLookback { get; set; } = 21;  //Forward looking period to determine the percentage move made after a range expansion occurred.

        // Bullish collections
        private List<double> m_bullHighFromOpen = new List<double>();
        private List<double> m_bullOpenToClose  = new List<double>();
        private List<double> m_bullOpenToLow    = new List<double>();
        private List<double> m_bullDayRange     = new List<double>();
        private List<int>    m_bullReBarNumbers = new List<int>();

        // Bearish collections
        private List<double> m_bearLowFromOpen  = new List<double>();
        private List<double> m_bearOpenToClose  = new List<double>();
        private List<double> m_bearOpenToHigh   = new List<double>();
        private List<double> m_bearDayRange     = new List<double>();
        private List<int>    m_bearReBarNumbers = new List<int>();

        private VariableSeries<double> m_longAvgRange;
        private VariableSeries<double> m_shortAvgRange;

        protected override void Create()
        {
            m_longAvgRange  = new VariableSeries<double>(this);
            m_shortAvgRange = new VariableSeries<double>(this);
        }

        protected override void StartCalc()
        {
            m_bullHighFromOpen.Clear(); m_bullOpenToClose.Clear(); m_bullOpenToLow.Clear(); m_bullDayRange.Clear(); m_bullReBarNumbers.Clear();
            m_bearLowFromOpen.Clear();  m_bearOpenToClose.Clear(); m_bearOpenToHigh.Clear(); m_bearDayRange.Clear(); m_bearReBarNumbers.Clear();
        }

        protected override void CalcBar()
        {
            double curRange = Bars.High[0] - Bars.Low[0];
            double open     = Bars.Open[0];
            double close    = Bars.Close[0];

            int maxLen = Math.Max(LongLength, ShortLength);
            if (Bars.CurrentBar <= maxLen)
            {
                m_longAvgRange.Value  = curRange;
                m_shortAvgRange.Value = curRange;
                return;
            }

            double longAvgPrev  = m_longAvgRange[1];
            double shortAvgPrev = m_shortAvgRange[1];

            bool isContraction = shortAvgPrev < longAvgPrev;
            bool isExpansion   = curRange > (shortAvgPrev * ExpansionFactor);
            bool isBullishDay  = close > open;
            bool isBearishDay  = close < open;

            bool includeBull = (TradeDirection == ETradeDirection.BullishAndBearish || TradeDirection == ETradeDirection.Bullish) && isBullishDay;
            bool includeBear = (TradeDirection == ETradeDirection.BullishAndBearish || TradeDirection == ETradeDirection.Bearish) && isBearishDay;

            if (isContraction && isExpansion && open > 0)
            {
                if (includeBull)
                {
                    m_bullHighFromOpen.Add((Bars.High[0] - open) / open * 100);
                    m_bullOpenToClose.Add ((close - open) / open * 100);
                    m_bullOpenToLow.Add   ((open - Bars.Low[0]) / open * 100);
                    m_bullDayRange.Add    (curRange / open * 100);
                    m_bullReBarNumbers.Add(Bars.CurrentBar);
                }
                if (includeBear)
                {
                    m_bearLowFromOpen.Add ((open - Bars.Low[0]) / open * 100);
                    m_bearOpenToClose.Add ((open - close) / open * 100);
                    m_bearOpenToHigh.Add  ((Bars.High[0] - open) / open * 100);
                    m_bearDayRange.Add    (curRange / open * 100);
                    m_bearReBarNumbers.Add(Bars.CurrentBar);
                }
            }

            // Update averages
            m_longAvgRange.Value  = (longAvgPrev * (LongLength - 1) + curRange) / LongLength;
            m_shortAvgRange.Value = (shortAvgPrev * (ShortLength - 1) + curRange) / ShortLength;

            // ==================== OUTPUT ====================
            if (Bars.LastBarOnChart)
            {
                Output.WriteLine("=== RANGE EXPANSION STATS (Contraction + Expansion) ===");
                Output.WriteLine($"Symbol: {Bars.Info.Name} | Long={LongLength}, Short={ShortLength}, Factor={ExpansionFactor}x, Direction={TradeDirection}, ForwardLookback={ForwardLookback}\n");

                // === BULLISH SECTION ===
                if ((TradeDirection == ETradeDirection.BullishAndBearish || TradeDirection == ETradeDirection.Bullish) && m_bullReBarNumbers.Count >= 3)
                {
                    PrintBullishSection();
                }

                // === BEARISH SECTION ===
                if ((TradeDirection == ETradeDirection.BullishAndBearish || TradeDirection == ETradeDirection.Bearish) && m_bearReBarNumbers.Count >= 3)
                {
                    PrintBearishSection();
                }
            }
        }

        private void PrintBullishSection()
        {
            int total = m_bullReBarNumbers.Count;
            double pct = total / (double)Bars.CurrentBar * 100;

            Output.WriteLine("--- BULLISH RANGE EXPANSIONS ---");
            Output.WriteLine($"Bullish RE Days: {total} ({pct:F1}% of bars)");
            Output.WriteLine($"Avg High from Open : {m_bullHighFromOpen.Average():F2}%");
            Output.WriteLine($"Avg Open→Close     : {m_bullOpenToClose.Average():F2}%");
            Output.WriteLine($"Avg Open→Low (dip) : {m_bullOpenToLow.Average():F2}%");
            Output.WriteLine($"Avg Day Range       : {m_bullDayRange.Average():F2}%\n");

            // Forward distribution + cumulative for bullish
            PrintForwardDistribution(m_bullReBarNumbers, true);
        }

        private void PrintBearishSection()
        {
            int total = m_bearReBarNumbers.Count;
            double pct = total / (double)Bars.CurrentBar * 100;

            Output.WriteLine("--- BEARISH RANGE EXPANSIONS ---");
            Output.WriteLine($"Bearish RE Days: {total} ({pct:F1}% of bars)");
            Output.WriteLine($"Avg Low from Open  : {m_bearLowFromOpen.Average():F2}%  (drop from open)");
            Output.WriteLine($"Avg Open→Close     : {m_bearOpenToClose.Average():F2}%  (net down move)");
            Output.WriteLine($"Avg Open→High (adv): {m_bearOpenToHigh.Average():F2}%  (adverse for shorts)");
            Output.WriteLine($"Avg Day Range       : {m_bearDayRange.Average():F2}%\n");

            PrintForwardDistribution(m_bearReBarNumbers, false);
        }

        private void PrintForwardDistribution(List<int> reBarNumbers, bool isBullish)
        {
            var bucketCounts = new Dictionary<int, int>();
            int totalForDist = 0;

            foreach (int reBarNum in reBarNumbers)
            {
                int barsAgo = Bars.CurrentBar - reBarNum;
                if (barsAgo < ForwardLookback) continue;

                double entry = Bars.Close[barsAgo];
                if (entry <= 0) continue;

                double extreme = 0;

                if (isBullish)
                {
                    for (int i = 1; i <= ForwardLookback; i++)
                    {
                        int idx = barsAgo - i;
                        if (idx < 0) break;
                        extreme = Math.Max(extreme, Bars.High[idx]);
                    }
                    if (extreme <= 0) continue;
                    double gain = (extreme / entry - 1.0) * 100.0;
                    AddToBucket(bucketCounts, gain, ref totalForDist);
                }
                else // bearish - use lowest low
                {
                    double minLow = double.MaxValue;
                    for (int i = 1; i <= ForwardLookback; i++)
                    {
                        int idx = barsAgo - i;
                        if (idx < 0) break;
                        minLow = Math.Min(minLow, Bars.Low[idx]);
                    }
                    if (minLow >= double.MaxValue) continue;
                    double drop = (entry - minLow) / entry * 100.0;
                    AddToBucket(bucketCounts, drop, ref totalForDist);
                }
            }

            if (totalForDist == 0) return;

            string dirLabel = isBullish ? "BULLISH" : "BEARISH";
            Output.WriteLine($"FORWARD MOVE DISTRIBUTION ({dirLabel}) - Max move in next {ForwardLookback} bars");

            // Histogram
            Output.WriteLine("Move Bucket     Count     Probability");
            Output.WriteLine("------------------------------------");

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

            // === CUMULATIVE PROBABILITIES ===
            Output.WriteLine($"\nCUMULATIVE PROBABILITY ({dirLabel}) – Probability of AT LEAST X% move");
            Output.WriteLine("At least X%     Probability");

            double running = 0;
            // >50%
            if (bucketCounts.ContainsKey(999)) running += bucketCounts[999];
            Output.WriteLine(string.Format("At least 50%    {0,6:F1}%", running / totalForDist * 100));

            for (int low = 48; low >= 4; low -= 2)
            {
                if (bucketCounts.ContainsKey(low)) running += bucketCounts[low];
                Output.WriteLine(string.Format("At least {0,2}%    {1,6:F1}%", low, running / totalForDist * 100));
            }

            // at least 4% = everything except <4%
            double atLeast4 = (totalForDist - (bucketCounts.ContainsKey(-1) ? bucketCounts[-1] : 0)) / (double)totalForDist * 100;
            Output.WriteLine(string.Format("At least  4%    {0,6:F1}%", atLeast4));

            Output.WriteLine("------------------------------------------------\n");
        }

        private void AddToBucket(Dictionary<int, int> dict, double pct, ref int total)
        {
            total++;
            int bucket;
            if (pct < 4)        bucket = -1;
            else if (pct > 50)  bucket = 999;
            else                bucket = (int)(Math.Floor(pct / 2) * 2);

            if (!dict.ContainsKey(bucket)) dict[bucket] = 0;
            dict[bucket]++;
        }
    }
}