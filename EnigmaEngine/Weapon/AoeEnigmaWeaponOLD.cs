using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using Random = UnityEngine.Random;

namespace OneBitRob.EnigmaEngine
{
	/// A weapon class aimed specifically at allowing the creation of various projectile weapons, from shotgun to machine gun, via plasma gun or rocket launcher
	[AddComponentMenu("Enigma Engine/Weapons/Enigma AOE Weapon")]
	public class AoeEnigmaWeaponOLD : EnigmaWeapon, MMEventListener<EnigmaEngineEvent>
	{
		[MMInspectorGroup("AOESpell", true, 22)]
		[ReadOnly]
		[Tooltip("The projectile's spawn position")]
		public Vector3 SpawnPosition = Vector3.zero;
		
		[Tooltip("The object pooler used to spawn projectiles, if left empty, this component will try to find one on its game object")]
		public MMObjectPooler ObjectPooler;

		public LayerMask groundLayerMask;


		protected Vector3 _flippedProjectileSpawnOffset;
		protected Vector3 _randomSpreadDirection;
		protected bool _poolInitialized = false;
		protected Transform _projectileSpawnTransform;
		protected int _spawnArrayIndex = 0;

		[MMInspectorButton("TestShoot")]
		/// a button to test the shoot method
		public bool TestShootButton;

		/// <summary>
		/// A test method that triggers the weapon
		/// </summary>
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

		/// <summary>
		/// Initialize this weapon
		/// </summary>
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

		/// <summary>
		/// Called everytime the weapon is used
		/// </summary>
		public override void WeaponUse()
		{
			base.WeaponUse();

			DetermineSpawnPosition();

			SpawnProjectile(SpawnPosition, true);
			
		}

		/// <summary>
		/// Spawns a new object and positions/resizes it
		/// </summary>
		public virtual GameObject SpawnProjectile(Vector3 spawnPosition, bool triggerObjectActivation = true)
		{
			/// we get the next object in the pool and make sure it's not null
			GameObject nextGameObject = ObjectPooler.GetPooledGameObject();

			// mandatory checks
			if (nextGameObject == null) { return null; }
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

			
			if (triggerObjectActivation)
			{
				if (nextGameObject.GetComponent<MMPoolableObject>() != null)
				{
					nextGameObject.GetComponent<MMPoolableObject>().TriggerOnSpawnComplete();
				}
			}
			return (nextGameObject);
		}

		/// <summary>
		/// Determines the spawn position
		/// </summary>
		public virtual void DetermineSpawnPosition()
		{
			// Cast a ray from the cursor position into the scene
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hitInfo;

			// Check if the ray hits any object on the ground layer
			if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity, groundLayerMask))
			{
				// Set the spawn position to the point where the ray hits the ground
				SpawnPosition = hitInfo.point;
			}
			else
			{
				Debug.LogWarning("No ground found for spawning projectile.");
			}
		}

		/// <summary>
		/// When the weapon is selected, draws a circle at the spawn's position
		/// </summary>
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

		/// On enable we start listening for events
		protected virtual void OnEnable()
		{
			this.MMEventStartListening<EnigmaEngineEvent>();
		}

		/// On disable we stop listening for events
		protected virtual void OnDisable()
		{
			this.MMEventStopListening<EnigmaEngineEvent>();
		}
	}
}