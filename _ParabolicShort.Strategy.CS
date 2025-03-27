using PriceAction;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Parabolic short signal to detect when to enter a short trade on a parabolic price move.
	/// </summary>
	public class _ParabolicShort : _CustomSignal
	{
		//constants


		//types


		//properties
		[Input] public int FractalLayers { get; set; } //number of fractal layers to use for the price action analysis

		//attribute
		private PriceActionAnalysis m_priceAction;
		private IOrderMarket m_se;

		//interface methods
		public _ParabolicShort(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.SellShort));

		}

		protected override void StartCalc()
		{
			m_priceAction = new PriceActionAnalysis(FractalLayers);
		}

		protected override void CalcBar()
		{
			// strategy logic 
			m_se.Send();
		}

		//methods


	}
}