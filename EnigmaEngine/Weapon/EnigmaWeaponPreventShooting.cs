using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    /// An abstract class used to define additional conditions on a weapon to prevent it from firing
    public abstract class EnigmaWeaponPreventShooting : MonoBehaviour
    {
        /// Override this method to define shooting conditions
        public abstract bool ShootingAllowed();
    }
}