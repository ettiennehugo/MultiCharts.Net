using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart trading toolbar (works with the playback functionality).
  /// 
  /// NOTE:
  ///    * To have this strategy work correctly you need to configure the "Signal->Properties" to allow multiple entries if you plan on entering more than one position otherwise your entry orders will be ignored.
  ///    
  /// TODO:
  ///    * [TBD] Add +/- buttons next to the entry/exit size when the "Absolute size" strategy is selected to add/subtract bulk numbers of orders - use SpinButton control.
  ///       https://learn.microsoft.com/en-us/office/vba/Language/Reference/User-Interface-Help/spindown-spinup-events-delay-property-example
  ///    * Add parameters to control accellarators and other settings used by the strategy.
  ///    * [TBD] Add input parameter AutoStopLossExitOnRiskEntry to automatically enter the stop-loss order when a risk entry is performed.
  ///    * Add buttons to sync the entry/exit price value controls with the current price data - this will help to quickly catch up if the market moves away from the current price values.
  ///    * [Optional] Replace order type objects with the trade manager orders that can insert advanced orderslike OSO and OCO.
  ///    * [Optional] Add some of the portfolio statistics to display, e.g. Net Profit, Drawdown, Profit Factor, Percent Profitable, Number of Winning/Losing Traces, Average Winning/Losing Trade, Average Bars in Winning/Losing trades
  /// </summary>
  public class _ChartTrading_Advanced : SignalObject, IChartCustomDrawer
  {
    #region types
    enum EntryOrder
    {
      None,
      LongMarket,
      LongStop,
      LongLimit,
      LongStopLimit,
      ShortMarket,
      ShortStop,
      ShortLimit,
      ShortStopLimit,
      Breakout,
      Fade,
    };

    enum ExitOrder
    {
      None,
      Market,
      Stop,
      Limit,
      StopLimit,
      Bracket,
    };
    #endregion

    #region inputs
    [Input] public bool Debug { get; set; }                 //Enabled ebug messages to output
    [Input] public double DefaultSize { get; set; }         //Default size value
    [Input] public double DefaultSizePercent { get; set; }  //Default size percent setting
    [Input] public double PriceIncrement { get; set; }      //Control price increments for the stop/limit price entries
    [Input] public int ExitStopLimitDefaultPriceLookback { get; set; }  //High/low lookback to default exit stop/limit price levels to when a position is first entered
    [Input] public int VolumeWithSDLength { get; set; }     //Length used to Volume with standard deviation entries to control high volume entry conditions
    [Input] public int RisingVolumeLookback { get; set; }   //Lookback for the rising volume entry condition
    #endregion

    #region attributes
    private bool initializedToolbar;
    private bool initializePriceLevels;
    private EntryOrder _entryOrder;
    private EntryOrder _previousEntryOrder;
    private ExitOrder _exitOrder;
    private ExitOrder _previousExitOrder;
    private int _previousMarketPosition;
    private _VolumeWithSD _volumeWithSD;
    private bool _exitPosition;

    private IOrderMarket _marketLE;
    private IOrderMarket _marketLX;
    private IOrderMarket _marketSE;
    private IOrderMarket _marketSX;
    private IOrderPriced _stopLE;
    private IOrderPriced _stopLX;
    private IOrderPriced _stopSE;
    private IOrderPriced _stopSX;
    private IOrderPriced _limitLE;
    private IOrderPriced _limitLX;
    private IOrderPriced _limitSE;
    private IOrderPriced _limitSX;
    private IOrderStopLimit _stopLimitLE;
    private IOrderStopLimit _stopLimitLX;
    private IOrderStopLimit _stopLimitSE;
    private IOrderStopLimit _stopLimitSX;

    private ToolStripLabel _entryLabel;
    private ToolStripLabel _entrySizeStrategyLabel;
    private ToolStripComboBox _entrySizeStrategy;
    private ToolStripLabel _entrySizeLabel;
    private NumericUpDown _entrySize;
    private ToolStripLabel _entryStopLossLabel;
    private NumericUpDown _entryStopLossPrice;
    private ToolStripLabel _entryStopLabel;
    private ToolStripControlHost _entryStopPriceHost;
    private NumericUpDown _entryStopPrice;
    private ToolStripLabel _entryLimitLabel;
    private ToolStripControlHost _entryLimitPriceHost;
    private NumericUpDown _entryLimitPrice;
    private ToolStripLabel _entryConditionLabel;
    private ToolStripComboBox _entryCondition;
    private ToolStripLabel _entryLongLabel;
    private ToolStripButton _leMarket;
    private ToolStripButton _leStop;
    private ToolStripButton _leLimit;
    private ToolStripButton _leStopLimit;
    private ToolStripLabel _entryShortLabel;
    private ToolStripButton _seMarket;
    private ToolStripButton _seStop;
    private ToolStripButton _seLimit;
    private ToolStripButton _seStopLimit;
    private ToolStripLabel _advancedEntryHighPriceLabel;
    private NumericUpDown _advancedEntryHighPrice;
    private ToolStripControlHost _advancedEntryHighPriceHost;
    private ToolStripLabel _advancedEntryLowLabel;
    private NumericUpDown _advancedEntryLowPrice;
    private ToolStripControlHost _advancedEntryLowPriceHost;
    private ToolStripLabel _advancedEntryStopLossLabel;
    private NumericUpDown _advancedEntryStopLossPrice;
    private ToolStripControlHost _advancedEntryStopLossPriceHost;
    private ToolStripButton _advancedEntryBreakout;
    private ToolStripButton _advancedEntryFade;

    private ToolStripLabel _exitLabel;
    private ToolStripLabel _exitPositionLabel;
    private ToolStripLabel _exitSizeLabel;
    private ToolStripLabel _exitStopLabel;
    private ToolStripLabel _exitLimitLabel;
    private CheckBox _exitAll;
    private ToolStripLabel _positionLabel;
    private NumericUpDown _exitSize;
    private NumericUpDown _exitStopPrice;
    private NumericUpDown _exitLimitPrice;
    private ToolStripButton _exitStop;
    private ToolStripButton _exitLimit;
    private ToolStripButton _exitStopLimit;
    private ToolStripButton _exitMarket;
    private ToolStripLabel _advancedExitHighPriceLabel;
    private NumericUpDown _advancedExitHighPrice;
    private ToolStripControlHost _advancedExitHighPriceHost;
    private ToolStripLabel _advancedExitLowLabel;
    private NumericUpDown _advancedExitLowPrice;
    private ToolStripControlHost _advancedExitLowPriceHost;
    private ToolStripButton _advancedExitBracket;

    private Color LE_COLOR = Color.DarkGreen; //buy/buy to cover color
    private Color SE_COLOR = Color.DarkRed;   //sell/short sell color
    private Color LONG_BUTTON_COLOR = Color.Green; //long entry order button color
    private Color SHORT_BUTTON_COLOR = Color.LightCoral; //short entry order button color
    private Color ADVANCED_BUTTON_COLOR = Color.Gold; //advanced entry/exit order button color
    private float ORDER_LINE_WIDTH = 3.0f;    //order line width used for rendering
    private float DASH_LINE = 5.0f;           //stop order dash line length in pixels
    private float DASH_SPACE = 5.0f;          //stop order dash line space in pixels
    private float ORDER_LINE_LENGTH_PERCENT = 0.34f;  //percentage of chart for length of order line rendered
    #endregion

    #region standard mutlicharts functions
    public _ChartTrading_Advanced(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);
      Debug = false;
      DefaultSize = 100;
      DefaultSizePercent = 0.05;
      PriceIncrement = 0.05;
      ExitStopLimitDefaultPriceLookback = 20;
      VolumeWithSDLength = 20;
      RisingVolumeLookback = 3;
      initializedToolbar = false;

      _marketLE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MLE", EOrderAction.Buy));
      _marketLX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MLX", EOrderAction.Sell, OrderExit.Total));
      _marketSE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MSE", EOrderAction.SellShort));
      _marketSX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MSX", EOrderAction.BuyToCover, OrderExit.Total));
      _stopLE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SLE", EOrderAction.Buy));
      _stopLX = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SLX", EOrderAction.Sell, OrderExit.Total));
      _stopSE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SSE", EOrderAction.SellShort));
      _stopSX = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SSX", EOrderAction.BuyToCover, OrderExit.Total));
      _limitLE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LLE", EOrderAction.Buy));
      _limitLX = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LLX", EOrderAction.Sell, OrderExit.Total));
      _limitSE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LSE", EOrderAction.SellShort));
      _limitSX = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LSX", EOrderAction.BuyToCover, OrderExit.Total));
      _stopLimitLE = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SLLE", EOrderAction.Buy));
      _stopLimitLX = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SLLX", EOrderAction.Sell, OrderExit.Total));
      _stopLimitSE = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SLSE", EOrderAction.SellShort));
      _stopLimitSX = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "SLSX", EOrderAction.BuyToCover, OrderExit.Total));

      _volumeWithSD = new _VolumeWithSD(this);
    }

    protected override void Destroy()
    {
      if (initializedToolbar)
      {
        ChartToolBar.AccessToolBarAsync(tb =>
        {
          var _for_erase = new List<ToolStripItem>();

          foreach (ToolStripItem item in tb.Items)
            if (ReferenceEquals(this, item.Tag))
              _for_erase.Add(item);

          foreach (var item in _for_erase)
            tb.Items.Remove(item);
        });
      }

      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc()
    {
      //validate inputs
      if (PriceIncrement < 0 || PriceIncrement == 0) throw new Exception("PriceIncrement must be positive.");
      if (DefaultSize <= 0) throw new Exception("Default size must be larger than zero.");
      if (DefaultSizePercent <= 0 || DefaultSizePercent > 1.0) throw new Exception("Size percentage must be larger than zero and less or equal to one.");

      //initialize state variables
      _previousMarketPosition = 0;  //on first CalcBar this will update to current market position and update buttons etc.
      _exitPosition = false;
      initializePriceLevels = true;

      if (Debug)
      {
        Output.Clear();
        Output.WriteLine("StartCalc");
        Output.WriteLine("Profile: {0}", Profile);
      }

      //set inputs for functions
      _volumeWithSD.Length = VolumeWithSDLength;
      _volumeWithSD.Instrument = Bars;

      //setup our toolbar buttons
      if (!initializedToolbar)
      {
        ChartToolBar.AccessToolBar(tb => { initToolbar(ref tb); }); //need to create controls synchronously
        initializedToolbar = true;
      }

      //refresh the exit buttons based on the market position
      setExitEnabledState(StrategyInfo.MarketPosition != 0);
    }

    protected override void CalcBar()
    {
      if (Bars.LastBarOnChart)
        ExecControl.RecalcLastBarAfter(TimeSpan.FromSeconds(0.5));

      switch (Environment.CalcReason)
      {
        case CalculationReason.Default:
          if (Debug) Output.Write("Bar({0:d}) Position({1} -> {2}) ", Bars.Time[0], _previousMarketPosition, StrategyInfo.MarketPosition);
          //on the first bar on the chart initialize the price levels on the toolbar numeric up/down controls to a relevant price level
          if (initializePriceLevels) initPriceInputs(Math.Truncate(Bars.Close[0] / PriceIncrement) * PriceIncrement);
          
          //MultiCharts does not send market position change events in backtesting or playback
          //so we have to manually keep track of it, it would be nice if that fix that!!!
          if (_previousMarketPosition != StrategyInfo.MarketPosition)
            calcBarMarketPositionChange();
          calcBarDefault();
          if (Debug) Output.WriteLine("");
          break;
        case CalculationReason.MarketPositionChange:
          if (Debug) Output.Write("Bar({0:d}) Position({1} -> {2}) ", Bars.Time[0], _previousMarketPosition, StrategyInfo.MarketPosition);
          calcBarMarketPositionChange();
          if (Debug) Output.WriteLine("");
          break;
        case CalculationReason.Timer:
          //nothing to do currently - this is fired by the playback mechanism based on the playback speeed 
          //but you will get repeated bars
          break;
      }

      //refresh order buttons
      refreshOrderButtons();

      //refresh the market position if we're in a position
      if (StrategyInfo.MarketPosition != 0)
        setProfitLoss(StrategyInfo.MarketPosition, Positions[0].OpenProfit);
      else
        setProfitLoss(0, 0);
    }
    #endregion

    #region UI functions
    private void addItemToolStrip(ref ToolStrip tb, ToolStripItem item)
    {
      item.Tag = this;
      tb.Items.Add(item);
    }

    private void initToolbar(ref ToolStrip tb)
    {
      //setup general layout to control toolstrip appearance
      tb.SuspendLayout();
      tb.RenderMode = ToolStripRenderMode.System;

      tb.LayoutStyle = ToolStripLayoutStyle.Table;
      var layoutSettings = tb.LayoutSettings as TableLayoutSettings;
      layoutSettings.ColumnCount = 193;
      layoutSettings.RowCount = 5;

      //set size to accomodate controls
      tb.AutoSize = false;
      tb.ClientSize = new Size(tb.ClientSize.Width, (tb.Font.Height + tb.Margin.Top + tb.Margin.Bottom + tb.Padding.Top + tb.Padding.Bottom + 6) * layoutSettings.RowCount);

      //setup toolbar layout, these values are fine tuned to given the toolstrip a form appearance in
      //terms of the controls it contains
      layoutSettings.ColumnStyles.Clear();
      for (var i = 0; i < layoutSettings.ColumnCount; i++) layoutSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15));
      layoutSettings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      //last column to fill rest of toolbar

      layoutSettings.RowStyles.Clear();
      for (var i = 0; i < layoutSettings.RowCount; i++) layoutSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, tb.ClientSize.Height / layoutSettings.RowCount));

      #region entry items
      //create entry items
      _entryLabel = new ToolStripLabel
      {
        Text = "Entry",
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        TextDirection = ToolStripTextDirection.Vertical270,
      };
      addItemToolStrip(ref tb, _entryLabel);

      _entrySizeStrategyLabel = new ToolStripLabel
      {
        Text = "Size Strategy",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entrySizeStrategyLabel);

      _entrySizeStrategy = new ToolStripComboBox
      {
        Dock = DockStyle.Fill,
      };
      //NOTE: These entries needs to align with the code in entrySize so if you update this
      //      go and update that method as well to avoid bugs.
      _entrySizeStrategy.Items.Add("Absolute size");
      _entrySizeStrategy.Items.Add("Risk percentage");
      _entrySizeStrategy.SelectedIndex = 0;
      _entrySizeStrategy.SelectedIndexChanged += entrySizeStrategy_SelectedIndexChanged;
      addItemToolStrip(ref tb, _entrySizeStrategy);

      _entrySizeLabel = new ToolStripLabel
      {
        Text = "Size",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entrySizeLabel);

      _entrySize = new NumericUpDown
      {
        Value = 100,
        Minimum = 1,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 0,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _entrySize.Accelerations.Add(new NumericUpDownAcceleration(5, 10));
      _entrySize.Accelerations.Add(new NumericUpDownAcceleration(8, 50));
      _entrySize.ValueChanged += entrySize_ValueChanged;
      var entrySizeHost = new ToolStripControlHost(_entrySize);
      entrySizeHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, entrySizeHost);

      _entryStopLossLabel = new ToolStripLabel
      {
        Text = "Stop Loss",
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = SystemColors.GrayText,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entryStopLossLabel);

      _entryStopLossPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 1,
        Enabled = false,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _entryStopLossPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _entryStopLossPrice.Accelerations.Add(new NumericUpDownAcceleration(8, (decimal)(10 * PriceIncrement)));
      var entryStopLossPriceHost = new ToolStripControlHost(_entryStopLossPrice);
      entryStopLossPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, entryStopLossPriceHost);

      _entryStopLabel = new ToolStripLabel
      {
        Text = "Stop",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entryStopLabel);

      _entryStopPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 1,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _entryStopPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _entryStopPrice.Accelerations.Add(new NumericUpDownAcceleration(8, (decimal)(10 * PriceIncrement)));
      _entryStopPriceHost = new ToolStripControlHost(_entryStopPrice);
      _entryStopPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _entryStopPriceHost);

      _entryLimitLabel = new ToolStripLabel
      {
        Text = "Limit",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entryLimitLabel);

      _entryLimitPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _entryLimitPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _entryLimitPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _entryLimitPriceHost = new ToolStripControlHost(_entryLimitPrice);
      _entryLimitPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _entryLimitPriceHost);

      _entryConditionLabel = new ToolStripLabel
      {
        Text = "Condition",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _entryConditionLabel);

      _entryCondition = new ToolStripComboBox
      {
        Dock = DockStyle.Fill,
      };
      //NOTE: These entries needs to align with the code in isEntryConditionMet so if you update this
      //      go and update that method as well to avoid bugs.
      _entryCondition.Items.Add("None");
      _entryCondition.Items.Add("Above Average");
      _entryCondition.Items.Add("Above Average + 1SD");
      _entryCondition.Items.Add("Above Average + 2SD");
      _entryCondition.Items.Add("Above Average + 3SD");
      _entryCondition.Items.Add("Rising Volume");
      _entryCondition.SelectedIndex = 0;
      addItemToolStrip(ref tb, _entryCondition);

      _entryLongLabel = new ToolStripLabel
      {
        Text = "Long",
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        TextDirection = ToolStripTextDirection.Vertical270,
      };
      addItemToolStrip(ref tb, _entryLongLabel);

      _leMarket = new ToolStripButton
      {
        Text = "Market",
        ToolTipText = "Buy at Market",
        BackColor = LONG_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _leMarket.Click += (_1, _2) => onEntryButton(EntryOrder.LongMarket);
      addItemToolStrip(ref tb, _leMarket);

      _leStop = new ToolStripButton
      {
        Text = "Stop",
        ToolTipText = "Buy market at Stop",
        BackColor = LONG_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _leStop.Click += (_1, _2) => onEntryButton(EntryOrder.LongStop);
      addItemToolStrip(ref tb, _leStop);

      _leLimit = new ToolStripButton
      {
        Text = "Limit",
        ToolTipText = "Buy at Limit",
        BackColor = LONG_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _leLimit.Click += (_1, _2) => onEntryButton(EntryOrder.LongLimit);
      addItemToolStrip(ref tb, _leLimit);

      _leStopLimit = new ToolStripButton
      {
        Text = "Stop-Limit",
        ToolTipText = "Buy at Stop-Limit",
        BackColor = LONG_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _leStopLimit.Click += (_1, _2) => onEntryButton(EntryOrder.LongStopLimit);
      addItemToolStrip(ref tb, _leStopLimit);

      _entryShortLabel = new ToolStripLabel
      {
        Text = "Short",
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        TextDirection = ToolStripTextDirection.Vertical270,
      };
      addItemToolStrip(ref tb, _entryShortLabel);

      _seMarket = new ToolStripButton
      {
        Text = "Market",
        ToolTipText = "Sell short at Market",
        BackColor = SHORT_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _seMarket.Click += (_1, _2) => onEntryButton(EntryOrder.ShortMarket);
      addItemToolStrip(ref tb, _seMarket);

      _seStop = new ToolStripButton
      {
        Text = "Stop",
        ToolTipText = "Sell short market at Stop",
        BackColor = SHORT_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _seStop.Click += (_1, _2) => onEntryButton(EntryOrder.ShortStop);
      addItemToolStrip(ref tb, _seStop);

      _seLimit = new ToolStripButton
      {
        Text = "Limit",
        ToolTipText = "Sell short at Limit",
        BackColor = SHORT_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _seLimit.Click += (_1, _2) => onEntryButton(EntryOrder.ShortLimit);
      addItemToolStrip(ref tb, _seLimit);

      _seStopLimit = new ToolStripButton
      {
        Text = "Stop-Limit",
        ToolTipText = "Sell short at Stop-Limit",
        BackColor = SHORT_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _seStopLimit.Click += (_1, _2) => onEntryButton(EntryOrder.ShortStopLimit);
      addItemToolStrip(ref tb, _seStopLimit);

      _advancedEntryLowLabel = new ToolStripLabel
      {
        Text = "Advanced - Low Price",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _advancedEntryLowLabel);

      _advancedEntryLowPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _advancedEntryLowPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _advancedEntryLowPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _advancedEntryLowPriceHost = new ToolStripControlHost(_advancedEntryLowPrice);
      _advancedEntryLowPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _advancedEntryLowPriceHost);

      _advancedEntryHighPriceLabel = new ToolStripLabel
      {
        Text = "High Price",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _advancedEntryHighPriceLabel);

      _advancedEntryHighPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _advancedEntryHighPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _advancedEntryHighPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _advancedEntryHighPriceHost = new ToolStripControlHost(_advancedEntryHighPrice);
      _advancedEntryHighPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _advancedEntryHighPriceHost);

      _advancedEntryStopLossLabel = new ToolStripLabel
      {
        Text = "Stop Loss",
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = SystemColors.GrayText,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _advancedEntryStopLossLabel);

      _advancedEntryStopLossPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = (decimal)0.01,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
        Enabled = false,
      };
      _advancedEntryStopLossPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _advancedEntryStopLossPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _advancedEntryStopLossPriceHost = new ToolStripControlHost(_advancedEntryStopLossPrice);
      _advancedEntryStopLossPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _advancedEntryStopLossPriceHost);

      _advancedEntryBreakout = new ToolStripButton
      {
        Text = "Breakout",
        ToolTipText = "Breakout entry of low/high price",
        BackColor = ADVANCED_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _advancedEntryBreakout.Click += (_1, _2) => onEntryButton(EntryOrder.Breakout);
      addItemToolStrip(ref tb, _advancedEntryBreakout);

      _advancedEntryFade = new ToolStripButton
      {
        Text = "Fade",
        ToolTipText = "Fade entry of low/high price",
        BackColor = ADVANCED_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _advancedEntryFade.Click += (_1, _2) => onEntryButton(EntryOrder.Fade);
      addItemToolStrip(ref tb, _advancedEntryFade);
      #endregion

      var entryExitSeparator = new ToolStripSeparator
      {
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, entryExitSeparator);

      #region exit items
      _exitLabel = new ToolStripLabel
      {
        Text = "Exit",
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        TextDirection = ToolStripTextDirection.Vertical270,
      };
      addItemToolStrip(ref tb, _exitLabel);

      _exitPositionLabel = new ToolStripLabel
      {
        Text = "Position",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _exitPositionLabel);

      _positionLabel = new ToolStripLabel
      {
        ToolTipText = "Click to Close Position at Market",
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _positionLabel);
      _positionLabel.Click += onPositionLabelClick;
      setProfitLoss(0, 0);

      _exitAll = new CheckBox
      {
        Text = "Exit whole position",
        CheckState = CheckState.Unchecked,
        Dock = DockStyle.Fill,
      };
      _exitAll.CheckStateChanged += exitAll_CheckStateChanged;
      var exitAllHost = new ToolStripControlHost(_exitAll);
      exitAllHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, exitAllHost);

      _exitSizeLabel = new ToolStripLabel
      {
        Text = "Size",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _exitSizeLabel);

      _exitSize = new NumericUpDown
      {
        Value = 100,
        Minimum = 1,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 0,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _exitSize.Accelerations.Add(new NumericUpDownAcceleration(5, 10));
      _exitSize.Accelerations.Add(new NumericUpDownAcceleration(8, 50));
      var exitSizeHost = new ToolStripControlHost(_exitSize);
      exitSizeHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, exitSizeHost);

      _exitStopLabel = new ToolStripLabel
      {
        Text = "Stop",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _exitStopLabel);

      _exitStopPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 1,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _exitStopPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _exitStopPrice.Accelerations.Add(new NumericUpDownAcceleration(8, (decimal)(10 * PriceIncrement)));
      var exitStopPriceHost = new ToolStripControlHost(_exitStopPrice);
      exitStopPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, exitStopPriceHost);

      _exitLimitLabel = new ToolStripLabel
      {
        Text = "Limit",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _exitLimitLabel);

      _exitLimitPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _exitLimitPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _exitLimitPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      var exitLimitPriceHost = new ToolStripControlHost(_exitLimitPrice);
      exitLimitPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, exitLimitPriceHost);

      _exitMarket = new ToolStripButton
      {
        Text = "Market",
        ToolTipText = "Exit at Market",
        Dock = DockStyle.Fill,
        Enabled = false,
      };
      _exitMarket.Click += (_1, _2) => onExitButton(ExitOrder.Market);
      addItemToolStrip(ref tb, _exitMarket);

      _exitStop = new ToolStripButton
      {
        Text = "Stop",
        ToolTipText = "Exit market at Stop",
        Dock = DockStyle.Fill,
        Enabled = false,
      };
      _exitStop.Click += (_1, _2) => onExitButton(ExitOrder.Stop);
      addItemToolStrip(ref tb, _exitStop);

      _exitLimit = new ToolStripButton
      {
        Text = "Limit",
        ToolTipText = "Exit market at Limit",
        Dock = DockStyle.Fill,
        Enabled = false,
      };
      _exitLimit.Click += (_1, _2) => onExitButton(ExitOrder.Limit);
      addItemToolStrip(ref tb, _exitLimit);

      _exitStopLimit = new ToolStripButton
      {
        Text = "Stop-Limit",
        ToolTipText = "Exit market at Stop-Limit",
        Dock = DockStyle.Fill,
        Enabled = false,
      };
      _exitStopLimit.Click += (_1, _2) => onExitButton(ExitOrder.StopLimit);
      addItemToolStrip(ref tb, _exitStopLimit);

      _advancedExitLowLabel = new ToolStripLabel
      {
        Text = "Advanced - Low Price",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _advancedExitLowLabel);

      _advancedExitLowPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _advancedExitLowPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _advancedExitLowPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _advancedExitLowPriceHost = new ToolStripControlHost(_advancedExitLowPrice);
      _advancedExitLowPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _advancedExitLowPriceHost);

      _advancedExitHighPriceLabel = new ToolStripLabel
      {
        Text = "High Price",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
      };
      addItemToolStrip(ref tb, _advancedExitHighPriceLabel);

      _advancedExitHighPrice = new NumericUpDown
      {
        ThousandsSeparator = true,
        DecimalPlaces = 2,
        Increment = (decimal)PriceIncrement,
        Minimum = 0,
        Maximum = Decimal.MaxValue,
        TabStop = true,
        TabIndex = 2,
        ReadOnly = true,    //chart intercepts keyboard input causing issues with NumericUpDown number entry
      };
      _advancedExitHighPrice.Accelerations.Add(new NumericUpDownAcceleration(5, (decimal)(5 * PriceIncrement)));
      _advancedExitHighPrice.Accelerations.Add(new NumericUpDownAcceleration(10, (decimal)(10 * PriceIncrement)));
      _advancedExitHighPriceHost = new ToolStripControlHost(_advancedExitHighPrice);
      _advancedExitHighPriceHost.Dock = DockStyle.Fill;
      addItemToolStrip(ref tb, _advancedExitHighPriceHost);

      _advancedExitBracket = new ToolStripButton
      {
        Text = "Bracket",
        ToolTipText = "Bracket exit of low/high price",
        BackColor = ADVANCED_BUTTON_COLOR,
        Dock = DockStyle.Fill,
      };
      _advancedExitBracket.Click += (_1, _2) => onExitButton(ExitOrder.Bracket);
      addItemToolStrip(ref tb, _advancedExitBracket);
      #endregion

      #region layout
      layoutSettings.SetCellPosition(_entryLabel, new TableLayoutPanelCellPosition(0, 0));
      layoutSettings.SetColumnSpan(_entryLabel, 3);
      layoutSettings.SetRowSpan(_entryLabel, layoutSettings.RowCount);

      layoutSettings.SetCellPosition(_entrySizeStrategyLabel, new TableLayoutPanelCellPosition(4, 0));
      layoutSettings.SetColumnSpan(_entrySizeStrategyLabel, 11);
      layoutSettings.SetRowSpan(_entrySizeStrategyLabel, 1);
      layoutSettings.SetCellPosition(_entrySizeStrategy, new TableLayoutPanelCellPosition(15, 0));
      layoutSettings.SetColumnSpan(_entrySizeStrategy, 25);
      layoutSettings.SetRowSpan(_entrySizeStrategy, 1);
      layoutSettings.SetCellPosition(_entrySizeLabel, new TableLayoutPanelCellPosition(43, 0));
      layoutSettings.SetColumnSpan(_entrySizeLabel, 10);
      layoutSettings.SetRowSpan(_entrySizeLabel, 1);
      layoutSettings.SetCellPosition(entrySizeHost, new TableLayoutPanelCellPosition(50, 0));
      layoutSettings.SetColumnSpan(entrySizeHost, 10);
      layoutSettings.SetRowSpan(entrySizeHost, 1);
      layoutSettings.SetCellPosition(_entryStopLossLabel, new TableLayoutPanelCellPosition(70, 0));
      layoutSettings.SetColumnSpan(_entryStopLossLabel, 10);
      layoutSettings.SetRowSpan(_entryStopLossLabel, 1);
      layoutSettings.SetCellPosition(entryStopLossPriceHost, new TableLayoutPanelCellPosition(80, 0));
      layoutSettings.SetColumnSpan(entryStopLossPriceHost, 10);
      layoutSettings.SetRowSpan(entryStopLossPriceHost, 1);

      layoutSettings.SetCellPosition(_entryStopLabel, new TableLayoutPanelCellPosition(32, 1));
      layoutSettings.SetColumnSpan(_entryStopLabel, 10);
      layoutSettings.SetRowSpan(_entryStopLabel, 1);
      layoutSettings.SetCellPosition(_entryStopPriceHost, new TableLayoutPanelCellPosition(42, 1));
      layoutSettings.SetColumnSpan(_entryStopPriceHost, 10);
      layoutSettings.SetRowSpan(_entryStopPriceHost, 1);
      layoutSettings.SetCellPosition(_entryLimitLabel, new TableLayoutPanelCellPosition(4, 1));
      layoutSettings.SetColumnSpan(_entryLimitLabel, 10);
      layoutSettings.SetRowSpan(_entryLimitLabel, 1);
      layoutSettings.SetCellPosition(_entryLimitPriceHost, new TableLayoutPanelCellPosition(15, 1));
      layoutSettings.SetColumnSpan(_entryLimitPriceHost, 10);
      layoutSettings.SetRowSpan(_entryLimitPriceHost, 1);
      layoutSettings.SetCellPosition(_entryConditionLabel, new TableLayoutPanelCellPosition(58, 1));
      layoutSettings.SetColumnSpan(_entryConditionLabel, 10);
      layoutSettings.SetRowSpan(_entryConditionLabel, 1);
      layoutSettings.SetCellPosition(_entryCondition, new TableLayoutPanelCellPosition(69, 1));
      layoutSettings.SetColumnSpan(_entryCondition, 25);
      layoutSettings.SetRowSpan(_entryCondition, 1);

      layoutSettings.SetCellPosition(_entryLongLabel, new TableLayoutPanelCellPosition(12, 2));
      layoutSettings.SetColumnSpan(_entryLongLabel, 4);
      layoutSettings.SetRowSpan(_entryLongLabel, 2);
      layoutSettings.SetCellPosition(_leMarket, new TableLayoutPanelCellPosition(17, 2));
      layoutSettings.SetColumnSpan(_leMarket, 15);
      layoutSettings.SetRowSpan(_leMarket, 1);
      layoutSettings.SetCellPosition(_leStop, new TableLayoutPanelCellPosition(17, 3));
      layoutSettings.SetColumnSpan(_leStop, 15);
      layoutSettings.SetRowSpan(_leStop, 1);
      layoutSettings.SetCellPosition(_leLimit, new TableLayoutPanelCellPosition(32, 2));
      layoutSettings.SetColumnSpan(_leLimit, 15);
      layoutSettings.SetRowSpan(_leLimit, 1);
      layoutSettings.SetCellPosition(_leStopLimit, new TableLayoutPanelCellPosition(32, 3));
      layoutSettings.SetColumnSpan(_leStopLimit, 15);
      layoutSettings.SetRowSpan(_leStopLimit, 1);

      layoutSettings.SetCellPosition(_entryShortLabel, new TableLayoutPanelCellPosition(48, 2));
      layoutSettings.SetColumnSpan(_entryShortLabel, 4);
      layoutSettings.SetRowSpan(_entryShortLabel, 2);
      layoutSettings.SetCellPosition(_seMarket, new TableLayoutPanelCellPosition(52, 2));
      layoutSettings.SetColumnSpan(_seMarket, 15);
      layoutSettings.SetRowSpan(_seMarket, 1);
      layoutSettings.SetCellPosition(_seStop, new TableLayoutPanelCellPosition(52, 3));
      layoutSettings.SetColumnSpan(_seStop, 15);
      layoutSettings.SetRowSpan(_seStop, 1);
      layoutSettings.SetCellPosition(_seLimit, new TableLayoutPanelCellPosition(67, 2));
      layoutSettings.SetColumnSpan(_seLimit, 15);
      layoutSettings.SetRowSpan(_seLimit, 1);
      layoutSettings.SetCellPosition(_seStopLimit, new TableLayoutPanelCellPosition(67, 3));
      layoutSettings.SetColumnSpan(_seStopLimit, 15);
      layoutSettings.SetRowSpan(_seStopLimit, 1);

      layoutSettings.SetCellPosition(_advancedEntryLowLabel, new TableLayoutPanelCellPosition(4, 4));
      layoutSettings.SetColumnSpan(_advancedEntryLowLabel, 16);
      layoutSettings.SetRowSpan(_advancedEntryLowLabel, 1);
      layoutSettings.SetCellPosition(_advancedEntryLowPriceHost, new TableLayoutPanelCellPosition(21, 4));
      layoutSettings.SetColumnSpan(_advancedEntryLowPriceHost, 10);
      layoutSettings.SetRowSpan(_advancedEntryLowPriceHost, 1);
      layoutSettings.SetCellPosition(_advancedEntryHighPriceLabel, new TableLayoutPanelCellPosition(32, 4));
      layoutSettings.SetColumnSpan(_advancedEntryHighPriceLabel, 8);
      layoutSettings.SetRowSpan(_advancedEntryHighPriceLabel, 1);
      layoutSettings.SetCellPosition(_advancedEntryHighPriceHost, new TableLayoutPanelCellPosition(41, 4));
      layoutSettings.SetColumnSpan(_advancedEntryHighPriceHost, 10);
      layoutSettings.SetRowSpan(_advancedEntryHighPriceHost, 1);
      layoutSettings.SetCellPosition(_advancedEntryStopLossLabel, new TableLayoutPanelCellPosition(52, 4));
      layoutSettings.SetColumnSpan(_advancedEntryStopLossLabel, 8);
      layoutSettings.SetRowSpan(_advancedEntryStopLossLabel, 1);
      layoutSettings.SetCellPosition(_advancedEntryStopLossPriceHost, new TableLayoutPanelCellPosition(60, 4));
      layoutSettings.SetColumnSpan(_advancedEntryStopLossPriceHost, 10);
      layoutSettings.SetRowSpan(_advancedEntryStopLossPriceHost, 1);
      layoutSettings.SetCellPosition(_advancedEntryBreakout, new TableLayoutPanelCellPosition(71, 4));
      layoutSettings.SetColumnSpan(_advancedEntryBreakout, 12);
      layoutSettings.SetRowSpan(_advancedEntryBreakout, 1);
      layoutSettings.SetCellPosition(_advancedEntryFade, new TableLayoutPanelCellPosition(80, 4));
      layoutSettings.SetColumnSpan(_advancedEntryFade, 12);
      layoutSettings.SetRowSpan(_advancedEntryFade, 1);

      layoutSettings.SetCellPosition(entryExitSeparator, new TableLayoutPanelCellPosition(96, 0));
      layoutSettings.SetColumnSpan(entryExitSeparator, 1);
      layoutSettings.SetRowSpan(entryExitSeparator, layoutSettings.RowCount);

      layoutSettings.SetCellPosition(_exitLabel, new TableLayoutPanelCellPosition(97, 0));
      layoutSettings.SetColumnSpan(_exitLabel, 3);
      layoutSettings.SetRowSpan(_exitLabel, layoutSettings.RowCount);
      layoutSettings.SetCellPosition(_exitPositionLabel, new TableLayoutPanelCellPosition(101, 0));
      layoutSettings.SetColumnSpan(_exitPositionLabel, 10);
      layoutSettings.SetRowSpan(_exitPositionLabel, 1);
      layoutSettings.SetCellPosition(_positionLabel, new TableLayoutPanelCellPosition(111, 0));
      layoutSettings.SetColumnSpan(_positionLabel, 30);
      layoutSettings.SetRowSpan(_positionLabel, 1);
      layoutSettings.SetCellPosition(exitAllHost, new TableLayoutPanelCellPosition(111, 1));
      layoutSettings.SetColumnSpan(exitAllHost, 20);
      layoutSettings.SetRowSpan(exitAllHost, 1);
      layoutSettings.SetCellPosition(_exitSizeLabel, new TableLayoutPanelCellPosition(130, 1));
      layoutSettings.SetColumnSpan(_exitSizeLabel, 10);
      layoutSettings.SetRowSpan(_exitSizeLabel, 1);
      layoutSettings.SetCellPosition(exitSizeHost, new TableLayoutPanelCellPosition(140, 1));
      layoutSettings.SetColumnSpan(exitSizeHost, 10);
      layoutSettings.SetRowSpan(exitSizeHost, 1);
      layoutSettings.SetCellPosition(_exitStopLabel, new TableLayoutPanelCellPosition(101, 2));
      layoutSettings.SetColumnSpan(_exitStopLabel, 10);
      layoutSettings.SetRowSpan(_exitStopLabel, 1);
      layoutSettings.SetCellPosition(exitStopPriceHost, new TableLayoutPanelCellPosition(111, 2));
      layoutSettings.SetColumnSpan(exitStopPriceHost, 10);
      layoutSettings.SetRowSpan(exitStopPriceHost, 1);
      layoutSettings.SetCellPosition(_exitLimitLabel, new TableLayoutPanelCellPosition(131, 2));
      layoutSettings.SetColumnSpan(_exitLimitLabel, 10);
      layoutSettings.SetRowSpan(_exitLimitLabel, 1);
      layoutSettings.SetCellPosition(exitLimitPriceHost, new TableLayoutPanelCellPosition(141, 2));
      layoutSettings.SetColumnSpan(exitLimitPriceHost, 10);
      layoutSettings.SetRowSpan(exitLimitPriceHost, 1);

      layoutSettings.SetCellPosition(_exitMarket, new TableLayoutPanelCellPosition(152, 1));
      layoutSettings.SetColumnSpan(_exitMarket, 15);
      layoutSettings.SetRowSpan(_exitMarket, 1);
      layoutSettings.SetCellPosition(_exitStop, new TableLayoutPanelCellPosition(152, 2));
      layoutSettings.SetColumnSpan(_exitStop, 15);
      layoutSettings.SetRowSpan(_exitStop, 1);
      layoutSettings.SetCellPosition(_exitLimit, new TableLayoutPanelCellPosition(169, 1));
      layoutSettings.SetColumnSpan(_exitLimit, 15);
      layoutSettings.SetRowSpan(_exitLimit, 1);
      layoutSettings.SetCellPosition(_exitStopLimit, new TableLayoutPanelCellPosition(169, 2));
      layoutSettings.SetColumnSpan(_exitStopLimit, 15);
      layoutSettings.SetRowSpan(_exitStopLimit, 1);

      layoutSettings.SetCellPosition(_advancedExitLowLabel, new TableLayoutPanelCellPosition(101, 3));
      layoutSettings.SetColumnSpan(_advancedExitLowLabel, 16);
      layoutSettings.SetRowSpan(_advancedExitLowLabel, 1);
      layoutSettings.SetCellPosition(_advancedExitLowPriceHost, new TableLayoutPanelCellPosition(118, 3));
      layoutSettings.SetColumnSpan(_advancedExitLowPriceHost, 10);
      layoutSettings.SetRowSpan(_advancedExitLowPriceHost, 1);
      layoutSettings.SetCellPosition(_advancedExitHighPriceLabel, new TableLayoutPanelCellPosition(129, 3));
      layoutSettings.SetColumnSpan(_advancedExitHighPriceLabel, 8);
      layoutSettings.SetRowSpan(_advancedExitHighPriceLabel, 1);
      layoutSettings.SetCellPosition(_advancedExitHighPriceHost, new TableLayoutPanelCellPosition(138, 3));
      layoutSettings.SetColumnSpan(_advancedExitHighPriceHost, 10);
      layoutSettings.SetRowSpan(_advancedExitHighPriceHost, 1);
      layoutSettings.SetCellPosition(_advancedExitBracket, new TableLayoutPanelCellPosition(149, 3));
      layoutSettings.SetColumnSpan(_advancedExitBracket, 15);
      layoutSettings.SetRowSpan(_advancedExitBracket, 1);
      #endregion

      tb.ResumeLayout();
    }

    //Once-off operation to initialize the price inputs to the given price.
    private void initPriceInputs(double price) {
      ChartToolBar.AccessToolBarAsync(tb =>
      {
        _entryStopLossPrice.Value = (decimal)price;
        _entryLimitPrice.Value = (decimal)price;
        _entryStopPrice.Value = (decimal)price;
        _advancedEntryLowPrice.Value = (decimal)price;
        _advancedEntryHighPrice.Value = (decimal)price;
        _exitStopPrice.Value = (decimal)price;
        _exitLimitPrice.Value = (decimal)price;
      });
      initializePriceLevels = false;
    }

    private void onPositionLabelClick(object sender, EventArgs ev)
    {
      _exitPosition = true;
    }

    private void setProfitLoss(int mp, double opl)
    {
      if (_positionLabel == null) return;

      ChartToolBar.AccessToolBarAsync(tb =>
      {
        _positionLabel.Enabled = 0 != mp;

        string _mp_string;
        if (0 < mp)
          _mp_string = mp.ToString() + " Long";
        else if (0 > mp)
          _mp_string = (-mp).ToString() + " Short";
        else
          _mp_string = "Flat";
        _positionLabel.Text = string.Format("{0} {1}", _mp_string, opl.ToString("C"));

        if (opl == 0)
          _positionLabel.BackColor = Control.DefaultBackColor;
        else if (opl > 0)
          _positionLabel.BackColor = Color.LawnGreen;
        else
          _positionLabel.BackColor = Color.OrangeRed;
      });
    }
    #endregion

    #region utility functions
    //Entry order button handler.
    private void onEntryButton(EntryOrder entryOrder)
    {
      //if order action/category is toggled by user cancel the order opertion or
      //refresh the order operation to the new settings
      _previousEntryOrder = _entryOrder;

      //toggle the entry order action and order category
      if (_entryOrder == entryOrder)
        _entryOrder = EntryOrder.None;
      else
        _entryOrder = entryOrder;

      if (Debug) Output.WriteLine("onEntryButton - previous({0}) new({1})", _previousEntryOrder, _entryOrder);
    }

    //Exit order handler used to toggle the state of an exit button if it is the current selected button, if it
    //is not the current selected button it's state would be changed to that button.
    private void onExitButton(ExitOrder exitOrder)
    {
      //save previous exit order  
      _previousExitOrder = _exitOrder;

      //toggle exit order if required
      if (_exitOrder == exitOrder)
        _exitOrder = ExitOrder.None;
      else
        _exitOrder = exitOrder;

      if (Debug) Output.WriteLine("onExitButton - previous({0}) new({1})", _previousExitOrder, _exitOrder);
    }

    //Toggles the checked state of the given entry button and resets the checked state of all other buttons.
    private void refreshEntryOrderButton(ref ToolStripButton refreshedButton)
    {
      refreshedButton.Checked = !refreshedButton.Checked;

      //never toggle the state of market orders since they are always filled
      _leMarket.Checked = false;
      _seMarket.Checked = false;

      //toggle state of the non-market orders since they'll persist until cancelled
      if (refreshedButton != _leStop) _leStop.Checked = false;
      if (refreshedButton != _leLimit) _leLimit.Checked = false;
      if (refreshedButton != _leStopLimit) _leStopLimit.Checked = false;
      if (refreshedButton != _seStop) _seStop.Checked = false;
      if (refreshedButton != _seLimit) _seLimit.Checked = false;
      if (refreshedButton != _seStopLimit) _seStopLimit.Checked = false;
      if (refreshedButton != _advancedEntryBreakout) _advancedEntryBreakout.Checked = false;
      if (refreshedButton != _advancedEntryFade) _advancedEntryFade.Checked = false;
    }

    //Toggles the checked state of the given exit button and resets the checked state of all other buttons.
    private void refreshExitOrderButton(ref ToolStripButton refreshedButton)
    {
      refreshedButton.Checked = !refreshedButton.Checked;

      //never toggle the state of market orders since they are always filled
      _exitMarket.Checked = false;

      //toggle state of the non-market orders since they'll persist until cancelled
      if (refreshedButton != _exitStop) _exitStop.Checked = false;
      if (refreshedButton != _exitLimit) _exitLimit.Checked = false;
      if (refreshedButton != _exitStopLimit) _exitStopLimit.Checked = false;
      if (refreshedButton != _advancedExitBracket) _advancedExitBracket.Checked = false;
    }

    //Refresh the order buttons based on specific order action and category.
    private void refreshEntryOrderButtons(EntryOrder entryOrder)
    {
      ChartToolBar.AccessToolBarAsync(tb =>
      {
        switch (entryOrder)
        {
          case EntryOrder.LongMarket:
            if (Debug) Output.WriteLine("refreshButton - LE market");
            refreshEntryOrderButton(ref _leMarket);
            break;
          case EntryOrder.LongStop:
            if (Debug) Output.WriteLine("refreshButton - LE stop");
            refreshEntryOrderButton(ref _leStop);
            break;
          case EntryOrder.LongLimit:
            if (Debug) Output.WriteLine("refreshButton - LE limit");
            refreshEntryOrderButton(ref _leLimit);
            break;
          case EntryOrder.LongStopLimit:
            if (Debug) Output.WriteLine("refreshButton - LE stop-limit");
            refreshEntryOrderButton(ref _leStopLimit);
            break;
          case EntryOrder.ShortMarket:
            if (Debug) Output.WriteLine("refreshButton - SE market");
            refreshEntryOrderButton(ref _seMarket);
            break;
          case EntryOrder.ShortStop:
            if (Debug) Output.WriteLine("refreshButton - SE stop");
            refreshEntryOrderButton(ref _seStop);
            break;
          case EntryOrder.ShortLimit:
            if (Debug) Output.WriteLine("refreshButton - SE limit");
            refreshEntryOrderButton(ref _seLimit);
            break;
          case EntryOrder.ShortStopLimit:
            if (Debug) Output.WriteLine("refreshButton - SE stop-limit");
            refreshEntryOrderButton(ref _seStopLimit);
            break;
          case EntryOrder.Breakout:
            if (Debug) Output.WriteLine("refreshButton - Breakout");
            refreshEntryOrderButton(ref _advancedEntryBreakout);
            break;
          case EntryOrder.Fade:
            if (Debug) Output.WriteLine("refreshButton - Fade");
            refreshEntryOrderButton(ref _advancedEntryFade);
            break;
          case EntryOrder.None:
            //reset entry order button checked state if entry order is none
            _leStop.Checked = false;
            _leLimit.Checked = false;
            _leStopLimit.Checked = false;
            _seStop.Checked = false;
            _seLimit.Checked = false;
            _seStopLimit.Checked = false;
            _advancedEntryBreakout.Checked = false;
            _advancedEntryFade.Checked = false;
            break;
        }
      });
    }

    //Refresh the order buttons based on specific order action and category.
    private void refreshExitOrderButtons(ExitOrder exitOrder)
    {
      ChartToolBar.AccessToolBarAsync(tb =>
      {
        switch (exitOrder)
        {
          case ExitOrder.Market:
            if (Debug) Output.WriteLine("refreshButton - LX/SX market");
            refreshExitOrderButton(ref _exitMarket);
            break;
          case ExitOrder.Stop:
            if (Debug) Output.WriteLine("refreshButton - LX/SX stop");
            refreshExitOrderButton(ref _exitStop);
            break;
          case ExitOrder.Limit:
            if (Debug) Output.WriteLine("refreshButton - LX/SX limit");
            refreshExitOrderButton(ref _exitLimit);
            break;
          case ExitOrder.StopLimit:
            if (Debug) Output.WriteLine("refreshButton - LX/SX stop-limit");
            refreshExitOrderButton(ref _exitStopLimit);
            break;
          case ExitOrder.Bracket:
            if (Debug) Output.WriteLine("refreshButton - bracket");
            refreshExitOrderButton(ref _advancedExitBracket);
            break;
          case ExitOrder.None:
            //reset exit order button checked state if no exit order being placed
            _exitStop.Checked = false;
            _exitLimit.Checked = false;
            _exitStopLimit.Checked = false;
            _advancedExitBracket.Checked = false;
            break;
        }
      });
    }

    //Refresh the order buttons if a state change occurs in the order action or category.
    private void refreshOrderButtons()
    {
      if (_previousEntryOrder != _entryOrder)
      {
        if (Debug) Output.WriteLine("refreshOrderButtons entry state change - previous({0}) current({1})", _previousEntryOrder, _entryOrder);
        refreshEntryOrderButtons(_entryOrder);
        _previousEntryOrder = _entryOrder;
      }

      if (_previousExitOrder != _exitOrder)
      {
        if (Debug) Output.WriteLine("refreshOrderButtons exit state change - previous({0}) current({1})", _previousExitOrder, _exitOrder);
        refreshExitOrderButtons(_exitOrder);
        _previousExitOrder = _exitOrder;
      }
    }

    //Enable/disable the exit size label and price based on whether we're exiting the whole position.
    private void exitAll_CheckStateChanged(Object sender, EventArgs e)
    {
      if (_exitAll.Checked)
      {
        _exitSizeLabel.ForeColor = SystemColors.GrayText;
        _exitSize.Enabled = false;
      }
      else
      {
        _exitSizeLabel.ForeColor = SystemColors.ActiveCaptionText;
        _exitSize.Enabled = true;
        _exitSize.Value = (decimal)Math.Abs(StrategyInfo.MarketPosition);
      }
    }

    //Updates the entry size and stop-loss entry price based on the selected size strategy.
    private void entrySizeStrategy_SelectedIndexChanged(Object sender, EventArgs e)
    {
      switch (_entrySizeStrategy.SelectedIndex)
      {
        case 0:
          _entrySizeLabel.Text = "Size";
          _entrySize.DecimalPlaces = 0;
          _entrySize.Minimum = 1;
          _entrySize.Maximum = Decimal.MaxValue;
          _entrySize.Increment = 1;
          _entrySize.Value = (decimal)DefaultSize;

          //disable stop loss since it's not applicable
          _entryStopLossLabel.ForeColor = SystemColors.GrayText;
          _entryStopLossPrice.Enabled = false;
          _advancedEntryStopLossLabel.ForeColor = SystemColors.GrayText;
          _advancedEntryStopLossPrice.Enabled = false;
          break;
        case 1:
          _entrySizeLabel.Text = "Percent";
          _entrySize.DecimalPlaces = 2;
          _entrySize.Minimum = (decimal)0.01;
          _entrySize.Maximum = (decimal)1.0;
          _entrySize.Increment = (decimal)0.01;
          _entrySize.Value = (decimal)DefaultSizePercent;

          //enable stop loss for user to enter the stop price
          _entryStopLossLabel.ForeColor = SystemColors.ActiveCaptionText;
          _entryStopLossPrice.Enabled = true;
          _entryStopLossPrice.Value = (decimal)(Math.Truncate(Bars.Close[0] / PriceIncrement) * PriceIncrement);  //round value to nearest price increment
          _advancedEntryStopLossLabel.ForeColor = SystemColors.ActiveCaptionText;
          _advancedEntryStopLossPrice.Enabled = true;
          _advancedEntryStopLossPrice.Value = (decimal)(Math.Truncate((Bars.Close[0] * (double)_entrySize.Value) / PriceIncrement) * PriceIncrement); //round to percentage of close price and nearest price increment value
          break;
      }
    }

    //Updates the advanced stop-loss price value if a risk percentage entry is used.
    private void entrySize_ValueChanged(Object sender, EventArgs e) {
      if (_entrySizeStrategy.SelectedIndex == 1) {
        _advancedEntryStopLossPrice.Value = (decimal)(Math.Truncate((Bars.Close[0] * (double)_entrySize.Value) / PriceIncrement) * PriceIncrement); //round to percentage of close price and nearest price increment value
      }
    }

    //Handle exit orders.
    private void handleExitOrders()
    {
      //flatten position if required
      if (_exitPosition)
      {
        _exitPosition = false;
        if (StrategyInfo.MarketPosition > 0)
        {
          if (Debug) Output.Write("marketLX({0}) - Exit ", StrategyInfo.MarketPosition);
          _marketLX.Send(StrategyInfo.MarketPosition);
        }
        else if (StrategyInfo.MarketPosition < 0)
        {
          if (Debug) Output.Write("marketSX({0}) - Exit ", StrategyInfo.MarketPosition);
          _marketSX.Send(Math.Abs(StrategyInfo.MarketPosition));
        }

        onExitButton(ExitOrder.None);  //reset the exit order button states
      }

      //handle exit orders
      if (StrategyInfo.MarketPosition > 0)
      {
        switch (_exitOrder)
        {
          case ExitOrder.Market:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("marketLX({0}) - All ", StrategyInfo.MarketPosition);
              _marketLX.Send(StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("marketLX({0}) ", (int)_exitSize.Value);
              _marketLX.Send((int)_exitSize.Value);
            }
            onExitButton(ExitOrder.None);
            break;
          case ExitOrder.Stop:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("stopLX({0:C}, {1}) - All ", (double)_exitStopPrice.Value, StrategyInfo.MarketPosition);
              _stopLX.Send((double)_exitStopPrice.Value, StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("stopLX({0:C}, {1}) ", (double)_exitStopPrice.Value, (int)_exitSize.Value);
              _stopLX.Send((double)_exitStopPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.Limit:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("limitLX({0:C}, {1}) - All ", (double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
              _limitLX.Send((double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("limitLX({0:C}, {1}) ", (double)_exitLimitPrice.Value, (int)_exitSize.Value);
              _limitLX.Send((double)_exitLimitPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.StopLimit:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("stopLimitLX({0:C}, {1:C}, {2}) - All ", (double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
              _stopLimitLX.Send((double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("stopLimitLX({0:C}, {1:C}, {2}) ", (double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, (int)_exitSize.Value);
              _stopLimitLX.Send((double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.Bracket:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("bracketLX({0:C}, {1:C}, {2}) - All ", (double)_advancedExitLowPrice.Value, (double)_advancedExitHighPrice.Value, StrategyInfo.MarketPosition);
              _stopLX.Send((double)_advancedExitLowPrice.Value, StrategyInfo.MarketPosition);
              _limitLX.Send((double)_advancedExitHighPrice.Value, StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("bracketLX({0:C}, {1:C}, {2})", (double)_advancedExitLowPrice.Value, (double)_advancedExitHighPrice.Value, (int)_exitSize.Value);
              _stopLX.Send((double)_advancedExitLowPrice.Value, (int)_exitSize.Value);
              _limitLX.Send((double)_advancedExitHighPrice.Value, (int)_exitSize.Value);
            }
            break;
        }
      }
      else if (StrategyInfo.MarketPosition < 0)
      {
        switch (_exitOrder)
        {
          case ExitOrder.Market:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("marketSX({0}) - All ", StrategyInfo.MarketPosition);
              _marketSX.Send(Math.Abs(StrategyInfo.MarketPosition));
              onExitButton(0);
            }
            else
            {
              if (Debug) Output.Write("marketSX({0}) ", (int)_exitSize.Value);
              _marketSX.Send((int)_exitSize.Value);
            }
            break;
          case ExitOrder.Stop:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("stopSX({0:C}, {1}) - All ", (double)_exitStopPrice.Value, StrategyInfo.MarketPosition);
              _stopSX.Send((double)_exitStopPrice.Value, Math.Abs(StrategyInfo.MarketPosition));
            }
            else
            {
              if (Debug) Output.Write("stopSX({0:C}, {1}) ", (double)_exitStopPrice.Value, (int)_exitSize.Value);
              _stopSX.Send((double)_exitStopPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.Limit:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("limitSX({0:C}, {1}) - All ", (double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
              _limitSX.Send((double)_exitLimitPrice.Value, Math.Abs(StrategyInfo.MarketPosition));
            }
            else
            {
              if (Debug) Output.Write("limitSX({0:C}, {1}) ", (double)_exitLimitPrice.Value, (int)_exitSize.Value);
              _limitSX.Send((double)_exitLimitPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.StopLimit:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("stopLimitSX({0:C}, {1:C}, {2}) - All ", (double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, StrategyInfo.MarketPosition);
              _stopLimitSX.Send((double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, Math.Abs(StrategyInfo.MarketPosition));
            }
            else
            {
              if (Debug) Output.Write("stopLimitSX({0:C}, {1:C}, {2}) ", (double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, (int)_exitSize.Value);
              _stopLimitSX.Send((double)_exitStopPrice.Value, (double)_exitLimitPrice.Value, (int)_exitSize.Value);
            }
            break;
          case ExitOrder.Bracket:
            if (_exitAll.Checked)
            {
              if (Debug) Output.Write("bracketSX({0:C}, {1:C}, {2}) - All ", (double)_advancedExitLowPrice.Value, (double)_advancedExitHighPrice.Value, StrategyInfo.MarketPosition);
              _stopSX.Send((double)_advancedExitHighPrice.Value, StrategyInfo.MarketPosition);
              _limitSX.Send((double)_advancedExitLowPrice.Value, StrategyInfo.MarketPosition);
            }
            else
            {
              if (Debug) Output.Write("bracketSX({0:C}, {1:C}, {2})", (double)_advancedExitHighPrice.Value, (double)_advancedExitLowPrice.Value, (int)_exitSize.Value);
              _stopSX.Send((double)_advancedExitHighPrice.Value, (int)_exitSize.Value);
              _limitSX.Send((double)_advancedExitLowPrice.Value, (int)_exitSize.Value);
            }
            break;

        }
      }
    }

    //Checks the entry condition drop down and returns whether the entry condition is met before sending an order.
    private bool isEntryConditionMet()
    {
      switch (_entryCondition.SelectedIndex)
      {
        case 0: //None
          return true;
        case 1: //Above Average
          if (Debug) Output.WriteLine("Condition(above average, {0}) ", Bars.Volume[0] > _volumeWithSD.Average[0]);
          return Bars.Volume[0] > _volumeWithSD.Average[0];
        case 2: //Above Average + 1SD
          if (Debug) Output.WriteLine("Condition(above average + 1SD, {0}) ", Bars.Volume[0] > _volumeWithSD.AvgPlusLevel1[0]);
          return Bars.Volume[0] > _volumeWithSD.AvgPlusLevel1[0];
        case 3: //Above Average + 2SD
          if (Debug) Output.WriteLine("Condition(above average + 2SD, {0}) ", Bars.Volume[0] > _volumeWithSD.AvgPlusLevel2[0]);
          return Bars.Volume[0] > _volumeWithSD.AvgPlusLevel2[0];
        case 4: //Above Average + 3SD
          if (Debug) Output.WriteLine("Condition(above average + 3SD, {0}) ", Bars.Volume[0] > _volumeWithSD.AvgPlusLevel3[0]);
          return Bars.Volume[0] > _volumeWithSD.AvgPlusLevel3[0];
        case 5: //Rising volume
          //can only check rising volume if we have enough bars
          if ((RisingVolumeLookback + 1) < Bars.CurrentBar)
          {
            if (Debug) Output.WriteLine("Condition(rising volume, 0) ");
            return false;
          }
          //check counter condition of rising volumne - return false as soon as a bar is found
          //where the previous bar has volume greater or equal to the bar being checked
          for (var i = RisingVolumeLookback; i >= 0; i--)
            if (Bars.Volume[i + 1] >= Bars.Volume[i])
            {
              if (Debug) Output.WriteLine("Condition(rising volume, 0) ");
              return false;
            }

          //last RisingVolumeLookback bars have rising volumne
          if (Debug) Output.Write("Condition(rising volume, true) ");
          return true;
        default:
          if (Debug) Output.Write("WARNING: Unknown entry condition encountered. ");
          return false; //some unknown entry condition
      }
    }

    //Determines the entry size based on the entry size strategy.
    private int entrySize()
    {
      switch (_entrySizeStrategy.SelectedIndex)
      {
        case 0:
          return (int)_entrySize.Value;
          break;
        case 1:
          double riskEquity = (InitialCapital + NetProfit) * (double)_entrySize.Value;
          double riskPerShare = 0.0;

          //compute risk per share based on "normal" entry order vs advanced entry order
          if (_entryOrder == EntryOrder.Breakout || _entryOrder == EntryOrder.Fade)
            riskPerShare = (double)_advancedEntryStopLossPrice.Value;
          else
            riskPerShare = Math.Abs(Bars.Close[0] - (double)_entryStopLossPrice.Value);

          if (riskPerShare <= 0)
          {
            if (Debug) Output.WriteLine("Error: riskPerShare is negative or zero so no entry size can be computed.");
            return 0;
          }
          else
          {
            return (int)Math.Truncate(riskEquity / riskPerShare);
          }
          break;
        default:
          return 0;
      }
    }

    //Handle entry orders.
    private void handleEntryOrders()
    {
      switch (_entryOrder)
      {
        case EntryOrder.LongMarket:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("marketLE({0}) ", entrySize());
            _marketLE.Send(entrySize());
            onEntryButton(EntryOrder.None);  //market entry need to reset order action and category
          }
          break;
        case EntryOrder.LongStop:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("stopLE({0:C}, {1}) ", (double)_entryStopPrice.Value, entrySize());
            _stopLE.Send((double)_entryStopPrice.Value, entrySize());
          }
          break;
        case EntryOrder.LongLimit:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("limitLE({0:C}, {1}) ", (double)_entryLimitPrice.Value, entrySize());
            _limitLE.Send((double)_entryLimitPrice.Value, entrySize());
          }
          break;
        case EntryOrder.LongStopLimit:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("stopLimitLE({0:C}, {1:C}, {2})", (double)_entryStopPrice.Value, (double)_entryLimitPrice.Value, entrySize());
            _stopLimitLE.Send((double)_entryStopPrice.Value, (double)_entryLimitPrice.Value, entrySize());
          }
          break;

        case EntryOrder.ShortMarket:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("marketSE({0}) ", entrySize());
            _marketSE.Send(entrySize());
            onEntryButton(EntryOrder.None);  //market entry need to reset order action and category
          }
          break;
        case EntryOrder.ShortStop:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("stopLE({0:C}, {1}) ", (double)_entryStopPrice.Value, entrySize());
            _stopSE.Send((double)_entryStopPrice.Value, entrySize());
          }
          break;
        case EntryOrder.ShortLimit:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("limitSE({0:C}, {1}) ", (double)_entryLimitPrice.Value, entrySize());
            _limitSE.Send((double)_entryLimitPrice.Value, entrySize());
          }
          break;
        case EntryOrder.ShortStopLimit:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("stopLimitSE({0:C}, {1:C}, {2})", (double)_entryStopPrice.Value, (double)_entryLimitPrice.Value, entrySize());
            _stopLimitSE.Send((double)_entryStopPrice.Value, (double)_entryLimitPrice.Value, entrySize());
          }
          break;
        case EntryOrder.Breakout:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("breakout({0:C}, {1:C}, {2})", (double)_advancedEntryLowPrice.Value, (double)_advancedEntryHighPrice.Value, entrySize());
            _stopLE.Send((double)_advancedEntryHighPrice.Value, entrySize());
            _stopSE.Send((double)_advancedEntryLowPrice.Value, entrySize());
          }
          break;
        case EntryOrder.Fade:
          if (isEntryConditionMet())
          {
            if (Debug) Output.WriteLine("fade({0:C}, {1:C}, {2})", (double)_advancedEntryLowPrice.Value, (double)_advancedEntryHighPrice.Value, entrySize());
            _limitSE.Send((double)_advancedEntryHighPrice.Value, entrySize());
            _limitLE.Send((double)_advancedEntryLowPrice.Value, entrySize());
          }
          break;
      }
    }

    //Performs the default processing for CalcBar, typically handles order placement since it
    //needs to be synchronous with the calculation engine.
    private void calcBarDefault()
    {
      handleExitOrders();
      handleEntryOrders();
    }

    //Update the enabled state of the exit controls.
    private void setExitEnabledState(bool enabled)
    {
      ChartToolBar.AccessToolBar(tb =>
      {
        //determine whether this is the first enable after an entry and set the stop/limit
        //price values for exit to some sane values
        bool firstEnable = !_exitAll.Enabled && enabled;

        _exitAll.Enabled = enabled;
        _exitSize.Enabled = enabled && !_exitAll.Checked;
        _exitStopPrice.Enabled = enabled;
        _exitLimitPrice.Enabled = enabled;
        _exitMarket.Enabled = enabled;
        _exitStop.Enabled = enabled;
        _exitLimit.Enabled = enabled;
        _exitStopLimit.Enabled = enabled;
        _advancedExitLowPrice.Enabled = enabled;
        _advancedExitHighPrice.Enabled = enabled;
        _advancedExitBracket.Enabled = enabled;

        if (enabled)
        {
          _exitLabel.ForeColor = SystemColors.ActiveCaptionText;
          _exitPositionLabel.ForeColor = SystemColors.ActiveCaptionText;
          _exitStopLabel.ForeColor = SystemColors.ActiveCaptionText;
          _exitLimitLabel.ForeColor = SystemColors.ActiveCaptionText;
          _advancedExitLowLabel.ForeColor = SystemColors.ActiveCaptionText;
          _advancedExitHighPriceLabel.ForeColor = SystemColors.ActiveCaptionText;

          if (_exitAll.Checked)
            _exitSizeLabel.ForeColor = SystemColors.GrayText;
          else
            _exitSizeLabel.ForeColor = SystemColors.ActiveCaptionText;

          if (StrategyInfo.MarketPosition > 0) {
            _exitMarket.BackColor = SHORT_BUTTON_COLOR;
            _exitStop.BackColor = SHORT_BUTTON_COLOR;
            _exitLimit.BackColor = SHORT_BUTTON_COLOR;
            _exitStopLimit.BackColor = SHORT_BUTTON_COLOR;
          } 
          else
          {
            _exitMarket.BackColor = LONG_BUTTON_COLOR;
            _exitStop.BackColor = LONG_BUTTON_COLOR;
            _exitLimit.BackColor = LONG_BUTTON_COLOR;
            _exitStopLimit.BackColor = LONG_BUTTON_COLOR;
          }

          _advancedExitBracket.BackColor = ADVANCED_BUTTON_COLOR;
        }
        else
        {
          _exitLabel.ForeColor = SystemColors.GrayText;
          _exitPositionLabel.ForeColor = SystemColors.GrayText;
          _exitSizeLabel.ForeColor = SystemColors.GrayText;
          _exitStopLabel.ForeColor = SystemColors.GrayText;
          _exitLimitLabel.ForeColor = SystemColors.GrayText;
          _advancedExitLowLabel.ForeColor = SystemColors.GrayText;
          _advancedExitHighPriceLabel.ForeColor = SystemColors.GrayText;

          _exitMarket.Checked = false;
          _exitMarket.BackColor = SystemColors.ButtonFace;
          _exitStop.Checked = false;
          _exitStop.BackColor = SystemColors.ButtonFace;
          _exitLimit.Checked = false;
          _exitLimit.BackColor = SystemColors.ButtonFace;
          _exitStopLimit.Checked = false;
          _exitStopLimit.BackColor = SystemColors.ButtonFace;
          _advancedExitBracket.Checked = false;
          _advancedExitBracket.BackColor = SystemColors.ButtonFace;
        }

        //set values for the stop/limit exit prices on first entry
        if (firstEnable)
        {
          _exitStopPrice.Value = (decimal)(Math.Truncate(Bars.Low.Lowest(ExitStopLimitDefaultPriceLookback) / PriceIncrement) * PriceIncrement);
          _advancedExitLowPrice.Value = _exitStopPrice.Value;
          _exitLimitPrice.Value = (decimal)(Math.Truncate(Bars.High.Highest(ExitStopLimitDefaultPriceLookback) / PriceIncrement) * PriceIncrement);
          _advancedExitHighPrice.Value = _exitLimitPrice.Value;
        }
      });
    }

    //Performs processing related to a market position change on CalcBar.
    private void calcBarMarketPositionChange()
    {
      if (Debug) Output.Write("MarketPositionChange({0}, {1})", _previousMarketPosition, StrategyInfo.MarketPosition);

      //update entry order state if an entry order was executed
      if ((StrategyInfo.MarketPosition > 0 && _previousMarketPosition < StrategyInfo.MarketPosition) || //we're long and position increased
          (StrategyInfo.MarketPosition < 0 && _previousMarketPosition > StrategyInfo.MarketPosition))   //we're short and short position was increased (became more negative)
      {
        _previousEntryOrder = _entryOrder;
        _entryOrder = EntryOrder.None;
      } 

      //update exit order state if an exit order was executed or we became market neutral
      if ((StrategyInfo.MarketPosition > 0 && _previousMarketPosition > StrategyInfo.MarketPosition) || //we're long and position decreased
          (StrategyInfo.MarketPosition < 0 && _previousMarketPosition < StrategyInfo.MarketPosition) || //we're short and short position was decreased (became more positive)
          (StrategyInfo.MarketPosition == 0)) //we're market neutral   
      {
        _previousExitOrder = _exitOrder;
        _exitOrder = ExitOrder.None;
      } 

      //update exit controls enabled state
      setExitEnabledState(StrategyInfo.MarketPosition != 0);

      //update market position
      _previousMarketPosition = StrategyInfo.MarketPosition;
    }
    #endregion

    #region custom order rendering
    void renderOrder(DrawContext context, string orderText, double price, Brush brush, Pen pen)
    {
      //compute start and end of order line
      float orderLineLength = context.FullRect.Width * ORDER_LINE_LENGTH_PERCENT;
      ChartPoint priceChartPoint = new ChartPoint(Environment.RightScreenTime, price);
      PointF startPoint = new PointF(context.FullRect.Right - orderLineLength, context.Environment.ChartPoint2Point(priceChartPoint).Y);
      PointF endPoint = new PointF(context.FullRect.Right, startPoint.Y);

      //compute text rectangle
      SizeF textSize = context.graphics.MeasureString(orderText, context.StatusLineFont);
      RectangleF textRect = new RectangleF(startPoint.X - textSize.Width, startPoint.Y - (textSize.Height / 2), textSize.Width, textSize.Height);

      //compute rectangle of area where order will be rendered
      RectangleF orderRect = new RectangleF(textRect.X, textRect.Y, textRect.Width + orderLineLength, textRect.Height);

      //render components if they coincide with the dirty rectangle 
      if (context.DirtyRect.IntersectsWith(orderRect))
      {
        context.graphics.DrawLine(pen, startPoint, endPoint);
        context.graphics.DrawString(orderText, context.StatusLineFont, brush, textRect.Location);
      }
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      //only render order during the final drawing phase
      if (phase == EDrawPhases.Final)
      {
        using (Brush longSolidBrush = new SolidBrush(LE_COLOR))
        using (Brush shortSolidBrush = new SolidBrush(SE_COLOR))
        using (Pen longSolidPen = new Pen(LE_COLOR, ORDER_LINE_WIDTH))
        using (Pen shortSolidPen = new Pen(SE_COLOR, ORDER_LINE_WIDTH))
        using (Pen longDashPen = new Pen(LE_COLOR, ORDER_LINE_WIDTH))
        using (Pen shortDashPen = new Pen(SE_COLOR, ORDER_LINE_WIDTH))
        {
          float[] dashPattern = { DASH_LINE, DASH_SPACE };
          longDashPen.DashPattern = dashPattern;
          shortDashPen.DashPattern = dashPattern;

          //render entry orders
          switch (_entryOrder)
          {
            case EntryOrder.LongStop:
              renderOrder(context, "STP", (double)_entryStopPrice.Value, longSolidBrush, longSolidPen);
              break;
            case EntryOrder.LongLimit:
              renderOrder(context, "LMT", (double)_entryLimitPrice.Value, longSolidBrush, longSolidPen);
              break;
            case EntryOrder.LongStopLimit:
              renderOrder(context, "STP", (double)_entryStopPrice.Value, longSolidBrush, longDashPen);
              renderOrder(context, "LMT", (double)_entryLimitPrice.Value, longSolidBrush, longSolidPen);
              break;
            case EntryOrder.ShortStop:
              renderOrder(context, "STP", (double)_entryStopPrice.Value, shortSolidBrush, shortDashPen);
              break;
            case EntryOrder.ShortLimit:
              renderOrder(context, "LMT", (double)_entryLimitPrice.Value, shortSolidBrush, shortSolidPen);
              break;
            case EntryOrder.ShortStopLimit:
              renderOrder(context, "STP", (double)_entryStopPrice.Value, shortSolidBrush, shortDashPen);
              renderOrder(context, "LMT", (double)_entryLimitPrice.Value, shortSolidBrush, shortSolidPen);
              break;
            case EntryOrder.Breakout:
              renderOrder(context, "STP", (double)_advancedEntryLowPrice.Value, shortSolidBrush, shortSolidPen);
              renderOrder(context, "STP", (double)_advancedEntryHighPrice.Value, longSolidBrush, longSolidPen);
              break;
            case EntryOrder.Fade:
              renderOrder(context, "LMT", (double)_advancedEntryLowPrice.Value, longSolidBrush, longSolidPen);
              renderOrder(context, "LMT", (double)_advancedEntryHighPrice.Value, shortSolidBrush, shortSolidPen);
              break;
          }

          //render exit orders
          if (StrategyInfo.MarketPosition > 0)
          {
            switch (_exitOrder)
            {
              case ExitOrder.Stop:
                renderOrder(context, "STP", (double)_exitStopPrice.Value, shortSolidBrush, shortDashPen);
                break;
              case ExitOrder.Limit:
                renderOrder(context, "LMT", (double)_exitLimitPrice.Value, shortSolidBrush, shortSolidPen);
                break;
              case ExitOrder.StopLimit:
                renderOrder(context, "STP", (double)_exitStopPrice.Value, shortSolidBrush, shortDashPen);
                renderOrder(context, "LMT", (double)_exitLimitPrice.Value, shortSolidBrush, shortSolidPen);
                break;
              case ExitOrder.Bracket:
                renderOrder(context, "STP", (double)_advancedExitLowPrice.Value, shortSolidBrush, shortSolidPen);
                renderOrder(context, "LMT", (double)_advancedExitHighPrice.Value, shortSolidBrush, shortSolidPen);
                break;
            }
          }
          else if (StrategyInfo.MarketPosition < 0)
          {
            switch (_exitOrder)
            {
              case ExitOrder.Stop:
                renderOrder(context, "STP", (double)_exitStopPrice.Value, longSolidBrush, longDashPen);
                break;
              case ExitOrder.Limit:
                renderOrder(context, "LMT", (double)_exitLimitPrice.Value, longSolidBrush, longSolidPen);
                break;
              case ExitOrder.StopLimit:
                renderOrder(context, "STP", (double)_exitStopPrice.Value, longSolidBrush, longSolidPen);
                renderOrder(context, "LMT", (double)_exitLimitPrice.Value, longSolidBrush, longSolidPen);
                break;
              case ExitOrder.Bracket:
                renderOrder(context, "LMT", (double)_advancedExitLowPrice.Value, longSolidBrush, longSolidPen);
                renderOrder(context, "STP", (double)_advancedExitHighPrice.Value, longSolidBrush, longSolidPen);
                break;
            }
          }
        }
      }
    }
    #endregion

  }
}
