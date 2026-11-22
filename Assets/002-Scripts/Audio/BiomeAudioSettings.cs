using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Music and ambience per biome. Lives at Resources/Audio/BiomeAudio (Tools > Audio > Set Up Biome Audio).
/// Floor index: 0-2 = Prison (1-1 .. 1-3), 3-5 = Maze Yard (2-1 .. 2-3), 6-8 = Castle (3-1 .. 3-3).
/// Leave a clip empty to play nothing for it.
/// </summary>
[CreateAssetMenu(fileName = "BiomeAudio", menuName = "Audio/Biome Audio")]
public class BiomeAudioSettings : ScriptableObject
{
    public const string ResourcePath = "Audio/BiomeAudio";

    [System.Serializable]
    public class Biome
    {
        public string name = "Biome";
        [Tooltip("First floor index of this biome (0 = Floor 1-1).")]
        public int fromFloor;
        [Tooltip("Last floor index of this biome.")]
        public int toFloor;

        [Header("Music")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.6f;
        [Tooltip("Optional: plays instead of the music on this biome's last floor (the boss floor).")]
        public AudioClip bossFloorMusic;

        [Header("Ambience (optional loop under the music)")]
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    }

    [Tooltip("Seconds to fade from one track to the next.")]
    [Min(0f)] public float crossfadeTime = 2f;
    public List<Biome> biomes = new List<Biome>();

    private static BiomeAudioSettings cached;
    public static BiomeAudioSettings Load()
    {
        if (cached == null) { cached = Resources.Load<BiomeAudioSettings>(ResourcePath); }
        return cached;
    }

    public Biome ForFloor(int floor)
    {
        foreach (Biome b in biomes)
        {
            if (b != null && floor >= b.fromFloor && floor <= b.toFloor) { return b; }
        }
        return null;
    }
}
