using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine.Events;

namespace OneBitRob.EnigmaEngine
{
    [MMHiddenProperties("AbilityStopFeedbacks")]
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Pause")]
    public class EnigmaCharacterPause : EnigmaCharacterAbility
    {
        public override string HelpBoxText()
        {
            return "Allows this character (and the player controlling it) to press the pause button to pause the game.";
        }

        [Title("Pause audio tracks")]
        [Tooltip("Whether or not to mute the sfx track when the game pauses, and to unmute it when it unpauses")]
        public bool MuteSfxTrackSounds = true;

        [Tooltip("Whether or not to mute the UI track when the game pauses, and to unmute it when it unpauses")]
        public bool MuteUITrackSounds = false;

        [Tooltip("Whether or not to mute the music track when the game pauses, and to unmute it when it unpauses")]
        public bool MuteMusicTrackSounds = false;

        [Tooltip("Whether or not to mute the master track when the game pauses, and to unmute it when it unpauses")]
        public bool MuteMasterTrackSounds = false;

        [Title("Hooks")]
        [Tooltip("A UnityEvent that will trigger when the game pauses")]
        public UnityEvent OnPause;

        [Tooltip("A UnityEvent that will trigger when the game unpauses")]
        public UnityEvent OnUnpause;
        
        protected override void HandleInput()
        {
            if (_inputManager.PauseButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                TriggerPause();
            }
        }
        
        protected virtual void TriggerPause()
        {
            if (_condition.CurrentState == EnigmaCharacterStates.CharacterConditions.Dead)
            {
                return;
            }

            if (!AbilityAuthorized)
            {
                return;
            }

            PlayAbilityStartFeedbacks();
            // we trigger a Pause event for the GameManager and other classes that could be listening to it too
            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.TogglePause, null);
        }

        public virtual void PauseCharacter()
        {
            if (!this.enabled)
            {
                return;
            }

            _condition.ChangeState(EnigmaCharacterStates.CharacterConditions.Paused);

            OnPause?.Invoke();

            if (MuteSfxTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.MuteTrack, MMSoundManager.MMSoundManagerTracks.Sfx);
            }

            if (MuteUITrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.MuteTrack, MMSoundManager.MMSoundManagerTracks.UI);
            }

            if (MuteMusicTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.MuteTrack, MMSoundManager.MMSoundManagerTracks.Music);
            }

            if (MuteMasterTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.MuteTrack, MMSoundManager.MMSoundManagerTracks.Master);
            }
        }

        public virtual void UnPauseCharacter()
        {
            if (!this.enabled)
            {
                return;
            }

            _condition.RestorePreviousState();

            OnUnpause?.Invoke();

            if (MuteSfxTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.UnmuteTrack, MMSoundManager.MMSoundManagerTracks.Sfx);
            }

            if (MuteUITrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.UnmuteTrack, MMSoundManager.MMSoundManagerTracks.UI);
            }

            if (MuteMusicTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.UnmuteTrack, MMSoundManager.MMSoundManagerTracks.Music);
            }

            if (MuteMasterTrackSounds)
            {
                MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.UnmuteTrack, MMSoundManager.MMSoundManagerTracks.Master);
            }
        }
    }
}