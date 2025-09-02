using UnityEngine;
using OneBitRob.Config;
using OneBitRob.Debugging;
using OneBitRob.FX;

namespace OneBitRob
{
    [DefaultExecutionOrder(-10000)]
    public sealed class MainGameManager : MonoBehaviour
    {
        [Header("Global Configs")]
        public CombatLayersSettings combatLayersSettings;
        public DamageNumbersSettings damageNumbersSettings;
        public DebugSettings debugSettings;
        
        private void Awake()
        {
            if (combatLayersSettings) CombatLayers.Set(combatLayersSettings);
            if (damageNumbersSettings) DamageNumbersManager.SetProfile(damageNumbersSettings);
            if (debugSettings) DebugDraw.SetSettings(debugSettings);
        }
    }
}