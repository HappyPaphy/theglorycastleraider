using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The choice under the Game Over text: Restart (new run straight away) or End Run (result panel, then main menu).
/// Mouse and gamepad; Restart is selected first.
/// </summary>
public static class GameOverMenu
{
    private static GameObject current;

    public static bool IsOpen => current != null;

    public static void Show(Action onRestart, Action onEndRun)
    {
        Hide();
        Canvas canvas = MetaUI.NewCanvas("Game Over Menu", 55);
        current = canvas.gameObject;
        CanvasGroup group = current.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // Under the Game Over text, lower middle of the screen
        RectTransform row = MetaUI.Box("Buttons", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(720f, 64f));

        RectTransform restartRt = MetaUI.Box("Restart", row, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(340f, 64f));
        Button restart = MetaUI.Button(restartRt, "Restart", 28f, out _);

        RectTransform endRt = MetaUI.Box("End Run", row, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(340f, 64f));
        Button end = MetaUI.Button(endRt, "End Run", 28f, out _);

        restart.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = end, selectOnLeft = end };
        end.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = restart, selectOnRight = restart };

        restart.onClick.AddListener(() => { Lock(restart, end); Hide(); onRestart?.Invoke(); });
        end.onClick.AddListener(() => { Lock(restart, end); Hide(); onEndRun?.Invoke(); });

        restart.interactable = end.interactable = false;
        group.DOFade(1f, 0.35f).SetUpdate(true).SetLink(current).OnComplete(() =>
        {
            if (restart == null) { return; }
            restart.interactable = end.interactable = true;
            if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(restart.gameObject); }
        });
    }

    public static void Hide()
    {
        if (current != null) { UnityEngine.Object.Destroy(current); }
        current = null;
    }

    private static void Lock(Button a, Button b) { a.interactable = false; b.interactable = false; }
}
