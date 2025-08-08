using System;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.ECS
{
    [DisableAutoCreation]
    public partial class EntityCountDisplaySystem : SystemBase
    {
        public event Action<int> OnUpdateEntityCount;

        private EntityQuery entityQuery;

        protected override void OnCreate()
        {
            entityQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AgentTag>()
                .Build(EntityManager);
        }

        protected override void OnUpdate()
        {
            int count = entityQuery.CalculateEntityCount();
            OnUpdateEntityCount?.Invoke(count);
        }

        protected override void OnDestroy()
        {
            entityQuery.Dispose();
        }
    }
}