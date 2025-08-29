// FILE: Assets/PROJECT/Scripts/Runtime/Combat/SpellDefinition.cs
using UnityEngine;
using Sirenix.OdinInspector;
using OneBitRob.FX;

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
        // General
        [BoxGroup("General")]
        [LabelText("Spell Name")]
        public string  SpellName = "Spell";

        [BoxGroup("General")]
        public SpellKind Kind = SpellKind.ProjectileLine;

        [BoxGroup("General")]
        public SpellEffectType EffectType = SpellEffectType.Negative;

        [BoxGroup("General")]
        public SpellAcquireMode AcquireMode = SpellAcquireMode.ClosestEnemy;

        [BoxGroup("General")]
        [LabelText("Range"), SuffixLabel("units", true)]
        [MinValue(0f)]
        public float Range = 12f; // cast distance

        [BoxGroup("General")]
        public AttackAnimationSettings animations;

        [BoxGroup("General")]
        [LabelText("Fire Delay"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float FireDelaySeconds = 0f;

        [BoxGroup("General")]
        [LabelText("Cooldown"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float Cooldown = 2f;

        // Damage / Heal (instant)
        [BoxGroup("Damage+Heal (instant)")]
        [LabelText("Effect Amount")]
        [MinValue(0f)]
        public float EffectAmount = 10f;

        // DoT / HoT
        [BoxGroup("DoT+HoT")]
        [ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea")]
        [LabelText("Tick Amount"), MinValue(0f)]
        public float TickAmount = 5f;

        [BoxGroup("DoT+HoT")]
        [ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea")]
        [LabelText("Duration"), SuffixLabel("s", true), MinValue(0f)]
        public float Duration = 3f;

        [BoxGroup("DoT+HoT")]
        [ShowIf("@Kind == SpellKind.EffectOverTimeTarget || Kind == SpellKind.EffectOverTimeArea")]
        [LabelText("Tick Interval"), SuffixLabel("s", true), MinValue(0.05f)]
        public float TickInterval = 0.25f;

        // AOE
        [BoxGroup("AOE")]
        [ShowIf("@Kind == SpellKind.EffectOverTimeArea")]
        [LabelText("Area Radius"), SuffixLabel("units", true), MinValue(0f)]
        public float AreaRadius = 3f;

        // Projectile
        [BoxGroup("Projectile")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Projectile Id")]
        public string ProjectileId = "";

        [BoxGroup("Projectile")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Speed"), SuffixLabel("units/s", true), MinValue(0.01f)]
        public float ProjectileSpeed = 60f;

        [BoxGroup("Projectile")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Max Distance"), SuffixLabel("units", true), MinValue(0.1f)]
        public float ProjectileMaxDistance = 20f;

        [BoxGroup("Projectile")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Radius"), SuffixLabel("units", true), MinValue(0f)]
        public float ProjectileRadius = 0f;

        // Muzzle (Projectile)
        [BoxGroup("Projectile Muzzle")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Muzzle Forward (Local Z)"), SuffixLabel("units", true), MinValue(0f)]
        public float MuzzleForward = 0.60f;

        [BoxGroup("Projectile Muzzle")]
        [ShowIf("@Kind == SpellKind.ProjectileLine")]
        [LabelText("Muzzle Local Offset (XYZ)")]
        public Vector3 MuzzleLocalOffset = Vector3.zero;

        // VFX & AOE Visuals
        [BoxGroup("VFX & AOE Visuals")]
        [LabelText("Effect VFX Id")]
        public string EffectVfxId = "";

        [BoxGroup("VFX & AOE Visuals")]
        [LabelText("Area VFX Id")]
        public string AreaVfxId = "";

        [BoxGroup("VFX & AOE Visuals")]
        [LabelText("Area VFX Y Offset"), SuffixLabel("units", true), MinValue(0f)]
        [InfoBox("Vertical offset for the AOE VFX only (damage center remains at ground).")]
        public float AreaVfxYOffset = 0.04f;

        // Chain
        [BoxGroup("Chain")]
        [ShowIf("@Kind == SpellKind.Chain")]
        [MinValue(1)]
        public int ChainMaxTargets = 3;

        [BoxGroup("Chain")]
        [ShowIf("@Kind == SpellKind.Chain")]
        [LabelText("Chain Radius"), SuffixLabel("units", true), MinValue(0f)]
        public float ChainRadius = 6f;

        [BoxGroup("Chain")]
        [ShowIf("@Kind == SpellKind.Chain")]
        [LabelText("Per Jump Delay"), SuffixLabel("s", true), MinValue(0f)]
        public float ChainPerJumpDelay = 0.05f;

        // Summon
        [BoxGroup("Summon")]
        [ShowIf("@Kind == SpellKind.Summon")]
        [LabelText("Summon Prefab"), AssetsOnly]
        public GameObject SummonPrefab;

        [BoxGroup("Summon")]
        [ShowIf("@Kind == SpellKind.Summon")]
        [MinValue(1)]
        public int SummonCount = 1;

        // Debug
        [BoxGroup("Debug")]
        public bool  DebugDraw = true;

        [BoxGroup("Debug")]
        public Color DebugColor = new Color(0.8f, 0.2f, 1f, 0.5f);

        // Feedbacks
        [BoxGroup("Feedbacks")]
        [LabelText("Prepare Feedback")]
        [AssetsOnly] public FeedbackDefinition prepareFeedback;
        [BoxGroup("Feedbacks")]
        [LabelText("Fire Feedback")]
        [AssetsOnly] public FeedbackDefinition fireFeedback;
        [BoxGroup("Feedbacks")]
        [LabelText("Impact Feedback (AOE Center)")]
        [AssetsOnly] public FeedbackDefinition impactFeedback;
    }
}
