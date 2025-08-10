using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace OneBitRob.EnigmaEngine
{
    public enum SpawnTransformsModes { Random, Sequential }

    [AddComponentMenu("Enigma Engine/Weapons/Enigma Projectile Weapon (Trimmed)")]
    public class EnigmaProjectileWeapon : EnigmaWeapon
    {
        [FoldoutGroup("Projectiles"), Title("Projectiles")]
        [Tooltip("Local offset used when WeaponUseTransform is not set and no SpawnTransform is available")]
        public Vector3 ProjectileSpawnOffset = Vector3.zero;

        [FoldoutGroup("Projectiles")]
        [Tooltip("Fallback direction if there is no owner / aim")]
        public Vector3 DefaultProjectileDirection = Vector3.forward;

        [FoldoutGroup("Projectiles")]
        [Tooltip("Number of projectiles per shot")]
        public int ProjectilesPerShot = 1;

        [FoldoutGroup("Spawn Transforms"), Title("Spawn Transforms")]
        [Tooltip("Optional list of spawn points (overrides offset)")]
        public List<Transform> SpawnTransforms = new();

        [FoldoutGroup("Spawn Transforms")]
        public SpawnTransformsModes SpawnTransformsMode = SpawnTransformsModes.Sequential;

        [FoldoutGroup("Spread"), Title("Spread")]
        [Tooltip("Euler degrees of random/even spread")]
        public Vector3 Spread = Vector3.zero;

        [FoldoutGroup("Spread")]
        public bool RotateWeaponOnSpread = false;

        [FoldoutGroup("Spread")]
        public bool RandomSpread = true;

        [FoldoutGroup("Pooling")]
        [Tooltip("Pooler for projectile prefab (must have EnigmaProjectile + MMPoolableObject)")]
        public MMObjectPooler ObjectPooler;

        [FoldoutGroup("Spawn Feedbacks"), Title("Spawn Feedbacks")]
        public List<MMFeedbacks> SpawnFeedbacks = new();

        // runtime
        protected Vector3 _spawnPos;
        protected int _spawnIndex;
        protected Vector3 _spreadEuler;

        public override void Initialization()
        {
            base.Initialization();
            if (ObjectPooler == null) ObjectPooler = GetComponent<MMObjectPooler>();
#if UNITY_EDITOR
            if (ObjectPooler == null)
                Debug.LogWarning($"{name}: No MMObjectPooler set on EnigmaProjectileWeapon.");
#endif
        }

        public override void WeaponUse()
        {
            base.WeaponUse();
            if (ObjectPooler == null) return;

            DetermineSpawnPosition();

            for (int i = 0; i < Mathf.Max(1, ProjectilesPerShot); i++)
            {
                SpawnOne(_spawnPos, i, ProjectilesPerShot);
                PlaySpawnFeedbacks();
            }
        }

        protected void DetermineSpawnPosition()
        {
            _spawnPos = transform.position + transform.rotation * ProjectileSpawnOffset;

            if (WeaponUseTransform != null)
                _spawnPos = WeaponUseTransform.position;

            if (SpawnTransforms.Count > 0)
            {
                if (SpawnTransformsMode == SpawnTransformsModes.Random)
                {
                    _spawnIndex = Random.Range(0, SpawnTransforms.Count);
                }
                else
                {
                    if (_spawnIndex < 0 || _spawnIndex >= SpawnTransforms.Count) _spawnIndex = 0;
                }
                _spawnPos = SpawnTransforms[_spawnIndex].position;
            }
        }

        protected void PlaySpawnFeedbacks()
        {
            if (SpawnFeedbacks.Count == 0) return;

            int idx = Mathf.Clamp(_spawnIndex, 0, SpawnFeedbacks.Count - 1);
            try { SpawnFeedbacks[idx]?.PlayFeedbacks(_spawnPos); }
            catch { SpawnFeedbacks[idx]?.PlayFeedbacks(); }

            if (SpawnTransformsMode == SpawnTransformsModes.Sequential && SpawnTransforms.Count > 0)
            {
                _spawnIndex = (_spawnIndex + 1) % SpawnTransforms.Count;
            }
        }

        protected void SpawnOne(Vector3 pos, int i, int total)
        {
            GameObject go = ObjectPooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var proj     = go.GetComponent<EnigmaProjectile>();

            go.transform.position = pos;

            // calculate spread (random or even)
            if (RandomSpread)
            {
                _spreadEuler.x = Random.Range(-Spread.x, Spread.x);
                _spreadEuler.y = Random.Range(-Spread.y, Spread.y);
                _spreadEuler.z = Random.Range(-Spread.z, Spread.z);
            }
            else
            {
                _spreadEuler.x = total > 1 ? Mathf.Lerp(-Spread.x, Spread.x, total == 1 ? 0.5f : (float)i / (total - 1)) : 0f;
                _spreadEuler.y = total > 1 ? Mathf.Lerp(-Spread.y, Spread.y, total == 1 ? 0.5f : (float)i / (total - 1)) : 0f;
                _spreadEuler.z = total > 1 ? Mathf.Lerp(-Spread.z, Spread.z, total == 1 ? 0.5f : (float)i / (total - 1)) : 0f;
            }

            Quaternion spreadQ = Quaternion.Euler(_spreadEuler);

            Vector3 dir;
            if (Owner != null)
                dir = (spreadQ * transform.forward).normalized;
            else
                dir = (spreadQ * (transform.rotation * DefaultProjectileDirection)).normalized;

            if (RotateWeaponOnSpread)
                transform.rotation = transform.rotation * spreadQ;

            if (proj != null)
            {
                proj.SetWeapon(this);
                if (Owner != null) proj.SetOwner(Owner.gameObject);
                proj.SetDirection(dir, transform.rotation, true);
                proj.SetLayerMask(CharacterHandleWeapon != null && CharacterHandleWeapon.UseTargetLayerMask
                    ? CharacterHandleWeapon.TargetLayerMask
                    : TargetLayerMask);
            }

            go.SetActive(true);
            if (poolable != null) poolable.TriggerOnSpawnComplete();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            DetermineSpawnPosition();
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(_spawnPos, 0.15f);
            Vector3 fwd = (Owner != null ? Owner.transform.forward : transform.forward);
            Gizmos.DrawLine(_spawnPos, _spawnPos + fwd * 1.2f);
        }
    }
}
