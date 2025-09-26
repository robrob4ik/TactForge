using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.Config
{
    public enum ClipSelect
    {
        Sequential,
        Random
    }

  
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
            var c = clips[AnimIndexUtil.Wrap(next, clips.Count)];
            next++;
            return c;
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
        public bool HasFire => fire != null && fire.Count > 0;

        public AnimationClip SelectPrepare(ref int next)
        {
            if (!HasPrepare) return null;
            if (prepareMode == ClipSelect.Random) return prepare[Random.Range(0, prepare.Count)];
            var c = prepare[AnimIndexUtil.Wrap(next, prepare.Count)];
            next++;
            return c;
        }

        public AnimationClip SelectFire(ref int next)
        {
            if (!HasFire) return null;
            if (fireMode == ClipSelect.Random) return fire[Random.Range(0, fire.Count)];
            var c = fire[AnimIndexUtil.Wrap(next, fire.Count)];
            next++;
            return c;
        }
    }
}

public static class AnimIndexUtil
{
    public static int Wrap(int index, int count)
    {
        if (count <= 0) return 0;
        int r = index % count; // C# keeps sign of dividend
        return (r < 0) ? r + count : r; // make it positive
    }
}
