using DunGen.Async;
using DunGen.Collision;
using DunGen.Pooling;
using DunGen.TilePlacement;
using System;
using UnityEngine;

namespace DunGen.Services
{
	/// <summary>
	/// A container of services used during dungeon generation
	/// </summary>
	[Serializable]
	public class DungeonGenerationServices
	{
		[Tooltip("The service used to adding proxy tiles within the dungeon")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public ITilePlacer TilePlacer = new TilePlacer();

		[Tooltip("The service used to pool proxy tile instances")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public ITileProxyPool TileProxyPool = new TileProxyPool();

		[Tooltip("The service used to find pairs of doorways that are allowed to connect")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public IDoorwayPairFinder DoorwayPairFinder = new DoorwayPairFinder();

		[Tooltip("The service used to check for collisions between placed tiles")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public IDungeonCollisionService CollisionService = new DungeonCollisionService();

		[Tooltip("The service used to provide tile templates (when given a tile prefab reference) for use during dungeon generation")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public ITileTemplateProvider TileTemplateProvider = new TileTemplateProvider();

		[Tooltip("The service used to determine how generation yields in order to generate asynchronously over multiple frames")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public IYieldPolicy YieldPolicy = new YieldPolicy();

		[Tooltip("The service used to build a realized dungeon from a proxy dungeon, spawning prefab instances for tiles and doorway objects")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public IDungeonBuilder DungeonBuilder = new DungeonBuilder();

		[Tooltip("The service used to build candidate tile lists for a given doorway during doorway pair finding")]
		[SerializeReference, SubclassSelector(allowNone: false)]
		public ICandidateTileBuilder CandidateTileBuilder = new CandidateTileBuilder();
	}
}