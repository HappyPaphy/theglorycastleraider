using UnityEngine;

/// <summary>
/// Player look sensitivity, saved between sessions. Separate values for mouse and controller.
/// 1 = the game's default feel; the settings sliders run from Min to Max.
/// </summary>
public static class LookSensitivity
{
    public const float Min = 0.1f;
    public const float Max = 3f;
    public const float Default = 1f;

    private const string MouseKey = "LookSensitivity_Mouse";
    private const string ControllerKey = "LookSensitivity_Controller";

    private static float mouse = -1f, controller = -1f;

    public static float Mouse
    {
        get { if (mouse < 0f) { mouse = PlayerPrefs.GetFloat(MouseKey, Default); } return mouse; }
        set { mouse = Mathf.Clamp(value, Min, Max); PlayerPrefs.SetFloat(MouseKey, mouse); PlayerPrefs.Save(); }
    }

    public static float Controller
    {
        get { if (controller < 0f) { controller = PlayerPrefs.GetFloat(ControllerKey, Default); } return controller; }
        set { controller = Mathf.Clamp(value, Min, Max); PlayerPrefs.SetFloat(ControllerKey, controller); PlayerPrefs.Save(); }
    }

    /// <summary>The multiplier for the input device in use right now.</summary>
    public static float ForCurrentInput()
    {
        bool pc = InputSchemeManager.instance == null || InputSchemeManager.instance.CurrentInputMode == InputMode.PC;
        return pc ? Mouse : Controller;
    }
}
