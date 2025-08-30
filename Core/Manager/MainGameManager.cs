using UnityEngine;
using OneBitRob.Config;
using OneBitRob.FX;
using OneBitRob.AI.Debugging;
using OneBitRob.ECS;

namespace OneBitRob
{
    /// <summary>
    /// Single authority for runtime-wide configuration and service wiring.
    /// Keep this in the first loaded scene.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MainGameManager : MonoBehaviour
    {
        [Header("Global Configs")]
        public CombatLayersSettings combatLayers;

        [Header("FX Configs")]
        public DamageNumbersSettings damageNumbersSettings;

        [Header("Scene Services (optional)")]
        [Tooltip("Optional, but recommended — assign the scene-level pools/VFX managers.")]
        public ProjectilePoolManager projectilePools;
        public SpellVfxPoolManager spellVfx;

        private void Awake()
        {
#if UNITY_EDITOR
            if (!combatLayers) Debug.LogWarning("[MainGameManager] CombatLayersConfig is not assigned. Using defaults (Enemy=11, Ally=12).");
            if (!damageNumbersSettings) Debug.LogWarning("[MainGameManager] DamageNumbersProfile not assigned — damage popups disabled until a profile is set.");
#endif
            // 1) Combat layers (static accessor preserved for legacy)
            if (combatLayers) Config.CombatLayers.Set(combatLayers);
            GameServices.CombatLayers = combatLayers;

            // 2) Damage numbers profile
            if (damageNumbersSettings) DamageNumbersManager.SetProfile(damageNumbersSettings);
            GameServices.DamageNumbers = damageNumbersSettings;

            // 3) Scene services (resolve or create fallbacks)
            GameServices.ProjectilePools = projectilePools ? projectilePools : FindObjectOfType<ProjectilePoolManager>(true);
            GameServices.SpellVfxPools       = spellVfx       ? spellVfx       : FindObjectOfType<SpellVfxPoolManager>(true);
        }
    }
}
