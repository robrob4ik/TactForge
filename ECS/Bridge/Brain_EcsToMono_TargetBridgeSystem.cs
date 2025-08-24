// ECS/HybridSync/EcsToMono/Brain_EcsToMono_TargetBridgeSystem.cs
using Unity.Entities;

namespace OneBitRob.ECS
{
    /// Mirrors Target(Value: Entity) -> UnitBrain.CurrentTarget (GameObject).
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_TargetBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (target, e) in SystemAPI.Query<RefRO<Target>>().WithEntityAccess())
            {
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (!brain) continue;

                var targetBrain = OneBitRob.AI.UnitBrainRegistry.Get(target.ValueRO.Value);
                brain.CurrentTarget = targetBrain ? targetBrain.gameObject : null;
            }
        }
    }
}