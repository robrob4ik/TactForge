using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    /// Add this component to a Character and it'll persist with its exact current state when transitioning to a new scene.
    /// It'll be automatically passed to the new scene's LevelManager to be used as this scene's main character.
    /// It'll keep the exact state all its components are in at the moment they finish the level.
    /// Its health, enabled abilities, component values, equipped weapons, new components you may have added, etc, will all remain once in the new scene. 
    /// Animator parameters : None
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Persistence")]
    public class EnigmaCharacterPersistence : EnigmaCharacterAbility, MMEventListener<MMGameEvent>, MMEventListener<EnigmaEngineEvent>
    {
        public virtual bool Initialized { get; set; }


        /// On Start(), we prevent our character from being destroyed if needed
        protected override void Initialization()
        {
            base.Initialization();

            if (AbilityAuthorized)
            {
                DontDestroyOnLoad(this.gameObject);
            }

            Initialized = true;
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            Initialized = false;
        }


        /// When we get a save request, we store our character in the game manager for future use
        /// <param name="gameEvent"></param>
        public virtual void OnMMEvent(MMGameEvent gameEvent)
        {
            if (gameEvent.EventName == "Save")
            {
                SaveCharacter();
            }
        }


        /// When we get a TopDown Engine event, we act on it
        /// <param name="gameEvent"></param>
        public virtual void OnMMEvent(EnigmaEngineEvent engineEvent)
        {
            if (!AbilityAuthorized)
            {
                return;
            }

            switch (engineEvent.EventType)
            {
                case EnigmaEngineEventTypes.LoadNextScene:
                    this.gameObject.SetActive(false);
                    break;
                case EnigmaEngineEventTypes.SpawnCharacterStarts:
                    this.transform.position = EnigmaLevelManager.Instance.InitialSpawnPoint.transform.position;
                    this.gameObject.SetActive(true);
                    EnigmaCharacter character = this.gameObject.GetComponentInParent<EnigmaCharacter>();
                    character.enabled = true;
                    character.ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Normal);
                    character.MovementState.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                    character.SetInputManager();
                    break;
                case EnigmaEngineEventTypes.LevelStart:
                    if (_health != null)
                    {
                        _health.StoreInitialPosition();
                    }

                    break;
                case EnigmaEngineEventTypes.RespawnComplete:
                    Initialized = true;
                    break;
            }
        }


        /// Saves to the game manager a reference to our character
        protected virtual void SaveCharacter()
        {
            if (!AbilityAuthorized)
            {
                return;
            }

            EnigmaGameManager.Instance.PersistentCharacter = _character;
        }


        /// Clears any saved character that may have been stored in the GameManager
        public virtual void ClearSavedCharacter()
        {
            if (!AbilityAuthorized)
            {
                return;
            }

            EnigmaGameManager.Instance.PersistentCharacter = null;
        }


        /// On enable we start listening for events
        protected override void OnEnable()
        {
            base.OnEnable();
            this.MMEventStartListening<MMGameEvent>();
            this.MMEventStartListening<EnigmaEngineEvent>();
        }


        /// On disable we stop listening for events
        protected virtual void OnDestroy()
        {
            this.MMEventStopListening<MMGameEvent>();
            this.MMEventStopListening<EnigmaEngineEvent>();
        }
    }
}