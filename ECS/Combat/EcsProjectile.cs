// FILE: OneBitRob/ECS/EcsProjectile.cs
// Changes applied:
// - Update() is now 'protected override' instead of 'private' to avoid CS0114 hiding warning.
// - Calls base.Update() to preserve MMPoolableObject behavior.

using OneBitRob.AI;

namespace OneBitRob.ECS
{
    using MoreMountains.Tools;
    using UnityEngine;

    /// <summary>
    /// Minimal pooled projectile:
    /// - moves straight
    /// - single swept raycast per frame
    /// - damages first UnitBrain hit, then despawns
    /// </summary>
    [DisallowMultipleComponent]
    public class EcsProjectile : MMPoolableObject
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;   // normalized
            public float Speed;
            public float Damage;
            public float MaxDistance;
            public int LayerMask;       // targets
        }

        private GameObject _attacker;
        private Vector3 _dir;
        private float _speed;
        private float _damage;
        private float _remaining;
        private int _mask;
        private Vector3 _lastPos;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public void Arm(ArmData data)
        {
            _attacker   = data.Attacker;
            _dir        = (data.Direction.sqrMagnitude < 1e-6f ? Vector3.forward : data.Direction.normalized);
            _speed      = data.Speed;
            _damage     = data.Damage;
            _remaining  = data.MaxDistance;
            _mask       = data.LayerMask;

            transform.position = data.Origin;
            transform.forward  = _dir;
            _lastPos           = transform.position;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
        }

        // ← this was 'private void Update()' before; now it's an override
        protected override void Update()
        {
            base.Update(); // keep any base-class logic (e.g., auto-despawn timers)

            if (_remaining <= 0f) { Despawn(); return; }

            float stepLen = Mathf.Min(_speed * Time.deltaTime, _remaining);
            int count = Physics.RaycastNonAlloc(_lastPos, _dir, _hits, stepLen, _mask, QueryTriggerInteraction.Ignore);

            if (count > 0)
            {
                int best = ClosestValidHit(count);
                if (best >= 0)
                {
                    var h = _hits[best];
                    OnImpact(h);
                    transform.position = _lastPos + _dir * h.distance;
                    Despawn();
                    return;
                }
            }

            transform.position = _lastPos + _dir * stepLen;
            _lastPos = transform.position;
            _remaining -= stepLen;
        }

        private int ClosestValidHit(int count)
        {
            float bestDist = float.MaxValue;
            int best = -1;

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i].collider;
                if (!col) continue;

                if (_attacker != null && col.transform.root == _attacker.transform.root)
                    continue;

                var brain = col.GetComponentInParent<UnitBrain>();
                if (brain == null || brain.Health == null || !brain.IsTargetAlive())
                    continue;

                if (_hits[i].distance < bestDist)
                {
                    bestDist = _hits[i].distance;
                    best = i;
                }
            }

            return best;
        }

        private void OnImpact(RaycastHit hit)
        {
            var brain = hit.collider.GetComponentInParent<UnitBrain>();
            if (brain != null && brain.Health != null && brain.Health.CanTakeDamageThisFrame())
            {
                Vector3 dir = _dir;
                brain.Health.Damage(_damage, _attacker, 0f, 0f, dir);
            }
        }

        private void Despawn()
        {
            var poolable = GetComponent<MMPoolableObject>();
            if (poolable != null) poolable.Destroy();
            else gameObject.SetActive(false);
        }
    }
}
