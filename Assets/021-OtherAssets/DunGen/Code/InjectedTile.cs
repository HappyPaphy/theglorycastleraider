namespace DunGen
{
	/// <summary>
	/// Contains details about where a tile should be injected into the dungeon layout
	/// </summary>
	public sealed class InjectedTile
	{
		/// <summary>
		/// The tile set to choose a tile from
		/// </summary>
		public TileSet TileSet;
		/// <summary>
		/// The depth of the tile in the dungeon layout, as a percentage (0.0 - 1.0) of the main path
		/// </summary>
		public float NormalizedPathDepth;
		/// <summary>
		/// The depth of the tile as a percentage (0.0 - 1.0) along the branch it is on. Ignored if <see cref="IsOnMainPath"/> is true"/>
		/// </summary>
		public float NormalizedBranchDepth;
		/// <summary>
		/// Whether the tile should be injected on the main path or not. If false, it will be injected on a branch
		/// </summary>
		public bool IsOnMainPath;
		/// <summary>
		/// Whether the tile is required to be injected. If true, DunGen will retry generating the dungeon until this tile is successfully injected
		/// </summary>
		public bool IsRequired;
		/// <summary>
		/// Should the entrance doorway to this tile be locked?
		/// </summary>
		public bool IsLocked;
		/// <summary>
		/// If the tile should be locked, which lock (from the KeyManager asset) should be used?
		/// </summary>
		public int LockID;


		public InjectedTile(TileSet tileSet, bool isOnMainPath, float normalizedPathDepth, float normalizedBranchDepth, bool isRequired = false)
		{
			TileSet = tileSet;
			IsOnMainPath = isOnMainPath;
			NormalizedPathDepth = normalizedPathDepth;
			NormalizedBranchDepth = normalizedBranchDepth;
			IsRequired = isRequired;
		}

		public InjectedTile(TileInjectionRule rule, bool isOnMainPath, RandomStream randomStream)
		{
			TileSet = rule.TileSet;
			NormalizedPathDepth = rule.NormalizedPathDepth.GetRandom(randomStream);
			NormalizedBranchDepth = rule.NormalizedBranchDepth.GetRandom(randomStream);
			IsOnMainPath = isOnMainPath;
			IsRequired = rule.IsRequired;
			IsLocked = rule.IsLocked;
			LockID = rule.LockID;
		}

		public bool ShouldInjectTileAtPoint(bool isOnMainPath, float pathDepth, float branchDepth)
		{
			if (IsOnMainPath != isOnMainPath)
				return false;

			if (NormalizedPathDepth > pathDepth)
				return false;
			else if (isOnMainPath)
				return true;

			return NormalizedBranchDepth <= branchDepth;
		}
	}
}
