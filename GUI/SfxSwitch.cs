using UnityEngine;
using MoreMountains.Tools;

namespace OneBitRob.EnigmaEngine
{
	[AddComponentMenu("Enigma Engine/GUI/Sfx Switch")]
	public class SfxSwitch : MonoBehaviour
	{
		public virtual void On()
		{
			MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.UnmuteTrack, MMSoundManager.MMSoundManagerTracks.Sfx);
		}

		public virtual void Off()
		{
			MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.MuteTrack, MMSoundManager.MMSoundManagerTracks.Sfx);
		}
	}
}