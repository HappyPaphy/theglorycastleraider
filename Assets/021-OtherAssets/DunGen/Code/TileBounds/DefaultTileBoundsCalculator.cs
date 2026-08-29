using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DunGen.TileBounds
{
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class DefaultTileBoundsCalculator : ITileBoundsCalculator
	{
		public bool IncludeRenderers = true;
		public bool IncludeSpriteRenderers = true;
		public bool IncludeColliders = true;
		public bool IncludeTriggerColliders = false;
		public bool IncludeTerrain = true;
		public bool IncludeInactive = false;
		public LayerMask LayerMask = ~0;


		/// <summary>
		/// Prepares the bounds of the specified root GameObject and its children for accurate rendering and calculations
		/// </summary>
		/// <remarks>This method compresses the bounds of all Tilemap components in the hierarchy to prevent oversized
		/// bounds. If ProBuilder is enabled, it also ensures that all ProBuilder meshes are synchronized before
		/// bounds are calculated<remarks>
		/// <param name="root">The root GameObject whose hierarchy will be processed to ensure all relevant bounds are up to date</param>
		protected void PrepareBounds(GameObject root)
		{
			// We need to compress the bounds of a tilemap first or the renderer will return bounds that are too big
			foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(IncludeInactive))
				tilemap.CompressBounds();

#if PROBUILDER

			// Ensure ProBuilder meshes are built before calculating bounds
			foreach (var proBuilderMesh in root.GetComponentsInChildren<UnityEngine.ProBuilder.ProBuilderMesh>())
			{
				// No need to rebuild if the mesh is already in sync
				if (proBuilderMesh.meshSyncState == UnityEngine.ProBuilder.MeshSyncState.InSync)
					continue;

				proBuilderMesh.ToMesh();
				proBuilderMesh.Refresh();
			}

#endif
		}

		/// <summary>
		/// Determines whether the specified component should be considered when calculating bounds
		/// </summary>
		/// <remarks>This method applies filtering based on the component's type, layer, and certain configuration
		/// properties such as LayerMask, IncludeTriggerColliders, and IncludeSpriteRenderers. Override this method to
		/// customize which components are included in bounds calculations</remarks>
		/// <param name="comp">The component to evaluate for inclusion in bounds calculations</param>
		/// <returns>true if the component is valid for bounds calculation</returns>
		protected virtual bool ShouldUseComponentBounds(Component comp)
		{
			if (comp == null)
				return false;

			// Layer check
			if (((1 << comp.gameObject.layer) & LayerMask) == 0)
				return false;

			// Collider
			if (comp is Collider collider)
			{
				// Terrain colliders report incorrect bounds when not placed in the scene
				if (collider is TerrainCollider)
					return false;

				if (!IncludeTriggerColliders && collider.isTrigger)
					return false;
			}
			// Renderer
			else if (comp is Renderer renderer)
			{
				if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
					return false;

				if (!IncludeSpriteRenderers && renderer is SpriteRenderer)
					return false;
			}

			return true;
		}

		public virtual Bounds CalculateLocalBounds(GameObject root)
		{
			var worldBounds = new Bounds();
			bool hasBounds = false;

			void Encapsulate(Bounds b)
			{
				if (!hasBounds)
				{
					worldBounds = b;
					hasBounds = true;
				}
				else
					worldBounds.Encapsulate(b);
			}

			PrepareBounds(root);

			// Renderers
			if (IncludeRenderers)
			{
				foreach (var renderer in root.GetComponentsInChildren<Renderer>(IncludeInactive))
				{
					if (!ShouldUseComponentBounds(renderer))
						continue;

					Encapsulate(renderer.bounds);
				}
			}

			// Colliders
			if (IncludeColliders)
			{
				foreach (var collider in root.GetComponentsInChildren<Collider>(IncludeInactive))
				{
					if (!ShouldUseComponentBounds(collider))
						continue;

					Encapsulate(collider.bounds);
				}
			}

			// Terrain
			if (IncludeTerrain)
			{
				foreach (var terrain in root.GetComponentsInChildren<Terrain>(IncludeInactive))
				{
					if (!ShouldUseComponentBounds(terrain))
						continue;

					var terrainBounds = terrain.terrainData.bounds;
					terrainBounds.center += terrain.gameObject.transform.position;

					Encapsulate(terrainBounds);
				}
			}

			// Fix any zero or negative extents
			const float minExtents = 0.01f;
			Vector3 extents = worldBounds.extents;

			if (extents.x == 0f)
				extents.x = minExtents;
			else if (extents.x < 0f)
				extents.x *= -1f;

			if (extents.y == 0f)
				extents.y = minExtents;
			else if (extents.y < 0f)
				extents.y *= -1f;

			if (extents.z == 0f)
				extents.z = minExtents;
			else if (extents.z < 0f)
				extents.z *= -1f;

			worldBounds.extents = extents;

			// Condense bounds around doorways
			worldBounds = UnityUtil.CondenseBounds(worldBounds, root.GetComponentsInChildren<Doorway>(true));

			// Convert tileBounds to local space
			return root.transform.InverseTransformBounds(worldBounds);
		}
	}
}