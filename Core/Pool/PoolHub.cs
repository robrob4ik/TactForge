using System.Collections;
using System.Reflection;
using MoreMountains.Tools;
using OneBitRob.ECS;
using UnityEngine;
using OneBitRob.Tools;
using OneBitRob.VFX; 

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
                case PoolKind.Projectile: pooler = ProjectilePoolManager.GetPooler(id); break;
                case PoolKind.Feedback:   pooler = FeedbackPoolManager.GetPooler(id); break;
                case PoolKind.Vfx:        pooler = VfxPoolManager.GetPooler(id); break;
            }
            return pooler != null;
        }
    }

    public static class PoolHub
    {
        private static IPoolResolver _resolver;
        public static IPoolResolver Resolver
        {
            get { return _resolver ??= new DefaultPoolResolver(); }
            set { _resolver = value; }
        }

        public static MMObjectPooler GetPooler(PoolKind kind, string id)
            => Resolver.TryGetPooler(kind, id, out var p) ? p : null;

        /// <summary>
        /// Safe pooled object retrieval:
        /// - Try pooler once
        /// - If MissingReferenceException occurs, repair pool (remove destroyed entries) and retry once
        /// - Return null quietly if still failing
        /// </summary>
        public static GameObject GetPooled(PoolKind kind, string id)
        {
            var pooler = GetPooler(kind, id);
            if (!pooler) return null;

            try
            {
                return pooler.GetPooledGameObject();
            }
            catch (MissingReferenceException)
            {
                TryRepairDestroyedEntries(pooler);
                try { return pooler.GetPooledGameObject(); }
                catch { return null; }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Prunes destroyed entries from MM pooler internal list (prefers non-reflection path).</summary>
        private static void TryRepairDestroyedEntries(MMObjectPooler pooler)
        {
            if (!pooler) return;

            // Fast path: our custom pooler exposes pruning directly.
            if (pooler is EnigmaSimpleObjectPooler enigmaPooler)
            {
                try { enigmaPooler.PruneDestroyedEntries(); } catch { /* ignore */ }
                return;
            }

            // Fallback path: reflection into MMObjectPooler (kept for safety/compat).
            try
            {
                var objectPoolField = typeof(MMObjectPooler).GetField("_objectPool",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var objectPool = objectPoolField?.GetValue(pooler);
                if (objectPool == null) return;

                var listProp = objectPool.GetType().GetProperty("PooledGameObjects",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (listProp?.GetValue(objectPool) is not IList list) return;

                for (int i = list.Count - 1; i >= 0; --i)
                {
                    var go = list[i] as GameObject;     // Unity destroyed objects compare to null
                    if (go == null) list.RemoveAt(i);
                }
            }
            catch { /* silent */ }
        }
    }
}
