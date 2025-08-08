using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    public class ChainedSpell : MonoBehaviour
    {
        private SpellDefinition _definition;
        private readonly HashSet<EnigmaHealth> _hitTargets = new();

        public void Initialize(SpellDefinition definition, GameObject startTarget)
        {
            _definition = definition;
            var health = startTarget.GetComponent<EnigmaHealth>();
            if (health != null)
            {
                StartCoroutine(ChainEffect(health, 0));
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator ChainEffect(EnigmaHealth current, int depth)
        {
            if (current == null || depth >= _definition.MaxTargets || _hitTargets.Contains(current))
            {
                yield break;
            }

            _hitTargets.Add(current);
            ApplyEffect(current);

            yield return new WaitForSeconds(_definition.ChainJumpDelay);

            Collider[] hits = Physics.OverlapSphere(current.transform.position, _definition.AreaRadius, _definition.TargetLayerMask);
            foreach (var hit in hits)
            {
                var next = hit.GetComponent<EnigmaHealth>();
                if (next != null && !_hitTargets.Contains(next))
                {
                    StartCoroutine(ChainEffect(next, depth + 1));
                    break;
                }
            }
        }

        private void ApplyEffect(EnigmaHealth targetHealth)
        {
            if (_definition.EffectType == SpellEffectType.Positive)
            {
                targetHealth.ReceiveHealth(_definition.DamageAmount, gameObject);
            }
            else
            {
                targetHealth.Damage(_definition.DamageAmount, gameObject, 0f, 0.5f, Vector3.zero);
            }
        }
    }

}