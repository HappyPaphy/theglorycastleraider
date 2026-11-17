using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-centre banner during a challenge: name, wave, enemies left and the timer. Built in code on first use.
/// </summary>
public class ChallengeHud : MonoBehaviour
{
    private static ChallengeHud instance;
    private TMP_Text text;
    private CanvasGroup group;
    private ChallengeRoom room;
    private string status;
    private Tween hideTween;

    private static ChallengeHud Get()
    {
        if (instance == null) { instance = new GameObject("ChallengeHud").AddComponent<ChallengeHud>(); }
        return instance;
    }

    public static void Show(ChallengeRoom challengeRoom, string statusLine)
    {
        ChallengeHud hud = Get();
        hud.room = challengeRoom;
        hud.status = statusLine;
        hud.hideTween?.Kill();
        hud.group.DOKill();
        hud.group.DOFade(1f, 0.2f);
        hud.text.transform.DOKill(true);
        hud.text.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6);
        hud.Redraw();
    }

    public static void Refresh(ChallengeRoom challengeRoom)
    {
        if (instance == null) { return; }
        instance.room = challengeRoom;
        instance.Redraw();
    }

    public static void Hide()
    {
        if (instance == null) { return; }
        instance.hideTween?.Kill();
        instance.group.DOKill();
        instance.group.DOFade(0f, 0.3f);
    }

    public static void HideAfter(float seconds)
    {
        if (instance == null) { return; }
        instance.hideTween?.Kill();
        instance.hideTween = DOVirtual.DelayedCall(seconds, Hide);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform rt = new GameObject("Banner", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(1000f, 140f);

        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        GameFontSettings.Apply(t);
        t.alignment = TextAlignmentOptions.Top;
        t.fontSize = 36f;
        t.raycastTarget = false;
        t.outlineWidth = 0.22f;
        t.outlineColor = new Color32(0, 0, 0, 230);
        text = t;
    }

    private void Update()
    {
        if (room != null && room.CurrentState == ChallengeRoom.State.Running) { Redraw(); }
    }

    private void Redraw()
    {
        if (room == null || room.Challenge == null) { return; }
        ChallengeDefinition c = room.Challenge;

        string line2 = status;
        if (room.CurrentState == ChallengeRoom.State.Running && room.AliveCount > 0) { line2 += $"   -   {room.AliveCount} left"; }

        string timer = "";
        if (c.timeLimit > 0f && room.CurrentState == ChallengeRoom.State.Running)
        {
            float left = c.timeLimit - room.Elapsed;
            timer = left > 0f ? $"\n<size=70%>Time bonus: {left:0}s</size>" : "\n<size=70%><color=#888888>Time bonus missed</color></size>";
        }

        text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.displayName}</color>\n<size=80%>{line2}</size>{timer}";
    }
}
