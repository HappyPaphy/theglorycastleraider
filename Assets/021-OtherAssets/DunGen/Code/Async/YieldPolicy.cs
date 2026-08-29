using DunGen.Generation;
using System;
using System.Diagnostics;
using UnityEngine;

namespace DunGen.Async
{
	/// <summary>
	/// Defines a policy for determining when to yield control during procedural generation
	/// to support asynchronous processing
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class YieldPolicy : IYieldPolicy
	{
		protected Stopwatch yieldTimer;


		public virtual void BeginRun() => yieldTimer = Stopwatch.StartNew();

		public virtual void OnYielded() => yieldTimer.Restart();

		public virtual bool ShouldYield(GenerationContext context, YieldReason reason)
		{
			var settings = context.Request.Settings;

			// If async generation is disabled, never yield
			if (!settings.GenerateAsynchronously)
				return false;

			// Yield after placing each room if a pause is requested (for visualization purposes)
			if (reason == YieldReason.RoomPlaced && settings.PauseBetweenRooms > 0)
				return true;

			// Yield if we've exceeded the max frame time
			bool frameWasTooLong =	settings.MaxAsyncFrameMilliseconds <= 0 ||
									yieldTimer.Elapsed.TotalMilliseconds >= settings.MaxAsyncFrameMilliseconds;

			return frameWasTooLong;
		}

		public virtual object GetYieldInstruction(GenerationContext context, YieldReason reason)
		{
			// If a pause is requested after placing each room, yield for that duration
			// but only in the editor - in builds we just yield until the next frame
			if (reason == YieldReason.RoomPlaced)
			{
#if UNITY_EDITOR
				return new WaitForSecondsRealtime(context.Request.Settings.PauseBetweenRooms);
#else
				return new WaitForEndOfFrame();
#endif
			}

			return new WaitForEndOfFrame();
		}
	}
}