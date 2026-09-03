using System;
using DunGen;
using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using System.Collections.Generic;

public class RoguelikeManager : MonoBehaviour
{
    public static event Action OnDungeonReady;
    public static bool IsDungeonReady { get; private set; }

    [SerializeField] private RuntimeDungeon runtimeDungeon;
    [SerializeField] private NavMeshSurface navMeshSurface;

    [SerializeField] private Transform playerResetTransform;

    public List<Tile> allDungeonTiles = new List<Tile>();
    public Tile currentActiveTile;

    public static RoguelikeManager instance;

    private void Awake()
    {
        instance = this;
        IsDungeonReady = false;
    }

    private void OnEnable()
    {
        if (runtimeDungeon != null)
        {
            runtimeDungeon.Generator.OnGenerationStatusChanged += HandleGenerationStatusChanged;
        }
    }

    private void OnDisable()
    {
        if (runtimeDungeon != null)
        {
            runtimeDungeon.Generator.OnGenerationStatusChanged -= HandleGenerationStatusChanged;
        }
    }

    private void Update()
    {
        if(PlayerController.instance.IsMinimapPressed)
        {
            PlayerController.instance.IsMinimapPressed = false;
            allDungeonTiles.Clear();
            runtimeDungeon.Generate();
        }
    }

    private void HandleGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
    {
        // Only trigger the bake when the dungeon is 100% finished

        if (status != GenerationStatus.Complete)
        {
            IsDungeonReady = false;
        }

        if (status == GenerationStatus.Complete)
        {
            StartCoroutine(TeleportPlayerSafe());

            if (EnemyDirector.instance != null)
            {
                EnemyDirector.instance.ResetDirector();
            }

            BakeDungeonNavMesh();
        }
    }

    private IEnumerator TeleportPlayerSafe()
    {
        // Wait exactly one physics step so DunGen's new floor colliders wake up and become solid
        yield return new WaitForFixedUpdate();

        if (PlayerController.instance != null && playerResetTransform != null)
        {
            // If you are using a CharacterController, it MUST be disabled before teleporting
            CharacterController cc = PlayerController.instance.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Teleport the player
            PlayerController.instance.transform.position = playerResetTransform.position;

            // Force Unity's physics engine to instantly recognize the new position
            Physics.SyncTransforms();

            // Turn the controller back on
            if (cc != null) cc.enabled = true;
        }
    }

    public void BakeDungeonNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();

            IsDungeonReady = true;
            OnDungeonReady?.Invoke();

        }
    }

    public void ChangeCurrentTile(Tile newTile)
    {
        if (currentActiveTile == null) { return; }

        currentActiveTile = newTile;
        //UpdateRoomVisibility();
    }

    public void UpdateRoomVisibility()
    {
        if (currentActiveTile == null) return;

        // Gather current tile + immediate neighbors (depth 1) using UsedDoorways
        HashSet<Tile> visibleTiles = new HashSet<Tile> { currentActiveTile };

        if (currentActiveTile.UsedDoorways != null)
        {
            foreach (Doorway doorway in currentActiveTile.UsedDoorways)
            {
                if (doorway.ConnectedDoorway != null && doorway.ConnectedDoorway.Tile != null)
                {
                    visibleTiles.Add(doorway.ConnectedDoorway.Tile);
                }
            }
        }

        // Enable or disable renderers based on the visibility set
        foreach (Tile tile in allDungeonTiles)
        {
            if (tile == null) continue;
            bool isVisible = visibleTiles.Contains(tile);
            //SetTileRenderersActive(tile, isVisible);
        }
    }

    private void SetTileRenderersActive(Tile tile, bool state)
    {
        Renderer[] renderers = tile.GetComponent<TileRenderOptimization>().structureRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            rend.enabled = state;
        }
    }
}
