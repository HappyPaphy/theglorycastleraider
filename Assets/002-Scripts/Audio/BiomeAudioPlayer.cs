using System.Collections;
using UnityEngine;

/// <summary>
/// Plays each biome's music (and optional ambience) from BiomeAudioSettings, crossfading when a new floor is ready.
/// Music goes through the SoundManager's music source's mixer group (so the Music volume slider applies);
/// ambience goes through the SFX group. Creates itself; no scene setup needed.
/// </summary>
public class BiomeAudioPlayer : MonoBehaviour
{
    private static BiomeAudioPlayer instance;

    private AudioSource musicA, musicB, ambienceA, ambienceB;
    private bool musicOnA = true, ambienceOnA = true;
    private Coroutine musicFade, ambienceFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        RoguelikeManager.OnDungeonReady -= OnDungeonReady;
        RoguelikeManager.OnDungeonReady += OnDungeonReady;
    }

    private static void OnDungeonReady()
    {
        if (RoguelikeManager.instance == null) { return; }
        BiomeAudioSettings settings = BiomeAudioSettings.Load();
        if (settings == null) { return; }

        if (instance == null) { instance = new GameObject("BiomeAudioPlayer").AddComponent<BiomeAudioPlayer>(); }
        instance.Play(settings, RoguelikeManager.instance.dungeonIndex);
    }

    private void Awake()
    {
        AudioSource template = SoundManager.instance != null ? SoundManager.instance.MusicSource : null;
        musicA = MakeSource(template != null ? template.outputAudioMixerGroup : null);
        musicB = MakeSource(template != null ? template.outputAudioMixerGroup : null);
        var sfxGroup = SoundManager.instance != null ? SoundManager.instance.SfxGroup : null;
        ambienceA = MakeSource(sfxGroup);
        ambienceB = MakeSource(sfxGroup);

        // The SoundManager's own music source stays silent so the two never play at once
        if (template != null) { template.Stop(); }
    }

    private AudioSource MakeSource(UnityEngine.Audio.AudioMixerGroup group)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.outputAudioMixerGroup = group;
        return s;
    }

    private void Play(BiomeAudioSettings settings, int floor)
    {
        BiomeAudioSettings.Biome biome = settings.ForFloor(floor);
        AudioClip music = null, ambience = null;
        float musicVolume = 0f, ambienceVolume = 0f;
        if (biome != null)
        {
            bool bossFloor = floor == biome.toFloor && biome.bossFloorMusic != null;
            music = bossFloor ? biome.bossFloorMusic : biome.music;
            musicVolume = biome.musicVolume;
            ambience = biome.ambience;
            ambienceVolume = biome.ambienceVolume;
        }

        Crossfade(ref musicA, ref musicB, ref musicOnA, ref musicFade, music, musicVolume, settings.crossfadeTime);
        Crossfade(ref ambienceA, ref ambienceB, ref ambienceOnA, ref ambienceFade, ambience, ambienceVolume, settings.crossfadeTime);
    }

    private void Crossfade(ref AudioSource a, ref AudioSource b, ref bool onA, ref Coroutine running, AudioClip clip, float volume, float time)
    {
        AudioSource current = onA ? a : b;
        if (current.clip == clip && (clip == null || current.isPlaying))
        {
            current.volume = volume; // same track: just keep playing
            return;
        }

        AudioSource next = onA ? b : a;
        onA = !onA;
        if (running != null) { StopCoroutine(running); }
        running = StartCoroutine(Fade(current, next, clip, volume, time));
    }

    private static IEnumerator Fade(AudioSource from, AudioSource to, AudioClip clip, float volume, float time)
    {
        to.clip = clip;
        to.volume = 0f;
        if (clip != null) { to.Play(); }

        float startFrom = from.volume;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = time > 0f ? t / time : 1f;
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            if (clip != null) { to.volume = Mathf.Lerp(0f, volume, k); }
            yield return null;
        }

        from.volume = 0f;
        from.Stop();
        from.clip = null;
        if (clip != null) { to.volume = volume; }
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
    }
}
