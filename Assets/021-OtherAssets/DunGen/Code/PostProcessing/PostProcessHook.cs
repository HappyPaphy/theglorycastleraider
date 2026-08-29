using DunGen.Generation;
using System;

namespace DunGen.PostProcessing
{
	/// <summary>
	/// Represents a post-processing hook to be executed after dungeon generation, along with its execution priority.
	/// </summary>
	public class PostProcessHook
	{
		public Action<GenerationContext> Callback;
		public int Priority;


		public PostProcessHook(Action<GenerationContext> callback, int priority)
		{
			Callback = callback;
			Priority = priority;
		}
	}
}
