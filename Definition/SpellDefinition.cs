
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneBitRob
{
    public enum SpellTargetingStrategyType
    {
        ClosestEnemy,
        DensestCluster,
        LowestHealthAlly
    }

    public enum SpellTargetType
    {
        SingleTarget,
        AreaOfEffect,
        MultiTarget
    }

    public enum SpellEffectType
    {
        Positive,
        Negative
    }
    
    
    
    [CreateAssetMenu(menuName = "SO/Spell Definition")]
    public class SpellDefinition : ScriptableObject
    {
        [BoxGroup("General")]
        public string SpellName;

        [BoxGroup("General")]
        [EnumToggleButtons]
        public SpellTargetType TargetType;

        [BoxGroup("General")]
        [EnumToggleButtons]
        public SpellEffectType EffectType;

        [BoxGroup("Cast Settings")]
        public float CastTime = 1f;
        [BoxGroup("Cast Settings")]
        public float Cooldown = 5f;
        [BoxGroup("Cast Settings")]
        public float Range = 10f;
        [BoxGroup("Cast Settings")]
        public bool RequiresLineOfSight = true;
        [BoxGroup("Cast Settings")]
        public LayerMask TargetLayerMask;

        [BoxGroup("AOE Settings")]
        [ShowIf("TargetType", SpellTargetType.AreaOfEffect)]
        public float AreaRadius = 3f;

        [BoxGroup("MultiTarget Settings")]
        [ShowIf("TargetType", SpellTargetType.MultiTarget)]
        public int MaxTargets = 3;
        [BoxGroup("MultiTarget Settings")]
        [ShowIf("TargetType", SpellTargetType.MultiTarget)]
        public float ChainJumpDelay = 0.1f;

        [BoxGroup("Effect")]
        public AssetReferenceGameObject SpellEffectPrefab;

        [BoxGroup("Effect Parameters")]
        public float DamageAmount = 10f;
        [BoxGroup("Effect Parameters")]
        public float EffectDuration = 5f;

        [BoxGroup("Targeting Logic")]
        [EnumToggleButtons]
        public SpellTargetingStrategyType TargetingStrategyType;
    }

}