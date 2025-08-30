// File: Runtime/ECS/WeaponProjectile.cs
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
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float MaxDistance;
            public int LayerMask;
            public float CritChance;
            public float CritMultiplier;
            public float PierceChance;
            public int PierceMaxTargets;
        }

        private GameObject _attacker;
        private UnitBrain _attackerBrain;
        private bool _attackerIsEnemy;

        private Vector3 _dir;
        private float _speed;
        private float _baseDamage;
        private float _remaining;
        private int _mask;

        private float _critChance;
        private float _critMultiplier;

        private float _pierceChance;
        private int _pierceMaxTargets;
        private int _piercedCount;

        private Vector3 _lastPos;

        private static readonly RaycastHit[] s_Hits = new RaycastHit[32];
        private readonly List<int> _hitEntityKeys = new List<int>(8);

        public void Arm(ArmData data)
        {
            _attacker = data.Attacker;
            _attackerBrain = _attacker ? _attacker.GetComponent<UnitBrain>() : null;
            _attackerIsEnemy = _attackerBrain && _attackerBrain.UnitDefinition
                ? _attackerBrain.UnitDefinition.isEnemy
                : false;

            _dir = (data.Direction.sqrMagnitude < 1e-6f ? Vector3.forward : data.Direction.normalized);
            _speed = Mathf.Max(0.01f, data.Speed);
            _baseDamage = data.Damage;
            _remaining = Mathf.Max(0.01f, data.MaxDistance);
            _mask = data.LayerMask;

            _critChance = Mathf.Clamp01(data.CritChance);
            _critMultiplier = Mathf.Max(1f, data.CritMultiplier);

            _pierceChance = Mathf.Clamp01(data.PierceChance);
            _pierceMaxTargets = Mathf.Max(0, data.PierceMaxTargets);
            _piercedCount = 0;

            _hitEntityKeys.Clear();

            transform.position = data.Origin;
            transform.forward = _dir;
            _lastPos = transform.position;
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
            StepOrDespawn();
        }

        private void StepOrDespawn()
        {
            if (_remaining <= 0f) { Despawn(); return; }

            float stepLen = Mathf.Min(_speed * Time.deltaTime, _remaining);

            int count = TryRaycastStep(stepLen, out RaycastHit bestHit);
            if (count > 0 && bestHit.collider != null)
            {
#if UNITY_EDITOR
                Debug.DrawLine(_lastPos, _lastPos + _dir * bestHit.distance, new Color(1f, 0.95f, 0.2f, 0.95f), 0.40f, false);
#endif
                if (!ResolveImpactAndMaybePierce(bestHit))
                {
                    // nie przebijamy – zatrzymaj na miejscu trafienia
                    Advance(bestHit.distance);
                    Despawn();
                }
                return;
            }

            // brak trafień – po prostu leć
            Advance(stepLen);
        }

        private int TryRaycastStep(float stepLen, out RaycastHit bestHit)
        {
            bestHit = default;
            int maskToUse = (_mask == 0) ? ~0 : _mask;

            int count = Physics.RaycastNonAlloc(_lastPos, _dir, s_Hits, stepLen, maskToUse, QueryTriggerInteraction.Collide);
            if (count == 0 && maskToUse != ~0)
            {
                count = Physics.RaycastNonAlloc(_lastPos, _dir, s_Hits, stepLen, ~0, QueryTriggerInteraction.Collide);
            }
            if (count <= 0) return 0;

            int best = ClosestValidEnemyHit(count);
            if (best >= 0)
            {
                bestHit = s_Hits[best];
                // zapamiętaj, że ten byt już trafiony (anti-double)
                int key = ExtractEntityKey(bestHit.collider);
                if (key != 0) _hitEntityKeys.Add(key);
                return count;
            }

            return 0;
        }

        private bool ResolveImpactAndMaybePierce(RaycastHit hit)
        {
            OnImpact(hit);

            bool canPierceMore = _piercedCount < _pierceMaxTargets;
            bool rollPierce = _pierceChance > 0f && Random.value < _pierceChance;
            if (canPierceMore && rollPierce)
            {
                _piercedCount++;
                // przesuwamy troszkę dalej, aby nie uderzyć w ten sam collider
                float advance = hit.distance + 0.02f;
                Advance(advance);
                return true; // kontynuuj lot
            }
            return false; // nie przebijamy
        }

        private void Advance(float distance)
        {
            Vector3 newPos = _lastPos + _dir * distance;
            transform.position = newPos;
            _lastPos = newPos;
            _remaining -= distance;
        }

        private int ClosestValidEnemyHit(int count)
        {
            float bestDist = float.MaxValue;
            int best = -1;

            for (int i = 0; i < count; i++)
            {
                var col = s_Hits[i].collider;
                if (!col) continue;

                if (_attacker && col.transform.root == _attacker.transform.root) continue;

                var brain = col.GetComponentInParent<UnitBrain>();
                if (brain == null) continue;

                int key = ExtractEntityKey(col);
                if (key != 0 && _hitEntityKeys.Contains(key)) continue;

                bool targetIsEnemy = brain.UnitDefinition && brain.UnitDefinition.isEnemy;
                if (targetIsEnemy == _attackerIsEnemy) continue;

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

                DamageNumbersManager.Popup(
                    new DamageNumbersParams
                    {
                        Kind = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                        Position = hit.point,
                        Follow = brain.transform,
                        Amount = dmg
                    }
                );

                var rangedDef = _attackerBrain != null ? _attackerBrain.UnitDefinition?.weapon as OneBitRob.RangedWeaponDefinition : null;
                if (rangedDef != null && rangedDef.impactFeedback != null) { FeedbackService.TryPlay(rangedDef.impactFeedback, brain.transform, hit.point); }
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
