using System.Collections.Generic;
using DunGen;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Round minimap in the top-right corner, drawn from the dungeon layout (not a camera):
/// explored rooms, doorways between them, faint neighbouring rooms (fog of war), room icons
/// (shop, shrine, challenge, treasure, exit, boss) and the player arrow in the middle.
/// Created automatically in any scene with a RoguelikeManager. Tuning: Resources/UI/MinimapSettings.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    private enum RoomKind { Normal, Shop, Shrine, Challenge, Treasure, Exit, Boss }

    private class RoomVisual
    {
        public Tile tile;
        public RoomKind kind;
        public Image room;
        public Image icon;
        public Image levelMarker;
        public int level = int.MinValue; // -1 below the player, 0 same level, 1 above (cached to reorder only on change)
        public readonly List<Image> doors = new List<Image>();
        public readonly List<Vector3> doorPositions = new List<Vector3>();
    }

    private static MinimapUI instance;
    private MinimapSettings settings;

    private RectTransform frame;
    private RectTransform content;
    private RectTransform roomLayer, doorLayer, iconLayer;
    private RectTransform playerArrow;
    private CanvasGroup group;

    private readonly Dictionary<Tile, RoomVisual> rooms = new Dictionary<Tile, RoomVisual>();
    private readonly HashSet<Tile> visited = new HashSet<Tile>();
    // Reused every frame (no per-frame allocations)
    private readonly HashSet<Tile> neighbours = new HashSet<Tile>();
    private readonly List<Tile> goneTiles = new List<Tile>();
    private static Sprite whiteSprite, circleSprite, ringSprite, arrowSprite;

    // =========================================================
    // AUTO CREATE
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

    private static void TryCreate()
    {
        if (instance != null || RoguelikeManager.instance == null) { return; }
        instance = new GameObject("Minimap").AddComponent<MinimapUI>();
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
    }

    // =========================================================
    // UI
    // =========================================================

    private void Awake()
    {
        settings = MinimapSettings.Load();
        BuildSprites();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Frame (top right)
        frame = NewRect("Frame", transform);
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 1f);
        frame.pivot = new Vector2(1f, 1f);
        frame.sizeDelta = Vector2.one * settings.size;
        frame.anchoredPosition = new Vector2(-settings.margin.x, -settings.margin.y);

        // Round, masked background
        RectTransform mask = NewRect("Mask", frame);
        Stretch(mask);
        Image bg = mask.gameObject.AddComponent<Image>();
        bg.sprite = circleSprite;
        bg.color = settings.backgroundColor;
        bg.raycastTarget = false;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        // Map content: moved and turned so the player stays in the middle
        content = NewRect("Content", mask);
        content.sizeDelta = Vector2.zero;
        roomLayer = NewRect("Rooms", content);
        doorLayer = NewRect("Doors", content);
        iconLayer = NewRect("Icons", content);

        // Player arrow (always in the middle, pointing up)
        RectTransform arrow = NewRect("Player", mask);
        playerArrow = arrow;
        arrow.sizeDelta = new Vector2(16f, 18f);
        Image arrowImage = arrow.gameObject.AddComponent<Image>();
        arrowImage.sprite = arrowSprite;
        arrowImage.color = settings.playerColor;
        arrowImage.raycastTarget = false;

        // Border ring (outside the mask)
        RectTransform ring = NewRect("Border", frame);
        Stretch(ring);
        Image ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = ringSprite;
        ringImage.color = settings.borderColor;
        ringImage.raycastTarget = false;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void LateUpdate()
    {
        RoguelikeManager manager = RoguelikeManager.instance;
        PlayerController player = PlayerController.instance;
        if (manager == null || player == null) { return; }

        group.alpha = IsHidden() ? 0f : 1f;

        SyncRooms(manager);

        if (manager.currentActiveTile != null) { visited.Add(manager.currentActiveTile); }

        // Neighbours of explored rooms (seen through their doorways)
        neighbours.Clear();
        if (settings.showNeighbourRooms)
        {
            foreach (Tile t in visited)
            {
                if (t == null || t.UsedDoorways == null) { continue; }
                foreach (Doorway d in t.UsedDoorways)
                {
                    if (d != null && d.ConnectedDoorway != null && d.ConnectedDoorway.Tile != null) { neighbours.Add(d.ConnectedDoorway.Tile); }
                }
            }
        }

        float feet = PlayerFeetY(player);

        foreach (RoomVisual v in rooms.Values)
        {
            bool isVisited = visited.Contains(v.tile);
            bool isCurrent = v.tile == manager.currentActiveTile;
            bool isNeighbour = !isVisited && neighbours.Contains(v.tile);
            bool show = isVisited || isNeighbour;

            // Height: rooms on other levels are faded and sit underneath the player's level
            int level = isCurrent ? 0 : LevelOf(v.tile.Bounds, feet);
            if (level != v.level) { ApplyLevelOrder(v, level); }
            float levelAlpha = level == 0 ? 1f : settings.otherLevelAlpha;

            v.room.enabled = show;
            if (show)
            {
                Color c = isCurrent ? settings.currentRoomColor : isVisited ? settings.exploredRoomColor : settings.neighbourRoomColor;
                c.a *= levelAlpha;
                v.room.color = c;
            }

            for (int i = 0; i < v.doors.Count; i++)
            {
                Image door = v.doors[i];
                door.enabled = isVisited;
                if (!isVisited) { continue; }
                // A doorway is on the player's level if its height matches, even in a room on another level
                bool doorSameLevel = Mathf.Abs(v.doorPositions[i].y - feet) < settings.ceilingClearance;
                Color dc = settings.doorColor;
                dc.a *= doorSameLevel || level == 0 ? 1f : settings.otherLevelAlpha;
                door.color = dc;
            }

            if (v.icon != null)
            {
                v.icon.enabled = show;
                Color ic = Color.white;
                ic.a = levelAlpha;
                v.icon.color = ic;
            }

            if (v.levelMarker != null)
            {
                v.levelMarker.enabled = show && level != 0 && settings.showLevelMarkers;
                v.levelMarker.rectTransform.localScale = new Vector3(1f, level > 0 ? 1f : -1f, 1f); // up = above, down = below
            }
        }

        // Move / turn the map around the player
        float yaw = settings.rotateWithPlayer ? player.transform.eulerAngles.y : 0f;
        Vector3 p = player.transform.position;
        Vector2 playerOnMap = new Vector2(p.x, p.z) * settings.pixelsPerMeter;
        Quaternion turn = Quaternion.Euler(0f, 0f, yaw);
        content.localRotation = turn;
        content.localPosition = turn * -playerOnMap;

        // North-up map: the arrow turns to show where the player faces. Rotating map: the arrow always points up.
        float facing = player.transform.eulerAngles.y;
        playerArrow.localRotation = settings.rotateWithPlayer ? Quaternion.identity : Quaternion.Euler(0f, 0f, -facing);

        // Icons stay upright
        Quaternion upright = Quaternion.Euler(0f, 0f, -yaw);
        foreach (RoomVisual v in rooms.Values)
        {
            if (v.icon != null) v.icon.rectTransform.localRotation = upright;
            if (v.levelMarker != null) v.levelMarker.rectTransform.localRotation = upright;
        }
    }

    // Bottom of the player's capsule (the transform pivot may not be at the feet)
    private static float PlayerFeetY(PlayerController player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) { return player.transform.position.y; }
        return cc.bounds.min.y;
    }

    // 0 = the player's level, 1 = above, -1 = below. Tall rooms (stair halls) count as the player's level
    // anywhere between their floor and ceiling.
    private int LevelOf(Bounds b, float feet)
    {
        float floor = b.min.y;
        float highestStand = Mathf.Max(floor, b.max.y - settings.ceilingClearance);
        if (feet >= floor - settings.levelTolerance && feet <= highestStand + settings.levelTolerance) { return 0; }
        return floor > feet ? 1 : -1;
    }

    // Player's level on top, other levels underneath
    private static void ApplyLevelOrder(RoomVisual v, int level)
    {
        v.level = level;
        if (level == 0)
        {
            v.room.rectTransform.SetAsLastSibling();
            foreach (Image d in v.doors) { d.rectTransform.SetAsLastSibling(); }
            if (v.icon != null) { v.icon.rectTransform.SetAsLastSibling(); }
        }
        else
        {
            v.room.rectTransform.SetAsFirstSibling();
            foreach (Image d in v.doors) { d.rectTransform.SetAsFirstSibling(); }
            if (v.icon != null) { v.icon.rectTransform.SetAsFirstSibling(); }
        }
    }

    // Keep the visuals in step with the dungeon's rooms (new floor = new rooms)
    private void SyncRooms(RoguelikeManager manager)
    {
        // Rooms that are gone (previous floor)
        goneTiles.Clear();
        foreach (Tile t in rooms.Keys) { if (t == null) goneTiles.Add(t); }
        if (goneTiles.Count > 0)
        {
            foreach (Tile t in goneTiles) { DestroyVisual(rooms[t]); rooms.Remove(t); }
            visited.RemoveWhere(t => t == null);
        }

        foreach (Tile tile in manager.allDungeonTiles)
        {
            if (tile == null || rooms.ContainsKey(tile) || !tile.HasValidBounds) { continue; }
            rooms[tile] = CreateVisual(tile);
        }
    }

    private RoomVisual CreateVisual(Tile tile)
    {
        var v = new RoomVisual { tile = tile, kind = DetectKind(tile) };
        float ppm = settings.pixelsPerMeter;

        Bounds b = tile.Bounds;
        RectTransform rt = NewRect("Room", roomLayer);
        rt.localPosition = new Vector2(b.center.x, b.center.z) * ppm;
        rt.sizeDelta = new Vector2(Mathf.Max(0.5f, b.size.x - settings.roomGap), Mathf.Max(0.5f, b.size.z - settings.roomGap)) * ppm;
        v.room = rt.gameObject.AddComponent<Image>();
        v.room.sprite = whiteSprite;
        v.room.raycastTarget = false;
        v.room.enabled = false;

        // Doorways this room actually uses
        if (tile.UsedDoorways != null)
        {
            foreach (Doorway d in tile.UsedDoorways)
            {
                if (d == null) { continue; }
                Vector3 dp = d.transform.position;
                RectTransform drt = NewRect("Door", doorLayer);
                drt.localPosition = new Vector2(dp.x, dp.z) * ppm;
                drt.sizeDelta = Vector2.one * Mathf.Max(4f, 1.2f * ppm);
                Image door = drt.gameObject.AddComponent<Image>();
                door.sprite = whiteSprite;
                door.color = settings.doorColor;
                door.raycastTarget = false;
                door.enabled = false;
                v.doors.Add(door);
                v.doorPositions.Add(dp);
            }
        }

        Sprite iconSprite = IconFor(v.kind);
        if (iconSprite != null)
        {
            RectTransform irt = NewRect("Icon", iconLayer);
            irt.localPosition = new Vector2(b.center.x, b.center.z) * ppm;
            irt.sizeDelta = Vector2.one * settings.iconSize;
            v.icon = irt.gameObject.AddComponent<Image>();
            v.icon.sprite = iconSprite;
            v.icon.preserveAspect = true;
            v.icon.raycastTarget = false;
            v.icon.enabled = false;
        }

        // Up/down arrow for rooms on another level (corner of the room, or centre if it is small)
        RectTransform mrt = NewRect("Level", iconLayer);
        Vector2 half = rt.sizeDelta * 0.5f;
        Vector2 corner = new Vector2(Mathf.Max(0f, half.x - settings.levelMarkerSize), Mathf.Max(0f, half.y - settings.levelMarkerSize));
        mrt.localPosition = (Vector2)rt.localPosition + (iconSprite != null ? corner : Vector2.zero);
        mrt.sizeDelta = Vector2.one * settings.levelMarkerSize;
        v.levelMarker = mrt.gameObject.AddComponent<Image>();
        v.levelMarker.sprite = arrowSprite;
        v.levelMarker.color = settings.levelMarkerColor;
        v.levelMarker.raycastTarget = false;
        v.levelMarker.enabled = false;

        return v;
    }

    private static RoomKind DetectKind(Tile tile)
    {
        if (tile.GetComponentInChildren<BossRoomTrigger>(true) != null) { return RoomKind.Boss; }
        if (tile.GetComponentInChildren<LastRoomTrigger>(true) != null) { return RoomKind.Exit; }
        if (tile.GetComponentInChildren<ShopKeeper>(true) != null) { return RoomKind.Shop; }
        if (tile.GetComponentInChildren<ShrineRoom>(true) != null) { return RoomKind.Shrine; }
        if (tile.GetComponentInChildren<ChallengeRoom>(true) != null) { return RoomKind.Challenge; }
        if (tile.GetComponentInChildren<TreasureChest>(true) != null) { return RoomKind.Treasure; }
        return RoomKind.Normal;
    }

    private Sprite IconFor(RoomKind kind)
    {
        switch (kind)
        {
            case RoomKind.Shop: return settings.shopIcon;
            case RoomKind.Shrine: return settings.shrineIcon;
            case RoomKind.Challenge: return settings.challengeIcon;
            case RoomKind.Treasure: return settings.treasureIcon;
            case RoomKind.Exit: return settings.exitIcon;
            case RoomKind.Boss: return settings.bossIcon;
            default: return null;
        }
    }

    private static void DestroyVisual(RoomVisual v)
    {
        if (v.room != null) { Destroy(v.room.gameObject); }
        if (v.icon != null) { Destroy(v.icon.gameObject); }
        if (v.levelMarker != null) { Destroy(v.levelMarker.gameObject); }
        foreach (Image d in v.doors) { if (d != null) Destroy(d.gameObject); }
    }

    private static bool IsHidden()
    {
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return true; }
        if (EquipmentLoadOut.instance != null && EquipmentLoadOut.instance.isPanelActive) { return true; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return true; }
        if (ShopUI.IsOpen) { return true; }
        PlayerController player = PlayerController.instance;
        return player != null && player.CharacterHealthComponent.CurrentHP <= 0f;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Plain white, a filled circle, a ring and an arrow, drawn once in code
    private static void BuildSprites()
    {
        if (whiteSprite != null) { return; }

        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

        const int size = 128;
        float c = (size - 1) * 0.5f;
        circleSprite = MakeSprite(size, (x, y) =>
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            return Mathf.Clamp01(c - d + 0.5f);
        });
        ringSprite = MakeSprite(size, (x, y) =>
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float outer = Mathf.Clamp01(c - d + 0.5f);
            float inner = Mathf.Clamp01(c - 4f - d + 0.5f);
            return outer - inner;
        });

        const int a = 32;
        arrowSprite = MakeSprite(a, (x, y) =>
        {
            // Triangle pointing up with a notch at the back
            float u = (x + 0.5f) / a - 0.5f, v = (y + 0.5f) / a;
            float halfWidth = (1f - v) * 0.5f;
            bool inTri = v >= 0f && v <= 1f && Mathf.Abs(u) <= halfWidth;
            bool notch = v < 0.3f && Mathf.Abs(u) < (0.3f - v) * 0.8f;
            return inTri && !notch ? 1f : 0f;
        });
    }

    private static Sprite MakeSprite(int size, System.Func<int, int, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
