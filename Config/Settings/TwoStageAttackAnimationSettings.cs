using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Config/Two-Stage Attack Animation Profile", fileName = "TwoStageAttackAnimationProfile")]
    public class TwoStageAttackAnimationSettings : ScriptableObject
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
}