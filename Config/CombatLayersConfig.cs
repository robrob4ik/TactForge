// Runtime/Config/CombatLayersConfig.cs
using UnityEngine;

namespace OneBitRob.Config
{
    [CreateAssetMenu(menuName = "TactForge/Config/Combat Layers", fileName = "CombatLayersConfig")]
    public class CombatLayersConfig : ScriptableObject
    {
        [Header("Damageable Physics Layers (indices)")]
        [Tooltip("Layer index for Enemy damageable colliders")]
        public int EnemyDamageableLayer = 11;

        [Tooltip("Layer index for Ally damageable colliders")]
        public int AllyDamageableLayer = 12;

        [Header("Target Masks (LayerMask filters for auto-targeting)")]
        [Tooltip("Used when an ENEMY searches for ALLIES")]
        public LayerMask AllyMask;   // should include AllyDamageableLayer only

        [Tooltip("Used when an ALLY searches for ENEMIES")]
        public LayerMask EnemyMask;  // should include EnemyDamageableLayer only

#if UNITY_EDITOR
        [ContextMenu("Fix Masks Now")]
        private void FixMasksNow()
        {
            AllyMask  = 1 << Mathf.Clamp(AllyDamageableLayer,  0, 31);
            EnemyMask = 1 << Mathf.Clamp(EnemyDamageableLayer, 0, 31);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnValidate()
        {
            int allyBit  = 1 << Mathf.Clamp(AllyDamageableLayer,  0, 31);
            int enemyBit = 1 << Mathf.Clamp(EnemyDamageableLayer, 0, 31);

            if ((AllyMask.value  & allyBit)  != allyBit  || AllyMask.value  != allyBit)  AllyMask  = allyBit;
            if ((EnemyMask.value & enemyBit) != enemyBit || EnemyMask.value != enemyBit) EnemyMask = enemyBit;
        }
#endif
    }
}