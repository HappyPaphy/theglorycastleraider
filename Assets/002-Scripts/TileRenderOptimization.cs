using System.Collections.Generic;
using DunGen;
using UnityEngine;

public class TileRenderOptimization : MonoBehaviour
{
    public GameObject structureRoot;
    [SerializeField] private Tile tile;
    [SerializeField] private bool isThisTileStart = false;
    private int maxNeighborDepth = 4;

    private Camera mainCam;
    private Plane[] frustumPlanes;
    private Transform playerTransform;

    private void Start()
    {
        mainCam = Camera.main;

        if(isThisTileStart)
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
    }

    private void Update()
    {
        CheckPlayerPosition();
        EvaluateVisibility();
    }


    private void EvaluateVisibility()
    {
        if (mainCam == null || RoguelikeManager.instance == null) return;

        // 1. Check if this tile is the current active room or an immediate neighbor
        bool isWithinRange = IsTileWithinDepthRange();

        if (!isWithinRange)
        {
            // Too far away, force structure hidden
            SetStructureActive(false);
            return;
        }

        // 2. Check if the tile bounds are inside the camera's view frustum
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCam);
        Bounds bounds = GetTileBounds(tile);
        bool isInCameraView = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);

        // Room renders only if it's nearby AND visible to the camera
        SetStructureActive(isInCameraView);
    }

    private bool IsTileWithinDepthRange()
    {
        var manager = RoguelikeManager.instance;
        if (manager == null || manager.currentActiveTile == null) return true;

        if (manager.currentActiveTile == tile) return true;

        // Breadth-First Search (BFS) to find all connected tiles up to maxNeighborDepth
        HashSet<Tile> visited = new HashSet<Tile>();
        Queue<(Tile tile, int depth)> queue = new Queue<(Tile, int)>();

        queue.Enqueue((manager.currentActiveTile, 0));
        visited.Add(manager.currentActiveTile);

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();

            if (current == tile) return true;

            if (depth < maxNeighborDepth && current.UsedDoorways != null)
            {
                foreach (Doorway doorway in current.UsedDoorways)
                {
                    if (doorway.ConnectedDoorway != null && doorway.ConnectedDoorway.Tile != null)
                    {
                        Tile neighbor = doorway.ConnectedDoorway.Tile;
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, depth + 1));
                        }
                    }
                }
            }
        }

        return false;
    }

    private void SetStructureActive(bool state)
    {
        if (structureRoot == null) return;

        Renderer[] renderers = structureRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            rend.enabled = state;
        }
    }

    private void CheckPlayerPosition()
    { 
        if (RoguelikeManager.instance.currentActiveTile == tile) {return;}

        Bounds bounds = GetTileBounds(tile);

        if (bounds.Contains(playerTransform.position))
        {
            RoguelikeManager.instance.ChangeCurrentTile(tile);
        }
    }

    private Bounds GetTileBounds(Tile tile)
    {
        // Fallback bounds calculation based on renderers or colliders inside the tile
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(tile.transform.position, Vector3.one * 10f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void OnDrawGizmos()
    {
        if (tile == null) return;

        // Calculate bounds using your existing method
        Bounds bounds = GetTileBounds(tile);

        // Draw a green wire cube representing the tile's bounding box
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
