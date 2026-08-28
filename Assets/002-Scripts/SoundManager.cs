using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioSource music_Background;

    [Header("Player")]
    [SerializeField] private AudioSource[] sfx_Player_Grunt;

    [Header("Human")]
    [SerializeField] private AudioSource[] sfx_Human_Grunt;
    [SerializeField] private AudioSource[] sfx_Human_Attack;
    [SerializeField] private AudioSource[] sfx_Human_Died;
    [SerializeField] private AudioSource[] sfx_Human_Parried;


    [Header("Sword Swing")]
    [SerializeField] private AudioSource[] sfx_SwordSwing_Air;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Flesh;
    [SerializeField] private AudioSource[] sfx_SwordSwing_Metal;
    [SerializeField] private AudioSource[] sfx_SwordSound_Execute;
    [SerializeField] private AudioSource sfx_Parried; 
    [SerializeField] private AudioSource sfx_BowSound_String;

    [Header("FootStep")]
    [SerializeField] private AudioSource sfx_FootStep_Brick;

    [Header("Kick")]
    [SerializeField] private AudioSource sfx_Kick_Human;
    [SerializeField] private AudioSource sfx_Kick_Air;

    public static SoundManager instance;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void HumanSound_Grunt()
    {
        int rndIndex = Random.Range(0, sfx_Human_Grunt.Length);
        sfx_Human_Grunt[rndIndex].Play();
    }

    public void HumanSound_Attack()
    {
        int rndIndex = Random.Range(0, sfx_Human_Attack.Length);
        sfx_Human_Attack[rndIndex].Play();
    }

    public void HumanSound_Died()
    {
        int rndIndex = Random.Range(0, sfx_Human_Died.Length);
        sfx_Human_Died[rndIndex].Play();
    }

    public void HumanSound_Parried()
    {
        int rndIndex = Random.Range(0, sfx_Human_Parried.Length);
        sfx_Human_Parried[rndIndex].Play();
    }

    public void SwordSound_Air()
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Air.Length);
        sfx_SwordSwing_Air[rndIndex].Play();
    }

    public void SwordSound_Flesh()
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Flesh.Length);
        sfx_SwordSwing_Flesh[rndIndex].Play();
    }

    public void SwordSound_Metal()
    {
        int rndIndex = Random.Range(0, sfx_SwordSwing_Metal.Length);
        sfx_SwordSwing_Metal[rndIndex].Play();
    }

    public void SwordSound_Execute()
    {
        int rndIndex = Random.Range(0, sfx_SwordSound_Execute.Length);
        sfx_SwordSound_Execute[rndIndex].Play();
    }

    public void BowSound_String()
    {
        sfx_BowSound_String.Play();
    }

    public void ParriedSound()
    {
        sfx_Parried.Play();
    }

    public void KickSound_Human()
    {
        sfx_Kick_Human.Play();
    }

    public void KickSound_Air()
    {
        sfx_Kick_Air.Play();
    }

    public void PlayerHurtSound()
    {
        int rndIndex = Random.Range(0, sfx_Player_Grunt.Length);
        sfx_Player_Grunt[rndIndex].Play();
    }
}
