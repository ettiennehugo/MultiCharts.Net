using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Implements common utility functions used by signals.
	/// </summary>
	public abstract class _CustomSignal : PortfolioSignalObject
	{
		//constants


		//inputs


		//properties


		//attributes


		//interface methods
		public _CustomSignal(object _ctx) : base(_ctx) { }

		//methods
		/// <summary>
		/// Determines the equity available for trading a position based on some custom risk equity per position percentage. If
		/// the strategy is run on a chart the whole equity is used if no custom equity percentage is given. If the strategy is
		/// is run in the portfolio manager the portfolio percentage per position is used if no custom equity percentage is given.
		/// </summary>
		protected double equityAvailable(double maxCustomRiskEquityPerPosPercent = 0.0d)
		{
			double equityAvailable = 0.0d;

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					equityAvailable = maxCustomRiskEquityPerPosPercent > 0.0d ? (this.InitialCapital + this.NetProfit) * maxCustomRiskEquityPerPosPercent : this.InitialCapital + this.NetProfit;
					break;
				case EApplicationCode.Portfolio:
					double percentEquity = maxCustomRiskEquityPerPosPercent > 0.0d ? maxCustomRiskEquityPerPosPercent : this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d;
					equityAvailable = (this.InitialCapital + this.Portfolio.NetProfit) * percentEquity;
					break;
			}

			return equityAvailable;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity,
		/// if given maxRiskEquityPerPosPercent is zero the .
		/// </summary>
		protected int positionSize(IInstrument bars, double maxRiskEquityPerPosPercent = 0.0d)
		{
			int result = 0;
			if (bars.Close[0] == 0.0d) return result;

			double positionCash = equityAvailable(maxRiskEquityPerPosPercent);
			result = (int)Math.Truncate(positionCash / bars.Close[0]);

			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity and a given stop loss price.
		/// </summary>
		protected int positionSize(IInstrument bars, double stopLossPrice, double maxRiskEquityPerPosPercent = 0.0d)
		{
			int result = 0;

			double positionCash = equityAvailable(maxRiskEquityPerPosPercent);
			if (positionCash > 0)
			{
				double risk;
				try
				{
					risk = checked(Math.Round(Math.Abs(bars.Close[0] - stopLossPrice), 2));
					if (risk != 0.0d) result = checked((int)Math.Truncate(positionCash / risk));
				}
				catch (OverflowException)
				{
					Output.WriteLine("WARNING - positionSize based on STOP-LOSS raised an overflow exception, signal ignored.");
					return 0;   //risk overflow exception occurred, ignore signal
				}
			}

			return result;
		}

		/// <summary>
		/// Postion size based on a ideal size position to take based on risk but the position size is clipped to a specific
		/// maximum average volume percentage. The ideal size can be computed with one of the above positionSize overloads
		/// and then fed into this method to clip the size as a percentage of average volume.
		/// </summary>
		protected int positionSize(IInstrument bars, int size, int volumePeriod, double maxVolumePercent)
		{
			int result = size;

			if (maxVolumePercent > 0.0d && bars.CurrentBar >= volumePeriod)
			{
				try
				{
					int clippedSize = (int)checked(Math.Round(bars.Volume.Average(volumePeriod) * (maxVolumePercent / 100.0d), 0));
					result = clippedSize < size ? clippedSize : size;
				}
				catch (OverflowException)
				{
					Output.WriteLine("WARNING - positionSize based on VOLUME raised an overflow exception, signal ignored.");
					return 0;   //risk overflow exception occurred, ignore signal
				}
			}

			return result;
		}

		/// <summary>
		/// Computes the average true range of the instrument over a given length.
		/// NOTE: This function is not optimized for performance and should be used cautiosly.
		/// </summary>
		protected double averageTrueRange(IInstrument bars, int length)
		{
			if (bars.CurrentBar < length) return 0.0d;

			double sum = 0.0;
			for (int i = 0; i < length; i++)
			{
				double tr = Math.Max(bars.High[i] - bars.Low[i], Math.Max(Math.Abs(bars.High[i] - bars.Close[i + 1]), Math.Abs(bars.Low[i] - bars.Close[i + 1])));
				sum += tr;
			}

			return sum / length;
		}

		/// <summary>
		/// Checks whether a price is within range from the moving average of given length and a specific factor of ATR's.
		/// </summary>
		protected bool isWithinAverageRange(IInstrument bars, int maLength, int atrLength, double atrFactor)
		{
			if (bars.CurrentBar < maLength || bars.CurrentBar < atrLength) return false;
			return Math.Abs(bars.Close[0] - bars.Close.Average(maLength)) < averageTrueRange(bars, atrLength) * atrFactor;
		}
	}
}