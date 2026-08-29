using DunGen.Analysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Analysis
{
	public sealed class HistogramWindow : EditorWindow
	{
		private string key;
		private string unitLabel;
		private NumberSetData data;

		private string hoverTooltip;
		private GUIStyle tooltipStyle;
		private GUIStyle axisStartLabelStyle;
		private GUIStyle axisEndLabelStyle;

		private GUIStyle TooltipStyle
		{
			get
			{
				if (tooltipStyle != null)
					return tooltipStyle;

				tooltipStyle = new GUIStyle(EditorStyles.label)
				{
					wordWrap = true,
					padding = new RectOffset(10, 10, 8, 8)
				};

				tooltipStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
				return tooltipStyle;
			}
		}
		private GUIStyle AxisStartLabelStyle
		{
			get
			{
				if (axisStartLabelStyle != null)
					return axisStartLabelStyle;

				axisStartLabelStyle = new GUIStyle(EditorStyles.miniLabel)
				{
					alignment = TextAnchor.MiddleLeft
				};

				return axisStartLabelStyle;
			}
		}
		private GUIStyle AxisEndLabelStyle
		{
			get
			{
				if (axisEndLabelStyle != null)
					return axisEndLabelStyle;

				axisEndLabelStyle = new GUIStyle(EditorStyles.miniLabel)
				{
					alignment = TextAnchor.MiddleRight
				};

				return axisEndLabelStyle;
			}
		}


		public static void Open(string key, string units, NumberSetData data)
		{
			var w = CreateInstance<HistogramWindow>();
			w.titleContent = new GUIContent("Histogram");
			w.key = key;
			w.unitLabel = units;
			w.data = data;
			w.minSize = new Vector2(420, 240);
			w.ShowUtility();
		}

		private void OnEnable()
		{
			wantsMouseMove = true;
		}

		private void OnGUI()
		{
			// Force repaint so hover/tooltip follows cursor
			if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
				Repaint();

			EditorGUILayout.LabelField(key, EditorStyles.boldLabel);

			if (!string.IsNullOrEmpty(unitLabel))
				EditorGUILayout.LabelField($"Units: {unitLabel}");

			EditorGUILayout.Space(6);

			if (!data.HasSamples)
			{
				EditorGUILayout.HelpBox("No sample/bin data available for histogram rendering.", MessageType.Info);
				return;
			}

			double[] samples = data.GetSamples().ToArray();
			DrawHistogram(samples);
		}

		private void DrawHistogram(double[] samples, int minBins = 5, int maxBins = 80)
		{
			if (samples == null || samples.Length == 0)
				return;

			// Sanitize (skip NaN/Infinity)
			var clean = new List<double>(samples.Length);
			for (int i = 0; i < samples.Length; i++)
			{
				double x = samples[i];
				if (double.IsNaN(x) || double.IsInfinity(x))
					continue;

				clean.Add(x);
			}

			if (clean.Count == 0)
				return;

			// Plot area
			var rect = GUILayoutUtility.GetRect(10, 10_000, 120, 10_000, GUILayout.ExpandHeight(true));
			GUI.Box(rect, GUIContent.none);

			// Stats
			double min = clean.Min();
			double max = clean.Max();

			// Degenerate range: draw a single bar
			if (Math.Abs(max - min) < 1e-12)
			{
				// one bin centred around the single value
				hoverTooltip = DrawBars_IntegerAlignedWithHover(rect, new int[] { clean.Count }, 1, (long)Math.Round(min), 1, unitLabel);

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(min.ToString("0.###", CultureInfo.InvariantCulture), AxisStartLabelStyle);
					GUILayout.FlexibleSpace();
					EditorGUILayout.LabelField(max.ToString("0.###", CultureInfo.InvariantCulture), AxisEndLabelStyle, GUILayout.Width(80));
				}

				return;
			}

			// Decide integer-aligned vs continuous
			bool integerAligned = ShouldUseIntegerAlignedBins(clean);

			int[] counts;
			int bins;

			if (integerAligned)
			{
				// Integer-aligned bins: edges at (k - 0.5) so integer k falls in its own bar.
				// If too many bins, group into step size (1,2,5,10...).
				long minI = (long)Math.Round(min);
				long maxI = (long)Math.Round(max);
				if (maxI < minI) (minI, maxI) = (maxI, minI);

				long span = (maxI - minI) + 1; // number of integer values covered
				long step = 1;

				if (span > maxBins)
				{
					// Group into larger integer steps but keep integer boundaries aligned
					double raw = span / (double)maxBins;
					step = NiceIntStep((long)Math.Ceiling(raw));
				}

				// Align start to step boundary (floor) so edges are stable
				long start = FloorDiv(minI, step) * step;
				long endInclusive = maxI; // inclusive

				bins = (int)Math.Max(1, (long)Math.Ceiling(((endInclusive - start) + 1) / (double)step));
				bins = Mathf.Clamp(bins, 1, Math.Max(1, maxBins));
				counts = new int[bins];

				// Count
				for (int i = 0; i < clean.Count; i++)
				{
					long v = (long)Math.Round(clean[i]);
					long idxL = (v - start) / step;
					if (idxL < 0) continue;
					if (idxL >= bins) continue;
					counts[idxL]++;
				}

				// Draw
				hoverTooltip = DrawBars_IntegerAlignedWithHover(rect, counts, bins, start, step, unitLabel);

				// Axis labels
				double minEdge = start;
				double maxEdge = (start + (long)bins * step) - 1;

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(minEdge.ToString("0", CultureInfo.InvariantCulture), AxisStartLabelStyle);
					GUILayout.FlexibleSpace();
					EditorGUILayout.LabelField(maxEdge.ToString("0", CultureInfo.InvariantCulture), AxisEndLabelStyle, GUILayout.Width(120));
				}
			}
			else
			{
				// Continuous bins: "auto" = max(Sturges, Freedman–Diaconis)
				int n = clean.Count;
				double range = max - min;

				int kSturges = (int)Math.Ceiling(Math.Log(n, 2) + 1.0);
				kSturges = Mathf.Clamp(kSturges, 1, maxBins);

				// Compute IQR for Freedman–Diaconis
				double[] sorted = clean.ToArray();
				Array.Sort(sorted);
				double q1 = QuantileSorted(sorted, 0.25);
				double q3 = QuantileSorted(sorted, 0.75);
				double iqr = q3 - q1;

				int kFD = 0;
				if (iqr > 0)
				{
					double h = 2.0 * iqr * Math.Pow(n, -1.0 / 3.0);
					if (h > 0)
						kFD = (int)Math.Ceiling(range / h);
				}

				int k = Math.Max(kSturges, kFD > 0 ? kFD : 1);
				k = Mathf.Clamp(k, minBins, maxBins);

				bins = k;
				counts = new int[bins];

				double width = range / bins;
				if (width <= 0)
					width = range; // safety

				// Count (include max in last bin)
				for (int i = 0; i < clean.Count; i++)
				{
					double x = clean[i];
					int idx = (int)((x - min) / width);

					if (idx < 0)
						idx = 0;

					if (idx >= bins)
						idx = bins - 1;

					counts[idx]++;
				}

				// Draw
				hoverTooltip = DrawBars_ContinuousWithHover(rect, counts, bins, min, width, max, unitLabel);

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(min.ToString("0.##", CultureInfo.InvariantCulture), AxisStartLabelStyle);
					GUILayout.FlexibleSpace();
					EditorGUILayout.LabelField(max.ToString("0.##", CultureInfo.InvariantCulture), AxisEndLabelStyle, GUILayout.Width(80));
				}
			}

			DrawTooltipOverlay(hoverTooltip);
		}

		private void DrawBar(Rect barRect)
		{
			var outlineColour = EditorGUIUtility.isProSkin
					? new Color(0.2f, 0.4f, 0.8f, 1f)
					: new Color(0.1f, 0.25f, 0.6f, 1f);

			var fillColour = EditorGUIUtility.isProSkin
					? new Color(0.35f, 0.65f, 1f, 0.85f)
					: new Color(0.15f, 0.35f, 0.75f, 0.85f);

			const float outlineThickness = 1f;

			var outlineRect = barRect;
			var fillRect = new Rect(barRect.x + outlineThickness, barRect.y + outlineThickness, barRect.width - (outlineThickness * 2), barRect.height - (outlineThickness * 2));

			EditorGUI.DrawRect(outlineRect, outlineColour);
			EditorGUI.DrawRect(fillRect, fillColour);
		}

		private string DrawBars_IntegerAlignedWithHover(
			Rect plotRect,
			int[] counts,
			int bins,
			long start,
			long step,
			string units)
		{
			Vector2 mp = Event.current.mousePosition;
			string hovered = null;

			int maxCount = 1;
			for (int i = 0; i < counts.Length; i++)
				if (counts[i] > maxCount) maxCount = counts[i];

			Handles.BeginGUI();

			for (int i = 0; i < bins; i++)
			{
				float x0 = plotRect.x + (plotRect.width * i) / bins;
				float x1 = plotRect.x + (plotRect.width * (i + 1)) / bins;

				// Use full column for hover target (easier to hit)
				var hoverRect = new Rect(x0, plotRect.y, Mathf.Max(1, x1 - x0), plotRect.height);

				float h = plotRect.height * (counts[i] / (float)maxCount);
				var barRect = new Rect(x0, plotRect.yMax - h, Mathf.Max(1, x1 - x0 - 1), h);

				DrawBar(barRect);

				if (hoverRect.Contains(mp))
				{
					long low = start + (long)i * step;
					long high = low + step - 1;

					string rangeLabel = (step == 1) ? low.ToString() : $"{low} \u2013 {high}";
					string unitsSuffix = string.IsNullOrEmpty(units) ? "" : $" {units}";

					hovered =
						(step == 1 ? $"Value: {rangeLabel}{unitsSuffix}" : $"Range: {rangeLabel}{unitsSuffix}") +
						$"\nCount: {counts[i]}";

					EditorGUIUtility.AddCursorRect(hoverRect, MouseCursor.Link);
				}
			}

			Handles.EndGUI();
			return hovered;
		}

		private string DrawBars_ContinuousWithHover(
			Rect plotRect,
			int[] counts,
			int bins,
			double min,
			double width,
			double max,
			string units)
		{
			Vector2 mp = Event.current.mousePosition;
			string hovered = null;

			int maxCount = 1;
			for (int i = 0; i < counts.Length; i++)
				if (counts[i] > maxCount) maxCount = counts[i];

			Handles.BeginGUI();

			for (int i = 0; i < bins; i++)
			{
				float x0 = plotRect.x + (plotRect.width * i) / bins;
				float x1 = plotRect.x + (plotRect.width * (i + 1)) / bins;

				var hoverRect = new Rect(x0, plotRect.y, Mathf.Max(1, x1 - x0), plotRect.height);

				float h = plotRect.height * (counts[i] / (float)maxCount);
				var barRect = new Rect(x0, plotRect.yMax - h, Mathf.Max(1, x1 - x0 - 1), h);

				DrawBar(barRect);

				if (hoverRect.Contains(mp))
				{
					double a = min + i * width;
					double b = (i == bins - 1) ? max : (min + (i + 1) * width);

					string unitsSuffix = string.IsNullOrEmpty(units) ? "" : $" {units}";
					string bracket = (i == bins - 1) ? "[, ]" : "[, )";

					hovered =
						$"Range {bracket}: {FormatNum(a)} \u2013 {FormatNum(b)}{unitsSuffix}\nCount: {counts[i]}";

					EditorGUIUtility.AddCursorRect(hoverRect, MouseCursor.Link);
				}
			}

			Handles.EndGUI();
			return hovered;
		}


		private void DrawTooltipOverlay(string tooltip)
		{
			if (Event.current.type != EventType.Repaint)
				return;

			if (string.IsNullOrEmpty(tooltip))
				return;

			var content = new GUIContent(tooltip);

			const float maxWidth = 340f;
			float width = Mathf.Min(maxWidth, TooltipStyle.CalcSize(content).x);
			float height = TooltipStyle.CalcHeight(content, width);

			// Add padding already in style; just keep some extra breathing room
			width += 4f;
			height += 4f;

			Vector2 mp = Event.current.mousePosition;

			// Offset so the tooltip doesn't sit under the cursor
			float x = mp.x + 18f;
			float y = mp.y + 18f;

			var r = new Rect(x, y, width, height);

			// Clamp to window bounds (and flip if needed)
			var bounds = new Rect(0, 0, position.width, position.height);
			if (r.xMax > bounds.xMax) r.x = mp.x - 18f - r.width;
			if (r.yMax > bounds.yMax) r.y = mp.y - 18f - r.height;
			r.x = Mathf.Clamp(r.x, bounds.xMin, bounds.xMax - r.width);
			r.y = Mathf.Clamp(r.y, bounds.yMin, bounds.yMax - r.height);

			// Solid background + border
			var bg = EditorGUIUtility.isProSkin
				? new Color(0.12f, 0.12f, 0.12f, 0.98f)
				: new Color(0.95f, 0.95f, 0.95f, 0.98f);

			EditorGUI.DrawRect(r, bg);
			Handles.BeginGUI();

			Handles.color = EditorGUIUtility.isProSkin
				? new Color(1f, 1f, 1f, 0.15f)
				: new Color(0f, 0f, 0f, 0.15f);

			Handles.DrawAAPolyLine(1f,
				new Vector3(r.xMin, r.yMin),
				new Vector3(r.xMax, r.yMin),
				new Vector3(r.xMax, r.yMax),
				new Vector3(r.xMin, r.yMax),
				new Vector3(r.xMin, r.yMin));

			Handles.EndGUI();

			GUI.Label(r, content, TooltipStyle);
		}


		private static string FormatNum(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);

		// Quantile on sorted data (linear interpolation, p in [0,1])
		private static double QuantileSorted(double[] sorted, double p)
		{
			if (sorted == null || sorted.Length == 0)
				return double.NaN;

			if (sorted.Length == 1)
				return sorted[0];

			p = Math.Max(0.0, Math.Min(1.0, p));
			double pos = p * (sorted.Length - 1);
			int i0 = (int)Math.Floor(pos);
			int i1 = Math.Min(i0 + 1, sorted.Length - 1);
			double t = pos - i0;

			return sorted[i0] * (1.0 - t) + sorted[i1] * t;
		}

		private static long NiceIntStep(long rawStep)
		{
			if (rawStep <= 1)
				return 1;

			double x = rawStep;
			double exp = Math.Floor(Math.Log10(x));
			double pow = Math.Pow(10.0, exp);
			double f = x / pow;

			double niceF =
				(f <= 1.0) ? 1.0 :
				(f <= 2.0) ? 2.0 :
				(f <= 5.0) ? 5.0 : 10.0;

			long step = (long)Math.Round(niceF * pow);
			return Math.Max(1, step);
		}

		// Floor division that behaves for negative numbers too
		private static long FloorDiv(long a, long b)
		{
			if (b <= 0)
				throw new ArgumentOutOfRangeException(nameof(b));

			if (a >= 0)
				return a / b;

			return -(((-a) + b - 1) / b);
		}


		public static bool ShouldUseIntegerAlignedBins(
			IReadOnlyList<double> samples,
			double absTolerance = 1e-9,
			double relTolerance = 1e-12,
			double requiredFraction = 0.99,
			int minSamples = 10)
		{
			if (samples == null || samples.Count == 0)
				return false;

			int n = 0;
			int nearInt = 0;

			for (int i = 0; i < samples.Count; i++)
			{
				double x = samples[i];
				if (double.IsNaN(x) || double.IsInfinity(x))
					continue;

				n++;
				double r = Math.Round(x);
				double absR = Math.Abs(x - r);
				double tolerance = Math.Max(absTolerance, relTolerance * Math.Max(1.0, Math.Abs(x)));

				if (absR <= tolerance)
					nearInt++;
			}

			if (n < minSamples)
				return nearInt == n && n > 0;

			return (nearInt / (double)n) >= requiredFraction;
		}
	}
}