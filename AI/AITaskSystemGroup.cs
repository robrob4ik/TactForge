using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTreeSystemGroup))]
    [UpdateAfter(typeof(SpatialHashBuildSystem))]
    public partial class AITaskSystemGroup : ComponentSystemGroup {}
}