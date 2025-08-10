// File: Assets/PROJECT/Scripts/Combat/EcsRangedShooter.cs
using MoreMountains.Tools;
using OneBitRob.AI;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// <summary>
    /// Per-unit projectile spawner for ECS ranged attacks.
    /// Attach to any *ranged enemy prefab*. MonoBridgeSystem calls Fire().
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBrain))]
    public class EcsRangedShooter : MonoBehaviour
    {
        [Header("Pool / Prefab")]
        [Tooltip("Pooler that returns a prefab with EcsBullet + MMPoolableObject")]
        public MMObjectPooler ObjectPooler;

        [Header("Defaults")]
        public float ProjectileSpeed = 60f;
        public float Damage          = 10f;
        public float MaxDistance     = 40f;

        [Header("Targeting")]
        [Tooltip("If true and the unit has a HandleWeapon with a mask, use that; otherwise fall back to UnitBrain's target mask.")]
        public bool UseHandleWeaponLayerMask = true;

        [Tooltip("If non-zero, overrides all other masks")]
        public LayerMask TargetMaskOverride;

        private UnitBrain _brain;
        private EnigmaEngine.EnigmaCharacterHandleWeapon _handle;

        private void Awake()
        {
            _brain  = GetComponent<UnitBrain>();
            _handle = _brain ? _brain.HandleWeapon : null;

            if (ObjectPooler == null) ObjectPooler = GetComponent<MMObjectPooler>();
#if UNITY_EDITOR
            if (ObjectPooler == null)
                Debug.LogWarning($"[{name}] EcsRangedShooter has no ObjectPooler.");
#endif
        }

        public void Fire(Vector3 origin, Vector3 direction, GameObject attacker)
        {
            if (ObjectPooler == null) return;

            GameObject go = ObjectPooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var bullet   = go.GetComponent<EcsBullet>();

#if UNITY_EDITOR
            if (bullet == null)
            {
                Debug.LogError($"[{name}] Pooled projectile must have EcsBullet + MMPoolableObject.");
                return;
            }
#endif
            go.transform.position = origin;
            go.transform.forward  = direction;

            int layerMask = (TargetMaskOverride.value != 0)
                ? TargetMaskOverride.value
                : (UseHandleWeaponLayerMask && _handle != null && _handle.UseTargetLayerMask
                    ? _handle.TargetLayerMask.value
                    : _brain.GetTargetLayerMask().value);

            bullet.Arm(new EcsBullet.ArmData
            {
                Attacker    = attacker,
                Origin      = origin,
                Direction   = direction,
                Speed       = Mathf.Max(0.01f, ProjectileSpeed),
                Damage      = Mathf.Max(0f, Damage),
                MaxDistance = Mathf.Max(0.1f, MaxDistance),
                LayerMask   = layerMask
            });

            go.SetActive(true);
            if (poolable != null) poolable.TriggerOnSpawnComplete();
        }
    }
}
