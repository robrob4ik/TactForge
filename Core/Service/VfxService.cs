using UnityEngine;

namespace OneBitRob.VFX
{
    /// Facade for gameplay code. Knows only hashes and calls pool manager + registry for you.
    public static class VfxService
    {
        // One-shot by hash
        public static void PlayByHash(int vfxIdHash, Vector3 position, Transform follow = null)
        {
            if (vfxIdHash == 0) return;
            var id = VisualAssetRegistry.GetVfxId(vfxIdHash);
            if (string.IsNullOrEmpty(id)) return;
            VfxPoolManager.PlayById(id, position, follow);
        }

        // Persistent (ref-counted) by hash
        public static void BeginPersistentByHash(int vfxIdHash, long key, Vector3 position, Transform follow = null)
        {
            if (vfxIdHash == 0) return;
            var id = VisualAssetRegistry.GetVfxId(vfxIdHash);
            if (string.IsNullOrEmpty(id)) return;
            VfxPoolManager.BeginPersistent(id, key, position, follow);
        }

        public static void MovePersistent(long key, int vfxIdHash, Vector3 position, Transform follow = null)
            => VfxPoolManager.MovePersistent(key, vfxIdHash, position, follow);

        public static void MovePersistent(long key, Vector3 position, Transform follow = null)
            => VfxPoolManager.MovePersistent(key, position, follow);

        public static void EndPersistent(long key)
            => VfxPoolManager.EndPersistent(key);
    }
}