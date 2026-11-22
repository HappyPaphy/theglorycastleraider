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
}
