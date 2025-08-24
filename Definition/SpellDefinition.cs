// Runtime/Spells/SpellDefinition.cs
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob
{
    public enum SpellKind : byte
    {
        Summon = 0,
        ProjectileLine = 1,           // fast projectile that may pierce
        EffectOverTimeTarget = 2,     // DoT/HoT attached to a single unit
        EffectOverTimeArea = 3,       // DoT/HoT in a world-space area (AOE)
        Chain = 4                     // chain lightning / chain heal
    }

    public enum SpellAcquireMode : byte
    {
        ClosestEnemy = 0,
        DensestEnemyCluster = 1,
        LowestHealthAlly = 2,
        None = 3
    }

    public enum SpellEffectType : byte
    {
        Positive = 0,  // heal/buff
        Negative = 1   // damage/debuff
    }

    [CreateAssetMenu(menuName = "TactForge/Definition/Spell", fileName = "SpellDefinition")]
    public class SpellDefinition : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────── General
        [FoldoutGroup("General"), LabelWidth(150)]
        public string SpellName = "Spell";

        [FoldoutGroup("General"), LabelWidth(150), EnumToggleButtons]
        public SpellKind Kind = SpellKind.ProjectileLine;

        [FoldoutGroup("General"), LabelWidth(150), EnumToggleButtons, LabelText("Effect")]
        public SpellEffectType EffectType = SpellEffectType.Negative;

        [FoldoutGroup("General"), LabelWidth(150), EnumToggleButtons, LabelText("Acquire")]
        public SpellAcquireMode AcquireMode = SpellAcquireMode.ClosestEnemy;

        [FoldoutGroup("General"), LabelWidth(150), Min(0f), LabelText("Cast Range (m)")]
        public float Range = 12f;

        [FoldoutGroup("General"), LabelWidth(150), LabelText("Cast Animations")]
        public AttackAnimationSettings animations;

        [FoldoutGroup("General"), LabelWidth(150), Min(0f), LabelText("Fire Delay (s)")]
        public float FireDelaySeconds = 0f;

        [FoldoutGroup("General"), LabelWidth(150), Min(0f), LabelText("Cooldown (s)")]
        public float Cooldown = 2f;

        // ─────────────────────────────────────────────────────────── Damage / Heal
        [FoldoutGroup("DamageHeal"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(180), LabelText("Damage / Heal per hit"), Min(0f)]
        public float EffectAmount = 10f;

        // ─────────────────────────────────────────────────────────── DoT / HoT
        [FoldoutGroup("DoTHoT"), ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Tick Amount"), Min(0f)]
        public float TickAmount = 5f;

        [FoldoutGroup("DoTHoT"), ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Duration (s)"), Min(0f)]
        public float Duration = 3f;

        [FoldoutGroup("DoTHoT"), ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Tick Every (s)"), Min(0.05f)]
        public float TickInterval = 0.25f;

        // ─────────────────────────────────────────────────────────── AOE
        [FoldoutGroup("AoE"), ShowIf("@Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Area Radius (m)"), Min(0f)]
        public float AreaRadius = 3f;

        // ─────────────────────────────────────────────────────────── Projectile
        [FoldoutGroup("Projectile"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Projectile Pool Id")]
        public string ProjectileId = "";

        [FoldoutGroup("Projectile"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Speed (m/s)"), Min(0.01f)]
        public float ProjectileSpeed = 60f;

        [FoldoutGroup("Projectile"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Max Distance (m)"), Min(0.1f)]
        public float ProjectileMaxDistance = 20f;

        [FoldoutGroup("Projectile"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Radius (m)"), Min(0f)]
        public float ProjectileRadius = 0f;

        [FoldoutGroup("Projectile/Muzzle"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Muzzle Forward (m)"), Min(0f)]
        public float MuzzleForward = 0.60f;

        [FoldoutGroup("Projectile/Muzzle"), ShowIf("@Kind == SpellKind.ProjectileLine || Kind == SpellKind.Chain"),
         LabelWidth(150), LabelText("Muzzle Local Offset (x,y,z)")]
        public Vector3 MuzzleLocalOffset = Vector3.zero;

        // ─────────────────────────────────────────────────────────── VFX & AoE
        [FoldoutGroup("VFX"), ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Effect VFX Id")]
        public string EffectVfxId = "";

        [FoldoutGroup("VFX"), ShowIf("@Kind == SpellKind.EffectOverTimeArea"),
         LabelWidth(150), LabelText("Area VFX Id")]
        public string AreaVfxId = "";

        // ─────────────────────────────────────────────────────────── Chain
        [FoldoutGroup("Chain"), ShowIf("@Kind == SpellKind.Chain"), Min(1), LabelWidth(150), LabelText("Max Targets")]
        public int ChainMaxTargets = 3;

        [FoldoutGroup("Chain"), ShowIf("@Kind == SpellKind.Chain"), Min(0f), LabelWidth(150), LabelText("Jump Radius (m)")]
        public float ChainRadius = 6f;

        [FoldoutGroup("Chain"), ShowIf("@Kind == SpellKind.Chain"), Min(0f), LabelWidth(150), LabelText("Per Jump Delay (s)")]
        public float ChainPerJumpDelay = 0.05f;

        // ─────────────────────────────────────────────────────────── Summon
        [FoldoutGroup("Summon"), ShowIf("@Kind == SpellKind.Summon")]
        public GameObject SummonPrefab;

        [FoldoutGroup("Summon"), ShowIf("@Kind == SpellKind.Summon"), Min(1)]
        public int SummonCount = 1;

        // ─────────────────────────────────────────────────────────── Debug
        [FoldoutGroup("Debug"), LabelWidth(150), LabelText("Draw Gizmos")]
        public bool DebugDraw = true;

        [FoldoutGroup("Debug"), LabelWidth(150), LabelText("Gizmo Color")]
        public Color DebugColor = new Color(0.8f, 0.2f, 1f, 0.5f);

        // ─────────────────────────────────────────────────────────── Advanced (hidden – auto)
        [FoldoutGroup("Advanced"), HideInInspector] public bool RequiresLineOfSight = false;
        [FoldoutGroup("Advanced"), HideInInspector] public bool RequireFacing       = false;
        [FoldoutGroup("Advanced"), HideInInspector] public float FaceToleranceDegrees = 0f;
        [FoldoutGroup("Advanced"), HideInInspector] public float MaxExtraFacingDelay = 0f;
    }
}
