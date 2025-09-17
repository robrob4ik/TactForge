// File: OneBitRob/AI/SpellChainHopSystem.cs
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AIResolvePhaseGroup))]
    public partial struct SpellChainHopSystem : ISystem
    {
        ComponentLookup<LocalTransform>  _posRO;
        ComponentLookup<SpatialHashTarget> _factRO;
        ComponentLookup<SpellConfig>     _cfgRO;

        EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _posRO  = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashTarget>(true);
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

            using var ents = _q.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                ProcessRunner(ents[i], em, now, ref ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ProcessRunner(Entity caster, EntityManager em, float now, ref EntityCommandBuffer ecb)
        {
            var run = em.GetComponentData<SpellChainRunner>(caster);
            if (run.Remaining <= 0 || run.CurrentTarget == Entity.Null)
            { ecb.RemoveComponent<SpellChainRunner>(caster); return; }

            if (now < run.NextTime) return;

            var cfg = _cfgRO.HasComponent(caster) ? _cfgRO[caster] : default;
            float height = max(0.5f, cfg.MuzzleLocalOffset.y);

            GetCasterPose(caster, out float3 selfPos, out quaternion rot);

            float3 from = ComputeFromPosition(in run, in cfg, selfPos, rot);

            if (!_posRO.HasComponent(run.CurrentTarget))
            { ecb.RemoveComponent<SpellChainRunner>(caster); return; }

            float3 groundTo = _posRO[run.CurrentTarget].Position;
            float3 to       = groundTo + new float3(0, height, 0);
            float3 dir      = normalizesafe(to - from, new float3(0, 0, 1));
            float  dist     = distance(to, from);

            var req = BuildProjectileRequest(in run, from, dir, dist);

            if (!em.HasComponent<SpellProjectileSpawnRequest>(caster)) ecb.AddComponent<SpellProjectileSpawnRequest>(caster);
            ecb.SetComponent(caster, req);
            ecb.SetComponentEnabled<SpellProjectileSpawnRequest>(caster, true); // enableable trigger

            AdvanceRunner(ref run, to, now, dist, em);
            if (run.Remaining <= 0 || run.CurrentTarget == Entity.Null)
                ecb.RemoveComponent<SpellChainRunner>(caster);
            else
                ecb.SetComponent(caster, run);
        }

        private void GetCasterPose(Entity caster, out float3 pos, out quaternion rot)
        {
            pos = float3.zero; rot = quaternion.identity;
            if (_posRO.TryGetComponent(caster, out var ltCaster))
            { pos = ltCaster.Position; rot = ltCaster.Rotation; }
        }

        private static float3 ComputeFromPosition(in SpellChainRunner run, in SpellConfig cfg, float3 selfPos, quaternion rot)
        {
            if (run.HasFromPos != 0) return run.FromPos;

            float3 fwd   = normalizesafe(mul(rot, new float3(0, 0, 1)));
            float3 up    = normalizesafe(mul(rot, new float3(0, 1, 0)));
            float3 right = normalizesafe(mul(rot, new float3(1, 0, 0)));

            return selfPos
                   + fwd   * max(0f, cfg.MuzzleForward)
                   + right * cfg.MuzzleLocalOffset.x
                   + up    * cfg.MuzzleLocalOffset.y
                   + fwd   * cfg.MuzzleLocalOffset.z;
        }

        private static SpellProjectileSpawnRequest BuildProjectileRequest(in SpellChainRunner run, float3 from, float3 dir, float dist)
        {
            return new SpellProjectileSpawnRequest
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
            };
        }

        private void AdvanceRunner(ref SpellChainRunner run, float3 to, float now, float dist, EntityManager em)
        {
            run.Remaining--;
            run.PreviousTarget = run.CurrentTarget;
            run.FromPos        = to;
            run.HasFromPos     = 1;
            run.CurrentTarget  = FindNextByCasterIntent(in run);
            run.NextTime       = now + (dist / max(0.01f, run.ProjectileSpeed)) + run.JumpDelay;
        }

        private Entity FindNextByCasterIntent(in SpellChainRunner run)
        {
            byte wantFaction = (run.Positive != 0)
                ? run.CasterFaction
                : (run.CasterFaction == Constants.GameConstants.ENEMY_FACTION ? Constants.GameConstants.ALLY_FACTION : Constants.GameConstants.ENEMY_FACTION);

            var wanted = new Unity.Collections.FixedList128Bytes<byte>(); wanted.Add(wantFaction);

            if (!_posRO.HasComponent(run.PreviousTarget)) return Entity.Null;
            float3 center = _posRO[run.PreviousTarget].Position;

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
