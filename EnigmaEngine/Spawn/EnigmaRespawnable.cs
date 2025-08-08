namespace OneBitRob.EnigmaEngine
{
    /// Interface for player respawn
    public interface EnigmaRespawnable
    {
        void OnPlayerRespawn(EnigmaCheckPoint checkpoint, EnigmaCharacter player);
    }
}