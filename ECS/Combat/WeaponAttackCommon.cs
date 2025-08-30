// File: Runtime/AI/Systems/WeaponAttackCommon.cs
using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    internal static class WeaponAttackCommon
    {
        public static void Consume(ref EntityCommandBuffer ecb, Entity e)
        {
            ecb.SetComponent(e, new AttackRequest { HasValue = 0, Target = Entity.Null });
        }

        public static AttackCooldown ComputeAttackCooldown(float baseCd, float jitterRange, float speedMult, Entity e, float now)
        {
            float jitter = CalcJitter(jitterRange, e, now);
            speedMult    = max(0.0001f, speedMult);
            return new AttackCooldown { NextTime = now + max(0.01f, baseCd) / speedMult + jitter };
        }

        public static float CalcJitter(float range, Entity e, float now)
        {
            if (range <= 0f) return 0f;
            uint h = math.hash(new float3(now, e.Index, e.Version));
            float u = (h / (float)uint.MaxValue) * 2f - 1f;
            return u * range;
        }

        public static MeleeHitRequest BuildMeleeHitRequest(Entity e, UnitBrain brain, in MeleeWeaponDefinition melee, in UnitRuntimeStats stats, float3 pos, float3 forward)
        {
            return new MeleeHitRequest
            {
                Origin        = pos,
                Forward       = forward,
                Range         = max(0.01f, melee.attackRange * max(0.0001f, stats.MeleeRangeMult)),
                HalfAngleRad  = math.radians(math.clamp(melee.halfAngleDeg * max(0.0001f, stats.MeleeArcMult), 0f, 179f)),
                Damage        = max(1f, melee.attackDamage),
                Invincibility = max(0f, melee.invincibility),
                LayerMask     = (UnitBrainRegistry.Get(e)?.GetDamageableLayerMask().value) ?? ~0,
                MaxTargets    = max(1, melee.maxTargets),
                CritChance    = math.clamp(melee.critChance + stats.CritChanceAdd, 0f, 1f),
                CritMultiplier= max(1f, melee.critMultiplier * stats.CritMultiplierMult),
                HasValue      = 1
            };
        }
    }
}
