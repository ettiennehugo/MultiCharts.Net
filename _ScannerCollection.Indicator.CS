using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
/// <summary>
/// Scanner collection to detect stocks that might have potential setups.
/// </summary>
	public class _ScannerCollection : IndicatorObject {
		//constants

		
		//types
		/// <summary>
		/// Values set in the plot when different scanners match.
		/// </summary>
		public enum ScannerMatch {
			None = 0,
			BearishOverExtension = 1,
			BullishOverExtension = 2,
			CapitulationReversal = 3,
			CrableNRX = 4,
			DarvasBox = 5,
			EpisodicPivot = 6,
			HighVolumeSpike = 7,
		}

		//properties
		[Input] public int ADRLength { get; set; }
		[Input] public double MinimumADR { get; set; }		//set to zero to switch the ADR filter off

		[Input] public double BearishOverExtensionMinimumDropPercent { get; set; }   //minimum required drop in the past x-days
		[Input] public int BearishOverExtensionDropDays { get; set; }                //number of days to look back for the drop
		[Input] public int BearishOverExtensionATRLength { get; set; }               //length of the ATR to determine the overextension
		[Input] public double BearishOverExtensionMinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
		[Input] public int BearishOverExtensionFastSmoothingLength { get; set; }     //length of smoother to determine more current price move
		[Input] public int BearishOverExtensionSlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
		[Input] public int BearishOverExtensionGradualDeclineLength { get; set; }    //length of scan to check for gradual decline

		[Input] public double BullishOverExtensionMinimumRisePercent { get; set; }
		[Input] public int BullishOverExtensionRiseDays { get; set; }
		[Input] public int BullishOverExtensionATRLength { get; set; }
		[Input] public double BullishOverExtensionMinimumOverextensionFactor { get; set; }
		[Input] public int BullishOverExtensionFastSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionSlowSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionGradualInclineLength { get; set; }

		[Input] public double CapitulationReversalMinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public int CapitulationReversalGrowthDays { get; set; }                //number of days to look back for the growth
		[Input] public int CapitulationReversalScanLength { get; set; }                //lookback length for the market high
		[Input] public double CapitulationReversalStockDownPercent { get; set; }       //stock down percent from the high in the past y-days
		[Input] public double CapitulationReversalMarketDownPercent { get; set; }      //market down percent from the high in the past y-days
		[Input] public double CapitulationReversalGapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down

		[Input] public int CrableNRXLength { get; set; }       //required length of the narrow range bars
		[Input] public int CrableNRXScanLength { get; set; }   //scan length for the narrow range bars
		[Input] public double CrableNRXMinimumATR { get; set; }

		[Input] public int DarvasBoxLength { get; set; }       //required length of the Darvas box
		[Input] public double DarvasBoxEpsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

		[Input] public int EpisodicPivotGrowthDays { get; set; }
		[Input] public double EpisodicPivotGrowthPercent { get; set; }
		[Input] public double EpisodicPivotMinimumPercentageChange { get; set; } //gap up must be at least this minimum
		[Input] public double EpisodicPivotMaximumPercentageChange { get; set; } //gap up clipped to this maximum to avoid pump-and-dump stocks

		[Input] public int HighVolumeSpikeSpikeLength { get; set; }          //length to look for spike
		[Input] public double HighVolumeSpikeGrowthPercentage { get; set; }  //percentage growth to consider as spike
		[Input] public int HighVolumeSpikeVolumeLength { get; set; }         //length to determine for volume average
		[Input] public double HighVolumeSpikeVolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike

		[Input] public bool Debug { get; set; }                  //debug flag to print out the values

		//attribute
		private IPlotObject m_plot;
		private PowerLanguage.Function._ADR m_adr;
		private PowerLanguage.Function._BearishOverextension m_bearishOverextension;
		private PowerLanguage.Function._BullishOverextension m_bullishOverextension;
		private PowerLanguage.Function._CapitulationReversal m_capitulationReversal;
		private PowerLanguage.Function._CrableNRX m_crableNRX;
		private PowerLanguage.Function._DarvasBox m_darvasBox;
		private PowerLanguage.Function._EpisodicPivot m_episodicPivot;
		private PowerLanguage.Function._HighVolumeSpike m_highVolumeSpike;

		//interface methods
		public _ScannerCollection(object _ctx):base(_ctx){}

		protected override void Create() {
			ADRLength = PowerLanguage.Function._ADR.LENGTH;
			MinimumADR = 0.02;

			BearishOverExtensionMinimumDropPercent = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			BearishOverExtensionDropDays = PowerLanguage.Function._BearishOverextension.DEFAULT_DROP_DAYS;
			BearishOverExtensionATRLength = PowerLanguage.Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			BearishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BearishOverExtensionFastSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BearishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BearishOverExtensionGradualDeclineLength = PowerLanguage.Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;

			BullishOverExtensionMinimumRisePercent = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			BullishOverExtensionRiseDays = PowerLanguage.Function._BullishOverextension.DEFAULT_RISE_DAYS;
			BullishOverExtensionATRLength = PowerLanguage.Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			BullishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BullishOverExtensionFastSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BullishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BullishOverExtensionGradualInclineLength = PowerLanguage.Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;

			CapitulationReversalMinimumGrowthPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			CapitulationReversalGrowthDays = PowerLanguage.Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			CapitulationReversalScanLength = PowerLanguage.Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			CapitulationReversalStockDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_STOCK_DOWN_PERCENT;
			CapitulationReversalMarketDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MARKET_DOWN_PERCENT;
			CapitulationReversalGapDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;

			CrableNRXLength = PowerLanguage.Function._CrableNRX.DEFAULT_LENGTH;
			CrableNRXScanLength = PowerLanguage.Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			CrableNRXMinimumATR = PowerLanguage.Function._CrableNRX.DEFAULT_MINIMUM_ATR;

			DarvasBoxLength = PowerLanguage.Function._DarvasBox.DEFAULT_LENGTH;
			DarvasBoxEpsilon = PowerLanguage.Function._DarvasBox.DEFAULT_EPSILON;

			EpisodicPivotGrowthDays = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_DAYS;
			EpisodicPivotGrowthPercent = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
			EpisodicPivotMinimumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			EpisodicPivotMaximumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;

			HighVolumeSpikeSpikeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_LENGTH;
			HighVolumeSpikeGrowthPercentage = PowerLanguage.Function._HighVolumeSpike.DEFAULT_GROWTH_PERCENTAGE;
			HighVolumeSpikeVolumeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_LENGTH;
			HighVolumeSpikeVolumeSigmaMultiplier = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_SIGMA_MULTIPLIER;

			Debug = false;

			m_adr = new PowerLanguage.Function._ADR(this);
			m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
			m_bearishOverextension = new PowerLanguage.Function._BearishOverextension(this);
			m_bullishOverextension = new PowerLanguage.Function._BullishOverextension(this);
			m_capitulationReversal = new PowerLanguage.Function._CapitulationReversal(this);
			m_crableNRX = new PowerLanguage.Function._CrableNRX(this);
			m_darvasBox = new PowerLanguage.Function._DarvasBox(this);
			m_episodicPivot = new PowerLanguage.Function._EpisodicPivot(this);
			m_highVolumeSpike = new PowerLanguage.Function._HighVolumeSpike(this);
		}

		protected override void StartCalc() {
			m_adr.Length = ADRLength;
			if (MinimumADR < 0.0 || MinimumADR > 1.0) throw new ArgumentException("MinimumADR is a percentage and should be between 0 and 1 (zero to switch off the filter)");

			m_bearishOverextension.Instrument = Bars;
			m_bearishOverextension.MinimumDropPercent = BearishOverExtensionMinimumDropPercent;
			m_bearishOverextension.DropDays = BearishOverExtensionDropDays;
			m_bearishOverextension.ATRLength = BearishOverExtensionATRLength;
			m_bearishOverextension.MinimumOverextensionFactor = BearishOverExtensionMinimumOverextensionFactor;
			m_bearishOverextension.FastSmoothingLength = BearishOverExtensionFastSmoothingLength;
			m_bearishOverextension.SlowSmoothingLength = BearishOverExtensionSlowSmoothingLength;
			m_bearishOverextension.GradualDeclineLength = BearishOverExtensionGradualDeclineLength;

			m_bullishOverextension.Instrument = Bars;
			m_bullishOverextension.MinimumRisePercent = BullishOverExtensionMinimumRisePercent;
			m_bullishOverextension.RiseDays = BullishOverExtensionRiseDays;
			m_bullishOverextension.ATRLength = BullishOverExtensionATRLength;
			m_bullishOverextension.MinimumOverextensionFactor = BullishOverExtensionMinimumOverextensionFactor;
			m_bullishOverextension.FastSmoothingLength = BullishOverExtensionFastSmoothingLength;
			m_bullishOverextension.SlowSmoothingLength = BullishOverExtensionSlowSmoothingLength;
			m_bullishOverextension.GradualInclineLength = BullishOverExtensionGradualInclineLength;

			m_capitulationReversal.Instrument = Bars;
			//m_capitulationReversal.Market = BarsOfData(2);	-- scanner will not work measure the market but only the stock
			m_capitulationReversal.MinimumGrowthPercent = CapitulationReversalMinimumGrowthPercent;
			m_capitulationReversal.GrowthDays = CapitulationReversalGrowthDays;
			m_capitulationReversal.ScanLength = CapitulationReversalScanLength;
			m_capitulationReversal.StockDownPercent = CapitulationReversalStockDownPercent;
			m_capitulationReversal.MarketDownPercent = CapitulationReversalMarketDownPercent;
			m_capitulationReversal.GapDownPercent = CapitulationReversalGapDownPercent;

			m_crableNRX.Instrument = Bars;
			m_crableNRX.Length = CrableNRXLength;
			m_crableNRX.ScanLength = CrableNRXScanLength;
			m_crableNRX.MinimumATR = CrableNRXMinimumATR;

			m_darvasBox.Instrument = Bars;
			m_darvasBox.Length = DarvasBoxLength;
			m_darvasBox.Epsilon = DarvasBoxEpsilon;

			m_episodicPivot.Instrument = Bars;
			m_episodicPivot.GrowthDays = EpisodicPivotGrowthDays;
			m_episodicPivot.GrowthPercent = EpisodicPivotGrowthPercent;
			m_episodicPivot.MinimumPercentageChange = EpisodicPivotMinimumPercentageChange;
			m_episodicPivot.MaximumPercentageChange = EpisodicPivotMaximumPercentageChange;

			m_highVolumeSpike.Instrument = Bars;
			m_highVolumeSpike.SpikeLength = HighVolumeSpikeSpikeLength;
			m_highVolumeSpike.GrowthPercentage = HighVolumeSpikeGrowthPercentage;
			m_highVolumeSpike.VolumeLength = HighVolumeSpikeVolumeLength;
			m_highVolumeSpike.VolumeSigmaMultiplier = HighVolumeSpikeVolumeSigmaMultiplier;

			//Debug = false;  - TODO wire this to functions
		}

		protected override void CalcBar(){
			m_plot.Set((double)ScannerMatch.None);
			
			if (m_adr[0] < MinimumADR) return;	//minimum average daily range move is too small
			
			if (m_bearishOverextension[0])
				m_plot.Set((double)ScannerMatch.BearishOverExtension);
			else if (m_bullishOverextension[0])
				m_plot.Set((double)ScannerMatch.BullishOverExtension);
			else if (m_capitulationReversal[0])
				m_plot.Set((double)ScannerMatch.CapitulationReversal);
			else if (m_crableNRX[0])
				m_plot.Set((double)ScannerMatch.CrableNRX);
			else if (m_darvasBox[0])
				m_plot.Set((double)ScannerMatch.DarvasBox);
			else if (m_episodicPivot[0])
				m_plot.Set((double)ScannerMatch.EpisodicPivot);
			else if (m_highVolumeSpike[0])
				m_plot.Set((double)ScannerMatch.HighVolumeSpike);
		}

		//methods


	}
}