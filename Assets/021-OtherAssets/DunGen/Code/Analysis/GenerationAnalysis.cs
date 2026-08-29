using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DunGen.Analysis
{
	public class GenerationAnalysis
	{
		#region Static

		public static GenerationStatus[] MeasurableSteps { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void InitialiseStatics()
		{
			GenerationStatus[] ignoredSteps =
			{
				GenerationStatus.NotStarted,
				GenerationStatus.Complete,
				GenerationStatus.Failed
			};

			MeasurableSteps = Enum.GetValues(typeof(GenerationStatus))
				.Cast<GenerationStatus>()
				.Except(ignoredSteps)
				.ToArray();
		}

		#endregion

		/// <summary>
		/// Configuration settings for the analysis
		/// </summary>
		public AnalysisGenerationSettings Settings { get; private set; }

		/// <summary>
		/// The results of the analysis operation
		/// </summary>
		public AnalysisResults Results { get; private set; }

		protected Stopwatch analysisTimer;


		public GenerationAnalysis(AnalysisGenerationSettings settings)
		{
			Settings = UnityUtil.DeepCopy(settings);
		}

		/// <summary>
		/// Gets the elapsed analysis time, in seconds, since the analysis timer was started.
		/// </summary>
		/// <returns>The number of seconds that have elapsed since the analysis timer started. Returns 0 if the timer has not been
		/// started.</returns>
		public float GetCurrentAnalysisTimeSeconds() => (float)(analysisTimer?.Elapsed.TotalSeconds ?? 0f);

		/// <summary>
		/// Invoked when analysis is started to perform any necessary initialization.
		/// </summary>
		public virtual void OnAnalysisStarted()
		{
			Results = new AnalysisResults();
			analysisTimer = Stopwatch.StartNew();

			foreach (var module in Settings.AnalysisModules)
				module?.OnAnalysisStarted(Results);
		}

		/// <summary>
		/// Invoked when a dungeon has been successfully generated
		/// </summary>
		/// <param name="dungeon">The generated dungeon</param>
		/// <param name="stats">Stats about the generation process</param>
		public virtual void OnDungeonGenerated(Dungeon dungeon, GenerationStats stats)
		{
			Results.TotalAnalysisTimeMs += stats.TotalTime;
			Results.IterationCount++;
			Results.SuccessCount++;

			foreach (var module in Settings.AnalysisModules)
				module?.OnDungeonGenerated(Results, dungeon, stats);
		}

		/// <summary>
		/// Invoked when dungeon generation fails
		/// </summary>
		/// <param name="stats">Stats about the generation process</param>
		public virtual void OnDungeonGenerationFailed(GenerationStats stats)
		{
			Results.IterationCount++;

			foreach (var module in Settings.AnalysisModules)
				module?.OnDungeonGenerationFailed(Results, stats);
		}

		/// <summary>
		/// Invoked when the analysis is stopped
		/// </summary>
		public virtual void OnAnalysisEnded()
		{
			Results.TotalAnalysisTimeMs = (float)analysisTimer.Elapsed.TotalMilliseconds;
			analysisTimer.Stop();

			foreach (var module in Settings.AnalysisModules)
				module?.OnAnalysisEnded(Results);
		}

		[Obsolete("Deprecated in 2.18. Stats are now automatically computed once analysis ends")]
		public void Analyze() { }
	}
}

