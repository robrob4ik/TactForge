using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;

namespace OneBitRob.EnigmaEngine
{
    /// A list of possible events used by the character
    public enum EnigmaCharacterEventTypes
    {
        ButtonActivation,
        Jump
    }


    /// MMCharacterEvents are used in addition to the events triggered by the character's state machine, to signal stuff happening that is not necessarily linked to a change of state
    public struct EnigmaCharacterEvent
    {
        public EnigmaCharacter TargetCharacter;
        public EnigmaCharacterEventTypes EventType;

        /// Initializes a new instance of the <see cref="MoreMountains.TopDownEngine.MMCharacterEvent"/> struct.
        /// <param name="character">Character.</param>
        /// <param name="eventType">Event type.</param>
        public EnigmaCharacterEvent(EnigmaCharacter character, EnigmaCharacterEventTypes eventType)
        {
            TargetCharacter = character;
            EventType = eventType;
        }

        static EnigmaCharacterEvent e;

        public static void Trigger(EnigmaCharacter character, EnigmaCharacterEventTypes eventType)
        {
            e.TargetCharacter = character;
            e.EventType = eventType;
            MMEventManager.TriggerEvent(e);
        }
    }

    public enum EnigmaLifeCycleEventTypes
    {
        Death,
        Revive
    }

    public struct EnigmaLifeCycleEvent
    {
        public EnigmaHealth AffectedHealth;
        public EnigmaLifeCycleEventTypes EnigmaLifeCycleEventType;

        public EnigmaLifeCycleEvent(EnigmaHealth affectedHealth, EnigmaLifeCycleEventTypes lifeCycleEventType)
        {
            AffectedHealth = affectedHealth;
            EnigmaLifeCycleEventType = lifeCycleEventType;
        }

        static EnigmaLifeCycleEvent e;

        public static void Trigger(EnigmaHealth affectedHealth, EnigmaLifeCycleEventTypes lifeCycleEventType)
        {
            e.AffectedHealth = affectedHealth;
            e.EnigmaLifeCycleEventType = lifeCycleEventType;
            MMEventManager.TriggerEvent(e);
        }
    }


    /// An event fired when something takes damage
    public struct EnigmaDamageTakenEvent
    {
        public EnigmaHealth AffectedHealth;
        public GameObject Instigator;
        public float CurrentHealth;
        public float DamageCaused;
        public float PreviousHealth;
        public List<EnigmaTypedDamage> TypedDamages;


        /// Initializes a new instance of the <see cref="OneBitRob.EnigmaEngine.Enigma"/> struct.
        /// <param name="affectedHealth">Affected Health.</param>
        /// <param name="instigator">Instigator.</param>
        /// <param name="currentHealth">Current health.</param>
        /// <param name="damageCaused">Damage caused.</param>
        /// <param name="previousHealth">Previous health.</param>
        public EnigmaDamageTakenEvent(EnigmaHealth affectedHealth, GameObject instigator, float currentHealth, float damageCaused, float previousHealth, List<EnigmaTypedDamage> typedDamages)
        {
            AffectedHealth = affectedHealth;
            Instigator = instigator;
            CurrentHealth = currentHealth;
            DamageCaused = damageCaused;
            PreviousHealth = previousHealth;
            TypedDamages = typedDamages;
        }

        static EnigmaDamageTakenEvent e;

        public static void Trigger(EnigmaHealth affectedHealth, GameObject instigator, float currentHealth, float damageCaused, float previousHealth, List<EnigmaTypedDamage> typedDamages)
        {
            e.AffectedHealth = affectedHealth;
            e.Instigator = instigator;
            e.CurrentHealth = currentHealth;
            e.DamageCaused = damageCaused;
            e.PreviousHealth = previousHealth;
            e.TypedDamages = typedDamages;
            MMEventManager.TriggerEvent(e);
        }
    }
}