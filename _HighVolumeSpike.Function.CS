using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Checks whether the price moves higher by at least some percentage over the last N bars. Typically used to short pump-and-dump stocks.
		/// </summary>
		public sealed class _HighVolumeSpike : FunctionSimple<System.Boolean> {
			//constants
			public const int DEFAULT_LENGTH = 5;										//default length to look for spike
			public const double DEFAULT_GROWTH_PERCENTAGE = 2.0;    //100% growth
			public const int DEFAULT_VOLUME_LENGTH = 20;            //default length to look for volume spike
			public const double DEFAULT_VOLUME_SIGMA_MULTIPLIER = 2.0;  //default multiplier of average to consider as spike

			//inputs
			public IInstrument Instrument { get; set; }		//instrument to check
			public int SpikeLength { get; set; }          //length to look for spike
			public double GrowthPercentage { get; set; }  //percentage growth to consider as spike
			public int VolumeLength { get; set; }         //length to determine for volume average
			public double VolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike

			//properties


			//attributes
			private Function.VolumeWithSD m_volume;   //volume function

			//interface methods
			public _HighVolumeSpike(CStudyControl _master) : base(_master) { }
			public _HighVolumeSpike(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				m_volume = new Function.VolumeWithSD(this);
			}

			protected override void StartCalc()
			{
				if (SpikeLength <= 0) throw new Exception("Length must be larger than 0");
				//GrowthPercentage can be negative to test for decrease
				if (VolumeLength <= 0) throw new Exception("VolumeLength must be larger than 0");
				if (VolumeSigmaMultiplier <= 0) throw new Exception("VolumeMultiplier must be larger than 0");
				m_volume.Volume = Instrument.Volume;
				m_volume.Length = VolumeLength;
			}

			protected override System.Boolean CalcBar()
			{
				if (Instrument.CurrentBar < SpikeLength || Instrument.CurrentBar < VolumeLength) return false;
				if (m_volume.Average[0] + m_volume.Sigma[0] * VolumeSigmaMultiplier > Instrument.Volume[0]) return false;
				if (Instrument.Close[0] < Instrument.Close[SpikeLength - 1] * (1.0d + GrowthPercentage)) return false;
				return true;
			}

			//methods


		}
	}
}