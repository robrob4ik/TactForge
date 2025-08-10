using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{

    [CreateAssetMenu(menuName = "SO/Combat/Attack Animation Set")]
    public class AttackAnimationSet : ScriptableObject
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
            else
            {
                string p = parameters[nextIndex % parameters.Count];
                nextIndex++;
                return p;
            }
        }
    }
}