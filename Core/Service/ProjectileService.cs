// File: OneBitRob/VFX/ProjectileService.cs

using MoreMountains.Tools;
using OneBitRob.ECS;
using UnityEngine;

namespace OneBitRob.VFX
{
    public static class ProjectileService
    {
        /// Returns the pooler for a projectile id, or null if not found.
        public static MMObjectPooler GetPooler(string id) => ProjectilePoolManager.GetPooler(id);

        /// Returns a pooled (inactive) projectile GameObject ready to arm & activate, or null if pool/id missing.
        public static GameObject GetPooled(string id) => ProjectilePoolManager.GetPooled(id);
    }
}