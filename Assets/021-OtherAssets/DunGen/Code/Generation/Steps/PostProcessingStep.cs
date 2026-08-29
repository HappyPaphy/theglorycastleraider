using DunGen.Async;
using DunGen.PostProcessing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a post-processing step in the dungeon generation pipeline that finalizes statistics and activates
	/// generated elements.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class PostProcessingStep : IGenerationStep
	{
		#region Nested Types

		[Serializable]
		public sealed class PostProcessStepEntry
		{
			public bool Enabled = true;

			[SerializeReference, SubclassSelector(allowNone: true)]
			public IPostProcessStep Step;
		}

		#endregion

		public List<PostProcessStepEntry> Steps = new List<PostProcessStepEntry>();


		public virtual IEnumerator Execute(GenerationContext context)
		{
			yield return RunPostProcessStepsFromCode(context);

			foreach(var step in Steps)
			{
				if (step.Enabled && step.Step != null)
				{
					var sw = Stopwatch.StartNew();
					yield return step.Step.Execute(context);
					sw.Stop();

					context.GenerationStats.PostProcessStepTimes[step.Step.DisplayName] = (float)sw.Elapsed.TotalMilliseconds;

					yield return YieldSignal.Work;
				}
			}

			FinaliseGenerationStats(context);
			ActivateDoorObjects(context);

			context.ProxyDungeon.ClearDebugVisuals();
		}

		protected virtual IEnumerator RunPostProcessStepsFromCode(GenerationContext context)
		{
			// Sort post-processing steps by priority (highest priority first)
			context.PostProcessHooks.Sort((a, b) =>
			{
				return b.Priority.CompareTo(a.Priority);
			});

			// Run post-processing steps
			foreach (var hook in context.PostProcessHooks)
			{
				yield return YieldSignal.Work;
				hook.Callback(context);
			}
		}

		protected virtual void FinaliseGenerationStats(GenerationContext context)
		{
			var dungeon = context.Dungeon;

			// Calculate maximum branch depth
			int maxBranchDepth = 0;

			if (context.ProxyDungeon.BranchPathTiles.Count > 0)
			{
				foreach (var branchTile in context.ProxyDungeon.BranchPathTiles)
				{
					int branchDepth = branchTile.Placement.BranchDepth;

					if (branchDepth > maxBranchDepth)
						maxBranchDepth = branchDepth;
				}
			}

			context.GenerationStats.SetRoomStatistics(dungeon.MainPathTiles.Count, dungeon.BranchPathTiles.Count, maxBranchDepth);
			context.GenerationStats.EndTime();
		}

		protected virtual void ActivateDoorObjects(GenerationContext context)
		{
			// Activate all door GameObjects that were added to doorways
			foreach (var door in context.Dungeon.Doors)
			{
				if (door != null)
					door.SetActive(true);
			}
		}
	}
}