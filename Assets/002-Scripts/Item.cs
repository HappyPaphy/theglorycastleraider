using UnityEngine;

public enum ItemCategory
{
    None,
    Consumable,
    Ring,
    Pyromancy,
    Magic,
    Weapon // Used by your inheriting Weapon class
}

public enum ItemType
{
    None,
    // Consumables (Max 99)
    HealthPotion_Small,
    HealthPotion_Medium,
    HealthPotion_Big,
    ManaPotion_Small,
    ManaPotion_Medium,
    ManaPotion_Big,

    // Rings (Max 1)
    RingOfVitality,
    RingOfStamina,

    // Pyromancies (Max 1)
    SmallFireball,
    LargeFireball,
    SmallCombustion,
    LargeCombustion,

    // Magics (Max 1)
    SmallSoulArrow,
    LargeSoulArrow,
}

public class Item : MonoBehaviour
{
    public ItemCategory itemCategory;
    public ItemType itemType;
    public Sprite spr_Icon_Frame;
    public Sprite spr_Icon_NoFrame;
    public string equipmentName;

    [SerializeField] protected float pickupRadius = 1.5f;
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected Vector3 spawnOffSet;

    [SerializeField] protected GameObject obj_RotateObject;
    [SerializeField] protected GameObject pickupEffect;
    [SerializeField] protected AudioClip pickupSound;

    [SerializeField] protected GameObject uiButtonPrompt;

    [Header("Spell Modifiers (Can be combined)")]
    [Tooltip("If true, the spell pauses for a duration before executing.")]
    public bool useDelay;
    public float castDelay = 0.5f;
    public float flatManaCost = 20f;
    public float flatStaminaCost = 16f;

    [Tooltip("If true, hold the button to build up power. Releases on button lift.")]
    public bool useCharge;
    public float maxChargeTime = 3f;
    public float chargeManaDrainRate = 15f;
    public float chargeStaminaDrainRate = 15f;

    [Tooltip("If true, hold the button to continuously fire (e.g. Flamethrower).")]
    public bool useContinuous;
    public float continuousSpawnRate = 0.25f;
    public float continuousManaDrainRate = 15f;
    public float continuousStaminaDrainRate = 15f;

    public GameObject spellPrefab;

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
        if (InventoryManager.instance.TryAddGeneralItem(this))
        {
            isCollected = true;
            uiButtonPrompt.SetActive(false);
            if (obj_RotateObject != null) obj_RotateObject.SetActive(false);
        }
        else
        {
            Debug.Log("Inventory full for this item type!");
            // Optional: Play a "Cannot pick up" sound or UI message here
        }
    }

    protected void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
