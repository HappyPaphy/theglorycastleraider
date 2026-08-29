using DunGen.Weighting;
using System;
using UnityEngine;

namespace DunGen
{
	[Serializable]
	public sealed class LockedDoorwayAssociation
	{
		#region Legacy Properties

		[HideInInspector]
		[Obsolete("Obsolete in 2.19. Use Prefabs instead")]
        public GameObjectChanceTable LockPrefabs = new GameObjectChanceTable();

		#endregion

		public DoorwaySocket Socket;

		[GameObjectWeightFilter(allowPrefabAssets: true, allowSceneObjects: false)]
		public WeightedTable<GameObject> Prefabs = new WeightedTable<GameObject>();
    }
}

