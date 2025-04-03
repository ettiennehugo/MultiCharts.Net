using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _EpisodicPivotHighVolume : IndicatorObject, IChartCustomDrawer {
		//constants


		//types
		protected struct CriteriaMetArea
		{
			public ChartPoint high;
			public ChartPoint low;
		};

		//properties
		[Input] public Function._EpisodicPivotHighVolume.EVolumeRequired VolumeRequired { get; set; } //volume required to be above average or above average + x standard deviations 
		[Input] public int GrowthBars { get; set; }
		[Input] public double GrowthPercent { get; set; }
		[Input] public int Length { get; set; }       //Length to use for average and standard deviation.
		[Input] public double Level1 { get; set; }    //Factor to use for first level of standard deviation.
		[Input] public double Level2 { get; set; }    //Factor to use for second level of standard deviation.
		[Input] public double Level3 { get; set; }    //Factor to use for third level of standard deviation.
		[Input] public bool Debug { get; set; }				//debug flag to output debug information to the output window	

		//attributes
		private Function._EpisodicPivotHighVolume m_episodicPivot;
		protected List<CriteriaMetArea> m_criteriaMetAreas;
		protected float FRAME_LINE_WIDTH = 3.0f;
		protected float X_PADDING = 3.0f;
		protected float Y_PADDING = 3.0f;

		//interface methods
		public _EpisodicPivotHighVolume(object _ctx):base(_ctx){}

		protected override void Create() {
			ChartCustomDraw.Register(this);
			m_criteriaMetAreas = new List<CriteriaMetArea>();
			m_episodicPivot = new Function._EpisodicPivotHighVolume(this);
			GrowthBars = Function._EpisodicPivotHighVolume.DEFAULT_GROWTH_BARS;
			GrowthPercent = Function._EpisodicPivotHighVolume.DEFAULT_GROWTH_PERCENT;
			VolumeRequired = Function._EpisodicPivotHighVolume.EVolumeRequired.AboveAverage;
			Level1 = Function._VolumeWithSD.DEFAULT_LEVEL1;
			Level2 = Function._VolumeWithSD.DEFAULT_LEVEL2;
			Level3 = Function._VolumeWithSD.DEFAULT_LEVEL3;
		}

		protected override void StartCalc() {
			m_episodicPivot.Instrument = Bars;
			m_episodicPivot.VolumeRequired = VolumeRequired;
			m_episodicPivot.Length = Length;
			m_episodicPivot.Level1 = Level1;
			m_episodicPivot.Level2 = Level2;
			m_episodicPivot.Level3 = Level3;
			m_criteriaMetAreas.Clear();
		}

		protected override void CalcBar(){
			if (m_episodicPivot[0])
			{
				if (Debug) Output.WriteLine("EpisodicPivotHighVolume Indicator, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				CriteriaMetArea criteriaMetArea = new CriteriaMetArea();
				criteriaMetArea.high.Time = Bars.Time[0];
				criteriaMetArea.high.Price = Bars.High[0];
				criteriaMetArea.low.Time = Bars.Time[0];
				criteriaMetArea.low.Price = Bars.Low[0];
				m_criteriaMetAreas.Add(criteriaMetArea);
			}
		}

		void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
		{
			if (phase == EDrawPhases.BeforeBGShapes)
			{
				using (Brush brush = new SolidBrush(Color.FromArgb(64, Color.Purple.R, Color.Purple.G, Color.Purple.B)))
				using (Pen pen = new Pen(Color.Purple, FRAME_LINE_WIDTH))
				{
					foreach (CriteriaMetArea area in m_criteriaMetAreas)
					{
						//compute area for rectangle around price movement
						PointF startPoint = context.Environment.ChartPoint2Point(area.high);
						startPoint.X -= (float)context.Environment.ChartBarCloseDistance / 2.0f;
						startPoint.X -= X_PADDING + context.Environment.ChartBarCloseDistance / 2.0f;
						startPoint.Y -= Y_PADDING;
						PointF endPoint = context.Environment.ChartPoint2Point(area.low);
						endPoint.X += context.Environment.ChartBarCloseDistance / 2.0f;
						endPoint.X += (X_PADDING * 2) + context.Environment.ChartBarCloseDistance;
						endPoint.Y += Y_PADDING;
						RectangleF rectangleF = new RectangleF(startPoint.X, startPoint.Y, Math.Abs(startPoint.X - endPoint.X), Math.Abs(startPoint.Y - endPoint.Y));

						if (context.DirtyRect.IntersectsWith(rectangleF))
						{
							context.graphics.FillRectangle(brush, rectangleF);
							context.graphics.DrawRectangle(pen, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
						}
					}
				}
			}
		}
	}
}