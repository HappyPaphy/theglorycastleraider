namespace DunGen.Generation
{
	public enum PipelineAnchor
	{
		BeforeAll,

		AfterTileInjection,
		AfterPreProcessing,
		AfterMainPath,
		AfterBranching,
		AfterValidateRequiredTiles,
		AfterBranchPruning,
		AfterFinaliseLayout,
		AfterInstantiateTiles,
		AfterPropProcessing,
		AfterLockAndKeyPlacement,
		AfterPostProcessing,
	}
}