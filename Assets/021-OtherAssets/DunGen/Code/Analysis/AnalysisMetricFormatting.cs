using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DunGen.Analysis
{
	public static class AnalysisMetricFormatting
	{
		/// <summary>
		/// Generates a tab-separated table containing summary statistics for each metric
		/// </summary>
		/// <remarks>The returned table includes one row per metric, sorted alphabetically by metric name. Each value
		/// is separated by a tab character, making the output suitable for use in spreadsheet applications or for further
		/// processing.</remarks>
		/// <returns>A string representing the metrics table, with each row containing the metric name, count, minimum, maximum, mean,
		/// median, and standard deviation. The first row contains column headers.</returns>
		public static string MetricsToTabSeparatedTable(this Dictionary<string, AnalysisResults.Metric> metrics)
		{
			string header = "Metric\tCount\tMin\tMax\tMean\tMedian\tStdDev";

			var keys = new List<string>(metrics.Keys);
			keys.Sort();

			string output = header;

			foreach (var key in keys)
			{
				var metric = metrics[key];
				var data = metric.Data;

				string unitsLabel = string.IsNullOrEmpty(metric.UnitsLabel) ? "" : $" ({metric.UnitsLabel})";
				string keyLabel = key + unitsLabel;
				output += $"\n{keyLabel}\t{data.Count()}\t{data.Min}\t{data.Max}\t{data.Mean}\t{data.Median}\t{data.StandardDeviation}";
			}

			return output;
		}

		/// <summary>
		/// Generates a formatted, indented table representing the current set of metrics and their statistical values
		/// </summary>
		/// <remarks>Metric names are grouped and indented based on dot-separated hierarchy. The table automatically
		/// adjusts column widths for alignment based on the data</remarks>
		/// <param name="decimals">The number of decimal places to display for numeric values</param>
		/// <returns>A string containing an indented table of metrics with columns for count, minimum, maximum, mean, median, and
		/// standard deviation. Returns "[ No metrics available ]" if there are no metrics to display.</returns>
		public static string MetricsToIndentedTable(this Dictionary<string, AnalysisResults.Metric> metrics, int decimals = 2)
		{
			if (metrics == null || metrics.Count == 0)
				return "[ No metrics available ]";

			if (decimals < 0)
				decimals = 0;

			const int indentSize = 2;

			string[] labels =
			{
				"Count",
				"Min",
				"Max",
				"Mean",
				"Median",
				"Std Deviation",
			};

			string numberFormat = "N" + decimals;

			var keys = new List<string>(metrics.Keys);
			keys.Sort(StringComparer.Ordinal);

			var sb = new StringBuilder();

			int nameWidth = "Metric".Length;
			int countWidth = labels[0].Length;
			int minWidth = labels[1].Length;
			int maxWidth = labels[2].Length;
			int meanWidth = labels[3].Length;
			int medianWidth = labels[4].Length;
			int stdDevWidth = labels[5].Length;

			foreach (var key in keys)
			{
				var metric = metrics[key];
				var data = metric.Data;
				string[] parts = key.Split('.');

				parts[parts.Length - 1] = GetLabel(metric, parts[parts.Length - 1]);

				int nameLen = (parts.Length - 1) * indentSize + parts[parts.Length - 1].Length;

				if (nameLen > nameWidth)
					nameWidth = nameLen;

				string countText = data.Count().ToString();

				if (countText.Length > countWidth)
					countWidth = countText.Length;

				string minText = data.HasSamples ? data.Min.ToString(numberFormat) : "";

				if (minText.Length > minWidth)
					minWidth = minText.Length;

				string maxText = data.HasSamples ? data.Max.ToString(numberFormat) : "";

				if (maxText.Length > maxWidth)
					maxWidth = maxText.Length;

				string meanText = data.HasSamples ? data.Mean.ToString(numberFormat) : "";

				if (meanText.Length > meanWidth)
					meanWidth = meanText.Length;

				string medianText = data.HasSamples ? data.Median.ToString(numberFormat) : "";

				if (medianText.Length > medianWidth)
					medianWidth = medianText.Length;

				string stdDevText = data.HasSamples ? data.StandardDeviation.ToString(numberFormat) : "";

				if (stdDevText.Length > stdDevWidth)
					stdDevWidth = stdDevText.Length;
			}

			string header =
				"Metric".PadRight(nameWidth) + "  " +
				labels[0].PadLeft(countWidth) + "  " +
				labels[1].PadLeft(minWidth) + "  " +
				labels[2].PadLeft(maxWidth) + "  " +
				labels[3].PadLeft(meanWidth) + "  " +
				labels[4].PadLeft(medianWidth) + "  " +
				labels[5].PadLeft(stdDevWidth);

			sb.AppendLine(header);
			sb.AppendLine(new string('-', header.Length));

			string previousKey = null;

			string GetLabel(AnalysisResults.Metric metric, string name)
			{
				string unitsLabel = string.IsNullOrEmpty(metric.UnitsLabel) ? "" : $" ({metric.UnitsLabel})";
				return name + unitsLabel;
			}

			void AddGroupLine(string name, int depth)
			{
				sb.AppendLine(new string(' ', depth * indentSize) + name);
			}

			void AddMetricLine(string name, int depth, AnalysisResults.Metric metric)
			{
				var data = metric.Data;

				string left = (new string(' ', depth * indentSize) + GetLabel(metric, name)).PadRight(nameWidth);

				string countText = data.Count().ToString();
				string minText = data.HasSamples ? data.Min.ToString(numberFormat) : "";
				string maxText = data.HasSamples ? data.Max.ToString(numberFormat) : "";
				string meanText = data.HasSamples ? data.Mean.ToString(numberFormat) : "";
				string medianText = data.HasSamples ? data.Median.ToString(numberFormat) : "";
				string stdDevText = data.HasSamples ? data.StandardDeviation.ToString(numberFormat) : "";

				sb.AppendLine(
					left + "  " +
					countText.PadLeft(countWidth) + "  " +
					minText.PadLeft(minWidth) + "  " +
					maxText.PadLeft(maxWidth) + "  " +
					meanText.PadLeft(meanWidth) + "  " +
					medianText.PadLeft(medianWidth) + "  " +
					stdDevText.PadLeft(stdDevWidth));
			}

			foreach (var key in keys)
			{
				var data = metrics[key];

				string[] parts = key.Split('.');
				string[] prevParts = (previousKey == null) ? null : previousKey.Split('.');

				int common = 0;

				if (prevParts != null)
				{
					int max = Mathf.Min(prevParts.Length, parts.Length);

					while (common < max && prevParts[common] == parts[common])
						common++;
				}

				for (int i = common; i < parts.Length - 1; i++)
					AddGroupLine(parts[i], i);

				AddMetricLine(parts[parts.Length - 1], parts.Length - 1, data);
				previousKey = key;
			}

			return sb.ToString();
		}
	}
}