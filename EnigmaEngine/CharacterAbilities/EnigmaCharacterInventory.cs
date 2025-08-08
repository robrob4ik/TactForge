using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.InventoryEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [System.Serializable]
    public struct AutoPickItem
    {
        public InventoryItem Item;
        public int Quantity;
    }
    
    [MMHiddenProperties("AbilityStopFeedbacks")]
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Inventory")]
    public class EnigmaCharacterInventory : EnigmaCharacterAbility, MMEventListener<MMInventoryEvent>
    {
        public enum WeaponRotationModes
        {
            Normal,
            AddEmptySlot,
            AddInitialWeapon
        }

        [Title("Inventories")]
        [Tooltip("The unique ID of this player as far as the InventoryEngine is concerned. This has to match all its Inventory and InventoryEngine UI components' PlayerID for that player. If you're not going for multiplayer here, just leave Player1.")]
        public string PlayerID = "Player1";

        [Tooltip("The name of the main inventory for this character")]
        public string MainInventoryName;

        [Tooltip("The name of the inventory where this character stores weapons")]
        public string WeaponInventoryName;

        [Tooltip("The name of the hotbar inventory for this character")]
        public string HotbarInventoryName;

        [Tooltip("A transform to pass to the inventories, will be passed to the inventories and used as reference for drops. If left empty, this.transform will be used.")]
        public Transform InventoryTransform;

        [Title("Weapon Rotation")] [Tooltip("If this is true, will add an empty slot to the weapon rotation")]
        public WeaponRotationModes WeaponRotationMode = WeaponRotationModes.Normal;

        [Title("Auto Pick")] [Tooltip("A list of items to automatically add to this Character's inventories on start")]
        public AutoPickItem[] AutoPickItems;

        [Tooltip("If this is true, auto pick items will only be added if the main inventory is empty")]
        public bool AutoPickOnlyIfMainInventoryIsEmpty;

        [Title("Auto Equip")] [Tooltip("A weapon to auto equip on start")]
        public EnigmaInventoryWeapon AutoEquipWeaponOnStart;

        [Tooltip("If this is true, auto equip will only occur if the main inventory is empty")]
        public bool AutoEquipOnlyIfMainInventoryIsEmpty;

        [Tooltip("If this is true, auto equip will only occur if the equipment inventory is empty")]
        public bool AutoEquipOnlyIfEquipmentInventoryIsEmpty;

        [Tooltip("If this is true, auto equip will also happen on respawn")]
        public bool AutoEquipOnRespawn = true;

        [Tooltip("The target handle weapon ability - if left empty, will pick the first one it finds")]
        public EnigmaCharacterHandleWeapon CharacterHandleWeapon;

        public virtual Inventory MainInventory { get; set; }
        public virtual Inventory WeaponInventory { get; set; }
        public virtual Inventory HotbarInventory { get; set; }
        public virtual List<string> AvailableWeaponsIDs => _availableWeaponsIDs;

        protected List<int> _availableWeapons;
        protected List<string> _availableWeaponsIDs;
        protected string _nextWeaponID;
        protected bool _nextFrameWeapon = false;
        protected string _nextFrameWeaponName;
        protected const string _emptySlotWeaponName = "_EmptySlotWeaponName";
        protected const string _initialSlotWeaponName = "_InitialSlotWeaponName";
        protected bool _initialized = false;
        protected int _initializedFrame = -1;


        /// On init we setup our ability
        protected override void Initialization()
        {
            base.Initialization();
            Setup();
        }


        /// Grabs all inventories, and fills weapon lists
        protected virtual void Setup()
        {
            if (InventoryTransform == null)
            {
                InventoryTransform = this.transform;
            }

            GrabInventories();
            if (CharacterHandleWeapon == null)
            {
                CharacterHandleWeapon = _character?.FindAbility<EnigmaCharacterHandleWeapon>();
            }

            FillAvailableWeaponsLists();

            if (_initialized)
            {
                return;
            }

            bool mainInventoryEmpty = true;
            if (MainInventory != null)
            {
                mainInventoryEmpty = MainInventory.NumberOfFilledSlots == 0;
            }

            bool canAutoPick = !(AutoPickOnlyIfMainInventoryIsEmpty && !mainInventoryEmpty);
            bool canAutoEquip = !(AutoEquipOnlyIfMainInventoryIsEmpty && !mainInventoryEmpty);

            if (AutoEquipOnlyIfEquipmentInventoryIsEmpty && (WeaponInventory.NumberOfFilledSlots > 0))
            {
                canAutoEquip = false;
            }

            // we auto pick items if needed
            if ((AutoPickItems.Length > 0) && !_initialized && canAutoPick)
            {
                foreach (AutoPickItem item in AutoPickItems)
                {
                    MMInventoryEvent.Trigger(MMInventoryEventType.Pick, null, item.Item.TargetInventoryName, item.Item,
                        item.Quantity, 0, PlayerID);
                }
            }

            // we auto equip a weapon if needed
            if ((AutoEquipWeaponOnStart != null) && !_initialized && canAutoEquip)
            {
                AutoEquipWeapon();
            }

            _initialized = true;
            _initializedFrame = Time.frameCount;
        }

        protected virtual void AutoEquipWeapon()
        {
            MMInventoryEvent.Trigger(MMInventoryEventType.Pick, null, AutoEquipWeaponOnStart.TargetInventoryName,
                AutoEquipWeaponOnStart, 1, 0, PlayerID);
            EquipWeapon(AutoEquipWeaponOnStart.ItemID);
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();

            if (_nextFrameWeapon)
            {
                EquipWeapon(_nextFrameWeaponName);
                _nextFrameWeapon = false;
            }
        }


        /// Grabs any inventory it can find that matches the names set in the inspector
        protected virtual void GrabInventories()
        {
            Inventory[] inventories = FindObjectsOfType<Inventory>();
            foreach (Inventory inventory in inventories)
            {
                if (inventory.PlayerID != PlayerID)
                {
                    continue;
                }

                if ((MainInventory == null) && (inventory.name == MainInventoryName))
                {
                    MainInventory = inventory;
                }

                if ((WeaponInventory == null) && (inventory.name == WeaponInventoryName))
                {
                    WeaponInventory = inventory;
                }

                if ((HotbarInventory == null) && (inventory.name == HotbarInventoryName))
                {
                    HotbarInventory = inventory;
                }
            }

            if (MainInventory != null)
            {
                MainInventory.SetOwner(this.gameObject);
                MainInventory.TargetTransform = InventoryTransform;
            }

            if (WeaponInventory != null)
            {
                WeaponInventory.SetOwner(this.gameObject);
                WeaponInventory.TargetTransform = InventoryTransform;
            }

            if (HotbarInventory != null)
            {
                HotbarInventory.SetOwner(this.gameObject);
                HotbarInventory.TargetTransform = InventoryTransform;
            }
        }


        /// On handle input, we watch for the switch weapon button, and switch weapon if needed
        protected override void HandleInput()
        {
            if (!AbilityAuthorized
                || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal))
            {
                return;
            }

            if (_inputManager.SwitchWeaponButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                SwitchWeapon();
            }
        }


        /// Fills the weapon list. The weapon list will be used to determine what weapon we can switch to
        protected virtual void FillAvailableWeaponsLists()
        {
            _availableWeaponsIDs = new List<string>();
            if ((CharacterHandleWeapon == null) || (WeaponInventory == null))
            {
                return;
            }

            _availableWeapons = MainInventory.InventoryContains(ItemClasses.Weapon);
            foreach (int index in _availableWeapons)
            {
                _availableWeaponsIDs.Add(MainInventory.Content[index].ItemID);
            }

            if (!InventoryItem.IsNull(WeaponInventory.Content[0]))
            {
                if ((MainInventory.InventoryContains(WeaponInventory.Content[0].ItemID).Count <= 0) ||
                    WeaponInventory.Content[0].MoveWhenEquipped)
                {
                    _availableWeaponsIDs.Add(WeaponInventory.Content[0].ItemID);
                }
            }

            _availableWeaponsIDs.Sort();
        }


        /// Determines the name of the next weapon in line
        protected virtual void DetermineNextWeaponName()
        {
            if (InventoryItem.IsNull(WeaponInventory.Content[0]))
            {
                _nextWeaponID = _availableWeaponsIDs[0];
                return;
            }

            if ((_nextWeaponID == _emptySlotWeaponName) || (_nextWeaponID == _initialSlotWeaponName))
            {
                _nextWeaponID = _availableWeaponsIDs[0];
                return;
            }

            for (int i = 0; i < _availableWeaponsIDs.Count; i++)
            {
                if (_availableWeaponsIDs[i] == WeaponInventory.Content[0].ItemID)
                {
                    if (i == _availableWeaponsIDs.Count - 1)
                    {
                        switch (WeaponRotationMode)
                        {
                            case WeaponRotationModes.AddEmptySlot:
                                _nextWeaponID = _emptySlotWeaponName;
                                return;
                            case WeaponRotationModes.AddInitialWeapon:
                                _nextWeaponID = _initialSlotWeaponName;
                                return;
                        }

                        _nextWeaponID = _availableWeaponsIDs[0];
                    }
                    else
                    {
                        _nextWeaponID = _availableWeaponsIDs[i + 1];
                    }
                }
            }
        }


        /// Equips the weapon with the name passed in parameters
        public virtual void EquipWeapon(string weaponID)
        {
            if ((weaponID == _emptySlotWeaponName) && (CharacterHandleWeapon != null))
            {
                MMInventoryEvent.Trigger(MMInventoryEventType.UnEquipRequest, null, WeaponInventoryName,
                    WeaponInventory.Content[0], 0, 0, PlayerID);
                CharacterHandleWeapon.ChangeWeapon(null, _emptySlotWeaponName, false);
                MMInventoryEvent.Trigger(MMInventoryEventType.Redraw, null, WeaponInventory.name, null, 0, 0, PlayerID);
                return;
            }

            if ((weaponID == _initialSlotWeaponName) && (CharacterHandleWeapon != null))
            {
                MMInventoryEvent.Trigger(MMInventoryEventType.UnEquipRequest, null, WeaponInventoryName,
                    WeaponInventory.Content[0], 0, 0, PlayerID);
                CharacterHandleWeapon.ChangeWeapon(CharacterHandleWeapon.InitialWeapon, _initialSlotWeaponName, false);
                MMInventoryEvent.Trigger(MMInventoryEventType.Redraw, null, WeaponInventory.name, null, 0, 0, PlayerID);
                return;
            }

            for (int i = 0; i < MainInventory.Content.Length; i++)
            {
                if (InventoryItem.IsNull(MainInventory.Content[i]))
                {
                    continue;
                }

                if (MainInventory.Content[i].ItemID == weaponID)
                {
                    MMInventoryEvent.Trigger(MMInventoryEventType.EquipRequest, null, MainInventory.name,
                        MainInventory.Content[i], 0, i, PlayerID);
                    break;
                }
            }
        }


        /// Switches to the next weapon in line
        protected virtual void SwitchWeapon()
        {
            // if there's no character handle weapon component, we can't switch weapon, we do nothing and exit
            if ((CharacterHandleWeapon == null) || (WeaponInventory == null))
            {
                return;
            }

            FillAvailableWeaponsLists();

            // if we only have 0 or 1 weapon, there's nothing to switch, we do nothing and exit
            if (_availableWeaponsIDs.Count <= 0)
            {
                return;
            }

            DetermineNextWeaponName();
            EquipWeapon(_nextWeaponID);
            PlayAbilityStartFeedbacks();
            PlayAbilityStartSfx();
        }


        /// Watches for InventoryLoaded events
        /// When an inventory gets loaded, if it's our WeaponInventory, we check if there's already a weapon equipped, and if yes, we equip it
        public virtual void OnMMEvent(MMInventoryEvent inventoryEvent)
        {
            if (inventoryEvent.InventoryEventType == MMInventoryEventType.InventoryLoaded)
            {
                if (inventoryEvent.TargetInventoryName == WeaponInventoryName)
                {
                    this.Setup();
                    if (WeaponInventory != null)
                    {
                        if (!InventoryItem.IsNull(WeaponInventory.Content[0]))
                        {
                            CharacterHandleWeapon.Setup();
                            WeaponInventory.Content[0].Equip(PlayerID);
                        }
                    }
                }
            }

            if (inventoryEvent.InventoryEventType == MMInventoryEventType.Pick)
            {
                bool isSubclass = (inventoryEvent.EventItem.GetType().IsSubclassOf(typeof(EnigmaInventoryWeapon)));
                bool isClass = (inventoryEvent.EventItem.GetType() == typeof(EnigmaInventoryWeapon));
                if (isClass || isSubclass)
                {
                    EnigmaInventoryWeapon inventoryWeapon = (EnigmaInventoryWeapon)inventoryEvent.EventItem;
                    switch (inventoryWeapon.AutoEquipMode)
                    {
                        case EnigmaInventoryWeapon.AutoEquipModes.NoAutoEquip:
                            // we do nothing
                            break;

                        case EnigmaInventoryWeapon.AutoEquipModes.AutoEquip:
                            _nextFrameWeapon = true;
                            _nextFrameWeaponName = inventoryEvent.EventItem.ItemID;
                            break;

                        case EnigmaInventoryWeapon.AutoEquipModes.AutoEquipIfEmptyHanded:
                            if (CharacterHandleWeapon.CurrentWeapon == null)
                            {
                                _nextFrameWeapon = true;
                                _nextFrameWeaponName = inventoryEvent.EventItem.ItemID;
                            }

                            break;
                    }
                }
            }
        }

        protected override void OnRespawn()
        {
            if (_initializedFrame == Time.frameCount)
            {
                return;
            }

            if ((AutoEquipWeaponOnStart == null) || !AutoEquipOnRespawn || (MainInventory == null) ||
                (WeaponInventory == null))
            {
                return;
            }

            MMInventoryEvent.Trigger(MMInventoryEventType.Destroy, null, MainInventoryName, AutoEquipWeaponOnStart, 1,
                0, PlayerID);
            AutoEquipWeapon();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            if (WeaponInventory != null)
            {
                MMInventoryEvent.Trigger(MMInventoryEventType.UnEquipRequest, null, WeaponInventoryName,
                    WeaponInventory.Content[0], 0, 0, PlayerID);
            }
        }


        /// On enable, we start listening for MMGameEvents. You may want to extend that to listen to other types of events.
        protected override void OnEnable()
        {
            base.OnEnable();
            this.MMEventStartListening<MMInventoryEvent>();
        }


        /// On disable, we stop listening for MMGameEvents. You may want to extend that to stop listening to other types of events.
        protected override void OnDisable()
        {
            base.OnDisable();
            this.MMEventStopListening<MMInventoryEvent>();
        }
    }
}