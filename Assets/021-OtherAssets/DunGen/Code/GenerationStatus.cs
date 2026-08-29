using DunGen.Generation;

namespace DunGen
{
	public delegate void GenerationStatusDelegate(DungeonGenerator generator, GenerationStatus status);
	public delegate void GenerationStatusContextDelegate(DungeonGenerator generator, GenerationStatus status, GenerationContext context);
	public delegate void DungeonGenerationDelegate(DungeonGenerator generator);
	public delegate void DungeonGenerationCompleteDelegate(DungeonGenerator generator, Dungeon dungeon);

	public enum GenerationStatus
	{
		NotStarted = 0,
		PreProcessing,
		TileInjection,
		MainPath,
		Branching,
		ValidateRequiredTiles,
		BranchPruning,
		FinaliseLayout,
		InstantiatingTiles,
		PropProcessing,
		LockAndKeyPlacement,
		PostProcessing,
		Complete,
		Failed,
		Custom,
	}
}
