using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Delta divergence reversal for futures trading. Discussed in Volume Profile Book 1 chapter 6.1.
	/// The strategy is discussed more in the context of scalping in the book so stick to shorter time frames.
	/// Data feeds:
	///  * Primary - candlestick 1-minute up to 30-minute time frame or contract based counts if you want to stay at the pace of the market movement.
	///  * Secondary - cumulative delta chart over the same timeframe as the primary chart.
	///  * [optional] Tertiary - 30-minutes to 1/2/4 hours - this tertiary timeframe will be used to determine confluence accross the primary data feed and the secondary data feed. 
	/// Entry:
	///   1. Primary price is stalling out at a local high.
	///   2. Secondary cumulative delta is gaining bearish momentum.
	/// Exit:
	///   * Stop loss goes below the local high of the price move.
	///   * Take profit goes by the given reward factor - typically keep this 1.5 to 3 range. TBD - Should you support HVN/VAH exits???
	/// </summary>
	public class _DeltaDivRevFuturesSESX : _VolumeProfileFuturesBase {
        //constants

        
        //types


        //properties


        //attribute
        private IOrderMarket m_le;

        //interface methods
        public _DeltaDivRevFuturesSESX(object _ctx):base(_ctx){}

        protected override void Create() {
            // create variable objects, function objects, order objects etc.
            m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
        }

        protected override void StartCalc() {
            // assign inputs 
        }

        protected override void CalcBar(){
            // strategy logic 
            m_le.Send();
        }

        //methods


    }
}