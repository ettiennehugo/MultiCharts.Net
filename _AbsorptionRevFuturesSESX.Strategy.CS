using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Absorption reversal for futures trading. Discussed in Volume Profile Book 1 chapter 6.2.
	/// The strategy is discussed more in the context of scalping in the book so stick to shorter time frames.
	/// Data feeds:
	///  * Primary - 1-minute up to 30-minute time frame or contract based counts if you want to stay at the pace of the market movement.
	///  * [optional] Secondary - 30-minutes to 1/2/4 hours - this secondary timeframe will be used to determine confluence accross the shorter data feed and the secondary data feed. 
	/// Entry:
	///   1. Price is entering a support/resistance zone as defined by the volume profile and VWAP (based on the primary and secondary data feeds).
	///   2. High volume, as measured by volume percentile, comes into the market at the zone.
	///   3. A limit entry is taken at the price level just below the absorption zone.
	/// Exit:
	///   * Stop loss goes above the absorption low.
	///   * Take profit goes by the given reward factor as a limit order - typically keep this 1.5 to 3 range.
	/// </summary>
	public class _AbsorptionRevFuturesSESX : _VolumeProfileFuturesBase {
        //constants

        
        //types


        //properties


        //attribute
        private IOrderMarket m_le;

        //interface methods
        public _AbsorptionRevFuturesSESX(object _ctx):base(_ctx){}

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