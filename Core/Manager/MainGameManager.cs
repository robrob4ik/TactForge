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
            if (!combatLayersSettings) Debug.LogError("[MainGameManager] CombatLayersSettings missing.", this);
            else CombatLayers.Set(combatLayersSettings);

            if (!damageNumbersSettings) Debug.LogError("[MainGameManager] DamageNumbersSettings missing.", this);
            else DamageNumbersManager.SetProfile(damageNumbersSettings);

            if (!debugSettings) Debug.LogError("[MainGameManager] DebugSettings missing. DebugDraw will throw on use.", this);
            DebugDraw.SetSettings(debugSettings);
        }
    }
}