using DunGen.Utility;
using DunGen.Weighting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
	public enum LocalPropSetCountMode
	{
		Random,
		DepthBased,
		DepthMultiply,
	}

	public delegate int GetPropCountDelegate(LocalPropSet propSet, RandomStream randomStream, Tile tile);

	[AddComponentMenu("DunGen/Random Props/Local Prop Set")]
	[HelpURL("https://dungen-docs.aegongames.com/advanced-features/props-variety/#local-prop-set")]
	public class LocalPropSet : RandomProp
	{
		private static readonly Dictionary<LocalPropSetCountMode, GetPropCountDelegate> GetCountMethods = new Dictionary<LocalPropSetCountMode, GetPropCountDelegate>();

		public override int LatestVersion => 1;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		#region Legacy Properties

		[AcceptGameObjectTypes(GameObjectFilter.Scene)]
		[SerializeField]
		[Obsolete("Obsolete in 2.19. Use PropWeights property instead")]
		protected GameObjectChanceTable Props = new GameObjectChanceTable();

		#endregion

		[GameObjectWeightFilter(allowPrefabAssets: false, allowSceneObjects: true)]
		public WeightedTable<GameObject> PropWeights = new WeightedTable<GameObject>();
		public IntRange PropCount = new IntRange(1, 1);
		public LocalPropSetCountMode CountMode;
		public AnimationCurve CountDepthCurve = AnimationCurve.Linear(0, 0, 1, 1);

		[SerializeField]
		private int fileVersion;


		public override void Process(RandomStream randomStream, Tile tile, ref List<GameObject> spawnedObjects)
		{
			var propTable = new WeightedTable<GameObject>(PropWeights);

			if (!GetCountMethods.TryGetValue(CountMode, out var getCountDelegate))
				throw new NotImplementedException("LocalPropSet count mode \"" + CountMode + "\" is not yet implemented");

			int count = getCountDelegate(this, randomStream, tile);
			var toKeep = new List<GameObject>(count);

			var context = new WeightedTable<GameObject>.SelectionContext
			{
				IsOnMainPath = tile.Placement.IsOnMainPath,
				NormalizedPathDepth = tile.Placement.NormalizedPathDepth,
				NormalizedBranchDepth = tile.Placement.NormalizedBranchDepth,
				AllowImmediateRepeats = true,
				PreviouslyChosen = null,
				AllowNullSelection = true
			};

			for (int i = 0; i < count; i++)
			{
				// allowNullSelection is true so that we can treat empty entries as "no prop"
				if (propTable.TrySelectRandom(out var chosenEntry, randomStream,
					context, removeFromTable: true))
				{
					if (chosenEntry != null)
						toKeep.Add(chosenEntry);
				}
			}

			foreach (var entry in PropWeights.Entries)
			{
				if (entry.Value == null)
					continue;

				bool isActive = toKeep.Contains(entry.Value);
				entry.Value.SetActive(isActive);
			}
		}

		protected override void OnMigrate()
		{
#pragma warning disable 0618
			// Migrate Props to PropWeights
			if (DataVersion < 1)
			{
				MigrationHelpers.GameObjectChanceToWeightedTable(Props, ref PropWeights);
			}
#pragma warning restore 0618
		}

		#region GetCount Methods

		static LocalPropSet()
		{
			GetCountMethods[LocalPropSetCountMode.Random] = GetCountRandom;
			GetCountMethods[LocalPropSetCountMode.DepthBased] = GetCountDepthBased;
			GetCountMethods[LocalPropSetCountMode.DepthMultiply] = GetCountDepthMultiply;
		}

		private static int GetCountRandom(LocalPropSet propSet, RandomStream randomStream, Tile tile)
		{
			int count = propSet.PropCount.GetRandom(randomStream);
			count = Mathf.Clamp(count, 0, propSet.PropWeights.Entries.Count);

			return count;
		}

		private static int GetCountDepthBased(LocalPropSet propSet, RandomStream randomStream, Tile tile)
		{
			float curveValue = Mathf.Clamp(propSet.CountDepthCurve.Evaluate(tile.Placement.NormalizedPathDepth), 0, 1);
			int count = Mathf.RoundToInt(Mathf.Lerp(propSet.PropCount.Min, propSet.PropCount.Max, curveValue));

			return count;
		}

		private static int GetCountDepthMultiply(LocalPropSet propSet, RandomStream randomStream, Tile tile)
		{
			float curveValue = Mathf.Clamp(propSet.CountDepthCurve.Evaluate(tile.Placement.NormalizedPathDepth), 0, 1);
			int count = GetCountRandom(propSet, randomStream, tile);
			count = Mathf.RoundToInt(count * curveValue);

			return count;
		}

		#endregion
	}
}
