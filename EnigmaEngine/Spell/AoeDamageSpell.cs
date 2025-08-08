
using UnityEngine;


namespace OneBitRob.EnigmaEngine
{

    public class AreaOfEffectSpell : MonoBehaviour
    {
        private SpellDefinition _definition;

        public void Initialize(SpellDefinition definition, Vector3 center)
        {
            _definition = definition;
            transform.position = center;
            InvokeRepeating(nameof(ApplyEffect), 0f, 1f);
            Destroy(gameObject, _definition.EffectDuration);
        }

        private void ApplyEffect()
        {
            Collider[] targets = Physics.OverlapSphere(transform.position, _definition.AreaRadius, _definition.TargetLayerMask);
            foreach (var collider in targets)
            {
                var health = collider.GetComponent<EnigmaHealth>();
                if (health != null)
                {
                    if (_definition.EffectType == SpellEffectType.Positive)
                    {
                        health.ReceiveHealth(_definition.DamageAmount, gameObject);
                    }
                    else
                    {
                        health.Damage(_definition.DamageAmount, gameObject, 0f, 0.5f, Vector3.zero);
                    }
                }
            }
        }
    }


}