using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.Config
{
    public enum ClipSelect { Sequential, Random }

    [CreateAssetMenu(menuName = "TactForge/Animation/Compute Attack Set", fileName = "ComputeAttackSet")]
    public sealed class ComputeAttackAnimationSettings : ScriptableObject
    {
        public ClipSelect mode = ClipSelect.Sequential;
        public List<AnimationClip> clips = new();
        public bool Has => clips != null && clips.Count > 0;

        public AnimationClip Select(ref int next)
        {
            if (!Has) return null;
            if (mode == ClipSelect.Random) return clips[Random.Range(0, clips.Count)];
            var c = clips[next % clips.Count]; next++; return c;
        }
    }

    [CreateAssetMenu(menuName = "TactForge/Animation/Compute Two-Stage Attack Set", fileName = "ComputeTwoStageAttackSet")]
    public sealed class ComputeTwoStageAttackAnimationSettings : ScriptableObject
    {
        public ClipSelect prepareMode = ClipSelect.Sequential;
        public List<AnimationClip> prepare = new();

        public ClipSelect fireMode = ClipSelect.Sequential;
        public List<AnimationClip> fire = new();

        public bool HasPrepare => prepare != null && prepare.Count > 0;
        public bool HasFire    => fire    != null && fire.Count    > 0;

        public AnimationClip SelectPrepare(ref int next)
        {
            if (!HasPrepare) return null;
            if (prepareMode == ClipSelect.Random) return prepare[Random.Range(0, prepare.Count)];
            var c = prepare[next % prepare.Count]; next++; return c;
        }

        public AnimationClip SelectFire(ref int next)
        {
            if (!HasFire) return null;
            if (fireMode == ClipSelect.Random) return fire[Random.Range(0, fire.Count)];
            var c = fire[next % fire.Count]; next++; return c;
        }
    }
    [CreateAssetMenu(menuName = "TactForge/Attack Animation Set/Two-Stage Attack Animation Profile", fileName = "TwoStageAttackAnimationProfile")]
    public sealed class TwoStageAttackAnimationSettings : ScriptableObject
    {
        [Header("Prepare (windup)")]
        public AttackAnimationSelect prepareMode = AttackAnimationSelect.Sequential;
        public List<string> prepareParameters = new();

        [Header("Fire (release)")]
        public AttackAnimationSelect fireMode = AttackAnimationSelect.Sequential;
        public List<string> fireParameters = new();

        public bool HasPrepare => prepareParameters != null && prepareParameters.Count > 0;
        public bool HasFire    => fireParameters    != null && fireParameters.Count    > 0;

        public string SelectPrepare(ref int nextIndex)
        {
            if (!HasPrepare) return null;
            if (prepareMode == AttackAnimationSelect.Random)
                return prepareParameters[Random.Range(0, prepareParameters.Count)];
            string p = prepareParameters[nextIndex % prepareParameters.Count];
            nextIndex++;
            return p;
        }

        public string SelectFire(ref int nextIndex)
        {
            if (!HasFire) return null;
            if (fireMode == AttackAnimationSelect.Random)
                return fireParameters[Random.Range(0, fireParameters.Count)];
            string p = fireParameters[nextIndex % fireParameters.Count];
            nextIndex++;
            return p;
        }
    }
    
    [CreateAssetMenu(menuName = "TactForge/Attack Animation Set/Attack Animation Profile", fileName = "AttackAnimationProfile")]
    public sealed class AttackAnimationSettings : ScriptableObject
    {
        public AttackAnimationSelect mode = AttackAnimationSelect.Sequential;

        [Tooltip("Animator trigger parameter names")]
        public List<string> parameters = new();

        public bool HasEntries => parameters != null && parameters.Count > 0;

        public string SelectParameter(ref int nextIndex)
        {
            if (!HasEntries) return null;

            if (mode == AttackAnimationSelect.Random)
            {
                int i = Random.Range(0, parameters.Count);
                return parameters[i];
            }

            string p = parameters[nextIndex % parameters.Count];
            nextIndex++;
            return p;
        }
    }
}