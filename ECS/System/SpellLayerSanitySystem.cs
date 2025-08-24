// Runtime/AI/Systems/SpellLayerSanitySystem.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using OneBitRob.AI.Debugging;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    /// Ensures spell layer masks are never 0 and are consistent with caster faction & spell polarity.
    /// Also sanity-checks AoE radius vs cast range and logs helpful warnings (throttled).
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(SpellExecutionSystem))]
    public partial struct SpellLayerSanitySystem : ISystem
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

                // Fill TargetLayerMask if not set from authoring
                if (cfg.TargetLayerMask == 0 && _factRO.HasComponent(e))
                {
                    var faction = _factRO[e].Faction;
                    bool casterIsEnemy = faction == Constants.GameConstants.ENEMY_FACTION;

                    int mask =
                        cfg.EffectType == SpellEffectType.Positive
                            ? (casterIsEnemy
                                ? Config.CombatLayers.FriendlyLayerMaskFor(true).value     // enemy heals enemies
                                : Config.CombatLayers.FriendlyLayerMaskFor(false).value)   // ally heals allies
                            : (casterIsEnemy
                                ? Config.CombatLayers.DamageableLayerMaskFor(true).value   // enemy damages allies
                                : Config.CombatLayers.DamageableLayerMaskFor(false).value);// ally damages enemies

                    cfg.TargetLayerMask = mask;
                    changed = true;

                    if (SpellDebug.WarnOn)
                        SpellDebug.LogVerbose($"[Spell] Filled TargetLayerMask={mask} for {e.Index}:{e.Version}", null);
                }

                // Soft guard: AOE spells should use AreaRadius (not Range) to size the DoT circle
                if (cfg.Kind == SpellKind.EffectOverTimeArea && (cfg.AreaRadius <= 0f || cfg.AreaRadius > cfg.Range * 4f))
                {
                    SpellDebug.LogWarnThrottled(
                        $"aoe-radius-{e.Index}",
                        $"[Spell] AOE AreaRadius looks odd (AreaRadius={cfg.AreaRadius}, CastRange={cfg.Range}). Ensure your SpellDefinition.AreaRadius is set.",
                        null);
                }

                if (changed) _cfgRW[e] = cfg;
            }
        }
    }
}
