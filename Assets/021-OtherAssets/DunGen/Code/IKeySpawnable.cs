using System;

namespace DunGen
{
	[Obsolete("IKeySpawnable has been deprecated. Please update your code to use IKeySpawner instead")]
	public interface IKeySpawnable
	{
		void SpawnKey(Key key, KeyManager manager);
	}
}
