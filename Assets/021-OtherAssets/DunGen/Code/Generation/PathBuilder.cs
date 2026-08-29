using DunGen.Async;
using DunGen.TilePlacement;
using DunGen.Weighting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation
{
	public sealed class PathBuilder
	{
		public enum FailureBehaviour
		{
			Fail,
			ClearPath,
			KeepPartialPath,
		}

		public sealed class Options
		{
			public FailureBehaviour OnFailure { get; set; } = FailureBehaviour.Fail;
			public int MaxAttemptsPerSlot { get; set; } = 20;
			public int MaxBacktrackSlots { get; set; } = int.MaxValue;
			public int MaxTotalBacktracks { get; set; } = 200;
			public bool WeightedCandidateRandomTieBreak { get; set; } = true;
			public TileProxy AttachTo { get; set; } = null;
		}

		public sealed class OptionsBuilder
		{
			private readonly Options options = new Options();

			public OptionsBuilder OnFailure(FailureBehaviour behaviour) { options.OnFailure = behaviour; return this; }
			public OptionsBuilder MaxAttemptsPerSlot(int maxAttemptsPerSlot) { options.MaxAttemptsPerSlot = maxAttemptsPerSlot; return this; }
			public OptionsBuilder MaxBacktrackSlots(int maxBacktrackSlots) { options.MaxBacktrackSlots = maxBacktrackSlots; return this; }
			public OptionsBuilder MaxTotalBacktracks(int maxTotalBacktracks) { options.MaxTotalBacktracks = maxTotalBacktracks; return this; }
			public OptionsBuilder WeightedCandidateRandomTieBreak(bool tieBreakRandomly) { options.WeightedCandidateRandomTieBreak = tieBreakRandomly; return this; }
			public OptionsBuilder AttachTo(TileProxy attachTo) { options.AttachTo = attachTo; return this; }
			public Options Build() => options;
		}

		public readonly struct SlotSpec
		{
			public readonly IEnumerable<WeightedEntry<GameObject>> CandidateEntries;
			public readonly bool IsOnMainPath;
			public readonly float NormalizedDepth;
			public readonly TilePlacementParameters PlacementParameters;
			public readonly Action<TileProxy> OnTilePlaced;

			public SlotSpec(
				IEnumerable<WeightedEntry<GameObject>> candidateEntries,
				bool isOnMainPath,
				float normalizedDepth,
				TilePlacementParameters placementParameters,
				Action<TileProxy> onTilePlaced)
			{
				CandidateEntries = candidateEntries;
				IsOnMainPath = isOnMainPath;
				NormalizedDepth = normalizedDepth;
				PlacementParameters = placementParameters;
				OnTilePlaced = onTilePlaced;
			}
		}

		public sealed class BuildResult
		{
			public bool Success { get; internal set; }
			public string FailureReason { get; internal set; }
			public IReadOnlyList<TileProxy> Tiles { get; internal set; }
		}

		private sealed class SlotState
		{
			public SlotSpec Spec { get; }
			public List<WeightedEntry<GameObject>> Candidates { get; }
			public int Attempts { get; set; }


			public SlotState(SlotSpec spec)
			{
				Spec = spec;

				// Deterministic order: preserve incoming order, removing nulls and duplicates
				Candidates = new List<WeightedEntry<GameObject>>();
				var seen = new HashSet<WeightedEntry<GameObject>>();

				if (spec.CandidateEntries != null)
				{
					foreach (var entry in spec.CandidateEntries)
					{
						if (entry == null)
							continue;

						if (seen.Add(entry))
							Candidates.Add(entry);
					}
				}

				Attempts = 0;
			}
		}

		private readonly Options options;
		private readonly List<SlotState> slots = new List<SlotState>();

		private readonly List<TileProxy> placedTiles = new List<TileProxy>();
		private readonly Stack<int> placedSlotIndices = new Stack<int>();

		public PathBuilder(Options options = null)
		{
			this.options = options ?? new Options();
		}

		public void Clear()
		{
			slots.Clear();
			placedTiles.Clear();
			placedSlotIndices.Clear();
		}

		public void ProposeSlot(in SlotSpec spec)
		{
			slots.Add(new SlotState(spec));
		}

		public IEnumerator Build(GenerationContext context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			int totalBacktracks = 0;
			int i = 0;

			while (i < slots.Count)
			{
				var slot = slots[i];

				bool placed = false;
				while (!placed && slot.Attempts < options.MaxAttemptsPerSlot)
				{
					slot.Attempts++;

					TileProxy attachTo = (i == 0) ? options.AttachTo : placedTiles[placedTiles.Count - 1];

					var request = new TilePlacementRequest
					{
						AttachTo = attachTo,
						IsOnMainPath = slot.Spec.IsOnMainPath,
						NormalizedDepth = slot.Spec.NormalizedDepth,
						PlacementParameters = slot.Spec.PlacementParameters,
						CandidateTileEntries = slot.Candidates,
					};

					var tile = context.Services.TilePlacer.AddTile(context, request);

					if (tile != null)
					{
						placed = true;
						placedTiles.Add(tile);
						placedSlotIndices.Push(i);
						slot.Spec.OnTilePlaced?.Invoke(tile);

						yield return YieldSignal.RoomPlaced;
					}
				}

				if (placed)
				{
					i++;
					continue;
				}

				// Failed this slot, backtrack
				if (placedSlotIndices.Count == 0)
					break;

				totalBacktracks++;
				if (totalBacktracks > options.MaxTotalBacktracks)
					break;

				int lastPlacedSlotIndex = placedSlotIndices.Peek();
				int backtrackDistance = i - lastPlacedSlotIndex;
				if (backtrackDistance > options.MaxBacktrackSlots)
					break;

				int slotToRewindTo = placedSlotIndices.Pop();
				RollbackLastTile(context);

				slot.Attempts = 0;
				i = slotToRewindTo;
			}

			bool success = i == slots.Count;
			if (!success)
			{
				if (options.OnFailure == FailureBehaviour.Fail)
				{
					context.StepResult = GenerationStepResult.Failure("PathBuilder failed to build the full path");
					yield break;
				}

				if (options.OnFailure == FailureBehaviour.ClearPath)
					RollbackAll(context);
			}
		}

		public BuildResult BuildToCompletion(GenerationContext context)
		{
			var enumerator = Build(context);
			while (enumerator.MoveNext()) { }

			bool success = placedTiles.Count == slots.Count;
			return new BuildResult
			{
				Success = success,
				Tiles = placedTiles.ToArray(),
				FailureReason = success ? null : "Failed to build path",
			};
		}

		private void RollbackAll(GenerationContext context)
		{
			while (placedSlotIndices.Count > 0)
			{
				placedSlotIndices.Pop();
				RollbackLastTile(context);
			}
		}

		private void RollbackLastTile(GenerationContext context)
		{
			if (placedTiles.Count == 0)
				return;

			var tile = placedTiles[placedTiles.Count - 1];
			placedTiles.RemoveAt(placedTiles.Count - 1);

			if (tile == null)
				return;

			if (context.InjectedTiles != null && context.InjectedTiles.TryGetValue(tile, out InjectedTile injectedTile))
			{
				context.TilesPendingInjection.Add(injectedTile);
				context.InjectedTiles.Remove(tile);
			}

			context.ProxyDungeon.RemoveConnectionsToTile(tile);
			context.ProxyDungeon.RemoveTile(tile);
			context.Services.CollisionService.RemoveTile(tile);
		}
	}
}
