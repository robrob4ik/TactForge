
namespace OneBitRob.EnigmaEngine
{
    public class EnigmaCharacterStates
    {
        public enum CharacterConditions
        {
            Normal,
            ControlledMovement,
            Frozen,
            Paused,
            Dead,
            Stunned
        }
        
        public enum MovementStates
        {
            Null,
            Idle,
            Falling,
            Walking,
            CombatStance,
            Running,
            Crouching,
            Crawling,
            Dashing,
            Jumping,
            Pushing,
            Attacking,
            FallingDownHole
        }
    }
}