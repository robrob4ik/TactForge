using UnityEngine;

namespace OneBitRob.AI
{
    public class RangedCombatStrategy : ICombatStrategy
    {
        public void Attack(UnitBrain brain, Transform target)
        {
            if (Time.time < brain.NextAllowedAttackTime) return;
            if ((target.position - brain.transform.position).sqrMagnitude >
                brain.UnitDefinition.attackRange * brain.UnitDefinition.attackRange * 1.1f)
                return;

            brain.CombatSubsystem.Attack();
            brain.NextAllowedAttackTime = Time.time + brain.UnitDefinition.attackCooldown;
        }
    }
}