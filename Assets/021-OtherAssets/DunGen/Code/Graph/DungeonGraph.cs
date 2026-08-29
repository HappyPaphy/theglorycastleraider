using System.Collections.Generic;
using System.Linq;

namespace DunGen
{
	public class DungeonGraph
	{
		public readonly List<DungeonGraphNode> Nodes = new List<DungeonGraphNode>();
		public readonly List<DungeonGraphConnection> Connections = new List<DungeonGraphConnection>();


		public DungeonGraph(Dungeon dungeon, IEnumerable<Tile> additionalTiles)
		{
			Dictionary<Tile, DungeonGraphNode> nodeMap = new Dictionary<Tile, DungeonGraphNode>();

			var allTiles = dungeon.AllTiles.Concat(additionalTiles);

			foreach (var tile in allTiles)
			{
				var node = new DungeonGraphNode(tile);
				nodeMap[tile] = node;
				Nodes.Add(node);
			}

			foreach (var conn in dungeon.Connections)
			{
				var nodeConn = new DungeonGraphConnection(nodeMap[conn.A.Tile], nodeMap[conn.B.Tile], conn.A, conn.B);
				Connections.Add(nodeConn);
			}
		}
	}
}
