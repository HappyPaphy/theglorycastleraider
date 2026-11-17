using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One line in the shop list: icon, name, price. Tells the ShopUI when it is selected or clicked.</summary>
public class ShopRowUI : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public Button button;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public TMP_Text stockText;
    [Tooltip("Shown when this item is sold out.")]
    public GameObject soldOutOverlay;
    [Tooltip("Highlighted while this row is selected.")]
    public Image selectionFrame;

    [HideInInspector] public int index;

    public event Action<int> Selected;
    public event Action<int> Clicked;

    private void Awake()
    {
        if (button != null) { button.onClick.AddListener(() => Clicked?.Invoke(index)); }
    }

    public void OnSelect(BaseEventData eventData) => Selected?.Invoke(index);

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Mouse hover selects the row, so keyboard/gamepad and mouse stay in sync
        if (EventSystem.current != null && button != null) { EventSystem.current.SetSelectedGameObject(button.gameObject); }
    }

    public void SetHighlighted(bool on)
    {
        if (selectionFrame != null) { selectionFrame.enabled = on; }
    }
}
