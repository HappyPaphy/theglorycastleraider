using DunGen.Weighting;
using UnityEngine;

namespace DunGen.Utility
{
#pragma warning disable CS0618 // Type or member is obsolete
	public static class MigrationHelpers
	{
		public static void GameObjectChanceToWeightedTable(GameObjectChanceTable legacyTable, ref WeightedTable<GameObject> newTable)
		{
			newTable ??= new WeightedTable<GameObject>();

			foreach (var entry in legacyTable.Weights)
			{
				if (entry == null)
					continue;

				var newEntry = new WeightedEntry<GameObject>(entry.Value)
				{
					MainPathWeight = entry.MainPathWeight,
					BranchPathWeight = entry.BranchPathWeight,
					DepthWeightScale = entry.DepthWeightScale,
					DepthScalingMode = DepthScalingMode.Auto
				};

				newTable.Entries.Add(newEntry);
			}
		}
	}
#pragma warning restore CS0618 // Type or member is obsolete
}