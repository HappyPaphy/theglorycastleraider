using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Collision
{
	[Serializable]
	[SubclassDisplay(DisplayName = "Spatial Hashing")]
	public class SpatialHashBroadphaseSettings : BroadphaseSettings
	{
		public float CellSize = 40.0f;

		public override ICollisionBroadphase Create()
		{
			return new SpatialHashBroadphase();
		}
	}

	public class SpatialHashBroadphase : ICollisionBroadphase
	{
		public SpatialHashGrid<Bounds> SpatialHashGrid { get; private set; }


		public void Init(BroadphaseSettings settings, DungeonGenerator dungeonGenerator)
		{
			if (settings is not SpatialHashBroadphaseSettings spatialHashSettings)
				return;

			SpatialHashGrid = new SpatialHashGrid<Bounds>(
				spatialHashSettings.CellSize,
				(b) => b,
				dungeonGenerator.Settings.UpDirection);
		}

		public void Insert(Bounds bounds)
		{
			SpatialHashGrid.Insert(bounds);
		}

		public void Query(Bounds bounds, ref List<Bounds> results)
		{
			SpatialHashGrid.Query(bounds, ref results);
		}

		public void Remove(Bounds bounds)
		{
			SpatialHashGrid.Remove(bounds);
		}

		public void DrawDebug(float duration = 0)
		{
			SpatialHashGrid.DrawDebug(duration);
		}
	}
}
