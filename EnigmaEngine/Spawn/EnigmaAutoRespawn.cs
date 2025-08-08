using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace OneBitRob.EnigmaEngine
{
    /// Add this script to an object and it will automatically be reactivated and revived when the player respawns.
    [AddComponentMenu("Enigma Engine/Spawn/Enigma Auto Respawn")]
    public class EnigmaAutoRespawn : MonoBehaviour, EnigmaRespawnable
    {
        [Title("Respawn when the player respawns")]
        /// if this is true, this object will respawn at its last position when the player revives
        [Tooltip("If this is true, this object will respawn at its last position when the player revives")]
        public bool RespawnOnPlayerRespawn = true;

        /// if this is true, this object will be repositioned at its initial position when the player revives
        [Tooltip("If this is true, this object will be repositioned at its initial position when the player revives")]
        public bool RepositionToInitOnPlayerRespawn = false;

        /// if this is true, all components on this object will be disabled on kill
        [Tooltip("If this is true, all components on this object will be disabled on kill")]
        public bool DisableAllComponentsOnKill = false;

        /// if this is true, this gameobject will be disabled on kill
        [Tooltip("If this is true, this gameobject will be disabled on kill")]
        public bool DisableGameObjectOnKill = true;

        [Title("Checkpoints")]
        [Tooltip("If this is true, the object will always respawn, whether or not it's associated to a checkpoint")]
        public bool IgnoreCheckpointsAlwaysRespawn = true;

        [Tooltip("If the player respawns at these checkpoints, the object will be respawned")]
        public List<EnigmaCheckPoint> AssociatedCheckpoints;

        [Title("Auto respawn after X seconds")]
        [Tooltip("If this has a value superior to 0, this object will respawn at its last position X seconds after its death")]
        public float AutoRespawnDuration = 0f;

        [Tooltip("The amount of times this object can auto respawn, negative value : infinite")]
        public int AutoRespawnAmount = 3;

        [Tooltip("The remaining amounts of respawns (readonly, controlled by the class at runtime)")] [ReadOnly]
        public int AutoRespawnRemainingAmount = 3;

        [Tooltip("the effect to instantiate when the player respawns")]
        public GameObject RespawnEffect;

        [Tooltip("the sfx to play when the player respawns")]
        public AudioClip RespawnSfx;

        [FormerlySerializedAs("OnRespawn")]
        [Title("Events")]
        /// a Unity Event to trigger when respawning
        [Tooltip("A Unity Event to trigger when respawning")]
        public UnityEvent OnReviveEvent;

        // respawn
        public delegate void OnReviveDelegate();

        public OnReviveDelegate OnRevive;

        protected MonoBehaviour[] _otherComponents;
        protected Collider2D _collider2D;
        protected Renderer _renderer;
        protected EnigmaCharacter _character;
        protected EnigmaHealth _health;
        protected bool _reviving = false;
        protected float _timeOfDeath = 0f;
        protected bool _firstRespawn = true;
        protected Vector3 _initialPosition;
        protected AIBrain _aiBrain;
        
        /// On Start we grab our various components
        protected virtual void Start()
        {
            AutoRespawnRemainingAmount = AutoRespawnAmount;
            _otherComponents = this.gameObject.GetComponents<MonoBehaviour>();
            _collider2D = this.gameObject.GetComponent<Collider2D>();
            _renderer = this.gameObject.GetComponent<Renderer>();
            _character = this.gameObject.GetComponent<EnigmaCharacter>();
            if (_character != null)
            {
                _health = _character.CharacterHealth;
            }

            _initialPosition = this.transform.position;
        }
        
        /// When the player respawns, we reinstate this agent.
        public virtual void OnPlayerRespawn(EnigmaCheckPoint checkpoint, EnigmaCharacter player)
        {
            if (RepositionToInitOnPlayerRespawn)
            {
                this.transform.position = _initialPosition;
            }

            if (RespawnOnPlayerRespawn)
            {
                if (_health != null)
                {
                    _health.Revive();
                }

                Revive();
            }

            AutoRespawnRemainingAmount = AutoRespawnAmount;
        }

        /// On Update we check whether we should be reviving this agent
        protected virtual void Update()
        {
            if (_reviving)
            {
                if (_timeOfDeath + AutoRespawnDuration < Time.time)
                {
                    if (AutoRespawnAmount == 0)
                    {
                        return;
                    }

                    if (AutoRespawnAmount > 0)
                    {
                        if (AutoRespawnRemainingAmount <= 0)
                        {
                            return;
                        }

                        AutoRespawnRemainingAmount -= 1;
                    }

                    Revive();
                    _reviving = false;
                }
            }
        }
        
        /// Kills this object, turning its parts off based on the settings set in the inspector
        public virtual void Kill()
        {
            if (AutoRespawnDuration <= 0f)
            {
                // object is turned inactive to be able to reinstate it at respawn
                if (DisableGameObjectOnKill)
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                if (DisableAllComponentsOnKill)
                {
                    foreach (MonoBehaviour component in _otherComponents)
                    {
                        if (component != this)
                        {
                            component.enabled = false;
                        }
                    }
                }

                if (_collider2D != null)
                {
                    _collider2D.enabled = false;
                }

                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }

                _reviving = true;
                _timeOfDeath = Time.time;
            }
        }

        /// Revives this object, turning its parts back on again
        public virtual void Revive()
        {
            if (AutoRespawnDuration <= 0f)
            {
                // object is turned inactive to be able to reinstate it at respawn
                gameObject.SetActive(true);
            }
            else
            {
                if (DisableAllComponentsOnKill)
                {
                    foreach (MonoBehaviour component in _otherComponents)
                    {
                        component.enabled = true;
                    }
                }

                if (_collider2D != null)
                {
                    _collider2D.enabled = true;
                }

                if (_renderer != null)
                {
                    _renderer.enabled = true;
                }

                InstantiateRespawnEffect();
                PlayRespawnSound();
            }

            if (_health != null)
            {
                _health.Revive();
            }

            if (_aiBrain != null)
            {
                _aiBrain.ResetBrain();
            }

            OnRevive?.Invoke();
            if (OnReviveEvent != null)
            {
                OnReviveEvent.Invoke();
            }
        }

        /// Instantiates the respawn effect at the object's position
        protected virtual void InstantiateRespawnEffect()
        {
            // instantiates the destroy effect
            if (RespawnEffect != null)
            {
                GameObject instantiatedEffect = (GameObject)Instantiate(RespawnEffect, transform.position, transform.rotation);
                instantiatedEffect.transform.localScale = transform.localScale;
            }
        }
        
        /// Plays the respawn sound.
        protected virtual void PlayRespawnSound()
        {
            if (RespawnSfx != null)
            {
                MMSoundManagerSoundPlayEvent.Trigger(RespawnSfx, MMSoundManager.MMSoundManagerTracks.Sfx, this.transform.position);
            }
        }
    }
}