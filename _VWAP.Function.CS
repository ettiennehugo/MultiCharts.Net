using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage.Function
{
	/// <summary>
	/// Computes a VWAP based on a specific type and related inputs. Returns NaN if value is not computed.
	/// </summary>
	public sealed class _VWAP : FunctionSeries<System.Double>
	{
		//constants
		public const int DEFAULT_LENGTH = 20; //Default length when Length Type is set.

		//types
		public enum VWAPType
		{
			Day,          //VWAP per day - build the value for each new day
			Session,      //VWAP per session - no additional inputs needed
			Length,       //VWAP for number of bars
			AdaptiveFromDate, //VWAP from a specific date
			AdaptiveFromToDate, //VWAP from and to a specific date
		};

		//properties
		public VWAPType Type { get; set; } //Type of VWAP to use.  
		public int Length { get; set; }    //Length used when BarCount Type is set
		public DateTime FromDate { get; set; } //From date for AdaptiveFromDate/AdaptiveFromToDate Types
		public DateTime ToDate { get; set; } //To date for AdaptiveFromToDate Type

		//attribute
		private Double m_sumPriceVolume;
		private Double m_sumVolume;
		private VariableSeries<bool> m_lastBarInSession;
		private DateTime m_previousBarTime;

		//interface methods
		public _VWAP(CStudyControl _master) : base(_master) { }
		public _VWAP(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Type = VWAPType.Session;
			Length = DEFAULT_LENGTH;
			FromDate = DateTime.MinValue;
			ToDate = DateTime.MaxValue;
			m_lastBarInSession = new VariableSeries<bool>(this);
		}

		protected override void StartCalc()
		{
			m_previousBarTime = DateTime.MinValue;
			m_sumPriceVolume = 0;
			m_sumVolume = 0;
		}

		protected override System.Double CalcBar()
		{
			switch (Type)
			{
				case VWAPType.Day: return dayValue();
				case VWAPType.Session: return sessionValue();
				case VWAPType.Length: return lengthValue();
				case VWAPType.AdaptiveFromDate: return adaptiveFromDateValue();
				case VWAPType.AdaptiveFromToDate: return adaptiveFromToDateValue();
			}

			return System.Double.NaN;
		}

		//methods
		Double sessionValue()
		{
			m_lastBarInSession.Value = Bars.LastBarInSession;
			bool firstBarOfSession = Bars.CurrentBar == 1 || m_lastBarInSession[1];

			if (firstBarOfSession)
			{
				m_sumPriceVolume = 0;
				m_sumVolume = 0;
			}

			m_sumPriceVolume += Bars.CloseValue * Bars.VolumeValue;
			m_sumVolume += Bars.VolumeValue;

			return m_sumVolume != 0 ? m_sumPriceVolume / m_sumVolume : System.Double.NaN;
		}

		Double dayValue()
		{
			if (Environment.CalcReason != CalculationReason.Default)
				return m_sumVolume != 0 ? m_sumPriceVolume / m_sumVolume : System.Double.NaN;

			if (m_previousBarTime.Date != Bars.TimeValue.Date)
			{
				m_sumPriceVolume = 0;
				m_sumVolume = 0;
			}

			m_sumPriceVolume += Bars.CloseValue * Bars.VolumeValue;
			m_sumVolume += Bars.VolumeValue;

			m_previousBarTime = Bars.TimeValue.Date;

			return m_sumVolume != 0 ? m_sumPriceVolume / m_sumVolume : System.Double.NaN;
		}

		Double lengthValue()
		{
			if (Environment.CalcReason != CalculationReason.Default)
				return m_sumVolume != 0 ? m_sumPriceVolume / m_sumVolume : System.Double.NaN;

			if (Bars.CurrentBar > Length)
			{
				m_sumPriceVolume = 0;
				m_sumVolume = 0;

				for (int i = Length; i >= 0; --i)
				{
					m_sumPriceVolume += Bars.Close[i] * Bars.Volume[i];
					m_sumVolume += Bars.Volume[i];
				}

				return m_sumVolume != 0 ? m_sumPriceVolume / m_sumVolume : System.Double.NaN;
			}

			return System.Double.NaN;
		}

		Double adaptiveFromDateValue()
		{
			if (Bars.TimeValue < FromDate)
				return Double.NaN;

			double volume = Bars.VolumeValue;
			if (volume <= 0)
				volume = Bars.TicksValue;

			m_sumPriceVolume += Bars.CloseValue * volume;
			m_sumVolume += volume;

			return m_sumVolume > 0 ? m_sumPriceVolume / m_sumVolume : Double.NaN;
		}

		Double adaptiveFromToDateValue()
		{
			if (Bars.TimeValue < FromDate || Bars.TimeValue > ToDate)
				return Double.NaN;

			double volume = Bars.VolumeValue;
			if (volume <= 0)
				volume = Bars.TicksValue;

			m_sumPriceVolume += Bars.CloseValue * volume;
			m_sumVolume += volume;

			return m_sumVolume > 0 ? m_sumPriceVolume / m_sumVolume : Double.NaN;
		}

	}
}