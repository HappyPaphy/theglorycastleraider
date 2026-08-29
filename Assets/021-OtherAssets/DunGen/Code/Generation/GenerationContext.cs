using DunGen.PostProcessing;
using DunGen.Services;
using DunGen.Weighting;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Generation
{
	/// <summary>
	/// Provides contextual information and state used during the dungeon generation process
	/// </summary>
	/// <remarks>This class encapsulates all relevant data, services, and intermediate results required for
	/// procedural dungeon generation. It is created at the start of a generation operation and passed to the various
	/// steps in the generation pipeline</remarks>
	public sealed class GenerationContext
	{
		/// <summary>
		/// A collection of services available during dungeon generation
		/// </summary>
		public DungeonGenerationServices Services { get; private set; }

		/// <summary>
		/// The original request that initiated the dungeon generation process
		/// </summary>
		public DungeonGenerationRequest Request { get; private set; }

		/// <summary>
		/// Statistics collected during the dungeon generation process
		/// </summary>
		public GenerationStats GenerationStats { get; private set; }

		/// <summary>
		/// If true, the current generation is part of an analysis operation in which many dungeons are
		/// generated to gather statistical data
		/// </summary>
		public bool IsAnalysis { get; private set; }

		/// <summary>
		/// The result of the current generation step
		/// </summary>
		public GenerationStepResult StepResult { get; set; }

		public RandomStream RandomStream;
		public int ChosenSeed;
		public DungeonProxy ProxyDungeon;
		public Dungeon Dungeon;
		public int TargetLength;

		public List<InjectedTile> TilesPendingInjection = new List<InjectedTile>();
		public readonly List<TilePlacementResult> TilePlacementResults = new List<TilePlacementResult>();
		public readonly Dictionary<TileProxy, InjectedTile> InjectedTiles = new Dictionary<TileProxy, InjectedTile>();
		public readonly Dictionary<WeightedEntry<GameObject>, TileSet> TileSetLookup = new Dictionary<WeightedEntry<GameObject>, TileSet>();
		public List<PostProcessHook> PostProcessHooks = new List<PostProcessHook>();


		public GenerationContext(DungeonGenerationServices services, DungeonGenerationRequest request, bool isAnalysis)
		{
			Services = services;
			Request = request;
			IsAnalysis = isAnalysis;

			GenerationStats = new GenerationStats();
		}
	}
}
