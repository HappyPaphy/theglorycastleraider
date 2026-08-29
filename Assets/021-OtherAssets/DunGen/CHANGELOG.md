## 2.19.12 - [2026/07/31]

### Fixed
- Fixed an issue causing the DunGen project migration to incorrectly mark properties as overrides on prefab instances nested inside tile prefabs.


## 2.19.11 - [2026/07/08]

### Fixed
- Overhanging tiles should now correctly be rejected when `Disallow Overhangs` is checked in the dungeon generator settings.


## 2.19.10 - [2026/06/23]

### Fixed
- Fixed a `NullReferenceException` that could occur when using a blank entry in a weighted table such as on a local prop set component


## 2.19.9 - [2026/05/14]

### Fixed
- Fixed an issue causing tiles to rotate even when rotation was not allowed (either at a tile level or globally in the dungeon settings)
- The global `Allow Tile Rotation` override now correctly takes precedence over individual tile rotation settings when enabled. Previously, if a tile had rotation **disabled** at the tile level, it would not rotate even if overridden globally


## 2.19.8 - [2026/05/12]

### Fixed
- Fixed an issue causing a `KeyNotFoundException` to be thrown because tile sets were not being gathered when assigned only to 'Branch Start Tiles'


## 2.19.7 - [2026/05/07]

### Added
- Added a new `Collision Threshold` setting to the DunGen project settings that allows you to specify a distance threshold for when two tiles are considered to be colliding. Increasing this can help to mitigate determinism issues caused by floating-point precision differences across platforms and CPU architecture


## 2.19.6 - [2026/05/05]

### Added
- Added a new `OnGenerationStatusChangedWithContext` event to the `DungeonGenerator` class that provides the full generation context

### Fixed
- Fixed an issue causing renderers attached to the door/portal to stay visible when they should be culled
- Fixed an issue causing `DungeonGenerator.ChosenSeed` to not report the correct value


## 2.19.5 - [2026/05/04]

### Added
- Re-added functionality to the new culling camera component to bring it up to feature parity with the system it replaced
	- `Light` and `ReflectionProbe` components can now be culled along with `Renderer` components. Which types of components are culled can be configured in the culling camera inspector
	- Added a `SetVisibilityOverride` method that can be used to force a component to be visible or invisible regardless of whether the camera would normally cull it

### Fixed
- Fixed an issue causing rooms in attached dungeons to be culled incorrectly when using the new culling camera component


## 2.19.4 - [2026/04/29]

### Changed
- `TilePlacementData` setters are no longer marked as internal
- `RandomStream` is once again accessible from the `DungeonGenerator` instance


## 2.19.3 - [2026/04/21]

### Fixed
- Fixed an issue causing packaged builds to not compile due to the inclusion of editor-only code in the `GenerationAnalysis` class


## 2.19.2 - [2026/04/16]

### Fixed
- Generation pipeline services should now be properly serialized


## 2.19.1 - [2026/04/15]

### Fixed
- Fixed an issue where tiles with explicit entrance/exit doorways would never produce valid connections


## 2.19.0 - [2026/04/13]

### Added
- Added a new culling camera component (DunGen > Culling > Culling Camera) to manage various types of culling
	- Culling can be per-camera or global (persisting the culling state regardless of camera for better performance)
	- Available culling strategies:
		- Adjacency Culling: A replacement for the old BasicRoomCullingCamera and AdjacentRoomCulling components that have now been deprecated. Culls rooms based on adjacency to the room the player is currently in
		- Portal Culling (New): Culls rooms based on visibility through doorways
		- Frustum Culling (New): Culls all renderers in a room if the room bounds are outside the camera frustum. Can offer a small performance improvement over the built-in per-renderer frustum culling. Best used for top-down games where the player camera is **outside** the dungeon tiles, otherwise one of the other culling strategies is likely to be more effective
- Most DunGen components now have a Help URL. Clicking the help icon on the component header in the inspector will open the relevant documentation page in your web browser
- Added a tool for migrating DunGen data when upgrading to newer versions where the underlying data structures have changed
	- DunGen will prompt you to run the tool when upgrading to a new version, and can be run manually from the menu (`Window > DunGen > Run Project Migration`)
- Added a new `OnDungeonGenerationComplete` event to the `DungeonGenerator` class that is fired when dungeon generation is complete and passes a reference to the generated `Dungeon` instance
- Connection rules in the `DungeonFlow` can now be applied using tags on the doorways, in addition to tags on the tiles
- Tile bounds calculation parameters (including layer mask and which types of components to consider) can now be customised
	- Globally in the DunGen project settings. Bounds calculations can also be overridden entirely by creating a custom type derived from `ITileBoundsCalculator` or `DefaultTileBoundsCalculator` and assigning it in the project settings
	- Per-tile by adding a `TileBoundsOverride` component (or a custom component implementing `ITileBoundsCalculator`) to the tile prefab
- Refactored `DungeonGenerator` class to improve readability and maintainability
	- Most users should see no change in behaviour, but some advanced users who have extended or overridden parts of the generation process may need to update their code
	- The generation process has been split into multiple generation steps and services that can all be optionally extended or overridden by creating custom types derived from the appropriate classes
	- Core generation steps, services, and extension steps (custom code that runs **between** steps) can all be configured by creating a new **Generation Pipeline** asset (`Create > DunGen > Generation Pipeline`) and assigning it either in the `DungeonFlow` or as an override in the dungeon generation settings
	- Custom post-processing steps can be added to the generation pipeline by creating a new type implementing `DunGen.PostProcessing.IPostProcessStep` and adding a new entry to the list under the post-processing section of the generation pipeline asset. Existing ways of adding custom post-processing steps continue to work, but some signatures have changed and will need to be updated
- SECTR portal culling integration now supports interior doorways using the new `SECTR Doorway Link` component, allowing for per-room culling in tiles made of multiple rooms
	- Attaching a `SECTR Doorway Link` component to a doorway will allow you to select an interior sector that the door leads to, instead of using the sector of the entire tile
- Custom tile connection rules can now optionally modify connection weights in addition to (or instead of) just allowing or disallowing connections. Use the new `AdjustConnectionWeight` delegate in the `TileConnectionRule` class
- The set of candidate tiles for a given doorway can now be mutated before the tile is selected. Candidate tiles can be added/removed, and have their weights altered. Use the new static `CandidateTileRules` list in `DoorwayPairFinder`, or implement a custom `ICandidateTileBuilder` service and assign it in the custom generation pipeline asset

### Changed
- Generated dungeons are now nested inside their own container GameObject
- The `DungeonGenerator` class now has a `CompositeDungeon` property which stores dungeons that have been generated attached to one another. Generating a new non-attached dungeon will clear the previous composite dungeon
- In the collision settings, the old 'Avoid Collision with Other Dungeons' setting has been replaced with a new 'Multi-Dungeon Collision' drop-down with more options
- Individual debug drawing elements (collision visualization, path colouration) can now be toggled on and off independently in the dungeon generator settings
- Optimized doorway pair finding performance, especially on large dungeons with many doorways (~20% faster, ~80% reduction in GC allocations)
- Doorway prefabs (connectors and blockers) now use complex weights based on tile placement instead of a simple weight value
	- Includes: Main path weight, branch path weight, and a depth curve multiplier
- Weighted entries (such as in a `TileSet` or `LocalPropSet`) now have a 'Depth Mode' option to control which depth value is used to modify the base weight:
	- **None:** No depth modification
	- **Auto:** Uses main path depth for main path tiles, branch depth for branch tiles
	- **Main Path Depth:** Uses main path depth, even for branch tiles
	- **Branch Depth:** Uses branch depth, or 0 for main path tiles
- The `Tile` component now supports multi-object editing
- Refactored dungeon generation analysis
	- Any named metrics can now be added using a hierarchical naming scheme (e.g. 'Tiles.Count.MainPath')
	- Median values are now calculated and displayed alongside mean (formerly labelled 'average')
	- Now provides additional information such as:
		- Number of branches in the dungeon
		- Length of the main path and branch paths
		- Number of times each tile prefab was used
	- `RuntimeAnalyzer` now displays metrics in a grid for better readability
	- Analysis has been restructured into modules that can be extended for custom analysis
		- Create a new type that implements `IGenerationAnalysisModule` and it will automatically be picked up by the analyser
		- Use `results.AddValue()` to add values to the metrics table and `results.Messages.Add()` to add messages to the report
- Improved tile candidate selection. Candidate tiles are now sampled probabilistically by weight instead of always favouring the highest-weight entries, so small weight differences no longer cause disproportionate bias.

### Fixed
- Generating attached dungeons when using a custom Root GameObject should now work correctly
- Fixed an issue with tile collisions when using a Root GameObject that is not at the origin
- Fixed various transform issues where the dungeon would not correctly respect the position/rotation of its root or attachment point
- Attached dungeons will now automatically avoid collisions with the tile they are attached to
- Fixed an issue causing an incorrect number of global props to be spawned
- Various Dungeon Crawler Sample fixes:
	- Fixed a warning from the minimap camera in newer versions of Unity
	- The minimap now works with scriptable render pipelines as well as the built-in render pipeline
	- Static fields in the `HideableObject` component are now properly reset when domain reload is disabled in the project settings
- `Doorway.LockID` and `Dungeon.Branches` are now properly serialized

### Removed
- Quadtree broad phase for collision detection has been removed. The quadtree implementation wasn't fully supported and the spatial hashing broad phase is more stable, simple, and performs better in almost all cases anyway
- Removed the version of the tile connection rule delegate that was deprecated in 2.16


## 2.18.17 - [2026/04/07]

### Fixed
- Fixed an issue causing tiles to still be spawned at the origin when generating with a custom root transform and fixed a mismatch between the tile transform and the collision bounds when using a non-identity root transform


## 2.18.16 - [2026/03/16]

### Fixed
- Fixed an issue causing the first tile in the dungeon to not test for collisions before spawning. Mostly only an issue when generating attached to an existing dungeon


## 2.18.15 - [2026/03/10]

### Fixed
- Fixed issues with generating dungeons attached to a manually placed tile (not spawned by DunGen):
	- The manually placed tile will no longer have its transform incorrectly reset to the origin
	- Bounds for the manually placed tile will now be correctly recalculated in local space
- The collision manager now processes bounds in world space, instead of in the local space of the dungeon root


## 2.18.14 - [2025/12/20]

### Fixed
- The failure report window now correctly displays the failure reasons for all retries, not just the last one


## 2.18.13 - [2025/12/16]

### Fixed
- Updated `Tile` bounds transformation logic to more accurately handle rotation and scaling
- Fixed an issue sometimes preventing the automatic tile bounds calculation from running when saving a tile prefab


## 2.18.12 - [2025/12/10]

### Fixed
- Fixed an issue causing tags to not be available on the `DoorwayProxy` objects


## 2.18.11 - [2025/11/25]

### Changed
- Generating a dungeon in the scene view of the editor (using `Window > DunGen > Generate Dungeon`) will now spawn tiles as prefab instances, allowing for easier editing of generated dungeons


## 2.18.10 - [2025/10/05]

### Changed
- All samples now switch seamlessly between the old Input Manager and the new Input System package


## 2.18.9 - [2025/08/28]

### Fixed
- Fixed an issue causing `DoorwayPairFinder.CustomConnectionRules` to be reset **after** `Awake()` and `OnEnable()` are called, resulting in custom connection rules sometimes being lost


## 2.18.8 - [2025/08/20]

### Changed
- Performance improvement when generating large dungeons or dungeons with many doorways

### Fixed
- Tiles built with ProBuilder should now correctly have their bounds calculated when not open. Previously, ProBuilder bounds would only calculate correct bounds if the Tile was placed in the scene or open in a prefab stage


## 2.18.7 - [2025/08/14]

### Fixed
- Doorways can now be marked as both an entrance and an exit at the same time, but a doorway cannot be used as an entrance if it would take the place of the only valid exit


## 2.18.6 - [2025/07/18]

### Fixed
- Fixed a `MissingReferenceException` that could sometimes happen after importing DunGen
- PlayMaker integration now correctly uses the new `TriggerPlacement` mode enum instead of the deprecated `PlaceTileTriggers` boolean


## 2.18.5 - [2025/07/13]

### Fixed
- The analyser should now correctly handle generation failure, displaying a success rate upon completion


## 2.18.4 - [2025/07/10]

### Changed
- Built-in culling and SECTR VIS integration now support dungeons generated attached to other dungeons
- SECTR VIS integration now caches portal meshes and disposes of them in OnDestroy()

### Fixed
- Fixed an issue causing changes to not be saved when adding objects to a list by dragging and dropping them in the inspector


## 2.18.3 - [2025/07/05]

### Fixed
- Fixed an issue causing tiles to spawn in the wrong position when the dungeon root object is not at the origin
- Fixed an issue causing debug tile visualisation GameObjects to stick around after the dungeon has been generated when 'Pause Between Rooms' is greater than zero


## 2.18.2 - [2025/07/02]

### Added
- The `Dungeon` class now has a reference to the `TileInstanceSource` used for tile pooling. There's now a parameterless `Clear()` function that will return all tiles to the pool and clear the dungeon

### Fixed
- The generation failure report window should now correctly display an error when a required tile could not be injected, causing the dungeon generation as a whole to fail
- Fixed an issue where multiple copies of the same door prefab would be added to the `Dungeon` component's list of doors
- Fixed a build error caused by `DunGenSettings` validation using editor-only methods
- Fixed some build-only warnings


## 2.18.1 - [2025/06/17]

### Added
- Added support for 2D tile trigger placement

### Changed
- Tile injection rules can now be re-ordered and collapsed in the inspector. This has no effect on the generation process and is purely visual


## 2.18 - [2025/06/10]

### Added
- Doorway connector and blocker prefabs can now optionally use position & rotation offsets to correctly align the prefabs
- Added a new option for pooling tiles, improving performance when generating dungeons multiple times
	- Added a `TilePoolPreloader` component that can be added into the scene to spawn instances of tiles that can be saved with the level and will be used to pre-warm the tile pool
- Added a `ITileSpawnEventReceiver` interface that can be implemented on any script to receive a callback when the parent tile is spawned or de-spawned. Useful for resetting state when using tile pooling
- Added `OnDungeonGenerationStarted` and `OnAnyDungeonGenerationStarted` events to the `DungeonGenerator` class
- Added a new window for displaying the stats of a generated dungeon (Window > DunGen > Generation Stats)
- Added functions for finding tiles by their tags to the `Dungeon` class: `FindTilesWithTag`, `FindTilesWithAnyTag`, `FindTilesWithAllTags`
- The `Dungeon` class now lists all branches via the `Branches` property
- DunGen settings can now be accessed from the project settings window (Edit > Project Settings...)
- Added a validation rule that warns when a tile prefab is missing a `Tile` component
- Enhanced path straightening options:
	- Settings on `Archetypes` now include an option to straighten **branches** in addition to or instead of the **main path**
	- Path straightening can now also be applied to **nodes** in the dungeon flow graph
	- There are now global settings for path straightening on the `DungeonFlow` asset. These can be optionally overridden in `Archetypes` and on **Nodes** in the flow graph
- When the dungeon generation fails after reaching the maximum number of retries, a failure report window will now be displayed, showing all the reasons why the generation failed, and which tiles were involved. This can be turned off in the project settings
- Line segments in the dungeon flow graph can now be resized by clicking and dragging the boundary between two line segments

### Changed
- **[BREAKING CHANGE]:** `IKeySpawnable` has been deprecated. Please update your code to use the new `IKeySpawner` interface instead
- Tile instantiation is now also asynchronous if 'Generate Asynchronously' is enabled in the generator settings
- PauseBetweenRooms is now automatically disabled outside of the editor so it won't affect the performance of builds
- Improvements to generation performance:
	- Tile bounds are now pre-calculated in the editor to avoid performance overhead at runtime. By default, bounds are automatically recalculated each time the tile prefab is saved, but this can be turned off in the project settings. Bounds can be recalculated at any time using a new button in the Tile inspector
		- Performance improvement will scale up with the number and complexity of tile prefabs
	- Tiles are no longer deactivated before being destroyed
	- Bounds from additional tiles that should contribute to collision avoidance are now cached at the beginning of the generation process
	- Implemented a choice of broad phase for collision detection: None, Quadtree, or Spatial Hashing. NOTE: Quadtree is not recommended and currently only supports dungeons where the up vector is +Y
		- Affects the MainPath and Branching phases. Performance improvement will scale up with the number of tiles in the dungeon
	- Prospective doorway pairs are now sorted in a single pass, and custom connection rules are sorted once at the beginning of the generation process
	- TileProxy objects are now pooled
	- Various small performance improvements
- The option to include sprite renderers in the bounds calculations has been moved to the global DunGen settings asset
- Collision functionality and settings have been refactored into their own classes. Settings now belong in the `DungeonCollisionSettings` class
- The Dungeon class now has a `TileInstantiated` event that is fired when a new tile is instantiated
- Updated all samples to support tile pooling
- `DungeonGenerator`'s `GenerationStats` now contains information about how many of each type of tile were spawned, including how many were newly instantiated vs pulled from the pool
- Added assembly definitions for 'DunGen' and 'DunGen.Editor'
- Integrations for Unity's NavMesh Components and A\* Pathfinding Project Pro no longer need to be manually extracted. They will be automatically enabled if the appropriate packages are present
- Added collapsible foldout categories to the dungeon generator inspector
- Selected node/line is now highlighted in the dungeon flow graph editor window

### Fixed
- Custom connection rules should now be properly reset if domain reload is disabled in the project
- Fixed an issue in the Basic Sample causing many materials to be created when generating a dungeon
- Fixed an issue where the project would lose a reference to the default doorway socket
- The Doorway component should now always correctly revert to the default socket when no other is selected
- Labels on nodes in the dungeon flow graph editor should now correctly appear black in newer versions of Unity
- Removed remaining instances of deprecated `FindObjectOfType` and `FindObjectsOfType`
- Generating a dungeon attached to a manually placed tile (not spawned by DunGen) should now work as intended
- Fixed `NullReferenceException` when placing Doorways in a hierarchy with no `Tile` component
- The path straightening chance in the `DungeonArchetype` should now work as intended
- Fixed `NullReferenceException` when using empty entries in `LocalPropSet` component

### Removed
- Removed support for versions of A\* Pathfinding Project Pro before 5.0
- Removed RAIN AI integration


## 2.17.5 - [2025/05/03]

### Fixed
- Path straightening should now work as intended


## 2.17.4 - [2024/10/15]

### Fixed
- Fixed an issue that broke nesting for random prefab props


## 2.17.3

### Fixed
- All generation steps now call the `OnGenerationStatusChanged` callback. Previously the callback was incorrectly not triggering for TileInjection, BranchPruning, and InstantiatingTiles


## 2.17.2

### Fixed
- AStarPathfindingProjectPro adapter now correctly supports version 5.0


## 2.17.1

### Fixed
- Fixed a null reference exception caused by trying to process a prop that had been deleted by another prop script


## 2.17 - [2024/08/29]

### Added
- Dungeons can now be generated attached to other dungeons or even manually placed tiles in the scene using the new AttachmentSettings property on the DungeonGenerator class
- Added a new branch mode that allows users to specify the number of branches that should appear on a given section in the dungeon flow (see 'Branch Mode' in the dungeon flow settings)
- Added a new option in the archetype settings to optionally specify which tiles can be used at the beginning of branches (similar to the branch cap settings)
- DunGen can now avoid colliding with objects not placed by the dungeon generator in one of two ways:
	- Adding custom Bounds to the `AdditionalCollisionBounds` property on the DungeonGenerator class
	- Providing a custom function to the `AdditionalCollisionsPredicate` property on the DungeonGenerator class
- Multiple doorways can now be marked as designated potential entrances and exits, instead of just one of each

### Changed
- The `TilePlacementData` class now contains a new 'BranchId' property to allow custom code to know which branch a tile belongs to
- Added events for `OnGenerationComplete` and `OnAnyDungeonGenerationComplete` to the dungeon generator that can be used instead of the original `OnGenerationStatusChanged` if you're only interested in completion
- By default, the dungeon generator will now check for collisions against every other dungeon in the scene. This can be disabled by unchecking 'Collide All Dungeons' in the generator settings
- Replaced instances of obsolete methods (as a result, DunGen now requires Unity 2020.3 as the minimum version)
	- `FindObjectOfType` -> `FindFirstObjectByType`
	- `FindObjectsOfType` -> `FindObjectsByType`

### Fixed
- Fixed an issue causing the 'DunGen Settings' asset to not be created properly in newer versions of Unity
- Fixed a crash that could happen in the DungeonCrawler sample project when going from one scene to another
- Fixed an exception that could happen in the editor when using the culling camera when no scene cameras are present
- Fixed an issue that could sometimes cause a key to be placed inside the room being locked (or on any of the attached branches)
- Fixed an issue causing door prefabs to be removed when trying and failing to replace it with a locked door
- Fixed an issue where keys would exist inside a dungeon even if the corresponding locked doorway could not be placed
- `Dungeon.Bounds` is now correctly calculated in world-space when using a dungeon root object not at the origin


## 2.16 - [2023/08/15]

### Changed
- Most lists now support dragging and dropping to add new elements (including archetypes, tile sets, props, and doorway connectors/blockers)
- All lists now support undo/redo when adding or deleting elements
- Prop processing is now significantly faster on complex dungeons with many GameObjects
- Empty GameObject entries in the LocalPropSet or RandomPrefab components will no longer be ignored and can now be used to signal that no prop should be spawned
- Added a validation rule that warns when a terrain is being used in a tile that allows rotation (Unity terrains cannot be rotated)
- Selected doorways are now checked for valid placement in the editor:
	- The doorway will be colour-coded (green = valid, orange = not placed on the edge of the tile, red = not axis-aligned)
	- A red line will be drawn from the doorway to the nearest valid location
	- For doorways that are incorrectly placed, there will be a button in the inspector to attempt to fix the placement issues
- The dungeon validator will now warn when a doorway is not placed on the edge of the tile bounding box
- Unity NavMesh Adapter improvements
	- Added a new option to automatically calculate the start and end points of navigation links per-doorway instead of just using a pre-determined distance on either side of the doorway
	- When using the "Full Dungeon Bake" mode, any existing NavMesh surfaces in the dungeons will now be disabled to avoid overlapping navigation meshes
- `TileConnectionRule` supports a new 'ConnectionDelegate' which contains more information for more complex custom connection rules. The old delegate has been marked as obsolete but will continue to function for now
- Tags are now available on tile and doorway proxy objects for easier access when writing custom connection rules in code
- Tiles now have a branch ID that can be accessed via the tile's 'Placement' property. All tiles on the same branch will share a branch ID. Tiles not on a branch will have a branch ID of -1

### Fixed
- Fixed an issue where too much time was being attributed to the branch generation step. New steps for branch pruning and instantiating tile prefabs have been added for more accurate analysis
- Fixed and issue causing the 'Cull Each Child' option to not work properly with newer versions of SECTR (requires re-exporting SECTR_VIS integration package)
- Fixed a NullReferenceException when using an empty entry in the RandomPrefab component
- Fixed an issue causing the dungeon validator to incorrectly warn about doorways facing the wrong way in a tile containing a terrain
- Fixed an issue causing keys to sometimes not be able to spawn in the same room as the lock
- Key spawn points will now correctly only be used once
- Fixed an issue causing a doorway's 'LockID' to not be assigned
- Fixed a NullReferenceException caused when no valid locks could be found
- Fixed a NullReferenceException when trying to copy tags from a tile that has none assigned
- Fixed a bug causing DunGen to generate too few branches when the branch mode is set to 'Global'


## 2.15.1 - [2021/11/01]

### Changed
- The built-in Unity NavMesh adapter now supports providing a layer mask for any surfaces that need to be added at runtime. Existing surfaces are unaffected by this setting
- The `DungeonGenerator` class now contains a static event `OnAnyDungeonGenerationStatusChanged` which can be used without a direct reference to a given generator instance

### Fixed
- Fixed an issue causing injected tiles to sometimes have their locks not spawn on the entrance doorway
- Fixed an issue resulting in an uneven distribution of branches when using the 'Global' branch mode
- Fixed an issue causing the 'Global' branch mode to sometimes result in more than the maximum number of branches being created
- Fixed an issue causing the DungenCharacter component to not correctly track the current tile if the player only partially stepped into a new tile before returning to the original
- Fixed an error caused by the DungenCharacter component re-checking tiles when a dungeon is generated in the editor
- Dungeon information (tiles, connections, tile bounds, etc) should now be properly saved when generating a dungeon in the editor


## 2.15 - [2021/10/16]

### Added
- Injected tiles can now optionally have a locked door using the standard Lock & Key system
- The dungeon flow now has an option for pruning branches based on tile tags
	- A tile at the end of a branch will be deleted based on whether it has one of the tags in the "Branch Prune Tags" list in the dungeon flow settings
	- This setting ignores any injected tiles marked as required
- Added a new `IDungeonCompleteReceiver` interface which can be implemented on any script inside a dungeon to receive a callback when the dungeon generation is complete

### Changed
- Doorways now have a set of tags that can be used for custom connection logic in code using `DoorwayPairFinder.CustomConnectionRules`

### Fixed
- Doorways connected by the dungeon flow's random connection chance setting should now properly respect custom connection rules
- It should now once again be possible to select a different 'Count Mode' in the local prop set inspector
- Fixed an issue causing changes to sometimes not be saved when modifying the tags on a Tile component


## 2.14 - [2021/06/19]

### Added
- Added user-defined tags that can be applied to individual tiles
	- The dungeon flow now has a "Tile Connection Rules" section that can be used to customise which tiles are allowed to connect based on their tags
	- Tags can be accessed through code to apply more complex logic
- Added a new method for applying custom logic through code to allow/disallow connections between tiles (see `DoorwayPairFinder.CustomConnectionRules`)
	- Supports making decisions based on doorways and tiles instead of just two doorway sockets
	- Can be assigned priorities and chained together
- Added a new NavMesh adapter for 2D dungeons
	- Can be found in 'DunGen/Integration/Unity NavMesh.unitypackage'
	- Requires 2017.2 or higher
	- Currently supports SpriteRenderer meshes (not colliders) and Tilemaps sprite meshes (also not colliders)

### Changed
- AdjacentRoomCulling component Improvements
	- Added IsTileVisible method and TileVisibilityChanged event
	- Can now optionally ignore components that are disabled from the start
	- Many methods are now marked as virtual for easier extension
	- Now also culls reflection probes

## 2.13.4

### Fixed
- Fixed a compilation error in the Playmaker integration package
- Fixed an issue that allowed designated exits to be used as an entrance to the tile


## 2.13.3

### Changed
- **[Breaking Change]:** All references to `System.Random` have been replaced with a new type `DunGen.RandomStream`. Any user code that references the random number generator will need to be updated.
- The dungeon generator should now produce identical results when using the same seed even across different .NET versions


## 2.13.2 - [2020/08/13]

### Fixed
- Fixed an error when trying to process child props that have been removed by a prop script
- When doing a full dungeon bake using Unity's NavMesh system, the old NavMesh is now correctly cleared first


## 2.13.1 - [2020/07/01]

### Fixed
- Removed deprecated components from sample scenes to avoid warnings in Unity 2020.1
- The 'Adjacent Room Culling (Multi-Camera)' component should now work when using scriptable render pipelines, provided the project is Unity 2019.1 or higher
- The 'Adjacent Room Culling' component (non-multi-camera) now also has a 'Target Override' property to match its multi-camera counterpart, allowing it to be used in games where you want to cull around the character, not the camera (e.g. third-person or 2D)
- Fixed an issue in the Dungeon Crawler Sample that caused the NavMesh to persist between scenes
- Fixed an issue with SECTR portal culling integration that caused rooms to not be culled initially when 'Multi Camera Culling' was turned off
- Fixed an issue causing 'Doorway Connection Chance' to do nothing


## 2.13 - [2020/05/05]

### Added
- Archetypes can now be be marked as unique. DunGen will try to ensure that unique archetypes are only used once throughout the dungeon.
- The Door component now contains a `DontCullBehind` property to allow doors to be closed without culling rooms behind it. This works for the built-in culling and the SECTR VIS integration.
- Doorway connector & blocker prefabs can now all be assigned weights for more control over how frequently certain objects spawn.

### Changed
- Tile prefabs are now only instantiated after the entire dungeon layout is generated, resulting in much faster generation times. Tiles will still be spawned individually when generating asynchronously with a 'Pause Between Rooms' greater than zero to allow for visual debugging.
- Local Prop Set and Random Prefab props can now be nested properly. Global props should still not have other props nested inside them, but can be nested inside others.
- When using the 'Full Dungeon Bake' mode with the built-in Unity NavMesh adapter, it's now possible to use your own surfaces for more control over settings by unchecking the 'Auto-Generate Surfaces' checkbox.

### Fixed
- Local prop sets now correctly work with objects attached to door connectors & blockers
- The SECTR VIS integration will no longer throw an error if a door already has a SECTR_MEMBER component


## 2.12.1 - [2019/12/12]

### Fixed
- Dungeon generator settings should now properly work with the new prefab workflow in Unity
- Fixed an issue causing the phase of a post-process step to be ignored
- Connector prefab instances now correctly have their local position reset after being parented to the doorway


## 2.12 - [2019/11/20]

### Added
- Dungeon Crawler Sample (Extract from "DunGen/Samples/DungeonCrawler.unitypackage"). Requires Unity 2019.1 or higher
- Doorway sockets are no longer hard-coded and are instead assets that can be added without modifying DunGen's source code
	- Doorway size is now part of the new DoorwaySocket asset instead of being applied to each doorway instance
- Doorway socket connection logic can be overridden by providing your own function to `DoorwaySocket.CustomSocketConnectionDelegate`
- Added a new constraint in generator settings to enforce a minimum padding distance between unconnected tiles
- Added a new constraint in generator settings to disallow overhangs (so rooms cannot spawn above other rooms)
- Added a new icon for doorways

### Changed
- **[Breaking Change]:** Upgrading from older versions requires deleting the old version first. Doorways must have their sockets re-assigned using the new system. Up-direction must be re-assigned in dungeon generator settings.
- Door prefabs are now parented to their doorway objects, rather than the dungeon root
- The maximum overlap between two connected tiles can be tweaked in the dungeon generator settings
- Simplified up-vector selection
- If no socket is specified for an entry in a TileSet's locked door prefab list, the locked door can be assigned to any socket
- Assigning a prefab to a key is now optional
- Disabled doorways are no longer considered when connecting tiles together or when calculating tile bounds


## 2.11.9 - [2019/10/28]

### Added
- Added a new built-in culling component "AdjacentRoomCulling"
- Added "Full Dungeon Bake" mode to Unity NavMesh integration which allows the entire dungeon to be baked as a single surface when generated

### Changed
- Improvements to built-in basic culling camera
	- Improved performance by ~60% (3.19ms -> 1.29ms in test case)
	- Now optionally supports culling light sources
	- Now supports culling doors
- Some improved inspector tooltips


## 2.11.8 - [2019/08/05]

### Added
- Added an option to override the global "Doorway Connection Chance" on a per-tile basis
- Added an option to restrict connecting overlapping doorways to only tiles that occupy the same segment of the dungeon flow graph. This should help to prevent unintended shortcuts from appearing when this feature is used

### Changed
- The documentation has been updated to include some recent features which had mistakenly been omitted. The documentation also includes a previously missing step when setting up the Lock & Key system
- An error is now logged whenever a tiles automatically calculated bounds are invalid (have a negative or zero size)

### Fixed
- Automatic bounds calculation will now work properly with newer versions of ProBuilder


## 2.11.7 - [2019/04/23]

### Added
- A more comprehensive validation tool has been added to help find any errors when setting up a dungeon. This can be accessed using the "Validate" button in the DungeonFlow asset inspector
- Added a 'Branch Mode' option to the dungeon flow allowing users to optionally specify the number of branches that should appear globally across the entire dungeon, rather than locally per-tile

### Changed
- BasicRoomCullingCamera now has an option to also cull in the editor scene view
- Improved performance of BasicRoomCullingCamera in scenes with a lot of renderers
- Foldout labels can now be clicked to expand the foldout
- The old dungeon validation is now only run inside the editor for a minor performance improvement in packaged builds
- Doorways now also draw their expected up-vector to make it clearer which way they should be facing
- Adapters (such as integration for SECTR VIS and Unity's NavMesh) will no longer fail silently if attached to a GameObject without a RuntimeDungeon component
- Creating a new DunGen asset will now allow the user to specify a file name
- Some improvements for the DungeonFlow inspector


## 2.11.6 - [2019/02/19]

### Changed
- Most DunGen components now support editing multiple selected objects
- All inspector lists are now re-orderable
- Added some in-editor tooltips to properties that didn't already have them
- The Tile and RuntimeDungeon components now also allow for editing bounds in the scene view
- There is now the option to disallow any tile repetition. This can be done on a per-tile basis or overridden globally in the dungeon generator

### Fixed
- Fixed an issue preventing the dungeon generating from working until the scene/editor is restarted after finding an issue with the dungeon layout
- SECTR VIS integration will now correctly use the already calculated tile bounds instead of its own. This should prevent any gaps from forming between sectors (resulting in incorrect culling)
- Fixed an issue causing some DunGen components to not save correctly when edited in the new prefab editor in Unity 2018.3
- Undo/redo should now work consistently


## 2.11.5 - [2018/11/26]

### Changed
- **[Breaking Change]:** A* Pathfinding Project Pro integration updated to version 4.0+. If you're using an older version, you'll need to add `ASTAR_PATHFINDING_VERSION_3` to your "Scripting Define Symbols" in the Unity project settings
- Updated to work with the new prefab system in Unity 2018.3
- Moved demo scripts to their own namespace to avoid naming conflicts
- Small update to the 2D demo scene to include a controllable player character

### Fixed
- Fixed an issue preventing the basic culling camera from culling rooms behind a closed door
- The Basic Culling Camera will now no longer incorrectly refresh the visible set of tiles every frame - this could increase performance greatly
- Fixed an issue causing the integrated basic culling to not work if the camera was spawned through code


## 2.11.4 - [2018/08/28]

### Fixed
- The start tile should now correctly respect the transform of its root game object
- The integration for A* Pathfinding Project Pro should work correctly in Unity 5 or higher
- Off-mesh links produced for Unity's NavMesh system will now take the agent's radius into account


## 2.11.3 - [2018/01/08]

### Changed
- Auto-calculated bounds should now ignore particle systems
- DunGen now supports setting `Physics.autoSyncTransforms` (new in Unity 2017.2) to false

### Fixed
- Large tiles should no longer overlap a small amount
- The per-tile "Allow Rotation" and "Allow Immediate Repeats" options should work correctly again
- Lock & key placement should now correctly be done after props are processed


## 2.11.2 - [2017/07/24]

### Changed
- The "Basic Room Culling Camera" component can now optionally be provided with a TargetOverride transform for third-person games
- The ArchetypeValidator will now report a warning when a TileSet contains an entry with an unassigned tile and will no longer throw an unhandled exception

### Fixed
- Tiles will now correctly never appear in the dungeon layout when they have a tile weight of zero


## 2.11.1 - [2017/05/23]

### Added
- Tiles can now optionally designate entrance and exit doorways (available by manually adding a Tile component to your tile)

### Changed
- Door prefabs will now always take on the transform of the Doorway that spawned it
- "Allow Immediate Repeats" now defaults to true to avoid confusion when testing DunGen with a setup that has only one tile. It's still possible to override this behaviour both globally and on a per-tile basis
- The DungeonGenerator class has a new "Retrying" event that is fired whenever DunGen has to retry the entire dungeon layout

### Fixed
- JIT errors should no longer be thrown on platforms that require AoT compilation (such as Xbox One & iOS)
- Fixed an error preventing use of the PlayMaker integration
- Fixed a rare issue that caused rooms with vertical doorways to sometimes be flipped upside-down
- Fixed an issue that caused the dungeon generator to incorrectly revisit certain statuses (Branching, PostProcessing, Complete), thus making multiple calls to the `OnGenerationStatusChanged` event
- Fixed multiple errors with the dungeon flow editor window
- Fixed an OutOfMemoryException that could occur when a tile had a weight of zero


## 2.11 - [2017/04/14]

### Added
- Added API to DungeonGenerator for registering post-process callbacks: `RegisterPostProcessStep()` & `UnregisterPostProcessStep()`
	- Callbacks are invoked during DunGen's Post-Process step in order of phase (before or after built-in post-processing), then priority (highest to lowest)
	- The base NavMeshAdapter class has been changed to use this method to ensure that the NavMesh is build before DunGen reports that the generation is complete when using the `OnGenerationStatusChanged` event
- Added an adapter for Unity's new NavMesh system (5.6 beta)
- Added an adapter for built-in simple culling for use in interior first-person games; works best with auto-closing doors on each doorway
- Dungeon generation can now be performed asynchronously so as to avoid blocking Unity's main thread, allowing for things like animated loading screens to play while the dungeon is being generated
	- There are some new settings for runtime dungeons to control this behaviour
	- The new "Pause Between Rooms" settings allows you to pause the generation for a number of seconds after a room is placed to visualise the generation process
- Added tooltips to all dungeon generator settings

### Changed
- **[Breaking Change]:** Drastic changes from previous versions. Back up your project before upgrading. Unity 5.0+ targeted (minimum Unity 4.5+ recommended).
- Drastically changed the way DunGen appends tiles internally - it should now generate faster most of the time and practically never fail
- Culling adapters now use the same method as NavMesh adapters, they are added as components to the GameObject containing the RuntimeDungeon; moved culling code out of DungeonGenerator class
- Documentation has been completely re-written to be more modular, easier to follow, and to include features that may have been skipped over in previous versions. The documentation is now front-loaded with information to get started quickly, more advanced topics come later
- Improved the inspector for the Doorway component & changed some of the terminology; it should be much easier to understand now

### Removed
- The option to use the legacy weighting system has been removed, there was no reason to use it and it's no longer possible with the new generation method anyway
- Code Cleanup:
	- Removed experimental `GenerateAppended()` method. It was never fully supported and never would have been
	- Removed orphaned code for dungeon analysis window. RuntimeAnalyzer has always been the way to analyse dungeon generation
	- Removed code used to generate a main path without allowing backtracking - it wasn't even exposed as an option and allowing backtracking is objectively better
	- Removed visibility code from the base Tile class; visibility should be handled by culling adapters
	- Removed TypeUtil & AnalysisWindow

### Fixed
- Fixed an issue with the generation failing due to not finding matching doorways in a tile when using a custom `IsMatchingSocket()` method
- Disallowing repeated tiles should now work as intended and will now also consider branches
- Fixed a collision issue when using manually overridden tile bounds
- Fixed an issue with injected tiles on the main path marked as "required" not appearing occasionally
- Unused tiles deleted during the generation process should no longer contribute to the NavMesh when generating synchronously


## 2.10.1 - [2016/12/11]

### Added
- It's now possible to override DunGen's automatically generated tile bounds by attaching a Tile component to the room prefab and checking the "Override Automatic Tile Bounds" box

### Changed
- All Renderer component bounds will be taken into account when calculating tile bounds now, not just MeshRenderers and (optionally) SpriteRenderers

### Fixed
- Tiles created with ProBuilder should now have their bounds calculated properly and should no longer overlap
- Tile prefab's scale is now correctly handled


## 2.10 - [2016/11/15]

### Added
- Doorways now have a priority for deciding which doorway's "Door Prefab" should be chosen
- Added an option to specify which layer the tile trigger volume is placed on (Defaults to "Ignore Raycasts", only effective if "Place Tile Triggers" is checked)
- Added a DungeonFlowBuilder helper class to assist with creating a dungeon flow graph through code
- Added a new "count mode" to the local prop set script which allows the number of props to change based on the tile's position in the dungeon layout

### Changed
- "Ignore Sprite Bounds" in the dungeon generation settings is now unchecked by default

### Fixed
- Fixed an issue with the 2D demo scene which caused tiles to overlap
- "Avoid Door Prefab Rotation?" for doorways should now be set properly
- Door prefabs should now always be cleaned up correctly
- Auto-placed trigger volumes for tiles no longer sometimes have negative sizes
- Fixed an issue with the SECTR VIS integration that was causing door states to not correctly update
- Fixed an error that occurred when trying to place a lock on a doorway that was already locked
- Fixed an issue that was incorrectly allowing assets to be selected in the LocalPropSet component


## 2.9.1 - [2016/04/18]

### Added
- There are now options to avoid rotating Door and Blocker prefabs placed by the Doorway component

### Changed
- Dungeon generation will fail much less frequently, especially when imposing constraints such as fixed tile rotations
- In the event DunGen does fail (editor only; at runtime, DunGen will keep trying indefinitely), points-of-failure will be listed to give a better idea of the cause

### Fixed
- Fixed an error causing the "Allow Tile Rotation" override to not work properly
- Fixed an issue causing nodes in the Dungeon Flow to be un-selectable if placed over the top of the Start or Goal nodes
- Fixed an issue causing doorways from a previous dungeon to be considered when trying to connect overlapping doorways
- Fixed an ambiguous reference to `TooltipAttribute` when using PlayMaker integration with newer versions of Unity


## 2.9 - [2016/04/03]

### Added
- The `Dungeon` class now has a `Bounds` variable which gives the bounding box of the entire dungeon layout
- A root GameObject can now be chosen when using the RuntimeDungeon component. If none is specified, it will default to the old behaviour of creating a new root named "Dungeon"
- RandomPrefab props now have options for keeping the spawned prefab's position or rotation as an offset. Previously, spawned prefabs always snapped into position and ignored the prefab's transform (this is still the default behaviour)
- Added integration for generating navigation meshes with both RAIN and A* Pathfinding Project Pro
- Added an option to disable the trigger volumes DunGen places around each tile. If disabled, the DungenCharacter component won't receive events when moving between rooms

### Changed
- Reverted the ProBuilder support changes made in 2.8.0 - these are no longer necessary
- The utility function `UnityUtil.CalculateObjectBounds()` now ignores trigger colliders by default. Room bounds should no longer encompass any trigger colliders
- Moved `TypeUtil` class to the editor folder as it was causing issues when trying to build for Windows Store (and possibly other platforms)

### Fixed
- Fixed an error in the runtime analyser
- Fixed errors when using custom doorway socket connection logic


## 2.8 - [2016/01/11]

### Added
- DunGen now supports tiles made with ProBuilder

### Changed
- Tiles will now maintain their proper weights across multiple TileSets. The old behaviour can be reactivated using the "Use Legacy Weighting" option in the dungeon generator settings

### Fixed
- DunGen will no longer throw an ArgumentOutOfRangeException if a GameObject containing a Doorway component is disabled
- Fixed an error when trying to build a project with SECTR integration
- Fixed a stack overflow exception that occurred when no Tile matched the requirements for the next room in the layout
- The scale of Tile prefabs will no longer be reset before being placed by the generator


## 2.7 - [2015/10/24]

### Added
- Injected tiles can now be marked as required. If a required tile is not present at the end of the branch path stage, the generation will fail (and retry until the layout is successfully generated or until the maximum number of failed attempts is reached)
- Added a new "Length Multiplier" option to the dungeon generator. The main path length of the output dungeon will be multiplied by this number. Allows for dungeon length to be altered dynamically at runtime
- Added support for PlayMaker actions for generating and clearing dungeon layouts

### Changed
- The dungeon generator will now wait one frame before changing its status to "Complete" to ensure all unused GameObjects are properly destroyed first
- SECTR VIS culling is now easier to enable. Just unpack "DunGen/Integration/SECTR_VIS.unitypackage" and select "SECTR VIS" from the list of portal culling solutions in the dungeon generator settings. It's now also much easier to integrate your own portal culling solution; just derive a new type from PortalCullingAdapter and implement its abstract methods.
- Portal culling will now also automatically handle doors placed by DunGen so that rooms are culled when the connecting door is closed - as a result, door objects are now parented to the dungeon root rather than their doorway. There is now a Door component which will automatically be added to door prefabs placed by DunGen. This component includes information about which doorways and tiles it is connected to and has a IsOpen property which is used to turn portals on or off when used with portal culling
- As a result of the new culling changes, door objects are now parented to the dungeon root, rather than their doorway
- Doors placed by the Lock & Key system are now considered the same as a door prefab and so will also benefit from the above

### Fixed
- Fixed an issue that was causing dungeons to not generate properly when `Generate()` was called from a physics trigger/contact
- Fixed an error that caused DunGen to try to place locks & keys using a DungeonFlow without a KeyManager assigned
- Tiled placed using the tile injection system should now correctly make use of their full range of possible spawn locations
- Injected tiles should no longer occasionally overwrite tiles placed by a node in the DungeonFlow


## 2.6 - [2015/07/15]

### Added
- Tiles can now be "injected" into DunGen before randomization occurs. Injection delegates can be added to the generator's `TileInjectionMethods` property
- Tile injection for simple cases can be done through the DungeonFlow inspector. No code required.
- There's now a "Overwrite Existing?" option when generating dungeons in the editor
- Doorways now have a "Hide Conditional Objects?" option which hides all GameObjects in the "Add when in use" and "Add when NOT in use" lists. For the purpose of reducing clutter at design-time - has no effect on the runtime results
- Doorways now have a "Blocker Prefabs" list which works similarly to the existing "Door Prefabs" list, except with doorways that are not in use. Allows you to define blocking objects without placing them in the tile first, if that's your preferred workflow

### Changed
- All object lists now report how many objects they contain

### Fixed
- Fixed an issue that sometimes caused tiles to not be cleaned up correctly in editor-built dungeons, resulting in what looked like overlapping tiles


## 2.5.5 - [2015/06/13]

### Added
- Both "Allow Immediate Repeats" and "Allow Tile Rotation" now have optional global overrides that can be set in the dungeon generator

### Changed
- "Allow Immediate Repeats" can now be specified per-tile and is now set to false by default

### Fixed
- Fixed an issue causing Tile trigger volumes to sometimes be incorrectly oriented
- Fixed a bug which lead to SECTR portals not being removed when calling the `Generate()` function multiple times (like when using the analyser)
- Fixed some camera related bugs in the demo scene


## 2.5.4 - [2015/04/11]

### Added
- The `DungeonGenerator` class now has a `DetachDungeon` method allowing you to "tear-off" the dungeon from the generator so that it is not overwritten next time `Generate()` is called
- **[EXPERIMENTAL]** The `DungeonGenerator` class now has a `GenerateAppended` method which will generate a new dungeon appended to a previous dungeon that you specify. **NOTE:** This is entirely experimental and NOT supported functionality; dungeons generated in this manner will likely overlap or fail depending on whether allowIntersection is set. You'll have to decide how/if this is handled when it occurs. This is mostly in place as a starting point for those of you who want to implement infinite dungeons - but it needs additional logic (likely game-specific) to work as a complete solution.
- Doorway components now have a `ConnectedDoorway` variable
- Tiles now have some methods of getting/checking adjacent Tiles
- Tiles now contain a BoxCollider trigger component. There's a new DungenCharacter component which handles information about which Tile it's currently in (with events fired when switching tiles)

### Changed
- DunGen is using a new method for socketing doorways together which is more robust. Doorways can now be aligned vertically (for stairwells, for example)

### Fixed
- The Lock & Key system will correctly also place locks on doorways that don't have a prefab applied to them


## 2.5.3 - [2015/02/10]

### Changed
- The `Doorway` class is now properly defined under the namespace "DunGen" which should help with any naming conflicts
- Minor changes to make the demo scene compatible with Unity 5.0

### Fixed
- DunGen should no longer try to apply a lock to the same door twice, causing an exception to be thrown
- Doorways with no possible "locked door prefab" will no longer be considered when adding locks
- In-editor dungeons will once again generate correctly
- Dungeons should now be generated in the local coordinate space of its root GameObject, rather than at the world-space origin
- Fixed an issue causing a branch tile's `NormalizedPathDepth` to be calculated incorrectly, resulting in errors in Unity 5


## 2.5.2 - [2014/12/29]

### Added
- Added a Straighten slider to the DungeonArchetype that controls the likelihood that the main path generated will be a straight line (thanks to Binary42)
- Multiple keys can now be spawned for a single lock. In the KeyManager asset, each key has a "Keys per Lock" value

### Changed
- Users should notice a large improvement in success rate when generating dungeons
- DunGen will keep trying until it succeeds when the project is built (can still fail in the editor as a safety net to prevent infinite loops for invalid dungeons)
- Users should notice a further large improvement in success rate when generating dungeons; in addition to shorter generation times (thanks to ashwinFEC)

### Fixed
- Fixed an issue causing bounding boxes to sometimes be calculated incorrectly
- Doorways should no longer have multiple door prefabs assigned to them if the doorways were connected by overlapping
- Doorways with different sockets will no longer be connected when overlapping
- DunGen should now retry to place a Tile when none of those in the TileSet have a socket of the correct type
- DunGen should now correctly try to add the specified number of locked doors to a dungeon segment. **NOTE:** This still rarely produces desired results


## 2.5.1 - [2014/12/19]

### Fixed
- Fixed an issue causing RandomPrefabs to not inherit their parent's rotation
- RandomPrefabs will now correctly be added when nested inside another RandomPrefab
- Door prefabs will now correctly be added to open doorways


## 2.5 - [2014/10/04]

### Added
- Added an option to reduce the frequency that duplicate rooms are being placed right next to each other. Un-checking "Allow Immediate Repeats" in the dungeon generator settings will enable this behaviour.
- Added a button to the Local Prop Set inspector that allows for all currently selected GameObjects to be added to the list at once.

### Fixed
- Fixed a bug causing the bounding box of some rooms to be incorrect once rotated by DunGen.
- Fixed a bug causing the ChosenSeed variable to be set incorrectly after a failed generation attempt.


## 2.4.2

### Changed
- Doorways no longer have to be on the very edge of a room's bounds. DunGen will calculate the bounds as usual, then collapse them to fit the doorways where necessary.


## 2.4.1

### Fixed
- Fixed a bug preventing the seed from being set manually
- Fixed a bug causing branching depth to not behave as expected


## 2.4 - [2014/07/06]

### Added
- Added preliminary support for 2D dungeons. It's now possible to change the up-axis for dungeon generation inside the dungeon generator settings. 2D support hasn't been thoroughly tested yet but it is feature-complete.


## 2.3.1 - [2014/05/29]

### Added
- DunGen now supports SECTR portal culling. If you have the SECTR VIS extension, you will have automatic portal culling applied to your runtime and in-editor dungeons with minimal setup.
- Added door prefabs


## 2.2 - [2014/05/17]

### Added
- Added a new analysis tool that generates a set number of copies of a dungeon and presents detailed results such as success rate, average generation time, etc

### Changed
- It's no longer a requirement to make sure the prefab's position is set to (0,0,0) - not doing so will no longer cause the tile's position to be offset in the dungeon

### Fixed
- Fixed an issue that caused the timings returned by the generation stats to be inaccurate when the dungeon fails to generate the first time


## 2.1 - [2014/05/02]

### Added
- Lock Key system
- You can tell DunGen to connect doorways that overlap but were otherwise not connected during the generation process


## 2.0 - [2014/04/23]

### Added
- Implemented the dungeon flow editor. You can now control the flow of your procedural dungeons including the ability to add specific rooms at points on the main path
- Introduced two new types of asset: Dungeon Archetype and Tile Set which should allow for far more control when building a dungeon

### Changed
- **[Breaking Change]:** Complete rewrite from v1.x. Significant code and workflow changes.
- Homogenised object weights. Weights for all objects (tiles/rooms and all prop types) now contain a main path weight, a branch path weight, and a depth scale curve
- A lot of terminology and naming was changed, there's less ambiguity now when it comes to identifying key components of DunGen
- Cleaned up the UI a lot


## 1.0.1 - [2014/03/13]

### Added
- Added some height-varying rooms to the demo project to demonstrate multi-floor dungeons
- Added the option to generate a dungeon in the editor instead of at runtime


## 1.0 - [2014/03/07]

Initial Version