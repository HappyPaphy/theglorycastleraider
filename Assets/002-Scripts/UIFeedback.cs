using DG.Tweening;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum UISelectType
{
    Scale,
    FillPanel
}

public class UIFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private UISelectType selectType = UISelectType.Scale;

    [SerializeField] private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private GameObject gameObjectUI;
    [SerializeField] private GameObject text;
    [SerializeField] private Byte alpha = 60;
    [SerializeField] private GameObject activeGameObjectOnHover;

    private bool isSelected = false;
    private bool isHovered = false;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private RectTransform rectTransform;
    private AudioSource audioSource;
    private Image image;
    

    [Header("Sound Settings")]
    [SerializeField] private AudioClip clickSound;

    void Awake()
    {
        if(gameObjectUI != null)
        {
            audioSource = gameObjectUI.GetComponent<AudioSource>();
            rectTransform = gameObjectUI.GetComponent<RectTransform>();
            image = gameObjectUI.GetComponent<Image>();
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
        }
    }

    void Start()
    {
        switch (selectType)
        {
            case UISelectType.Scale:
                {
                    originalScale = rectTransform.localScale;
                }
                break;

            case UISelectType.FillPanel:
                {
                    originalScale = text.GetComponent<RectTransform>().localScale;
                }
                break;
        }

        targetScale = originalScale;
    }

    void Update()
    {
        switch(selectType)
        {
            case UISelectType.Scale:
                {
                    rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
                }
                break;

            case UISelectType.FillPanel:
                {
                    text.GetComponent<RectTransform>().localScale = Vector3.Lerp(text.GetComponent<RectTransform>().localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
                }
                break;
        }

        if(activeGameObjectOnHover != null)
        {
            if(isHovered || isSelected)
            {
                if(!activeGameObjectOnHover.activeInHierarchy)
                {
                    activeGameObjectOnHover.SetActive(true);
                }
            }
            else if(!isHovered && !isSelected)
            {
                if (activeGameObjectOnHover.activeInHierarchy)
                {
                    activeGameObjectOnHover.SetActive(false);
                }
            }
        }
    }

    void LateUpdate()
    {
        switch (GameManager.instance.navigationMode)
        {
            case UINavigtionMode.Select:
                UnHoveredUI();
                FeedbackUI(isSelected);
                break;

            case UINavigtionMode.Pointer:
                FeedbackUI(isHovered);
                break;
        }
    }

    public void UnSelectedUI()
    {
        isSelected = false;
    }

    public void UnHoveredUI()
    {
        isHovered = false;
    }

    private void FeedbackUI(bool isTarget)
    {
        switch(selectType)
        {
            case UISelectType.Scale:
                {

                    Color currentColor = new Color();

                    if(image != null)
                        currentColor = image.color;

                    if (isTarget && EventSystem.current.currentSelectedGameObject == gameObject)
                    {
                        targetScale = hoverScale;
                        currentColor.a = 1.0f;

                        if(image != null)
                            image.color = currentColor;
                    }
                    else
                    {
                        targetScale = originalScale;
                        currentColor.a = alpha / 255f;

                        if (image != null)
                            image.color = currentColor;
                    }
                }
                break;

            case UISelectType.FillPanel:
                {
                    if (isTarget && EventSystem.current.currentSelectedGameObject == gameObject)
                    {

                        if (image != null)
                        {
                            image.DOKill();
                            image.DOFillAmount(1f, 0.125f).SetEase(Ease.OutQuad).SetUpdate(true);
                        }

                        targetScale = hoverScale;
                    }
                    else
                    {
                        if (image != null)
                        {
                            image.DOKill();
                            image.DOFillAmount(0f, 0.125f).SetEase(Ease.OutQuad).SetUpdate(true);
                        }

                        targetScale = originalScale;
                    }
                }
                break;
        }
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GameManager.instance.navigationMode != UINavigtionMode.Pointer) { return; }

        EventSystem.current.SetSelectedGameObject(gameObject);
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
    public void OnSelect(BaseEventData eventData)
    {
        if (GameManager.instance.navigationMode != UINavigtionMode.Select) { return; }

        EventSystem.current.SetSelectedGameObject(gameObject);
        isSelected = true;
    }

    // SELECTION LOST: Fires when selection moves to another object
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
    }
}
