using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    public enum DamageTypeModes
    {
        BaseDamage,
        TypedDamage
    }

    /// A scriptable object you can create assets from, to identify damage types
    [CreateAssetMenu(menuName = "EnigmaEngine/EnigmaDamageType", fileName = "DamageType")]
    public class EnigmaDamageType : ScriptableObject
    {
    }
}