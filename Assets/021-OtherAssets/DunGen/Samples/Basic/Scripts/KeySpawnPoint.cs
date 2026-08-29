using DunGen.LockAndKey;
using UnityEngine;

namespace DunGen.Demo
{
	public class KeySpawnPoint : MonoBehaviour, IKeySpawner
	{
		public bool SetColourOnSpawn = true;

		private MaterialPropertyBlock propertyBlock;
		private GameObject spawnedKey;


		#region IKeySpawner Implementation

		public bool CanSpawnKey(KeyManager keyManaager, Key key)
		{
			// Has already spawned a key (this check shouldn't be necessary)
			if (spawnedKey != null)
				return false;

			// Cannot spawn a key that doesn't have a prefab
			return key.Prefab != null;
		}

		public void SpawnKey(KeySpawnParameters keySpawnParameters)
		{
			// Spawn the key attached to the dungeon root
			spawnedKey = GameObject.Instantiate(keySpawnParameters.Key.Prefab);
			spawnedKey.transform.parent = keySpawnParameters.Dungeon.transform;
			spawnedKey.transform.SetPositionAndRotation(transform.position, transform.rotation);

			if (SetColourOnSpawn && Application.isPlaying)
			{
				if (propertyBlock == null)
					propertyBlock = new MaterialPropertyBlock();

				propertyBlock.SetColor("_Color", keySpawnParameters.Key.Colour);

				foreach (var r in spawnedKey.GetComponentsInChildren<Renderer>())
					r.SetPropertyBlock(propertyBlock);
			}

			// Pass any components that implement IKeyLock back to the dungeon generator
			keySpawnParameters.OutputSpawnedKeys.AddRange(spawnedKey.GetComponents<IKeyLock>());
		}

		#endregion
	}
}