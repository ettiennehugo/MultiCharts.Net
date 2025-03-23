using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics.Eventing.Reader;
using System.ComponentModel.Design;

namespace PowerLanguage.Strategy {
/// <summary>
/// Scanner and entry/exit for back testing of the different strategies.
/// Requires the following data feeds:
/// 1. Minute entry timeframe
/// 2. Daily stock filtering timeframe
/// 3. Relevant market filtering timeframe
/// 
/// Entry:
/// * Uses dialy scanners
/// * Look for stocks that are moving with at least a specific minimum ADR, e.g. Qullamagiie uses 5%
/// * TODO - Revise this rule to look for any number of opening bars since a lot of the time the breakout would occur in the first half hour of the day.
///   * Enter long/short using a limit order on the close of a strong opening bar (e.g. 9:35 would be the opening bar)
/// * Risk of x-percent is used for entry size computed off the close of the opening bar and it's low/high based on entry direction plus/minus some buffer price.
///
/// Exit:
/// * If the order is filled the stop-loss is placed above/below the low/high of the entry bar.
/// * Close third to half of the position about n-days into the trade
/// * Close rest of the position if the market starts diverging away from the x-day moving average, OR
/// * Close rest of the position if the market closes under the x-day moving average
/// * Exit orders are sent n-minutes before the session close
/// 
/// Notes:
/// * IMPORTANT: To correctly trade the opening bar the sessions should all be assigned to the Regular Trading Hours for stocks, otherwise opening bar entries would be made
///              in the pre-/post-market sessions.
/// </summary>
	public class _ScannerCollectionStrategy : _CustomSignal {
		//constants
		public const int DEFAULT_DARVAS_GROWTH_DAYS = 120;
		public const double DEFAULT_DARVAS_GROWTH_PERCENT = 0.3;
		public const double DEFAULT_ENTRY_MINIMUM_PRICE = 1.0;
		public const int DEFAULT_ENTRY_MINIMUM_AVERAGE_VOLUME = 4000000;
		public const double DEFAULT_MINIMUM_ADR = 0.05;
		public const double DEFAULT_ENTRY_MINIMUM_AVERAGE_DOLLAR_VOLUME = 15000000;
		public const double DEFAULT_ENTRY_ACCOUNT_EQUITY_AT_RISK_PERCENT = 0.01;
		public const double DEFAULT_ENTRY_BAR_CLOSE_PERCENTILE = 0.9;
		public const double DEFAULT_ENTRY_MAX_RISK_BASED_EQUITY_PERCENT = 0.25;
		public const double DEFAULT_ENTRY_BUFFER_STOP_LOSS_PRICE = 0.0;
		public const double DEFAULT_EXIT_FIRST_EXIT_PERCENT = 0.5;
		public const int DEFAULT_EXIT_FIRST_EXIT_DAYS = 5;
		public const int DEFAULT_EXIT_MOVING_AVERAGE_LENGTH = 10;
		public const int DEFAULT_EXIT_SECOND_MINUTES_BEFORE_SESSION_END = 10;

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
		[Input] public int ADRLength { get; set; }
		[Input] public double MinimumADR { get; set; }		//set to zero to switch the ADR filter off
		[Input] public bool UseRoundLots { get; set; }    //use round lots for position size calculations

		[Input] public bool BearishOverExtensionEnable { get; set; }								 //enable bearish over extension 
		[Input] public double BearishOverExtensionMinimumDropPercent { get; set; }   //minimum required drop in the past x-days
		[Input] public int BearishOverExtensionDropDays { get; set; }                //number of days to look back for the drop
		[Input] public int BearishOverExtensionATRLength { get; set; }               //length of the ATR to determine the overextension
		[Input] public double BearishOverExtensionMinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
		[Input] public int BearishOverExtensionFastSmoothingLength { get; set; }     //length of smoother to determine more current price move
		[Input] public int BearishOverExtensionSlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
		[Input] public int BearishOverExtensionGradualDeclineLength { get; set; }    //length of scan to check for gradual decline

		[Input] public bool BullishOverExtensionEnable { get; set; }                 //enable bullish over extension 
		[Input] public double BullishOverExtensionMinimumRisePercent { get; set; }
		[Input] public int BullishOverExtensionRiseDays { get; set; }
		[Input] public int BullishOverExtensionATRLength { get; set; }
		[Input] public double BullishOverExtensionMinimumOverextensionFactor { get; set; }
		[Input] public int BullishOverExtensionFastSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionSlowSmoothingLength { get; set; }
		[Input] public int BullishOverExtensionGradualInclineLength { get; set; }

		[Input] public bool CapitulationReversalEnable { get; set; }                   //enable capitulation reversal 
		[Input] public double CapitulationReversalMinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public int CapitulationReversalGrowthDays { get; set; }                //number of days to look back for the growth
		[Input] public int CapitulationReversalScanLength { get; set; }                //lookback length for the market high
		[Input] public double CapitulationReversalStockDownPercent { get; set; }       //stock down percent from the high in the past y-days
		[Input] public double CapitulationReversalMarketDownPercent { get; set; }      //market down percent from the high in the past y-days
		[Input] public double CapitulationReversalGapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down

		[Input] public bool CrableNRXEnable { get; set; }			 //enable Crable NRX		
		[Input] public int CrableNRXLength { get; set; }       //required length of the narrow range bars
		[Input] public int CrableNRXScanLength { get; set; }   //scan length for the narrow range bars
		[Input] public double CrableNRXMinimumATR { get; set; }

		[Input] public bool DarvasBoxEnable { get; set; }      //enable Darvas Box
		[Input] public int DarvasBoxLength { get; set; }       //required length of the Darvas box
		[Input] public double DarvasBoxEpsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 
		[Input] public double DarvasBoxMinimumGrowthPercent { get; set; } //minimum required growth in the past x-days
		[Input] public int DarvasBoxGrowthDays { get; set; }          //number of days to look back for the growth

		[Input] public bool EpisodicPivotEnable { get; set; }      //enable Episodic Pivot
		[Input] public int EpisodicPivotGrowthDays { get; set; }
		[Input] public double EpisodicPivotGrowthPercent { get; set; }
		[Input] public double EpisodicPivotMinimumPercentageChange { get; set; } //gap up must be at least this minimum
		[Input] public double EpisodicPivotMaximumPercentageChange { get; set; } //gap up clipped to this maximum to avoid pump-and-dump stocks

		[Input] public bool HighVolumeSpikeEnable { get; set; }					//enable high volume spike
		[Input] public int HighVolumeSpikeLength { get; set; }          //length to look for spike
		[Input] public double HighVolumeSpikeGrowthPercentage { get; set; }  //percentage growth to consider as spike
		[Input] public int HighVolumeSpikeVolumeLength { get; set; }         //length to determine for volume average
		[Input] public double HighVolumeSpikeVolumeSigmaMultiplier { get; set; }  //volume multiplier of average to consider as spike

		[Input] public bool VCPEnable { get; set; }							//enable VCP
		[Input] public double VCPMinimumGrowthPercent { get; set; } //minimum required growth in the past x-days
		[Input] public int VCPGrowthDays { get; set; }          //number of days to look back for the growth
		[Input] public int VCPFastLength { get; set; }          //fast length for the KAMA
		[Input] public int VCPSlowLength { get; set; }          //slow length for the KAMA
		[Input] public double VCPMinimumATRDelta { get; set; }  //minimum ATR delta from the KAMA to consider a move significant enough for a new pivot interval
		[Input] public int VCPScanLength { get; set; }          //scan length for the VCP
		[Input] public int VCPMinimumRequiredLegs { get; set; } //minimum number of required legs to form the VCP
		[Input] public double VCPMinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 

		[Input] public double EntryMinimumPrice { get; set; }			//minimum price for the stocks to trade
		[Input] public int EntryAverageVolumeLength { get; set; }	//length used to compute the average volume and average dollar volume
		[Input] public int EntryMinimumAverageVolume { get; set; }	//minimum average daily volume of the stock
		[Input] public double EntryMinimumAverageDollarVolume { get; set; }	//minimum average dollar volume of the stock
		[Input] public double EntryAccountEquityAtRiskPercent { get; set; }	//account equity percent to use for entry
		[Input] public double EntryBarClosePercentile { get; set; } //close price in terms of the range of the bar that would be considered a "strong" close
		[Input] public double EntryBufferStopLossPrice { get; set; }	//buffer price to add to the determined stop-loss price
		[Input] public double EntryMaxRiskBasedEquityPercent { get; set; }	//since all entry sizes are computed on risk basis this is the maximum percentage of the account to user per position
		[Input] public int ExitMovingAverageLength { get; set; }		//moving average length used for exits
		[Input] public double ExitFirstExitPercent { get; set; }		//percentage of position to exit after below n-days (0 to disable)
		[Input] public int ExitFirstExitDays { get; set; }					//number of days after which we should exit the first part of the 
		[Input] public int ExitSecondMinutesBeforeSessionEnd { get; set; }	//minutes before the session end to start sending exit market orders

		[Input] public bool Debug { get; set; }                  //debug flag to print out the values

		//attribute
		private IInstrument m_stock;
		private IInstrument m_market;
		private IOrderPriced m_le;
		private IOrderPriced m_se;
		private IOrderMarket m_firstLx;
		private IOrderMarket m_firstSx;
		private IOrderMarket m_secondLx;
		private IOrderMarket m_secondSx;
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
		private PowerLanguage.Function._SuperSmoother m_exitMovingAverage;
		private DateTime m_entryDateTime;
		private int m_entrySize;
		private double m_riskAmountPerContract;
		private VariableSeries<bool> m_lastBarInSession;
		private ScannerMatch m_scannerMatch;
		private TimeSpan m_exitSecondTimeSpan;

		//interface methods
		public _ScannerCollectionStrategy(object _ctx):base(_ctx){}

		protected override void Create() {
			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "scsLE", EOrderAction.Buy));
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "scsSE", EOrderAction.SellShort));
			m_firstLx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "scs1LX", EOrderAction.Sell));
			m_firstSx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "scs1SX", EOrderAction.BuyToCover));
			m_secondLx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "scs2LX", EOrderAction.Sell));
			m_secondSx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "scs2SX", EOrderAction.BuyToCover));

			ADRLength = PowerLanguage.Function._ADR.DEFAULT_LENGTH;
			MinimumADR = DEFAULT_MINIMUM_ADR;
			UseRoundLots = false;

			BearishOverExtensionEnable = true;
			BearishOverExtensionMinimumDropPercent = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			BearishOverExtensionDropDays = PowerLanguage.Function._BearishOverextension.DEFAULT_DROP_DAYS;
			BearishOverExtensionATRLength = PowerLanguage.Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			BearishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BearishOverExtensionFastSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BearishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BearishOverExtensionGradualDeclineLength = PowerLanguage.Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;

			BullishOverExtensionEnable = true;
			BullishOverExtensionMinimumRisePercent = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			BullishOverExtensionRiseDays = PowerLanguage.Function._BullishOverextension.DEFAULT_RISE_DAYS;
			BullishOverExtensionATRLength = PowerLanguage.Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			BullishOverExtensionMinimumOverextensionFactor = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			BullishOverExtensionFastSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			BullishOverExtensionSlowSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			BullishOverExtensionGradualInclineLength = PowerLanguage.Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;

			CapitulationReversalEnable = true;
			CapitulationReversalMinimumGrowthPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			CapitulationReversalGrowthDays = PowerLanguage.Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			CapitulationReversalScanLength = PowerLanguage.Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			CapitulationReversalStockDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_STOCK_DOWN_PERCENT;
			CapitulationReversalMarketDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_MARKET_DOWN_PERCENT;
			CapitulationReversalGapDownPercent = PowerLanguage.Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;

			CrableNRXEnable = true;
			CrableNRXLength = PowerLanguage.Function._CrableNRX.DEFAULT_LENGTH;
			CrableNRXScanLength = PowerLanguage.Function._CrableNRX.DEFAULT_SCAN_LENGTH;
			CrableNRXMinimumATR = PowerLanguage.Function._CrableNRX.DEFAULT_MINIMUM_ATR;

			DarvasBoxEnable = true;
			DarvasBoxLength = PowerLanguage.Function._DarvasBox.DEFAULT_LENGTH;
			DarvasBoxEpsilon = PowerLanguage.Function._DarvasBox.DEFAULT_EPSILON;
			DarvasBoxGrowthDays = DEFAULT_DARVAS_GROWTH_DAYS;
			DarvasBoxMinimumGrowthPercent = DEFAULT_DARVAS_GROWTH_PERCENT;

			EpisodicPivotEnable = true;
			EpisodicPivotGrowthDays = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_DAYS;
			EpisodicPivotGrowthPercent = PowerLanguage.Function._EpisodicPivot.DEFAULT_GROWTH_PERCENT;
			EpisodicPivotMinimumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			EpisodicPivotMaximumPercentageChange = PowerLanguage.Function._EpisodicPivot.DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;

			HighVolumeSpikeEnable = true;
			HighVolumeSpikeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_LENGTH;
			HighVolumeSpikeGrowthPercentage = PowerLanguage.Function._HighVolumeSpike.DEFAULT_GROWTH_PERCENTAGE;
			HighVolumeSpikeVolumeLength = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_LENGTH;
			HighVolumeSpikeVolumeSigmaMultiplier = PowerLanguage.Function._HighVolumeSpike.DEFAULT_VOLUME_SIGMA_MULTIPLIER;

			VCPEnable = true;
			VCPMinimumGrowthPercent = PowerLanguage.Strategy._VCP.DEFAULT_MINIMUM_GROWTH_PERCENT;
			VCPGrowthDays = PowerLanguage.Strategy._VCP.DEFAULT_GROWTH_DAYS;
			VCPFastLength = PowerLanguage.Function._VCP.DEFAULT_FAST_LENGTH;
			VCPSlowLength = PowerLanguage.Function._VCP.DEFAULT_SLOW_LENGTH;
			VCPMinimumATRDelta = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_ATR_DELTA;
			VCPScanLength = PowerLanguage.Function._VCP.DEFAULT_SCAN_LENGTH;
			VCPMinimumRequiredLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_REQUIRED_LEGS;
			VCPMinimumPercentageContracingLegs = PowerLanguage.Function._VCP.DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;

			EntryMinimumPrice = DEFAULT_ENTRY_MINIMUM_PRICE;
			EntryAverageVolumeLength = PowerLanguage.Function._VolumeWithSD.DEFAULT_LENGTH;
			EntryMinimumAverageVolume = DEFAULT_ENTRY_MINIMUM_AVERAGE_VOLUME;
			EntryMinimumAverageDollarVolume = DEFAULT_ENTRY_MINIMUM_AVERAGE_DOLLAR_VOLUME;
			EntryAccountEquityAtRiskPercent = DEFAULT_ENTRY_ACCOUNT_EQUITY_AT_RISK_PERCENT;
			EntryBarClosePercentile = DEFAULT_ENTRY_BAR_CLOSE_PERCENTILE;
			EntryMaxRiskBasedEquityPercent = DEFAULT_ENTRY_MAX_RISK_BASED_EQUITY_PERCENT;
			EntryBufferStopLossPrice = DEFAULT_ENTRY_BUFFER_STOP_LOSS_PRICE;
			ExitFirstExitPercent = DEFAULT_EXIT_FIRST_EXIT_PERCENT;
			ExitFirstExitDays = DEFAULT_EXIT_FIRST_EXIT_DAYS;
			ExitMovingAverageLength = DEFAULT_EXIT_MOVING_AVERAGE_LENGTH;
			ExitSecondMinutesBeforeSessionEnd = DEFAULT_EXIT_SECOND_MINUTES_BEFORE_SESSION_END;

			Debug = false;

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
			m_exitMovingAverage = new PowerLanguage.Function._SuperSmoother(this);
			m_lastBarInSession = new VariableSeries<bool>(this);
		}

		protected override void StartCalc() {
			m_stock = BarsOfData(2);
			m_market = BarsOfData(3);

			m_adr.Length = ADRLength;
			m_adr.Instrument = m_stock;
			m_volumeWithSD.Length = EntryAverageVolumeLength;
			m_volumeWithSD.Instrument = m_stock;

			if (MinimumADR < 0.0 || MinimumADR > 1.0) throw new ArgumentException("MinimumADR is a percentage and should be between 0 and 1 (zero to switch off the filter)");
			if (DarvasBoxGrowthDays < 0) throw new ArgumentException("DarvasBoxGrowthDays needs to be larger or equal to zero (0 = disable)");
			if (EntryMinimumPrice < 0.0) throw new ArgumentException("EntryMinimumPrice needs to be larger or equal to zero (0 = disable)");
			if (EntryMinimumAverageVolume < 0.0) throw new ArgumentException("EntryMinimumAverageVolume needs to be larger or equal to zero (0 = disable)");
			if (EntryMinimumAverageDollarVolume < 0.0) throw new ArgumentException("EntryMinimumAverageDollarVolume needs to be larger or equal to zero (0 = disable)");
			if (EntryAccountEquityAtRiskPercent <= 0.0 || EntryAccountEquityAtRiskPercent > 1.0) throw new ArgumentException("EntryAccountEquityPercent must be between 0 and 1 [exclusive]");
			if (EntryBarClosePercentile <= 0.0 || EntryBarClosePercentile > 1.0) throw new ArgumentException("EntryBarClosePercentile must be between 0 [exclusive] and 1 [inclusive]");
			if (EntryBufferStopLossPrice < 0) throw new ArgumentException("EntryBufferStopLossPrice must be larger or equal to 0");
			if (ExitFirstExitPercent < 0.0 || ExitFirstExitPercent > 1.0) throw new ArgumentException("ExitFirstExitPercent must be between 0 and 1 [inclusive]");
			if (ExitFirstExitDays < 0) throw new ArgumentException("ExitFirstExitDays must be equal or greater than 0");
			if (ExitMovingAverageLength <= 0) throw new ArgumentException("ExitMovingAverageLength must be larger than 0");
			if (ExitSecondMinutesBeforeSessionEnd < 0) throw new ArgumentException("ExitSecondMinutesBeforeSessionEnd must be larger or equal than 0");

			m_bearishOverextension.Instrument = m_stock;
			m_bearishOverextension.MinimumDropPercent = BearishOverExtensionMinimumDropPercent;
			m_bearishOverextension.DropDays = BearishOverExtensionDropDays;
			m_bearishOverextension.ATRLength = BearishOverExtensionATRLength;
			m_bearishOverextension.MinimumOverextensionFactor = BearishOverExtensionMinimumOverextensionFactor;
			m_bearishOverextension.FastSmoothingLength = BearishOverExtensionFastSmoothingLength;
			m_bearishOverextension.SlowSmoothingLength = BearishOverExtensionSlowSmoothingLength;
			m_bearishOverextension.GradualDeclineLength = BearishOverExtensionGradualDeclineLength;

			m_bullishOverextension.Instrument = m_stock;
			m_bullishOverextension.MinimumRisePercent = BullishOverExtensionMinimumRisePercent;
			m_bullishOverextension.RiseDays = BullishOverExtensionRiseDays;
			m_bullishOverextension.ATRLength = BullishOverExtensionATRLength;
			m_bullishOverextension.MinimumOverextensionFactor = BullishOverExtensionMinimumOverextensionFactor;
			m_bullishOverextension.FastSmoothingLength = BullishOverExtensionFastSmoothingLength;
			m_bullishOverextension.SlowSmoothingLength = BullishOverExtensionSlowSmoothingLength;
			m_bullishOverextension.GradualInclineLength = BullishOverExtensionGradualInclineLength;

			m_capitulationReversal.Instrument = m_stock;
			m_capitulationReversal.Market = m_market;
			m_capitulationReversal.MinimumGrowthPercent = CapitulationReversalMinimumGrowthPercent;
			m_capitulationReversal.GrowthDays = CapitulationReversalGrowthDays;
			m_capitulationReversal.ScanLength = CapitulationReversalScanLength;
			m_capitulationReversal.StockDownPercent = CapitulationReversalStockDownPercent;
			m_capitulationReversal.MarketDownPercent = CapitulationReversalMarketDownPercent;
			m_capitulationReversal.GapDownPercent = CapitulationReversalGapDownPercent;

			m_crableNRX.Instrument = m_stock;
			m_crableNRX.Length = CrableNRXLength;
			m_crableNRX.ScanLength = CrableNRXScanLength;
			m_crableNRX.MinimumATR = CrableNRXMinimumATR;

			m_darvasBox.Instrument = m_stock;
			m_darvasBox.Length = DarvasBoxLength;
			m_darvasBox.Epsilon = DarvasBoxEpsilon;

			m_episodicPivot.Instrument = m_stock;
			m_episodicPivot.GrowthDays = EpisodicPivotGrowthDays;
			m_episodicPivot.GrowthPercent = EpisodicPivotGrowthPercent;
			m_episodicPivot.MinimumPercentageChange = EpisodicPivotMinimumPercentageChange;
			m_episodicPivot.MaximumPercentageChange = EpisodicPivotMaximumPercentageChange;

			m_highVolumeSpike.Instrument = m_stock;
			m_highVolumeSpike.SpikeLength = HighVolumeSpikeLength;
			m_highVolumeSpike.GrowthPercentage = HighVolumeSpikeGrowthPercentage;
			m_highVolumeSpike.VolumeLength = HighVolumeSpikeVolumeLength;
			m_highVolumeSpike.VolumeSigmaMultiplier = HighVolumeSpikeVolumeSigmaMultiplier;

			m_vcp.Instrument = m_stock;
			m_vcp.FastLength = VCPFastLength;
			m_vcp.SlowLength = VCPSlowLength;
			m_vcp.MinimumATRDelta = VCPMinimumATRDelta;
			m_vcp.ScanLength = VCPScanLength;
			m_vcp.MinimumRequiredLegs = VCPMinimumRequiredLegs;
			m_vcp.MinimumPercentageContracingLegs = VCPMinimumPercentageContracingLegs;
			
			m_exitMovingAverage.Instrument = m_stock;
			m_exitMovingAverage.Length = ExitMovingAverageLength;

			m_riskAmountPerContract = 0;
			m_entrySize = 0;
			m_entryDateTime = DateTime.MinValue;
			m_lastBarInSession.Value = false;
			m_scannerMatch = ScannerMatch.None;
			m_exitSecondTimeSpan = new TimeSpan(0, ExitSecondMinutesBeforeSessionEnd, 0);
		}

		protected override void CalcBar(){			
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					if (Bars.Status == EBarState.Close) 
						m_lastBarInSession.Value = Bars.LastBarInSession;

					if (PositionSide == 0)
						calcEntry();
					else
						calcExit();
					break;
			}
		}

		//methods
		/// <summary>
		/// Implement entry rules for the strategy when scanners signal entries.
		/// </summary>
		private void calcEntry()
		{
			//reset the entry state since we are no longer in a position
			m_scannerMatch = ScannerMatch.None;
			m_entryDateTime = DateTime.MinValue;
			m_riskAmountPerContract = 0.0;

			if (Bars.Close[0] < EntryMinimumPrice) return;  //minimum price is too small
			if (m_volumeWithSD.Average[0] < EntryMinimumAverageVolume) return;	//minimum volume not met
			if (m_volumeWithSD.AverageDollarVolume[0] < EntryMinimumAverageDollarVolume) return;	//minimum dollar volume not met
			if (m_adr[0] < MinimumADR) return;  //minimum average daily range move is too small

			if (BearishOverExtensionEnable && m_bearishOverextension[0])
				m_scannerMatch = ScannerMatch.BearishOverExtension;
			else if (BullishOverExtensionEnable && m_bullishOverextension[0])
				m_scannerMatch = ScannerMatch.BullishOverExtension;
			else if (CapitulationReversalEnable && m_capitulationReversal[0])
				m_scannerMatch = ScannerMatch.CapitulationReversal;
			else if (CrableNRXEnable && m_crableNRX[0])
				m_scannerMatch = ScannerMatch.CrableNRX;
			else if (DarvasBoxEnable && m_darvasBox[0] && m_stock.CurrentBar > DarvasBoxGrowthDays)
			{
				double lowest = m_stock.Close.Lowest(DarvasBoxGrowthDays);
				if (lowest != 0 && m_stock.High[1] != 0 &&
						((m_stock.Close[0] / lowest) - 1) > DarvasBoxMinimumGrowthPercent) 
					m_scannerMatch = ScannerMatch.DarvasBox;
			}
			else if (EpisodicPivotEnable && m_episodicPivot[0])
				m_scannerMatch = ScannerMatch.EpisodicPivot;
			else if (HighVolumeSpikeEnable && m_highVolumeSpike[0])
				m_scannerMatch = ScannerMatch.HighVolumeSpike;
			else if (VCPEnable && m_vcp[0] && m_stock.CurrentBar > VCPGrowthDays)
			{
				double lowest = m_stock.Close.Lowest(VCPGrowthDays);
				if (lowest != 0 && m_stock.High[1] != 0 &&
						((m_stock.Close[0] / lowest) - 1) > VCPMinimumGrowthPercent)
					m_scannerMatch = ScannerMatch.VCP;
			}

			//TBD - should the "long" entries rather be breakout entries??? (at least for the VCP - try to test this as well)

			switch (m_scannerMatch)
			{
				//long entries
				case ScannerMatch.BearishOverExtension:
				case ScannerMatch.CapitulationReversal:
				case ScannerMatch.CrableNRX:
				case ScannerMatch.DarvasBox:
				case ScannerMatch.EpisodicPivot:
				case ScannerMatch.VCP:
					//this bar is an opening bar if the previous bar was the last bar in the session
					//and we're only interested to send the order on the bar close
					if (m_lastBarInSession[1] && Bars.Status == EBarState.Close)
					{
						double low = Bars.Low[0];
						double close = Bars.Close[0];
						double range = Bars.High[0] - low;
						double risk = close - low; 
						double closePercentile = range != 0 ? risk / range : 0;    //this will skip trades for bars with no range

						if (closePercentile >= EntryBarClosePercentile)
						{
							m_riskAmountPerContract = risk + EntryBufferStopLossPrice;
							m_entrySize = entrySize(close, m_riskAmountPerContract, UseRoundLots);
							if (m_entrySize > 0)
							{
								if (Debug) Output.WriteLine("{0} - Long entry on {1}({2}) at close {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, m_entrySize, Bars.Time[0], close);
								m_le.Send(close, m_entrySize);
							}
							else if (Debug) Output.WriteLine("{0} - Long SKIPPED entry on {1}({2}) at close {3}, {4} - entry size is zero due to round lots usage", m_scannerMatch.ToString(), m_stock.Info.Name, m_entrySize, Bars.Time[0], close);
						}
					}
					break;
				//short entries
				case ScannerMatch.BullishOverExtension:
				case ScannerMatch.HighVolumeSpike:
					//this bar is an opening bar if the previous bar was the last bar in the session
					//and we're only interested to send the order on the bar close
					if (m_lastBarInSession[1] && Bars.Status == EBarState.Close)
					{
						double close = Bars.Close[0];
						double high = Bars.High[0];
						double range = high - Bars.Low[0];
						double risk = high - close;
						//this will skip trades for bars with no range, for short trades 1 - closePercentile is used to invert the percentiles so that
						//close prices closer to the bottom of the bar would be considered higher percentiles
						double closePercentile = range != 0 ? risk / range : 0;

						if (closePercentile >= EntryBarClosePercentile)
						{
							m_riskAmountPerContract = risk + EntryBufferStopLossPrice;
							m_entrySize = entrySize(close, m_riskAmountPerContract, UseRoundLots);
							if (m_entrySize > 0)
							{
								if (Debug) Output.WriteLine("{0} - Short entry on {1}({2}) at close {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, m_entrySize, Bars.Time[0], close);
								m_se.Send(close, m_entrySize);
							}
							else if (Debug) Output.WriteLine("{0} - Short SKIPPED entry on {1}({2}) at close {3}, {4} - entry size is zero due to round lots usage", m_scannerMatch.ToString(), m_stock.Info.Name, m_entrySize, Bars.Time[0], close);
						}
					}
					break;
			}
		}

		/// <summary>
		/// Implement exit rules around the first and second exits.
		/// </summary>
		private void calcExit() {
			//record first day of trade to determine n-day exit
			if (m_entryDateTime == DateTime.MinValue) m_entryDateTime = m_stock.Time[0];

			//STOP-LOSS - keep stop-loss in place based on entry bar
			CurSpecOrdersMode = ESpecOrdersMode.PerContract;

			//FIRST EXIT - if we're still holdng the full entry position exit half position into the n-day exit
			int currentPositionSize = (int)Math.Abs(StrategyInfo.MarketPosition);
			bool scanForSecondExit = m_entrySize != currentPositionSize;    //second exit scan will only be done once the first exit was performed
			if (!scanForSecondExit)
			{
				TimeSpan entryToCurrentBar = m_stock.Time[0] - m_entryDateTime;
				if (entryToCurrentBar.Days >= ExitFirstExitDays)
				{
					int sizeToClose = (int)Math.Truncate((double)m_entrySize / 2);
					if (PositionSide > 0)
					{
						if (Debug) Output.WriteLine("{0} - First long exit on {1}, selling {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, sizeToClose, m_stock.Time[0], Bars.Close[0]);
						m_firstLx.Send(sizeToClose);
					}
					else
					{
						if (Debug) Output.WriteLine("{0} - First short exit on {1}, buying {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, sizeToClose, m_stock.Time[0], Bars.Close[0]);
						m_firstSx.Send(sizeToClose);
					}
				}
			}
			else if (StrategyInfo.AvgEntryPrice != m_riskAmountPerContract)
				m_riskAmountPerContract = StrategyInfo.AvgEntryPrice;   //move stop-loss to break even

			GenerateStopLoss(m_riskAmountPerContract);

			//SECOND EXIT - determine whether we're getting within the n-minute window of the session exit to start
			//exit when the bar closes above/below the moving average based off associated entry and
			//we're getting close (n-minutes) from the session close
			if (scanForSecondExit)
			{
				scanForSecondExit = false;	//only scan for exit once we get into the n-minute window from the close
				TimeSpan currentBarTime = Bars.Time[0].TimeOfDay;

				foreach (var session in Bars.Sessions)
				{
					if (session.EndDay == Bars.Time[0].DayOfWeek &&
							session.EndTime - currentBarTime <= m_exitSecondTimeSpan)
					{
						scanForSecondExit = true;
						break;
					}
				}

				if (PositionSide > 0)
					switch (m_scannerMatch)
					{
						case ScannerMatch.BearishOverExtension:
							if (Bars.Close[0] > m_exitMovingAverage[0])
							{
								if (Debug) Output.WriteLine("{0} - Second long exit on {1}, selling {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, currentPositionSize, m_stock.Time[0], Bars.Close[0]);
								m_secondLx.Send();
							}
							break;
						default:
							if (Bars.Close[0] < m_exitMovingAverage[0])
							{
								if (Debug) Output.WriteLine("{0} - Second long exit on {1}, selling {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, currentPositionSize, m_stock.Time[0], Bars.Close[0]);
								m_secondLx.Send();
							}
							break;
					}
				else
					switch (m_scannerMatch)
					{
						case ScannerMatch.BullishOverExtension:
							if (Bars.Close[0] < m_exitMovingAverage[0])
							{
								if (Debug) Output.WriteLine("{0} - Second short exit on {1}, buying {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, currentPositionSize, m_stock.Time[0], Bars.Close[0]);
								m_secondSx.Send();
							}
							break;
						default:
							if (Bars.Close[0] > m_exitMovingAverage[0])
							{
								if (Debug) Output.WriteLine("{0} - Second short exit on {1}, buying {2} at {3}, {4}", m_scannerMatch.ToString(), m_stock.Info.Name, currentPositionSize, m_stock.Time[0], Bars.Close[0]);
								m_secondSx.Send();
							}
							break;
					}
			}
		}

		/// <summary>
		/// Computes the entry size based on the risk created by the entryPRice and stopLossPrice.
		/// </summary>
		private int entrySize(double entryPrice, double riskPerContract, bool useRoundLots) {
			if (entryPrice == 0)
			{
				if (Debug) Output.WriteLine("Error: entryPrice is zero so no size to compute.");
				return 0;
			}

			double equity = equityAvailable();
			double positionEquity = equity * EntryAccountEquityAtRiskPercent;
			int size = riskPerContract != 0 ? (int)Math.Truncate(positionEquity / riskPerContract) : 0;

			//clip position size based on entry equity per position or the account equity
			if (EntryMaxRiskBasedEquityPercent < 1.0)
				equity = equity * EntryMaxRiskBasedEquityPercent;
			int equityClippedPositionSize = (int)Math.Truncate(equity / entryPrice);
			size = size < equityClippedPositionSize ? size : equityClippedPositionSize;

			//round lots
			if (useRoundLots) size = (int)(Math.Truncate(size / 100d) * 100d);

			return size;
		}
	}
}