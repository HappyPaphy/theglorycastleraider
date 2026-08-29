using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Collision
{
	[Serializable]
	public abstract class BroadphaseSettings
	{
		public abstract ICollisionBroadphase Create();
	}

	public interface ICollisionBroadphase
	{
		void Init(BroadphaseSettings settings, DungeonGenerator dungeonGenerator);
		void Insert(Bounds bounds);
		void Remove(Bounds bounds);
		void Query(Bounds bounds, ref List<Bounds> results);
		void DrawDebug(float duration = 0f);
	}
}
