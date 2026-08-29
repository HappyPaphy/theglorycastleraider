using DunGen.Utility;
using DunGen.Versioning;
using DunGen.Weighting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// A set of tiles with weights to be picked from at random
    /// </summary>
    [Serializable]
    [CreateAssetMenu(menuName = "DunGen/Tile Set", order = 700)]
    public sealed class TileSet : VersionedScriptableObject
    {
        #region Legacy Properties

        [HideInInspector]
        [Obsolete("Obsolete in 2.19. Use Tiles instead")]
        public GameObjectChanceTable TileWeights = new GameObjectChanceTable();

        #endregion

        public override int LatestVersion => 1;
        public override int DataVersion { get => fileVersion; set => fileVersion = value; }

        public WeightedTable<GameObject> Tiles = new WeightedTable<GameObject>();
        public List<LockedDoorwayAssociation> LockPrefabs = new List<LockedDoorwayAssociation>();

        [SerializeField, HideInInspector]
        private int fileVersion;

        public void AddTile(GameObject tilePrefab, float mainPathWeight, float branchPathWeight)
        {
            Tiles.Entries.Add(new WeightedEntry<GameObject>()
            {
                Value = tilePrefab,
                MainPathWeight = mainPathWeight,
                BranchPathWeight = branchPathWeight
            });
        }

        public void AddTiles(IEnumerable<GameObject> tilePrefab, float mainPathWeight, float branchPathWeight)
        {
            foreach (var tile in tilePrefab)
                AddTile(tile, mainPathWeight, branchPathWeight);
        }

        protected override void OnMigrate()
        {
#pragma warning disable CS0618 // Type or member is obsolete

            // Migrate to WeightedTable<GameObject>
            if (DataVersion < 1)
            {
                MigrationHelpers.GameObjectChanceToWeightedTable(TileWeights, ref Tiles);

                foreach (var lockPrefab in LockPrefabs)
                    MigrationHelpers.GameObjectChanceToWeightedTable(lockPrefab.LockPrefabs, ref lockPrefab.Prefabs);
            }

#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
