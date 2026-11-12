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
	}

	public class TimeSeries
	{
		public DateTime this[int barsAgo] { get { return DateTime.MinValue; } }
	}

	public class InstrumentSettings
	{
		public double BigPointValue;
		public Resolution Resolution;
	}

	public class BarsStub
	{
		public EBarState Status;
		public int CurrentBar;
		public Series High = new Series();
		public Series Low = new Series();
		public Series Close = new Series();
		public TimeSeries Time = new TimeSeries();
		public InstrumentSettings Info = new InstrumentSettings();
	}

	public class StrategyInfoStub
	{
		public int MarketPosition;
		public double AvgEntryPrice;
	}

	public class PositionStub
	{
		public int OpenLots;
		public double OpenProfit;
	}

	public class AppEnv
	{
		public EApplicationCode ApplicationCode;
	}

	public class PortfolioDataStub
	{
		public bool ContainsKey(string key) { return false; }
		public object this[string key] { get { return null; } set { } }
	}
}

namespace PowerLanguage.Function
{
	public class InstrumentProfile
	{
		public int costFloorTicks;
	}
}

namespace PowerLanguage.Strategy
{
	using PowerLanguage.Function;

	public abstract class _CustomSignal
	{
		protected _CustomSignal(object ctx) { }

		protected virtual bool PortfolioPublish { get { return false; } }
		protected virtual void Create() { }
		protected virtual void StartCalc() { }
		protected virtual void CalcBar() { }

		public double InitialCapital;
		public double NetProfit;
		protected BarsStub Bars = new BarsStub();
		protected StrategyInfoStub StrategyInfo = new StrategyInfoStub();
		protected PositionStub CurrentPosition = new PositionStub();
		protected AppEnv Environment = new AppEnv();
		protected PortfolioDataStub MyPortfolioData = new PortfolioDataStub();
		protected OrderCreatorStub OrderCreator = new OrderCreatorStub();
		protected ESpecOrdersMode CurSpecOrdersMode;

		protected string GxOrderPrefix { get; set; }
		protected IOrderPriced m_gxLS, m_gxSS, m_gxLT1, m_gxST1, m_gxLT, m_gxST;
		protected IOrderMarket m_gxLX, m_gxSX;

		public void trace(ETraceLevel level, string message) { }
		protected bool traceOn(ETraceLevel level) { return false; }
		protected double tickSize() { return 0.25; }
		protected void GenerateStopLoss(double amount) { }
		protected InstrumentProfile InstrumentProfileOf(int stream) { return null; }
		protected void createGxOrders() { }
		protected void gxPublishPlan(double plannedEntry, double plannedStop) { }
		protected void gxSetMarket(ExitMarket mkt) { }
		protected void gxAttachSession(ExitSession session, ExitPosition pos) { }
		protected void gxSetPlan(double entry, double stop, string source) { }
		protected void gxNoteExit(string reason) { }
		protected void gxSendExit(string reason, bool gapThrough, ExitPosition p, bool longSide, int lots) { }
		protected double exitPlan(bool longSide, double plannedEntry, double structuralStop, int qty, ExitMarket mkt, string entryOrderName) { return 0; }
		protected void exitBar() { }
	}
}
