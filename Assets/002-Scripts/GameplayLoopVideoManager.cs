using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameplayLoopVideoManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup_PressAnybutton_Text;
    private bool isTextFadeOn = true;
    private bool isTransitioning = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        canvasGroup_PressAnybutton_Text.alpha = 1f;
    }

    // Update is called once per frame
    void Update()
    {
        HandlePressAnyButtonText();

        if (IsAnyKeyDown() && !isTransitioning)
        {
            isTransitioning = true;
            canvasGroup_PressAnybutton_Text.DOKill();
            canvasGroup_PressAnybutton_Text.alpha = 0f;

            AsyncLoaderManager.instance.LoadLevel("Mainmenu", false);
        }
    }

    private void HandlePressAnyButtonText()
    {
        if(isTransitioning) { return; }

        if (canvasGroup_PressAnybutton_Text.alpha == 1f && isTextFadeOn)
        {
            isTextFadeOn = false;
            canvasGroup_PressAnybutton_Text.DOFade(0f, 0.3f).SetUpdate(true);
        }
        else if (canvasGroup_PressAnybutton_Text.alpha == 0f && !isTextFadeOn)
        {
            isTextFadeOn = true;
            canvasGroup_PressAnybutton_Text.DOFade(1f, 0.3f).SetUpdate(true);
        }
    }

    private bool IsAnyKeyDown()
    {
        bool isKeyboard = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool isMouse = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);

        bool isGamepad = false;
        if (Gamepad.current != null)
        {
            // Checks standard gamepad face buttons and start button
            isGamepad = Gamepad.current.buttonSouth.wasPressedThisFrame ||
                        Gamepad.current.buttonEast.wasPressedThisFrame ||
                        Gamepad.current.buttonWest.wasPressedThisFrame ||
                        Gamepad.current.buttonNorth.wasPressedThisFrame ||
                        Gamepad.current.startButton.wasPressedThisFrame;
        }

        return isKeyboard || isMouse || isGamepad;
    }
}
