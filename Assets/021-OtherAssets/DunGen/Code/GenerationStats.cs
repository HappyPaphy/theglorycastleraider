using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DunGen
{
	public sealed class GenerationStats
	{
		#region Nested Types

		public sealed class TileStatistics
		{
			public Tile TilePrefab { get; set; } = null;
			public int TotalCount { get; set; } = 0;
			public int FromPoolCount { get; set; } = 0;
		}

		#endregion

		public int MainPathRoomCount { get; private set; }
		public int BranchPathRoomCount { get; private set; }
		public int TotalRoomCount { get; private set; }
		public int MaxBranchDepth { get; private set; }
		public int TotalRetries { get; private set; }

		public int PrunedBranchTileCount { get; internal set; }

		public Dictionary<GenerationStatus, float> GenerationStepTimes { get; private set; }
		public Dictionary<string, float> PostProcessStepTimes { get; private set; }
		public float TotalTime => GenerationStepTimes.Values.Sum();

		private Stopwatch stopwatch = new Stopwatch();
		private GenerationStatus generationStatus;
		private readonly Dictionary<Tile, TileStatistics> tileStatistics = new Dictionary<Tile, TileStatistics>();


		public GenerationStats()
		{
			GenerationStepTimes = new Dictionary<GenerationStatus, float>();
			PostProcessStepTimes = new Dictionary<string, float>();
		}

		public float GetGenerationStepTime(GenerationStatus step)
		{
			if (GenerationStepTimes.TryGetValue(step, out float time))
				return time;
			else
				return 0f;
		}

		public float GetPostProcessStepTime(string stepName)
		{
			if (PostProcessStepTimes.TryGetValue(stepName, out float time))
				return time;
			else
				return 0f;
		}

		public IEnumerable<TileStatistics> GetTileStatistics() => tileStatistics.Values;

		public void TileAdded(Tile tilePrefab, bool fromPool)
		{
			if (!tileStatistics.TryGetValue(tilePrefab, out TileStatistics stats))
			{
				stats = new TileStatistics
				{
					TilePrefab = tilePrefab
				};
				tileStatistics.Add(tilePrefab, stats);
			}

			stats.TotalCount++;

			if (fromPool)
				stats.FromPoolCount++;
		}

		internal void Clear()
		{
			MainPathRoomCount = 0;
			BranchPathRoomCount = 0;
			TotalRoomCount = 0;
			MaxBranchDepth = 0;
			TotalRetries = 0;
			PrunedBranchTileCount = 0;
			GenerationStepTimes.Clear();
			PostProcessStepTimes.Clear();
			tileStatistics.Clear();
		}

		internal void IncrementRetryCount()
		{
			TotalRetries++;
		}

		internal void SetRoomStatistics(int mainPathRoomCount, int branchPathRoomCount, int maxBranchDepth)
		{
			MainPathRoomCount = mainPathRoomCount;
			BranchPathRoomCount = branchPathRoomCount;
			MaxBranchDepth = maxBranchDepth;
			TotalRoomCount = MainPathRoomCount + BranchPathRoomCount;
		}

		internal void BeginTime(GenerationStatus status)
		{
			if (stopwatch.IsRunning)
				EndTime();

			generationStatus = status;
			stopwatch.Reset();
			stopwatch.Start();
		}

		internal void EndTime()
		{
			stopwatch.Stop();
			float elapsedTime = (float)stopwatch.Elapsed.TotalMilliseconds;

			GenerationStepTimes.TryGetValue(generationStatus, out float currentTime);
			currentTime += elapsedTime;

			GenerationStepTimes[generationStatus] = currentTime;
		}

		public GenerationStats Clone()
		{
			GenerationStats newStats = new GenerationStats();

			newStats.MainPathRoomCount = MainPathRoomCount;
			newStats.BranchPathRoomCount = BranchPathRoomCount;
			newStats.TotalRoomCount = TotalRoomCount;
			newStats.MaxBranchDepth = MaxBranchDepth;
			newStats.TotalRetries = TotalRetries;
			newStats.PrunedBranchTileCount = PrunedBranchTileCount;
			newStats.GenerationStepTimes = new Dictionary<GenerationStatus, float>(GenerationStepTimes);

			return newStats;
		}
	}
}
