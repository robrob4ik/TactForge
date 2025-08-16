using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneBitRob
{
    public enum SpellKind : byte
    {
        Summon = 0,
        ProjectileLine = 1,           // “HitAllAlongProjectile” — fast projectile that pierces, optional radius
        EffectOverTimeTarget = 2,     // DoT/HoT attached to a single unit
        EffectOverTimeArea = 3,       // DoT/HoT in a world-space area
        Chain = 4                     // chain lightning / chain heal
    }

    public enum SpellAcquireMode : byte
    {
        ClosestEnemy = 0,
        DensestEnemyCluster = 1,
        LowestHealthAlly = 2,
        None = 3
    }

    public enum SpellEffectType : byte { Positive = 0, Negative = 1 }

    [CreateAssetMenu(menuName = "SO/Spell Definition")]
    public class SpellDefinition : ScriptableObject
    {
        [Header("General")]
        public string SpellName = "Spell";
        public SpellKind Kind = SpellKind.ProjectileLine;
        public SpellEffectType EffectType = SpellEffectType.Negative;
        public SpellAcquireMode AcquireMode = SpellAcquireMode.ClosestEnemy;

        [Header("Cast")]
        [Min(0f)] public float CastTime = 0.25f;
        [Min(0f)] public float Cooldown = 2f;
        [Min(0f)] public float Range = 12f;
        public bool RequiresLineOfSight = true;
        public LayerMask TargetLayerMask = ~0;

        [Header("Facing Gate")]
        public bool RequireFacing = true;
        [Range(1f, 60f)] public float FaceToleranceDegrees = 10f;
        [Min(0f)] public float MaxExtraFacingDelay = 0.35f;

        [Header("Animations (Two-Stage)")]
        public TwoStageAttackAnimationSet animations;

        [Header("Common Effect Power")]
        public float Amount = 10f; // damage or heal per hit/tick

        // ─────────────────────────────────────────────────────────────────────
        // Projectile (ProjectileLine & Chain visuals)
        [Header("Projectile (Line/Chain visuals)")]
        [Tooltip("Pool key in ProjectilePoolManager")]
        public string ProjectileId = "";           // visual pool key
        [Min(0.01f)] public float ProjectileSpeed = 60f;
        [Min(0.10f)] public float ProjectileMaxDistance = 20f;
        [Tooltip("Cylinder radius along the line while traveling (0 = ray).")]
        [Min(0f)] public float ProjectileRadius = 0.0f;

        // ─────────────────────────────────────────────────────────────────────
        // AoE / DoT
        [Header("Over-Time / AoE")]
        [Min(0f)] public float AreaRadius = 3f;
        [Min(0f)] public float Duration = 3f;
        [Min(0.05f)] public float TickInterval = 0.25f;
        [Tooltip("Optional VFX pool key attached to the target or spawned at area center.")]
        public string EffectVfxId = "";       // on target
        public string AreaVfxId = "";         // on area

        // ─────────────────────────────────────────────────────────────────────
        // Chain
        [Header("Chain")]
        [Min(1)] public int ChainMaxTargets = 3;
        [Min(0f)] public float ChainRadius = 6f;
        [Min(0f)] public float ChainPerJumpDelay = 0.05f;

        // ─────────────────────────────────────────────────────────────────────
        // Summon
        [Header("Summon")]
        [Tooltip("Prefab to instantiate (must include UnitBrain, UnitDefinitionProvider, GPUIPrefab).")]
        public GameObject SummonPrefab;
        [Min(1)] public int SummonCount = 1;
    }
}
