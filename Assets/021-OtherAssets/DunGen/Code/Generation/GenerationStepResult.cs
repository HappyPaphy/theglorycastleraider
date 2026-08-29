namespace DunGen.Generation
{
	public sealed class GenerationStepResult
	{
		public bool IsSuccess { get; private set; }
		public string FailureReason { get; private set; }

		private GenerationStepResult(bool success, string failureReason = null)
		{
			IsSuccess = success;
			FailureReason = failureReason;
		}

		public static GenerationStepResult Success() => new GenerationStepResult(true);

		public static GenerationStepResult Failure(string reason) => new GenerationStepResult(false, reason);

	}
}