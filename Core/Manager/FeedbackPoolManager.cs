// FILE: Assets/PROJECT/Scripts/Runtime/FX/Feedbacks/FeedbackPoolManager.cs
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    [DisallowMultipleComponent]
    public sealed class FeedbackPoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;
            public MMObjectPooler Pooler;
        }

        [Tooltip("Map of feedback ids to MM poolers. Setup once per scene.")]
        public List<Entry> Pools = new();

        private static Dictionary<string, MMObjectPooler> _map;

        private void Awake()
        {
            if (_map == null) _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            else _map.Clear();

            for (int i = 0; i < Pools.Count; i++)
            {
                var p = Pools[i];
                if (p.Pooler == null || string.IsNullOrEmpty(p.Id)) continue;
                // Do NOT call FillObjectPool() here; MM handles it.
                _map[p.Id] = p.Pooler;
            }
        }

        public static MMObjectPooler Resolve(string id)
        {
            if (string.IsNullOrEmpty(id) || _map == null) return null;
            return _map.TryGetValue(id, out var pooler) ? pooler : null;
        }
    }
}