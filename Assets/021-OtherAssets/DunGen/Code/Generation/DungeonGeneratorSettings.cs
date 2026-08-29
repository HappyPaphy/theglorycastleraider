using DunGen.Collision;
using DunGen.Common;
using DunGen.Graph;
using DunGen.Placement;
using System;
using UnityEngine;

namespace DunGen.Generation
{
	[Serializable]
	public sealed class DungeonGeneratorSettings
	{
		/// <summary>
		/// The DungeonFlow used to define the structure and progression of the generated dungeon
		/// </summary>
		public DungeonFlow DungeonFlow;

		/// <summary>
		/// An optional custom generation pipeline to use instead of the default one
		/// </summary>
		public GenerationPipeline PipelineOverride;

		/// <summary>
		/// If true, the seed will be randomized
		/// </summary>
		public bool ShouldRandomizeSeed = true;

		/// <summary>
		/// The specific seed to use when generating the dungeon. Only used if ShouldRandomizeSeed is false
		/// </summary>
		public int Seed;

		/// <summary>
		/// The maximum number of attempts to generate a valid dungeon before giving up. This is ignored in
		/// a packaging build, where it will always try until successful
		/// </summary>
		public int MaxAttemptCount = 20;

		/// <summary>
		/// Should we limit the number of doorway pairing attempts when trying to connect rooms?
		/// </summary>
		public bool UseMaximumPairingAttempts = false;

		/// <summary>
		/// The maximum number of doorway pairing attempts allowed. Only used if UseMaximumPairingAttempts is true
		/// </summary>
		public int MaxPairingAttempts = 5;

		/// <summary>
		/// The axis direction that is considered 'up' in the current coordinate system (e.g. usually PosY for 3D)
		/// </summary>
		public AxisDirection UpDirection = AxisDirection.PosY;

		/// <summary>
		/// Indicates whether the default tile repeat mode behaviour should be overridden
		/// </summary>
		public bool OverrideRepeatMode = false;

		/// <summary>
		/// The repeat mode to use when placing tiles. Only used if OverrideRepeatMode is true
		/// </summary>
		public TileRepeatMode RepeatMode = TileRepeatMode.Allow;

		/// <summary>
		/// Indicates whether tile rotation is being overridden
		/// </summary>
		public bool OverrideAllowTileRotation = false;

		/// <summary>
		/// Indicates whether tile rotation is allowed during placement. Only used if OverrideAllowTileRotation is true
		/// </summary>
		public bool AllowTileRotation = false;

		/// <summary>
		/// Settings related to debug rendering
		/// </summary>
		public DebugRenderSettings DebugRenderSettings = new DebugRenderSettings();

		/// <summary>
		/// The factor by which the main path length is scaled (e.g. a value of 2.0 will double
		/// the length of the dungeon's main path)
		/// </summary>
		public float LengthMultiplier = 1.0f;

		/// <summary>
		/// Specifies if and how trigger volumes are placed for each tile in the dungeon
		/// </summary>
		public TriggerPlacementMode TriggerPlacement = TriggerPlacementMode.ThreeDimensional;

		/// <summary>
		/// The layer index used for spawning tile trigger volumes
		/// </summary>
		public int TileTriggerLayer = 2;

		/// <summary>
		/// If true, the dungeon will be generated asynchronously over multiple frames
		/// </summary>
		public bool GenerateAsynchronously = false;

		/// <summary>
		/// The maximum amount of time, in milliseconds, that an asynchronous frame is allowed to run before
		/// yielding
		/// </summary>
		/// <remarks>Use this value to control the responsiveness of asynchronous operations by limiting the duration
		/// of each frame. Setting a lower value can improve responsiveness but may reduce throughput.</remarks>
		public float MaxAsyncFrameMilliseconds = 10;

		/// <summary>
		/// An optional pause, in seconds, to insert between the placement of each room when generating
		/// </summary>
		/// <remarks>This setting is primarily intended for debugging and demonstration purposes, and
		/// is ignored when generating in a packaging build.</remarks>
		public float PauseBetweenRooms = 0;

		/// <summary>
		/// If true, the dungeon will be restricted to the specified world-space bounds during generation
		/// </summary>
		public bool RestrictDungeonToBounds = false;

		/// <summary>
		/// The world-space bounds within which the dungeon must be contained. Only used if RestrictDungeonToBounds is true
		/// </summary>
		public Bounds TilePlacementBounds = new Bounds(Vector3.zero, Vector3.one * 10f);

		/// <summary>
		/// The collision settings used to determine how tile placement collisions are handled
		/// </summary>
		public DungeonCollisionSettings CollisionSettings = new DungeonCollisionSettings();
	}
}