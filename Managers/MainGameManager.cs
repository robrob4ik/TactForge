// Runtime/Config/Installers/GameConfigInstaller.cs

using UnityEngine;
using OneBitRob.Config;
using OneBitRob.FX;

namespace OneBitRob
{
    /// <summary>
    /// Wires global config assets into their static accessors at boot.
    /// Keep this in your first-loaded scene.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MainGameManager : MonoBehaviour
    {
        [Header("Global Configs")]
        public CombatLayersConfig combatLayers;

        [Header("FX Configs")]
        public DamageNumbersSettings damageNumbersSettings;

        private void Awake()
        {
#if UNITY_EDITOR
            if (!combatLayers) Debug.LogWarning("[GameConfigInstaller] CombatLayersConfig is not assigned. Using defaults (Enemy=11, Ally=12).");
            if (!damageNumbersSettings) Debug.LogWarning("[GameConfigInstaller] DamageNumbersProfile is not assigned. Damage popups will be disabled until a profile is set.");
#endif
            if (combatLayers) CombatLayers.Set(combatLayers);

            if (damageNumbersSettings) DamageNumbersManager.SetProfile(damageNumbersSettings);
        }
    }
}