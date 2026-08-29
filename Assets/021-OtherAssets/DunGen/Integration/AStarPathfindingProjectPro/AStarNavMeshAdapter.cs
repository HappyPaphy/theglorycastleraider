#if ASTAR_PATHFINDING

using Pathfinding;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Adapters
{
	[AddComponentMenu("DunGen/NavMesh/A* Pathfinding NavMesh Generator")]
	[HelpURL("https://dungen-docs.aegongames.com/pathfinding/astar-pathfinding/")]
	public class AStarNavMeshAdapter : NavMeshAdapter
	{
		public AstarPath PathFinder;
		public PathfindingTag OpenDoorTag = 2;
		public PathfindingTag ClosedDoorTag = 3;

		protected NavMeshGenerationProgress progress = new NavMeshGenerationProgress();
		protected List<Door> previousDungeonDoors = new List<Door>();


		public override void Generate(Dungeon dungeon)
		{
			progress.Percentage = 0.0f;

			// Cleanup from last time
			foreach (var door in previousDungeonDoors)
				door.OnDoorStateChanged -= OnDoorStateChanged;

			previousDungeonDoors.Clear();

			// Try to find a pathfinder in the scene if one wasn't supplied
			if (PathFinder == null)
			{
				PathFinder = UnityUtil.FindObjectByType<AstarPath>();

				if (PathFinder == null)
				{
					Debug.LogError("DunGen can't find an A* Pathfinder script in the scene. Aborting NavMesh generation");
					return;
				}
			}

			// Snap the bounds of any useable graph to the full bounds of the dungeon
			foreach (var graph in PathFinder.graphs)
			{
				var recast = graph as RecastGraph;

				if (recast != null)
				{
					recast.forcedBoundsCenter = dungeon.Bounds.center;
					recast.forcedBoundsSize = dungeon.Bounds.size;
				}
			}

			// Rescan all graphs
			foreach (var graph in PathFinder.graphs)
				foreach (var progress in PathFinder.ScanAsync(graph))
					ProgressCallback(progress);

			AddDoorOpenListeners();
		}

		protected virtual void ProgressCallback(Progress astarProgress)
		{
			progress.Percentage = astarProgress.progress;
			progress.Description = astarProgress.ToString();

			if (OnProgress != null)
				OnProgress(progress);
		}

		protected virtual void OnDoorStateChanged(Door door, bool isOpen)
		{
			Bounds bounds = UnityUtil.CalculateObjectBounds(door.gameObject, false, true);
			GraphUpdateObject guo = new GraphUpdateObject(bounds);
			var tag = isOpen ? OpenDoorTag : ClosedDoorTag;

			// Invalid tag ID
			if (tag > 31)
			{
				Debug.LogError("Invalid tag ID. Tags must be < 32");
				return;
			}

			guo.modifyTag = true;
			guo.setTag = tag;
			guo.updatePhysics = false;

			PathFinder.UpdateGraphs(guo);
		}

		protected virtual void AddDoorOpenListeners()
		{
			foreach (var door in UnityUtil.FindObjectsByType<Door>())
			{
				door.OnDoorStateChanged += OnDoorStateChanged;

				// We need to run it once to update tags
				OnDoorStateChanged(door, door.IsOpen);
			}
		}
	}
}
#endif