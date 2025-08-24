// Assets/PROJECT/Scripts/Runtime/AI/Combat/Spell/SpellChainHopSystem.cs

using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

namespace OneBitRob.AI
{
    /// <summary>
    /// Processes chain spells hop-by-hop by spawning a short projectile to the next target and scheduling the next hop.
    /// </summary>
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct SpellChainHopSystem : ISystem
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
                float height = max(0.5f, cfg.MuzzleLocalOffset.y);

                float3 selfPos = float3.zero;
                quaternion rot = quaternion.identity;
                if (_posRO.TryGetComponent(caster, out var ltCaster))
                {
                    selfPos = ltCaster.Position;
                    rot     = ltCaster.Rotation;
                }

                float3 from;
                if (run.HasFromPos != 0) from = run.FromPos;
                else
                {
                    float3 fwd   = normalizesafe(mul(rot, new float3(0, 0, 1)));
                    float3 up    = normalizesafe(mul(rot, new float3(0, 1, 0)));
                    float3 right = normalizesafe(mul(rot, new float3(1, 0, 0)));
                    from = selfPos
                           + fwd   * max(0f, cfg.MuzzleForward)
                           + right * cfg.MuzzleLocalOffset.x
                           + up    * cfg.MuzzleLocalOffset.y
                           + fwd   * cfg.MuzzleLocalOffset.z;
                }

                if (!_posRO.HasComponent(run.CurrentTarget))
                { ecb.RemoveComponent<SpellChainRunner>(caster); continue; }

                float3 groundTo = _posRO[run.CurrentTarget].Position;
                float3 to       = groundTo + new float3(0, height, 0);
                float3 dir      = normalizesafe(to - from, new float3(0, 0, 1));
                float  dist     = distance(to, from);

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

                run.Remaining--;
                run.PreviousTarget = run.CurrentTarget;
                run.FromPos        = to;
                run.HasFromPos     = 1;
                run.CurrentTarget  = FindNextByCasterIntent(in run);
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

        private Entity FindNextByCasterIntent(in SpellChainRunner run)
        {
            byte wantFaction = (run.Positive != 0)
                ? run.CasterFaction
                : (run.CasterFaction == OneBitRob.Constants.GameConstants.ENEMY_FACTION
                    ? OneBitRob.Constants.GameConstants.ALLY_FACTION
                    : OneBitRob.Constants.GameConstants.ENEMY_FACTION);

            var wanted = new FixedList128Bytes<byte>(); wanted.Add(wantFaction);

            if (!_posRO.HasComponent(run.PreviousTarget)) return Entity.Null;
            float3 center = _posRO[run.PreviousTarget].Position;

            using var list = new NativeList<Entity>(Allocator.Temp);
            OneBitRob.ECS.SpatialHashSearch.CollectInSphere(center, run.Radius, wanted, list, ref _posRO, ref _factRO);

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
