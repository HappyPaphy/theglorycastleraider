using DunGen.Generation.Steps;
using DunGen.Services;
using DunGen.Versioning;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation
{
	/// <summary>
	/// Defines a configurable pipeline for procedural dungeon generation
	/// </summary>
	/// <remarks>Steps are run in a pre-defined order and cannot be changed, but can be swapped out for
	/// new derived types to override the functionality of each step in the pipeline</remarks>
	[CreateAssetMenu(fileName = "GenerationPipeline", menuName = "DunGen/Generation Pipeline", order = 1)]
	public sealed class GenerationPipeline : VersionedScriptableObject
	{
		#region Nested Type

		public sealed class PipelineStep
		{
			public GenerationStatus Status { get; private set; }
			public IGenerationStep Handler { get; private set; }


			public PipelineStep(GenerationStatus status, IGenerationStep handler)
			{
				Status = status;
				Handler = handler;
			}
		}

		#endregion

		public override int DataVersion { get => fileVersion; set => fileVersion = value; }
		public override int LatestVersion => 1;

		/// <summary>
		/// A collection of services used for dungeon generation
		/// </summary>
		public DungeonGenerationServices Services = new DungeonGenerationServices();


		[SerializeReference, SubclassSelector(allowNone: false)]
		public TileInjectionStep TileInjectionStep = new TileInjectionStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public PreProcessingStep PreProcessingStep = new PreProcessingStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public MainPathStep MainPathStep = new MainPathStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public BranchingStep BranchingStep = new BranchingStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public BranchPruningStep BranchPruningStep = new BranchPruningStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public ValidateRequiredTilesStep ValidateRequiredTilesStep = new ValidateRequiredTilesStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public FinaliseLayoutStep FinaliseLayoutStep = new FinaliseLayoutStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public InstantiateTilesStep InstantiateTilesStep = new InstantiateTilesStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public ProcessPropsStep ProcessPropsStep = new ProcessPropsStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public LockAndKeyPlacementStep LockAndKeyPlacementStep = new LockAndKeyPlacementStep();

		[SerializeReference, SubclassSelector(allowNone: false)]
		public PostProcessingStep PostProcessingStep = new PostProcessingStep();

		public List<ExtensionStepEntry> ExtensionSteps = new List<ExtensionStepEntry>();

		[SerializeField]
		private int fileVersion = 1;


		public List<PipelineStep> BuildPipelineSteps()
		{
			var extensionBuckets = ExtensionSteps
				.Where(e => e.Enabled && e.Step != null)
				.GroupBy(e => e.Anchor)
				.ToDictionary(g => g.Key, g => g.OrderBy(e => e.Order).ToList());

			IEnumerable<PipelineStep> GetExtensionStepsForAnchor(PipelineAnchor a)
			{
				if (!extensionBuckets.TryGetValue(a, out var list))
					yield break;

				foreach (var extension in list)
					yield return new PipelineStep(GenerationStatus.Custom, extension.Step);
			}

			var pipelineSteps = new List<PipelineStep>();

			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.BeforeAll));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.TileInjection, TileInjectionStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterTileInjection));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.PreProcessing, PreProcessingStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterPreProcessing));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.MainPath, MainPathStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterMainPath));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.Branching, BranchingStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterBranching));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.BranchPruning, BranchPruningStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterBranchPruning));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.ValidateRequiredTiles, ValidateRequiredTilesStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterValidateRequiredTiles));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.FinaliseLayout, FinaliseLayoutStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterFinaliseLayout));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.InstantiatingTiles, InstantiateTilesStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterInstantiateTiles));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.PropProcessing, ProcessPropsStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterPropProcessing));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.LockAndKeyPlacement, LockAndKeyPlacementStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterLockAndKeyPlacement));
			pipelineSteps.Add(new PipelineStep(GenerationStatus.PostProcessing, PostProcessingStep));
			pipelineSteps.AddRange(GetExtensionStepsForAnchor(PipelineAnchor.AfterPostProcessing));

			return pipelineSteps;
		}

		protected override void OnMigrate() { }
	}
}
