using System;

// Minimal MultiCharts shapes for the GX MC half. Names and signatures follow calls that already compile
// in _CustomSignal and the legacy exit studies. This is not the MultiCharts API.

namespace PowerLanguage
{
	[AttributeUsage(AttributeTargets.Property)]
	public class InputAttribute : Attribute { }

	public enum IOGMode { Inherited, Enabled, Disabled }

	[AttributeUsage(AttributeTargets.Class)]
	public class IOGModeAttribute : Attribute
	{
		public IOGModeAttribute(IOGMode mode) { }
	}

	public enum ETraceLevel { None, Error, Warning, Info, Journal, Verbose, Debug }
	public enum EBarState { None, Open, Inside, Close }
	public enum EApplicationCode { Charting, Portfolio, Scanner }
	public enum EResolution { Tick, Second, Minute, Hour, Day, Week, Month, Quarter, Year, Point, Volume }
	public enum EOrderAction { Buy, Sell, SellShort, BuyToCover }
	public enum Contracts { Default, UserSpecified }
	public enum OrderExit { FromAll, Total }
	public enum ESpecOrdersMode { PerPosition, PerContract }

	public struct Resolution
	{
		public EResolution Type;
		public uint Size;
		public string ChartType;
		public Resolution(EResolution type, int size) { Type = type; Size = (uint)size; ChartType = ""; }
		public TimeSpan Duration() { return TimeSpan.FromMinutes(Size); }
	}

	public class SOrderParameters
	{
		public SOrderParameters(Contracts contracts, string name, EOrderAction action) { }
		public SOrderParameters(Contracts contracts, string name, EOrderAction action, OrderExit exit) { }
	}

	public interface IOrderPriced
	{
		void Send(double price);
		void Send(double price, int contracts);
	}

	public interface IOrderMarket
	{
		void Send();
		void Send(int contracts);
	}

	public class OrderCreatorStub
	{
		public IOrderPriced Stop(SOrderParameters p) { return null; }
		public IOrderPriced Limit(SOrderParameters p) { return null; }
		public IOrderMarket MarketNextBar(SOrderParameters p) { return null; }
	}

	public class Series
	{
		public double this[int barsAgo] { get { return 0; } }
		public double Average(int length) { return 0; }
		public double Average(int length, int offset) { return 0; }
	}

	public class TimeSeries
	{
		public DateTime this[int barsAgo] { get { return DateTime.MinValue; } }
	}

	public class InstrumentSettings : IInstrumentSettings
	{
		public string Name { get; set; }
		public string Exchange { get; set; }
		public string SessionName { get; set; }
		public double BigPointValue { get; set; }
		public double MinMove { get; set; }
		public double PriceScale { get; set; }
		public Resolution Resolution { get; set; }
		public ESymbolCategory Category { get; set; }
		public RequestTimeZone TimeZone { get; set; }
		public string DataFeed { get; set; }
		public string BVO { get; set; }
		public ASymbolInfo2 ASymbolInfo2 { get; set; }
		public InstrumentSettings() { ASymbolInfo2 = new ASymbolInfo2(); Resolution = new Resolution(EResolution.Minute, 1); }
	}

	public class BarsStub : IInstrument
	{
		public EBarState Status { get; set; }
		public int CurrentBar { get; set; }
		public bool LastBarOnChart { get; set; }
		public Series High { get; set; }
		public Series Low { get; set; }
		public Series Close { get; set; }
		public Series Open { get; set; }
		public Series Volume { get; set; }
		public Series UpTicks { get; set; }
		public Series DownTicks { get; set; }
		public TimeSeries Time { get; set; }
		public InstrumentSettings Info { get; set; }
		public FullSymbolData FullSymbolData { get; set; }
		public InstrumentDataRequest Request { get; set; }
		public IROList<SessionObject> Sessions { get; set; }
		IInstrumentSettings IInstrument.Info { get { return Info; } }
		public BarsStub()
		{
			High = new Series(); Low = new Series(); Close = new Series(); Open = new Series(); Volume = new Series();
			UpTicks = new Series(); DownTicks = new Series();
			Time = new TimeSeries(); Info = new InstrumentSettings(); FullSymbolData = new FullSymbolData();
			Request = new InstrumentDataRequest(); Sessions = new IROList<SessionObject>();
		}
	}

	public class StrategyInfoStub
	{
		public int MarketPosition;
		public double AvgEntryPrice;
	}

	public class PositionStub : IMarketPosition
	{
		public int OpenLots { get; set; }
		public double OpenProfit { get; set; }
		public bool IsLong { get; set; }
		public int OpenTradesCount { get; set; }
		public System.Collections.Generic.IList<ITrade> OpenTrades { get; set; }
		public System.Collections.Generic.IList<ITrade> ClosedTrades { get; set; }
		public double MaxRunUp { get; set; }
		public double MaxDrawDown { get; set; }
		public EMarketPositionSide Side { get; set; }
		public PositionStub()
		{
			OpenTrades = new System.Collections.Generic.List<ITrade>();
			ClosedTrades = new System.Collections.Generic.List<ITrade>();
		}
	}

	public class AppEnv
	{
		public EApplicationCode ApplicationCode;
		public bool IsRealTimeCalc;
		public bool IsAutoTradingMode;
		public CalculationReason CalcReason;
	}

	public enum CalculationReason { Default, MarketPositionChange }
	public enum RequestTimeZone { Local, GMT, Exchange }
	public enum ETM_OrderState { eTM_OS_Filled }
	public enum ETM_OrderAction { eTM_OA_Buy, eTM_OA_Sell }
	public enum DataLoadedEvent { History, RTNewBar }
	public enum RequestQuoteField { Trade }
	public enum DataRequestType { BarsBack }

	public class DataRequest
	{
		public DataRequestType RequestType;
		public int Count;
	}

	public class InstrumentDataRequest
	{
		public Resolution Resolution;
		public RequestQuoteField QuoteField;
		public bool Subscribe2RT;
		public DataRequest Range;
	}

	public class SessionObject
	{
		public DayOfWeek StartDay, EndDay;
		public TimeSpan StartTime, EndTime;
	}

	public class IROList<T> : System.Collections.Generic.List<T> { }

	public class TmOrder
	{
		public string SymbolStr, Name;
		public ETM_OrderState State;
		public ETM_OrderAction Action;
		public int LeftContracts;
		public DateTime? FinalDT;
		public DateTime GeneratedDT;
	}

	public class SymbolFilter { public string CurrentValue; }
	public class IntervalFilter
	{
		public DateTime From, To;
		public void Set(DateTime from, DateTime to) { From = from; To = to; }
	}
	public class TmOrders
	{
		public SymbolFilter SymbolFltr = new SymbolFilter();
		public IntervalFilter IntervalFltr = new IntervalFilter();
		public System.Collections.Generic.List<TmOrder> Items = new System.Collections.Generic.List<TmOrder>();
	}

	public class TradingData : ITradingData
	{
		public TmOrders Orders { get; set; }
		public AccountBook Accounts { get; set; }
		public TradingData() { Orders = new TmOrders(); Accounts = new AccountBook(); }
	}

	public class OutputStub : IOutput { public void WriteLine(string s) { } }
	public class ExecInfoStub { public int MaxBarsBack { get; set; } }
	public class ExecControlStub { public void RecalcLastBarAfter(TimeSpan delay) { } }
	public class PortfolioStub { public double MaxRiskEquityPerPosPercent { get; set; } public double NetProfit { get; set; } }
	public class AccountItem { public string Name { get; set; } public double? Equity { get; set; } }
	public class AccountBook { public AccountItem[] Items { get; set; } public AccountBook() { Items = new AccountItem[0]; } }

	public class PortfolioDataStub : IPortfolioData
	{
		public bool ContainsKey(string key) { return false; }
		public object this[string key] { get { return null; } set { } }
	}

	public interface IPortfolioData
	{
		bool ContainsKey(string key);
		object this[string key] { get; set; }
	}

	public interface IOutput
	{
		void WriteLine(string s);
	}

	public interface ITradingData
	{
		TmOrders Orders { get; }
		AccountBook Accounts { get; }
	}

	public struct Bar
	{
		public DateTime Time;
		public double Open, High, Low, Close;
		public float TotalVolume;
		public int UpTicks, DownTicks;
	}

	public interface IDataLoaderResult
	{
		System.Collections.Generic.IEnumerable<Bar> Data { get; }
		DataLoadedEvent Event { get; }
		bool IsCompleted { get; }
		Bar? RTData { get; }
	}

	public enum ESymbolCategory
	{
		None, Future, FutureRolover, Stock, Forex, Crypto, Index, FutureOption, StockOption, CurrOption,
		IndexOption, MutualFund, MoneyMarketFund, Cash, Bond, Spread, CPCSymbol, Composites, FutureRoot,
		FutureOptionRoot, StockOptionRoot, Commodity, Warrant, Unknown
	}
	public enum EMarketPositionSide { Flat, Long, Short }

	public interface IMarketPosition
	{
		bool IsLong { get; }
		int OpenTradesCount { get; }
		System.Collections.Generic.IList<ITrade> OpenTrades { get; }
		System.Collections.Generic.IList<ITrade> ClosedTrades { get; }
		double MaxRunUp { get; }
		double MaxDrawDown { get; }
		EMarketPositionSide Side { get; }
	}

	public interface ITrade
	{
		IOrder EntryOrder { get; }
		IOrder ExitOrder { get; }
		bool IsLong { get; }
		double Profit { get; }
		double CommissionValue { get; }
	}

	public interface IOrder
	{
		string Name { get; }
		int BarNumber { get; }
		DateTime Time { get; }
		double Price { get; }
		int Contracts { get; }
	}

	public class CStudyControl { }

	public class FunctionSimple<T>
	{
		public FunctionSimple(CStudyControl master) { Bars = new BarsStub(); }
		public FunctionSimple(CStudyControl master, int dataStream) { Bars = new BarsStub(); }
		protected virtual void StartCalc() { }
		protected virtual T CalcBar() { return default(T); }
		public T Value { get { return CalcBar(); } }
		protected IInstrument Bars { get; set; }
	}

	public interface IInstrumentSettings
	{
		string Name { get; }
		string Exchange { get; }
		string SessionName { get; }
		Resolution Resolution { get; }
		ESymbolCategory Category { get; }
		double BigPointValue { get; }
		double MinMove { get; }
		double PriceScale { get; }
		RequestTimeZone TimeZone { get; }
		string DataFeed { get; }
		string BVO { get; }
		ASymbolInfo2 ASymbolInfo2 { get; }
	}

	public class ASymbolInfo2
	{
		public ATCenterProxy.interop.MTPA_MCSymbolCurrency SymbolCurrency;
		public object SymbolCategory;
		public ATCenterProxy.interop.MTPA_SymbolFields UnsupportedFields;
		public string SymbolRoot;
		public double PriceScale;
		public double BigPointValue;
		public DateTime SymbolExpiry;
	}

	public interface IInstrument
	{
		EBarState Status { get; }
		int CurrentBar { get; }
		IInstrumentSettings Info { get; }
		Series High { get; }
		Series Low { get; }
		Series Close { get; }
		Series Open { get; }
		Series Volume { get; }
		Series UpTicks { get; }
		Series DownTicks { get; }
		TimeSeries Time { get; }
		FullSymbolData FullSymbolData { get; }
		IROList<SessionObject> Sessions { get; }
	}

	public class FullSymbolData
	{
		public int Current;
		public TimeSeries Time = new TimeSeries();
	}
}

namespace ATCenterProxy.interop
{
	[System.Flags]
	public enum MTPA_SymbolFields { None = 0, eMTPA_SF_Root = 1 }
	public enum MTPA_MCSymbolCurrency { None = 0, eMTPA_MCSC_USD = 1 }
}

public static class McTradeManager
{
	public static void ProcessEvents() { }
	public static PowerLanguage.ITradingData TradingData { get { return null; } }
}

namespace PowerLanguage
{
	public class DataLoader
	{
		public static void EndLoadData(IDataLoaderResult r) { }
		public static IDataLoaderResult BeginLoadData(InstrumentDataRequest req, Action<IDataLoaderResult> cb, object state) { return null; }
	}
}


namespace PowerLanguage.Strategy
{
	public class PortfolioSignalObject : CStudyControl
	{
		public PortfolioSignalObject(object ctx) { }
		public double InitialCapital;
		public double NetProfit;
		public double Commission;
		public double Slippage;
		protected PowerLanguage.BarsStub Bars = new PowerLanguage.BarsStub();
		protected PowerLanguage.StrategyInfoStub StrategyInfo = new PowerLanguage.StrategyInfoStub();
		protected PowerLanguage.PositionStub CurrentPosition = new PowerLanguage.PositionStub();
		protected PowerLanguage.AppEnv Environment = new PowerLanguage.AppEnv();
		protected PowerLanguage.PortfolioDataStub MyPortfolioData = new PowerLanguage.PortfolioDataStub();
		protected PowerLanguage.OrderCreatorStub OrderCreator = new PowerLanguage.OrderCreatorStub();
		protected ESpecOrdersMode CurSpecOrdersMode;
		public string Name { get; set; }
		protected IOutput Output { get; set; }
		protected ExecInfoStub ExecInfo { get; set; }
		protected ExecControlStub ExecControl { get; set; }
		protected PortfolioStub Portfolio { get; set; }
		protected object Account { get; set; }
		protected int MaxDataStream { get { return 4; } }
		protected IPortfolioData CommonPortfolioData { get { return MyPortfolioData; } }
		protected System.Collections.Generic.IList<IMarketPosition> Positions { get; set; }
		protected IInstrument BarsOfData(int ds) { return Bars; }
		protected bool IsExist(int ds) { return true; }
		public PortfolioSignalObject()
		{
			Positions = new System.Collections.Generic.List<IMarketPosition>();
			Name = ""; Output = new OutputStub(); ExecInfo = new ExecInfoStub(); ExecControl = new ExecControlStub(); Portfolio = new PortfolioStub();
		}
		protected virtual void Create() { }
		protected virtual void StartCalc() { }
		protected virtual void CalcBar() { }
		protected virtual void StopCalc() { }
		protected virtual void Destroy() { }
		public void trace(ETraceLevel level, string message) { }
		protected bool traceOn(ETraceLevel level) { return false; }
		protected double tickSize() { return 0.25; }
		protected void GenerateStopLoss(double amount) { }
	}
}
