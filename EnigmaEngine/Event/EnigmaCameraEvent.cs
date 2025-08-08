using MoreMountains.Tools;

using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
	public enum EnigmaCameraEventTypes { SetTargetCharacter, SetConfiner, StartFollowing, StopFollowing, RefreshPosition, ResetPriorities, RefreshAutoFocus }

	public struct EnigmaCameraEvent
	{
		public EnigmaCameraEventTypes EventType;
		public EnigmaCharacter TargetCharacter;
		public Collider Bounds;
		public Collider2D Bounds2D;

		public EnigmaCameraEvent(EnigmaCameraEventTypes eventType, EnigmaCharacter targetCharacter = null, Collider bounds = null, Collider2D bounds2D = null)
		{
			EventType = eventType;
			TargetCharacter = targetCharacter;
			Bounds = bounds;
			Bounds2D = bounds2D;
		}

		static EnigmaCameraEvent e;
		public static void Trigger(EnigmaCameraEventTypes eventType, EnigmaCharacter targetCharacter = null, Collider bounds = null, Collider2D bounds2D = null)
		{
			e.EventType = eventType;
			e.Bounds = bounds;
			e.Bounds2D = bounds2D;
			e.TargetCharacter = targetCharacter;
			MMEventManager.TriggerEvent(e);
		}
	}
}