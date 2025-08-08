using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    /// The 3D version of the WeaponAutoAim, meant to be used on objects equipped with a WeaponAim3D.
    /// It'll detect targets within the defined radius, pick the closest, and force the WeaponAim component to aim at them if a target is found
    [RequireComponent(typeof(EnigmaWeaponAim3D))]
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Weapon Auto Aim 3D")]
    public class EnigmaWeaponAutoAim3D : EnigmaWeaponAutoAim
    {
        [Title("Overlap Detection")]
        /// the maximum amount of targets the overlap detection can acquire
        [Tooltip("The maximum amount of targets the overlap detection can acquire")]
        public int OverlapMaximum = 10;

        protected Vector3 _aimDirection;
        protected Collider[] _hits;
        protected Vector3 _raycastDirection;
        protected Collider _potentialHit;
        protected EnigmaController3D _enigmaController3D;
        protected Vector3 _origin;
        protected List<Transform> _potentialTargets;

        public Vector3 Origin
        {
            get
            {
                _origin = this.transform.position;
                if (_enigmaController3D != null)
                {
                    _origin += Quaternion.FromToRotation(Vector3.forward,
                        _enigmaController3D.CurrentDirection.normalized) * DetectionOriginOffset;
                }

                return _origin;
            }
        }


        /// On init we grab our orientation to be able to detect facing direction
        protected override void Initialization()
        {
            base.Initialization();
            _potentialTargets = new List<Transform>();
            _hits = new Collider[10];
            if (_weapon.Owner != null)
            {
                _enigmaController3D = _weapon.Owner.GetComponent<EnigmaController3D>();
            }
        }


        /// Scans for targets by performing an overlap detection, then verifying line of fire with a boxcast
        protected override bool ScanForTargets()
        {
            Target = null;

            int numberOfHits = Physics.OverlapSphereNonAlloc(Origin, ScanRadius, _hits, TargetsMask);

            if (numberOfHits == 0)
            {
                return false;
            }

            _potentialTargets.Clear();

            // we go through each collider found
            int min = Mathf.Min(OverlapMaximum, numberOfHits);
            for (int i = 0; i < min; i++)
            {
                if (_hits[i] == null)
                {
                    continue;
                }

                if ((_hits[i].gameObject == this.gameObject) || (_hits[i].transform.IsChildOf(this.transform)))
                {
                    continue;
                }

                _potentialTargets.Add(_hits[i].gameObject.transform);
            }

            // we sort our targets by distance
            _potentialTargets.Sort(delegate(Transform a, Transform b)
            {
                return Vector3.Distance(this.transform.position, a.transform.position)
                    .CompareTo(
                        Vector3.Distance(this.transform.position, b.transform.position));
            });

            // we return the first unobscured target
            foreach (Transform t in _potentialTargets)
            {
                _raycastDirection = t.position - _raycastOrigin;
                RaycastHit hit = MMDebug.Raycast3D(_raycastOrigin, _raycastDirection, _raycastDirection.magnitude,
                    ObstacleMask.value, Color.yellow, true);
                if ((hit.collider == null) && CanAcquireNewTargets())
                {
                    Target = t;
                    return true;
                }
            }

            return false;
        }


        /// Sets the aim to the relative direction of the target
        protected override void SetAim()
        {
            _aimDirection = (Target.transform.position - _raycastOrigin).normalized;
            _weaponAim.SetCurrentAim(_aimDirection, ApplyAutoAimAsLastDirection);
        }


        /// Determines the raycast origin
        protected override void DetermineRaycastOrigin()
        {
            _raycastOrigin = Origin;
        }

        protected override void OnDrawGizmos()
        {
            if (DrawDebugRadius)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Origin, ScanRadius);
            }
        }
    }
}