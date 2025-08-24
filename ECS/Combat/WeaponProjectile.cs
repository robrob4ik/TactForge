// CHANGED: Renamed to WeaponProjectile; kept legacy alias EcsProjectile for safety.

using MoreMountains.Tools;
using OneBitRob.AI;
using OneBitRob.FX;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Minimal pooled weapon projectile (bows, etc.)
    [DisallowMultipleComponent]
    public class WeaponProjectile : MMPoolableObject
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;   // normalized
            public float Speed;
            public float Damage;
            public float MaxDistance;
            public int LayerMask;       // targets (damageable)
            public float CritChance;     // 0..1
            public float CritMultiplier; // >= 1
        }

        private GameObject _attacker;
        private UnitBrain  _attackerBrain;
        private bool       _attackerIsEnemy;

        private Vector3 _dir;
        private float _speed;
        private float _baseDamage;
        private float _remaining;
        private int _mask;

        private float _critChance;
        private float _critMultiplier;

        private Vector3 _lastPos;

        private static readonly RaycastHit[] s_Hits = new RaycastHit[32];

        public void Arm(ArmData data)
        {
            _attacker      = data.Attacker;
            _attackerBrain = _attacker ? _attacker.GetComponent<UnitBrain>() : null;
            _attackerIsEnemy = _attackerBrain && _attackerBrain.UnitDefinition
                             ? _attackerBrain.UnitDefinition.isEnemy
                             : false;

            _dir         = (data.Direction.sqrMagnitude < 1e-6f ? Vector3.forward : data.Direction.normalized);
            _speed       = Mathf.Max(0.01f, data.Speed);
            _baseDamage  = data.Damage;
            _remaining   = Mathf.Max(0.01f, data.MaxDistance);
            _mask        = data.LayerMask;

            _critChance     = Mathf.Clamp01(data.CritChance);
            _critMultiplier = Mathf.Max(1f, data.CritMultiplier <= 0f ? 1f : data.CritMultiplier);

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
            int count = Physics.RaycastNonAlloc(
                _lastPos, _dir, s_Hits, stepLen, maskToUse, QueryTriggerInteraction.Collide
            );

            if (count == 0 && maskToUse != ~0)
            {
                count = Physics.RaycastNonAlloc(
                    _lastPos, _dir, s_Hits, stepLen, ~0, QueryTriggerInteraction.Collide
                );
            }

            if (count > 0)
            {
                int best = ClosestValidEnemyHit(count);
                if (best >= 0)
                {
                    var h = s_Hits[best];
#if UNITY_EDITOR
                    Debug.DrawLine(_lastPos, _lastPos + _dir * h.distance, new Color(1f, 0.95f, 0.2f, 1f), 0.08f, false);
#endif
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

        private int ClosestValidEnemyHit(int count)
        {
            float bestDist = float.MaxValue;
            int best = -1;

            for (int i = 0; i < count; i++)
            {
                var col = s_Hits[i].collider;
                if (!col) continue;

                if (_attacker && col.transform.root == _attacker.transform.root)
                    continue;

                var brain = col.GetComponentInParent<UnitBrain>();
                if (brain == null) continue;

                bool targetIsEnemy = brain.UnitDefinition && brain.UnitDefinition.isEnemy;
                if (targetIsEnemy == _attackerIsEnemy)
                    continue;

                if (s_Hits[i].distance < bestDist)
                {
                    bestDist = s_Hits[i].distance;
                    best = i;
                }
            }

            return best;
        }

        private void OnImpact(RaycastHit hit)
        {
            var brain = hit.collider.GetComponentInParent<UnitBrain>();
            if (brain != null && brain.Health != null)
            {
                bool isCrit = _critChance > 0f && Random.value < _critChance;
                float dmg = isCrit ? _baseDamage * _critMultiplier : _baseDamage;

                Vector3 dir = _dir;
                brain.Health.Damage(dmg, _attacker, 0f, 0f, dir);

                DamageNumbersManager.Popup(new DamageNumbersParams
                {
                    Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                    Position = hit.point,
                    Follow   = brain.transform,
                    Amount   = dmg
                });
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
