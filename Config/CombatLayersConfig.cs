// FILE: Assets/PROJECT/Scripts/Runtime/Config/CombatLayersConfig.cs
using UnityEngine;

namespace OneBitRob.Config
{
    [CreateAssetMenu(menuName = "TactForge/Config/Combat Layers", fileName = "CombatLayersConfig")]
    public class CombatLayersConfig : ScriptableObject
    {
        [Header("Physics Layers (indices)")]
        [Tooltip("Layer index for the Player (castle/hero/etc).")]
        public int PlayerLayer = 8;

        [Tooltip("Layer index for Allied units.")]
        public int AllyLayer = 9;

        [Tooltip("Layer index for Enemy units.")]
        public int EnemyLayer = 10;

        [Header("Single-bit Masks (auto-kept in sync with indices)")]
        public LayerMask PlayerMask;
        public LayerMask AllyMask;
        public LayerMask EnemyMask;

#if UNITY_EDITOR
        [ContextMenu("Fix Masks Now")]
        private void FixMasksNow()
        {
            PlayerMask = 1 << Mathf.Clamp(PlayerLayer, 0, 31);
            AllyMask   = 1 << Mathf.Clamp(AllyLayer,   0, 31);
            EnemyMask  = 1 << Mathf.Clamp(EnemyLayer,  0, 31);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnValidate()
        {
            int p = 1 << Mathf.Clamp(PlayerLayer, 0, 31);
            int a = 1 << Mathf.Clamp(AllyLayer,   0, 31);
            int e = 1 << Mathf.Clamp(EnemyLayer,  0, 31);

            if (PlayerMask.value != p) PlayerMask = p;
            if (AllyMask.value   != a) AllyMask   = a;
            if (EnemyMask.value  != e) EnemyMask  = e;
        }
#endif
    }
}