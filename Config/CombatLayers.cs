// Runtime/Config/CombatLayers.cs
using UnityEngine;

namespace OneBitRob.Config
{
    public static class CombatLayers
    {
        private static CombatLayersConfig _instance;
        private static bool _warned;

        public static void Set(CombatLayersConfig config) => _instance = config;

        public static CombatLayersConfig Instance
        {
            get
            {
                if (_instance) return _instance;
                _instance = Resources.Load<CombatLayersConfig>("CombatLayersConfig");
#if UNITY_EDITOR
                if (!_instance && !_warned)
                {
                    Debug.LogWarning("[CombatLayers] No CombatLayersConfig found in Resources. Using defaults.");
                    _warned = true;
                }
#endif
                return _instance;
            }
        }

        // Defaults if no asset is present.
        private const int DefaultAlly = 10;
        private const int DefaultEnemy = 11;

        public static int AllyDamageableLayer =>
            Instance ? Instance.AllyDamageableLayer : DefaultAlly;

        public static int EnemyDamageableLayer =>
            Instance ? Instance.EnemyDamageableLayer : DefaultEnemy;

        public static LayerMask AllyMask =>
            Instance ? Instance.AllyMask : (1 << EnemyDamageableLayer);

        public static LayerMask EnemyMask =>
            Instance ? Instance.EnemyMask : (1 << AllyDamageableLayer);

        public static LayerMask TargetMaskFor(bool isEnemy) =>
            isEnemy ? AllyMask : EnemyMask;

        public static int DamageableLayerFor(bool isEnemy) =>
            isEnemy ? AllyDamageableLayer : EnemyDamageableLayer;

        public static LayerMask DamageableLayerMaskFor(bool isEnemy) =>
            1 << DamageableLayerFor(isEnemy);

        public static LayerMask FriendlyLayerMaskFor(bool isEnemy) =>
            1 << (isEnemy ? EnemyDamageableLayer : AllyDamageableLayer);
    }
}