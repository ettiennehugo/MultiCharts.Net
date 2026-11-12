using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
    public class _McBidAskProbe : _VolumeProfileFuturesBase {
        // Live check 9a (specs/live-check-sheet.md Check 9, P-BA4) MC-BidAsk order-flow probe. Moved unchanged from _Test1 on 2026-11-12.
        //constants

        
        //types


        //properties


        //attribute

        
        //interface methods
        public _McBidAskProbe(object _ctx):base(_ctx){
            OrderFlowEnabled = true;
            FlowCfg.McBidAsk = _OrderFlow.EMcBidAsk.Prefer; 
            MinNodeTicks = 3;
            McBidAskChartType = ChartType.FootPrint;
        }

        protected override void Create() {
            // create variable objects, function objects, order objects etc.
            base.Create();
        }

        protected override void StartCalc() {
            // assign inputs
            base.StartCalc();
         
        }

        protected override void CalcBar(){
            // strategy logic 
            base.CalcBar();
        }

        //methods


    }
}