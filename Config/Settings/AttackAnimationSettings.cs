using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Attack Animation Set/Attack Animation Profile", fileName = "AttackAnimationProfile")]
    public class AttackAnimationSettings : ScriptableObject
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