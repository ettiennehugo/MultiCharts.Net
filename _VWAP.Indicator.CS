using PowerLanguage.Function;
using System;
using System.Drawing;
using System.Linq;
using static PowerLanguage.Function._VWAP;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol(true)]
	public class _VWAP : IndicatorObject
	{
		public _VWAP(object _ctx) : base(_ctx) { }

		//properties
		[Input] public Function._VWAP.VWAPType Type { get; set; } //Type of VWAP to use.  
		[Input] public int Length { get; set; }    //Length used when BarCount Type is set
		[Input] public string FromDate { get; set; } //From date for AdaptiveFromDate/AdaptiveFromToDate Types
		[Input] public string ToDate { get; set; } //To date for AdaptiveFromToDate Type

		//attribute
		private IPlotObject m_plot;
		private Function._VWAP m_vwap;

		protected override void Create()
		{
			Length = Function._VWAP.DEFAULT_LENGTH;
			Type = VWAPType.Session;
			Length = DEFAULT_LENGTH;
			FromDate = DateTime.Now.ToString("yyyy/MM/dd hh:mm");
			ToDate = DateTime.Now.ToString("yyyy/MM/dd hh:mm");

			m_plot = AddPlot(new PlotAttributes("VWAP", EPlotShapes.Line, Color.Green, Color.Transparent, 3, 0, true));
			m_vwap = new Function._VWAP(this);
		}

		protected override void StartCalc()
		{
			m_vwap.Type = Type;
			m_vwap.Length = Length;
			m_vwap.FromDate = DateTime.ParseExact(
				FromDate.Trim(),
				new[] { "yyyy/MM/dd HH:mm", "yyyy/MM/dd H:mm", "yyyy/MM/dd" },
				System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None);
			m_vwap.ToDate = DateTime.ParseExact(
				ToDate.Trim(),
				new[] { "yyyy/MM/dd HH:mm", "yyyy/MM/dd H:mm", "yyyy/MM/dd" },
				System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None);
		}

		protected override void CalcBar()
		{
			if (!Double.IsNaN(m_vwap.Value)) 
				m_plot.Set(m_vwap.Value);
		}
	}
}