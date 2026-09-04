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
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Vector3 spawnOffSet;

    [SerializeField] private GameObject obj_RotateObject;
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private AudioClip pickupSound;

    [SerializeField] private GameObject uiButtonPrompt;

    private float rotationSpeed = 90f; // Degrees it spins per second
    private float hoverSpeed = 2f;     // How fast it moves up and down
    private float hoverHeight = 0.14f; // How high/low it moves from its center
    private Vector3 startPos;

    private void Start()
    {
        transform.position += spawnOffSet;
        startPos = transform.position;
        uiButtonPrompt.SetActive(false);
    }

    void Update()
    {
        HandleCollect();
        AnimateItem();
    }

    private void AnimateItem()
    {
        // Rotate the item continuously around its Y axis
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // Calculate a new Y position using a sine wave for smooth up/down motion
        float newY = startPos.y + (Mathf.Sin(Time.time * hoverSpeed) * hoverHeight);

        // Apply the new position while keeping the current X and Z
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void HandleCollect()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRadius, playerLayer);

        if (hitColliders.Length > 0)
        {
            uiButtonPrompt.SetActive(true);

            if(PlayerController.instance.IsInteractPressed)
            {
                PlayerController.instance.IsInteractPressed = false;
                CollectItem();
            }
        }
        else
        {
            uiButtonPrompt.SetActive(false);
        }
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
