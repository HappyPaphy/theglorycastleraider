using UnityEditor;
using UnityEngine;

namespace DunGen.Versioning
{
	public abstract class VersionedScriptableObject : ScriptableObject, IVersionable, ISerializationCallbackReceiver
	{
		public abstract int DataVersion { get; set; }
		public abstract int LatestVersion { get; }
		public bool RequiresMigration => DataVersion < LatestVersion;


		public void Migrate()
		{
			if (DataVersion >= LatestVersion)
				return;

			OnMigrate();
			DataVersion = LatestVersion;
		}

		protected abstract void OnMigrate();

		public virtual void Reset()
		{
			DataVersion = LatestVersion;
		}

		#region ISerializationCallbackReceiver

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			if (this == null)
				return;

#if UNITY_EDITOR
			if (DataVersion < LatestVersion)
				EditorApplication.delayCall += Migrate;
#endif
		}

		#endregion
	}
}