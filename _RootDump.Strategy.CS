using System;
using System.Globalization;
using ATCenterProxy.interop;
using System.Collections.Generic;

namespace PowerLanguage.Strategy
{
    public class _RootDump : SignalObject
    {
        public _RootDump(object ctx) : base(ctx) { }

        protected override void StartCalc()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            try
            {
                MTPA_MCSymbolInfo2 s = Bars.Info.ASymbolInfo2;
                Output.WriteLine("ROOTDUMP name(" + Bars.Info.Name + ") symName(" + s.SymbolName + ") root(" + s.SymbolRoot +
                    ") exch(" + s.SymbolExchange + ") cat(" + s.SymbolCategory + ") ccy(" + s.SymbolCurrency +
                    ") expiry(" + s.SymbolExpiry.ToString("yyyy-MM-dd", inv) + ") minMove(" + s.MinMove + ")");
                Output.WriteLine("ROOTDUMP " + Bars.Info.Name + " priceScale struct(" + s.PriceScale.ToString("R", inv) +
                    ") info(" + Bars.Info.PriceScale.ToString("R", inv) + ") bpv struct(" + s.BigPointValue.ToString("R", inv) +
                    ") info(" + Bars.Info.BigPointValue.ToString("R", inv) + ")");
                Output.WriteLine("ROOTDUMP " + Bars.Info.Name + " unsupported(" + s.UnsupportedFields + ") flagsEnum(" +
                    typeof(MTPA_SymbolFields).IsDefined(typeof(FlagsAttribute), false) + ")");
                foreach (MTPA_SymbolFields f in Enum.GetValues(typeof(MTPA_SymbolFields)))
                    Output.WriteLine("ROOTDUMP " + Bars.Info.Name + " fieldsMember(" + f + "=" + Convert.ToInt64(f, inv) + ")");
            }
            catch (Exception e) { Output.WriteLine("ROOTDUMP " + Bars.Info.Name + " failed: " + e.GetType().Name + ": " + e.Message); }
        }

        protected override void CalcBar() { }
    }
}