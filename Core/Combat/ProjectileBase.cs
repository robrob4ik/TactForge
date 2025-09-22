// File: OneBitRob/VFX/ProjectileBase.cs

using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using OneBitRob.AI;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public abstract class ProjectileBase : MMPoolableObject
    {
        protected GameObject _attacker;
        protected UnitBrain _attackerBrain;
        protected bool _attackerIsEnemy;

        protected Vector3 _dir;
        protected float _speed;
        protected float _remaining;
        protected int _mask;
        protected float _radius;

        protected Vector3 _lastPos;
        protected bool _didImmediateOverlapCheck;

        private static readonly RaycastHit[] s_Hits = new RaycastHit[32];
        private static readonly Collider[] s_Overlap = new Collider[32];

        private readonly List<int> _hitEntityKeys = new List<int>(8);

        protected void ArmBase(GameObject attacker, Vector3 origin, Vector3 direction, float speed, float maxDistance, int layerMask, float radius = 0f)
        {
            _attacker = attacker;
            _attackerBrain = _attacker ? _attacker.GetComponent<UnitBrain>() : null;
            _attackerIsEnemy = _attackerBrain && _attackerBrain.UnitDefinition && _attackerBrain.UnitDefinition.isEnemy;

            _dir = (direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized);
            _speed = Mathf.Max(0.01f, speed);
            _remaining = Mathf.Max(0.01f, maxDistance);
            _mask = layerMask;
            _radius = Mathf.Max(0f, radius);

            _hitEntityKeys.Clear();

            transform.position = origin;
            transform.forward = _dir;
            _lastPos = transform.position;
            _didImmediateOverlapCheck = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastPos = transform.position;
            _hitEntityKeys.Clear();
            _didImmediateOverlapCheck = false;
        }

        protected override void Update()
        {
            base.Update();
            StepOrDespawn();
        }

        private void StepOrDespawn()
        {
            if (_remaining <= 0f)
            {
                Despawn();
                return;
            }

            if (!_didImmediateOverlapCheck)
            {
                _didImmediateOverlapCheck = true;
                if (TryImmediateOverlapHit()) return;
            }

            float stepLen = Mathf.Min(_speed * Time.deltaTime, _remaining);
            int count = TryRayOrSphereCast(stepLen, out RaycastHit bestHit);

            if (count > 0 && bestHit.collider != null)
            {
#if UNITY_EDITOR
                Debug.DrawLine(_lastPos, _lastPos + _dir * bestHit.distance, new Color(1f, 0.95f, 0.2f, 0.95f), 0.40f, false);
#endif
                if (!ResolveImpactAndMaybeContinue(bestHit))
                {
                    Advance(bestHit.distance);
                    Despawn();
                }

                return;
            }

            Advance(stepLen);
        }

        private bool TryImmediateOverlapHit()
        {
            int maskToUse = (_mask == 0) ? ~0 : _mask;
            int count = Physics.OverlapSphereNonAlloc(_lastPos, Mathf.Max(0.05f, _radius), s_Overlap, maskToUse, QueryTriggerInteraction.Collide);
            if (count == 0 && maskToUse != ~0) count = Physics.OverlapSphereNonAlloc(_lastPos, Mathf.Max(0.05f, _radius), s_Overlap, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return false;

            for (int i = 0; i < count; i++)
            {
                var col = s_Overlap[i];
                if (!col) continue;
                if (_attacker && col.transform.root == (_attacker.transform?.root)) continue;

                var brain = col.GetComponentInParent<UnitBrain>();
                if (brain == null || brain.Health == null) continue;

                if (!ApplyOnHit(brain, _lastPos))
                {
                    Despawn();
                    return true;
                }
            }

            return false;
        }

        private int TryRayOrSphereCast(float stepLen, out RaycastHit bestHit)
        {
            bestHit = default;
            int maskToUse = (_mask == 0) ? ~0 : _mask;

            int count = (_radius > 0f)
                ? Physics.SphereCastNonAlloc(_lastPos, _radius, _dir, s_Hits, stepLen, maskToUse, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(_lastPos, _dir, s_Hits, stepLen, maskToUse, QueryTriggerInteraction.Collide);

            if (count == 0 && maskToUse != ~0)
            {
                count = (_radius > 0f)
                    ? Physics.SphereCastNonAlloc(_lastPos, _radius, _dir, s_Hits, stepLen, ~0, QueryTriggerInteraction.Collide)
                    : Physics.RaycastNonAlloc(_lastPos, _dir, s_Hits, stepLen, ~0, QueryTriggerInteraction.Collide);
            }

            if (count <= 0) return 0;

            int best = ClosestValidEnemyHit(count);
            if (best >= 0)
            {
                bestHit = s_Hits[best];
                int key = ExtractEntityKey(bestHit.collider);
                if (key != 0) _hitEntityKeys.Add(key);
                return count;
            }

            return 0;
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

        private void Advance(float distance)
        {
            Vector3 newPos = _lastPos + _dir * distance;
            transform.position = newPos;
            _lastPos = newPos;
            _remaining -= distance;
        }

        private bool ResolveImpactAndMaybeContinue(RaycastHit hit)
        {
            var brain = hit.collider.GetComponentInParent<UnitBrain>();
            if (brain != null)
            {
                bool continueFlying = ApplyOnHit(brain, hit.point);
                if (continueFlying)
                {
                    // tiny step ahead to avoid immediate re-hit
                    Advance(hit.distance + 0.02f);
                    return true;
                }
            }

            return false;
        }

        protected static int ExtractEntityKey(Collider col)
        {
            var brain = col ? col.GetComponentInParent<UnitBrain>() : null;
            if (!brain) return 0;
            var ent = UnitBrainRegistry.GetEntity(brain.gameObject);
            return ent == Unity.Entities.Entity.Null ? brain.gameObject.GetInstanceID() : (ent.Index ^ (ent.Version << 8));
        }

        protected void Despawn()
        {
            var poolable = GetComponent<MMPoolableObject>();
            if (poolable != null)
                poolable.Destroy();
            else
                gameObject.SetActive(false);
        }

        protected abstract bool ApplyOnHit(UnitBrain targetBrain, Vector3 point);
    }
}