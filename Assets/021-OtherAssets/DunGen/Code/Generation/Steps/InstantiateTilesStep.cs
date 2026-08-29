using System;
using System.Collections;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that instantiates tiles in the dungeon based on the current generation context
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class InstantiateTilesStep : IGenerationStep
	{
		public virtual IEnumerator Execute(GenerationContext context)
		{
			var dungeonBuilder = context.Services.DungeonBuilder;
			yield return dungeonBuilder.BuildDungeon(context, context.Dungeon, context.Dungeon.TileInstanceSource);
		}
	}
}