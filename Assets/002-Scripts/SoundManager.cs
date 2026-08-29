using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField] private int audioPoolSize = 30;
    private List<AudioSource> audioPool = new List<AudioSource>();

    [Header("Music")]
    [SerializeField] private AudioSource music_Background;

    [Header("Player")]
    [SerializeField] private AudioSource[] sfx_Player_Grunt;

    [Header("Human")]
    [SerializeField] private AudioSource[] sfx_Human_Grunt;
    [SerializeField] private AudioSource[] sfx_Human_Attack;
    [SerializeField] private AudioSource[] sfx_Human_Died;
    [SerializeField] private AudioSource[] sfx_Human_Parried;

    [SerializeField] private AudioSource[] sfx_Chicken;

    [Header("Sword Swing")]
    [SerializeField] private AudioSource[] sfx_SwordSwing_Air;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Flesh;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Metal;
    [SerializeField] private AudioSource[] sfx_SwordSound_Execute;
    [SerializeField] private AudioSource sfx_Parried; 
    [SerializeField] private AudioSource sfx_BowSound_String;
    [SerializeField] private AudioSource sfx_BowSound_Hit;

    [Header("FootStep")]
    [SerializeField] private AudioSource sfx_FootStep_Brick;

    [Header("Kick")]
    [SerializeField] private AudioSource sfx_Kick_Human;
    [SerializeField] private AudioSource sfx_Kick_Air;

    public static SoundManager instance;

    void Awake()
    {
        instance = this;

        InitializeAudioPool();
    }

    private void InitializeAudioPool()
    {
        // Generate the pool of reusable AudioSources at startup
        for (int i = 0; i < audioPoolSize; i++)
        {
            GameObject obj = new GameObject("PooledAudio_" + i);
            obj.transform.SetParent(this.transform); // Keep the hierarchy clean

            AudioSource source = obj.AddComponent<AudioSource>();
            source.spatialBlend = 1f; // Fully 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f; // Prevent pitch shifting on camera movement
            source.playOnAwake = false;

            audioPool.Add(source);
        }
    }

    public void PlaySound(AudioSource templateSource, Vector3 soundPosition, float maxDistance)
    {
        if (templateSource == null || templateSource.clip == null) return;

        AudioSource availableSource = GetAvailableAudioSource();

        if (availableSource != null)
        {
            // Move the pooled object to the sound's location
            availableSource.transform.position = soundPosition;

            // Apply the specific clip's settings
            availableSource.clip = templateSource.clip;
            availableSource.volume = templateSource.volume;
            availableSource.pitch = templateSource.pitch;
            availableSource.minDistance = 2f;
            availableSource.maxDistance = maxDistance;

            availableSource.Play();
        }
    }

    private AudioSource GetAvailableAudioSource()
    {
        // Loop through the pool to find one that isn't currently playing
        for (int i = 0; i < audioPool.Count; i++)
        {
            if (!audioPool[i].isPlaying)
            {
                return audioPool[i];
            }
        }

        // If all 30 sounds are playing at once, we just ignore the new sound 
        // to prevent audio clutter and protect performance.
        return null;
    }

    public void HumanSound_Grunt(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Grunt.Length);
        PlaySound(sfx_Human_Grunt[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Attack(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Attack.Length);
        PlaySound(sfx_Human_Attack[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Died(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Died.Length);
        PlaySound(sfx_Human_Died[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Parried(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Parried.Length);
        PlaySound(sfx_Human_Parried[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Air(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Air.Length);
        PlaySound(sfx_SwordSwing_Air[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Flesh(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Flesh.Length);
        PlaySound(sfx_SwordSwing_Flesh[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Metal(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Metal.Length);
        PlaySound(sfx_SwordSwing_Metal[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Execute(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSound_Execute.Length);
        PlaySound(sfx_SwordSound_Execute[rndIndex], soundPosition, 100f);
    }

    public void BowSound_String(Vector3 soundPosition)
    {
        PlaySound(sfx_BowSound_String, soundPosition, 100f);
    }

    public void BowSound_Hit(Vector3 soundPosition)
    {
        PlaySound(sfx_BowSound_Hit, soundPosition, 100f);
    }

    public void ParriedSound(Vector3 soundPosition)
    {
        PlaySound(sfx_Parried, soundPosition, 100f);
    }

    public void KickSound_Human(Vector3 soundPosition)
    {
        PlaySound(sfx_Kick_Human, soundPosition, 100f);
    }

    public void KickSound_Air(Vector3 soundPosition)
    {
        PlaySound(sfx_Kick_Air, soundPosition, 100f);
    }

    public void PlayerHurtSound(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Player_Grunt.Length);
        PlaySound(sfx_Player_Grunt[rndIndex], soundPosition, 100f);
    }

    public void ChickenSound(int index)
    {
        sfx_Chicken[index].Play();
    }
}
