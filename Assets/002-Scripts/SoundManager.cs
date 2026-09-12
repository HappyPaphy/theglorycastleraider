using System.Collections.Generic;
using UnityEngine;

public enum DamageImpactSound
{
    None,
    MetalFlesh,
    FireFlesh
}

public enum SurfaceType
{
    Brick,
    Wood,
    Dirt,
    Water
}

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

    [Header("BigKnight")]
    [SerializeField] private AudioSource[] sfx_BigKnight_Grunt;
    [SerializeField] private AudioSource[] sfx_BigKnight_Attack;
    [SerializeField] private AudioSource[] sfx_BigKnight_Died;
    [SerializeField] private AudioSource[] sfx_BigKnight_Tired;

    [Header("Dog")]
    [SerializeField] private AudioSource[] sfx_Dog_Grunt;
    [SerializeField] private AudioSource[] sfx_Dog_Attack;
    [SerializeField] private AudioSource[] sfx_Dog_Died;

    [Header("Chicken")]
    [SerializeField] private AudioSource[] sfx_Chicken;

    [Header("Sword Swing")]
    [SerializeField] private AudioSource[] sfx_SwordSwing_Air;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Flesh;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Metal;
    [SerializeField] private AudioSource[] sfx_SwordSound_Execute;
    [SerializeField] private AudioSource sfx_BlockAndParried; 
    [SerializeField] private AudioSource sfx_BowSound_String;
    [SerializeField] private AudioSource sfx_BowSound_Hit;

    [SerializeField] private AudioSource[] sfx_FireSound_Impact;
    [SerializeField] private AudioSource[] sfx_FireSound_Combustion;
    public bool isCombustionSoundPlaying = false;

    [Header("Destructable")]
    [SerializeField] private AudioSource[] sfx_Wooden_Hit;
    [SerializeField] private AudioSource[] sfx_Wooden_Break;

    [Header("FootStep")]
    [SerializeField] private AudioSource[] sfx_FootStep_Brick;
    [SerializeField] private AudioSource[] sfx_FootStep_Wood;
    [SerializeField] private AudioSource[] sfx_FootStep_Dirt;
    [SerializeField] private AudioSource[] sfx_FootStep_Water;

    [Header("Kick")]
    [SerializeField] private AudioSource sfx_Kick_Human;
    [SerializeField] private AudioSource sfx_Kick_Air;

    [SerializeField] private AudioSource sfx_PotionDrinkingSound;

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

    public void PlayNewSound(AudioSource templateSource, Vector3 soundPosition, float maxDistance)
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

    public void PlaySound(AudioSource templateSource, Vector3 soundPosition, float maxDistance, bool isPlay)
    {
        if(isPlay)
        {
            templateSource.transform.position = soundPosition;
            templateSource.maxDistance = maxDistance;
            templateSource.Play();
        }
        else
        {
            templateSource.Stop();
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
        PlayNewSound(sfx_Human_Grunt[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Attack(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Attack.Length);
        PlayNewSound(sfx_Human_Attack[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Died(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Died.Length);
        PlayNewSound(sfx_Human_Died[rndIndex], soundPosition, 100f);
    }

    public void BigKnightSound_Grunt(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_BigKnight_Grunt.Length);
        PlayNewSound(sfx_BigKnight_Grunt[rndIndex], soundPosition, 100f);
    }

    public void BigKnightSound_Attack(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_BigKnight_Attack.Length);
        PlayNewSound(sfx_BigKnight_Attack[rndIndex], soundPosition, 100f);
    }

    public void BigKnightSound_Died(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_BigKnight_Died.Length);
        PlayNewSound(sfx_BigKnight_Died[rndIndex], soundPosition, 100f);
    }

    public void BigKnightSound_Tired(Vector3 soundPosition, bool isPlay)
    {
        int rndIndex = Random.Range(0, sfx_BigKnight_Tired.Length);
        PlaySound(sfx_BigKnight_Tired[rndIndex], soundPosition, 100f, isPlay);
    }

    public void DogSound_Grunt(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Dog_Grunt.Length);
        PlayNewSound(sfx_Dog_Grunt[rndIndex], soundPosition, 100f);
    }

    public void DogSound_Attack(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Dog_Attack.Length);
        PlayNewSound(sfx_Dog_Attack[rndIndex], soundPosition, 100f);
    }

    public void DogSound_Died(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Dog_Died.Length);
        PlayNewSound(sfx_Dog_Died[rndIndex], soundPosition, 100f);
    }

    public void HumanSound_Parried(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Human_Parried.Length);
        PlayNewSound(sfx_Human_Parried[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Air(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Air.Length);
        PlayNewSound(sfx_SwordSwing_Air[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Flesh(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Flesh.Length);
        PlayNewSound(sfx_SwordSwing_Flesh[rndIndex], soundPosition, 100f);
    }

    public void SwordSound_Metal(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Metal.Length);
        PlayNewSound(sfx_SwordSwing_Metal[rndIndex], soundPosition, 100f);
    }

    public void BlockOrParrySound(AudioClip[] clips,Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, clips.Length);
        sfx_BlockAndParried.clip = clips[rndIndex];
        PlayNewSound(sfx_BlockAndParried, soundPosition, 100f);
    }

    public void SwordSound_Execute(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_SwordSound_Execute.Length);
        PlayNewSound(sfx_SwordSound_Execute[rndIndex], soundPosition, 100f);
    }

    public void FireSound_Impact(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_FireSound_Impact.Length);
        PlayNewSound(sfx_FireSound_Impact[rndIndex], soundPosition, 100f);
    }

    public void FireSound_Combustion(Vector3 soundPosition, bool isPlay)
    {
        int rndIndex = Random.Range(0, sfx_FireSound_Combustion.Length);
        PlaySound(sfx_FireSound_Combustion[rndIndex], soundPosition, 100f, isPlay);
    }

    public void BowSound_String(Vector3 soundPosition)
    {
        PlayNewSound(sfx_BowSound_String, soundPosition, 100f);
    }

    public void BowSound_Hit(Vector3 soundPosition)
    {
        PlayNewSound(sfx_BowSound_Hit, soundPosition, 100f);
    }

    public void KickSound_Human(Vector3 soundPosition)
    {
        PlayNewSound(sfx_Kick_Human, soundPosition, 100f);
    }

    public void KickSound_Air(Vector3 soundPosition)
    {
        PlayNewSound(sfx_Kick_Air, soundPosition, 100f);
    }

    public void PlayerHurtSound(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Player_Grunt.Length);
        PlayNewSound(sfx_Player_Grunt[rndIndex], soundPosition, 100f);
    }

    public void ChickenSound(int index)
    {
        sfx_Chicken[index].Play();
    }

    public void PotionDrinkingSound(Vector3 soundPosition)
    {
        PlayNewSound(sfx_PotionDrinkingSound, soundPosition, 100f);
    }

    public void WoodenSound_Hit(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Wooden_Hit.Length);
        PlayNewSound(sfx_Wooden_Hit[rndIndex], soundPosition, 100f);
    }

    public void WoodenSound_Break(Vector3 soundPosition)
    {
        int rndIndex = Random.Range(0, sfx_Wooden_Break.Length);
        PlayNewSound(sfx_Wooden_Break[rndIndex], soundPosition, 100f);
    }

    public void PlayFootStep(Vector3 soundPosition, SurfaceType surfaceType)
    {
        AudioSource[] selectedSteps = null;

        switch (surfaceType)
        {
            case SurfaceType.Brick: selectedSteps = sfx_FootStep_Brick; break;
            case SurfaceType.Wood: selectedSteps = sfx_FootStep_Wood; break;
            case SurfaceType.Dirt: selectedSteps = sfx_FootStep_Dirt; break;
            case SurfaceType.Water: selectedSteps = sfx_FootStep_Water; break;
        }

        if (selectedSteps != null && selectedSteps.Length > 0)
        {
            int rndIndex = Random.Range(0, selectedSteps.Length);
            PlayNewSound(selectedSteps[rndIndex], soundPosition, 20f);
        }
    }
}
