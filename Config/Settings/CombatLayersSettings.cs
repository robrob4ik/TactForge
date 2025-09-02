using UnityEngine;

namespace OneBitRob.Config
{
    [CreateAssetMenu(menuName = "TactForge/Config/Combat Layers", fileName = "CombatLayersProfile")]
    public class CombatLayersSettings : ScriptableObject
    {
        [Header("Physics Layers (indices)")]
        [Tooltip("Layer index for the Player")]
        public int PlayerLayer;

        [Tooltip("Layer index for Allied units.")]
        public int AllyLayer;

        [Tooltip("Layer index for Enemy units.")]
        public int EnemyLayer;

        [Header("Single-bit Masks (auto-kept in sync with indices)")]
        public LayerMask PlayerMask;
        public LayerMask AllyMask;
        public LayerMask EnemyMask;
    }
}