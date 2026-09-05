using UnityEngine;

public enum ItemType
{
    HealthPotion_Small,
    HealthPotion_Medium,
    HealthPotion_Big,
    ManaPotion_Small,
    ManaPotion_Medium,
    ManaPotion_Big
}

public class Item : MonoBehaviour
{
    [SerializeField] private ItemType itemType;
    [SerializeField] protected float pickupRadius = 1.5f;
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected Vector3 spawnOffSet;

    [SerializeField] protected GameObject obj_RotateObject;
    [SerializeField] protected GameObject pickupEffect;
    [SerializeField] protected AudioClip pickupSound;

    [SerializeField] protected GameObject uiButtonPrompt;

    protected bool isCollected = false;

    protected float rotationSpeed = 90f; // Degrees it spins per second
    protected float hoverSpeed = 2f;     // How fast it moves up and down
    protected float hoverHeight = 0.14f; // How high/low it moves from its center
    protected Vector3 startPos;

    protected virtual void Start()
    {
        transform.position += spawnOffSet;
        startPos = transform.position;
        uiButtonPrompt.SetActive(false);
    }

    protected virtual void Update()
    {
        HandleCollect();
        AnimateItem();
    }

    protected virtual void AnimateItem()
    {
        if(isCollected) { return; }

        // Rotate the item continuously around its Y axis
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // Calculate a new Y position using a sine wave for smooth up/down motion
        float newY = startPos.y + (Mathf.Sin(Time.time * hoverSpeed) * hoverHeight);

        // Apply the new position while keeping the current X and Z
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    protected virtual void HandleCollect()
    {
        if (isCollected) { return; }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRadius, playerLayer);

        if (hitColliders.Length > 0)
        {
            uiButtonPrompt.SetActive(true);

            if(PlayerController.instance.IsInteractPressed)
            {
                PlayerController.instance.IsInteractPressed = false;
                Collecting();
            }
        }
        else
        {
            uiButtonPrompt.SetActive(false);
        }
    }

    protected virtual void Collecting()
    {
        CollectItem();
    }

    private void CollectItem()
    {
        switch(itemType)
        {
            case ItemType.HealthPotion_Small:
                {
                    PlayerController.instance.CharacterHealthComponent.Heal(20);
                }
                break;

            case ItemType.HealthPotion_Medium:
                {
                    PlayerController.instance.CharacterHealthComponent.Heal(40);
                }
                break;

            case ItemType.HealthPotion_Big:
                {
                    PlayerController.instance.CharacterHealthComponent.Heal(60);
                }
                break;

            case ItemType.ManaPotion_Small:
                {
                    PlayerController.instance.CharacterUltimateComponent.GainUltimate(20);
                }
                break;

            case ItemType.ManaPotion_Medium:
                {
                    PlayerController.instance.CharacterUltimateComponent.GainUltimate(40);
                }
                break;

            case ItemType.ManaPotion_Big:
                {
                    PlayerController.instance.CharacterUltimateComponent.GainUltimate(60);
                }
                break;
        }

        Destroy(gameObject);
    }

    protected void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
