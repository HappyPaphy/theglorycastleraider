using System;
using System.Collections;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that finalises the dungeon layout by connecting overlapping doorways and calculating
	/// room depths.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class FinaliseLayoutStep : IGenerationStep
	{
		public virtual IEnumerator Execute(GenerationContext context)
		{
			var settings = context.Request.Settings;
			context.ProxyDungeon.ConnectOverlappingDoorways(settings.DungeonFlow.DoorwayConnectionChance, settings.DungeonFlow, context.RandomStream);
			context.ProxyDungeon.CalculateDepths();

			yield break;
		}
	}
}