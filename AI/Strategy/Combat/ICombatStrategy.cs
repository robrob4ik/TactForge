using UnityEngine;

namespace OneBitRob.AI
{
    public interface ICombatStrategy
    {
        void Attack(UnitBrain brain, Transform target);
    }

}