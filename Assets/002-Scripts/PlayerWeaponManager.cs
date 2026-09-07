using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum PerformActionState
{
    Idle,
    Press1,
    Press2,
    Press3,
    Hold1,
    Hold2,
    Hold3,
    Block,
    Execute
}

public class PlayerWeaponManager : MonoBehaviour
{
    [SerializeField] private Image image_LeftHandWeaponIcon;
    [SerializeField] private Image image_RightHandWeaponIcon;
    [SerializeField] private Image image_Spell;
    [SerializeField] private Image image_Item;

    [SerializeField] private Sprite emptyWeaponSprite;
    [SerializeField] private Sprite emptyItemSprite;
    [SerializeField] private Sprite emptySpellSprite;

    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private CameraBob cameraBob;
    public PerformActionState currentState_LeftHand;
    public PerformActionState currentState_Righthand;

    public Image image_LeftHand;
    public Image image_RightHand;

    [SerializeField] private Animator anim_LeftHand;
    [SerializeField] private Animator anim_RightHand;

    public Weapon[] rightHandWeapons = new Weapon[2];
    public Weapon[] leftHandWeapons = new Weapon[2];

    [HideInInspector] public int activeRightSlot = 0;
    [HideInInspector] public int activeLeftSlot = 0;

    public Weapon rightHandWeapon => rightHandWeapons[activeRightSlot];
    public Weapon leftHandWeapon => leftHandWeapons[activeLeftSlot];

    [HideInInspector] public bool isTwoHanding = false;

    [Header("Melee Settings")]
    [SerializeField] private float executeStaminaCost = 20f;
    [SerializeField] private float meleeStaminaCost = 18f;
    [SerializeField] private float meleeDamage = 10f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float lungeForce = 12f;

    [Tooltip("Set this to the layer your enemies are on")]
    [SerializeField] private LayerMask attackLayerMask;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerController playerController;

    private float lastAttackTimeRight;
    private float lastAttackTimeLeft;
    private int comboStepRight = 0;
    private int comboStepLeft = 0;
    private float attackStateResetTimerRight = 0f;
    private float attackStateResetTimerLeft = 0f;

    [HideInInspector] public bool isBlockingRight = false;
    [HideInInspector] public bool isBlockingLeft = false;
    [HideInInspector] public float currentParryRight = 0f;
    [HideInInspector] public float currentParryLeft = 0f;

    private float parryDuration = 0.3f;
    private float blockCooldownDuration = 0.15f;
    private float currentBlockCooldownRight = 0f;
    private float currentBlockCooldownLeft = 0f;

    [Header("Other Equipment Loadouts")]
    public Item[] equippedItems = new Item[5];
    public Item[] equippedRings = new Item[4];
    public Item[] equippedSpells = new Item[4];
    [HideInInspector] public int activeSpellSlot = 0;
    [HideInInspector] public int activeItemSlot = 0;

    public Item currentActiveSpell => equippedSpells[activeSpellSlot];
    public Item currentActiveItem => equippedItems[activeItemSlot];

    public static PlayerWeaponManager instance;


    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        currentState_LeftHand = PerformActionState.Idle;
        currentState_Righthand = PerformActionState.Idle;
    }

    void Update()
    {
        if (PauseGame.instance.IsPaused || playerController.CharacterHealthComponent.CurrentHP <= 0) return;

        ChooseAnimations();

        if (currentBlockCooldownRight > 0f) currentBlockCooldownRight -= Time.deltaTime;
        if (currentBlockCooldownLeft > 0f) currentBlockCooldownLeft -= Time.deltaTime;
        if (currentParryRight > 0f) currentParryRight -= Time.deltaTime;
        if (currentParryLeft > 0f) currentParryLeft -= Time.deltaTime;

        HandleHandSprite();
        HandleItemAndSpellSprites();
        HandleWeaponSwitching();
        HandleItemUsage();
        HandleRightHandInput();
        HandleLeftHandInput();

        if (playerController.IsToggleTwoHandedPressed)
        {
            playerController.IsToggleTwoHandedPressed = false;
            ToggleTwoHandedStance();    
        }


        if (playerController.isKickPressed && !playerController.isKicking 
            && currentState_Righthand == PerformActionState.Idle && currentState_LeftHand == PerformActionState.Idle)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                EnemyEntity targetEnemy = GetEnemyInFront();

                if (targetEnemy != null && targetEnemy.isStunned)
                {
                    RumbleManager.instance.RumblePulse(1f, 2.5f, 0.3f);
                    playerController.StaminaDepleted(executeStaminaCost);
                    StartCoroutine(ExecutionSequence(targetEnemy));
                }
            }
        }

        // State Reset Timers
        if (currentState_Righthand != PerformActionState.Idle && currentState_Righthand != PerformActionState.Hold1 && currentState_Righthand != PerformActionState.Block && !playerController.isKicking && !playerController.isExecuting)
        {
            attackStateResetTimerRight -= Time.deltaTime;
            if (attackStateResetTimerRight <= 0f && currentBlockCooldownRight <= 0f)
            {
                currentState_Righthand = PerformActionState.Idle;
            }
        }

        if (currentState_LeftHand != PerformActionState.Idle && currentState_LeftHand != PerformActionState.Hold1 && currentState_Righthand != PerformActionState.Block && !playerController.isKicking && !playerController.isExecuting)
        {
            attackStateResetTimerLeft -= Time.deltaTime;
            if (attackStateResetTimerLeft <= 0f && currentBlockCooldownLeft <= 0f)
            {
                currentState_LeftHand = PerformActionState.Idle;
            }
        }
    }

    private void HandleWeaponSwitching()
    {
        if(EquipmentLoadOut.instance.isPanelActive) { return; }

        // Right D-Pad: Cycle Right Hand
        if (playerController.IsSwitchWeaponPressed_Right)
        {
            playerController.IsSwitchWeaponPressed_Right = false;
            CycleWeapon(ref activeRightSlot, rightHandWeapons, false);
        }

        // Left D-Pad: Cycle Left Hand
        if (playerController.IsSwitchWeaponPressed_Left)
        {
            playerController.IsSwitchWeaponPressed_Left = false;
            CycleWeapon(ref activeLeftSlot, leftHandWeapons, true);
        }

        // Up D-Pad: Cycle Spells
        if (playerController.IsSwitchWeaponPressed_Up)
        {
            playerController.IsSwitchWeaponPressed_Up = false;
            CycleEquipment(ref activeSpellSlot, equippedSpells);
        }

        // Down D-Pad: Cycle Items 
        if (playerController.IsSwitchWeaponPressed_Down)
        {
            playerController.IsSwitchWeaponPressed_Down = false;
            CycleEquipment(ref activeItemSlot, equippedItems);
        }
    }

    private void HandleItemUsage()
    {
        // Check for player input and ensure they aren't stuck in another uninterruptible animation
        if (playerController.IsReloadPressed && !playerController.isKicking && !playerController.isExecuting)
        {
            playerController.IsReloadPressed = false;

            if (currentActiveItem != null && currentActiveItem.itemCategory == ItemCategory.Consumable)
            {
                // Check if the item exists in the inventory and has at least 1 charge
                if (InventoryManager.instance.consumables.ContainsKey(currentActiveItem.itemType) &&
                    InventoryManager.instance.consumables[currentActiveItem.itemType] > 0)
                {
                    // 1. Decrease the quantity by 1
                    InventoryManager.instance.consumables[currentActiveItem.itemType]--;

                    // 2. Apply the effect
                    ApplyConsumableEffect(currentActiveItem.itemType);
                }
                else
                {
                    // Optional: Play an "empty inventory" click sound
                }
            }
        }
    }

    private void ApplyConsumableEffect(ItemType type)
    {
        switch (type)
        {
            case ItemType.HealthPotion_Small:
                playerController.CharacterHealthComponent.Heal(40f);
                break;
            case ItemType.HealthPotion_Medium:
                playerController.CharacterHealthComponent.Heal(80f);
                break;
            case ItemType.HealthPotion_Big:
                playerController.CharacterHealthComponent.Heal(135f);
                break;
            case ItemType.ManaPotion_Small:
                playerController.CharacterUltimateComponent.GainUltimate(40f);
                break;
            case ItemType.ManaPotion_Medium:
                playerController.CharacterUltimateComponent.GainUltimate(80f);
                break;
            case ItemType.ManaPotion_Big:
                playerController.CharacterUltimateComponent.GainUltimate(135f);
                break;
        }
    }

    private void CycleWeapon(ref int currentSlot, Weapon[] weaponArray, bool isLeftHand)
    {
        if (weaponArray == null || weaponArray.Length == 0) return;

        // Search for the next available non-null weapon slot
        int originalSlot = currentSlot;
        int nextSlot = currentSlot;
        bool found = false;

        for (int i = 1; i <= weaponArray.Length; i++)
        {
            nextSlot = (originalSlot + i) % weaponArray.Length;
            if (weaponArray[nextSlot] != null)
            {
                found = true;
                break;
            }
        }

        // If no weapons are equipped in any slot, don't cycle
        if (!found) return;

        // Cancel two-handed stance if we switch weapons
        if (isTwoHanding) ToggleTwoHandedStance();

        // Cancel any active blocks
        if (isLeftHand && isBlockingLeft) { isBlockingLeft = false; currentState_LeftHand = PerformActionState.Idle; }
        if (!isLeftHand && isBlockingRight) { isBlockingRight = false; currentState_Righthand = PerformActionState.Idle; }

        currentSlot = nextSlot;

        // Force the animator to update to the newly selected weapon immediately
        UpdateAnimatorControllers(isLeftHand);
    }

    private void CycleEquipment(ref int currentSlot, Item[] equipmentArray)
    {
        if (equipmentArray == null || equipmentArray.Length == 0) return;

        // Search for the next available non-null equipment slot
        int originalSlot = currentSlot;
        int nextSlot = currentSlot;
        bool found = false;

        for (int i = 1; i <= equipmentArray.Length; i++)
        {
            nextSlot = (originalSlot + i) % equipmentArray.Length;
            if (equipmentArray[nextSlot] != null)
            {
                found = true;
                break;
            }
        }

        // If no items are equipped in any slot, don't cycle
        if (!found) return;

        currentSlot = nextSlot;
    }

    public void EquipItem(Item item, EquipmentSlotType slotType, int slotIndex)
    {
        if (item == null) return;

        // 1. Check if the item is already equipped anywhere else and clear that old slot
        ClearExistingSlot(item);

        // 2. Assign to the new target slot
        switch (slotType)
        {
            case EquipmentSlotType.RightHand:
                if (item is Weapon wRight)
                {
                    rightHandWeapons[slotIndex] = wRight;
                    if (slotIndex == activeRightSlot) UpdateAnimatorControllers(false);
                }
                break;
            case EquipmentSlotType.LeftHand:
                if (item is Weapon wLeft)
                {
                    leftHandWeapons[slotIndex] = wLeft;
                    if (slotIndex == activeLeftSlot) UpdateAnimatorControllers(true);
                }
                break;
            case EquipmentSlotType.Item:
                equippedItems[slotIndex] = item;
                break;
            case EquipmentSlotType.Ring:
                equippedRings[slotIndex] = item;
                break;
            case EquipmentSlotType.Spell:
                equippedSpells[slotIndex] = item;
                break;
        }
    }

    private void ClearExistingSlot(Item item)
    {
        // Check Right Hand Weapons
        for (int i = 0; i < rightHandWeapons.Length; i++)
        {
            if (rightHandWeapons[i] == item)
            {
                rightHandWeapons[i] = null;
                if (i == activeRightSlot) UpdateAnimatorControllers(false);
            }
        }

        // Check Left Hand Weapons
        for (int i = 0; i < leftHandWeapons.Length; i++)
        {
            if (leftHandWeapons[i] == item)
            {
                leftHandWeapons[i] = null;
                if (i == activeLeftSlot) UpdateAnimatorControllers(true);
            }
        }

        // Check Items
        for (int i = 0; i < equippedItems.Length; i++)
        {
            if (equippedItems[i] == item) equippedItems[i] = null;
        }

        // Check Rings
        for (int i = 0; i < equippedRings.Length; i++)
        {
            if (equippedRings[i] == item) equippedRings[i] = null;
        }

        // Check Spells
        for (int i = 0; i < equippedSpells.Length; i++)
        {
            if (equippedSpells[i] == item) equippedSpells[i] = null;
        }
    }

    public void ToggleTwoHandedStance()
    {
        if (rightHandWeapon == null) return;

        if (isBlockingRight || isBlockingLeft)
        {
            isBlockingRight = false;
            isBlockingLeft = false;
            currentBlockCooldownRight = blockCooldownDuration;
            currentBlockCooldownLeft = blockCooldownDuration;
            currentState_Righthand = PerformActionState.Idle;
            currentState_LeftHand = PerformActionState.Idle;
        }

        isTwoHanding = !isTwoHanding;

        if (isTwoHanding)
        {
            if (anim_RightHand != null && rightHandWeapon.animController_TwoHanded != null)
            {
                anim_RightHand.runtimeAnimatorController = rightHandWeapon.animController_TwoHanded;
                anim_RightHand.Play("IsIdle", -1, 0f);
            }

            currentState_LeftHand = PerformActionState.Idle;
            image_LeftHand.enabled = false;
            image_LeftHandWeaponIcon.color = new Color(255,255,255,60);
        }
        else
        {
            if (anim_RightHand != null && rightHandWeapon.animController_OneHanded != null)
            {
                anim_RightHand.runtimeAnimatorController = rightHandWeapon.animController_OneHanded;
                anim_RightHand.Play("IsIdle", -1, 0f);
            }

            if (leftHandWeapon != null)
            {
                image_LeftHand.enabled = true;
                image_LeftHandWeaponIcon.color = new Color(255,255,255,60);
                if (anim_LeftHand != null && leftHandWeapon.animController_OneHanded != null)
                {
                    anim_LeftHand.runtimeAnimatorController = leftHandWeapon.animController_OneHanded;
                    anim_LeftHand.Play("IsIdle", -1, 0f);
                }
            }
        }
    }

    private void HandleHandSprite()
    {
        // Left hand sprite respects the isTwoHanding stance
        if (leftHandWeapon != null && !isTwoHanding)
        {
            if (image_LeftHandWeaponIcon.sprite != leftHandWeapon.spr_Weapon)
            {
                image_LeftHandWeaponIcon.sprite = leftHandWeapon.spr_Weapon;
                image_LeftHandWeaponIcon.SetNativeSize();
            }

            if (image_LeftHand.enabled == false)
            {
                image_LeftHandWeaponIcon.enabled = true;
                image_LeftHand.enabled = true;

                if (anim_LeftHand != null && leftHandWeapon.animController_OneHanded != null)
                    anim_LeftHand.runtimeAnimatorController = leftHandWeapon.animController_OneHanded;
            }
        }
        else
        {
            if (image_LeftHand.enabled == true)
            {
                image_LeftHand.enabled = false;
            }

            if (image_LeftHandWeaponIcon.sprite != emptyWeaponSprite)
            {
                image_LeftHandWeaponIcon.sprite = emptyWeaponSprite;
                image_LeftHandWeaponIcon.SetNativeSize();
            }
        }

        // Right hand sprite logic
        if (rightHandWeapon != null)
        {
            if(image_RightHandWeaponIcon.sprite != rightHandWeapon.spr_Weapon)
            {
                image_RightHandWeaponIcon.sprite = rightHandWeapon.spr_Weapon;
                image_RightHandWeaponIcon.SetNativeSize();
            }

            if (image_RightHand.enabled == false)
            {
                image_RightHandWeaponIcon.enabled = true;
                image_RightHand.enabled = true;

                RuntimeAnimatorController targetController = (isTwoHanding && rightHandWeapon.animController_TwoHanded != null) ? rightHandWeapon.animController_TwoHanded : rightHandWeapon.animController_OneHanded;
                if (anim_RightHand != null && targetController != null)
                    anim_RightHand.runtimeAnimatorController = targetController;
            }
        }
        else
        {
            if (image_RightHand.enabled == true)
            {
                image_RightHand.enabled = false;
            }

            if (image_RightHandWeaponIcon.sprite != emptyWeaponSprite)
            {
                image_RightHandWeaponIcon.sprite = emptyWeaponSprite;
                image_RightHandWeaponIcon.SetNativeSize();
            }
        }
    }

    private void HandleItemAndSpellSprites()
    {
        // -----------------------
        // SPELL UI UPDATE
        // -----------------------
        if (currentActiveSpell != null && currentActiveSpell.spr_Icon != null)
        {
            if (image_Spell.sprite != currentActiveSpell.spr_Icon)
            {
                image_Spell.sprite = currentActiveSpell.spr_Icon;
                image_Spell.SetNativeSize();
                image_Spell.color = Color.white; // Full opacity
            }
        }
        else
        {
            if (image_Spell.sprite != emptySpellSprite)
            {
                image_Spell.sprite = emptySpellSprite;
                image_Spell.SetNativeSize();
                image_Spell.color = new Color32(255, 255, 255, 60); // Dimmed when empty
            }
        }

        // -----------------------
        // ITEM UI UPDATE
        // -----------------------
        if (currentActiveItem != null && currentActiveItem.spr_Icon != null)
        {
            if (image_Item.sprite != currentActiveItem.spr_Icon)
            {
                image_Item.sprite = currentActiveItem.spr_Icon;
                image_Item.SetNativeSize();
                image_Item.color = Color.white; // Full opacity
            }
        }
        else
        {
            if (image_Item.sprite != emptyItemSprite)
            {
                image_Item.sprite = emptyItemSprite;
                image_Item.SetNativeSize();
                image_Item.color = new Color32(255, 255, 255, 60); // Dimmed when empty
            }
        }
    }

    private void HandleRightHandInput()
    {
        if (rightHandWeapon == null) return;
        if (EquipmentLoadOut.instance.isPanelActive) { return; }

        switch (rightHandWeapon.weaponCategory)
        {
            case WeaponCategory.Shield:
                HandleShieldInput(rightHandWeapon, isLeftHand: false);
                break;
            case WeaponCategory.Melee:
                HandleMeleeInput(rightHandWeapon, isLeftHand: false);
                break;
            case WeaponCategory.Bow:
                HandleBowInput(rightHandWeapon, isLeftHand: false);
                break;
            case WeaponCategory.PyromancyFlame:
                HandlePyromancyInput(rightHandWeapon, isLeftHand: false);
                break;
            case WeaponCategory.SorceryCatalyst:
                HandleMagicInput(rightHandWeapon, isLeftHand: false);
                break;
        }
    }

    private void HandleLeftHandInput()
    {
        if (isTwoHanding || leftHandWeapon == null) return;
        if (EquipmentLoadOut.instance.isPanelActive) { return; }

        switch (leftHandWeapon.weaponCategory)
        {
            case WeaponCategory.Shield:
                HandleShieldInput(leftHandWeapon, isLeftHand: true);
                break;
            case WeaponCategory.Melee:
                HandleMeleeInput(leftHandWeapon, isLeftHand: true);
                break;
            case WeaponCategory.Bow:
                HandleBowInput(leftHandWeapon, isLeftHand: true);
                break;
            case WeaponCategory.PyromancyFlame:
                HandlePyromancyInput(leftHandWeapon, isLeftHand: true);
                break;
            case WeaponCategory.SorceryCatalyst:
                HandleMagicInput(leftHandWeapon, isLeftHand: true);
                break;
        }
    }

    private void HandleShieldInput(Weapon weapon, bool isLeftHand)
    {
        bool isHeld = isLeftHand ? playerController.isLeftHandHeld : playerController.isRightHandHeld;
        ref float blockCooldown = ref (isLeftHand ? ref currentBlockCooldownLeft : ref currentBlockCooldownRight);
        ref bool isBlocking = ref (isLeftHand ? ref isBlockingLeft : ref isBlockingRight);
        ref float currentParry = ref (isLeftHand ? ref currentParryLeft : ref currentParryRight);

        if (isHeld && blockCooldown <= 0f && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f && !isBlocking)
            {
                RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                isBlocking = true;
                if (isLeftHand) currentState_LeftHand = PerformActionState.Hold1;
                else currentState_Righthand = PerformActionState.Hold1;

                currentParry = parryDuration;
            }
        }
        else if (!isHeld && isBlocking)
        {
            isBlocking = false;
            blockCooldown = blockCooldownDuration;
            if (isLeftHand) currentState_LeftHand = PerformActionState.Idle;
            else currentState_Righthand = PerformActionState.Idle;
        }
    }

    private void HandleMeleeInput(Weapon weapon, bool isLeftHand)
    {
        bool isPressed = isLeftHand ? playerController.isLeftHandPressed : playerController.isRightHandPressed;
        ref float lastAttackTime = ref (isLeftHand ? ref lastAttackTimeLeft : ref lastAttackTimeRight);

        if (isTwoHanding)
        {
            bool isBlockHeld = playerController.isLeftHandHeld;
            ref float blockCooldown = ref currentBlockCooldownRight;

            if (isBlockHeld && blockCooldown <= 0f && !playerController.isKicking)
            {
                if (playerController.CharacterStaminaComponent.CurrentStamina > 0f && !isBlockingRight)
                {
                    RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                    isBlockingRight = true;
                    currentState_Righthand = PerformActionState.Block;
                    currentParryRight = parryDuration;
                }
            }
            else if (!isBlockHeld && isBlockingRight)
            {
                isBlockingRight = false;
                blockCooldown = blockCooldownDuration;
                currentState_Righthand = PerformActionState.Idle;
            }
        }

        bool isThisHandBlocking = isLeftHand ? isBlockingLeft : isBlockingRight;
        if (isThisHandBlocking) return;

        if (isPressed && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                if (Time.time >= lastAttackTime + weapon.attackCooldown)
                {
                    PerformAttack(weapon, isLeftHand);
                }
            }
        }
    }

    private void HandleBowInput(Weapon weapon, bool isLeftHand)
    {
        bool isHeld = isLeftHand ? playerController.isLeftHandHeld : playerController.isRightHandHeld;
        bool isPressed = isLeftHand ? playerController.isLeftHandPressed : playerController.isRightHandPressed;

        // Press to draw string (Hold1), release to shoot (Press1)
        if (isHeld)
        {
            if (isLeftHand) currentState_LeftHand = PerformActionState.Hold1;
            else currentState_Righthand = PerformActionState.Hold1;
        }
        else if (!isHeld && (isLeftHand ? currentState_LeftHand == PerformActionState.Hold1 : currentState_Righthand == PerformActionState.Hold1))
        {
            // Release arrow logic here
            playerController.StaminaDepleted(weapon.staminaCost);
            if (isLeftHand) currentState_LeftHand = PerformActionState.Press1;
            else currentState_Righthand = PerformActionState.Press1;
        }
    }

    private void HandlePyromancyInput(Weapon weapon, bool isLeftHand)
    {
        if (currentActiveSpell == null || currentActiveSpell.itemCategory != ItemCategory.Pyromancy) return;
        ProcessSpellInput(weapon, currentActiveSpell, isLeftHand);
    }

    private void HandleMagicInput(Weapon weapon, bool isLeftHand)
    {
        if (currentActiveSpell == null || currentActiveSpell.itemCategory != ItemCategory.Magic) return;
        ProcessSpellInput(weapon, currentActiveSpell, isLeftHand);
    }

    private void ProcessSpellInput(Weapon weapon, Item spell, bool isLeftHand)
    {
        PerformActionState currentState = isLeftHand ? currentState_LeftHand : currentState_Righthand;
        bool isPressed = isLeftHand ? playerController.isLeftHandPressed : playerController.isRightHandPressed;
        ref float lastAttackTime = ref (isLeftHand ? ref lastAttackTimeLeft : ref lastAttackTimeRight);

        // Only begin a cast if the hand is Idle, the attack cooldown has passed, and we aren't kicking
        if (currentState == PerformActionState.Idle && isPressed && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f &&
                playerController.CharacterUltimateComponent.CurrentUltimate > 0f &&
                Time.time >= lastAttackTime + weapon.attackCooldown)
            {
                StartCoroutine(SpellCastSequence(weapon, spell, isLeftHand));
            }
        }
    }

    private IEnumerator SpellCastSequence(Weapon weapon, Item spell, bool isLeftHand)
    {
        // INITIAL SETUP: Deplete stamina and set the animation to Hold1 (Wind-up)
        playerController.StaminaDepleted(weapon.staminaCost);

        if (isLeftHand) currentState_LeftHand = PerformActionState.Hold1;
        else currentState_Righthand = PerformActionState.Hold1;

        Transform castTransform = cameraTransform;

        // -----------------------------------------------------
        // PHASE 1: DELAY
        // -----------------------------------------------------
        if (spell.useDelay)
        {
            playerController.DepleteUltimate(spell.flatManaCost);
            yield return new WaitForSeconds(spell.castDelay);
        }
        else if (!spell.useCharge && !spell.useContinuous)
        {
            // Standard instant flat cost for simple, non-modified casts
            playerController.DepleteUltimate(spell.flatManaCost);
        }

        // If the player let go during the delay and it's a continuous spell, cancel.
        if (spell.useContinuous && !IsHandHeld(isLeftHand))
        {
            EndSpellCast(weapon, isLeftHand);
            yield break;
        }

        // -----------------------------------------------------
        // PHASE 2: CHARGE
        // -----------------------------------------------------
        float finalCharge = 0f;
        if (spell.useCharge)
        {
            // If it's a continuous spell, charge acts as a 'spin-up' (must hold until max before firing).
            // If it's a single shot, you hold to build power, and release to fire early or at max.
            bool requireMaxChargeForContinuous = spell.useContinuous;

            while (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate > 0f)
            {
                if (finalCharge < spell.maxChargeTime)
                {
                    finalCharge += Time.deltaTime;
                    playerController.DepleteUltimate(spell.chargeManaDrainRate * Time.deltaTime);
                }

                // Break charge phase to start firing if spinning up a continuous weapon
                if (requireMaxChargeForContinuous && finalCharge >= spell.maxChargeTime)
                {
                    break;
                }

                yield return null; // Wait until next frame
            }
        }

        // -----------------------------------------------------
        // PHASE 3: EXECUTION
        // -----------------------------------------------------
        if (spell.useContinuous)
        {
            // Must still be holding the button to spray
            if (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate > 0f)
            {
                GameObject activeSpell = null;
                if (spell.spellPrefab != null)
                {
                    // Parented to transform so the flamethrower follows the player's camera turning
                    activeSpell = Instantiate(spell.spellPrefab, castTransform.position, castTransform.rotation, castTransform);
                }

                // Keep spraying until button released or mana empty
                while (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate > 0f)
                {
                    playerController.DepleteUltimate(spell.continuousManaDrainRate * Time.deltaTime);
                    yield return null;
                }

                if (activeSpell != null) Destroy(activeSpell);
            }
        }
        else
        {
            // Single cast execution (Fires on button release if charged, or instantly if standard)
            if (spell.spellPrefab != null)
            {
                GameObject proj = Instantiate(spell.spellPrefab, castTransform.position, castTransform.rotation);

                // TODO: Pass 'finalCharge' to the spawned projectile script here so it knows its multiplier
            }
        }

        // -----------------------------------------------------
        // PHASE 4: CLEANUP
        // -----------------------------------------------------
        EndSpellCast(weapon, isLeftHand);
    }

    private bool IsHandHeld(bool isLeftHand)
    {
        return isLeftHand ? playerController.isLeftHandHeld : playerController.isRightHandHeld;
    }

    private void EndSpellCast(Weapon weapon, bool isLeftHand)
    {
        // Reverts state back to the Update loop reset timers
        if (isLeftHand)
        {
            lastAttackTimeLeft = Time.time;
            currentState_LeftHand = PerformActionState.Press1; // Release animation
            attackStateResetTimerLeft = weapon.attackCooldown;
        }
        else
        {
            lastAttackTimeRight = Time.time;
            currentState_Righthand = PerformActionState.Press1; // Release animation
            attackStateResetTimerRight = weapon.attackCooldown;
        }
    }

    private IEnumerator ExecutionSequence(EnemyEntity targetEnemy)
    {
        playerController.isAttacking = true;
        playerController.isExecuting = true;

        Vector3 directionToPlayer = (transform.position - targetEnemy.transform.position).normalized;
        directionToPlayer.y = 0;

        Vector3 executeCenter = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position;
        Vector3 targetPos = executeCenter + (directionToPlayer * 1.2f);

        float startYaw = transform.eulerAngles.y;
        Vector3 dirToEnemyBody = executeCenter - targetPos;
        dirToEnemyBody.y = 0;
        float targetYaw = Quaternion.LookRotation(dirToEnemyBody).eulerAngles.y;

        float startPitch = playerController.verticalRotation;
        Vector3 lookTargetPos = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position + (Vector3.up * 1.5f);

        float cameraHeight = cameraTransform.position.y - transform.position.y;
        Vector3 finalCameraPos = targetPos + (Vector3.up * cameraHeight);

        Vector3 dirToLookTarget = (lookTargetPos - finalCameraPos).normalized;
        float targetPitch = Quaternion.LookRotation(dirToLookTarget).eulerAngles.x;
        if (targetPitch > 180f) targetPitch -= 360f;

        float dashDuration = 0.2f;
        float elapsed = 0f;
        CharacterController charController = playerController.GetComponent<CharacterController>();

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDuration;

            Vector3 currentLerpPos = Vector3.Lerp(transform.position, targetPos, t);
            Vector3 moveDelta = currentLerpPos - transform.position;
            charController.Move(moveDelta);

            float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            transform.eulerAngles = new Vector3(0f, currentYaw, 0f);

            playerController.verticalRotation = Mathf.LerpAngle(startPitch, targetPitch, t);

            yield return null;
        }

        transform.eulerAngles = new Vector3(0f, targetYaw, 0f);
        playerController.verticalRotation = targetPitch;

        currentState_Righthand = PerformActionState.Execute;

        targetEnemy.GotExecuted();
        cameraFollow.isSmoothing = false;
        cameraBob.TriggerShake(0.293f, 0.05f);
        cameraBob.TriggerZoomEffect(40f, 0.293f, 0.25f);

        yield return new WaitForSeconds(0.293f);

        cameraFollow.isSmoothing = false;
        playerController.isExecuting = false;
        playerController.isAttacking = false;
        currentState_Righthand = PerformActionState.Idle;
    }

    public bool HasPyromancyEquipped()
    {
        bool rightIsPyro = rightHandWeapon != null && rightHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        bool leftIsPyro = leftHandWeapon != null && leftHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        return rightIsPyro || leftIsPyro;
    }

    private EnemyEntity GetEnemyInFront()
    {
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = attackRange + attackRadius;

        if (Physics.SphereCast(rayAttack, attackRadius, out RaycastHit hit, totalRange, attackLayerMask))
        {
            return hit.collider.GetComponent<EnemyEntity>();
        }

        return null;
    }

    private void PerformAttack(Weapon weapon, bool isLeftHand)
    {
        if (!playerController.isGrounded || playerController.isSliding) { return; }

        ApplyAttackJolt(weapon, isLeftHand);

        playerController.TriggerAttackMovement(weapon.attackCooldown, lungeForce);

        if (isLeftHand)
        {
            lastAttackTimeLeft = Time.time;
            attackStateResetTimerLeft = weapon.attackCooldown;
        }
        else
        {
            lastAttackTimeRight = Time.time;
            attackStateResetTimerRight = weapon.attackCooldown;
        }
    }

    private void ApplyAttackJolt(Weapon weapon, bool isLeftHand)
    {
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
        playerController.StaminaDepleted(weapon.staminaCost);

        if (isLeftHand)
        {
            if (Time.time > lastAttackTimeLeft + weapon.attackCooldown + 0.5f) comboStepLeft = 0;
        }
        else
        {
            if (Time.time > lastAttackTimeRight + weapon.attackCooldown + 0.5f) comboStepRight = 0;
        }

        Vector3 joltDirection = Vector3.zero;
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = attackRange + attackRadius;
        bool isLeftAttack = isLeftHand;
        int currentCombo = isLeftHand ? comboStepLeft : comboStepRight;

        switch (currentCombo)
        {
            case 0:
                {
                    joltDirection = new Vector3(2f, -6f, 3f);
                    if (isLeftHand) currentState_LeftHand = PerformActionState.Press1;
                    else currentState_Righthand = PerformActionState.Press1;
                }
                break;
            case 1:
                {
                    isLeftAttack = !isLeftAttack;
                    joltDirection = new Vector3(2f, 6f, -3f);
                    if (isLeftHand) currentState_LeftHand = PerformActionState.Press2;
                    else currentState_Righthand = PerformActionState.Press2;
                }
                break;
            case 2:
                {
                    joltDirection = new Vector3(5f, 0f, 0f);
                    if (isLeftHand) currentState_LeftHand = PerformActionState.Press3;
                    else currentState_Righthand = PerformActionState.Press3;
                }
                break;
        }

        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, attackRadius, totalRange, attackLayerMask);
        bool hitSomething = false;

        if (hits.Length > 0)
        {
            HashSet<Component> processedTargets = new HashSet<Component>();

            foreach (RaycastHit hit in hits)
            {
                EnemyEntity targetEnemy = hit.collider.GetComponent<EnemyEntity>();
                Fracture destructible = hit.collider.GetComponent<Fracture>();
                FractureTrigger fractureTrigger = hit.collider.GetComponent<FractureTrigger>();

                if (targetEnemy != null && processedTargets.Add(targetEnemy))
                {
                    targetEnemy.TakeSwordHit(isLeftAttack, hit.point, weapon.damage);
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    destructible.TakeDamage(weapon.damage, hit.collider, hit.point);
                    hitSomething = true;

                    if (fractureTrigger != null)
                    {
                        fractureTrigger.TriggerMaterialSound_Hit();
                    }
                }
            }
        }

        if (!hitSomething)
        {
            SoundManager.instance.SwordSound_Air(transform.position);
        }

        playerController.TriggerMeleeJolt(joltDirection);

        if (isLeftHand)
        {
            comboStepLeft++;
            if (comboStepLeft > 1) comboStepLeft = 0;
        }
        else
        {
            comboStepRight++;
            if (comboStepRight > 1) comboStepRight = 0;
        }
    }

    private static readonly Dictionary<PerformActionState, int> StateToHash = new Dictionary<PerformActionState, int>
    {
        { PerformActionState.Idle, Animator.StringToHash("IsIdle") },
        { PerformActionState.Press1, Animator.StringToHash("IsPress1") },
        { PerformActionState.Press2, Animator.StringToHash("IsPress2") },
        { PerformActionState.Press3, Animator.StringToHash("IsPress3") },
        { PerformActionState.Hold1, Animator.StringToHash("IsHold1") },
        { PerformActionState.Hold2, Animator.StringToHash("IsHold2") },
        { PerformActionState.Hold3, Animator.StringToHash("IsHold3") },
        { PerformActionState.Block, Animator.StringToHash("IsBlock") },
        { PerformActionState.Execute, Animator.StringToHash("IsExecute") }
    };

    private int lastLeftStateHash;
    private int lastRightStateHash;

    private void ChooseAnimations()
    {
        // Handle Left Hand Animator
        if (StateToHash.TryGetValue(currentState_LeftHand, out int leftHash))
        {
            if (leftHash != lastLeftStateHash)
            {
                if (lastLeftStateHash != 0 && anim_LeftHand != null) anim_LeftHand.SetBool(lastLeftStateHash, false);
                if (anim_LeftHand != null) anim_LeftHand.SetBool(leftHash, true);
                lastLeftStateHash = leftHash;
            }
        }

        // Handle Right Hand Animator
        if (StateToHash.TryGetValue(currentState_Righthand, out int rightHash))
        {
            if (rightHash != lastRightStateHash)
            {
                if (lastRightStateHash != 0 && anim_RightHand != null) anim_RightHand.SetBool(lastRightStateHash, false);
                if (anim_RightHand != null) anim_RightHand.SetBool(rightHash, true);
                lastRightStateHash = rightHash;
            }
        }
    }

    private void UpdateAnimatorControllers(bool isLeftHand)
    {
        if (isLeftHand)
        {
            if (anim_LeftHand != null)
            {
                if (leftHandWeapon != null && leftHandWeapon.animController_OneHanded != null)
                {
                    anim_LeftHand.runtimeAnimatorController = leftHandWeapon.animController_OneHanded;
                    anim_LeftHand.Play("IsIdle", -1, 0f);
                }
                else
                {
                    anim_LeftHand.runtimeAnimatorController = null;
                }
            }
        }
        else
        {
            if (anim_RightHand != null)
            {
                if (rightHandWeapon != null && rightHandWeapon.animController_OneHanded != null)
                {
                    anim_RightHand.runtimeAnimatorController = rightHandWeapon.animController_OneHanded;
                    anim_RightHand.Play("IsIdle", -1, 0f);
                }
                else
                {
                    anim_RightHand.runtimeAnimatorController = null;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;

        Gizmos.color = Color.red;

        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        float totalRange = attackRange + attackRadius;

        Gizmos.DrawRay(castStart, cameraTransform.forward * totalRange);
        Gizmos.DrawWireSphere(castStart, attackRadius);

        Vector3 endPosition = castStart + (cameraTransform.forward * totalRange);
        Gizmos.DrawWireSphere(endPosition, attackRadius);
    }
}
