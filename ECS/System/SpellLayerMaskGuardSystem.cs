// Assets/PROJECT/Scripts/Runtime/AI/Combat/Spell/SpellLayerMaskGuardSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using OneBitRob.AI;
using OneBitRob.AI.Debugging;
using OneBitRob.Config;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    /// <summary>
    /// Ensures every SpellConfig has a valid TargetLayerMask derived from caster faction and spell polarity.
    /// Also warns about odd AoE radius values.
    /// </summary>
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(SpellWindupAndFireSystem))]
    public partial struct SpellLayerMaskGuardSystem : ISystem
    {
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<SpellConfig> _cfgRW;

        public void OnCreate(ref SystemState state)
        {
            _factRO = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _cfgRW  = state.GetComponentLookup<SpellConfig>(false);

            state.RequireForUpdate(state.GetEntityQuery(
                ComponentType.ReadOnly<SpellConfig>(),
                ComponentType.ReadOnly<SpatialHashComponents.SpatialHashTarget>()));
        }

        public void OnUpdate(ref SystemState state)
        {
            _factRO.Update(ref state);
            _cfgRW.Update(ref state);

            foreach (var (cfgRO, e) in SystemAPI.Query<RefRO<SpellConfig>>().WithAll<SpatialHashComponents.SpatialHashTarget>().WithEntityAccess())
            {
                var cfg = cfgRO.ValueRO;
                bool changed = false;

                if (cfg.TargetLayerMask == 0 && _factRO.HasComponent(e))
                {
                    var faction = _factRO[e].Faction;
                    bool casterIsEnemy = faction == Constants.GameConstants.ENEMY_FACTION;

                    int mask =
                        cfg.EffectType == SpellEffectType.Positive
                            ? (casterIsEnemy
                                ? CombatLayers.FriendlyLayerMaskFor(true).value
                                : CombatLayers.FriendlyLayerMaskFor(false).value)
                            : (casterIsEnemy
                                ? CombatLayers.DamageableLayerMaskFor(true).value
                                : CombatLayers.DamageableLayerMaskFor(false).value);

                    cfg.TargetLayerMask = mask;
                    changed = true;

                    if (SpellDebug.Verbose)
                        Debug.Log($"[Spell] Filled TargetLayerMask={mask} for {e.Index}:{e.Version}");
                }

                if (cfg.Kind == SpellKind.EffectOverTimeArea && (cfg.AreaRadius <= 0f || cfg.AreaRadius > cfg.Range * 4f))
                {
                    SpellDebug.LogWarnThrottled(
                        $"aoe-radius-{e.Index}",
                        $"[Spell] AOE AreaRadius looks odd (AreaRadius={cfg.AreaRadius}, CastRange={cfg.Range}). Ensure SpellDefinition.AreaRadius is set.");
                }

                if (changed) _cfgRW[e] = cfg;
            }
        }
    }
}
