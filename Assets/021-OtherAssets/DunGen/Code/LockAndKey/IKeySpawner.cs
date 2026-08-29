using System.Collections.Generic;

namespace DunGen.LockAndKey
{
	/// <summary>
	/// Parameters for spawning keys
	/// </summary>
	public sealed class KeySpawnParameters
	{
		/// <summary>
		/// The key we're trying to spawn
		/// </summary>
		public Key Key { get; }
		/// <summary>
		/// The key manager that owns the key
		/// </summary>
		public KeyManager KeyManager { get; }
		/// <summary>
		/// The dungeon that the key is being spawned in
		/// </summary>
		public Dungeon Dungeon { get; }
		/// <summary>
		/// An optional list of IKeyLock objects that we created, for which DunGen should assign a key
		/// </summary>
		public readonly List<IKeyLock> OutputSpawnedKeys = new List<IKeyLock>();


		public KeySpawnParameters(Key key, KeyManager keyManager, Dungeon dungeon)
		{
			Key = key;
			KeyManager = keyManager;
			Dungeon = dungeon;
		}
	}

	/// <summary>
	/// MonoBehaviours implementing this interface may be chosen by DunGen when deciding where and how to spawn keys
	/// </summary>
	public interface IKeySpawner
	{
		/// <summary>
		/// Can the provided key be spawned by this spawner?
		/// </summary>
		/// <param name="keyManager">KeyManager that owns the key</param>
		/// <param name="key">Key to spawn</param>
		/// <returns>True if spawning is possible</returns>
		bool CanSpawnKey(KeyManager keyManager, Key key);

		/// <summary>
		/// Spawns a key and returns an optional list of IKeyLocks that the key should be assigned to
		/// </summary>
		/// <param name="keySpawnParameters">Spawn parameters</param>
		/// <returns>Any IKeyLock objects that were created, for which DunGen should assign a key</returns>
		void SpawnKey(KeySpawnParameters keySpawnParameters);
	}
}
