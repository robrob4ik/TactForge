using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTreeSystemGroup))]
    [UpdateAfter(typeof(OneBitRob.ECS.SpatialHashBuildSystem))]
    public partial class AITaskSystemGroup : ComponentSystemGroup {}
}