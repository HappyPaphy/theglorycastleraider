using DG.Tweening;
using DunGen;
using DunGen.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class RoguelikeManager : MonoBehaviour
{
    public static event Action OnDungeonReady;
    public static bool IsDungeonReady { get; private set; }

    public int dungeonIndex = 0;

    public List<GameObject> obj_Enemies; 

    [SerializeField] private DungeonFlow[] dungeonFlows;
    [SerializeField] private RuntimeDungeon runtimeDungeon;
    [SerializeField] private NavMeshSurface navMeshSurface;

    [SerializeField] private Transform playerResetTransform;

    public List<Tile> allDungeonTiles = new List<Tile>();
    public Tile currentActiveTile;

    [SerializeField] private CanvasGroup canvasGroup_ProceedNextFloor;
    [SerializeField] private CanvasGroup canvasGroup_BlackFadeUI;

    [HideInInspector] public bool isPlayerInTheLastRoom = false;
    private bool isProceedNextFloorOnce = false;

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

    private void Start()
    {
        isPlayerInTheLastRoom = false; 

        SetUpUI();
    }

    private void Update()
    {
        obj_Enemies.RemoveAll(item => item == null);

        if (isPlayerInTheLastRoom)
        {
            if (canvasGroup_ProceedNextFloor.alpha == 0f)
            {
                canvasGroup_ProceedNextFloor.DOFade(1f, 0.3f).SetUpdate(false);
            }
            else if(canvasGroup_ProceedNextFloor.alpha == 1f)
            {
                canvasGroup_ProceedNextFloor.DOFade(0f, 0.3f).SetUpdate(false);
            }

            if (PlayerController.instance.IsMinimapPressed && !isProceedNextFloorOnce)
            {
                PlayerController.instance.IsMinimapPressed = false;
                isProceedNextFloorOnce = true;

                StartCoroutine(ProceedToNextFloor());
            }
        }
        else
        {
            canvasGroup_ProceedNextFloor.DOKill();
            canvasGroup_ProceedNextFloor.DOFade(0f, 0.3f).SetUpdate(false);
        }
        
    }

    private IEnumerator ProceedToNextFloor()
    {
        IsDungeonReady = false;
        canvasGroup_ProceedNextFloor.DOKill();
        canvasGroup_ProceedNextFloor.DOFade(0f, 0.3f).SetUpdate(false);
        canvasGroup_BlackFadeUI.DOFade(1f, 0.5f).SetUpdate(false);

        yield return new WaitForSeconds(1f);

        foreach(GameObject enemy in obj_Enemies)
        {
            Destroy(enemy);
        }

        allDungeonTiles.Clear();

        if(dungeonIndex < dungeonFlows.Length)
        {
            dungeonIndex++;
        }

        runtimeDungeon.Generator.Settings.DungeonFlow = dungeonFlows[dungeonIndex];  

        runtimeDungeon.Generate();
        isPlayerInTheLastRoom = false;
        isProceedNextFloorOnce = false;
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

            IsDungeonReady = true;

            if (EnemyDirector.instance != null)
            {
                EnemyDirector.instance.ResetDirector();
            }

            canvasGroup_ProceedNextFloor.DOKill();
            canvasGroup_ProceedNextFloor.alpha = 0f;
            canvasGroup_BlackFadeUI.DOFade(0f, 0.5f).SetUpdate(false);
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

    private void SetUpUI()
    {
        canvasGroup_BlackFadeUI.alpha = 1f;
        canvasGroup_ProceedNextFloor.alpha = 0f;
    }
}
