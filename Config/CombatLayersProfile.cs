// Runtime/Config/CombatLayersConfig.cs
using UnityEngine;

namespace OneBitRob.Config
{
    [CreateAssetMenu(menuName = "SO/Config/Combat Layers", fileName = "CombatLayersConfig")]
    public class CombatLayersConfig : ScriptableObject
    {
        [Header("Damageable Physics Layers (indices)")]
        [Tooltip("Layer index for Ally damageable colliders")]
        public int AllyDamageableLayer = 10;

        [Tooltip("Layer index for Enemy damageable colliders")]
        public int EnemyDamageableLayer = 11;

        [Header("Target Masks (LayerMask filters for auto-targeting)")]
        public LayerMask AllyMask;   // what enemies should raycast/overlap for Allies
        public LayerMask EnemyMask;  // what allies should raycast/overlap for Enemies
    }
}