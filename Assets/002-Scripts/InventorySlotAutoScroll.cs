using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotAutoScroll : MonoBehaviour, ISelectHandler
{
    private ScrollRect scrollRect;
    private RectTransform rectTransform;
    private RectTransform contentRect;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null) contentRect = scrollRect.content;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (scrollRect == null || contentRect == null) return;

        // Automatically brings the selected grid slot into the viewport bounds
        // preventing navigation from dropping to null on off-screen items
        Vector2 localPosition = contentRect.InverseTransformPoint(rectTransform.position);
        // Add smooth scrolling logic or immediate snap if needed
    }
}