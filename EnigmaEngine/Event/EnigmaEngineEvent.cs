using MoreMountains.Tools;

namespace OneBitRob.EnigmaEngine
{
    public enum EnigmaEngineEventTypes
    {
        SpawnCharacterStarts,
        LevelStart,
        LevelComplete,
        LevelEnd,
        Pause,
        UnPause,
        PlayerDeath,
        SpawnComplete,
        RespawnStarted,
        RespawnComplete,
        StarPicked,
        GameOver,
        Repaint,
        TogglePause,
        LoadNextScene,
        PauseNoMenu
    }


    public struct EnigmaEngineEvent
    {
        public EnigmaEngineEventTypes EventType;
        public EnigmaCharacter OriginCharacter;
        
        public EnigmaEngineEvent(EnigmaEngineEventTypes eventType, EnigmaCharacter originCharacter)
        {
            EventType = eventType;
            OriginCharacter = originCharacter;
        }

        static EnigmaEngineEvent e;

        public static void Trigger(EnigmaEngineEventTypes eventType, EnigmaCharacter originCharacter)
        {
            e.EventType = eventType;
            e.OriginCharacter = originCharacter;
            MMEventManager.TriggerEvent(e);
        }
    }
}