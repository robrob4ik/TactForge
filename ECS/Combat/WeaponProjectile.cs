// File: OneBitRob/ECS/WeaponProjectile.cs
using UnityEngine;
using OneBitRob.FX;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public class WeaponProjectile : ProjectileBase
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float MaxDistance;
            public int LayerMask;
            public float CritChance;
            public float CritMultiplier;
            public float PierceChance;
            public int PierceMaxTargets;
        }

        private float _baseDamage;
        private float _critChance;
        private float _critMultiplier;
        private float _pierceChance;
        private int   _pierceMaxTargets;
        private int   _piercedCount;

        public void Arm(ArmData data)
        {
            ArmBase(data.Attacker, data.Origin, data.Direction, data.Speed, data.MaxDistance, data.LayerMask);
            _baseDamage      = data.Damage;
            _critChance      = Mathf.Clamp01(data.CritChance);
            _critMultiplier  = Mathf.Max(1f,    data.CritMultiplier);
            _pierceChance    = Mathf.Clamp01(data.PierceChance);
            _pierceMaxTargets= Mathf.Max(0,     data.PierceMaxTargets);
            _piercedCount    = 0;
        }

        protected override bool ApplyOnHit(OneBitRob.AI.UnitBrain targetBrain, Vector3 point)
        {
            if (targetBrain?.Health == null) return true;

            bool  isCrit = (_critChance > 0f) && (Random.value < _critChance);
            float dmg    = isCrit ? _baseDamage * Mathf.Max(1f, _critMultiplier) : _baseDamage;

            targetBrain.Health.Damage(dmg, _attacker, 0f, 0f, _dir);

            DamageNumbersManager.Popup(new DamageNumbersParams
            {
                Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                Follow   = targetBrain.transform,
                Position = point,
                Amount   = dmg
            });

            var rangedDef = _attackerBrain != null ? _attackerBrain.UnitDefinition?.weapon as OneBitRob.RangedWeaponDefinition : null;
            if (rangedDef != null && rangedDef.impactFeedback != null)
                FeedbackService.TryPlay(rangedDef.impactFeedback, targetBrain.transform, point);

            // Pierce roll
            bool canPierceMore = _piercedCount < _pierceMaxTargets;
            bool rollPierce    = _pierceChance > 0f && Random.value < _pierceChance;
            if (canPierceMore && rollPierce)
            {
                _piercedCount++;
                return true; // continue flying
            }
            return false;     // stop
        }
    }
}
