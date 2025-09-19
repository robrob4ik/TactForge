using OneBitRob.AI;
using Unity.Entities;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_TargetBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (target, e) in SystemAPI.Query<RefRO<Target>>().WithEntityAccess())
            {
                var brain = UnitBrainRegistry.Get(e);
                if (!brain) continue;

                var targetBrain = UnitBrainRegistry.Get(target.ValueRO.Value);
                brain.CurrentTarget = targetBrain ? targetBrain.gameObject : null;
            }
        }
    }
}