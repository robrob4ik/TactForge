using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Spell projectile that can be a ray (radius=0) or thick cylinder, and optional piercing.
    [DisallowMultipleComponent]
    public class EcsSpellProjectile : MMPoolableObject
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Damage;        // negative => heal
            public float MaxDistance;
            public int LayerMask;
            public float Radius;
            public bool Pierce;
        }

        private GameObject _attacker;
        private Vector3 _dir;
        private float _speed;
        private float _damage;
        private float _remaining;
        private int _mask;
        private float _radius;
        private bool _pierce;

        private Vector3 _lastPos;
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        public void Arm(ArmData data)
        {
            _attacker   = data.Attacker;
            _dir        = (data.Direction.sqrMagnitude < 1e-6f ? Vector3.forward : data.Direction.normalized);
            _speed      = data.Speed;
            _damage     = data.Damage;
            _remaining  = data.MaxDistance;
            _mask       = data.LayerMask;
            _radius     = Mathf.Max(0f, data.Radius);
            _pierce     = data.Pierce;

            transform.position = data.Origin;
            transform.forward  = _dir;
            _lastPos           = transform.position;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
        }

        protected override void Update()
        {
            base.Update();
            if (_remaining <= 0f) { Despawn(); return; }

            float stepLen = Mathf.Min(_speed * Time.deltaTime, _remaining);

            int maskToUse = (_mask == 0) ? ~0 : _mask;
            int count = _radius > 0f
                ? Physics.SphereCastNonAlloc(_lastPos, _radius, _dir, _hits, stepLen, maskToUse, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(_lastPos, _dir, _hits, stepLen, maskToUse, QueryTriggerInteraction.Collide);

            if (count > 0)
            {
                System.Array.Sort(_hits, 0, count, RaycastHitDistanceComparer.Instance);
                for (int i = 0; i < count; i++)
                {
                    var h = _hits[i];
                    var col = h.collider;
                    if (!col) continue;
                    if (_attacker && col.transform.root == _attacker.transform.root) continue;

                    var brain = col.GetComponentInParent<OneBitRob.AI.UnitBrain>();
                    if (brain == null || brain.Health == null) continue;

                    Apply(brain, h);

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

        private void Apply(OneBitRob.AI.UnitBrain brain, RaycastHit h)
        {
            float invuln = 0f;
            brain.Health.Damage(_damage, _attacker, 0f, invuln, _dir);

            // Popups: negative damage => heal
            OneBitRob.FX.DamageNumbersManager.Popup(new OneBitRob.FX.DamageNumbersParams
            {
                Kind     = _damage < 0 ? OneBitRob.FX.DamagePopupKind.Heal : OneBitRob.FX.DamagePopupKind.Damage,
                Follow   = brain.transform,
                Position = h.point,
                Amount   = Mathf.Abs(_damage)
            });
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
