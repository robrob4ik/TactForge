using UnityEngine;

namespace OneBitRob.Constants
{
    public static class GameLayers
    {
        private const string AllyDetectionLayerName = "AllyDetection";
        private const string EnemyDetectionLayerName = "EnemyDetection";
        private const string AllyDamageableLayerName = "AllyDamageable";
        private const string EnemyDamageableLayerName = "EnemyDamageable";
        private const string PlayerLayerName = "Player";
        private const string EnemyLayerName = "Enemy";
        private const string AllyLayerName = "Ally";
        
        public static LayerMask AllyDetectionMask= LayerMask.GetMask(AllyDetectionLayerName);
        public static LayerMask EnemyDetectionMask = LayerMask.GetMask(EnemyDetectionLayerName);
        
        public static int AllyDamageableLayer = LayerMask.NameToLayer(AllyDamageableLayerName);
        public static int EnemyDamageableLayer = LayerMask.NameToLayer(EnemyDamageableLayerName);
        
        public static LayerMask PlayerMask = LayerMask.GetMask(PlayerLayerName);
        public static LayerMask EnemyMask = LayerMask.GetMask(EnemyLayerName);
        public static LayerMask AllyMask = LayerMask.GetMask(AllyLayerName);
        
        //public static readonly LayerMask PlayerAndAllyMask = (1 << PlayerMask) | (1 << AllyMask);
    }
}