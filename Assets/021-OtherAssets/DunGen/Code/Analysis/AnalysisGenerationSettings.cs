using DunGen.Analysis.Modules;
using DunGen.Generation;
using DunGen.Graph;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Analysis
{
	[Serializable]
	public sealed class AnalysisGenerationSettings
	{
		public enum SeedMode
		{
			Random,
			Incremental,
			Fixed,
		}

		public DungeonFlow DungeonFlow;
		public GenerationPipeline PipelineOverride;
		public int Iterations = 100;
		public int MaxFailedAttempts = 20;
		public bool RunOnStart = true;
		public float MaximumAnalysisTime = 0;
		public SeedMode SeedGenerationMode = SeedMode.Random;
		public int Seed = 0;
		public bool ClearDungeonOnCompletion = true;
		public bool AllowTilePooling = false;
		public bool LogMessagesToConsole = false;

		[SerializeReference, SubclassSelector(allowNone: false)]
		public List<IGenerationAnalysisModule> AnalysisModules = new List<IGenerationAnalysisModule>();


		public AnalysisGenerationSettings()
		{
			AnalysisModules = new List<IGenerationAnalysisModule>()
			{
				new GenerationStatsAnalysisModule(),
				new PathLengthsAnalysisModule(),
				new TilePrefabUsageAnalysisModule()
			};
		}
	}
}