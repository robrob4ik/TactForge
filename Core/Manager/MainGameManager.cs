using UnityEngine;
using OneBitRob.Config;
using OneBitRob.Debugging;
using OneBitRob.ECS;
using OneBitRob.FX;
using OneBitRob.VFX;

namespace OneBitRob
{
    [DefaultExecutionOrder(-10000)]
    public sealed class MainGameManager : MonoBehaviour
    {
        [Header("Global Configs")]
        public CombatLayersSettings combatLayers;

        [Header("FX Configs")]
        public DamageNumbersSettings damageNumbersSettings;

        [Header("Scene Services (optional)")]
        public ProjectilePoolManager projectilePools;
        public VfxPoolManager vfxPools;
        public FeedbackPoolManager feedbackPools;

        [Header("Debug")]
        public DebugSettings debugSettings;
        
        private void Awake()
        {
            if (combatLayers) CombatLayers.Set(combatLayers);
            if (damageNumbersSettings) DamageNumbersManager.SetProfile(damageNumbersSettings);
            
            var overlay = FindObjectOfType<DebugOverlay>(true);
            if (overlay) overlay.SetSettings(debugSettings);
            DebugDraw.SetSettings(debugSettings);
        }
    }
}