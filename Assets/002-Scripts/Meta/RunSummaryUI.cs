using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// End-of-run result panel: the Emeralds the run earned, the total, and progress to the next Camp unlock,
/// with a "Return to Main Menu" button. Shown when the player ends the run from the Game Over menu, and after a win.
/// </summary>
public static class RunSummaryUI
{
    private static GameObject current;

    /// <summary>True while the panel is waiting for its button (cursor / input like a menu).</summary>
    public static bool HasFocus => current != null;

    /// <summary>Shows the panel; the button calls onContinue.</summary>
    public static void Show(bool won, Action onContinue) => Build(won, onContinue);

    /// <summary>Shows the panel and waits for its button (use inside a coroutine).</summary>
    public static IEnumerator ShowAndWait(bool won)
    {
        bool done = false;
        Build(won, () => done = true);
        while (!done) { yield return null; }
    }

    public static void Hide()
    {
        if (current != null) { UnityEngine.Object.Destroy(current); }
        current = null;
    }

    private static void Build(bool won, Action onContinue)
    {
        Hide();
        Canvas canvas = MetaUI.NewCanvas("Run Summary", 60);
        current = canvas.gameObject;
        CanvasGroup group = current.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        const float w = 620f, h = 720f;
        RectTransform panel = MetaUI.Box("Panel", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        MetaUI.Image(panel, MetaUI.Panel);
        RectTransform accent = MetaUI.Box("Accent", panel, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, 5f));
        MetaUI.Image(accent, MetaUI.Gold);

        float y = -30f;
        RectTransform Row(float height) { var r = MetaUI.Box("Row", panel, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(w - 80f, height)); y -= height; return r; }

        MetaUI.Label(Row(56f), won ? "VICTORY" : "RUN OVER", 44f, won ? MetaUI.Good : MetaUI.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        MetaUI.Label(Row(34f), "Reached " + FloorName(MetaRun.FloorReached), 22f, MetaUI.Dim, TextAlignmentOptions.Center);
        y -= 18f;

        // Emerald breakdown
        bool any = false;
        foreach (MetaRun.Line line in MetaRun.Lines)
        {
            any = true;
            RectTransform r = Row(34f);
            MetaUI.Label(r, line.count > 1 ? $"{line.label}  x{line.count}" : line.label, 22f, MetaUI.Text);
            MetaUI.Label(r, $"+{line.emeralds}", 22f, MetaUI.Gold, TextAlignmentOptions.MidlineRight);
        }
        if (!any) { MetaUI.Label(Row(34f), "No Emeralds collected this run", 22f, MetaUI.Dim); }

        y -= 10f;
        MetaUI.Image(Row(2f), new Color(MetaUI.Gold.r, MetaUI.Gold.g, MetaUI.Gold.b, 0.5f));
        y -= 10f;

        RectTransform earned = Row(44f);
        MetaUI.Label(earned, "Emeralds earned", 28f, MetaUI.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        MetaUI.Label(earned, $"+{MetaRun.RunEmeralds}", 30f, MetaUI.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        RectTransform total = Row(36f);
        MetaUI.Label(total, "Total Emeralds", 22f, MetaUI.Dim);
        MetaUI.Label(total, MetaProgress.Emeralds.ToString(), 22f, MetaUI.Text, TextAlignmentOptions.MidlineRight);

        // Next unlock
        y -= 18f;
        MetaUnlocks.Unlock next = MetaUnlocks.NextGoal();
        if (next != null)
        {
            int cost = MetaProgress.NextCost(next);
            bool ready = MetaProgress.Emeralds >= cost;
            MetaUI.Label(Row(32f), ready ? $"Ready to unlock: {next.name}" : $"Next unlock: {next.name}", 22f, ready ? MetaUI.Good : MetaUI.Text);
            MetaUI.Bar(Row(18f), cost > 0 ? (float)MetaProgress.Emeralds / cost : 1f, ready ? MetaUI.Good : MetaUI.Gold);
            MetaUI.Label(Row(30f), $"{Mathf.Min(MetaProgress.Emeralds, cost)} / {cost} Emeralds", 18f, MetaUI.Dim, TextAlignmentOptions.MidlineRight);
        }
        else
        {
            MetaUI.Label(Row(32f), "Everything at the Camp is unlocked!", 22f, MetaUI.Good);
        }

        // Footer + button
        RectTransform footer = MetaUI.Box("Footer", panel, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(w - 80f, 30f));
        MetaUI.Label(footer, "Spend Emeralds at the Camp in the main menu.", 18f, MetaUI.Dim, TextAlignmentOptions.Center);

        RectTransform buttonRt = MetaUI.Box("Continue", panel, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(340f, 56f));
        Button button = MetaUI.Button(buttonRt, "Return to Main Menu", 24f, out _);
        button.interactable = false; // enabled after a short moment so a held button doesn't skip it
        button.onClick.AddListener(() =>
        {
            button.interactable = false;
            Hide();
            onContinue?.Invoke();
        });

        group.DOFade(1f, 0.4f).SetUpdate(true).SetLink(current).OnComplete(() =>
        {
            if (button == null) { return; }
            button.interactable = true;
            if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(button.gameObject); }
        });
    }

    private static string FloorName(int index)
    {
        RoguelikeManager m = RoguelikeManager.instance;
        string n = m != null && m.str_DungeonIndex != null && index >= 0 && index < m.str_DungeonIndex.Length ? m.str_DungeonIndex[index] : null;
        return string.IsNullOrEmpty(n) ? $"Floor {index / 3 + 1}-{index % 3 + 1}" : n.Replace("\n", " ");
    }
}
