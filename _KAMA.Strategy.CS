using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy {
    /// <summary>
	/// Chart Type: Candlestick
	/// 
    /// Hypothesis:
	/// 	Works around Perry Kaufman's adaptive moving average indicator.
	/// 
    /// Ideal Instruments and Timeframes:
	///     
    ///     TBD!!!
	/// 
    /// Entry Rules - Long:
	/// 	KAMA has positive momentum.
	/// 
    /// Entry Rules - Short:
	/// 	KAMA has negative momentum.
	/// 
    /// Exit Rules - Long:
	/// 	KAMA is flat or has negative momentum.
    /// 
    /// Exit Rules - Short:
    /// 	KAMA is flat or has positive momentum.
    /// 
    /// Money Management:
    /// 	Fixed ratio position sizing.
	/// 
    /// Notes:
	/// 
    ///     TODO: Whipsaws kill this strategy but it captures longer trends really nicely.
	/// 
	/// </summary>
	public class _KAMA : SignalObject {
        //Inputs:
        [Input] public int FastLength { get; set; }
        [Input] public int SlowLength { get; set; }
        [Input] public int EfficiencyRatioLength { get; set; }
        [Input] public int TFStartTime { get; set; }                //time filter start time, e.g. 800 is 8am and 1300 is 1pm		
        [Input] public int TFWindowLength { get; set; }             //time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
        [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
        [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
        [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
        [Input] public bool EnableLong { get; set; }                //enable long trades
        [Input] public bool EnableShort { get; set; }				//enable short trades
        [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

        //Attributes:
        private PowerLanguage.Function._KAMA _kama;
        private IOrderMarket _LE_DMM;
        private IOrderMarket _SE_DMM;
        private IOrderMarket _LE_SMM;
        private IOrderMarket _SE_SMM;
        private IOrderMarket _LX;
        private IOrderMarket _SX;
        private TimeSpan _StartTime;
        private TimeSpan _EndTime;

        public _KAMA(object _ctx):base(_ctx){
            FastLength = 2;
            SlowLength = 30;
            EfficiencyRatioLength = 10;
            TFStartTime = 0;        //12:00am
            TFWindowLength = 1439;  //minutes in a full 24-hour trading day
            MMStartPosSize = 0;     //start contracts and additional contracts to add (0 - disable dynamic MM)
            MMProfitDelta = 0;      //profit delta before another additional contract is added
            MMStartNetProfit = 0;   //start net profit between restarts of the strategy
            EnableLong = true;      //per default enable long trades
            EnableShort = true;		//per default enable short trades		
            Debug = false;
        }

        protected override void Create() {
            //create indicators used
            _kama = new PowerLanguage.Function._KAMA(this);

            //create the order types used
            _LE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "KAMADLE", EOrderAction.Buy));         //dynamic money management orders
            _SE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "KAMADSE", EOrderAction.SellShort));
            _LE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "KAMASLE", EOrderAction.Buy));               //static money management orders
            _SE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "KAMASSE", EOrderAction.SellShort));
            _LX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "KAMALX", EOrderAction.Sell));          //exit orders
            _SX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "KAMASX", EOrderAction.BuyToCover));
        }

        protected override void StartCalc() {
            _kama.FastLength = FastLength;
            _kama.SlowLength = SlowLength;
            _kama.EfficiencyRatioLength = EfficiencyRatioLength;
            _kama.Price = Bars.Close;

            //set the start/end times for the time filter
            _StartTime = new TimeSpan(TFStartTime / 100, TFStartTime % 100, 0);
            _EndTime = _StartTime + new TimeSpan(TFWindowLength / 60, TFWindowLength % 60, 0);

            //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
            //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
            if (_EndTime.Days > 0) _EndTime = _EndTime.Subtract(new TimeSpan(_EndTime.Days, 0, 0, 0));
        }

        //protected override void StopCalc() {
        //	//cleanup/deinitialize strategy components
        //	
        //}

        protected override void CalcBar() {
            switch (Environment.CalcReason) {
                case CalculationReason.Default:
                    CalcBarDefault();
                    break;
                case CalculationReason.OrderFilled:
                    CalcBarOrderFilled();
                    break;
            }
		}
		
        protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
            orderFilled();
        }
		
		private void CalcBarDefault() {
            //get the momentum for the indicator
            double momentum = _kama.Momentum;

            //check market position and process bar accordingly
            if (PositionSide == 0) {
                //apply time filter for entries
                //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
                //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
                DateTime currentTime = Bars.Time.Value;

                if (((_StartTime < _EndTime) && (_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= _EndTime)) ||    //start/end time fall in the same trading day
                    ((_StartTime > _EndTime) && ((_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= _EndTime)))) {  //start/end time span over different trading days
                    bool enterLong = false;
                    bool enterShort = false;

                    if (EnableLong) enterLong = momentum > 0;
                    if (EnableShort) enterShort = momentum < 0;

                    //check whether we're using dynamic or static position sizing
                    if (MMStartPosSize > 0 && MMProfitDelta > 0) {
                        //dynamic position sizing, compute new trade size
                        //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
                        double netProfit = MMStartNetProfit + NetProfit;
                        netProfit = (netProfit > 0) ? netProfit : 0;        //net profit can not be negetive for square root calculation below
                        int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

                        if (enterLong) _LE_DMM.Send(numLots);
                        if (enterShort) _SE_DMM.Send(numLots);
                    } else {
                        //static position sizing controlled by the user settings
                        if (enterLong) _LE_SMM.Send();
                        if (enterShort) _SE_SMM.Send();
                    }
                }
            } else if (PositionSide > 0) {
                //exit long position
                if (momentum <= 0) _LX.Send();
            } else if (PositionSide < 0) {
                //exit short position
                if (momentum >= 0) _SX.Send();
            }
		}

        private void CalcBarOrderFilled() {
            
            //currently do nothing

            //orderFilled();
        }

        private void orderFilled() {
			//send initial stop-loss when an entry order is filled
		
			//if (PositionSide > 0) {
			//
            //}

            //if (PositionSide < 0) {
			//
            //}
        }
		
		
		
	}
}