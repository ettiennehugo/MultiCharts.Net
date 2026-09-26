using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Stacked imbalance continuation for futures trading. Discussed in Volume Profile Book 1 chapter 6.3.
	/// The strategy is discussed more in the context of scalping in the book so stick to shorter time frames.
	/// Data feeds:
	///  * Primary - candlestick 1-minute up to 30-minute time frame or contract based counts if you want to stay at the pace of the market movement.
	///  * [optional] Secondary - 30-minutes to 1/2/4 hours - this secondary timeframe will be used to determine confluence accross the primary data feed and the secondary data feed. 
	/// Entry:
	///   1. Price sweep through at least a specific number of levels on the VP of imbalances between the bid/ask - creating a zone of support/resistance.
	///   2. Price either moves away or keeps on testing the zone without breaking the zone extreme low.
	///     2.1 If price moves away and returns to the zone testing it a stop entry is placed at the bar close if the bar fails to break the extreme low - the last close bar should also be bullish.
	///     2.2 If price keeps on testing inside the zone without breaking the extreme low a limit order entry is placed in the middle of the zone on the second/third bar testing the zone.
	/// Exit:
	///   * Stop loss goes below the zone low of the stacked imbalance low + a buffer.
	///   * Take profit goes by the given reward factor - typically keep this 1.5 to 3 range. TBD - Should you support HVN/VAH exits???
	/// </summary>
	public class _StackedImblContLELX : _VolumeProfileFuturesBase {
        //constants

        
        //types


        //properties


        //attribute
        private IOrderMarket m_le;

        //interface methods
        public _StackedImblContLELX(object _ctx):base(_ctx){}

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