using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		public sealed class VWAP : FunctionSeries<System.Double>
		{
			//Constants:
			public const int DEFAULT_LENGTH = 20;

			//Properties:
			public int Length { get; set; }
			public ISeries<double> Price { get; set; }
			public ISeries<double> Volume { get; set; }

			//Attributes:
			private VariableSeries<double> _priceVolume;
			private SummationFC _priceVolumeSum;
			private SummationFC _volumeSum;

			public VWAP(CStudyControl _master) : base(_master) { }
			public VWAP(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				// create variable objects and function objects
				Length = DEFAULT_LENGTH;
				_priceVolume = new VariableSeries<double>(this);
				_priceVolumeSum = new SummationFC(this);
				_volumeSum = new SummationFC(this);
			}

			protected override void StartCalc()
			{
				_priceVolumeSum.length = Length;
				_priceVolumeSum.price = _priceVolume;
				_volumeSum.length = Length;
				_volumeSum.price = Volume;
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close)
					_priceVolume.Value = Price.Value * Volume.Value;

				return (_volumeSum.Value != 0 ? _priceVolumeSum.Value / _volumeSum.Value : _priceVolumeSum.Value);
			}
		}
	}
}