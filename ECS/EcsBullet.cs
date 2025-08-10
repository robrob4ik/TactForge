// File: Assets/PROJECT/Scripts/Combat/EcsBullet.cs
using MoreMountains.Tools;
using OneBitRob.AI;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// <summary>
    /// Minimal pooled bullet:
    /// - moves straight
    /// - single swept raycast per frame
    /// - damages first UnitBrain hit, then despawns
    /// </summary>
    [DisallowMultipleComponent]
    public class EcsBullet : MMPoolableObject
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3    Origin;
            public Vector3    Direction;   // normalized
            public float      Speed;
            public float      Damage;
            public float      MaxDistance;
            public int        LayerMask;   // targets
        }

        // runtime
        private GameObject _attacker;
        private Vector3 _dir;
        private float _speed;
        private float _damage;
        private float _remaining;
        private int   _mask;
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

        private void Update()
        {
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

                // don't hit the shooter root
                if (_attacker != null && col.transform.root == _attacker.transform.root)
                    continue;

                // must hit a UnitBrain with a living health
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
                // signature used elsewhere in your codebase (melee system)
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
