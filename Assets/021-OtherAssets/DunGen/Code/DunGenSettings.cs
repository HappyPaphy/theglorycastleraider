#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using UnityEngine;
using DunGen.Tags;
using DunGen.Collision;
using DunGen.TileBounds;
using DunGen.Versioning;
using System;

namespace DunGen
{
	public sealed class DunGenSettings : VersionedScriptableObject
	{
		#region Singleton

		private static DunGenSettings instance;
		public static DunGenSettings Instance
		{
			get
			{
				if (instance != null)
					return instance;
				else
				{
					instance = FindOrCreateInstanceAsset();
					return instance;
				}
			}
		}


		public static DunGenSettings FindOrCreateInstanceAsset()
		{
			// Try to find an existing instance in a resource folder
			instance = Resources.Load<DunGenSettings>("DunGen Settings");

			// Create a new instance if one is not found
			if (instance == null)
			{
#if UNITY_EDITOR
				instance = CreateInstance<DunGenSettings>();

				if (!Directory.Exists(Application.dataPath + "/Resources"))
					AssetDatabase.CreateFolder("Assets", "Resources");

				AssetDatabase.CreateAsset(instance, "Assets/Resources/DunGen Settings.asset");
				instance.defaultSocket = instance.GetOrAddSocketByName("Default");
#else
				throw new System.Exception("No instance of DunGen settings was found.");
#endif
			}

			return instance;
		}

		#endregion

		#region Legacy

		[Obsolete("Obsolete in 2.19. Use BoundsCalculator instead")]
		
		public bool BoundsCalculationsIgnoreSprites = false;

		#endregion

		public override int LatestVersion => 2;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		private const int CurrentMigrationWarningVersion = 1;

		public DoorwaySocket DefaultSocket { get { return defaultSocket; } }
		public TagManager TagManager { get { return tagManager; } }

		/// <summary>
		/// Gets or sets the calculator used to determine the bounds of a tile
		/// </summary>
		/// <remarks>Assign a custom implementation of the ITileBoundsCalculator interface to modify how tile
		/// boundaries are computed. By default, a DefaultTileBoundsCalculator is used</remarks>
		[SerializeReference]
		[SubclassSelector(allowNone: false)]
		public ITileBoundsCalculator BoundsCalculator = new DefaultTileBoundsCalculator();

		/// <summary>
		/// Optional broadphase settings for speeding up collision tests
		/// </summary>
		[SubclassSelector]
		[SerializeReference]
		public BroadphaseSettings BroadphaseSettings = new SpatialHashBroadphaseSettings();

		/// <summary>
		/// If true, tile bounds will be automatically recalculated whenever a tile is saved. Otherwise, bounds must be recalculated manually using the button in the Tile inspector
		/// </summary>
		public bool RecalculateTileBoundsOnSave = true;
		/// <summary>
		/// If true, tile instances will be re-used instead of destroyed and re-created, improving generation performance at the cost of increased memory consumption
		/// </summary>
		public bool EnableTilePooling = false;
		/// <summary>
		/// If true, a window will be displayed when a generation failure occurs, allowing you to inspect the failure report
		/// </summary>
		public bool DisplayFailureReportWindow = true;
		/// <summary>
		/// Should the DunGen folder be checked for files that are no longer in use? If true, when loading DunGen will check if any old files are still present in the DunGen directory from previous DunGen version and will present the user with a list of files to potentially delete
		/// </summary>
		public bool CheckForUnusedFiles = true;
		/// <summary>
		/// Small epsilon used when checking room bounds for overlap to improve cross-platform seed determinism.
		/// Keep this value low; increase only if identical seeds produce different dungeons on different platforms.
		/// Higher values may cause rooms to visually overlap.
		/// </summary>
		public float CollisionThreshold = 0.001f;

		[SerializeField]
		private DoorwaySocket defaultSocket = null;

		[SerializeField]
		private TagManager tagManager = new TagManager();

		[SerializeField]
		private int fileVersion;

		[SerializeField]
		private int migrationWarningVersion;


#pragma warning disable CS0618 // Type or member is obsolete
		protected override void OnMigrate()
		{
			if(DataVersion < 1)
			{
				BoundsCalculator ??= new DefaultTileBoundsCalculator();

				if(BoundsCalculator is DefaultTileBoundsCalculator defaultCalculator)
					defaultCalculator.IncludeSpriteRenderers = !BoundsCalculationsIgnoreSprites;
			}
			if (DataVersion < 2)
			{
				// Keep the old behaviour for users who are upgrading to avoid breaking changes
				CollisionThreshold = 0.0f;
			}
		}
#pragma warning restore CS0618 // Type or member is obsolete

#if UNITY_EDITOR

		public bool IsMigrationRequired()
		{
			return migrationWarningVersion < CurrentMigrationWarningVersion;
		}

		public void UpdateMigrationVersion()
		{
			migrationWarningVersion = CurrentMigrationWarningVersion;
		}

		private void OnValidate()
		{
			if (defaultSocket == null)
				defaultSocket = GetOrAddSocketByName("Default");
		}

		public override void Reset()
		{
			base.Reset();

			migrationWarningVersion = CurrentMigrationWarningVersion;
		}

		private DoorwaySocket GetOrAddSocketByName(string name)
		{
			string path = AssetDatabase.GetAssetPath(this);

			var socket = AssetDatabase.LoadAllAssetsAtPath(path)
				.OfType<DoorwaySocket>()
				.FirstOrDefault(x => x.name == name);

			if (socket != null)
				return socket;

			socket = CreateInstance<DoorwaySocket>();
			socket.name = name;

			AssetDatabase.AddObjectToAsset(socket, this);

#if UNITY_2021_1_OR_NEWER
			AssetDatabase.SaveAssetIfDirty(socket);
#else
			AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(socket));
#endif

			return socket;
		}
#endif
	}
}
