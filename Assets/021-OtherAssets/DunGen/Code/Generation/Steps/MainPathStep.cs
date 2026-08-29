using DunGen.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that constructs the main path of a dungeon layout
	/// </summary>
	/// <remarks>This step determines the sequence of rooms and connections that form the primary route through the
	/// dungeon, ensuring that all required nodes are included and that the main path adheres to the target length
	/// constraints. The step may retry tile placements if conflicts occur, and will terminate early
	/// if a valid main path cannot be constructed.</remarks>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class MainPathStep : IGenerationStep
	{
		/// <summary>
		/// The maximum number of attempts to place a tile in a given slot before considering it a failure.
		/// </summary>
		public int MaxAttemptsPerSlot = 20;

		/// <summary>
		/// How many steps backwards the builder can backtrack when encountering placement issues.
		/// </summary>
		public int MaxBacktrackSlots = 5;

		/// <summary>
		/// The maximum number of backtracks allowed during path building. A value of 0 indicates no backtracking is allowed.
		/// When backtracking is enabled, the builder may revisit previous placements to find alternative configurations if it encounters
		/// placement issues further along the path.
		/// </summary>
		public int MaxTotalBacktracks = 20;


		public virtual IEnumerator Execute(GenerationContext context)
		{
			var settings = context.Request.Settings;

			int nextNodeIndex = 0;
			GraphLine previousLineSegment = null;
			DungeonArchetype currentArchetype = null;

			var handledNodes = new List<GraphNode>(settings.DungeonFlow.Nodes.Count);
			bool isDone = false;
			int i = 0;

			var placementSlots = new List<TilePlacementParameters>(context.TargetLength);
			var slotTileSets = new List<List<TileSet>>();

			// We can't rigidly stick to the target length since we need at least one room for each node and that might be more than targetLength
			while (!isDone)
			{
				float depth = Mathf.Clamp(i / (float)(context.TargetLength - 1), 0, 1);
				GraphLine lineSegment = settings.DungeonFlow.GetLineAtDepth(depth);

				// This should never happen
				if (lineSegment == null)
				{
					context.StepResult = GenerationStepResult.Failure($"Settings.DungeonFlow returned a null line segment at depth {depth}. This should never happen");
					yield break;
				}

				// We're on a new line segment, change the current archetype
				if (lineSegment != previousLineSegment)
				{
					currentArchetype = lineSegment.GetRandomArchetype(context.RandomStream, placementSlots.Select(x => x.Archetype));
					previousLineSegment = lineSegment;
				}

				List<TileSet> useableTileSets = null;
				GraphNode nextNode = null;
				var orderedNodes = settings.DungeonFlow.Nodes.OrderBy(x => x.Position).ToArray();

				// Determine which node comes next
				foreach (var node in orderedNodes)
				{
					if (depth >= node.Position && !handledNodes.Contains(node))
					{
						nextNode = node;
						handledNodes.Add(node);
						break;
					}
				}

				var placementParams = new TilePlacementParameters();
				placementSlots.Add(placementParams);

				// Assign the TileSets to use based on whether we're on a node or a line segment
				if (nextNode != null)
				{
					useableTileSets = nextNode.TileSets;
					nextNodeIndex = (nextNodeIndex >= orderedNodes.Length - 1) ? -1 : nextNodeIndex + 1;
					placementParams.Node = nextNode;

					if (nextNode == orderedNodes[orderedNodes.Length - 1])
						isDone = true;
				}
				else
				{
					useableTileSets = currentArchetype.TileSets;
					placementParams.Archetype = currentArchetype;
					placementParams.Line = lineSegment;
				}

				slotTileSets.Add(useableTileSets);
				i++;
			}

			// Build main path using backtracking rather than manual removal/retry
			var pathBuilderOptions = new PathBuilder.OptionsBuilder()
				.OnFailure(PathBuilder.FailureBehaviour.Fail)
				.MaxAttemptsPerSlot(MaxAttemptsPerSlot)
				.MaxBacktrackSlots(MaxBacktrackSlots)
				.MaxTotalBacktracks(MaxTotalBacktracks)
				.Build();

			var builder = new PathBuilder(pathBuilderOptions);

			TileProxy previousTile = null;
			int slotCount = placementSlots.Count;

			for (int j = 0; j < slotCount; j++)
			{
				var tileSetsForSlot = slotTileSets[j];
				var placementParamsForSlot = placementSlots[j];
				float normalizedDepth = j / (float)(slotCount - 1);

				var candidateTiles = tileSetsForSlot.SelectMany(x => x.Tiles.Entries);

				builder.ProposeSlot(new PathBuilder.SlotSpec(
					candidateEntries: candidateTiles,
					isOnMainPath: true,
					normalizedDepth: normalizedDepth,
					placementParameters: placementParamsForSlot,
					onTilePlaced: (tile) =>
					{
						tile.Placement.PlacementParameters = placementParamsForSlot;
						tile.IsRequired = true;
						previousTile = tile;
					}));
			}

			yield return builder.Build(context);

			if (context.ProxyDungeon.MainPathTiles.Count != slotCount)
			{
				context.StepResult = GenerationStepResult.Failure("Failed to place a tile on the main path");
				yield break;
			}
		}
	}
}