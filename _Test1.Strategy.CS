using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
    public class _Test1 : _VolumeProfileFuturesBase {
        //constants

        
        //types


        //properties


        //attribute

        
        //interface methods
        public _Test1(object _ctx):base(_ctx){
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