using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that prepares the set of tiles and tile sets to be used during dungeon generation.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class PreProcessingStep : IGenerationStep
	{
		public virtual IEnumerator Execute(GenerationContext context)
		{
			ClearCache(context);

			var usedTileSets = GatherRequiredTileSets(context);
			BuildLookupFromTileSets(context, usedTileSets);

			yield break;
		}

		/// <summary>
		/// Clears all cached tile set data from the specified generation context
		/// </summary>
		/// <param name="context">The generation context whose caches are to be cleared</param>
		protected virtual void ClearCache(GenerationContext context)
		{
			context.TileSetLookup.Clear();
		}

		/// <summary>
		/// Gathers all tile sets that are used or required for the current dungeon generation request
		/// </summary>
		/// <remarks>The returned collection includes both tile sets explicitly used by the dungeon flow and any
		/// additional tile sets scheduled for injection. Duplicate tile sets are removed from the result</remarks>
		/// <param name="context">The generation context containing the request and any pending tile set injections</param>
		/// <returns>An enumerable collection of tile sets that are referenced by the dungeon flow or are pending injection.
		/// The collection contains distinct tile sets only</returns>
		protected virtual IEnumerable<TileSet> GatherRequiredTileSets(GenerationContext context)
		{
			var settings = context.Request.Settings;

			// Gather all tile sets from the dungeon flow and
			// any that are to be injected into the generation
			return settings.DungeonFlow
				.GetUsedTileSets()
				.Concat(context.TilesPendingInjection.Select(x => x.TileSet))
				.Distinct();
		}

		/// <summary>
		/// Extracts all usable tiles from the specified tile sets and builds a lookup cache in the generation context
		/// </summary>
		/// <param name="context">The generation context where the caches reside</param>
		/// <param name="requiredTileSets">A distinct set of all of the tile sets that are potentially required
		/// for the generation process</param>
		protected virtual void BuildLookupFromTileSets(GenerationContext context, IEnumerable<TileSet> requiredTileSets)
		{
			// Gather all useable tiles from those tile sets
			// and build a lookup for which tile set each tile belongs to
			foreach (var tileSet in requiredTileSets)
			{
				if(tileSet == null)
				{
					Debug.LogWarning("A null tile set was found in the list of required tile sets. This may indicate a missing reference. Skipping this tile set.");
					continue;
				}

				foreach (var entry in tileSet.Tiles.Entries)
				{
					if (entry.Value != null)
						context.TileSetLookup[entry] = tileSet;
				}
			}
		}
	}
}