using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;
using System.Collections.Generic;

namespace PowerLanguage.Strategy 
{
	public class _Test2 : SignalObject
	{
		//constants:


		//properties:


		//attributes:
		private IInstrument m_instrument1;
		private IInstrument m_instrument2;
		
		public _Test2(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Output.Clear();
		}

		protected override void StartCalc()
		{
			m_instrument1 = BarsOfData(1);
			m_instrument2 = BarsOfData(2);
		}

		protected override void CalcBar()
		{

			if (m_instrument1.Status == EBarState.Close && m_instrument2.Status == EBarState.Close)
			{
				Output.WriteLine(string.Format("BOTH - M5 {0}, D {1}", m_instrument1.Time[0].ToString(), m_instrument2.Time[0].ToString()));
			}
			else if (m_instrument1.Status == EBarState.Close)
			{
				Output.WriteLine(string.Format("ONLY - M5 {0}", m_instrument1.Time[0].ToString()));
			}
		}
	}
}
