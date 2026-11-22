using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Saved Master / SFX / Music volumes (0..1, stored in PlayerPrefs) and applying them to the main mixer.
/// Used by the settings sliders and by the SoundManager, so every scene starts at the saved volumes.
/// </summary>
public static class VolumeSettings
{
    public enum Channel { Master, SFX, Music }

    // New keys: the old Saved*Volume values came from the -40..0 sliders and aren't valid 0..1 volumes
    private static readonly string[] Keys = { "Volume_Master", "Volume_SFX", "Volume_Music" };
    private static readonly string[] MixerParams = { "MasterVolume", "SFXVolume", "MusicVolume" };
    private static readonly float[] Defaults = { 0.8f, 0.8f, 0.6f };

    private static bool dirty;
    private static float saveAt;

    public static float Get(Channel c) => Mathf.Clamp01(PlayerPrefs.GetFloat(Keys[(int)c], Defaults[(int)c]));

    /// <summary>Set, apply to the mixer, and schedule a save to disk.</summary>
    public static void Set(Channel c, float value, AudioMixer mixer)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Keys[(int)c], value);
        Apply(c, value, mixer);
        dirty = true;
        saveAt = Time.unscaledTime + 0.5f; // saved half a second after you stop dragging
    }

    public static void ApplyAll(AudioMixer mixer)
    {
        if (mixer == null) { return; }
        for (int i = 0; i < Keys.Length; i++) { Apply((Channel)i, Get((Channel)i), mixer); }
    }

    private static void Apply(Channel c, float value, AudioMixer mixer)
    {
        if (mixer == null) { return; }
        // 0..1 -> decibels (0.0001 = -80 dB = silent, 1 = 0 dB)
        float db = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        mixer.SetFloat(MixerParams[(int)c], db);
    }

    /// <summary>Writes pending changes to disk (call regularly and when closing settings / quitting).</summary>
    public static void SaveIfDirty(bool force = false)
    {
        if (!dirty || (!force && Time.unscaledTime < saveAt)) { return; }
        PlayerPrefs.Save();
        dirty = false;
    }
}
