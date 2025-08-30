// File: /WeaponProjectile.cs
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.AI;
using OneBitRob.FX;
using UnityEngine;

namespace OneBitRob.ECS
{
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
            public float PierceChance;   // 0..1
            public int   PierceMaxTargets; // >= 0
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

        private float _pierceChance;
        private int   _pierceMaxTargets;
        private int   _piercedCount;

        private Vector3 _lastPos;

        private static readonly RaycastHit[] s_Hits = new RaycastHit[32];
        private readonly List<int> _hitEntityKeys = new List<int>(8);

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
            _critMultiplier = Mathf.Max(1f, data.CritMultiplier);

            _pierceChance   = Mathf.Clamp01(data.PierceChance);
            _pierceMaxTargets = Mathf.Max(0, data.PierceMaxTargets);
            _piercedCount   = 0;

            _hitEntityKeys.Clear();

            transform.position = data.Origin;
            transform.forward  = _dir;
            _lastPos           = transform.position;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
            _hitEntityKeys.Clear();
            _piercedCount = 0;
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

                    int key = ExtractEntityKey(h.collider);
                    if (key != 0) _hitEntityKeys.Add(key);

#if UNITY_EDITOR
                    Debug.DrawLine(_lastPos, _lastPos + _dir * h.distance, new Color(1f, 0.95f, 0.2f, 1f), 0.08f, false);
#endif
                    OnImpact(h);

                    // Decide piercing
                    bool canPierceMore = _piercedCount < _pierceMaxTargets;
                    bool rollPierce = _pierceChance > 0f && Random.value < _pierceChance;

                    if (canPierceMore && rollPierce)
                    {
                        _piercedCount++;
                        // advance slightly to avoid re-hitting same contact
                        Vector3 newPos = _lastPos + _dir * (h.distance + 0.02f);
                        transform.position = newPos;
                        _lastPos = newPos;
                        _remaining -= (h.distance + 0.02f);
                        return; // keep flying
                    }

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

                // Already pierced this entity? skip
                int key = ExtractEntityKey(col);
                if (key != 0 && _hitEntityKeys.Contains(key))
                    continue;

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

        private static int ExtractEntityKey(Collider col)
        {
            var brain = col ? col.GetComponentInParent<UnitBrain>() : null;
            if (!brain) return 0;
            var ent = UnitBrainRegistry.GetEntity(brain.gameObject);
            return ent == Unity.Entities.Entity.Null ? brain.gameObject.GetInstanceID() : (ent.Index ^ (ent.Version << 8));
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

                var rangedDef = _attackerBrain != null ? _attackerBrain.UnitDefinition?.weapon as OneBitRob.RangedWeaponDefinition : null;
                if (rangedDef != null && rangedDef.impactFeedback != null)
                {
                    FeedbackService.TryPlay(rangedDef.impactFeedback, brain.transform, hit.point);
                }
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
