using UnityEngine;

/// <summary>
/// Look and behaviour of the minimap. Lives at Resources/UI/MinimapSettings (Tools > UI > Build Minimap Settings).
/// </summary>
[CreateAssetMenu(fileName = "MinimapSettings", menuName = "UI/Minimap Settings")]
public class MinimapSettings : ScriptableObject
{
    public const string ResourcePath = "UI/MinimapSettings";

    [Header("Placement (1920x1080 reference)")]
    [Tooltip("Diameter of the round minimap.")]
    public float size = 260f;
    [Tooltip("Distance from the top-right corner.")]
    public Vector2 margin = new Vector2(28f, 28f);

    [Header("Map")]
    [Tooltip("Map scale: screen pixels per metre. Higher = zoomed in.")]
    public float pixelsPerMeter = 7f;
    [Tooltip("Turn the map with the player so 'forward' is always up. Off = north is always up.")]
    public bool rotateWithPlayer = false;
    [Tooltip("Gap between rooms on the map, in metres.")]
    public float roomGap = 0.5f;
    [Tooltip("Rooms next to explored ones show faintly (fog of war). Off = only explored rooms show.")]
    public bool showNeighbourRooms = true;

    [Header("Colours")]
    public Color backgroundColor = new Color(0.05f, 0.04f, 0.03f, 0.6f);
    public Color borderColor = new Color(0.85f, 0.7f, 0.4f, 1f);
    public Color exploredRoomColor = new Color(0.62f, 0.56f, 0.46f, 0.9f);
    public Color currentRoomColor = new Color(0.95f, 0.85f, 0.6f, 1f);
    public Color neighbourRoomColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
    public Color doorColor = new Color(0.85f, 0.75f, 0.55f, 1f);
    public Color playerColor = new Color(1f, 0.95f, 0.85f, 1f);

    [Header("Room icons")]
    public float iconSize = 22f;
    public Sprite shopIcon;
    public Sprite shrineIcon;
    public Sprite challengeIcon;
    public Sprite treasureIcon;
    public Sprite exitIcon;
    public Sprite bossIcon;

    private static MinimapSettings cached;
    public static MinimapSettings Load()
    {
        if (cached == null) { cached = Resources.Load<MinimapSettings>(ResourcePath); }
        if (cached == null) { cached = CreateInstance<MinimapSettings>(); } // defaults if the asset is missing
        return cached;
    }
}
