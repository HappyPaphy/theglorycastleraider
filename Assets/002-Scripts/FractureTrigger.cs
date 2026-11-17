using UnityEngine;

public enum MaterialType
{
    None,
    Wood,
    Vase
}

public class FractureTrigger : MonoBehaviour
{
    [SerializeField] private MaterialType materialType;

    public void RequestNavMeshBake()
    {
        if (RoguelikeManager.instance != null)
        {
            //StartCoroutine(RoguelikeManager.instance.BakeDungeonNavMesh(0.5f));
        }
    }

    public void TriggerMaterialSound_Break()
    {
        // Called by Fracture.onFracture on every breakable prop, so it's the hook for Splinter Storm
        if (PlayerPerkManager.Instance != null)
        {
            PlayerPerkManager.Instance.OnFurnitureBroken(transform.position);
        }

        switch (materialType)
        {
            case MaterialType.Wood:
                SoundManager.instance.WoodenSound_Break(transform.position);
                break;

            case MaterialType.Vase:
                //SoundManager.instance.WoodenSound_Break(transform.position);
                break;

        }
    }

    public void TriggerMaterialSound_Hit()
    {
        switch (materialType)
        {
            case MaterialType.Wood:
                SoundManager.instance.WoodenSound_Hit(transform.position);
                break;

            case MaterialType.Vase:
                //SoundManager.instance.WoodenSound_Hit(transform.position);
                break;

        }
    }
}
