using System.Collections.Generic;
using OneBitRob.Constants;
using OneBitRob.ECS;
using OneBitRob.EnigmaEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI {
    
   public struct LowestHealthAllyTargeting : ISpellTargetingStrategy
    {
        // ────────────────────────────────────────────────────────── Single‑target healing
        public GameObject GetTarget(UnitBrain brain, SpellDefinition spell,
                                    ref ComponentLookup<LocalTransform>              posLookup,
                                    ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factLookup)
        {
            var wanted = new FixedList128Bytes<byte>();
            // BUGFIX: healers should scan SAME faction (allies), not opponents
            wanted.Add(brain.UnitDefinition.isEnemy
                ? GameConstants.ENEMY_FACTION
                : GameConstants.ALLY_FACTION);

            using var ents = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(
                brain.transform.position,
                spell.Range,
                wanted,
                ents,
                ref posLookup,
                ref factLookup);

            EnigmaHealth bestHealth = null;
            float        lowestPct  = 1.1f;

            for (int i = 0; i < ents.Length; i++)
            {
                var go = UnitBrainRegistry.Get(ents[i])?.gameObject;
                if (!go) continue;

                var hp = go.GetComponent<EnigmaHealth>();
                if (!hp || hp.CurrentHealth >= hp.MaximumHealth) continue;

                float pct = hp.CurrentHealth / (float)hp.MaximumHealth;
                if (pct < lowestPct)
                {
                    lowestPct  = pct;
                    bestHealth = hp;
                }
            }
            return bestHealth ? bestHealth.gameObject : null;
        }

        public List<GameObject> GetTargets(UnitBrain _, SpellDefinition __,
                                           ref ComponentLookup<LocalTransform> ___,
                                           ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____)
            => null;

        public Vector3? GetAOETargetPoint(UnitBrain _, SpellDefinition __,
                                          ref ComponentLookup<LocalTransform> ___,
                                          ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____)
            => null;
    }
}
