using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
	public class _VCP : IndicatorObject, IChartCustomDrawer
	{
		//constants


		//inputs
		[Input] public int ScanLength { get; set; }   //scan length for the VCP
		[Input] public int MinimumLegCount { get; set; }  //minimum number of required legs
		[Input] public int MinimumLegBarDelta { get; set; } //minimum number of bars between legs to avoid whipsaws
		[Input] public double MinimumLegOverlay { get; set; } //minimum required leg overlay to be considered a VCP, 0 to simply check for contaction without overlay
		[Input] public Color OutlineColor { get; set; }
		[Input] public Color FillColor { get; set; }
		[Input] public bool Debug { get; set; }

		//properties


		//attributes
		private Function._VCP m_vcp;
		private List<Tuple<DateTime, DateTime, double, double>> m_vcps;    //data for start time, end time, low, and high of the VCP

		//interface methods
		public _VCP(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			ScanLength = Function._VCP.DEFAULT_SCAN_LENGTH;
			MinimumLegCount = Function._VCP.MINIMUM_LEG_COUNT;
			MinimumLegBarDelta = Function._VCP.MINIMUM_LEG_BAR_DELTA;
			MinimumLegOverlay = Function._VCP.DEFAULT_MINIMUM_LEG_OVERLAY;
			OutlineColor = Color.Red;
			FillColor = Color.LightBlue;
			Debug = false;
			m_vcp = new Function._VCP(this);
			m_vcps = new List<Tuple<DateTime, DateTime, double, double>>();
		}

		protected override void StartCalc()
		{
			ChartCustomDraw.Register(this);
			m_vcp.Instrument = Bars;
			m_vcp.ScanLength = ScanLength;
			m_vcp.MinimumLegCount = MinimumLegCount;
			m_vcp.MinimumLegBarDelta = MinimumLegBarDelta;
			m_vcp.MinimumLegOverlay = MinimumLegOverlay;
		}

		protected override void StopCalc()
		{
			ChartCustomDraw.Unregister(this);
		}

		protected override void CalcBar()
		{
			if (m_vcp[0])
			{
				if (Debug) Output.WriteLine("VCP - FirstBar {0}, LastBar {1}, Low {2}, High {3}", m_vcp.FirstBar, m_vcp.LastBar, m_vcp.Low, m_vcp.High);
				m_vcps.Add(new Tuple<DateTime, DateTime, double, double>(m_vcp.FirstBar, m_vcp.LastBar, m_vcp.Low, m_vcp.High));
			}
		}

		//methods
		void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase) {
			//only render VCP's on before the foreground shapes	
			if (phase != EDrawPhases.BeforeFGShapes) return;

			foreach (var vcp in m_vcps)
			{
				PointF start = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item1, vcp.Item3));
				PointF end = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item2, vcp.Item4));
				RectangleF rect = new RectangleF(start, new SizeF(end.X - start.X, end.Y - start.Y));

				if (context.DirtyRect.IntersectsWith(rect))
				{
					using (Pen pen = new Pen(OutlineColor, 1.0f))
					using (Brush brush = new SolidBrush(FillColor))
					{
						context.graphics.FillRectangle(brush, rect);
						context.graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
					}
				}
			}
		}
	}
}