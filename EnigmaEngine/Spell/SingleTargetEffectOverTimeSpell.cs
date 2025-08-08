using System.Collections;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
   
    public class SingleTargetEffectOverTimeSpell : MonoBehaviour
    {
        private SpellDefinition _definition;
        private EnigmaHealth _targetHealth;

        public void Initialize(SpellDefinition definition, GameObject target)
        {
            _definition = definition;
            _targetHealth = target.GetComponent<EnigmaHealth>();

            if (_targetHealth == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = target.transform.position;
            transform.SetParent(target.transform);

            StartCoroutine(EffectRoutine());
        }

        private IEnumerator EffectRoutine()
        {
            float elapsed = 0f;
            float tickRate = 1f;

            while (elapsed < _definition.EffectDuration)
            {
                ApplyEffect();
                yield return new WaitForSeconds(tickRate);
                elapsed += tickRate;
            }

            Destroy(gameObject);
        }

        private void ApplyEffect()
        {
            if (_definition.EffectType == SpellEffectType.Positive)
            {
                _targetHealth.ReceiveHealth(_definition.DamageAmount, gameObject);
            }
            else
            {
                _targetHealth.Damage(_definition.DamageAmount, gameObject, 0f, 0.5f, Vector3.zero);
            }
        }
    }
}