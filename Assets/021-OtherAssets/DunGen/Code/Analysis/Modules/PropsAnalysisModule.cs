using System;
using System.Linq;

namespace DunGen.Analysis.Modules
{
	[Serializable, SubclassDisplay(displayName: "Props")]
	public sealed class PropsAnalysisModule : IGenerationAnalysisModule
	{
		public void OnAnalysisStarted(AnalysisResults results) { }

		public void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats)
		{
			// Global Prop Counts
			var globalProps = dungeon.gameObject.GetComponentsInChildren<GlobalProp>(false)
				.Where(x => x.gameObject.activeInHierarchy)
				.GroupBy(x => x.PropGroupID)
				.ToDictionary(g => g.Key, g => g.ToList());

			foreach (var globalPropSpec in dungeon.DungeonFlow.GlobalProps)
			{
				int propCount = globalProps.TryGetValue(globalPropSpec.ID, out var props) ? props.Count : 0;
				results.AddValue($"Props.Count.Global.ID_{globalPropSpec.ID}", propCount);
			}
		}

		public void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats) { }
		public void OnAnalysisEnded(AnalysisResults results) { }
	}
}