using DunGen.Generation;
using DunGen.Tags;
using DunGen.TilePlacement;
using DunGen.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace DunGen.Graph
{
	/// <summary>
	/// A graph representing the flow of a dungeon
	/// </summary>
	[Serializable]
	[CreateAssetMenu(fileName = "New Dungeon", menuName = "DunGen/Dungeon Flow", order = 700)]
	public class DungeonFlow : VersionedScriptableObject
	{
		public override int LatestVersion => 1;
		public override int DataVersion { get => currentFileVersion; set => currentFileVersion = value; }

		#region Nested Types

		[Serializable]
		public sealed class GlobalPropSettings
		{
			public int ID;
			public IntRange Count;


			public GlobalPropSettings()
			{
				ID = 0;
				Count = new IntRange(0, 1);
			}

			public GlobalPropSettings(int id, IntRange count)
			{
				ID = id;
				Count = count;
			}
		}

		/// <summary>
		/// Defines how tile connection rules are applied.
		/// </summary>
		public enum TagConnectionMode
		{
			/// <summary>
			/// Tiles will only connect if they have tags that appear in the TileConnectionTags list
			/// </summary>
			Accept,
			/// <summary>
			/// Tiles will always connect unless they have tags that appear in the TileConnectionTags list
			/// </summary>
			Reject,
		}

		/// <summary>
		/// Defines how tags are used to determine which branch tip tiles to prune
		/// </summary>
		public enum BranchPruneMode
		{
			/// <summary>
			/// Removes tiles at the end of a branch when they have any of the specified tags
			/// </summary>
			AnyTagPresent,
			/// <summary>
			/// Removes tiles at the end of a branch when they don't have any of the specified tags
			/// </summary>
			AllTagsMissing,
		}

		#endregion

		#region Legacy Properties

		[SerializeField]
		[FormerlySerializedAs("GlobalPropGroupIDs")]
		private List<int> globalPropGroupID_obsolete = new List<int>();

		[SerializeField]
		[FormerlySerializedAs("GlobalPropRanges")]
		private List<IntRange> globalPropRanges_obsolete = new List<IntRange>();

		#endregion

		/// <summary>
		/// The minimum and maximum length of the dungeon
		/// </summary>
		public IntRange Length = new IntRange(5, 10);
		/// <summary>
		/// Determines how the number of branches from the main path is calculated
		/// </summary>
		public BranchMode BranchMode = BranchMode.Local;
		/// <summary>
		/// The number of branches to appear across the entire dungeon
		/// Only used if <see cref="BranchMode"/> is set to <see cref="BranchMode.Global"/>
		/// </summary>
		public IntRange BranchCount = new IntRange(1, 5);
		/// <summary>
		/// Information about which (and how many) global props should appear throughout the dungeon
		/// </summary>
		public List<GlobalPropSettings> GlobalProps = new List<GlobalPropSettings>();
		/// <summary>
		/// The asset that handles all of the keys that this dungeon needs to know about
		/// </summary>
		public KeyManager KeyManager = null;
		/// <summary>
		/// The percentage chance of two unconnected but overlapping doorways being connected (0-1)
		/// </summary>
		[Range(0f, 1f)]
		public float DoorwayConnectionChance = 0f;
		/// <summary>
		/// If true, only doorways belonging to tiles on the same section of the dungeon can be connected
		/// This will prevent some unexpected shortcuts from opening up through the dungeon
		/// </summary>
		public bool RestrictConnectionToSameSection = false;
		/// <summary>
		/// Simple rules for injecting special tiles into the dungeon generation process
		/// </summary>
		public List<TileInjectionRule> TileInjectionRules = new List<TileInjectionRule>();
		/// <summary>
		/// Defines how tile connection rules are applied see <see cref="TagConnectionMode"/>
		/// </summary>
		public TagConnectionMode TileTagConnectionMode;
		/// <summary>
		/// A list of tag pairs that define how tiles are allowed to connect. If empty, all
		/// tiles can connect to one-another, otherwise the tiles must have a pair of
		/// matching tags from this list (omni-directional)
		/// </summary>
		public List<TagPair> TileConnectionTags = new List<TagPair>();
		/// <summary>
		/// Defines how doorway connection rules are applied see <see cref="TagConnectionMode"/>
		/// </summary>
		public TagConnectionMode DoorwayTagConnectionMode;
		/// <summary>
		/// If true, doorway socket types are ignored when processing the doorway connection rules.
		/// This allows rules to accept connections between doorways even with non-matching sockets
		/// </summary>
		public bool OverrideDoorwaySockets = false;
		/// <summary>
		/// A list of tag pairs that define how doorways are allowed to connect. If empty, all
		/// doorways can connect to one-another (assuming they have matching sockets), otherwise the doorways must have a pair of
		/// matching tags from this list (omni-directional)
		/// </summary>
		public List<TagPair> DoorwayConnectionTags = new List<TagPair>();
		/// <summary>
		/// Determines how tags are used to prune tiles at the end of branches
		/// </summary>
		public BranchPruneMode BranchTagPruneMode = BranchPruneMode.AllTagsMissing;
		/// <summary>
		/// A list of tags used to decide if a tile at the end of a branch should be deleted
		/// </summary>
		public List<Tag> BranchPruneTags = new List<Tag>();
		/// <summary>
		/// Global settings for straightening the dungeon path. Can be overridden in Archetypes and on Nodes in the flow graph
		/// </summary>
		public PathStraighteningSettings GlobalStraighteningSettings = new PathStraighteningSettings();
		/// <summary>
		/// An optional custom pipeline to use when generating this dungeon. If null, the default pipeline will be used.
		/// </summary>
		public GenerationPipeline CustomPipeline;

		public List<GraphNode> Nodes = new List<GraphNode>();
		public List<GraphLine> Lines = new List<GraphLine>();

		[SerializeField]
		private int currentFileVersion;


		/// <summary>
		/// Creates the default graph
		/// </summary>
		public override void Reset()
		{
			base.Reset();

			var emptyTileSet = new TileSet[0];
			var emptyArchetype = new DungeonArchetype[0];

			var builder = new DungeonFlowBuilder(this)
				.AddNode(emptyTileSet, "Start")
				.AddLine(emptyArchetype, 1.0f)
				.AddNode(emptyTileSet, "Goal");

			builder.Complete();
		}

		public GraphLine GetLineAtDepth(float normalizedDepth)
		{
			normalizedDepth = Mathf.Clamp(normalizedDepth, 0, 1);

			if (normalizedDepth == 0)
				return Lines[0];
			else if (normalizedDepth == 1)
				return Lines[Lines.Count - 1];

			foreach (var line in Lines)
				if (normalizedDepth >= line.Position && normalizedDepth < line.Position + line.Length)
					return line;

			Debug.LogError("GetLineAtDepth was unable to find a line at depth " + normalizedDepth + ". This shouldn't happen.");
			return null;
		}

		public DungeonArchetype[] GetUsedArchetypes()
		{
			return Lines.SelectMany(x => x.DungeonArchetypes).ToArray();
		}

		public TileSet[] GetUsedTileSets()
		{
			List<TileSet> tileSets = new List<TileSet>();

			foreach (var node in Nodes)
				tileSets.AddRange(node.TileSets);

			foreach (var line in Lines)
			{
				foreach (var archetype in line.DungeonArchetypes)
				{
					tileSets.AddRange(archetype.TileSets);
					tileSets.AddRange(archetype.BranchCapTileSets);
					tileSets.AddRange(archetype.BranchStartTileSets);
				}
			}

			return tileSets.ToArray();
		}

		public bool ShouldPruneBranchTipWithTags(TagContainer tileTags)
		{
			return BranchTagPruneMode switch
			{
				BranchPruneMode.AnyTagPresent => tileTags.HasAnyTag(BranchPruneTags.ToArray()),
				BranchPruneMode.AllTagsMissing => !tileTags.HasAnyTag(BranchPruneTags.ToArray()),
				_ => throw new NotImplementedException(string.Format("BranchPruneMode {0} is not implemented", BranchTagPruneMode)),
			};
		}

		protected override void OnMigrate()
		{
			// Convert to new format for Global Props
			if (DataVersion < 1)
			{
				for (int i = 0; i < globalPropGroupID_obsolete.Count; i++)
				{
					int id = globalPropGroupID_obsolete[i];
					var count = globalPropRanges_obsolete[i];
					GlobalProps.Add(new GlobalPropSettings(id, count));
				}

				globalPropGroupID_obsolete.Clear();
				globalPropRanges_obsolete.Clear();
			}
		}

		/// <summary>
		/// Checks the connection rules (if any) to see if two tiles are allowed
		/// to connect by checking their tags
		/// </summary>
		/// <param name="tileA">The first tile</param>
		/// <param name="tileB">The second tile</param>
		/// <returns>True if the tiles are allowed to connect</returns>
		public bool CanTilesConnect(Tile tileA, Tile tileB)
		{
			if (TileConnectionTags.Count == 0)
				return true;

			return TileTagConnectionMode switch
			{
				TagConnectionMode.Accept => HasMatchingTagPair(tileA, tileB),
				TagConnectionMode.Reject => !HasMatchingTagPair(tileA, tileB),
				_ => throw new NotImplementedException($"Tile tag connection mode {nameof(TagConnectionMode)}.{TileTagConnectionMode} is not implemented"),
			};
		}

		public bool CanDoorwaysConnect(ProposedConnection connection)
		{
			// Allow custom rules to handle this connection first.
			// If no custom rules handle the connection, fall back to the rules specified in the DungeonFlow UI
			foreach (var rule in DoorwayPairFinder.CustomConnectionRules)
			{
				TileConnectionRule.ConnectionResult result = TileConnectionRule.ConnectionResult.Passthrough;

				if (rule.EvaluateConnection != null)
					result = rule.EvaluateConnection(connection);

				if (result == TileConnectionRule.ConnectionResult.Passthrough)
					continue;
				else
					return result == TileConnectionRule.ConnectionResult.Allow;
			}

			// Tile Connection Rules
			bool tilesCanConnect = CanTilesConnect(connection.PreviousTile.PrefabTile, connection.NextTile.PrefabTile);

			if (!tilesCanConnect)
				return false;

			// Doorway Connection Rules
			bool doorwaysCanConnect;

			if (OverrideDoorwaySockets)
				doorwaysCanConnect = true;
			else
			{
				doorwaysCanConnect = DoorwaySocket.CanSocketsConnect(connection.PreviousDoorway.Socket, connection.NextDoorway.Socket);

				// Early exit if the doorway sockets can't connect
				if (!doorwaysCanConnect)
					return false;
			}

			if (DoorwayConnectionTags.Count > 0)
			{
				return DoorwayTagConnectionMode switch
				{
					TagConnectionMode.Accept => HasMatchingTagPair(connection.PreviousDoorway, connection.NextDoorway),
					TagConnectionMode.Reject => !HasMatchingTagPair(connection.PreviousDoorway, connection.NextDoorway),
					_ => throw new NotImplementedException($"Doorway tag connection mode {nameof(TagConnectionMode)}.{DoorwayTagConnectionMode} is not implemented"),
				};
			}

			return doorwaysCanConnect;
		}

		/// <summary>
		/// Modifies the weight of a proposed connection by applying custom connection rules.
		/// </summary>
		/// <remarks>This method iterates through all custom connection rules and applies any defined weight
		/// modification delegates to the provided connection. The final weight may be influenced by multiple rules, depending
		/// on their implementation.</remarks>
		/// <param name="connection">The proposed connection whose weight may be adjusted. Must not be null.</param>
		/// <param name="weight">A reference to the weight value to be modified based on custom rules.</param>
		public void ModifyConnectionWeight(ProposedConnection connection, ref float weight)
		{
			// Allow custom connection rules to modify the connection weight
			foreach (var rule in DoorwayPairFinder.CustomConnectionRules)
			{
				if (rule.AdjustConnectionWeight == null)
					continue;

				rule.AdjustConnectionWeight(connection, ref weight);
			}
		}

		private bool HasMatchingTagPair(Tile tileA, Tile tileB)
		{
			foreach (var tagPair in TileConnectionTags)
			{
				if ((tileA.Tags.HasTag(tagPair.TagA) && tileB.Tags.HasTag(tagPair.TagB)) ||
					(tileB.Tags.HasTag(tagPair.TagA) && tileA.Tags.HasTag(tagPair.TagB)))
					return true;
			}

			return false;
		}

		private bool HasMatchingTagPair(DoorwayProxy doorwayA, DoorwayProxy doorwayB)
		{
			foreach (var tagPair in DoorwayConnectionTags)
			{
				if ((doorwayA.Tags.HasTag(tagPair.TagA) && doorwayB.Tags.HasTag(tagPair.TagB)) ||
					(doorwayB.Tags.HasTag(tagPair.TagA) && doorwayA.Tags.HasTag(tagPair.TagB)))
					return true;
			}

			return false;
		}
	}
}
