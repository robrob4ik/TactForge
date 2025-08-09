using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;

namespace OneBitRob.AI
{
    /// Runs after Behavior Designer (BT) and after spatial hash is built.
    /// All AI task systems live here.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTreeSystemGroup))]
    [UpdateAfter(typeof(OneBitRob.ECS.SpatialHashBuildSystem))]
    public partial class AITaskSystemGroup : ComponentSystemGroup {}
}