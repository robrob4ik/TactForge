using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System;
using MoreMountains.InventoryEngine;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [CreateAssetMenu(fileName = "InventoryWeapon", menuName = "EnigmaEngine/InventoryWeapon", order = 2)]
    [Serializable]
    /// Weapon item in the TopDown Engine
    public class EnigmaInventoryWeapon : InventoryItem
    {
        /// the possible auto equip modes
        public enum AutoEquipModes
        {
            NoAutoEquip,
            AutoEquip,
            AutoEquipIfEmptyHanded
        }

        [Title("Weapon")]
        [MMInformation("Here you need to bind the weapon you want to equip when picking that item.",
            MMInformationAttribute.InformationType.Info, false)]
        /// the weapon to equip
        [Tooltip("The weapon to equip")]
        public EnigmaWeapon EquippableWeapon;

        /// how to equip this weapon when picked : not equip it, automatically equip it, or only equip it if no weapon is currently equipped
        [Tooltip(
            "how to equip this weapon when picked : not equip it, automatically equip it, or only equip it if no weapon is currently equipped")]
        public AutoEquipModes AutoEquipMode = AutoEquipModes.NoAutoEquip;

        /// the ID of the CharacterHandleWeapon you want this weapon to be equipped to
        [Tooltip("The ID of the CharacterHandleWeapon you want this weapon to be equipped to")]
        public int HandleWeaponID = 1;


        /// When we grab the weapon, we equip it
        public override bool Equip(string playerID)
        {
            EquipWeapon(EquippableWeapon, playerID);
            return true;
        }


        /// When dropping or unequipping a weapon, we remove it
        public override bool UnEquip(string playerID)
        {
            // if this is a currently equipped weapon, we unequip it
            if (this.TargetEquipmentInventory(playerID) == null)
            {
                return false;
            }

            if (this.TargetEquipmentInventory(playerID).InventoryContains(this.ItemID).Count > 0)
            {
                EquipWeapon(null, playerID);
            }

            return true;
        }


        /// Grabs the CharacterHandleWeapon component and sets the weapon
        /// <param name="newWeapon">New weapon.</param>
        protected virtual void EquipWeapon(EnigmaWeapon newWeapon, string playerID)
        {
            if (EquippableWeapon == null)
            {
                return;
            }

            if (TargetInventory(playerID).Owner == null)
            {
                return;
            }

            EnigmaCharacter character = TargetInventory(playerID).Owner.GetComponentInParent<EnigmaCharacter>();

            if (character == null)
            {
                return;
            }

            // we equip the weapon to the chosen CharacterHandleWeapon
            EnigmaCharacterHandleWeapon targetHandleWeapon = null;
            EnigmaCharacterHandleWeapon[] handleWeapons =
                character.GetComponentsInChildren<EnigmaCharacterHandleWeapon>();
            foreach (EnigmaCharacterHandleWeapon handleWeapon in handleWeapons)
            {
                if (handleWeapon.HandleWeaponID == HandleWeaponID)
                {
                    targetHandleWeapon = handleWeapon;
                }
            }

            if (targetHandleWeapon != null)
            {
                targetHandleWeapon.ChangeWeapon(newWeapon, this.ItemID);
            }
        }
    }
}