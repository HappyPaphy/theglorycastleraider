using DunGen.Pooling;
using DunGen.Utility;
using DunGen.Weighting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
	[AddComponentMenu("DunGen/Random Props/Random Prefab")]
	[HelpURL("https://dungen-docs.aegongames.com/advanced-features/props-variety/#random-prefab")]
	public class RandomPrefab : RandomProp, ITileSpawnEventReceiver
	{
        #region Legacy Properties

        [AcceptGameObjectTypes(GameObjectFilter.Asset)]
		[Obsolete("Obsolete in 2.19. Use 'PropTable' instead")]
        public GameObjectChanceTable Props = new GameObjectChanceTable();

        #endregion

        public override int LatestVersion => 1;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		[GameObjectWeightFilter(allowPrefabAssets: true, allowSceneObjects: false)]
		public WeightedTable<GameObject> PropTable = new WeightedTable<GameObject>();
		public bool ZeroPosition = true;
		public bool ZeroRotation = true;

		private GameObject propInstance;

		[SerializeField]
		private int fileVersion;


		private void ClearExistingInstances()
		{
			if (propInstance == null)
				return;

			DestroyImmediate(propInstance);
			propInstance = null;
		}

		public override void Process(RandomStream randomStream, Tile tile, ref List<GameObject> spawnedObjects)
		{
			ClearExistingInstances();

			if (PropTable.Entries.Count <= 0)
				return;

			var selectionContext = new WeightedTable<GameObject>.SelectionContext
			{
				IsOnMainPath = tile.Placement.IsOnMainPath,
				NormalizedPathDepth = tile.Placement.NormalizedPathDepth,
				NormalizedBranchDepth = tile.Placement.NormalizedBranchDepth,
				AllowImmediateRepeats = true,
				PreviouslyChosen = null,
				AllowNullSelection = true
			};

			if (!PropTable.TrySelectRandom(out var prefab, randomStream, selectionContext))
				return;

			propInstance = Instantiate(prefab);
			propInstance.transform.parent = transform;

			spawnedObjects.Add(propInstance);

			if (ZeroPosition)
				propInstance.transform.localPosition = Vector3.zero;
			else
				propInstance.transform.localPosition = prefab.transform.localPosition;

			if (ZeroRotation)
				propInstance.transform.localRotation = Quaternion.identity;
			else
				propInstance.transform.localRotation = prefab.transform.localRotation;
		}

		protected override void OnMigrate()
		{
#pragma warning disable CS0618 // Type or member is obsolete

			// Migrate to WeightedTable<GameObject>
			if(DataVersion < 1)
			{
				MigrationHelpers.GameObjectChanceToWeightedTable(Props, ref PropTable);
            }

#pragma warning restore CS0618 // Type or member is obsolete
        }

		#region ITileSpawnEventReceiver

		public void OnTileSpawned(Tile tile) { }

		public void OnTileDespawned(Tile tile) => ClearExistingInstances();

		#endregion
	}
}