using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RumbleManager : MonoBehaviour
{
    public static RumbleManager instance;
    private Gamepad pad;
    private Coroutine stopRumbleCoroutine;

    private float pad_lowFrequency = 0f;
    private float pad_highFrequency = 0f;

    private float nextStopRumble = 0f;

    private void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (pad != null)
        {
            pad.SetMotorSpeeds(pad_lowFrequency, pad_highFrequency);
        }

        if (InputSchemeManager.instance.CurrentInputMode != InputMode.Xbox &&
            InputSchemeManager.instance.CurrentInputMode != InputMode.PlayStation)
        {
            pad_lowFrequency = 0f;
            pad_highFrequency = 0f;
            nextStopRumble = 0f;

            return;
        }

        if (pad != null && Time.timeScale != 0)
        {

            if (nextStopRumble > 0f)
            {
                nextStopRumble -= Time.deltaTime;
            }
            else
            {
                pad_lowFrequency = 0f;
                pad_highFrequency = 0f;
            }
        }
        else
        {
            pad_lowFrequency = 0f;
            pad_highFrequency = 0f;
        }

    }

    public void RumblePulse(float lowFrequency, float highFrequency, float duration)
    {
        if (InputSchemeManager.instance.CurrentInputMode != InputMode.Xbox
        && InputSchemeManager.instance.CurrentInputMode != InputMode.PlayStation) { return; }

        pad = Gamepad.current;

        pad_lowFrequency = lowFrequency;
        pad_highFrequency = highFrequency;

        nextStopRumble = duration;
    }
}
