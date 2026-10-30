using ATCenterProxy.interop;
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PowerLanguage.Strategy;
using static PowerLanguage.Strategy._CustomSignal;
using static PowerLanguage.Strategy._VolumeProfileBase;

namespace PowerLanguage.Function
{
	// Spec VPB-9 (analyst-base-class-spec.md): _VpVwapEngine moved verbatim out of _VolumeProfileBase.Strategy.CS into its own
	// helper file, per the CS-0 probe convention (_SignalJournal.Function.CS: plain class in PowerLanguage.Function, class name = file name).

	// <vwap-roll-rules>
	/// <summary>VWAP slot options (protected VwapConfig on _VolumeProfileBase; no [Input]s). Read once in StartCalc.</summary>
	public sealed class VwapConfig
	{
		/// <summary>Opt-in (educator review V1): Week / Month VWAP slots also reset at a contract roll (rollOn on the slot's
		/// stream, on the closing roll bar, like RollDate: committed sums dropped, the roll bar stays). Default false = unchanged:
		/// they reset on the period key only. Needs RollDetection != Off (else rollOn never fires). Quarter is not affected.</summary>
		public bool resetPeriodSlotsOnRoll = false;
	}

	/// <summary>MC-free reset rule of the period VWAP slots (tested in VpHelperTests).</summary>
	public static class VwapRollRules
	{
		/// <summary>True when a closing bar that fires rollOn resets the slot: always for RollDate; for Week / Month only when
		/// resetPeriodSlotsOnRoll is on; never otherwise.</summary>
		public static bool ResetsAtRoll(bool isRollDate, bool isWeekOrMonth, bool resetPeriodSlotsOnRoll, bool rollOn)
		{
			if (!rollOn) return false;
			return isRollDate || (isWeekOrMonth && resetPeriodSlotsOnRoll);
		}
	}
	// </vwap-roll-rules>

	/// <summary>
	/// Self-contained VWAP accumulator. Stepped from CalcBar against a data stream's Bars.
	/// Allows IOG and calculations on same bars by calculating provisional values and only
	/// committing the result to the Vwap once a bar is completed and no more ticks/data are
	/// expected for it.
	/// </summary>
	internal sealed class _VpVwapEngine
	{
		public _VolumeProfileBase parent;
		public EVwapType type;
		public int length;
		public DateTime fromDate;
		public int datastream;
		public string label;
		public bool resetPeriodOnRoll;            // VwapConfig.resetPeriodSlotsOnRoll (Week / Month only), set by configureVwap

		public double value;
		public double sigma;
		public bool valid;

		private double m_sumPV;
		private double m_sumP2V;
		private double m_sumV;

		// Provisional = the stream bar currently at index 0 (may be forming). Committed sums exclude it.
		private int m_curBar = int.MinValue;      // FullSymbolData.Current of the provisional bar
		private bool m_curLastInSession;          // LastBarInSession of that bar (latest call)
		private double m_provPV, m_provP2V, m_provV;
		private bool m_gapWarned;

		private bool m_seenAnchorBar;
		// Length VWAP only. Session and AdaptiveFromDate stay incremental in their own methods.
		// Ring holds the typical price and volume that add() would have seen, oldest slot at
		// m_lenNext. Reset / configureVwap clears the count so the next step refills.
		private double[] m_lenPrice;
		private double[] m_lenVol;
		private int m_lenCap;
		private int m_lenCount;
		private int m_lenNext;
		private int m_lenBar = int.MinValue;

		public void Reset()
		{
			m_sumPV = 0;
			m_sumP2V = 0;
			m_sumV = 0;
			m_seenAnchorBar = false;
			m_lenCount = 0;
			m_lenNext = 0;
			m_lenBar = int.MinValue;
			m_curBar = int.MinValue;
			m_curLastInSession = false;
			m_periodKey = long.MinValue;
			clearProvisional();
			m_gapWarned = false;
			value = double.NaN;
			sigma = double.NaN;
			valid = false;
		}

		public void Step(IInstrument bars)
		{
			valid = false;
			value = double.NaN;
			sigma = double.NaN;
			if (type == EVwapType.Disabled || bars == null) return;

			switch (type)
			{
				case EVwapType.Session:
					stepSession(bars);
					break;
				case EVwapType.Length:
					stepLength(bars);
					break;
				case EVwapType.AdaptiveFromDate:
					stepFromDate(bars);
					break;
				case EVwapType.Week:
				case EVwapType.Month:
				case EVwapType.Quarter:
				case EVwapType.RollDate:
					stepPeriod(bars);
					break;
			}
		}

		// M-AVWAP (spec rev 4 §2): Week / Month / Quarter reset at the first bar whose exchange-session date is in a new period;
		// RollDate resets when the roll detector fires on the closing roll bar (the committed sums are dropped, the roll bar stays).
		private long m_periodKey = long.MinValue;
		private void stepPeriod(IInstrument bars)
		{
			int bar = bars.FullSymbolData.Current;
			if (bar != m_curBar)
			{
				bool reset = m_curBar == int.MinValue;
				if (type != EVwapType.RollDate)
				{
					long key = _AvwapCalendar.PeriodKey(type == EVwapType.Week ? EAvwapPeriod.Week : type == EVwapType.Month ? EAvwapPeriod.Month : EAvwapPeriod.Quarter,
						parent.vwapSessionDate(bars.Time[0]));
					if (key != m_periodKey) { reset = true; m_periodKey = key; }
				}
				if (reset)
				{
					if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting {type} VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
					clearSums();
					clearProvisional();
				}
				else
					commitProvisional();
				warnIfGap(bar);
				m_curBar = bar;
			}
			bool rollSlot = type == EVwapType.RollDate || (resetPeriodOnRoll && (type == EVwapType.Week || type == EVwapType.Month));
			if (rollSlot && bars.Status == EBarState.Close &&
				VwapRollRules.ResetsAtRoll(type == EVwapType.RollDate, type == EVwapType.Week || type == EVwapType.Month, resetPeriodOnRoll, parent.vwapRollOn(datastream)))
			{
				if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting {type} VWAP at roll {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
				clearSums();
			}
			setProvisional(priceAt(bars, 0), volume(bars, 0));
			finish();
		}

		private void stepSession(IInstrument bars)
		{
			int bar = bars.FullSymbolData.Current;
			if (bar != m_curBar)
			{
				// The previously held bar is final now. A session starts after a bar that was LastBarInSession.
				bool newSession = m_curBar == int.MinValue || m_curLastInSession;
				if (newSession)
				{
					if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting session VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
					clearSums();
					clearProvisional();
				}
				else
					commitProvisional();
				warnIfGap(bar);
				m_curBar = bar;
			}
			m_curLastInSession = bars.LastBarInSession;
			setProvisional(priceAt(bars, 0), volume(bars, 0));
			finish();
		}

		/// <summary>
		/// Rolling length VWAP. Must match the old clear-and-resum of the last n bars,
		/// including the CurrentBar less-than n early-out and the vol/price skip inside add.
		/// sameBar replaces the newest slot so an IOG tick does not double-count.
		/// A CurrentBar jump or a length change refills the ring. The falling bar is
		/// subtracted, then sums are rebuilt newest-first so floating-point order matches
		/// the old loop (barsAgo 0 .. n-1).
		/// </summary>
		private void stepLength(IInstrument bars)
		{
			int n = length;
			if (n < 1 || bars.CurrentBar < n) return;
			ensureLengthRing(n);

			bool contiguous = m_lenCount == n && m_lenBar >= 0 && bars.CurrentBar == m_lenBar + 1;
			bool sameBar = m_lenCount == n && bars.CurrentBar == m_lenBar;
			if (sameBar)
			{
				// IOG: CurrentBar did not advance. Replace the slot written for this bar.
				int slot = (m_lenNext - 1 + n) % n;
				writeLengthSlot(slot, bars, 0);
			}
			else if (contiguous)
			{
				// m_lenNext is the oldest sample. Drop it, write the new bar there, advance.
				writeLengthSlot(m_lenNext, bars, 0);
				m_lenNext = (m_lenNext + 1) % n;
				m_lenBar = bars.CurrentBar;
			}
			else
			{
				// First fill, length change, or a non-contiguous CurrentBar. Oldest first so
				// m_lenNext lands on the bar that will fall out next.
				clearSums();
				m_lenCount = 0;
				m_lenNext = 0;
				for (int ago = n - 1; ago >= 0; ago--)
				{
					writeLengthSlot(m_lenNext, bars, ago);
					m_lenNext = (m_lenNext + 1) % n;
					m_lenCount++;
				}
				m_lenBar = bars.CurrentBar;
			}
			// Newest-first resync matches clear-and-resum addition order, including vol/price skips. It rebuilds the
			// sums on every path, so the ring is not adjusted incrementally (VPB-19: the old subtractions were dead work).
			resyncLengthSums(n);
			finish();
		}

		private void ensureLengthRing(int n)
		{
			if (m_lenPrice != null && m_lenCap == n) return;
			m_lenPrice = new double[n];
			m_lenVol = new double[n];
			m_lenCap = n;
			m_lenCount = 0;
			m_lenNext = 0;
			m_lenBar = int.MinValue;
		}

		private void writeLengthSlot(int slot, IInstrument bars, int barsAgo)
		{
			m_lenPrice[slot] = priceAt(bars, barsAgo);
			m_lenVol[slot] = volume(bars, barsAgo);
		}

		private void resyncLengthSums(int n)
		{
			clearSums();
			int count = m_lenCount < n ? m_lenCount : n;
			for (int k = 0; k < count; k++)
			{
				int slot = (m_lenNext - 1 - k + (n * 2)) % n;
				add(m_lenPrice[slot], m_lenVol[slot]);
			}
		}

		private void stepFromDate(IInstrument bars)
		{
			if (fromDate == DateTime.MinValue) return;
			if (bars.TimeValue < fromDate) return;
			int bar = bars.FullSymbolData.Current;
			if (!m_seenAnchorBar)
			{
				if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting anchored VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
				clearSums();
				clearProvisional();
				m_seenAnchorBar = true;
				m_curBar = bar;
			}
			else if (bar != m_curBar)
			{
				commitProvisional();
				warnIfGap(bar);
				m_curBar = bar;
			}
			setProvisional(priceAt(bars, 0), volume(bars, 0));
			finish();
		}

		private void clearSums()
		{
			m_sumPV = 0;
			m_sumP2V = 0;
			m_sumV = 0;
		}

		private void add(double price, double vol)
		{
			if (vol <= 0 || price <= 0) return;
			m_sumPV += price * vol;
			m_sumP2V += price * price * vol;
			m_sumV += vol;
		}

		private void clearProvisional()
		{
			m_provPV = 0;
			m_provP2V = 0;
			m_provV = 0;
		}

		private void setProvisional(double price, double vol)
		{
			if (vol <= 0 || price <= 0) { clearProvisional(); return; }   // same skip rule as add()
			m_provPV = price * vol;
			m_provP2V = price * price * vol;
			m_provV = vol;
		}

		private void commitProvisional()
		{
			m_sumPV += m_provPV;
			m_sumP2V += m_provP2V;
			m_sumV += m_provV;
			clearProvisional();
		}

		private void warnIfGap(int bar)
		{
			if (m_gapWarned || m_curBar == int.MinValue || bar <= m_curBar + 1) return;
			m_gapWarned = true;
			parent.trace(ETraceLevel.Warning, $"{label}: data stream {datastream} advanced {bar - m_curBar} bars between Data1 calculations; " +
																				"intermediate bars are not in the VWAP. Use a stream with bars at least as long as Data1.");
		}

		private void finish()
		{
			double v = m_sumV + m_provV;
			if (v <= 0) return;
			value = (m_sumPV + m_provPV) / v;
			double var = ((m_sumP2V + m_provP2V) / v) - (value * value);
			sigma = var > 0 ? Math.Sqrt(var) : 0;
			valid = value > 0;
		}

		private static double priceAt(IInstrument bars, int barsAgo)
		{
			double h = barsAgo == 0 ? bars.HighValue : bars.High[barsAgo];
			double l = barsAgo == 0 ? bars.LowValue : bars.Low[barsAgo];
			double c = barsAgo == 0 ? bars.CloseValue : bars.Close[barsAgo];
			if (h > 0 && l > 0) return (h + l + c) / 3.0;
			return c;
		}

		private static double volume(IInstrument bars, int barsAgo)
		{
			double v = barsAgo == 0 ? bars.VolumeValue : bars.Volume[barsAgo];
			if (v > 0) return v;
			return barsAgo == 0 ? bars.TicksValue : bars.Ticks[barsAgo];
		}
	}
}
