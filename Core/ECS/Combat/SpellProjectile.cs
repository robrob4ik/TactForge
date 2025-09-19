// File: OneBitRob/ECS/SpellProjectile.cs
using UnityEngine;
using OneBitRob.FX;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public class SpellProjectile : ProjectileBase
    {
        public struct ArmData
        {
            public GameObject Attacker;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Damage;       // signed; negative = heal
            public float MaxDistance;
            public int LayerMask;
            public float Radius;
            public bool Pierce;
            public FeedbackDefinition HitFeedback;
        }

        private float _damage;
        private bool  _pierce;
        private FeedbackDefinition _hitFeedback;

        public void Arm(ArmData data)
        {
            ArmBase(data.Attacker, data.Origin, data.Direction, data.Speed, data.MaxDistance, data.LayerMask, data.Radius);
            _damage       = data.Damage;
            _pierce       = data.Pierce;
            _hitFeedback  = data.HitFeedback;
        }

        protected override bool ApplyOnHit(OneBitRob.AI.UnitBrain targetBrain, Vector3 point)
        {
            if (targetBrain?.Health == null) return true;

            float amt = _damage;
            bool  isHeal = amt < 0f;

            targetBrain.Health.Damage(amt, _attacker, 0f, 0f, _dir);

            DamageNumbersManager.Popup(new DamageNumbersParams
            {
                Kind     = isHeal ? DamagePopupKind.Heal : DamagePopupKind.Damage,
                Follow   = targetBrain.transform,
                Position = point,
                Amount   = Mathf.Abs(amt)
            });

            if (_hitFeedback != null)
                FeedbackService.TryPlay(_hitFeedback, targetBrain.transform, point);

            return _pierce; // continue only if piercing enabled
        }
    }
}
