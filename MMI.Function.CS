using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Market Meaness Index can be used to improve the performance of trend following systems. The function can be interpreted as follows:
		/// Unlike prices, price changes have not necessarily serial correlation. A one hundred percent efficient market has no correlation between
		/// the price change from yesterday to today and the price change from today to tomorrow. If the MMI function is fed with perfectly random
		/// price changes from a perfectly efficient market, it will return a value of about 75%. The less efficient and the more trending the market
		/// becomes, the more the MMI decreases. Thus a falling MMI is a indicator of an upcoming trend. A rising MMI hints that the market will get
		/// nastier, at least for trend trading systems.
		/// Another mistake would be using the MMI for detecting a cyclic or mean-reverting market regime. True, the MMI will rise in such a situation,
		/// but it will also rise when the market becomes more random and more effective. A rising MMI alone is no promise of profit by cycle trading systems.
		/// 
		/// So the MMI won’t tell us the next price, and it won’t tell us if the market is mean reverting or just plain mean, but it can reveal information
		/// about the success chance of trend following.
		/// </summary>
		public sealed class MMI : FunctionSimple<System.Double>
		{
			[Input] public int Length { get; set; }

			//Attributes:
			private double result;

			public MMI(CStudyControl _master) : base(_master) { }
			public MMI(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = 20;
				result = 0;
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close && Bars.CurrentBar > Length)
				{
					double average = Bars.Close.Average(Length);
					int nh = 0;
					int nl = 0;

					for (int i = 0; i < Length; i++)
					{
						if (Bars.Close[i] > average && Bars.Close[i] > Bars.Close[i + 1])
							nl++;
						else if (Bars.Close[i] < average && Bars.Close[i] < Bars.Close[i + 1])
							nh++;
					}

					result = 100.0 * (nl + nh) / (Length - 1);
				}

				return result;
			}
		}
	}
}