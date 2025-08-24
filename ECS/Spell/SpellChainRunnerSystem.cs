using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.ECS;

// Disambiguate types from math.float3(...) method overloads
using F3 = Unity.Mathematics.float3;
using Q  = Unity.Mathematics.quaternion;

using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellExecutionSystem))]
    public partial struct SpellChainRunnerSystem : ISystem
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<SpellConfig> _cfgRO;

        EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _posRO  = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _cfgRO  = state.GetComponentLookup<SpellConfig>(true);
            _q      = state.GetEntityQuery(ComponentType.ReadWrite<SpellChainRunner>());
            state.RequireForUpdate(_q);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);
            _cfgRO.Update(ref state);

            var em  = state.EntityManager;
            var now = (float)SystemAPI.Time.ElapsedTime;

            var ents = _q.ToEntityArray(Allocator.Temp);
            var ecb  = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var caster = ents[i];
                var run = em.GetComponentData<SpellChainRunner>(caster);

                if (run.Remaining <= 0 || run.CurrentTarget == Entity.Null)
                { ecb.RemoveComponent<SpellChainRunner>(caster); continue; }

                if (now < run.NextTime) continue;

                var cfg = _cfgRO.HasComponent(caster) ? _cfgRO[caster] : default;
                float height = max(0.5f, cfg.MuzzleLocalOffset.y); // chest-level default

                // Read LocalTransform once
                F3 selfPos;
                Q  rot;
                if (_posRO.TryGetComponent(caster, out var ltCaster))
                {
                    selfPos = ltCaster.Position;
                    rot     = ltCaster.Rotation;
                }
                else
                {
                    selfPos = F3.zero;
                    rot     = Q.identity;
                }

                // From position (carry over or recompute from muzzle)
                F3 from;
                if (run.HasFromPos != 0)
                {
                    from = run.FromPos + new F3(0, height, 0);
                }
                else
                {
                    F3 fwd   = normalizesafe(mul(rot, new F3(0, 0, 1)));
                    F3 up    = normalizesafe(mul(rot, new F3(0, 1, 0)));
                    F3 right = normalizesafe(mul(rot, new F3(1, 0, 0)));

                    from = selfPos
                         + fwd   * max(0f, cfg.MuzzleForward)
                         + right * cfg.MuzzleLocalOffset.x
                         + up    * cfg.MuzzleLocalOffset.y
                         + fwd   * cfg.MuzzleLocalOffset.z;
                }

                if (!_posRO.HasComponent(run.CurrentTarget))
                { ecb.RemoveComponent<SpellChainRunner>(caster); continue; }

                F3 to   = _posRO[run.CurrentTarget].Position + new F3(0, height, 0);
                F3 dir  = normalizesafe(to - from, new F3(0, 0, 1));
                float dist = distance(to, from);

                // Fire non-piercing projectile for this hop
                var req = new SpellProjectileSpawnRequest
                {
                    Origin      = from,
                    Direction   = dir,
                    Speed       = max(0.01f, run.ProjectileSpeed),
                    Damage      = run.Positive != 0 ? -run.Amount : run.Amount,
                    MaxDistance = max(0.1f, dist + 0.25f),
                    Radius      = 0f,
                    ProjectileIdHash = run.ProjectileIdHash,
                    LayerMask   = run.LayerMask,
                    Pierce      = 0,
                    HasValue    = 1
                };
                if (em.HasComponent<SpellProjectileSpawnRequest>(caster)) ecb.SetComponent(caster, req);
                else                                                      ecb.AddComponent(caster, req);

                // Prepare next hop
                run.Remaining--;
                run.PreviousTarget = run.CurrentTarget;
                run.FromPos        = to;  // next hop starts here (already includes height)
                run.HasFromPos     = 1;
                run.CurrentTarget  = FindNextByCasterIntent(run, ref state);
                run.NextTime       = now + (dist / max(0.01f, run.ProjectileSpeed)) + run.JumpDelay;

                if (run.Remaining <= 0 || run.CurrentTarget == Entity.Null)
                    ecb.RemoveComponent<SpellChainRunner>(caster);
                else
                    ecb.SetComponent(caster, run);
            }

            ecb.Playback(em);
            ecb.Dispose();
            ents.Dispose();
        }

        private Entity FindNextByCasterIntent(in SpellChainRunner run, ref SystemState state)
        {
            byte wantFaction = (run.Positive != 0)
                ? run.CasterFaction
                : (run.CasterFaction == OneBitRob.Constants.GameConstants.ENEMY_FACTION
                    ? OneBitRob.Constants.GameConstants.ALLY_FACTION
                    : OneBitRob.Constants.GameConstants.ENEMY_FACTION);

            var wanted = new FixedList128Bytes<byte>(); wanted.Add(wantFaction);

            if (!_posRO.HasComponent(run.PreviousTarget)) return Entity.Null;
            F3 center = _posRO[run.PreviousTarget].Position;

            using var list = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(center, run.Radius, wanted, list, ref _posRO, ref _factRO);

            Entity best = Entity.Null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == run.Caster || e == run.PreviousTarget) continue;

                float d = distance(_posRO[e].Position, center);
                if (d < bestDist) { bestDist = d; best = e; }
            }

            return best;
        }
    }
}
