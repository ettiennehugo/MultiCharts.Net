using System;
using System.Drawing;
using System.Linq;
using System.Timers;
using PowerLanguage.Function;
using PowerLanguage.Strategy;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Scanner collection to detect stocks that might have potential setups.
	/// </summary>
	[SameAsSymbol(true)]
	public class _ScannerCollection : IndicatorObject {
		//constants
		public const double DEFAULT_MINIMUM_PRICE = 1.0;
		public const int DEFAULT_MINIMUM_AVERAGE_VOLUME = 4000000;
		public const double DEFAULT_MINIMUM_AVERAGE_DOLLAR_VOLUME = 15000000;
		public const int DEFAULT_WIDTH = 15;

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
			VCP = 8,
		}

		//properties
		[Input] public double MinimumPrice { get; set; }  //minimum price to consider
		[Input] public int ADRLength { get; set; }        //length of the ADR to determine whether the stock is moving
		[Input] public double MinimumADR { get; set; }    //minimum percentage move to consider the stock for trading, set to zero to switch the ADR filter off
		[Input] public int VolumeLength { get; set; }     //length to determine for volume average
		[Input] public double MinimumAverageVolume { get;set; }  //minimum volume to consider as high volume
		[Input] public double MinimumAverageDollarVolume { get; set; }  //minimum dollar volume to consider as high volume

		[Input] public bool BearishOverExtensionEnable { get; set; }                 //enable bearish over extension 
		[Input] public Color BearishOverExtensionColor { get; set; }                 //color of the plot
		[Input] public double BearishOverExtensionMinimumDropPercent { get; set; }   //minimum required drop in the past x-days
		[Input] public int BearishOverExtensionDropDays { get; set; }                //number of days to look back for the drop
		[Input] public int BearishOverExtensionATRLength { get; set; }               //length of the ATR to determine the overextension
		[Input] public double BearishOverExtensionMinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
		[Input] public int BearishOverExtensionFastSmoothingLength { get; set; }     //length of smoother to determine more current price move
		[Input] public int BearishOverExtensionSlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
		[Input] public int BearishOverExtensionGradualDeclineLength { get; set; }    //length of scan to check for gradual decline

		[Input] public bool BullishOverExtensionEnable { get; set; }                 //enable bullish over extension 
		[Input] public Color BullishOverExtensionColor { get; set; }                 //color of the plot
		[Input] public double BullishOverExtensionMinimumRisePercent { get; set; }
		[Input] public int BullishOverExtensionRiseDays { get; set; }
		[Input] public int BullishOverExtensionATRLength { get; set; }
		[Input] public double BullishOverExtensionMinimumOverextensionFactor { get; set; }
		[Input] public int BullishOverExtensionFastSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionSlowSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionGradualInclineLength { get; set; }

		[Input] public bool CapitulationReversalEnable { get; set; }                   //enable capitulation reversal 
		[Input] public Color CapitulationReversalColor { get; set; }                   //color of the plot
		[Input] public double CapitulationReversalMinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public int CapitulationReversalGrowthDays { get; set; }                //number of days to look back for the growth
		[Input] public int CapitulationReversalScanLength { get; set; }                //lookback length for the market high
		[Input] public double CapitulationReversalStockDownPercent { get; set; }       //stock down percent from the high in the past y-days
		[Input] public double CapitulationReversalMarketDownPercent { get; set; }      //market down percent from the high in the past y-days
		[Input] public double CapitulationReversalGapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down

		[Input] public bool CrableNRXEnable { get; set; }      //enable Crable NRX
		[Input] public Color CrableNRXColor { get; set; }			 //color of the plot		
		[Input] public int CrableNRXLength { get; set; }       //required length of the narrow range bars
		[Input] public int CrableNRXScanLength { get; set; }   //scan length for the narrow range bars
		[Input] public double CrableNRXMinimumATR { get; set; }

		[Input] public bool DarvasBoxEnable { get; set; }      //enable Darvas Box
		[Input] public Color DarvasBoxColor { get; set; }      //color of the plot		
		[Input] public int DarvasBoxLength { get; set; }       //required length of the Darvas box
		[Input] public double DarvasBoxEpsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

		[Input] public bool EpisodicPivotEnable { get; set; }      //enable Episodic Pivot
		[Input] public Color EpisodicPivotColor { get; set; }      //color of the plot				
		[Input] public int EpisodicPivotGrowthDays { get; set; }
		[Input] public double EpisodicPivotGrowthPercent { get; set; }
		[Input] public double EpisodicPivotMinimumPercentageChange { get; set; } //gap up must be at least this minimum
		[Input] public double EpisodicPivotMaximumPercentageChange { get; set; } //gap up clipped to this maximum to avoid pump-and-dump stocks

		[Input] public bool HighVolumeSpikeEnable { get; set; }         //enable high volume spike
		[Input] Color HighVolumeSpikeColor { get; set; }               //color of the plot
		[Input] public int HighVolumeSpikeLength { get; set; }          //length to look for spike
		[Input] public double HighVolumeSpikeGrowthPercentage { get; set; }  //percentage growth to consider as spike
		[Input] public int HighVolumeSpikeVolumeLength { get; set; }         //length to determine for volume average
		[Input] public double HighVolumeSpikeVolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike

		[Input] public bool VCPEnable { get; set; }             //enable VCP
		[Input] public Color VCPColor { get; set; }             //color of the plot
		[Input] public double VCPMinimumGrowthPercent { get; set; } //minimum required growth in the past x-days
		[Input] public int VCPGrowthDays { get; set; }          //number of days to look back for the growth
		[Input] public int VCPFastLength { get; set; }          //fast length for the KAMA
		[Input] public int VCPSlowLength { get; set; }          //slow length for the KAMA
		[Input] public double VCPMinimumATRDelta { get; set; }  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
		[Input] public int VCPScanLength { get; set; }          //scan length for the VCP
		[Input] public int VCPMinimumRequiredLegs { get; set; } //minimum number of required legs to form the VCP
		[Input] public double VCPMinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 

		[Input] public bool Debug { get; set; }                  //debug flag to print out the values

		//attribute
		//private IPlotObject m_plot;
		private IPlotObjectStr m_text;
		private PowerLanguage.Function._ADR m_adr;
		private PowerLanguage.Function._VolumeWithSD m_volumeWithSD;
		private PowerLanguage.Function._BearishOverextension m_bearishOverextension;
		private PowerLanguage.Function._BullishOverextension m_bullishOverextension;
		private PowerLanguage.Function._CapitulationReversal m_capitulationReversal;
		private PowerLanguage.Function._CrableNRX m_crableNRX;
		private PowerLanguage.Function._DarvasBox m_darvasBox;
		private PowerLanguage.Function._EpisodicPivot m_episodicPivot;
		private PowerLanguage.Function._HighVolumeSpike m_highVolumeSpike;
		private PowerLanguage.Function._VCP m_vcp;

		//interface methods
		public _ScannerCollection(object _ctx):base(_ctx){}

		protected override void Create() {
			MinimumPrice = DEFAULT_MINIMUM_PRICE;
			ADRLength = PowerLanguage.Function._ADR.DEFAULT_LENGTH;
			MinimumADR = 0.022;
			VolumeLength = PowerLanguage.Function._VolumeWithSD.DEFAULT_LENGTH;
			MinimumAverageVolume = DEFAULT_MINIMUM_AVERAGE_VOLUME;
			MinimumAverageDollarVolume = DEFAULT_MINIMUM_AVERAGE_DOLLAR_VOLUME;

			BearishOverExtensionEnable = true;
			BearishOverExtensionColor = Color.Red;
			BearishOverExtensionMinimumDropPercent = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			BearishOverExtensionDropDays = PowerLanguage.Function._BearishOverextension.DEFAULT_DROP_DAYS;
			BearishOverExtensionATRLength = PowerLanguage.Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			BearishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BearishOverExtensionFastSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BearishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BearishOverExtensionGradualDeclineLength = PowerLanguage.Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;

			BullishOverExtensionEnable = true;
			BullishOverExtensionColor = Color.Green;
			BullishOverExtensionMinimumRisePercent = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			BullishOverExtensionRiseDays = PowerLanguage.Function._BullishOverextension.DEFAULT_RISE_DAYS;
			BullishOverExtensionATRLength = PowerLanguage.Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			BullishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BullishOverExtensionFastSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BullishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BullishOverExtensionGradualInclineLength = PowerLanguage.Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;

			CapitulationReversalEnable = true;
			CapitulationReversalColor = Color.Blue;
			CapitulationReversalMinimumGrowthPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			CapitulationReversalGrowthDays = PowerLanguage.Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			CapitulationReversalScanLength = PowerLanguage.Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			CapitulationReversalStockDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_STOCK_DOWN_PERCENT;
			CapitulationReversalMarketDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MARKET_DOWN_PERCENT;
			CapitulationReversalGapDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;

			CrableNRXEnable = true;
			CrableNRXColor = Color.Yellow;
			CrableNRXLength = PowerLanguage.Function._CrableNRX.DEFAULT_LENGTH;
			CrableNRXScanLength = PowerLanguage.Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			CrableNRXMinimumATR = PowerLanguage.Function._CrableNRX.DEFAULT_MINIMUM_ATR;

			DarvasBoxEnable = true;
			DarvasBoxColor = Color.Magenta;
			DarvasBoxLength = PowerLanguage.Function._DarvasBox.DEFAULT_LENGTH;
			DarvasBoxEpsilon = PowerLanguage.Function._DarvasBox.DEFAULT_EPSILON;

			EpisodicPivotEnable = true;
			EpisodicPivotColor = Color.Cyan;
			EpisodicPivotGrowthDays = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_BARS;
			EpisodicPivotGrowthPercent = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
			EpisodicPivotMinimumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			EpisodicPivotMaximumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;

			HighVolumeSpikeEnable = true;
			HighVolumeSpikeColor = Color.Orange;
			HighVolumeSpikeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_LENGTH;
			HighVolumeSpikeGrowthPercentage = PowerLanguage.Function._HighVolumeSpike.DEFAULT_GROWTH_PERCENTAGE;
			HighVolumeSpikeVolumeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_LENGTH;
			HighVolumeSpikeVolumeSigmaMultiplier = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_SIGMA_MULTIPLIER;

			VCPEnable = true;
			VCPColor = Color.Purple;
			VCPMinimumGrowthPercent = PowerLanguage.Strategy._VCP.DEFAULT_MINIMUM_GROWTH_PERCENT;
			VCPGrowthDays = PowerLanguage.Strategy._VCP.DEFAULT_GROWTH_DAYS;
			VCPFastLength = PowerLanguage.Function._VCP.DEFAULT_FAST_LENGTH;
			VCPSlowLength = PowerLanguage.Function._VCP.DEFAULT_SLOW_LENGTH;
			VCPMinimumATRDelta = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_ATR_DELTA;
			VCPScanLength = PowerLanguage.Function._VCP.DEFAULT_SCAN_LENGTH;
			VCPMinimumRequiredLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_REQUIRED_LEGS;
			VCPMinimumPercentageContracingLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;

			Debug = false;

			m_text = AddPlot(new StringPlotAttributes());
			//m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Point, Color.White));
			m_adr = new PowerLanguage.Function._ADR(this);
			m_volumeWithSD = new PowerLanguage.Function._VolumeWithSD(this);
			m_bearishOverextension = new PowerLanguage.Function._BearishOverextension(this);
			m_bullishOverextension = new PowerLanguage.Function._BullishOverextension(this);
			m_capitulationReversal = new PowerLanguage.Function._CapitulationReversal(this);
			m_crableNRX = new PowerLanguage.Function._CrableNRX(this);
			m_darvasBox = new PowerLanguage.Function._DarvasBox(this);
			m_episodicPivot = new PowerLanguage.Function._EpisodicPivot(this);
			m_highVolumeSpike = new PowerLanguage.Function._HighVolumeSpike(this);
			m_vcp = new PowerLanguage.Function._VCP(this);
		}

		protected override void StartCalc() {
			m_adr.Length = ADRLength;
			m_adr.Instrument = Bars;
			if (MinimumADR < 0.0 || MinimumADR > 1.0) throw new ArgumentException("MinimumADR is a percentage and should be between 0 and 1 (zero to switch off the filter)");
			if (MinimumAverageVolume < 0) throw new ArgumentException("MinimumAverageVolume must be greater than or equal to zero (0 = disable)");
			if (MinimumAverageDollarVolume < 0) throw new ArgumentException("MinimumAverageDollarVolume must be greater than or equal to zero (0 = disable)");
			m_volumeWithSD.Instrument = Bars;
			m_volumeWithSD.Length = VolumeLength;

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
			if (MaxDataStream > 1) 
				m_capitulationReversal.Market = BarsOfData(2);
			else
				m_capitulationReversal.Market = null;		//disable scanning of relative market movement

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
			m_episodicPivot.GrowthBars = EpisodicPivotGrowthDays;
			m_episodicPivot.GrowthPercent = EpisodicPivotGrowthPercent;
			m_episodicPivot.MinimumPercentageChange = EpisodicPivotMinimumPercentageChange;
			m_episodicPivot.MaximumPercentageChange = EpisodicPivotMaximumPercentageChange;

			m_highVolumeSpike.Instrument = Bars;
			m_highVolumeSpike.SpikeLength = HighVolumeSpikeLength;
			m_highVolumeSpike.GrowthPercentage = HighVolumeSpikeGrowthPercentage;
			m_highVolumeSpike.VolumeLength = HighVolumeSpikeVolumeLength;
			m_highVolumeSpike.VolumeSigmaMultiplier = HighVolumeSpikeVolumeSigmaMultiplier;

			m_vcp.Instrument = Bars;
			m_vcp.FastLength = VCPFastLength;
			m_vcp.SlowLength = VCPSlowLength;
			m_vcp.MinimumATRDelta = VCPMinimumATRDelta;
			m_vcp.ScanLength = VCPScanLength;
			m_vcp.MinimumRequiredLegs = VCPMinimumRequiredLegs;
			m_vcp.MinimumPercentageContracingLegs = VCPMinimumPercentageContracingLegs;
		}

		protected override void CalcBar(){
			m_text.Reset();
			if (Bars.Close[0] < MinimumPrice) return;  //minimum price filter
			if (m_adr[0] < MinimumADR) return;  //minimum average daily range move is too small
			if (MinimumAverageVolume > 0 && m_volumeWithSD[0] < MinimumAverageVolume) return;  //minimum average volume filter
			if (MinimumAverageDollarVolume > 0 && m_volumeWithSD.AverageDollarVolume[0] < MinimumAverageDollarVolume) return;  //minimum average dollar volume filter

			//TODO - build in the trend intensity indicator to filter out the stocks that are not moving

			if (BearishOverExtensionEnable && m_bearishOverextension[0])
				setPlot(ScannerMatch.BearishOverExtension, BearishOverExtensionColor);
			else if (BullishOverExtensionEnable && m_bullishOverextension[0])
				setPlot(ScannerMatch.BullishOverExtension, BullishOverExtensionColor);
			else if (CapitulationReversalEnable && m_capitulationReversal[0])
				setPlot(ScannerMatch.CapitulationReversal, CapitulationReversalColor);
			else if (CrableNRXEnable && m_crableNRX[0])
				setPlot(ScannerMatch.CrableNRX, CrableNRXColor);
			else if (DarvasBoxEnable && m_darvasBox[0])
				setPlot(ScannerMatch.DarvasBox, DarvasBoxColor);
			else if (EpisodicPivotEnable && m_episodicPivot[0])
				setPlot(ScannerMatch.EpisodicPivot, EpisodicPivotColor);
			else if (HighVolumeSpikeEnable && m_highVolumeSpike[0])
				setPlot(ScannerMatch.HighVolumeSpike, HighVolumeSpikeColor);
			else if (VCPEnable && m_vcp[0])
			{
				//growth filter for VCP
				if (VCPGrowthDays > 0 && Bars.CurrentBar < VCPGrowthDays) return;
				double growthPercent = double.NaN;
				if (VCPGrowthDays > 0)
					growthPercent = Bars.Close[VCPGrowthDays - 1] != 0.0d ? (Bars.Close[0] - Bars.Close[VCPGrowthDays - 1]) / Bars.Close[VCPGrowthDays - 1] : 0.0d;
				if (Environment.CalcReason == CalculationReason.Default && (double.IsNaN(growthPercent) || VCPMinimumGrowthPercent < growthPercent)) 
					setPlot(ScannerMatch.VCP, VCPColor);
			}
		}

		//methods
		private void setPlot(ScannerMatch match, Color color)
		{
			if (Debug) Output.WriteLine("{0}, {1}, {2}", match.ToString(), Bars.Close[0], Bars.Time[0]);
			if (Environment.ApplicationCode == EApplicationCode.Scanner)
				m_text.Set(match.ToString(), color);
			//else
			//	m_plot.Set(Bars.Low[0], color, DEFAULT_WIDTH);
		}
	}
}