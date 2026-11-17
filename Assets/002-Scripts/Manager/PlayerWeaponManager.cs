using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private CanvasGroup canvasGroup_LeftHandWeaponIcon;
    [SerializeField] private GameObject obj_LeftHandActionIcon_OneHanded;
    [SerializeField] private GameObject obj_LeftHandActionIcon_TwoHanded;
    [SerializeField] private Image image_RightHandWeaponIcon;
    [SerializeField] private CanvasGroup canvasGroup_RightHandWeaponIcon;
    [SerializeField] private GameObject obj_RightHandActionIcon_OneHanded;
    [SerializeField] private GameObject obj_RightHandActionIcon_TwoHanded;
    [SerializeField] private Image image_Spell;
    [SerializeField] private Image image_Item;
    [SerializeField] private TextMeshProUGUI text_ItemCount;

    [SerializeField] private Sprite emptyWeaponSprite;
    [SerializeField] private Sprite emptyItemSprite;
    [SerializeField] private Sprite emptySpellSprite;

    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private CameraBob cameraBob;
    [SerializeField] private CameraPostProcessEffect postProcressEffect;
    public PerformActionState currentState_LeftHand;
    public PerformActionState currentState_Righthand;

    public Image image_LeftHand;
    public Image image_RightHand;

    [SerializeField] private Animator anim_LeftHand;
    [SerializeField] private Animator anim_RightHand;

    public Weapon[] rightHandWeapons = new Weapon[3];
    public Weapon[] leftHandWeapons = new Weapon[3];

    [HideInInspector] public int activeRightSlot = 0;
    [HideInInspector] public int activeLeftSlot = 0;

    [SerializeField] private GameObject switchWeaponUI_Gamepad;
    [SerializeField] private GameObject switchWeaponUI_PC;

    public Weapon rightHandWeapon => rightHandWeapons[activeRightSlot];
    public Weapon leftHandWeapon => leftHandWeapons[activeLeftSlot];

    [HideInInspector] public bool isTwoHanding_Left = false;
    [HideInInspector] public bool isTwoHanding_Right = false;

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

    [Header("Guard (Block button)")]
    [Tooltip("Block stamina cost multiplier when guarding with a single one-handed sword.")]
    [SerializeField] private float oneHandSwordBlockMultiplier = 1.5f;
    [Tooltip("Block stamina cost multiplier when guarding with a sword in each hand.")]
    [SerializeField] private float dualSwordBlockMultiplier = 1.25f;
    [Tooltip("Block stamina cost multiplier when guarding with a two-handed sword (the best sword guard).")]
    [SerializeField] private float twoHandedBlockMultiplier = 1f;

    // Which button drives each hand this frame (set by HandleCombatInput)
    private enum HandButton { None, Attack, Block }
    private HandButton leftHandButton = HandButton.None;
    private HandButton rightHandButton = HandButton.None;
    private bool nextDualAttackIsLeft = false; // dual swords: right swings first, then alternate
    [HideInInspector] public float currentParryRight = 0f;
    [HideInInspector] public float currentParryLeft = 0f;

    private float parryDuration = 0.3f;
    private float blockCooldownDuration = 0.15f;

    [Header("Parry-only weapons (e.g. dagger)")]
    [Tooltip("How long the parry pose (Block animation) shows after a tap.")]
    [SerializeField] private float parryPoseDuration = 0.25f;
    private float parryPoseTimerLeft;
    private float parryPoseTimerRight;
    private float currentBlockCooldownRight = 0f;
    private float currentBlockCooldownLeft = 0f;

    [Header("Other Equipment Loadouts")]
    public Item[] equippedItems = new Item[5];
    public Item[] equippedRings = new Item[4];
    public Item[] equippedSpells = new Item[4];
    [HideInInspector] public int activeSpellSlot = 0;
    [HideInInspector] public int activeItemSlot = 0;

    [SerializeField] private QuickRadialMenu quickMenu_Spell;
    [SerializeField] private QuickRadialMenu quickMenu_Item;
    [SerializeField] private QuickRadialMenu quickMenu_LeftWeapon;
    [SerializeField] private QuickRadialMenu quickMenu_RightWeapon;
    [HideInInspector] public bool isQuickMenuActive = false;

    private float itemSwitchHoldDownDuration = 0.2f;
    private float currentItemSwitchHoldDown = 0f;

    private float spellSwitchHoldDownDuration = 0.2f;
    private float currentSpellSwitchHoldDown = 0f;

    private float leftWeaponSwitchHoldDownDuration = 0.2f;
    private float currentLeftWeaponSwitchHoldDown = 0f;

    private float rightWeaponSwitchHoldDownDuration = 0.2f;
    private float currentRightWeaponSwitchHoldDown = 0f;

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

        HandleQuickRadialMenu();
        HandleHandSprite();
        HandleItemAndSpellSprites();
        HandleWeaponSwitching();
        HandleItemUsage();
        HandleCombatInput();
        HandleWeaponSwitchUI();

        if (InputSchemeManager.instance.IsToggleTwoHandedPressed_Right)
        {
            InputSchemeManager.instance.IsToggleTwoHandedPressed_Right = false;
            ToggleTwoHandedStance(false);
        }
        else if(InputSchemeManager.instance.IsToggleTwoHandedPressed_Left)
        {
            InputSchemeManager.instance.IsToggleTwoHandedPressed_Left = false;
            ToggleTwoHandedStance(true);
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

        if (currentState_LeftHand != PerformActionState.Idle && currentState_LeftHand != PerformActionState.Hold1 && currentState_LeftHand != PerformActionState.Block && !playerController.isKicking && !playerController.isExecuting)
        {
            attackStateResetTimerLeft -= Time.deltaTime;
            if (attackStateResetTimerLeft <= 0f && currentBlockCooldownLeft <= 0f)
            {
                currentState_LeftHand = PerformActionState.Idle;
            }
        }
    }

    private void HandleWeaponSwitchUI()
    {
        if(InputSchemeManager.instance.CurrentInputMode == InputMode.PC)
        {
            if(!switchWeaponUI_PC.activeInHierarchy)
            {
                switchWeaponUI_PC.SetActive(true);
                switchWeaponUI_Gamepad.SetActive(false);
            }
        }
        else if(InputSchemeManager.instance.CurrentInputMode == InputMode.PlayStation ||
            InputSchemeManager.instance.CurrentInputMode == InputMode.Xbox)
        {
            if(!switchWeaponUI_Gamepad.activeInHierarchy)
            {
                switchWeaponUI_Gamepad.SetActive(true);
                switchWeaponUI_PC.SetActive(false);
            }
        }
    }

    private void HandleQuickRadialMenu()
    {
        if (!isQuickMenuActive)
        {
            HandleQuickMenuHoldDownInput();
            return; 
        }

        Vector2 inputDir = InputSchemeManager.instance.input_Look.ReadValue<Vector2>();

        if (quickMenu_Spell.isMenuActive)
        {
            if (!InputSchemeManager.instance.IsSwitchWeaponHeld_Up)
            {
                int selectedIndex = quickMenu_Spell.CloseMenu();
                isQuickMenuActive = false;
                InputSchemeManager.instance.IsSwitchWeaponPressed_Up = false; // Prevent accidental tap cycle on release

                if (selectedIndex != -1 && selectedIndex < equippedSpells.Length && equippedSpells[selectedIndex] != null)
                {
                    activeSpellSlot = selectedIndex;
                }
            }
            else quickMenu_Spell.UpdateMenu(inputDir);
        }
        // ITEM MENU
        else if (quickMenu_Item.isMenuActive)
        {
            if (!InputSchemeManager.instance.IsSwitchWeaponHeld_Down)
            {
                int selectedIndex = quickMenu_Item.CloseMenu();
                isQuickMenuActive = false;
                InputSchemeManager.instance.IsSwitchWeaponPressed_Down = false;

                if (selectedIndex != -1 && selectedIndex < equippedItems.Length && equippedItems[selectedIndex] != null)
                {
                    activeItemSlot = selectedIndex;
                }
            }
            else quickMenu_Item.UpdateMenu(inputDir);
        }
        // RIGHT WEAPON MENU
        else if (quickMenu_RightWeapon.isMenuActive)
        {
            if (!InputSchemeManager.instance.IsSwitchWeaponHeld_Right)
            {
                int selectedIndex = quickMenu_RightWeapon.CloseMenu();
                isQuickMenuActive = false;
                InputSchemeManager.instance.IsSwitchWeaponPressed_Right = false;

                if (selectedIndex != -1 && selectedIndex < rightHandWeapons.Length && rightHandWeapons[selectedIndex] != null)
                {
                    if (isTwoHanding_Right) ToggleTwoHandedStance(false);
                    if (isBlockingRight) { isBlockingRight = false; currentState_Righthand = PerformActionState.Idle; }

                    activeRightSlot = selectedIndex;
                    UpdateAnimatorControllers(false);
                }
            }
            else quickMenu_RightWeapon.UpdateMenu(inputDir);
        }
        // LEFT WEAPON MENU
        else if (quickMenu_LeftWeapon.isMenuActive)
        {
            if (!InputSchemeManager.instance.IsSwitchWeaponHeld_Left)
            {
                int selectedIndex = quickMenu_LeftWeapon.CloseMenu();
                isQuickMenuActive = false;
                InputSchemeManager.instance.IsSwitchWeaponPressed_Left = false;

                if (selectedIndex != -1 && selectedIndex < leftHandWeapons.Length && leftHandWeapons[selectedIndex] != null)
                {
                    if (isTwoHanding_Left) ToggleTwoHandedStance(true);
                    if (isBlockingLeft) { isBlockingLeft = false; currentState_LeftHand = PerformActionState.Idle; }

                    activeLeftSlot = selectedIndex;
                    UpdateAnimatorControllers(true);
                }
            }
            else quickMenu_LeftWeapon.UpdateMenu(inputDir);
        }
    }

    private void HandleQuickMenuHoldDownInput()
    {
        if (InputSchemeManager.instance.IsSwitchWeaponHeld_Up)
        {
            currentSpellSwitchHoldDown += Time.deltaTime;
            if (currentSpellSwitchHoldDown > spellSwitchHoldDownDuration)
            {
                quickMenu_Spell.OpenMenu();
                isQuickMenuActive = true;
                currentSpellSwitchHoldDown = 0f;
            }
        }
        else if(!InputSchemeManager.instance.IsSwitchWeaponHeld_Up 
            && currentSpellSwitchHoldDown > 0f
            && currentSpellSwitchHoldDown <= spellSwitchHoldDownDuration)
        {
            currentSpellSwitchHoldDown = 0f;
            CycleEquipment(ref activeSpellSlot, equippedSpells);
        }

        // Item (Down)
        if (InputSchemeManager.instance.IsSwitchWeaponHeld_Down)
        {
            currentItemSwitchHoldDown += Time.deltaTime;
            if (currentItemSwitchHoldDown > itemSwitchHoldDownDuration)
            {
                quickMenu_Item.OpenMenu();
                isQuickMenuActive = true;
                currentItemSwitchHoldDown = 0f;
            }
        }
        else if(!InputSchemeManager.instance.IsSwitchWeaponHeld_Down
            && currentItemSwitchHoldDown > 0f
            && currentItemSwitchHoldDown <= itemSwitchHoldDownDuration)
        {
            currentItemSwitchHoldDown = 0f;
            CycleEquipment(ref activeItemSlot, equippedItems);
        }

        // Right Weapon (Right)
        if (InputSchemeManager.instance.IsSwitchWeaponHeld_Right)
        {
            currentRightWeaponSwitchHoldDown += Time.deltaTime;
            if (currentRightWeaponSwitchHoldDown > rightWeaponSwitchHoldDownDuration)
            {
                quickMenu_RightWeapon.OpenMenu();
                isQuickMenuActive = true;
                currentRightWeaponSwitchHoldDown = 0f;
            }
        }
        else if(!InputSchemeManager.instance.IsSwitchWeaponHeld_Right
            && currentRightWeaponSwitchHoldDown > 0f
            && currentRightWeaponSwitchHoldDown <= rightWeaponSwitchHoldDownDuration)
        {
            currentRightWeaponSwitchHoldDown = 0f;
            CycleWeapon(ref activeRightSlot, rightHandWeapons, false);
        }

        // Left Weapon (Left)
        if (InputSchemeManager.instance.IsSwitchWeaponHeld_Left)
        {
            currentLeftWeaponSwitchHoldDown += Time.deltaTime;
            if (currentLeftWeaponSwitchHoldDown > leftWeaponSwitchHoldDownDuration)
            {
                quickMenu_LeftWeapon.OpenMenu();
                isQuickMenuActive = true;
                currentLeftWeaponSwitchHoldDown = 0f;
            }
        }
        else if(!InputSchemeManager.instance.IsSwitchWeaponHeld_Left
            && currentLeftWeaponSwitchHoldDown > 0f
            && currentLeftWeaponSwitchHoldDown <= leftWeaponSwitchHoldDownDuration)
        {
            currentLeftWeaponSwitchHoldDown = 0f;
            CycleWeapon(ref activeLeftSlot, leftHandWeapons, true);
        }

        if(InputSchemeManager.instance.CurrentInputMode == InputMode.PC)
        {
            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;

                // Check if the scroll input exceeds the threshold
                if (Mathf.Abs(scrollY) >= 1f)
                {
                    if (scrollY < 0)
                    {
                        CycleEquipment(ref activeItemSlot, equippedItems);
                    }
                    else if (scrollY > 0)
                    {
                        CycleEquipment(ref activeSpellSlot, equippedSpells);
                    }
                }
            }
        }
    }

    private void HandleWeaponSwitching()
    {
        if(EquipmentLoadOut.instance.isPanelActive || isQuickMenuActive) { return; }

        // Right D-Pad: Cycle Right Hand
        if (InputSchemeManager.instance.IsSwitchWeaponPressed_Right)
        {
            InputSchemeManager.instance.IsSwitchWeaponPressed_Right = false;
            
        }

        // Left D-Pad: Cycle Left Hand
        if (InputSchemeManager.instance.IsSwitchWeaponPressed_Left)
        {
            InputSchemeManager.instance.IsSwitchWeaponPressed_Left = false;
        }

        // Up D-Pad: Cycle Spells
        if (InputSchemeManager.instance.IsSwitchWeaponPressed_Up)
        {
            InputSchemeManager.instance.IsSwitchWeaponPressed_Up = false;
        }

        // Down D-Pad: Cycle Items 
        if (InputSchemeManager.instance.IsSwitchWeaponPressed_Down)
        {
            InputSchemeManager.instance.IsSwitchWeaponPressed_Down = false;
        }
    }

    private void HandleItemUsage()
    {
        if (EquipmentLoadOut.instance.isPanelActive) { return; }
        if (PauseGame.instance.IsPaused) { return; }

        // Check for player input and ensure they aren't stuck in another uninterruptible animation
        if (InputSchemeManager.instance.IsReloadPressed && !playerController.isKicking && !playerController.isExecuting)
        {
            InputSchemeManager.instance.IsReloadPressed = false;
            UseCurrentItem();
        }
    }

    private void UseCurrentItem()
    {
        if (currentActiveItem != null && currentActiveItem.itemCategory == ItemCategory.Consumable)
        {
            ItemType activeType = currentActiveItem.itemType;

            // Check if the item exists in the inventory and has at least 1 charge
            if (InventoryManager.instance.consumables.ContainsKey(activeType) &&
                InventoryManager.instance.consumables[activeType] > 0)
            {
                // 1. Decrease the quantity by 1
                InventoryManager.instance.consumables[activeType]--;

                // 2. Apply the effect
                ApplyConsumableEffect(activeType);

                // 3. If depleted, clear it completely from inventory and loadouts
                if (InventoryManager.instance.consumables[activeType] <= 0)
                {
                    InventoryManager.instance.RemoveConsumableIfEmpty(activeType);

                    // Automatically cycle to the next available item so the player isn't left empty-handed
                    CycleEquipment(ref activeItemSlot, equippedItems);
                }

                // 4. Force immediate UI and loadout update
                if (EquipmentLoadOut.instance != null)
                {
                    EquipmentLoadOut.instance.RefreshUI();

                    // If the inventory selection menu is currently open, refresh that grid too
                    if (EquipmentLoadOut.instance.isPanelActive && EquipmentLoadOut.instance.isInInventoryMenu)
                    {
                        EquipmentLoadOut.instance.OpenInventoryCategory(EquipmentSlotType.Item, activeItemSlot);
                    }
                }
            }
        }
    }

    private void ApplyConsumableEffect(ItemType type)
    {
        switch (type)
        {
            case ItemType.HealthPotion_Small:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerHealEffect();
                playerController.CharacterHealthComponent.Heal(60f);
                playerController.HealthEffect(true);
                break;
            case ItemType.HealthPotion_Medium:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerHealEffect();
                playerController.CharacterHealthComponent.Heal(80f);
                playerController.HealthEffect(true);
                break;
            case ItemType.HealthPotion_Big:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerHealEffect();
                playerController.CharacterHealthComponent.Heal(135f);
                playerController.HealthEffect(true);
                break;
            case ItemType.ManaPotion_Small:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerManaEffect();
                playerController.CharacterUltimateComponent.GainUltimate(40f);
                playerController.ManaEffect(true);
                break;
            case ItemType.ManaPotion_Medium:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerManaEffect();
                playerController.CharacterUltimateComponent.GainUltimate(80f);
                playerController.ManaEffect(true);
                break;
            case ItemType.ManaPotion_Big:
                SoundManager.instance.PotionDrinkingSound(playerController.transform.position);
                postProcressEffect.TriggerManaEffect();
                playerController.CharacterUltimateComponent.GainUltimate(135f);
                playerController.ManaEffect(true);
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
        if (isLeftHand && isTwoHanding_Left) ToggleTwoHandedStance(true);
        if (!isLeftHand && isTwoHanding_Right) ToggleTwoHandedStance(false);

        // Cancel any active blocks
        if (isLeftHand && isBlockingLeft) { isBlockingLeft = false; currentState_LeftHand = PerformActionState.Idle; }
        if (!isLeftHand && isBlockingRight) { isBlockingRight = false; currentState_Righthand = PerformActionState.Idle; }

        currentSlot = nextSlot;
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

    public void ToggleTwoHandedStance(bool isLeftHand)
    {
        // Weapons that can't be two-handed (e.g. daggers) ignore the toggle. Leaving a two-handed stance
        // (pressing while the other hand is two-handing) still works.
        bool isEnteringTwoHanded = !isTwoHanding_Left && !isTwoHanding_Right;
        Weapon toggled = isLeftHand ? leftHandWeapon : rightHandWeapon;
        if (isEnteringTwoHanded && toggled != null && !toggled.canTwoHand) { return; }

        if (isBlockingRight || isBlockingLeft)
        {
            isBlockingRight = false;
            isBlockingLeft = false;
            currentBlockCooldownRight = blockCooldownDuration;
            currentBlockCooldownLeft = blockCooldownDuration;
        }

        if (isLeftHand)
        {
            if (leftHandWeapon == null) return;

            // If the Right hand is currently two-handing, just reset to dual one-handed
            if (isTwoHanding_Right)
            {
                isTwoHanding_Right = false;
            }
            else
            {
                // Otherwise, perform normal toggle for the Left hand
                isTwoHanding_Left = !isTwoHanding_Left;
            }
        }
        else
        {
            if (rightHandWeapon == null) return;

            // If the Left hand is currently two-handing, just reset to dual one-handed
            if (isTwoHanding_Left)
            {
                isTwoHanding_Left = false;
            }
            else
            {
                // Otherwise, perform normal toggle for the Right hand
                isTwoHanding_Right = !isTwoHanding_Right;
            }
        }

        currentState_Righthand = PerformActionState.Idle;
        currentState_LeftHand = PerformActionState.Idle;

        UpdateAnimatorControllers(true);
        UpdateAnimatorControllers(false);
        HandleHandSprite();
    }

    private void UpdateAnimatorControllers(bool isLeftHand)
    {
        if (isLeftHand)
        {
            if (anim_LeftHand != null)
            {
                if (leftHandWeapon != null)
                {
                    RuntimeAnimatorController targetController = (isTwoHanding_Left && leftHandWeapon.animController_TwoHanded != null)
                        ? leftHandWeapon.animController_TwoHanded
                        : leftHandWeapon.animController_OneHanded;

                    anim_LeftHand.runtimeAnimatorController = targetController;
                    if (targetController != null) anim_LeftHand.Play("IsIdle", -1, 0f);
                }
                else anim_LeftHand.runtimeAnimatorController = null;
            }
        }
        else
        {
            if (anim_RightHand != null)
            {
                if (rightHandWeapon != null)
                {
                    RuntimeAnimatorController targetController = (isTwoHanding_Right && rightHandWeapon.animController_TwoHanded != null)
                        ? rightHandWeapon.animController_TwoHanded
                        : rightHandWeapon.animController_OneHanded;

                    anim_RightHand.runtimeAnimatorController = targetController;
                    if (targetController != null) anim_RightHand.Play("IsIdle", -1, 0f);
                }
                else anim_RightHand.runtimeAnimatorController = null;
            }
        }
    }

    private void HandleHandSprite()
    {
        if (leftHandWeapon != null && !isTwoHanding_Right)
        {
            if (image_LeftHandWeaponIcon.sprite != leftHandWeapon.spr_Weapon)
            {
                image_LeftHandWeaponIcon.sprite = leftHandWeapon.spr_Weapon;
                image_LeftHandWeaponIcon.SetNativeSize();
            }

            if(isTwoHanding_Left)
            {
                obj_LeftHandActionIcon_OneHanded.SetActive(false);
                obj_LeftHandActionIcon_TwoHanded.SetActive(true);
            }
            else
            {
                obj_LeftHandActionIcon_OneHanded.SetActive(true);
                obj_LeftHandActionIcon_TwoHanded.SetActive(false);
            }

            canvasGroup_LeftHandWeaponIcon.alpha = 1f;
            image_LeftHand.enabled = true;
            image_LeftHandWeaponIcon.color = Color.white;
        }
        else
        {
            image_LeftHand.enabled = false;

            if (isTwoHanding_Right)
            {
                canvasGroup_LeftHandWeaponIcon.alpha = 0.4f; // Hide completely if right hand is two-handing
                obj_LeftHandActionIcon_OneHanded.SetActive(false);
                obj_LeftHandActionIcon_TwoHanded.SetActive(false);
            }
            else
            {
                image_LeftHandWeaponIcon.enabled = true;
                if (image_LeftHandWeaponIcon.sprite != emptyWeaponSprite)
                {
                    image_LeftHandWeaponIcon.sprite = emptyWeaponSprite;
                    image_LeftHandWeaponIcon.SetNativeSize();
                }
                image_LeftHandWeaponIcon.color = new Color32(255, 255, 255, 60);
            }
        }

        // ---------------- RIGHT HAND ----------------
        if (rightHandWeapon != null && !isTwoHanding_Left)
        {
            if (image_RightHandWeaponIcon.sprite != rightHandWeapon.spr_Weapon)
            {
                image_RightHandWeaponIcon.sprite = rightHandWeapon.spr_Weapon;
                image_RightHandWeaponIcon.SetNativeSize();
            }

            if (isTwoHanding_Right)
            {
                obj_RightHandActionIcon_OneHanded.SetActive(false);
                obj_RightHandActionIcon_TwoHanded.SetActive(true);
            }
            else
            {
                obj_RightHandActionIcon_OneHanded.SetActive(true);
                obj_RightHandActionIcon_TwoHanded.SetActive(false);
            }

            canvasGroup_RightHandWeaponIcon.alpha = 1f;
            image_RightHand.enabled = true;
            image_RightHandWeaponIcon.color = Color.white;
        }
        else
        {
            image_RightHand.enabled = false;

            if (isTwoHanding_Left)
            {
                canvasGroup_RightHandWeaponIcon.alpha = 0.4f; // Hide completely if left hand is two-handing
                obj_RightHandActionIcon_OneHanded.SetActive(false);
                obj_RightHandActionIcon_TwoHanded.SetActive(false);
            }
            else
            {
                image_RightHandWeaponIcon.enabled = true;
                if (image_RightHandWeaponIcon.sprite != emptyWeaponSprite)
                {
                    image_RightHandWeaponIcon.sprite = emptyWeaponSprite;
                    image_RightHandWeaponIcon.SetNativeSize();
                }
                image_RightHandWeaponIcon.color = new Color32(255, 255, 255, 60);
            }
        }
    }

    private void HandleItemAndSpellSprites()
    {
        // -----------------------
        // SPELL UI UPDATE
        // -----------------------
        if (currentActiveSpell != null && currentActiveSpell.spr_Icon_Frame != null)
        {
            if (image_Spell.sprite != currentActiveSpell.spr_Icon_Frame)
            {
                image_Spell.sprite = currentActiveSpell.spr_Icon_Frame;
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
        if (currentActiveItem != null && currentActiveItem.spr_Icon_Frame != null)
        {
            int quantity = InventoryManager.instance.consumables[currentActiveItem.itemType];
            text_ItemCount.text = $"{quantity}";

            if (image_Item.sprite != currentActiveItem.spr_Icon_Frame)
            {
                image_Item.sprite = currentActiveItem.spr_Icon_Frame;
                image_Item.SetNativeSize();
                image_Item.color = Color.white; // Full opacity
            }
        }
        else
        {
            if (image_Item.sprite != emptyItemSprite)
            {
                text_ItemCount.text = "";
                image_Item.sprite = emptyItemSprite;
                image_Item.SetNativeSize();
                image_Item.color = new Color32(255, 255, 255, 60); // Dimmed when empty
            }
        }
    }

    // =========================================================
    // COMBAT INPUT
    //   Attack button (left mouse / right trigger)  -> attack
    //   Block button  (right mouse / left trigger)  -> block + parry
    //
    //   Two-handing a sword : Attack = swing, Block = two-handed guard (best stamina)
    //   Shield + anything   : Attack = other hand's weapon, Block = shield
    //   Sword + sword       : Attack = alternate right/left swings, Block = both swords guard (one-hand pose each)
    //   One sword only      : Attack = swing, Block = one-hand sword guard
    //   Anything else       : no guard. Attack = right-hand weapon, Block button = left-hand weapon's attack
    //                         (if only one hand holds a weapon, it uses the Attack button)
    // =========================================================

    private bool AttackPressed => InputSchemeManager.instance.isLeftHandPressed;
    private bool AttackHeld => InputSchemeManager.instance.isLeftHandHeld;
    private bool BlockPressed => InputSchemeManager.instance.isRightHandPressed;
    private bool BlockHeld => InputSchemeManager.instance.isRightHandHeld;

    private bool HandPressed(bool isLeftHand)
    {
        HandButton button = isLeftHand ? leftHandButton : rightHandButton;
        return button == HandButton.Attack ? AttackPressed : button == HandButton.Block && BlockPressed;
    }

    private bool HandHeld(bool isLeftHand)
    {
        HandButton button = isLeftHand ? leftHandButton : rightHandButton;
        return button == HandButton.Attack ? AttackHeld : button == HandButton.Block && BlockHeld;
    }

    // Attack cooldown with upgrades and the weapon's two-handed modifier
    private float AttackCooldown(Weapon weapon)
    {
        float cooldown = PlayerUpgrades.GetAttackCooldown(weapon);
        return IsTwoHanded(weapon) ? cooldown * weapon.twoHandedCooldownMultiplier : cooldown;
    }

    private bool IsTwoHanded(Weapon weapon)
    {
        return weapon != null && ((isTwoHanding_Left && leftHandWeapon == weapon) || (isTwoHanding_Right && rightHandWeapon == weapon));
    }

    private static bool IsMelee(Weapon w) => w != null && w.weaponCategory == WeaponCategory.Melee;
    private static bool IsShield(Weapon w) => w != null && w.weaponCategory == WeaponCategory.Shield;

    private void HandleCombatInput()
    {
        if (EquipmentLoadOut.instance.isPanelActive) { return; }
        if (isQuickMenuActive) { return; }

        Weapon right = rightHandWeapon;
        Weapon left = leftHandWeapon;
        leftHandButton = HandButton.None;
        rightHandButton = HandButton.None;

        // 1. Two-handing: only that weapon is used
        if ((isTwoHanding_Right && right != null) || (isTwoHanding_Left && left != null))
        {
            bool twoHandLeft = isTwoHanding_Left && left != null;
            Weapon weapon = twoHandLeft ? left : right;

            if (IsMelee(weapon))
            {
                SetHandButton(twoHandLeft, HandButton.Attack);
                Guard(twoHandLeft, BlockHeld, PerformActionState.Block);
                HandleMeleeInput(weapon, twoHandLeft);
            }
            else
            {
                SetHandButton(twoHandLeft, HandButton.Attack);
                HandleHandWeapon(weapon, twoHandLeft);
            }
            return;
        }

        // 2. Shield in either hand: Block = shield, Attack = the other hand
        if (IsShield(right) || IsShield(left))
        {
            bool shieldLeft = IsShield(left); // two shields: the left one guards
            SetHandButton(shieldLeft, HandButton.Block);
            HandleShieldInput(shieldLeft ? left : right, shieldLeft);

            Weapon other = shieldLeft ? right : left;
            if (other != null && !IsShield(other))
            {
                SetHandButton(!shieldLeft, HandButton.Attack);
                HandleHandWeapon(other, !shieldLeft);
            }
            return;
        }

        // 3. A sword in each hand: alternate swings, guard with both
        if (IsMelee(right) && IsMelee(left))
        {
            Guard(true, BlockHeld, PerformActionState.Block);
            Guard(false, BlockHeld, PerformActionState.Block);
            if (isBlockingLeft || isBlockingRight) { return; }

            if (AttackPressed && !playerController.isKicking && playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                bool useLeft = nextDualAttackIsLeft;
                if (TryMeleeAttack(useLeft ? left : right, useLeft))
                {
                    nextDualAttackIsLeft = !nextDualAttackIsLeft;
                }
            }
            return;
        }

        // 4. One sword, other hand empty: swing + one-hand guard
        if ((IsMelee(right) && left == null) || (IsMelee(left) && right == null))
        {
            bool swordLeft = IsMelee(left);
            SetHandButton(swordLeft, HandButton.Attack);
            Guard(swordLeft, BlockHeld, PerformActionState.Block);
            HandleMeleeInput(swordLeft ? left : right, swordLeft);
            return;
        }

        // 5. No guard possible: right hand on Attack, left hand on the Block button
        //    (a single weapon always uses Attack)
        if (right != null && left != null)
        {
            SetHandButton(false, HandButton.Attack);
            SetHandButton(true, HandButton.Block);
        }
        else if (right != null) { SetHandButton(false, HandButton.Attack); }
        else if (left != null) { SetHandButton(true, HandButton.Attack); }

        ReleaseGuard(true);
        ReleaseGuard(false);
        if (right != null) { HandleHandWeapon(right, false); }
        if (left != null) { HandleHandWeapon(left, true); }
    }

    private void SetHandButton(bool isLeftHand, HandButton button)
    {
        if (isLeftHand) { leftHandButton = button; } else { rightHandButton = button; }
    }

    // The weapon's own action, driven by whatever button the hand is mapped to
    private void HandleHandWeapon(Weapon weapon, bool isLeftHand)
    {
        switch (weapon.weaponCategory)
        {
            case WeaponCategory.Shield: HandleShieldInput(weapon, isLeftHand); break;
            case WeaponCategory.Melee: HandleMeleeInput(weapon, isLeftHand); break;
            case WeaponCategory.Bow: HandleBowInput(weapon, isLeftHand); break;
            case WeaponCategory.PyromancyFlame: HandlePyromancyInput(weapon, isLeftHand); break;
            case WeaponCategory.SorceryCatalyst: HandleMagicInput(weapon, isLeftHand); break;
        }
    }

    // Raise / lower a sword guard on one hand (parry window opens when the guard goes up)
    private void Guard(bool isLeftHand, bool isHeld, PerformActionState guardState)
    {
        // Parry-only weapons (daggers): a tap parries, there is no held block
        Weapon guardWeapon = isLeftHand ? leftHandWeapon : rightHandWeapon;
        if (guardWeapon != null && guardWeapon.blockOnlyWhenTwoHanded && !IsTwoHanded(guardWeapon)) { isHeld = false; } // e.g. spear one-handed
        if (guardWeapon != null && guardWeapon.parryOnly)
        {
            ParryOnly(isLeftHand, guardState);
            return;
        }

        ref float blockCooldown = ref (isLeftHand ? ref currentBlockCooldownLeft : ref currentBlockCooldownRight);
        ref bool isBlocking = ref (isLeftHand ? ref isBlockingLeft : ref isBlockingRight);
        ref PerformActionState currentState = ref (isLeftHand ? ref currentState_LeftHand : ref currentState_Righthand);
        ref float currentParry = ref (isLeftHand ? ref currentParryLeft : ref currentParryRight);

        if (isHeld && blockCooldown <= 0f && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f && !isBlocking)
            {
                RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                isBlocking = true;
                currentState = guardState;
                currentParry = parryDuration;
            }
        }
        else if (!isHeld && isBlocking)
        {
            isBlocking = false;
            blockCooldown = blockCooldownDuration;
            currentState = PerformActionState.Idle;
        }
    }

    // Tap Block: open the parry window and show the parry pose briefly. Hits outside the window aren't blocked.
    private void ParryOnly(bool isLeftHand, PerformActionState poseState)
    {
        ref float cooldown = ref (isLeftHand ? ref currentBlockCooldownLeft : ref currentBlockCooldownRight);
        ref bool isBlocking = ref (isLeftHand ? ref isBlockingLeft : ref isBlockingRight);
        ref PerformActionState currentState = ref (isLeftHand ? ref currentState_LeftHand : ref currentState_Righthand);
        ref float currentParry = ref (isLeftHand ? ref currentParryLeft : ref currentParryRight);
        ref float poseTimer = ref (isLeftHand ? ref parryPoseTimerLeft : ref parryPoseTimerRight);

        isBlocking = false; // never a held block

        // Parrying is free: no stamina cost, and it works even with an empty stamina bar
        if (BlockPressed && cooldown <= 0f && !playerController.isKicking)
        {
            RumbleManager.instance.RumblePulse(0.6f, 1.5f, 0.1f);
            currentParry = parryDuration;
            currentState = poseState;
            poseTimer = parryPoseDuration;
            Weapon parryWeapon = isLeftHand ? leftHandWeapon : rightHandWeapon;
            cooldown = parryWeapon != null ? parryWeapon.parryCooldown : 0.6f; // per weapon (dagger: 0.35 s)
        }

        if (poseTimer > 0f)
        {
            poseTimer -= Time.deltaTime;
            if (poseTimer <= 0f && currentState == poseState) { currentState = PerformActionState.Idle; }
        }
    }

    private void ReleaseGuard(bool isLeftHand)
    {
        ref bool isBlocking = ref (isLeftHand ? ref isBlockingLeft : ref isBlockingRight);
        if (!isBlocking) { return; }

        isBlocking = false;
        if (isLeftHand) { currentBlockCooldownLeft = blockCooldownDuration; currentState_LeftHand = PerformActionState.Idle; }
        else { currentBlockCooldownRight = blockCooldownDuration; currentState_Righthand = PerformActionState.Idle; }
    }

    /// <summary>
    /// Block stamina cost multiplier for the guarding hand: the weapon's own modifier, scaled by how it's held
    /// (shields use only their own modifier; two-handing is the best sword guard).
    /// </summary>
    public float GetBlockStaminaModifier(bool isLeftHand)
    {
        Weapon weapon = isLeftHand ? leftHandWeapon : rightHandWeapon;
        if (weapon == null) { return 1f; }

        float modifier = weapon.blockStaminaDamageModifier;
        if (IsMelee(weapon))
        {
            if (isTwoHanding_Left || isTwoHanding_Right) { modifier *= twoHandedBlockMultiplier; }
            else if (IsMelee(leftHandWeapon) && IsMelee(rightHandWeapon)) { modifier *= dualSwordBlockMultiplier; }
            else { modifier *= oneHandSwordBlockMultiplier; }
        }
        return modifier * PlayerCurses.BlockStaminaMultiplier * PlayerRings.BlockStaminaMultiplier; // curses (e.g. Brittle), rings
    }

    // Swing if this hand's attack cooldown is ready. Returns true when a swing happened.
    private bool TryMeleeAttack(Weapon weapon, bool isLeftHand)
    {
        float lastAttackTime = isLeftHand ? lastAttackTimeLeft : lastAttackTimeRight;
        if (Time.time < (lastAttackTime + AttackCooldown(weapon)) * PlayerPerkManager.Instance.CurrentMoveSpeedMultiplier_Executioner)
        {
            return false;
        }
        // Swinging while sliding or jumping is allowed

        PerformAttack(weapon, isLeftHand);
        return true;
    }

    private void HandleShieldInput(Weapon weapon, bool isLeftHand)
    {
        bool isHeld = HandHeld(isLeftHand);
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
        bool isPressed = HandPressed(isLeftHand);
        ref float lastAttackTime = ref (isLeftHand ? ref lastAttackTimeLeft : ref lastAttackTimeRight);

        // (Guarding with swords is handled by HandleCombatInput / Guard)
        bool isThisHandBlocking = isLeftHand ? isBlockingLeft : isBlockingRight;
        if (isThisHandBlocking) return;

        if (isPressed && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                if (Time.time >= (lastAttackTime + AttackCooldown(weapon)) * PlayerPerkManager.Instance.CurrentMoveSpeedMultiplier_Executioner)
                {
                    PerformAttack(weapon, isLeftHand);
                }
            }
        }
    }

    private void HandleBowInput(Weapon weapon, bool isLeftHand)
    {
        bool isHeld = HandHeld(isLeftHand);
        bool isPressed = HandPressed(isLeftHand);

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
        bool isPressed = HandPressed(isLeftHand);
        ref float lastAttackTime = ref (isLeftHand ? ref lastAttackTimeLeft : ref lastAttackTimeRight);

        // Only begin a cast if the hand is Idle, the attack cooldown has passed, and we aren't kicking
        if (currentState == PerformActionState.Idle && isPressed && !playerController.isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > spell.flatStaminaCost &&
                playerController.CharacterUltimateComponent.CurrentUltimate >= spell.flatManaCost &&
                Time.time >= (lastAttackTime + AttackCooldown(weapon)) * PlayerPerkManager.Instance.CurrentMoveSpeedMultiplier_Executioner)
            {
                StartCoroutine(SpellCastSequence(weapon, spell, isLeftHand));
            }
        }
    }

    private IEnumerator SpellCastSequence(Weapon weapon, Item spell, bool isLeftHand)
    {
        // INITIAL SETUP: Deplete stamina and set the animation to Hold1 (Wind-up)
        playerController.StaminaDepleted(spell.flatStaminaCost);

        if (isLeftHand) anim_LeftHand.Play("Press1");
        else anim_RightHand.Play("Press1");

        Transform castTransform = cameraTransform;

        playerController.DepleteUltimate(spell.flatManaCost);

        // -----------------------------------------------------
        // PHASE 1: DELAY
        // -----------------------------------------------------
        if (spell.useDelay)
        {
            yield return new WaitForSeconds(spell.castDelay);
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

            while (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate >= spell.chargeManaDrainRate)
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
            SoundManager.instance.FireSound_Combustion(castTransform.position, true);

            // Must still be holding the button to spray
            if (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate >= spell.continuousManaDrainRate)
            {
                //GameObject activeSpell = null;
                
                // Keep spraying until button released or mana empty
                while (IsHandHeld(isLeftHand) && playerController.CharacterUltimateComponent.CurrentUltimate >= spell.continuousManaDrainRate
                    && playerController.CharacterStaminaComponent.CurrentStamina >= spell.continuousStaminaDrainRate)
                {
                    if (spell.spellPrefab != null)
                    {
                        // Parented to transform so the flamethrower follows the player's camera turning
                        Instantiate(spell.spellPrefab, castTransform.position, castTransform.rotation, castTransform);
                        
                        playerController.DepleteUltimate(spell.continuousManaDrainRate);
                        playerController.StaminaDepleted(spell.continuousStaminaDrainRate);
                        yield return new WaitForSeconds(spell.continuousSpawnRate);
                    }

                    if(playerController.CharacterUltimateComponent.CurrentUltimate < spell.continuousManaDrainRate)
                    {
                        break;
                    }

                    yield return null;
                }

                SoundManager.instance.FireSound_Combustion(castTransform.position, false);
                EndSpellCast(weapon, isLeftHand);
            }
        }
        else
        {
            // Single cast execution (Fires on button release if charged, or instantly if standard)
            if (spell.spellPrefab != null)
            {
                Instantiate(spell.spellPrefab, castTransform.position, castTransform.rotation, castTransform);
                EndSpellCast(weapon, isLeftHand);
                // TODO: Pass 'finalCharge' to the spawned projectile script here so it knows its multiplier
            }
        }

    }

    private bool IsHandHeld(bool isLeftHand)
    {
        return HandHeld(isLeftHand);
    }

    private void EndSpellCast(Weapon weapon, bool isLeftHand)
    {
        // Reverts state back to the Update loop reset timers
        if (isLeftHand)
        {
            lastAttackTimeLeft = Time.time;
            anim_LeftHand.Play("Idle");
            attackStateResetTimerLeft = AttackCooldown(weapon);
        }
        else
        {
            lastAttackTimeRight = Time.time;
            anim_RightHand.Play("Idle");
            attackStateResetTimerRight = AttackCooldown(weapon);
        }
    }

    private IEnumerator ExecutionSequence(EnemyEntity targetEnemy)
    {
        /////////////////// MOVE TOWARD TO ENEMY START /////////////////////////
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

        /////////////////// MOVE TOWARD TO ENEMY END /////////////////////////

        StartCoroutine(KickHeadOffFinisher(targetYaw, targetPitch, targetEnemy));

        /*if (rightHandWeapon == null && leftHandWeapon == null)
        {
            StartCoroutine(KickHeadOffFinisher(targetYaw, targetPitch, targetEnemy));
            yield break;
        }

        if (rightHandWeapon != null && leftHandWeapon != null && (!isTwoHanding_Left && !isTwoHanding_Right))
        {
            int randomIndex = Random.Range(0, 2);

            switch (randomIndex)
            {
                case 0: StartCoroutine(WeaponFinisher(leftHandWeapon, targetYaw, targetPitch, targetEnemy)); break;
                case 1: StartCoroutine(WeaponFinisher(rightHandWeapon ,targetYaw, targetPitch, targetEnemy)); break;
            }

            yield break;
        }

        if(leftHandWeapon != null)
        {
            StartCoroutine(WeaponFinisher(leftHandWeapon, targetYaw, targetPitch, targetEnemy));
            yield break;
        }

        if (rightHandWeapon != null)
        {
            StartCoroutine(WeaponFinisher(rightHandWeapon, targetYaw, targetPitch, targetEnemy));
            yield break;
        }*/
    }

    private IEnumerator KickHeadOffFinisher(float targetYaw, float targetPitch, EnemyEntity targetEnemy)
    {
        playerController.EnableKick();
        transform.eulerAngles = new Vector3(0f, targetYaw, 0f);
        playerController.verticalRotation = targetPitch;

        //currentState_Righthand = PerformActionState.Execute;

        targetEnemy.GotExecuted();

        cameraFollow.isSmoothing = false;
        cameraBob.TriggerShake(0.293f, 0.05f);
        cameraBob.TriggerZoomEffect(40f, 0.293f, 0.25f);

        yield return new WaitForSeconds(0.293f);
        PlayerPerkManager.Instance.OnEnemyKilled(true);
        if (targetEnemy != null) { targetEnemy.MarkKillReported(); } // don't count it again when it dies

        if (targetEnemy.decapitatedHeadPrefab != null)
        {
            Vector3 headSpawnPos = targetEnemy.executeTransform.position;
            Instantiate(targetEnemy.decapitatedHeadPrefab, headSpawnPos, Random.rotation);
        }

        cameraFollow.isSmoothing = false;
        playerController.isExecuting = false;
        playerController.isAttacking = false;

        // Trigger PlayerController's cleanup to hide the foot icon and set animator state back to idle
        playerController.EndKickVisuals();
    }

    public bool HasPyromancyEquipped()
    {
        bool rightIsPyro = rightHandWeapon != null && rightHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        bool leftIsPyro = leftHandWeapon != null && leftHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        return rightIsPyro || leftIsPyro;
    }

    public void StartExecutionRoutine(EnemyEntity targetEnemy)
    {
        playerController.StaminaDepleted(executeStaminaCost);
        StartCoroutine(ExecutionSequence(targetEnemy));
    }

    // Change this from 'private EnemyEntity' to 'public EnemyEntity'
    public EnemyEntity GetEnemyInFront()
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
        // Swinging while sliding or jumping is allowed

        ApplyAttackJolt(weapon, isLeftHand);

        playerController.TriggerAttackMovement(AttackCooldown(weapon), lungeForce);

        if (isLeftHand)
        {
            lastAttackTimeLeft = Time.time;
            attackStateResetTimerLeft = AttackCooldown(weapon);
        }
        else
        {
            lastAttackTimeRight = Time.time;
            attackStateResetTimerRight = AttackCooldown(weapon);
        }
    }

    private void ApplyAttackJolt(Weapon weapon, bool isLeftHand)
    {
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
        playerController.StaminaDepleted(weapon.staminaCost);

        if (isLeftHand)
        {
            if (Time.time > lastAttackTimeLeft + AttackCooldown(weapon) + 0.5f) comboStepLeft = 0;
        }
        else
        {
            if (Time.time > lastAttackTimeRight + AttackCooldown(weapon) + 0.5f) comboStepRight = 0;
        }

        Vector3 joltDirection = Vector3.zero;
        // Per-weapon attack shape (e.g. spear: longer, narrower thrust)
        float radius = weapon.attackRadiusOverride > 0f ? weapon.attackRadiusOverride : attackRadius;
        float range = attackRange * weapon.attackRangeMultiplier;
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * radius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = range + radius;
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

        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, radius, totalRange, attackLayerMask);
        bool hitSomething = false;

        // Nearest first, so a target limit (spear: 3) takes the closest enemies in the line
        if (weapon.maxTargets > 0 && hits.Length > 1) { System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); }
        int enemiesHit = 0;
        bool twoHanded = IsTwoHanded(weapon);

        if (hits.Length > 0)
        {
            HashSet<Component> processedTargets = new HashSet<Component>();

            foreach (RaycastHit hit in hits)
            {
                EnemyEntity targetEnemy = hit.collider.GetComponent<EnemyEntity>();
                Fracture destructible = hit.collider.GetComponent<Fracture>();
                ArrowProjectile arrow = hit.collider.GetComponent<ArrowProjectile>();
                FractureTrigger fractureTrigger = hit.collider.GetComponent<FractureTrigger>();

                float calculatedDamage = weapon.damage;
                if (twoHanded) { calculatedDamage *= weapon.twoHandedDamageMultiplier; }

                if(PlayerPerkManager.Instance != null)
                {
                    calculatedDamage *= PlayerPerkManager.Instance.OutgoingDamageMultiplier; // Blood Lust x Glass Jaw
                }

                if (targetEnemy != null && !processedTargets.Contains(targetEnemy) && weapon.maxTargets > 0 && enemiesHit >= weapon.maxTargets)
                {
                    continue; // already hit as many enemies as this weapon pierces
                }

                if (targetEnemy != null && processedTargets.Add(targetEnemy))
                {
                    enemiesHit++;

                    // Too close for the weapon's tip (spear)
                    if (weapon.closeRangePenaltyDistance > 0f)
                    {
                        Vector3 toEnemy = targetEnemy.transform.position - transform.position;
                        toEnemy.y = 0f;
                        if (toEnemy.magnitude < weapon.closeRangePenaltyDistance) { calculatedDamage *= weapon.closeRangeDamageMultiplier; }
                    }

                    targetEnemy.TakeSwordHit(isLeftAttack, hit.point, calculatedDamage, true, isLeftHand, weapon.damageImpactSound);
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    destructible.TakeDamage(calculatedDamage, hit.collider, hit.point, cameraTransform.forward);
                    hitSomething = true;

                    if (fractureTrigger != null)
                    {
                        fractureTrigger.TriggerMaterialSound_Hit();
                    }
                }
                else if (arrow != null && processedTargets.Add(arrow))
                {
                    playerController.BlockedOrParriedEffect(arrow.gameObject.transform.position);
                    RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
                    cameraBob.TriggerShake(0.293f, 0.05f);
                    Destroy(arrow.gameObject);
                    hitSomething = true;
                    SoundManager.instance.BlockOrParrySound(weapon.audioClips_ParrySound, playerController.transform.position);
                }
            }
        }

        // Riposte Echo: a stored parry charge turns this swing into a shockwave
        bool releasedRiposte = PlayerPerkManager.Instance != null &&
            PlayerPerkManager.Instance.TryReleaseRiposte(cameraTransform.position, cameraTransform.forward, weapon.damage);

        if (!hitSomething && !releasedRiposte)
        {
            SoundManager.instance.SwordSound_Air(transform.position);
        }

        playerController.TriggerMeleeJolt(joltDirection);

        if (isLeftHand)
        {
            comboStepLeft++;
            if (comboStepLeft >= Mathf.Clamp(weapon.comboLength, 1, 3)) comboStepLeft = 0;
        }
        else
        {
            comboStepRight++;
            if (comboStepRight >= Mathf.Clamp(weapon.comboLength, 1, 3)) comboStepRight = 0;
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
