using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	public class _HighVolumeSpike : _OpeningRangeBarSignal {
		//constants


		//inputs
		[Input] public int SpikeLength { get; set; }          //length to look for spike
		[Input] public double GrowthPercentage { get; set; }  //percentage growth to consider as spike
		[Input] public int VolumeLength { get; set; }         //length to determine for volume average
		[Input] public double VolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike

		//properties


		//attributes
		private PowerLanguage.Function._HighVolumeSpike m_highVolumeSpike;

		//interface methods
		public _HighVolumeSpike(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			SpikeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_LENGTH;
			GrowthPercentage = PowerLanguage.Function._HighVolumeSpike.DEFAULT_GROWTH_PERCENTAGE;
			VolumeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_LENGTH;
			VolumeSigmaMultiplier = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_SIGMA_MULTIPLIER;
			m_highVolumeSpike = new PowerLanguage.Function._HighVolumeSpike(this);
		}

		protected override void StartCalc()
		{
			//we always short the spike
			this._EntryPositionSide = EMarketPositionSide.Short;
			m_highVolumeSpike.Instrument = Bars;
			m_highVolumeSpike.SpikeLength = SpikeLength;
			m_highVolumeSpike.GrowthPercentage = GrowthPercentage;
			m_highVolumeSpike.VolumeLength = VolumeLength;
			m_highVolumeSpike.VolumeSigmaMultiplier = VolumeSigmaMultiplier;
		}

		protected override void CalcBar()
		{
			if (!m_highVolumeSpike[0]) return;
			base.CalcBar();
		}

		//methods


	}
}