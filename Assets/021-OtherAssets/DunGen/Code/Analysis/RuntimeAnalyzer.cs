using DunGen.Generation;
using DunGen.Graph;
using DunGen.Versioning;
using System;
using System.Text;
using UnityEngine;

namespace DunGen.Analysis
{
	public delegate void RuntimeAnalyzerDelegate(RuntimeAnalyzer analyzer);
	public delegate void RuntimeAnalysisCompleteDelegate(RuntimeAnalyzer analyzer, AnalysisResults results);
	public delegate void AnalysisUpdatedDelegate(RuntimeAnalyzer analyzer, GenerationAnalysis analysis, GenerationStats generationStats, int currentIteration, int totalIterations);

	[AddComponentMenu("DunGen/Analysis/Runtime Analyzer")]
	[HelpURL("https://dungen-docs.aegongames.com/troubleshooting/analysis/#runtime-analyzer-component")]
	public sealed class RuntimeAnalyzer : VersionedMonoBehaviour
	{
		#region Legacy Properties

		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public DungeonFlow DungeonFlow;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public GenerationPipeline PipelineOverride;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public int Iterations = 100;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public int MaxFailedAttempts = 20;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public bool RunOnStart = true;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public float MaximumAnalysisTime = 0;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public AnalysisGenerationSettings.SeedMode SeedGenerationMode = AnalysisGenerationSettings.SeedMode.Random;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public int Seed = 0;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public bool ClearDungeonOnCompletion = true;
		[HideInInspector, Obsolete("Deprecated in 2.19. Generation settings are now accessed through the new 'Settings' property")]
		public bool AllowTilePooling = false;

		#endregion

		public override int LatestVersion => 1;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		[SerializeField, HideInInspector]
		private int fileVersion;

		// Events
		public static event RuntimeAnalyzerDelegate AnalysisStarted;
		public static event RuntimeAnalysisCompleteDelegate AnalysisComplete;
		public static event AnalysisUpdatedDelegate AnalysisUpdated;

		public AnalysisGenerationSettings Settings = new AnalysisGenerationSettings();
		public int CurrentIterations { get { return targetIterations - remainingIterations; } }

		private DungeonGenerator generator = new DungeonGenerator();
		private GenerationAnalysis analysis;
		private readonly StringBuilder infoText = new StringBuilder();
		private bool finishedEarly;
		private bool prevShouldRandomizeSeed;
		private int targetIterations;

		private int remainingIterations;
		private bool generateNextFrame;
		private int currentSeed;
		private RandomStream randomStream;

		// GUI
		private Vector2 scrollPos;

		private const float MetricColumnWidth = 320f;
		private const float NumberColumnWidth = 110f;
		private const float StdDevColumnWidth = 130f;
		private const float ColumnSpacing = 6f;

		private GUIStyle numberStyle;
		private GUIStyle headerNumberStyle;


		private void Start()
		{
			if (Settings.RunOnStart)
				RunAnalysis();
		}

		[Obsolete("Deprecated in 2.18. Use RunAnalysis() instead")]
		public void Analyze() => RunAnalysis();

		public void RunAnalysis()
		{
			bool isValid = false;

			if (Settings.DungeonFlow == null)
				Debug.LogError("No DungeonFlow assigned to analyser");
			else if (Settings.Iterations <= 0)
				Debug.LogError("Iteration count must be greater than 0");
			else if (Settings.MaxFailedAttempts <= 0)
				Debug.LogError("Max failed attempt count must be greater than 0");
			else
				isValid = true;

			if (!isValid)
				return;

			AnalysisStarted?.Invoke(this);
			prevShouldRandomizeSeed = generator.Settings.ShouldRandomizeSeed;

			generator.IsAnalysis = true;
			generator.Settings.DungeonFlow = Settings.DungeonFlow;
			generator.Settings.PipelineOverride = Settings.PipelineOverride;
			generator.Settings.MaxAttemptCount = Settings.MaxFailedAttempts;
			generator.Settings.ShouldRandomizeSeed = false;
			generator.AllowTilePooling = Settings.AllowTilePooling;

			analysis = new GenerationAnalysis(Settings);
			remainingIterations = targetIterations = Settings.Iterations;


			randomStream = new RandomStream(Settings.Seed);
			generator.OnGenerationStatusChanged += OnGenerationStatusChanged;

			analysis.OnAnalysisStarted();
			GenerateNext();
		}

		private void GenerateNext()
		{
			switch (Settings.SeedGenerationMode)
			{
				case AnalysisGenerationSettings.SeedMode.Random:
					currentSeed = randomStream.Next();
					break;
				case AnalysisGenerationSettings.SeedMode.Incremental:
					currentSeed++;
					break;
				case AnalysisGenerationSettings.SeedMode.Fixed:
					currentSeed = Settings.Seed;
					break;
			}

			generator.Settings.Seed = currentSeed;
			generator.Generate(new DungeonGenerationRequest(generator.Settings));
		}

		private void Update()
		{
			if (Settings.MaximumAnalysisTime > 0 && analysis.GetCurrentAnalysisTimeSeconds() >= Settings.MaximumAnalysisTime)
			{
				remainingIterations = 0;
				finishedEarly = true;
			}

			if (generateNextFrame)
			{
				generateNextFrame = false;
				GenerateNext();
			}
		}

		private void CompleteAnalysis()
		{
			analysis.OnAnalysisEnded();

			if (Settings.ClearDungeonOnCompletion)
				UnityUtil.Destroy(generator.Root);

			OnAnalysisComplete();
			AnalysisComplete?.Invoke(this, analysis.Results);
		}

		private void OnGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
		{
			if (status != GenerationStatus.Complete && status != GenerationStatus.Failed)
				return;

			bool success = status == GenerationStatus.Complete;

			if (success)
				analysis.OnDungeonGenerated(generator.CurrentDungeon, generator.GenerationStats);
			else
				analysis.OnDungeonGenerationFailed(generator.GenerationStats);

			AnalysisUpdated?.Invoke(this, analysis, generator.GenerationStats, CurrentIterations, targetIterations);

			remainingIterations--;

			if (remainingIterations <= 0)
			{
				generator.OnGenerationStatusChanged -= OnGenerationStatusChanged;
				CompleteAnalysis();
			}
			else
				generateNextFrame = true;
		}

		private void OnAnalysisComplete()
		{
			generator.Settings.ShouldRandomizeSeed = prevShouldRandomizeSeed;
			infoText.Length = 0;
			scrollPos = Vector2.zero;

			if (finishedEarly)
				infoText.AppendLine("[ Reached maximum analysis time before the target number of iterations was reached ]");

			infoText.AppendFormat("Iterations: {0}, Max Failed Attempts: {1}", (finishedEarly) ? analysis.Results.IterationCount : analysis.Settings.Iterations, Settings.MaxFailedAttempts);
			infoText.AppendFormat("\nTotal Analysis Time: {0:0.00} seconds", analysis.Results.TotalAnalysisTimeMs / 1000f);
			infoText.AppendFormat("\nDungeons successfully generated: {0}% ({1} failed)", Mathf.RoundToInt(analysis.Results.SuccessPercentage), analysis.Settings.Iterations - analysis.Results.SuccessCount);

			if (Settings.LogMessagesToConsole)
			{
				foreach (var message in analysis.Results.Messages)
					message.LogToConsole();
			}
		}

		private static readonly GUIContent MinHeader = new GUIContent("Min");
		private static readonly GUIContent MaxHeader = new GUIContent("Max");
		private static readonly GUIContent MeanHeader = new GUIContent("Mean");
		private static readonly GUIContent MedianHeader = new GUIContent("Median");
		private static readonly GUIContent StdDevHeader = new GUIContent("Std Deviation");

		private void EnsureStyles()
		{
			numberStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleRight,
				wordWrap = false,
				richText = false
			};

			headerNumberStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleRight,
				wordWrap = false,
				richText = false
			};
		}

		private void DrawMetricsTable(AnalysisResults results, int decimals)
		{
			EnsureStyles();

			if (results == null || results.Metrics == null || results.Metrics.Count == 0)
			{
				GUILayout.Label("[ No metrics available ]");
				return;
			}

			if (decimals < 0)
				decimals = 0;

			string floatFormat = "N" + decimals;

			var keys = new System.Collections.Generic.List<string>(results.Metrics.Keys);
			keys.Sort(StringComparer.Ordinal);

			using (new GUILayout.VerticalScope())
			{
				using (new GUILayout.HorizontalScope())
				{
					GUILayout.Label("Metric", GUILayout.Width(MetricColumnWidth));
					GUILayout.Space(ColumnSpacing);
					GUILayout.Label(MinHeader, headerNumberStyle, GUILayout.Width(NumberColumnWidth));
					GUILayout.Space(ColumnSpacing);
					GUILayout.Label(MaxHeader, headerNumberStyle, GUILayout.Width(NumberColumnWidth));
					GUILayout.Space(ColumnSpacing);
					GUILayout.Label(MeanHeader, headerNumberStyle, GUILayout.Width(NumberColumnWidth));
					GUILayout.Space(ColumnSpacing);
					GUILayout.Label(MedianHeader, headerNumberStyle, GUILayout.Width(NumberColumnWidth));
					GUILayout.Space(ColumnSpacing);
					GUILayout.Label(StdDevHeader, headerNumberStyle, GUILayout.Width(StdDevColumnWidth));
				}

				GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));

				string previousKey = null;

				for (int k = 0; k < keys.Count; k++)
				{
					string key = keys[k];
					var metric = results.Metrics[key];
					var data = metric.Data;

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
					{
						using (new GUILayout.HorizontalScope())
						{
							GUILayout.Label(new string(' ', i * 2) + parts[i], GUILayout.Width(MetricColumnWidth));
						}
					}

					string metricName = new string(' ', (parts.Length - 1) * 2) + parts[parts.Length - 1];
					string unitsLabel = string.IsNullOrEmpty(metric.UnitsLabel) ? "" : $" ({metric.UnitsLabel})";
					metricName += unitsLabel;

					string minText = data.HasSamples ? data.Min.ToString(floatFormat) : "-";
					string maxText = data.HasSamples ? data.Max.ToString(floatFormat) : "-";
					string meanText = data.HasSamples ? data.Mean.ToString(floatFormat) : "-";
					string medianText = data.HasSamples ? data.Median.ToString(floatFormat) : "-";
					string stdDevText = data.HasSamples ? data.StandardDeviation.ToString(floatFormat) : "-";

					using (new GUILayout.HorizontalScope())
					{
						GUILayout.Label(metricName, GUILayout.Width(MetricColumnWidth));
						GUILayout.Space(ColumnSpacing);

						GUILayout.Label(minText, numberStyle, GUILayout.Width(NumberColumnWidth));
						GUILayout.Space(ColumnSpacing);

						GUILayout.Label(maxText, numberStyle, GUILayout.Width(NumberColumnWidth));
						GUILayout.Space(ColumnSpacing);

						GUILayout.Label(meanText, numberStyle, GUILayout.Width(NumberColumnWidth));
						GUILayout.Space(ColumnSpacing);

						GUILayout.Label(medianText, numberStyle, GUILayout.Width(NumberColumnWidth));
						GUILayout.Space(ColumnSpacing);

						GUILayout.Label(stdDevText, numberStyle, GUILayout.Width(StdDevColumnWidth));
					}

					previousKey = key;
				}
			}
		}

		private void OnGUI()
		{
			if (analysis == null)
				return;

			if (infoText == null || infoText.Length == 0)
			{
				string failedGenerationsCountText = (analysis.Results.SuccessCount < analysis.Results.IterationCount) ? ("\nFailed Dungeons: " + (analysis.Results.IterationCount - analysis.Results.SuccessCount).ToString()) : "";

				GUILayout.Label(string.Format("Analysing... {0} / {1} ({2:0.0}%){3}", CurrentIterations, targetIterations, (CurrentIterations / (float)targetIterations) * 100, failedGenerationsCountText));
				return;
			}

			GUILayout.Label(infoText.ToString());

			scrollPos = GUILayout.BeginScrollView(scrollPos);
			DrawMetricsTable(analysis.Results, 1);
			GUILayout.EndScrollView();
		}

#pragma warning disable CS0618 // Type or member is obsolete
		protected override void OnMigrate()
		{
			// Migrate legacy properties to the new Settings property
			if (DataVersion < 1)
			{
				Settings ??= new AnalysisGenerationSettings();

				Settings.DungeonFlow = DungeonFlow;
				Settings.Iterations = Iterations;
				Settings.MaxFailedAttempts = MaxFailedAttempts;
				Settings.RunOnStart = RunOnStart;
				Settings.MaximumAnalysisTime = MaximumAnalysisTime;
				Settings.SeedGenerationMode = SeedGenerationMode;
				Settings.Seed = Seed;
				Settings.ClearDungeonOnCompletion = ClearDungeonOnCompletion;
				Settings.AllowTilePooling = AllowTilePooling;
			}
		}
#pragma warning restore CS0618 // Type or member is obsolete
	}
}