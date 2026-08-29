using System.Collections.Generic;

namespace DunGen.Analysis
{
	/// <summary>
	/// Results from a dungeon generation analysis session. Contains various metrics and
	/// messages (warnings and errors) collected during the analysis.
	/// </summary>
	public sealed class AnalysisResults
	{
		#region Nested Types

		public sealed class Metric
		{
			public NumberSetData Data { get; } = new NumberSetData();
			public string UnitsLabel { get; set; }
		}

		#endregion

		/// <summary>
		/// Total time taken to generate all dungeons during the analysis session, in milliseconds
		/// </summary>
		public float TotalAnalysisTimeMs { get; set; }

		/// <summary>
		/// The number of generation iterations performed during the analysis session
		/// </summary>
		public int IterationCount { get; set; }

		/// <summary>
		/// How many of the generation iterations were successful
		/// </summary>
		public int SuccessCount { get; set; }

		/// <summary>
		/// The percentage of successful generation iterations
		/// </summary>
		public float SuccessPercentage { get { return (SuccessCount / (float)IterationCount) * 100; } }

		/// <summary>
		/// Named metrics collected during the analysis session
		/// </summary>
		public readonly Dictionary<string, Metric> Metrics = new Dictionary<string, Metric>();

		/// <summary>
		/// Messages (warnings and errors) collected during the analysis session
		/// </summary>
		public readonly List<AnalysisMessage> Messages = new List<AnalysisMessage>();


		/// <summary>
		/// Adds a value to the named metric. If the metric does not exist yet, it will be created
		/// </summary>
		/// <param name="key">The key to assign the metric to</param>
		/// <param name="value">The value of the metric</param>
		/// <param name="unitsLabel">An optional label for the units of measurement for this metric</param>
		public void AddValue(string key, float value, string unitsLabel = null)
		{
			if (!Metrics.TryGetValue(key, out var metric))
			{
				metric = new Metric();
				Metrics[key] = metric;
			}

			metric.Data.Add(value);

			if (unitsLabel != null)
				metric.UnitsLabel = unitsLabel;
		}

		/// <summary>
		/// Adds a range of values to the named metric. If the metric does not exist yet, it will be created
		/// </summary>
		/// <param name="key">The key to assign the metric to</param>
		/// <param name="values">The value of the metric</param>
		/// <param name="unitsLabel">An optional label for the units of measurement for this metric</param>
		public void AddValues(string key, IEnumerable<double> values, string unitsLabel = null)
		{
			if (!Metrics.TryGetValue(key, out var metric))
			{
				metric = new Metric();
				Metrics[key] = metric;
			}

			metric.Data.AddRange(values);

			if (unitsLabel != null)
				metric.UnitsLabel = unitsLabel;
		}

		/// <summary>
		/// Retrieves all metric entries whose keys start with the specified path.
		/// </summary>
		/// <remarks>If no metric keys match the specified path, the returned dictionary will be empty. The returned
		/// keys are either the full key (if it exactly matches the path) or the substring following the path
		/// prefix.</remarks>
		/// <param name="path">The prefix path to match against metric keys.</param>
		/// <returns>A dictionary containing metric entries with keys that start with the specified path. The keys in the returned
		/// dictionary are relative to the provided path.</returns>
		public Dictionary<string, Metric> GetMetrics(string path)
		{
			var output = new Dictionary<string, Metric>();

			if (string.IsNullOrEmpty(path))
				return output;

			foreach (var kvp in Metrics)
			{
				if (kvp.Key.StartsWith(path))
				{
					string newKey;

					if (path.Length == kvp.Key.Length)
						newKey = kvp.Key;
					else
						newKey = kvp.Key.Substring(path.Length);

					output[newKey] = kvp.Value;
				}
			}

			return output;
		}

		public void Log(string message, UnityEngine.Object contextObject = null) => Messages.Add(new AnalysisMessage(AnalysisMessageSeverity.Info, message, contextObject));
		public void Warning(string message, UnityEngine.Object contextObject = null) => Messages.Add(new AnalysisMessage(AnalysisMessageSeverity.Warning, message, contextObject));
		public void Error(string message, UnityEngine.Object contextObject = null) => Messages.Add(new AnalysisMessage(AnalysisMessageSeverity.Error, message, contextObject));
	}
}