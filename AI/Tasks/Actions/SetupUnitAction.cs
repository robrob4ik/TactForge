using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Basic setup of EnigmaEngine character")]
    public class SetupUnitAction : AbstractTaskAction<SetupUnitComponent, SetupUnitTag, SetupUnitSystem>, IAction
    {
        protected override SetupUnitComponent CreateBufferElement(ushort runtimeIndex) { return new SetupUnitComponent { Index = runtimeIndex }; }
    }

    public struct SetupUnitComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct SetupUnitTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(MoveToTargetSystem))] // ensure definition-driven setup is available to AI tasks
    public partial class SetupUnitSystem : TaskProcessorSystem<SetupUnitComponent, SetupUnitTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            // Keep existing Mono setup
            brain.Setup();

            var em = EntityManager;

            // ──────────────────────────────────────────────────────
            // Health from UnitDefinition (fallback 100)
            int hp = 100;
            var def = brain != null ? brain.UnitDefinition : null;
            if (def != null) hp = def.health;

            if (em.HasComponent<HealthMirror>(e))
            {
                var hm = em.GetComponentData<HealthMirror>(e);
                hm.Current = hp;
                hm.Max = hp;
                em.SetComponentData(e, hm);
            }
            else { em.AddComponentData(e, new HealthMirror { Current = hp, Max = hp }); }

            // ──────────────────────────────────────────────────────
            // Combat style from weapon type (1 = melee, 2 = ranged)
            byte style = 1;
            var weapon = def != null ? def.weapon : null;
            if (weapon is RangedWeaponDefinition) style = 2;

            if (em.HasComponent<CombatStyle>(e))
                em.SetComponentData(e, new CombatStyle { Value = style });
            else
                em.AddComponentData(e, new CombatStyle { Value = style });

            // ──────────────────────────────────────────────────────
            // Retarget helpers (assist + optional cooldown presence)
            float3 spawnPos = em.HasComponent<LocalTransform>(e) ? em.GetComponentData<LocalTransform>(e).Position : float3.zero;

            if (em.HasComponent<RetargetAssist>(e))
            {
                var ra = em.GetComponentData<RetargetAssist>(e);
                ra.LastPos = spawnPos;
                ra.LastDistSq = float.MaxValue;
                ra.NoProgressTime = 0f;
                em.SetComponentData(e, ra);
            }
            else
            {
                em.AddComponentData(
                    e, new RetargetAssist
                    {
                        LastPos = spawnPos,
                        LastDistSq = float.MaxValue,
                        NoProgressTime = 0f
                    }
                );
            }

            if (!em.HasComponent<RetargetCooldown>(e)) em.AddComponentData(e, new RetargetCooldown { NextTime = 0 });

            // ──────────────────────────────────────────────────────
            // Spells: configure only if the unit actually has a first spell
            var spells = def != null ? def.unitSpells : null;
            bool hasSpell = spells != null && spells.Count > 0 && spells[0] != null;

            if (hasSpell)
            {
                var spell = spells[0];

                // Visual registry hashes (no GPUI here)
                int projHash = SpellVisualRegistry.RegisterProjectile(spell.ProjectileId);
                int effectHash = SpellVisualRegistry.RegisterVfx(spell.EffectVfxId);
                int areaVfxHash = SpellVisualRegistry.RegisterVfx(spell.AreaVfxId);
                int summonHash = SpellVisualRegistry.RegisterSummon(spell.SummonPrefab);

                float amount = (spell.Kind == SpellKind.EffectOverTimeArea || spell.Kind == SpellKind.EffectOverTimeTarget)
                    ? spell.TickAmount
                    : spell.EffectAmount;

                var config = new SpellConfig
                {
                    Kind = spell.Kind,
                    EffectType = spell.EffectType,
                    AcquireMode = spell.AcquireMode,

                    CastTime = spell.FireDelaySeconds,
                    Cooldown = spell.Cooldown,
                    Range = spell.Range,
                    RequiresLineOfSight = 0,
                    TargetLayerMask = 0,

                    RequireFacing = 0,
                    FaceToleranceDeg = 0f,
                    MaxExtraFaceDelay = 0f,

                    Amount = amount,

                    ProjectileSpeed = spell.ProjectileSpeed,
                    ProjectileMaxDistance = spell.ProjectileMaxDistance,
                    ProjectileRadius = spell.ProjectileRadius,
                    ProjectileIdHash = projHash,
                    MuzzleForward = spell.MuzzleForward,
                    MuzzleLocalOffset = new float3(spell.MuzzleLocalOffset.x, spell.MuzzleLocalOffset.y, spell.MuzzleLocalOffset.z),

                    // AOE DAMAGE RADIUS must come from SpellDefinition.AreaRadius (NOT Range)
                    AreaRadius = spell.Kind == SpellKind.EffectOverTimeArea ? spell.AreaRadius : 0f,
                    Duration = spell.Duration,
                    TickInterval = spell.TickInterval,
                    EffectVfxIdHash = effectHash,
                    AreaVfxIdHash = areaVfxHash,
                    AreaVfxYOffset = spell.AreaVfxYOffset,

                    ChainMaxTargets = spell.ChainMaxTargets,
                    ChainRadius = spell.ChainRadius,
                    ChainJumpDelay = spell.ChainPerJumpDelay,

                    SummonPrefabHash = summonHash
                };

                if (em.HasComponent<SpellConfig>(e))
                    em.SetComponentData(e, config);
                else
                    em.AddComponentData(e, config);

                if (!em.HasComponent<SpellDecisionRequest>(e)) em.AddComponentData(e, new SpellDecisionRequest { HasValue = 0 });

                if (!em.HasComponent<SpellWindup>(e)) em.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });

                if (!em.HasComponent<SpellCooldown>(e)) em.AddComponentData(e, new SpellCooldown { NextTime = 0f });

                // Make sure SpellState (shell from spawner) is valid/enabled
                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);
                    if (ss.CanCast == 0 || ss.Ready == 0)
                    {
                        ss.CanCast = 1;
                        ss.Ready = 1;
                        em.SetComponentData(e, ss);
                    }
                }
            }

            return TaskStatus.Success;
        }
    }
}