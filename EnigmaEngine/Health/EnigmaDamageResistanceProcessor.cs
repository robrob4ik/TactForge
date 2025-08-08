using System.Collections.Generic;
using System.Linq;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace OneBitRob.EnigmaEngine
{
    /// Link this component to a Health component, and it'll be able to process incoming damage through resistances, handling damage reduction/increase, condition changes, movement multipliers, feedbacks and more.
    [AddComponentMenu("Enigma Engine/Character/Health/Enigma Damage Resistance Processor")]
    public class EnigmaDamageResistanceProcessor : MonoBehaviour
    {
        [Title("Damage Resistance List")]
        /// If this is true, this component will try to auto-fill its list of damage resistances from the ones found in its children 
        [Tooltip("If this is true, this component will try to auto-fill its list of damage resistances from the ones found in its children")]
        public bool AutoFillDamageResistanceList = true;

        /// If this is true, disabled resistances will be ignored by the auto fill 
        [Tooltip("If this is true, disabled resistances will be ignored by the auto fill")]
        public bool IgnoreDisabledResistances = true;

        /// If this is true, damage from damage types that this processor has no resistance for will be ignored
        [Tooltip("If this is true, damage from damage types that this processor has no resistance for will be ignored")]
        public bool IgnoreUnknownDamageTypes = false;

        /// the list of damage resistances this processor will handle. Auto filled if AutoFillDamageResistanceList is true
        [FormerlySerializedAs("DamageResitanceList")] [Tooltip("The list of damage resistances this processor will handle. Auto filled if AutoFillDamageResistanceList is true")]
        public List<EnigmaDamageResistance> DamageResistanceList;


        /// On awake we initialize our processor
        protected virtual void Awake()
        {
            Initialization();
        }


        /// Auto finds resistances if needed and sorts them
        protected virtual void Initialization()
        {
            if (AutoFillDamageResistanceList)
            {
                EnigmaDamageResistance[] foundResistances =
                    this.gameObject.GetComponentsInChildren<EnigmaDamageResistance>(
                        includeInactive: !IgnoreDisabledResistances);
                if (foundResistances.Length > 0)
                {
                    DamageResistanceList = foundResistances.ToList();
                }
            }

            SortDamageResistanceList();
        }


        /// A method used to reorder the list of resistances, based on priority by default.
        /// Don't hesitate to override this method if you'd like your resistances to be handled in a different order
        public virtual void SortDamageResistanceList()
        {
            // we sort the list by priority
            DamageResistanceList.Sort((p1, p2) => p1.Priority.CompareTo(p2.Priority));
        }


        /// Processes incoming damage through the list of resistances, and outputs the final damage value
        /// <param name="damage"></param>
        /// <param name="typedDamages"></param>
        /// <param name="damageApplied"></param>
        /// <returns></returns>
        public virtual float ProcessDamage(float damage, List<EnigmaTypedDamage> typedDamages, bool damageApplied)
        {
            float totalDamage = 0f;
            if (DamageResistanceList.Count == 0) // if we don't have resistances, we output raw damage
            {
                totalDamage = damage;
                if (typedDamages != null)
                {
                    foreach (EnigmaTypedDamage typedDamage in typedDamages)
                    {
                        totalDamage += typedDamage.DamageCaused;
                    }
                }

                if (IgnoreUnknownDamageTypes)
                {
                    totalDamage = damage;
                }

                return totalDamage;
            }
            else // if we do have resistances
            {
                totalDamage = damage;

                foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                {
                    totalDamage = resistance.ProcessDamage(totalDamage, null, damageApplied);
                }

                if (typedDamages != null)
                {
                    foreach (EnigmaTypedDamage typedDamage in typedDamages)
                    {
                        float currentDamage = typedDamage.DamageCaused;

                        bool atLeastOneResistanceFound = false;
                        foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                        {
                            if (resistance.TypeResistance == typedDamage.AssociatedDamageType)
                            {
                                atLeastOneResistanceFound = true;
                            }

                            currentDamage = resistance.ProcessDamage(currentDamage, typedDamage.AssociatedDamageType, damageApplied);
                        }

                        if (IgnoreUnknownDamageTypes && !atLeastOneResistanceFound)
                        {
                            // we don't add to the total
                        }
                        else
                        {
                            totalDamage += currentDamage;
                        }
                    }
                }

                return totalDamage;
            }
        }

        public virtual void SetResistanceByLabel(string searchedLabel, bool active)
        {
            foreach (EnigmaDamageResistance resistance in DamageResistanceList)
            {
                if (resistance.Label == searchedLabel)
                {
                    resistance.gameObject.SetActive(active);
                }
            }
        }


        /// When interrupting all damage over time of the specified type, stops their associated feedbacks if needed
        /// <param name="enigmaDamageType"></param>
        public virtual void InterruptDamageOverTime(EnigmaDamageType enigmaDamageType)
        {
            foreach (EnigmaDamageResistance resistance in DamageResistanceList)
            {
                if (resistance.gameObject.activeInHierarchy &&
                    ((resistance.DamageTypeMode == DamageTypeModes.BaseDamage) ||
                     (resistance.TypeResistance == enigmaDamageType))
                    && resistance.InterruptibleFeedback)
                {
                    resistance.OnDamageReceived?.StopFeedbacks();
                }
            }
        }


        /// Checks if any of the resistances prevents the character from changing condition, and returns true if that's the case, false otherwise
        /// <param name="typedEnigmaDamage"></param>
        /// <returns></returns>
        public virtual bool CheckPreventCharacterConditionChange(EnigmaDamageType typedEnigmaDamage)
        {
            foreach (EnigmaDamageResistance resistance in DamageResistanceList)
            {
                if (!resistance.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (typedEnigmaDamage == null)
                {
                    if ((resistance.DamageTypeMode == DamageTypeModes.BaseDamage) &&
                        (resistance.PreventCharacterConditionChange))
                    {
                        return true;
                    }
                }
                else
                {
                    if ((resistance.TypeResistance == typedEnigmaDamage) &&
                        (resistance.PreventCharacterConditionChange))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// Checks if any of the resistances prevents the character from changing condition, and returns true if that's the case, false otherwise
        /// <param name="typedEnigmaDamage"></param>
        /// <returns></returns>
        public virtual bool CheckPreventMovementModifier(EnigmaDamageType typedEnigmaDamage)
        {
            foreach (EnigmaDamageResistance resistance in DamageResistanceList)
            {
                if (!resistance.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (typedEnigmaDamage == null)
                {
                    if ((resistance.DamageTypeMode == DamageTypeModes.BaseDamage) &&
                        (resistance.PreventMovementModifier))
                    {
                        return true;
                    }
                }
                else
                {
                    if ((resistance.TypeResistance == typedEnigmaDamage) &&
                        (resistance.PreventMovementModifier))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// Returns true if the resistances on this processor make it immune to knockback, false otherwise
        /// <param name="typedDamage"></param>
        /// <returns></returns>
        public virtual bool CheckPreventKnockback(List<EnigmaTypedDamage> typedDamages)
        {
            if ((typedDamages == null) || (typedDamages.Count == 0))
            {
                foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                {
                    if (!resistance.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if ((resistance.DamageTypeMode == DamageTypeModes.BaseDamage) &&
                        (resistance.ImmuneToKnockback))
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (EnigmaTypedDamage typedDamage in typedDamages)
                {
                    foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                    {
                        if (!resistance.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        if (typedDamage == null)
                        {
                            if ((resistance.DamageTypeMode == DamageTypeModes.BaseDamage) &&
                                (resistance.ImmuneToKnockback))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if ((resistance.TypeResistance == typedDamage.AssociatedDamageType) &&
                                (resistance.ImmuneToKnockback))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }


        /// Processes the input knockback force through the various resistances
        /// <param name="knockback"></param>
        /// <param name="typedDamages"></param>
        /// <returns></returns>
        public virtual Vector3 ProcessKnockbackForce(Vector3 knockback, List<EnigmaTypedDamage> typedDamages)
        {
            if (DamageResistanceList.Count == 0) // if we don't have resistances, we output raw knockback value
            {
                return knockback;
            }
            else // if we do have resistances
            {
                foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                {
                    knockback = resistance.ProcessKnockback(knockback, null);
                }

                if (typedDamages != null)
                {
                    foreach (EnigmaTypedDamage typedDamage in typedDamages)
                    {
                        foreach (EnigmaDamageResistance resistance in DamageResistanceList)
                        {
                            if (IgnoreDisabledResistances && !resistance.isActiveAndEnabled)
                            {
                                continue;
                            }

                            knockback = resistance.ProcessKnockback(knockback, typedDamage.AssociatedDamageType);
                        }
                    }
                }

                return knockback;
            }
        }
    }
}