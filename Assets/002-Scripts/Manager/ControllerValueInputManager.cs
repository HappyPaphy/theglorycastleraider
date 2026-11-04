using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerValueInputManager : MonoBehaviour
{
    public static ControllerValueInputManager instance;

    [HideInInspector] public float modifiedValue = 0.1f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (modifiedValue > 0.1f)
            {
                modifiedValue -= 0.1f;
            }
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            modifiedValue += 0.1f;
        }
    }
}
