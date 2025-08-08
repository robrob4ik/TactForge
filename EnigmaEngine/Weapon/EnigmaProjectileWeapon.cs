using UnityEngine;
using MoreMountains.Tools;
using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using Random = UnityEngine.Random;

namespace OneBitRob.EnigmaEngine
{
    public enum SpawnTransformsModes
    {
        Random,
        Sequential
    }
    
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Projectile Weapon")]
    public class EnigmaProjectileWeapon : EnigmaWeapon, MMEventListener<EnigmaEngineEvent>
    {
       [FoldoutGroup("Projectiles"), Title("Projectiles")]
        [Tooltip("The offset position at which the projectile will spawn")]
        public Vector3 ProjectileSpawnOffset = Vector3.zero;

        [FoldoutGroup("Projectiles")]
        [Tooltip("In the absence of a character owner, the default direction of the projectiles")]
        public Vector3 DefaultProjectileDirection = Vector3.forward;

        [FoldoutGroup("Projectiles")]
        [Tooltip("The number of projectiles to spawn per shot")]
        public int ProjectilesPerShot = 1;

        [FoldoutGroup("Spawn Transforms"), Title("Spawn Transforms")]
        [Tooltip("A list of transforms that can be used a spawn points, instead of the ProjectileSpawnOffset. Will be ignored if left emtpy")]
        public List<Transform> SpawnTransforms = new List<Transform>();
        
        [FoldoutGroup("Spawn Transforms")]
        [Tooltip("The selected mode for spawn transforms. Sequential will go through the list sequentially, while Random will pick a random one every shot")]
        public SpawnTransformsModes SpawnTransformsMode = SpawnTransformsModes.Sequential;

        [FoldoutGroup("Spread"), Title("Spread")]
        [Tooltip("The spread (in degrees) to apply randomly (or not) on each angle when spawning a projectile")]
        public Vector3 Spread = Vector3.zero;

        [FoldoutGroup("Spread")]
        [Tooltip("Whether or not the weapon should rotate to align with the spread angle")]
        public bool RotateWeaponOnSpread = false;

        [FoldoutGroup("Spread")]
        [Tooltip("Whether or not the spread should be random (if not it'll be equally distributed)")]
        public bool RandomSpread = true;

        [FoldoutGroup("Spread")]
        [ReadOnly]
        [Tooltip("The projectile's spawn position")]
        public Vector3 SpawnPosition = Vector3.zero;

        [FoldoutGroup("Projectiles")]
        [Tooltip("The object pooler used to spawn projectiles, if left empty, this component will try to find one on its game object")]
        public MMObjectPooler ObjectPooler;

        [FoldoutGroup("Spawn Feedbacks"), Title("Spawn Feedbacks")]
        public List<MMFeedbacks> SpawnFeedbacks = new List<MMFeedbacks>();
        
        protected Vector3 _flippedProjectileSpawnOffset;
        protected Vector3 _randomSpreadDirection;
        protected bool _poolInitialized = false;
        protected Transform _projectileSpawnTransform;
        protected int _spawnArrayIndex = 0;

        [MMInspectorButton("TestShoot")]
        public bool TestShootButton;
        
        protected virtual void TestShoot()
        {
            if (WeaponState.CurrentState == WeaponStates.WeaponIdle)
            {
                WeaponInputStart();
            }
            else
            {
                WeaponInputStop();
            }
        }
        
        public override void Initialization()
        {
            base.Initialization();
            EnigmaWeaponAim = GetComponent<EnigmaWeaponAim>();

            if (!_poolInitialized)
            {
                if (ObjectPooler == null)
                {
                    ObjectPooler = GetComponent<MMObjectPooler>();
                }

                if (ObjectPooler == null)
                {
                    Debug.LogWarning(this.name + " : no object pooler (simple or multiple) is attached to this Projectile Weapon, it won't be able to shoot anything.");
                    return;
                }

                _poolInitialized = true;
            }
        }
        
        public override void WeaponUse()
        {
            base.WeaponUse();

            DetermineSpawnPosition();

            for (int i = 0; i < ProjectilesPerShot; i++)
            {
                SpawnProjectile(SpawnPosition, i, ProjectilesPerShot, true);
                PlaySpawnFeedbacks();
            }
        }
        
        public virtual GameObject SpawnProjectile(Vector3 spawnPosition, int projectileIndex, int totalProjectiles, bool triggerObjectActivation = true)
        {
            GameObject nextGameObject = ObjectPooler.GetPooledGameObject();

            if (nextGameObject == null)
            {
                return null;
            }

            if (nextGameObject.GetComponent<MMPoolableObject>() == null)
            {
                throw new Exception(gameObject.name + " is trying to spawn objects that don't have a PoolableObject component.");
            }

            // we position the object
            nextGameObject.transform.position = spawnPosition;
            if (_projectileSpawnTransform != null)
            {
                nextGameObject.transform.position = _projectileSpawnTransform.position;
            }
            // we set its direction

            EnigmaProjectile enigmaProjectile = nextGameObject.GetComponent<EnigmaProjectile>();
            if (enigmaProjectile != null)
            {
                enigmaProjectile.SetWeapon(this);
                if (Owner != null)
                {
                    enigmaProjectile.SetOwner(Owner.gameObject);
                }
            }

            // we activate the object
            nextGameObject.gameObject.SetActive(true);

            if (enigmaProjectile != null)
            {
                if (RandomSpread)
                {
                    _randomSpreadDirection.x = UnityEngine.Random.Range(-Spread.x, Spread.x);
                    _randomSpreadDirection.y = UnityEngine.Random.Range(-Spread.y, Spread.y);
                    _randomSpreadDirection.z = UnityEngine.Random.Range(-Spread.z, Spread.z);
                }
                else
                {
                    if (totalProjectiles > 1)
                    {
                        _randomSpreadDirection.x = MMMaths.Remap(projectileIndex, 0, totalProjectiles - 1, -Spread.x, Spread.x);
                        _randomSpreadDirection.y = MMMaths.Remap(projectileIndex, 0, totalProjectiles - 1, -Spread.y, Spread.y);
                        _randomSpreadDirection.z = MMMaths.Remap(projectileIndex, 0, totalProjectiles - 1, -Spread.z, Spread.z);
                    }
                    else
                    {
                        _randomSpreadDirection = Vector3.zero;
                    }
                }

                Quaternion spread = Quaternion.Euler(_randomSpreadDirection);

                if (Owner == null)
                {
                    enigmaProjectile.SetDirection(spread * transform.rotation * DefaultProjectileDirection, transform.rotation, true);
                }
                else
                {
                    enigmaProjectile.SetDirection(spread * transform.forward, transform.rotation, true);
                }

                if (RotateWeaponOnSpread)
                {
                    this.transform.rotation = this.transform.rotation * spread;
                }
            }

            if (triggerObjectActivation)
            {
                if (nextGameObject.GetComponent<MMPoolableObject>() != null)
                {
                    nextGameObject.GetComponent<MMPoolableObject>().TriggerOnSpawnComplete();
                }
            }

            return (nextGameObject);
        }
        
        protected virtual void PlaySpawnFeedbacks()
        {
            if (SpawnFeedbacks.Count > 0)
            {
                SpawnFeedbacks[_spawnArrayIndex]?.PlayFeedbacks();
            }

            _spawnArrayIndex++;
            if (_spawnArrayIndex >= SpawnTransforms.Count)
            {
                _spawnArrayIndex = 0;
            }
        }
        
        public virtual void SetProjectileSpawnTransform(Transform newSpawnTransform)
        {
            _projectileSpawnTransform = newSpawnTransform;
        }
        
        public virtual void DetermineSpawnPosition()
        {
            SpawnPosition = this.transform.position + this.transform.rotation * ProjectileSpawnOffset;
            

            if (WeaponUseTransform != null)
            {
                SpawnPosition = WeaponUseTransform.position;
            }

            if (SpawnTransforms.Count > 0)
            {
                if (SpawnTransformsMode == SpawnTransformsModes.Random)
                {
                    _spawnArrayIndex = Random.Range(0, SpawnTransforms.Count);
                    SpawnPosition = SpawnTransforms[_spawnArrayIndex].position;
                }
                else
                {
                    SpawnPosition = SpawnTransforms[_spawnArrayIndex].position;
                }
            }
        }
        
        protected virtual void OnDrawGizmosSelected()
        {
            DetermineSpawnPosition();

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(SpawnPosition, 0.2f);
        }

        public void OnMMEvent(EnigmaEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case EnigmaEngineEventTypes.LevelStart:
                    _poolInitialized = false;
                    Initialization();
                    break;
            }
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<EnigmaEngineEvent>();
        }
        
        protected virtual void OnDisable()
        {
            this.MMEventStopListening<EnigmaEngineEvent>();
        }
    }
}