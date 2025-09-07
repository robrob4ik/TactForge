using MoreMountains.Tools;
using OneBitRob.AI;
using OneBitRob.FX;
using UnityEngine;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public class SpellProjectile : MMPoolableObject
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float MaxDistance;
            public int LayerMask;
            public float Radius;
            public bool Pierce;

            // NEW: per-target feedback to play on hit
            public FeedbackDefinition HitFeedback;
        }

        private GameObject _attacker;
        private Vector3 _dir;
        private float _speed;
        private float _damage;
        private float _remaining;
        private int _mask;
        private float _radius;
        private bool _pierce;
        private FeedbackDefinition _hitFeedback; // NEW

        private Vector3 _lastPos;
        private bool _didImmediateOverlapCheck;
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private static readonly Collider[] _cols = new Collider[32];

        public void Arm(ArmData data)
        {
            _attacker   = data.Attacker;
            _dir        = (data.Direction.sqrMagnitude < 1e-6f ? Vector3.forward : data.Direction.normalized);
            _speed      = Mathf.Max(0.01f, data.Speed);
            _damage     = data.Damage;
            _remaining  = Mathf.Max(0.01f, data.MaxDistance);
            _mask       = data.LayerMask;
            _radius     = Mathf.Max(0f, data.Radius);
            _pierce     = data.Pierce;
            _hitFeedback= data.HitFeedback;   // NEW

            transform.position = data.Origin;
            transform.forward  = _dir;
            _lastPos           = transform.position;
            _didImmediateOverlapCheck = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
            _didImmediateOverlapCheck = false;
        }

        protected override void Update()
        {
            base.Update();
            if (_remaining <= 0f) { Despawn(); return; }

            if (!_didImmediateOverlapCheck)
            {
                _didImmediateOverlapCheck = true;
                if (TryImmediateOverlapHit())
                    return;
            }

            float stepLen = Mathf.Min(_speed * Time.deltaTime, _remaining);

            int maskToUse = (_mask == 0) ? ~0 : _mask;
            int count = (_radius > 0f)
                ? Physics.SphereCastNonAlloc(_lastPos, _radius, _dir, _hits, stepLen, maskToUse, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(_lastPos, _dir, _hits, stepLen, maskToUse, QueryTriggerInteraction.Collide);

            if (count == 0 && maskToUse != ~0)
            {
                count = (_radius > 0f)
                    ? Physics.SphereCastNonAlloc(_lastPos, _radius, _dir, _hits, stepLen, ~0, QueryTriggerInteraction.Collide)
                    : Physics.RaycastNonAlloc(_lastPos, _dir, _hits, stepLen, ~0, QueryTriggerInteraction.Collide);
            }

            if (count > 0)
            {
                System.Array.Sort(_hits, 0, count, RaycastHitDistanceComparer.Instance);
                for (int i = 0; i < count; i++)
                {
                    var h = _hits[i];
                    var col = h.collider;
                    if (!col) continue;
                    if (_attacker && col.transform.root == _attacker.transform.root) continue;

                    var brain = col.GetComponentInParent<UnitBrain>();
                    if (brain == null || brain.Health == null) continue;

                    Apply(brain, h.point);

                    if (!_pierce)
                    {
                        transform.position = _lastPos + _dir * h.distance;
                        Despawn();
                        return;
                    }
                }
            }

            transform.position = _lastPos + _dir * stepLen;
            _lastPos = transform.position;
            _remaining -= stepLen;
        }

        private bool TryImmediateOverlapHit()
        {
            int maskToUse = (_mask == 0) ? ~0 : _mask;
            int count = Physics.OverlapSphereNonAlloc(_lastPos, Mathf.Max(0.05f, _radius), _cols, maskToUse, QueryTriggerInteraction.Collide);
            if (count == 0 && maskToUse != ~0)
                count = Physics.OverlapSphereNonAlloc(_lastPos, Mathf.Max(0.05f, _radius), _cols, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return false;

            for (int i = 0; i < count; i++)
            {
                var col = _cols[i];
                if (!col) continue;
                if (_attacker && col.transform.root == (_attacker.transform?.root)) continue;

                var brain = col.GetComponentInParent<UnitBrain>();
                if (brain == null || brain.Health == null) continue;

                Apply(brain, _lastPos);

                if (!_pierce) { Despawn(); return true; }
            }
            return false;
        }

        private void Apply(UnitBrain brain, Vector3 point)
        {
            float invuln = 0f;
            brain.Health.Damage(_damage, _attacker, 0f, invuln, _dir);

            DamageNumbersManager.Popup(new DamageNumbersParams
            {
                Kind     = _damage < 0 ? DamagePopupKind.Heal : DamagePopupKind.Damage,
                Follow   = brain.transform,
                Position = point,
                Amount   = Mathf.Abs(_damage)
            });

            // NEW: per-target hit feedback (e.g., lightning spark + thunder)
            if (_hitFeedback != null)
            {
                // If feedback.attachToTarget == true, it will parent to the target. Otherwise it spawns at world pos.
                FeedbackService.TryPlay(_hitFeedback, brain.transform, point);
            }
        }

        private void Despawn()
        {
            var poolable = GetComponent<MMPoolableObject>();
            if (poolable != null) poolable.Destroy();
            else gameObject.SetActive(false);
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
