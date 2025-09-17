// File: Assets/PROJECT/Scripts/Core/Service/PoolHub.cs
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    public enum PoolKind { Projectile, Feedback, Vfx }

    public interface IPoolResolver
    {
        bool TryGetPooler(PoolKind kind, string id, out MMObjectPooler pooler);
    }

    /// <summary>Default resolver that proxies to existing scene managers.</summary>
    public sealed class DefaultPoolResolver : IPoolResolver
    {
        public bool TryGetPooler(PoolKind kind, string id, out MMObjectPooler pooler)
        {
            pooler = null;
            if (string.IsNullOrEmpty(id)) return false;

            switch (kind)
            {
                case PoolKind.Projectile: pooler = OneBitRob.ECS.ProjectilePoolManager.GetPooler(id); break;
                case PoolKind.Feedback:   pooler = FeedbackPoolManager.GetPooler(id); break;
                case PoolKind.Vfx:        pooler = OneBitRob.VFX.VfxPoolManager.GetPooler(id); break;
            }
            return pooler != null;
        }
    }

    public static class PoolHub
    {
        private static IPoolResolver _resolver;
        public static IPoolResolver Resolver
        {
            get
            {
                if (_resolver == null) _resolver = new DefaultPoolResolver();
                return _resolver;
            }
            set { _resolver = value; }
        }

        public static MMObjectPooler GetPooler(PoolKind kind, string id)
            => Resolver.TryGetPooler(kind, id, out var p) ? p : null;

        /// <summary>
        /// Safe pooled object retrieval. Swallows MissingReferenceException from third‑party poolers whose lists contain destroyed entries.
        /// Returns null on failure; callers should gracefully skip the FX or fallback to a prefab.
        /// </summary>
        public static GameObject GetPooled(PoolKind kind, string id)
        {
            var p = GetPooler(kind, id);
            if (!p) return null;

            try
            {
                return p.GetPooledGameObject();
            }
            catch (MissingReferenceException ex)
            {
                Debug.LogWarning($"[PoolHub] Pool '{id}' had destroyed entries; returning null. {ex.Message}");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PoolHub] Exception while getting pooled '{id}': {ex.Message}");
                return null;
            }
        }
    }
}
