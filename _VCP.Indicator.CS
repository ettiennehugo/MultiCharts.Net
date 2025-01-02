using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
	public class _VCP : IndicatorObject, IChartCustomDrawer
	{
		//constants
		public const float VCP_LINE_WIDTH = 5.0f;
		public const float TEXT_Y_PADDING = 5.0f;

		//inputs
		[Input] public int FastLength { get; set; }          //fast length of the VCP KAMA
		[Input] public int SlowLength { get; set; }          //slow length of the VCP KAMA
		[Input] public double MinimumATRDelta { get; set; }  //minimum ATR delta between the high and low of the VCP
		[Input] public int ScanLength { get; set; }					 //scan length for the VCP
		[Input] public int MinimumRequiredLegs { get; set; } //minimum number of legs required to form the VCP
		[Input] public double MinimumPercentageContracingLegs { get; set; } //minimum percentage of acceptable legs contracting to form the VCP, e.g. 0.8 means 80% of the discovered legs need to contract to meet our VCP criteria 
		[Input] public Color Color { get; set; }
		[Input] public float FontSize { get; set; }
		[Input] public bool Debug { get; set; }

		//properties


		//attributes
		private Function._VCP m_vcp;
		private List<Tuple<DateTime, DateTime, double, double, double, List<Tuple<int, int, DateTime, double>>>> m_vcps;    //data for start time, end time, low, and high of the VCP

		//interface methods
		public _VCP(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			FastLength = Function._VCP.DEFAULT_FAST_LENGTH;
			SlowLength = Function._VCP.DEFAULT_SLOW_LENGTH;
			MinimumATRDelta = Function._VCP.DEFAULT_MINIMUM_ATR_DELTA;
			ScanLength = Function._VCP.DEFAULT_SCAN_LENGTH;
			MinimumRequiredLegs = Function._VCP.DEFAULT_MINIMUM_REQUIRED_LEGS;
			MinimumPercentageContracingLegs = Function._VCP.DEFAULT_MINIMUM_PERCENTAGE_CONTRACTING_LEGS;
			Color = Color.Blue;
			FontSize = 10.0f;
			Debug = false;
			m_vcp = new Function._VCP(this);
			m_vcps = new List<Tuple<DateTime, DateTime, double, double, double, List<Tuple<int, int, DateTime, double>>>>();
		}

		protected override void StartCalc()
		{
			ChartCustomDraw.Register(this);
			m_vcp.Instrument = Bars;
			m_vcp.FastLength = FastLength;
			m_vcp.SlowLength = SlowLength;
			m_vcp.MinimumATRDelta = MinimumATRDelta;
			m_vcp.ScanLength = ScanLength;
			m_vcp.MinimumRequiredLegs = MinimumRequiredLegs;
			m_vcp.MinimumPercentageContracingLegs = MinimumPercentageContracingLegs;
		}

		protected override void StopCalc()
		{
			ChartCustomDraw.Unregister(this);
			m_vcps.Clear();   //clear the VCP data as the indicator is being removed/disabled
		}

		protected override void CalcBar()
		{
			if (m_vcp[0])
			{
				//NOTE: As the VCP are forming from one bar to the next it would be detected so we need to only keep the LAST instance of the VCP.
				//      So when we have an overlap between the current VCP and the previous VCP we need to remove the previous VCP.
				if (m_vcps.Count > 0)
				{
					var lastVCP = m_vcps.Last();
					if (m_vcp.OpenTime <= lastVCP.Item2 && lastVCP.Item2 <= m_vcp.CloseTime)
						m_vcps.RemoveAt(m_vcps.Count - 1);
					else if (Debug)
						//we're keeping the current VCP so output debugging information
						Output.WriteLine("VCP - FirstBar {0}, LastBar {1}, Low {2}, High {3}", m_vcp.OpenTime, m_vcp.CloseTime, m_vcp.Low, m_vcp.High);
				}

				m_vcps.Add(new Tuple<DateTime, DateTime, double, double, double, List<Tuple<int, int, DateTime, double>>>(m_vcp.OpenTime, m_vcp.CloseTime, m_vcp.Low, m_vcp.High, m_vcp.PercentageContracingLegs, new List<Tuple<int, int, DateTime, double>>(m_vcp.PivotPoints)));
			}
		}

		//methods
		void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
		{
			//only render VCP's on before the foreground shapes	
			if (phase != EDrawPhases.BeforeFGShapes) return;

			using (Font textFont = new Font("Arial", FontSize, FontStyle.Regular))
			using (StringFormat textFormat = new StringFormat(StringFormatFlags.NoWrap))
			using (Pen vcpPen = new Pen(Color, VCP_LINE_WIDTH))
			using (Brush textBrush = new SolidBrush(Color))
			{
				foreach (var vcp in m_vcps)
				{
					string text = string.Format("{0:F2}%", vcp.Item5 * 100.0d);
					SizeF textSize = context.graphics.MeasureString(text, textFont);
					PointF start = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item1, vcp.Item3));
					PointF end = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item2, vcp.Item4));
					RectangleF rect = new RectangleF(start, new SizeF(end.X - start.X, end.Y - start.Y));
					RectangleF vcpRectWithText = new RectangleF(rect.X, rect.Y - textSize.Height, rect.Width, rect.Height + textSize.Height + TEXT_Y_PADDING);

					if (context.DirtyRect.IntersectsWith(vcpRectWithText))
					{
						for (int i = 1; i < vcp.Item6.Count; i++)
						{
							PointF legStart = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item6[i - 1].Item3, vcp.Item6[i - 1].Item4));
							PointF legEnd = context.Environment.ChartPoint2Point(new ChartPoint(vcp.Item6[i].Item3, vcp.Item6[i].Item4));
							if (context.DirtyRect.Contains(legStart) || context.DirtyRect.Contains(legEnd))
								context.graphics.DrawLine(vcpPen, legStart, legEnd);
						}

						PointF textPoint = new PointF(vcpRectWithText.X + (vcpRectWithText.Width / 2.0f), vcpRectWithText.Y + TEXT_Y_PADDING);
						context.graphics.DrawString(text, textFont, textBrush, textPoint, textFormat);
					}
				}
			}


		}
	}
}