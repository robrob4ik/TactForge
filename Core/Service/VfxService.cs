// File: Assets/PROJECT/Scripts/Core/Service/VfxService.cs
using OneBitRob.FX;
using UnityEngine;

namespace OneBitRob.VFX
{
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

            // Direct call – avoids SendMessage allocations & visibility issues
            VfxPoolManager.Ensure().PrepareOneShot(go, position, follow);
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