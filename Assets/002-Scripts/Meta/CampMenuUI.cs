using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Camp (main menu): spend Emeralds on permanent unlocks (MetaUnlocks). Opened from the main menu's Camp button.
/// Mouse and gamepad: rows are linked up/down, Cancel closes.
/// </summary>
public class CampMenuUI : MonoBehaviour
{
    private static CampMenuUI instance;
    public static bool IsOpen => instance != null;

    private class Row
    {
        public MetaUnlocks.Unlock unlock;
        public TextMeshProUGUI level, buyLabel;
        public Button buy;
    }

    private readonly List<Row> rows = new List<Row>();
    private TextMeshProUGUI gloryText, message;
    private float messageUntil;
    private Selectable returnTo;
    private Button back;

    public static void Open(Selectable returnTo)
    {
        if (instance != null) { return; }
        Canvas canvas = MetaUI.NewCanvas("Camp", 100);
        instance = canvas.gameObject.AddComponent<CampMenuUI>();
        instance.returnTo = returnTo;
        instance.Build(canvas);
    }

    public static void Close()
    {
        if (instance == null) { return; }
        Selectable back = instance.returnTo;
        Destroy(instance.gameObject);
        instance = null;
        if (back != null && EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(back.gameObject); }
    }

    private void OnEnable() { MetaProgress.OnChanged += Refresh; }
    private void OnDisable() { MetaProgress.OnChanged -= Refresh; }

    private void Update()
    {
        InputSchemeManager input = InputSchemeManager.instance;
        if (input != null && input.IsCancelPressed)
        {
            input.IsCancelPressed = false;
            Close();
            return;
        }

        // Keep something selected for gamepad (clicking empty space would clear it)
        if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
            !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)))
        {
            EventSystem.current.SetSelectedGameObject(rows.Count > 0 ? rows[0].buy.gameObject : back.gameObject);
        }

        if (message != null && message.text.Length > 0 && Time.unscaledTime > messageUntil) { message.text = ""; }
    }

    private void Build(Canvas canvas)
    {
        // Dim the menu behind and block clicks to it
        RectTransform shade = MetaUI.Rect("Shade", canvas.transform);
        MetaUI.Stretch(shade);
        MetaUI.Image(shade, new Color(0f, 0f, 0f, 0.8f), true);

        const float w = 1180f, h = 990f;
        RectTransform panel = MetaUI.Box("Panel", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        MetaUI.Image(panel, MetaUI.Panel);

        RectTransform title = MetaUI.Box("Title", panel, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(600f, 60f));
        MetaUI.Label(title, "CAMP", 46f, MetaUI.Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        RectTransform sub = MetaUI.Box("Sub", panel, new Vector2(0f, 1f), new Vector2(40f, -82f), new Vector2(800f, 30f));
        MetaUI.Label(sub, "Spend the Emeralds enemies drop on permanent unlocks.", 20f, MetaUI.Dim);

        RectTransform glory = MetaUI.Box("Emeralds", panel, new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(420f, 60f));
        gloryText = MetaUI.Label(glory, "", 34f, MetaUI.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        RectTransform stats = MetaUI.Box("Stats", panel, new Vector2(1f, 1f), new Vector2(-40f, -82f), new Vector2(560f, 30f));
        string best = MetaProgress.BestFloor >= 0 ? $"Floor {MetaProgress.BestFloor / 3 + 1}-{MetaProgress.BestFloor % 3 + 1}" : "-";
        MetaUI.Label(stats, $"Runs {MetaProgress.Runs}   Wins {MetaProgress.Wins}   Best {best}", 18f, MetaUI.Dim, TextAlignmentOptions.MidlineRight);

        // Unlock rows
        float y = -130f;
        const float rowH = 66f;
        MetaUnlocks.Kind? lastKind = null;
        foreach (MetaUnlocks.Unlock u in MetaUnlocks.All)
        {
            if (lastKind != u.kind)
            {
                RectTransform head = MetaUI.Box("Section", panel, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(w - 80f, 30f));
                MetaUI.Label(head, u.kind == MetaUnlocks.Kind.Content ? "NEW PERKS" : "BOOSTS", 18f, MetaUI.Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                y -= 32f;
                lastKind = u.kind;
            }

            RectTransform r = MetaUI.Box(u.name, panel, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(w - 80f, rowH - 6f));
            MetaUI.Image(r, new Color(1f, 1f, 1f, 0.04f));

            RectTransform name = MetaUI.Box("Name", r, new Vector2(0f, 1f), new Vector2(16f, -4f), new Vector2(640f, 30f));
            MetaUI.Label(name, u.name, 24f, MetaUI.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            RectTransform desc = MetaUI.Box("Desc", r, new Vector2(0f, 0f), new Vector2(16f, 4f), new Vector2(720f, 26f));
            MetaUI.Label(desc, u.description, 17f, MetaUI.Dim);

            var row = new Row { unlock = u };
            RectTransform lvl = MetaUI.Box("Level", r, new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(120f, 40f));
            row.level = MetaUI.Label(lvl, "", 20f, MetaUI.Dim, TextAlignmentOptions.MidlineRight);

            RectTransform buy = MetaUI.Box("Buy", r, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(200f, 46f));
            row.buy = MetaUI.Button(buy, "", 20f, out row.buyLabel);
            row.buy.onClick.AddListener(() => Buy(row));
            rows.Add(row);

            y -= rowH;
        }

        RectTransform msg = MetaUI.Box("Message", panel, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(700f, 40f));
        message = MetaUI.Label(msg, "", 22f, MetaUI.Bad);

        RectTransform backRt = MetaUI.Box("Back", panel, new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(220f, 52f));
        back = MetaUI.Button(backRt, "Back", 24f, out _);
        back.onClick.AddListener(Close);

        // Gamepad / keyboard: straight up/down through the Buy buttons, Back at the end
        for (int i = 0; i < rows.Count; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = i > 0 ? rows[i - 1].buy : (Selectable)back;
            nav.selectOnDown = i < rows.Count - 1 ? rows[i + 1].buy : (Selectable)back;
            rows[i].buy.navigation = nav;
        }
        back.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = rows.Count > 0 ? rows[rows.Count - 1].buy : null,
            selectOnDown = rows.Count > 0 ? rows[0].buy : null,
        };

        Refresh();
        if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(rows.Count > 0 ? rows[0].buy.gameObject : back.gameObject); }
    }

    private void Buy(Row row)
    {
        if (MetaProgress.IsMaxed(row.unlock)) { return; }
        if (!MetaProgress.TryBuy(row.unlock))
        {
            message.text = "Not enough Emeralds";
            messageUntil = Time.unscaledTime + 2f;
            return;
        }
        message.text = "";
        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Open(Vector3.zero); }
    }

    private void Refresh()
    {
        if (gloryText == null) { return; }
        gloryText.text = $"Emeralds  {MetaProgress.Emeralds}";
        foreach (Row row in rows)
        {
            int level = MetaProgress.GetLevel(row.unlock.id);
            int max = row.unlock.MaxLevel;
            int cost = MetaProgress.NextCost(row.unlock);
            row.level.text = max > 1 ? $"Lv {level} / {max}" : (level > 0 ? "Owned" : "");
            row.level.color = level > 0 ? MetaUI.Good : MetaUI.Dim;

            if (cost < 0)
            {
                row.buyLabel.text = max > 1 ? "Max" : "Unlocked";
                row.buyLabel.color = MetaUI.Good;
            }
            else
            {
                row.buyLabel.text = $"Buy  {cost}";
                row.buyLabel.color = MetaProgress.Emeralds >= cost ? MetaUI.Text : MetaUI.Bad;
            }
        }
    }
}
