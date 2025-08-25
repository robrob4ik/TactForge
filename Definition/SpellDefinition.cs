// FILE: Assets/PROJECT/Scripts/Runtime/Config/SpellDefinition.cs
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using OneBitRob.AI.Debugging;

namespace OneBitRob
{
    public enum SpellKind : byte
    {
        Summon = 0,
        ProjectileLine = 1,
        EffectOverTimeTarget = 2,
        EffectOverTimeArea = 3,
        Chain = 4
    }

    public enum SpellAcquireMode : byte
    {
        ClosestEnemy = 0,
        DensestEnemyCluster = 1,
        LowestHealthAlly = 2,
        None = 3
    }

    public enum SpellEffectType : byte { Positive = 0, Negative = 1 }

    [CreateAssetMenu(menuName = "TactForge/Definition/Spell", fileName = "SpellDefinition")]
    public class SpellDefinition : ScriptableObject
    {
        [Header("General")]
        public string  SpellName = "Spell";
        public SpellKind Kind = SpellKind.ProjectileLine;
        public SpellEffectType EffectType = SpellEffectType.Negative;
        public SpellAcquireMode AcquireMode = SpellAcquireMode.ClosestEnemy;
        [Min(0f)] public float Range = 12f;                     // cast distance
        public AttackAnimationSettings animations;
        [Min(0f)] public float FireDelaySeconds = 0f;
        [Min(0f)] public float Cooldown = 2f;

        [Header("Damage / Heal (instant)")]
        [Min(0f)] public float EffectAmount = 10f;

        [Header("DoT / HoT")]
        [Min(0f)] public float TickAmount = 5f;
        [Min(0f)] public float Duration = 3f;
        [Min(0.05f)] public float TickInterval = 0.25f;

        [Header("AOE")]
        [Min(0f)] public float AreaRadius = 3f;

        [Header("Projectile")]
        public string ProjectileId = "";
        [Min(0.01f)] public float ProjectileSpeed = 60f;
        [Min(0.1f)]  public float ProjectileMaxDistance = 20f;
        [Min(0f)]    public float ProjectileRadius = 0f;

        [Header("Projectile/Muzzle")]
        [Min(0f)] public float MuzzleForward = 0.60f;
        public Vector3 MuzzleLocalOffset = Vector3.zero;

        [Header("VFX & AOE Visuals")]
        public string EffectVfxId = "";
        public string AreaVfxId = "";
        [Tooltip("Vertical offset for the AOE VFX only (damage center remains at ground).")]
        [Min(0f)] public float AreaVfxYOffset = 0.04f;

        [Header("Chain")]
        [Min(1)] public int   ChainMaxTargets = 3;
        [Min(0f)] public float ChainRadius = 6f;
        [Min(0f)] public float ChainPerJumpDelay = 0.05f;

        [Header("Summon")]
        public GameObject SummonPrefab;

        [Min(1)] public int SummonCount = 1;

        [Header("Debug")]
        public bool  DebugDraw = true;
        public Color DebugColor = new Color(0.8f, 0.2f, 1f, 0.5f);

        // (Hidden/auto fields omitted)
    }
}
