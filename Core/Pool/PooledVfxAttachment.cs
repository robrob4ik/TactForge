using UnityEngine;

namespace OneBitRob.FX
{
    /// <summary>Attach to pooled FX that may be parented under units. Lets us detach back to the pool before the unit is destroyed.</summary>
    public sealed class PooledFxAttachment : MonoBehaviour
    {
        public Transform PoolRoot;

        public void DetachToPoolRoot()
        {
            if (this == null) return;
            var t = transform;
            if (PoolRoot != null) t.SetParent(PoolRoot, true);
            gameObject.SetActive(false);
        }
    }
}