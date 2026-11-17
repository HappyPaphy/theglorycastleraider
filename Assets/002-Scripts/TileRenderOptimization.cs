using System.Collections.Generic;
using DunGen;
using UnityEngine;

/// <summary>
/// Hides a room's structure when it's far from the player (more than a few rooms away) or outside the camera view,
/// and tells the RoguelikeManager which room the player is in.
///
/// Optimised: each room caches its bounds and renderers once; the camera frustum and the set of nearby rooms
/// are computed once per frame for the whole dungeon (the nearby set only when the player changes room);
/// renderers are only switched when the room's visibility actually changes. No per-frame allocations.
/// </summary>
public class TileRenderOptimization : MonoBehaviour
{
    public GameObject structureRoot;
    [SerializeField] private Tile tile;
    [SerializeField] private bool isThisTileStart = false;
    private const int MaxNeighborDepth = 4;

    // ---------- per room (cached once) ----------
    private Renderer[] structureRenderers;
    private Bounds bounds;
    private bool boundsReady;
    private int visibilityState = -1; // -1 unknown, 0 hidden, 1 shown
    private Transform playerTransform;

    // ---------- shared by every room, refreshed once per frame ----------
    private static Camera mainCam;
    private static readonly Plane[] FrustumPlanes = new Plane[6];
    private static int frustumFrame = -1;

    private static readonly HashSet<Tile> NearTiles = new HashSet<Tile>();
    private static readonly Queue<(Tile tile, int depth)> SearchQueue = new Queue<(Tile, int)>();
    private static Tile nearTilesCenter;

    private void Start()
    {
        if (isThisTileStart && RoguelikeManager.instance != null)
        {
            RoguelikeManager.instance.currentActiveTile = tile;
        }

        if (PlayerController.instance != null)
        {
            playerTransform = PlayerController.instance.transform;
        }

        if (RoguelikeManager.instance != null)
        {
            RoguelikeManager.instance.allDungeonTiles.Add(tile);
        }

        if (structureRoot != null)
        {
            structureRenderers = structureRoot.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void Update()
    {
        if (!boundsReady) { CacheBounds(); }

        CheckPlayerPosition();
        EvaluateVisibility();
    }

    // Bounds of everything in the room, measured once (the room doesn't move after the dungeon is built)
    private void CacheBounds()
    {
        bounds = CalculateBounds(tile);
        boundsReady = true;
    }

    private void EvaluateVisibility()
    {
        RoguelikeManager manager = RoguelikeManager.instance;
        if (manager == null) { return; }

        // 1. Nearby? (within a few rooms of the player's room)
        if (!IsTileWithinDepthRange(manager))
        {
            SetStructureVisible(false);
            return;
        }

        // 2. In the camera view?
        if (!RefreshFrustum()) { return; }
        SetStructureVisible(GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds));
    }

    // Camera frustum, computed once per frame for all rooms (non-allocating)
    private static bool RefreshFrustum()
    {
        if (mainCam == null || !mainCam.isActiveAndEnabled) { mainCam = Camera.main; }
        if (mainCam == null) { return false; }

        if (frustumFrame != Time.frameCount)
        {
            GeometryUtility.CalculateFrustumPlanes(mainCam, FrustumPlanes);
            frustumFrame = Time.frameCount;
        }
        return true;
    }

    private bool IsTileWithinDepthRange(RoguelikeManager manager)
    {
        Tile current = manager.currentActiveTile;
        if (current == null || current == tile) { return true; }

        // The nearby set only changes when the player changes room
        if (nearTilesCenter != current) { RebuildNearTiles(current); }
        return NearTiles.Contains(tile);
    }

    // Breadth-first search from the player's room, up to MaxNeighborDepth doorways away
    private static void RebuildNearTiles(Tile center)
    {
        nearTilesCenter = center;
        NearTiles.Clear();
        SearchQueue.Clear();

        NearTiles.Add(center);
        SearchQueue.Enqueue((center, 0));

        while (SearchQueue.Count > 0)
        {
            var (current, depth) = SearchQueue.Dequeue();
            if (depth >= MaxNeighborDepth || current == null || current.UsedDoorways == null) { continue; }

            foreach (Doorway doorway in current.UsedDoorways)
            {
                if (doorway == null || doorway.ConnectedDoorway == null) { continue; }
                Tile neighbor = doorway.ConnectedDoorway.Tile;
                if (neighbor != null && NearTiles.Add(neighbor))
                {
                    SearchQueue.Enqueue((neighbor, depth + 1));
                }
            }
        }
    }

    // Only touches the renderers when the visibility actually changes
    private void SetStructureVisible(bool visible)
    {
        int state = visible ? 1 : 0;
        if (state == visibilityState || structureRenderers == null) { return; }
        visibilityState = state;

        for (int i = 0; i < structureRenderers.Length; i++)
        {
            if (structureRenderers[i] != null) { structureRenderers[i].enabled = visible; }
        }
    }

    private void CheckPlayerPosition()
    {
        RoguelikeManager manager = RoguelikeManager.instance;
        if (manager == null || playerTransform == null || manager.currentActiveTile == tile) { return; }

        if (bounds.Contains(playerTransform.position))
        {
            manager.ChangeCurrentTile(tile);
        }
    }

    private static Bounds CalculateBounds(Tile tile)
    {
        // Everything rendered inside the room
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { return new Bounds(tile.transform.position, Vector3.one * 10f); }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) { b.Encapsulate(renderers[i].bounds); }
        return b;
    }

    private void OnDrawGizmos()
    {
        if (tile == null) { return; }

        Bounds b = boundsReady ? bounds : CalculateBounds(tile);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
