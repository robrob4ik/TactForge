// File: OneBitRob/VFX/ProjectileService.cs
using MoreMountains.Tools;
using OneBitRob.FX;

namespace OneBitRob.VFX
{
    public static class ProjectileService
    {
        public static MMObjectPooler GetPooler(string id) => PoolHub.GetPooler(PoolKind.Projectile, id);
        public static UnityEngine.GameObject GetPooled(string id) => PoolHub.GetPooled(PoolKind.Projectile, id);
    }
}