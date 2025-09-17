// File: Assets/PROJECT/Scripts/Core/Service/VfxService.cs
using UnityEngine;

namespace OneBitRob.VFX
{
    using OneBitRob.FX;

    public static class VfxService
    {
        public static void PlayByHash(int vfxIdHash, Vector3 position, Transform follow = null)
        {
            if (vfxIdHash == 0) return;
            var id = VisualAssetRegistry.GetVfxId(vfxIdHash);
            if (string.IsNullOrEmpty(id)) return;
            PlayById(id, position, follow);
        }

        public static void PlayById(string id, Vector3 position, Transform follow = null)
        {
            var go = PoolHub.GetPooled(PoolKind.Vfx, id); // SAFE pooled retrieval
            if (!go) return;

            VfxPoolManager.Ensure().SendMessage("PrepareOneShot", new object[] { go, position, follow }, SendMessageOptions.DontRequireReceiver);
            // The VfxPoolManager handles parenting/activation.
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

        public static void EndPersistent(long key) => VfxPoolManager.EndPersistent(key);
    }
}