using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	public class _TradeManagerTest : _CustomSignal
	{
		//constants


		//types


		//properties


		//attribute


		//interface methods
		public _TradeManagerTest(object _ctx) : base(_ctx) { }

		protected override void StartCalc()
		{
			base.StartCalc();
			TradeManager.TradingData.Orders.IntervalFltr.Set(DateTime.Now.AddDays(-21), DateTime.Now);
			TradeManager.TradingData.Orders.SymbolFltr.CurrentValue = "SXC";
			TradeManager.ProcessEvents();
			foreach (PowerLanguage.TradeManager.Order order in TradeManager.TradingData.Orders.Items)
				Output.WriteLine($"Symbol: {order.BrokerSymbol} | Order: {order.Name} | ID: {order.OrderID} | State: {order.State} | Qty: {order.Contracts} | Filled Qty: {order.FilledContracts} | Price: {order.ExecPrice} | GeneratedDT: {order.GeneratedDT} | FinalDT: {order.FinalDT ?? DateTime.MinValue}");
		}

		//methods


	}
}