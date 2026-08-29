using DunGen.Editor.Validation;
using DunGen.Graph;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DunGen.Assets.DunGen.Code.Editor.Validation.Rules
{
    sealed class TileConfigurationRule : IValidationRule
    {
        public void Validate(DungeonFlow flow, DungeonValidator validator)
        {
            var tilePrefabs = flow.GetUsedTileSets()
                        .SelectMany(ts => ts.Tiles.Entries.Select(w => w.Value))
                        .Where(t => t != null)
                        .ToArray();

            CheckTileComponents(flow, validator, tilePrefabs);
            CheckTilemaps(flow, validator, tilePrefabs);
            CheckTerrains(flow, validator, tilePrefabs);
        }

        // Check to see if all of our tile prefabs have a Tile component
        private void CheckTileComponents(DungeonFlow flow, DungeonValidator validator, GameObject[] tilePrefabs)
        {
            foreach (var tileObj in tilePrefabs)
            {
                var tile = tileObj.GetComponent<Tile>();

                if (tile == null)
                    validator.AddWarning($"Tile prefab '{tileObj.name}' is missing a Tile component", tileObj);
            }
        }

        // Checks every tile and logs a warning if any have a Tilemap and are using automatic bounds calculations
        // Unity's tilemap doesn't have accurate bounds when first instantiated and so must use overridden tile bounds
        private void CheckTilemaps(DungeonFlow flow, DungeonValidator validator, IEnumerable<GameObject> tilePrefabs)
        {
            foreach (var tileObj in tilePrefabs)
            {
                var tile = tileObj.GetComponent<Tile>();

                if (tile == null || !tile.OverrideAutomaticTileBounds)
                {
                    var tilemap = tileObj.GetComponentInChildren<Tilemap>();

                    if (tilemap != null)
                        validator.AddWarning("[Tile: {0}] Automatic tile bounds don't work correctly with Unity's tilemaps. Check 'Override Automatic Tile Bounds' on your tile component and press the 'Fit to Tile' button", tileObj, tileObj.name);
                }
            }
        }

        // Unity terrain cannot be rotated, so we have to ensure tiles containing terrains are set to disallow rotation
        private void CheckTerrains(DungeonFlow flow, DungeonValidator validator, IEnumerable<GameObject> tilePrefabs)
        {
            foreach (var tileObj in tilePrefabs)
            {
                if (tileObj.GetComponentInChildren<Terrain>() == null)
                    continue;

                var tile = tileObj.GetComponent<Tile>();

                if (tile == null || tile.AllowRotation)
                    validator.AddError("[Tile: {0}] Tile contains a Unity terrain which cannot be rotated. The tile should have 'Allow Rotation' unchecked", tileObj, tileObj.name);
            }
        }
    }
}
