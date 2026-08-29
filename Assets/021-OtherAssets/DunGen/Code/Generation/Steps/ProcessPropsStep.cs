using DunGen.Weighting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that handles the placement and activation of local and
	/// global prop components within a dungeon during procedural generation
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class ProcessPropsStep : IGenerationStep
	{
		#region Helpers

		public struct PropProcessingData
		{
			public RandomProp PropComponent;
			public int HierarchyDepth;
			public Tile OwningTile;
		}

		#endregion


		public virtual IEnumerator Execute(GenerationContext context)
		{
			ProcessLocalProps(context);
			ProcessGlobalProps(context);
			yield break;
		}

		protected virtual void ProcessLocalProps(GenerationContext context)
		{
			static void GetHierarchyDepth(Transform transform, ref int depth)
			{
				if (transform.parent != null)
				{
					depth++;
					GetHierarchyDepth(transform.parent, ref depth);
				}
			}

			var props = context.Dungeon.gameObject.GetComponentsInChildren<RandomProp>();
			var propData = new List<PropProcessingData>();

			foreach (var prop in props)
			{
				int depth = 0;
				GetHierarchyDepth(prop.transform, ref depth);

				propData.Add(new PropProcessingData()
				{
					PropComponent = prop,
					HierarchyDepth = depth,
					OwningTile = prop.GetComponentInParent<Tile>()
				});
			}

			// Order by hierarchy depth to ensure a parent prop group is processed before its children
			propData = propData
				.OrderBy(x => x.HierarchyDepth)
				.ToList();

			var spawnedObjects = new List<GameObject>();

			for (int i = 0; i < propData.Count; i++)
			{
				var data = propData[i];

				if (data.PropComponent == null)
					continue;

				spawnedObjects.Clear();
				data.PropComponent.Process(context.RandomStream, data.OwningTile, ref spawnedObjects);

				// Gather any spawned sub-props and insert them into the list to be processed too
				var spawnedSubProps = spawnedObjects
					.SelectMany(x => x.GetComponentsInChildren<RandomProp>())
					.Distinct();

				foreach (var subProp in spawnedSubProps)
				{
					propData.Insert(i + 1, new PropProcessingData()
					{
						PropComponent = subProp,
						HierarchyDepth = data.HierarchyDepth + 1,
						OwningTile = data.OwningTile
					});
				}
			}
		}

		protected virtual void ProcessGlobalProps(GenerationContext context)
		{
			var globalPropWeights = new Dictionary<int, WeightedTable<GameObject>>();

			foreach (var tile in context.Dungeon.AllTiles)
			{
				foreach (var prop in tile.GetComponentsInChildren<GlobalProp>(true))
				{
					if (!globalPropWeights.TryGetValue(prop.PropGroupID, out var table))
					{
						table = new WeightedTable<GameObject>();
						globalPropWeights[prop.PropGroupID] = table;
					}

					float weight = tile.Placement.IsOnMainPath ? prop.MainPathWeight : prop.BranchPathWeight;
					weight *= prop.DepthWeightScale.Evaluate(tile.Placement.NormalizedDepth);

					table.Entries.Add(new WeightedEntry<GameObject>
					{
						Value = prop.gameObject,
						MainPathWeight = weight
					});
				}
			}

			foreach (var chanceTable in globalPropWeights.Values)
				foreach (var weight in chanceTable.Entries)
					weight.Value.SetActive(false);

			var selectionContext = new WeightedTable<GameObject>.SelectionContext()
			{
				IsOnMainPath = true,
				AllowNullSelection = false,
				NormalizedPathDepth = 0f,
				NormalizedBranchDepth = 0f,
			};

			var settings = context.Request.Settings;

			foreach (var pair in globalPropWeights)
			{
				var propSettingsWithID = settings.DungeonFlow.GlobalProps
					.Where(x => x != null && x.ID == pair.Key);

				// No matching prop settings found for this ID
				if (propSettingsWithID.Count() == 0)
					continue;

				// Dungeon flow contains multiple entries for this prop ID
				if (propSettingsWithID.Count() > 1)
					Debug.LogWarning($"Dungeon Flow contains multiple entries for the global prop group ID: {pair.Key}. Only the first entry will be used.");

				var prop = propSettingsWithID.First();

				var weights = new WeightedTable<GameObject>(pair.Value);
				int propCount = prop.Count.GetRandom(context.RandomStream);
				propCount = Mathf.Min(propCount, weights.Entries.Count);

				for (int i = 0; i < propCount; i++)
				{
					if (weights.TrySelectRandom(out var chosenEntry, context.RandomStream, selectionContext, removeFromTable: true))
						chosenEntry.SetActive(true);
				}
			}
		}
	}
}