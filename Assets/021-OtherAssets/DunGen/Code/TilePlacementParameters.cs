using DunGen.Graph;
using System;
using UnityEngine;

namespace DunGen
{
	[Serializable]
	public class TilePlacementParameters
	{
		public DungeonArchetype Archetype
		{
			get => archetype;
			internal set
			{
				archetype = value;
			}
		}
		public GraphNode Node
		{
			get => node;
			internal set
			{
				node = value;
			}
		}
		public GraphLine Line
		{
			get => line;
			internal set
			{
				line = value;
			}
		}

		[SerializeField]
		private DungeonArchetype archetype;

		[SerializeField]
		private GraphNode node;

		[SerializeField]
		private GraphLine line;
	}
}