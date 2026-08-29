using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that injects tiles into the dungeon layout according to configured rules and
	/// injectors.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class TileInjectionStep : IGenerationStep
	{
		/// <summary>
		/// A collection of tile injectors that will be used to add tiles to the generation context
		/// </summary>
		[SerializeReference, SubclassSelector]
		public List<ITileInjector> TileInjectors = new List<ITileInjector>();


		public virtual IEnumerator Execute(GenerationContext context)
		{
			ClearTileInjection(context);
			GatherTilesToInject(context);

			yield break;
		}

		/// <summary>
		/// Clears all tile injection data from the specified generation context
		/// </summary>
		/// <param name="context">The generation context to clear tile injection data from</param>
		protected virtual void ClearTileInjection(GenerationContext context)
		{
			context.TilesPendingInjection.Clear();
			context.InjectedTiles.Clear();
		}

		/// <summary>
		/// Gathers the set of tiles to be injected into the generation process from a variety of sources
		/// </summary>
		/// <param name="context">The current generation context containing configuration and state information for tile injection</param>
		protected virtual void GatherTilesToInject(GenerationContext context)
		{
			var injectionRandomStream = new RandomStream(context.ChosenSeed);

			GatherFromDungeonFlow(context, injectionRandomStream);
			GatherFromPipeline(context, injectionRandomStream);
			GatherFromRequest(context, injectionRandomStream);
		}

		/// <summary>
		/// Gathers tiles to be injected based on the tile injection rules defined in the <see cref="DunGen.Graph.DungeonFlow"/>
		/// </summary>
		/// <param name="context">The current generation context containing configuration and state information for tile injection</param>
		/// <param name="injectionRandomStream">The random stream to use to ensure determinism between runs</param>
		protected virtual void GatherFromDungeonFlow(GenerationContext context, RandomStream injectionRandomStream)
		{
			foreach (var rule in context.Request.Settings.DungeonFlow.TileInjectionRules)
			{
				// Ignore invalid rules
				if (rule.TileSet == null || (!rule.CanAppearOnMainPath && !rule.CanAppearOnBranchPath))
					continue;

				// Determine if this tile should be on the main path
				bool isOnMainPath = (!rule.CanAppearOnBranchPath) ||
									(rule.CanAppearOnMainPath && injectionRandomStream.NextDouble() > 0.5);

				var injectedTile = new InjectedTile(rule, isOnMainPath, injectionRandomStream);
				context.TilesPendingInjection.Add(injectedTile);
			}
		}

		/// <summary>
		/// Gathers tiles to be injected from the <see cref="TileInjectors"/> configured on this step
		/// </summary>
		/// <param name="context">The current generation context containing configuration and state information for tile injection</param>
		/// <param name="injectionRandomStream">The random stream to use to ensure determinism between runs</param>
		protected virtual void GatherFromPipeline(GenerationContext context, RandomStream injectionRandomStream)
		{
			if (TileInjectors == null)
				return;

			foreach (var injector in TileInjectors)
				injector?.InjectTiles(injectionRandomStream, ref context.TilesPendingInjection);
		}

		/// <summary>
		/// Gathers tiles to be injected from the delegates pass in with the current generation request
		/// </summary>
		/// <param name="context">The current generation context containing configuration and state information for tile injection</param>
		/// <param name="injectionRandomStream">The random stream to use to ensure determinism between runs</param>
		protected virtual void GatherFromRequest(GenerationContext context, RandomStream injectionRandomStream)
		{
			if (context.Request.TileInjectionMethods == null)
				return;

			foreach (var injectionMethod in context.Request.TileInjectionMethods)
				injectionMethod?.Invoke(injectionRandomStream, ref context.TilesPendingInjection);
		}
	}
}
