using Unity.Entities;

namespace OneBitRob.ECS.Link
{
    public class LinkComponents
    {
        public struct AgentEntityRef : IComponentData   // on the BT entity
        {
            public Entity Value;
        }

        public struct BrainEntityRef : IComponentData   // on the nav entity
        {
            public Entity Value;
        }
    }
}