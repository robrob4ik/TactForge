// FILE: Assets/PROJECT/Scripts/ECS/Spatial/SpatialHashBuildSystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static OneBitRob.ECS.SpatialHashComponents;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OneBitRob.ECS.Sync.SyncBrainFromMonoSystem))]
    public partial struct SpatialHashBuildSystem : ISystem
    {
        public static NativeParallelMultiHashMap<int, Entity> Grid;
        public static float CellSize;

        NativeParallelMultiHashMap<int, Entity> _gridNative;
        EntityQuery _targetsQuery;

        public void OnCreate(ref SystemState state)
        {
            _targetsQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SpatialHashTarget>(),
                ComponentType.ReadOnly<LocalTransform>());

            state.RequireForUpdate<SpatialHashSettings>();
            _gridNative = new NativeParallelMultiHashMap<int, Entity>(1024, Allocator.Persistent);
            Grid = _gridNative;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_gridNative.IsCreated) _gridNative.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Ensure capacity scales with number of targets (avoid overflow with >1024 entries)
            int targetCount = _targetsQuery.CalculateEntityCount();
            int requiredCapacity = math.max(1024, targetCount * 2);
            if (!_gridNative.IsCreated || _gridNative.Capacity < requiredCapacity)
            {
                if (_gridNative.IsCreated) _gridNative.Dispose();
                _gridNative = new NativeParallelMultiHashMap<int, Entity>(requiredCapacity, Allocator.Persistent);
            }

            _gridNative.Clear();

            var settings = SystemAPI.GetSingleton<SpatialHashSettings>();
            // Prevent bad settings from nuking performance or causing NaN ranges
            CellSize = math.max(0.01f, settings.CellSize);

            state.Dependency = new BuildJob
            {
                CellSize = CellSize,
                Grid = _gridNative.AsParallelWriter()
            }.ScheduleParallel(_targetsQuery, state.Dependency);

            // Complete writer before anyone reads Grid this frame.
            state.Dependency.Complete();

            Grid = _gridNative;
        }

        [BurstCompile]
        partial struct BuildJob : IJobEntity
        {
            public float CellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Grid;

            public void Execute(Entity entity, in LocalTransform transform)
            {
                float3 p = transform.Position;
                // 2D top-down: collapse Y to 0 so vertical offsets don't create different cells
                int3 cell = (int3)math.floor(new float3(p.x, 0f, p.z) / CellSize);
                int key = (int)math.hash(cell);
                Grid.Add(key, entity);
            }
        }
    }
}
