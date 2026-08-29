using System.Collections.Generic;

namespace DunGen.Generation
{
	public interface ITileInjector
	{
		void InjectTiles(RandomStream randomStream, ref List<InjectedTile> tilesToInject);
	}
}
