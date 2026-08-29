using DunGen.Async;
using DunGen.Collision;
using DunGen.Common;
using DunGen.Generation;
using DunGen.Graph;
using DunGen.Placement;
using DunGen.PostProcessing;
using DunGen.TilePlacement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace DunGen
{
	public delegate void TileInjectionDelegate(RandomStream randomStream, ref List<InjectedTile> tilesToInject);
	public delegate void GenerationFailureReportProduced(DungeonGenerator generator, GenerationFailureReport report);

	[Serializable]
	public class DungeonGenerator : ISerializationCallbackReceiver
	{
		public const int CurrentFileVersion = 4;

		#region Legacy Properties

		// Legacy properties only exist to avoid breaking existing projects
		// Converting old data structures over to the new ones

		[SerializeField]
		[FormerlySerializedAs("AllowImmediateRepeats")]
		private bool allowImmediateRepeats = false;

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public float OverlapThreshold = 0.01f;

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public float Padding = 0f;

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public bool DisallowOverhangs = false;

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public bool AvoidCollisionsWithOtherDungeons = true;

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public readonly List<Bounds> AdditionalCollisionBounds = new List<Bounds>();

		[Obsolete("Use the 'CollisionSettings' property instead")]
		public AdditionalCollisionsPredicate AdditionalCollisionsPredicate { get; set; }

		[Obsolete("Use the 'TriggerPlacement' enum property instead")]
		public bool PlaceTileTriggers = true;

		[Obsolete("Use 'DebugRenderSettings' property instead")]
		public bool DebugRender = false;

		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public DungeonFlow DungeonFlow;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public int Seed;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool ShouldRandomizeSeed = true;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public int MaxAttemptCount = 20;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool UseMaximumPairingAttempts = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public int MaxPairingAttempts = 5;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public AxisDirection UpDirection = AxisDirection.PosY;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		[FormerlySerializedAs("OverrideAllowImmediateRepeats")]
		public bool OverrideRepeatMode = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public TileRepeatMode RepeatMode = TileRepeatMode.Allow;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool OverrideAllowTileRotation = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool AllowTileRotation = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public DebugRenderSettings DebugRenderSettings = new DebugRenderSettings();
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public float LengthMultiplier = 1.0f;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public TriggerPlacementMode TriggerPlacement = TriggerPlacementMode.ThreeDimensional;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `GeneratorSettings` instead")]
		public int TileTriggerLayer = 2;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool GenerateAsynchronously = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public float MaxAsyncFrameMilliseconds = 10;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public float PauseBetweenRooms = 0;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public bool RestrictDungeonToBounds = false;
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public Bounds TilePlacementBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
		[Obsolete("Deprecated in 2.19. Find the corresponding property in `Settings` instead")]
		public DungeonCollisionSettings CollisionSettings = new DungeonCollisionSettings();

		#endregion

		public DungeonGeneratorSettings Settings = new DungeonGeneratorSettings();

		public event GenerationStatusDelegate OnGenerationStatusChanged;
		public event GenerationStatusContextDelegate OnGenerationStatusChangedWithContext;
		public event DungeonGenerationDelegate OnGenerationStarted;
		[Obsolete("Deprecated in 2.19. Use OnDungeonGenerationComplete instead. This event still works for now, but will be removed in future versions")]
		public event DungeonGenerationDelegate OnGenerationComplete;
		public event DungeonGenerationCompleteDelegate OnDungeonGenerationComplete;
		public static event GenerationStatusDelegate OnAnyDungeonGenerationStatusChanged;
		public static event DungeonGenerationDelegate OnAnyDungeonGenerationStarted;
		public static event DungeonGenerationCompleteDelegate OnAnyDungeonGenerationComplete;
		public event Action Cleared;
		public event Action Retrying;
		public static event GenerationFailureReportProduced OnGenerationFailureReportProduced;

		public GameObject Root;
		public GenerationStatus Status { get; private set; }
		public int ChosenSeed { get; protected set; }
		public RandomStream RandomStream { get; protected set; }
		public Dungeon CurrentDungeon { get; private set; }
		public bool IsGenerating { get; private set; }
		public bool IsAnalysis { get; set; }
		public bool AllowTilePooling { get; set; }

		public DungeonCollisionManager CollisionManager { get; private set; }
		public CompositeDungeon CompositeDungeon { get; private set; } = new CompositeDungeon();

		// TODO: Find a better place to store attachment settings and generation stats so they're still widely accessible
		public DungeonAttachmentSettings AttachmentSettings => context.Request.AttachmentSettings;
		public GenerationStats GenerationStats => context.GenerationStats;

		protected GenerationPipeline generationPipeline;
		protected List<GenerationPipeline.PipelineStep> pipelineSteps;
		protected Coroutine generationCoroutine;
		protected GenerationContext context;
		protected readonly TileInstanceSource tileInstanceSource;
		protected List<PostProcessHook> postProcessHooks = new List<PostProcessHook>();

		[SerializeField]
		private int fileVersion;


		public DungeonGenerator()
		{
			AllowTilePooling = true;
			CollisionManager = new DungeonCollisionManager();

			tileInstanceSource = new TileInstanceSource();
			tileInstanceSource.TileInstanceSpawned += (tilePrefab, tileInstance, fromPool) =>
			{
				context.GenerationStats.TileAdded(tilePrefab, fromPool);
			};
		}

		public DungeonGenerator(GameObject root)
			: this()
		{
			Root = root;
		}

		public void Generate(DungeonGenerationRequest request)
		{
			if (IsGenerating)
				return;

			// Pick the generation pipeline to use in this order:
			// 1. Custom pipeline specified in the request settings
			// 2. Custom pipeline specified in the dungeon flow
			// 3. Default pipeline
			if (request.Settings.PipelineOverride != null)
				generationPipeline = UnityEngine.Object.Instantiate(request.Settings.PipelineOverride);
			else
			{
				var dungeonFlow = request.Settings.DungeonFlow;

				if (dungeonFlow != null && dungeonFlow.CustomPipeline != null)
					generationPipeline = UnityEngine.Object.Instantiate(dungeonFlow.CustomPipeline);
				else
					generationPipeline = ScriptableObject.CreateInstance<GenerationPipeline>();
			}

			context = new GenerationContext(generationPipeline.Services, request, IsAnalysis)
			{
				PostProcessHooks = postProcessHooks,
			};

			// TODO: Refactor collision service so it doesn't require a collision manager instance
			generationPipeline.Services.CollisionService.SetCollisionManager(CollisionManager);
			pipelineSteps = generationPipeline.BuildPipelineSteps();

			OnGenerationStarted?.Invoke(this);
			OnAnyDungeonGenerationStarted?.Invoke(this);

			// Detach the previous dungeon if we're generating the new one as an attachment
			// We need to do this to avoid overwriting the previous dungeon
			if (request.AttachmentSettings != null && CurrentDungeon != null)
				DetachDungeon();

			Settings.CollisionSettings ??= new DungeonCollisionSettings();
			CollisionManager ??= new DungeonCollisionManager();

			CollisionManager.Settings = Settings.CollisionSettings;
			DoorwayPairFinder.SortCustomRules();

			IsGenerating = true;
			generationCoroutine = CoroutineHelper.Start(Wait(GenerateRoutine()));
		}

		public void Cancel()
		{
			if (!IsGenerating)
				return;

			CoroutineHelper.Stop(generationCoroutine);
			generationCoroutine = null;

			ClearCurrentDungeon(true);
			IsGenerating = false;
		}

		public Dungeon DetachDungeon()
		{
			if (CurrentDungeon == null)
				return null;

			Dungeon dungeon = CurrentDungeon;
			CurrentDungeon = null;

			ClearCurrentDungeon(true);

			// If the dungeon is empty, we should just destroy it
			if (dungeon.transform.childCount == 0)
				UnityEngine.Object.DestroyImmediate(dungeon.gameObject);

			return dungeon;
		}

		protected virtual IEnumerator GenerateRoutine()
		{
			// We should clear the composite dungeon if we're generating a new dungeon from scratch
			if (context.Request.AttachmentSettings == null)
				ClearAllDungeons(false);
			else
				ClearCurrentDungeon(false);

			var yieldPolicy = context.Services.YieldPolicy;
			yieldPolicy.BeginRun();

			Status = GenerationStatus.NotStarted;
			context.TilePlacementResults.Clear();

#if UNITY_EDITOR
			// Validate the dungeon archetype if we're running in the editor
			DungeonArchetypeValidator validator = new DungeonArchetypeValidator(Settings.DungeonFlow);

			if (!validator.IsValid())
			{
				ChangeStatus(GenerationStatus.Failed);
				IsGenerating = false;
				yield break;
			}
#endif

			ChosenSeed = (Settings.ShouldRandomizeSeed) ? new RandomStream().Next() : Settings.Seed;
			RandomStream = new RandomStream(ChosenSeed);
			context.RandomStream = RandomStream;
			context.ChosenSeed = ChosenSeed;

			if (Root == null)
				Root = new GameObject(Constants.DefaultDungeonRootName);


			// Create dungeon
			var dungeonRoot = new GameObject(Constants.DefaultDungeonName);
			dungeonRoot.transform.SetParent(Root.transform, false);

			CurrentDungeon = dungeonRoot.AddComponent<Dungeon>();
			context.Dungeon = CurrentDungeon;

			// Initialise tile instance source
			bool enableTilePooling = AllowTilePooling && DunGenSettings.Instance.EnableTilePooling;
			tileInstanceSource.Initialise(enableTilePooling, dungeonRoot);

			CurrentDungeon.TileInstanceSource = tileInstanceSource;


			bool isRetry = false;
			int retryCount = 0;
			context.GenerationStats.Clear();

			while (true)
			{
				if (retryCount >= Settings.MaxAttemptCount && Application.isEditor)
				{
					// Generate a failure report if we're not running an analysis
					if (!IsAnalysis)
					{
						Debug.LogError(TilePlacementResult.ProduceReport(context.TilePlacementResults, Settings.MaxAttemptCount));
						OnGenerationFailureReportProduced?.Invoke(this, new GenerationFailureReport(Settings.MaxAttemptCount, context.TilePlacementResults));
					}

					ChangeStatus(GenerationStatus.Failed);
					break;
				}

				if (isRetry)
				{
					ChosenSeed = context.RandomStream.Next();
					RandomStream = new RandomStream(ChosenSeed);
					context.RandomStream = RandomStream;
					context.ChosenSeed = ChosenSeed;
					context.GenerationStats.IncrementRetryCount();
				}

				retryCount++;
				Retrying?.Invoke();

				yield return InnerGenerate(isRetry);

				// If the attempt (or a child routine called by it) completed or failed, we are done.
				if (Status == GenerationStatus.Complete || Status == GenerationStatus.Failed)
					break;

				// Otherwise, the attempt requested a retry. Loop again.
				isRetry = true;
			}

			IsGenerating = false;
		}

		private IEnumerator Wait(IEnumerator routine)
		{
			var yieldPolicy = context.Services.YieldPolicy;
			bool isAsync = context.Request.Settings.GenerateAsynchronously;

			while (routine.MoveNext())
			{
				var current = routine.Current;

				// Handle yield signals
				if (current is YieldSignal signal)
				{
					if (isAsync && yieldPolicy.ShouldYield(context, signal.Reason))
					{
						yield return yieldPolicy.GetYieldInstruction(context, signal.Reason);
						yieldPolicy.OnYielded();
					}

					continue;
				}

				// If async is disabled, swallow yields and keep going immediately
				if (!isAsync)
				{
					// If a step yielded a nested IEnumerator, run it to completion
					if (current is IEnumerator nested)
					{
						var nestedWait = Wait(nested);
						while (nestedWait.MoveNext()) { }
					}

					// Ignore null / WaitForSeconds / YieldInstruction / CustomYieldInstruction etc.
					continue;
				}

				// Async mode: pass through yields
				if (current is IEnumerator nestedAsync)
					yield return Wait(nestedAsync);
				else
					yield return current;

				// Budget check after any yield point
				if (yieldPolicy.ShouldYield(context, YieldReason.WorkBudget))
				{
					yield return yieldPolicy.GetYieldInstruction(context, YieldReason.WorkBudget);
					yieldPolicy.OnYielded();
				}
			}
		}

		protected virtual IEnumerator InnerGenerate(bool isRetry)
		{
			// TODO: Should all of this initialisation be moved to its own step?
			CollisionManager.Initialize(this);

			CurrentDungeon.DebugRenderSettings = Settings.DebugRenderSettings;

			ClearCurrentDungeon(false);
			context.TargetLength = Mathf.RoundToInt(Settings.DungeonFlow.Length.GetRandom(context.RandomStream) * Settings.LengthMultiplier);
			context.TargetLength = Mathf.Max(context.TargetLength, 2);

			Transform debugVisualsRoot = (Settings.PauseBetweenRooms > 0f) ? CurrentDungeon.transform : null;
			context.ProxyDungeon = new DungeonProxy(debugVisualsRoot);

			// Run Steps
			foreach (var step in pipelineSteps)
			{
				context.GenerationStats.BeginTime(step.Status);
				ChangeStatus(step.Status);

				context.StepResult = GenerationStepResult.Success();
				yield return step.Handler.Execute(context);

				// The previous step requested a retry
				if (!context.StepResult.IsSuccess)
					yield break;

				yield return YieldSignal.BetweenSteps;
			}

			// Inform objects in the dungeon that generation is complete
			foreach (var callbackReceiver in CurrentDungeon.gameObject.GetComponentsInChildren<IDungeonCompleteReceiver>(false))
				callbackReceiver.OnDungeonComplete(CurrentDungeon);

			if (context.Request.AttachmentSettings == null)
				CompositeDungeon.Clear();

			CompositeDungeon.AddDungeon(CurrentDungeon);
			ChangeStatus(GenerationStatus.Complete);

			bool charactersShouldRecheckTile = true;

#if UNITY_EDITOR
			charactersShouldRecheckTile = UnityEditor.EditorApplication.isPlaying;
#endif

			// Let DungenCharacters know that they should re-check the Tile they're in
			if (charactersShouldRecheckTile)
			{
				foreach (var character in UnityUtil.FindObjectsByType<DungenCharacter>())
					character.ForceRecheckTile();
			}
		}

		public virtual void ClearAllDungeons(bool stopCoroutines)
		{
			var dungeonsToClear = CompositeDungeon.Dungeons.ToArray();

			// Clear the composite dungeon first to avoid triggering events after we've started destroying GameObjects
			CompositeDungeon.Clear();

			foreach (var dungeon in dungeonsToClear)
			{
				if (dungeon != null)
				{
					dungeon.Clear();
					UnityEngine.Object.DestroyImmediate(dungeon.gameObject);
				}
			}

			ClearCurrentDungeon(stopCoroutines);
		}

		public virtual void ClearCurrentDungeon(bool stopCoroutines)
		{
			if (stopCoroutines)
				CoroutineHelper.StopAll();

			context.ProxyDungeon?.ClearDebugVisuals();

			context.ProxyDungeon = null;

			if (CurrentDungeon != null)
				CurrentDungeon.Clear();

			Cleared?.Invoke();
		}

		private void ChangeStatus(GenerationStatus status)
		{
			var previousStatus = Status;
			Status = status;

			if (status == GenerationStatus.Complete || status == GenerationStatus.Failed)
				IsGenerating = false;

			if (status == GenerationStatus.Failed)
				ClearCurrentDungeon(true);

			if (previousStatus != status)
			{
				OnGenerationStatusChanged?.Invoke(this, status);
				OnGenerationStatusChangedWithContext?.Invoke(this, status, context);
				OnAnyDungeonGenerationStatusChanged?.Invoke(this, status);

				if (status == GenerationStatus.Complete)
				{
					OnGenerationComplete?.Invoke(this);
					OnDungeonGenerationComplete?.Invoke(this, CurrentDungeon);
					OnAnyDungeonGenerationComplete?.Invoke(this, CurrentDungeon);
				}
			}
		}

		/// <summary>
		/// Registers a post-processing hook to be executed after dungeon generation.
		/// </summary>
		/// <remarks>Post-processing hooks are executed in order of descending priority. Registering multiple steps
		/// with the same priority will execute them in the order they were registered.</remarks>
		/// <param name="callback">An action to perform using the <see cref="GenerationContext"/> after generation is complete. Cannot be null.</param>
		/// <param name="priority">The priority of the post-processing hook. Hooks with higher priority values are executed first.</param>
		/// <returns>The registered <see cref="PostProcessHook"/></returns>
		public PostProcessHook RegisterPostProcessHook(Action<GenerationContext> callback, int priority = 0)
		{
			var hook = new PostProcessHook(callback, priority);
			postProcessHooks.Add(hook);

			return hook;
		}

		/// <summary>
		/// Unregisters a previously registered post-process hook so that it will no longer be invoked during
		/// generation.
		/// </summary>
		/// <param name="hook">The hook to remove from the list of post-process steps. Must not be null.</param>
		public void UnregisterPostProcessHook(PostProcessHook hook)
		{
			for (int i = 0; i < postProcessHooks.Count; i++)
				if (postProcessHooks[i] == hook)
					postProcessHooks.RemoveAt(i);
		}

		[Obsolete("Deprecated in 2.19. Use RegisterPostProcessHook instead")]
		public void RegisterPostProcessStep(Action<DungeonGenerator> postProcessCallback, int priority = 0) { }

		[Obsolete("Deprecated in 2.19. Use UnregisterPostProcessHook instead")]
		public void UnregisterPostProcessStep(Action<DungeonGenerator> postProcessCallback) { }

		#region ISerializationCallbackReceiver Implementation

		public void OnBeforeSerialize()
		{
			fileVersion = CurrentFileVersion;
		}

		public void OnAfterDeserialize()
		{
#pragma warning disable CS0618 // Type or member is obsolete

			// Upgrade to new repeat mode
			if (fileVersion < 1)
				RepeatMode = (allowImmediateRepeats) ? TileRepeatMode.Allow : TileRepeatMode.DisallowImmediate;

			// Moved collision properties to their own settings class
			if (fileVersion < 2)
			{
				CollisionSettings ??= new DungeonCollisionSettings();

				CollisionSettings.DisallowOverhangs = DisallowOverhangs;
				CollisionSettings.OverlapThreshold = OverlapThreshold;
				CollisionSettings.Padding = Padding;
				CollisionSettings.AvoidCollisionsWithOtherDungeons = AvoidCollisionsWithOtherDungeons;
			}

			// Trigger placement converted from bool to enum
			if (fileVersion < 3)
				TriggerPlacement = PlaceTileTriggers ? TriggerPlacementMode.ThreeDimensional : TriggerPlacementMode.None;

			// Settings moved to their own class
			if (fileVersion < 4)
			{
				Settings = new DungeonGeneratorSettings()
				{
					DungeonFlow = DungeonFlow,
					Seed = Seed,
					ShouldRandomizeSeed = ShouldRandomizeSeed,
					MaxAttemptCount = MaxAttemptCount,
					UseMaximumPairingAttempts = UseMaximumPairingAttempts,
					MaxPairingAttempts = MaxPairingAttempts,
					UpDirection = UpDirection,
					OverrideRepeatMode = OverrideRepeatMode,
					RepeatMode = RepeatMode,
					OverrideAllowTileRotation = OverrideAllowTileRotation,
					AllowTileRotation = AllowTileRotation,
					DebugRenderSettings = DebugRenderSettings,
					LengthMultiplier = LengthMultiplier,
					TriggerPlacement = TriggerPlacement,
					TileTriggerLayer = TileTriggerLayer,
					GenerateAsynchronously = GenerateAsynchronously,
					MaxAsyncFrameMilliseconds = MaxAsyncFrameMilliseconds,
					PauseBetweenRooms = PauseBetweenRooms,
					RestrictDungeonToBounds = RestrictDungeonToBounds,
					TilePlacementBounds = TilePlacementBounds,
					CollisionSettings = CollisionSettings,
				};
			}

#pragma warning restore CS0618 // Type or member is obsolete
		}

		#endregion
	}
}