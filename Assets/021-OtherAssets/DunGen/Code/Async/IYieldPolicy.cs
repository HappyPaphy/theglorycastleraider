using DunGen.Generation;

namespace DunGen.Async
{
	/// <summary>
	/// Defines a policy for determining when and how to yield execution during dungeon generation
	/// </summary>
	public interface IYieldPolicy
	{
		/// <summary>
		/// Called when a new generation run begins
		/// </summary>
		void BeginRun();

		/// <summary>
		/// Determines whether execution should yield based on the specified reason
		/// </summary>
		/// <param name="context">The current generation context containing state and configuration for the ongoing dungeon generation process</param>
		/// <param name="reason">The reason for considering a yield. Specifies the context or condition prompting the yield check</param>
		/// <returns>true if execution should yield for the given reason; otherwise, false</returns>
		bool ShouldYield(GenerationContext context, YieldReason reason);

		/// <summary>
		/// Returns an object that can be used as a yield instruction corresponding to the specified yield reason
		/// </summary>
		/// /// <param name="context">The current generation context containing state and configuration for the ongoing dungeon generation process</param>
		/// <param name="reason">The reason for yielding, which determines the type of yield instruction to return</param>
		/// <returns>An object representing the yield instruction for the specified reason. The exact type of the object depends on the
		/// provided yield reason</returns>
		object GetYieldInstruction(GenerationContext context, YieldReason reason);

		/// <summary>
		/// Notifies the implementer that control has been yielded during an iteration or asynchronous operation
		/// </summary>
		void OnYielded();
	}
}