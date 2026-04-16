using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
    /// <summary>
    /// Flattens the position of any instrument where a position is detected.
    /// </summary>
    public class _FlattenPosition : SignalObject {
        //constants


        //inputs


        //properties


        //attributes
        private IOrderMarket m_lx;
        private IOrderMarket m_sx;
        
        //interface methods
        public _FlattenPosition(object _ctx):base(_ctx){}

        protected override void Create()
        {
            m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "FlatLX", EOrderAction.Sell, OrderExit.FromAll));
            m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "FlatSX", EOrderAction.BuyToCover, OrderExit.FromAll));
        }        

        protected override void CalcBar(){
            switch (CurrentPosition.Side)
            {
                case EMarketPositionSide.Long:
                    m_lx.Send();
                    break;
                case EMarketPositionSide.Short:
                    m_lx.Send();
                    break;
            }            
        }

        //methods


    }
}