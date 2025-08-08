using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Character/Damage/Enigma Kill Zone")]
    public class EnigmaKillZone : MonoBehaviour
    {
        [Title("Targets")]
        [MMInformation("This component will make your object kill objects that collide with it. Here you can define what layers will be killed.", MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
        
        [Tooltip("The layers containing the objects that will be damaged by this object")]
        public LayerMask TargetLayerMask = EnigmaLayerManager.PlayerLayerMask;

        protected EnigmaHealth _colliderHealth;
        
        protected virtual void Awake()
        {
        }
        
        protected virtual void OnEnable()
        {
        }
        
        public virtual void OnTriggerStay2D(Collider2D collider)
        {
            Colliding(collider.gameObject);
        }
        
        public virtual void OnTriggerEnter2D(Collider2D collider)
        {
            Colliding(collider.gameObject);
        }


        /// when something stays in the zone, we call our colliding endpoint
        /// <param name="collider"></param>
        public virtual void OnTriggerStay(Collider collider)
        {
            Colliding(collider.gameObject);
        }


        /// When something enters our zone, we call our colliding endpoint
        /// <param name="collider"></param>
        public virtual void OnTriggerEnter(Collider collider)
        {
            Colliding(collider.gameObject);
        }


        /// When colliding, we kill our collider if it's a Health equipped object
        /// <param name="collider"></param>
        protected virtual void Colliding(GameObject collider)
        {
            if (!this.isActiveAndEnabled)
            {
                return;
            }

            // if what we're colliding with isn't part of the target layers, we do nothing and exit
            if (!MMLayers.LayerInLayerMask(collider.layer, TargetLayerMask))
            {
                return;
            }

            _colliderHealth = collider.gameObject.MMGetComponentNoAlloc<EnigmaHealth>();

            // if what we're colliding with is damageable
            if (_colliderHealth != null)
            {
                if (_colliderHealth.CurrentHealth > 0)
                {
                    _colliderHealth.Kill();
                }
            }
        }
    }
}