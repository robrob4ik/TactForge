using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static OneBitRob.ECS.SpatialHashComponents;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct SpatialHashBuildSystem : ISystem
    {
        public static NativeParallelMultiHashMap<int, Entity> Grid;
        public static float CellSize;

        NativeParallelMultiHashMap<int, Entity> _gridNative;
        EntityQuery _targetsQuery;

        public void OnCreate(ref SystemState state)
        {
            _targetsQuery = state.GetEntityQuery(ComponentType.ReadOnly<SpatialHashTarget>(), ComponentType.ReadOnly<LocalTransform>());
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
            _gridNative.Clear();

            CellSize = SystemAPI.GetSingleton<SpatialHashSettings>().CellSize;
            var writer = _gridNative.AsParallelWriter();

            state.Dependency = new BuildJob
                {
                    CellSize = CellSize,
                    Grid = writer
                }
                .ScheduleParallel(_targetsQuery, state.Dependency);

// #if UNITY_EDITOR
//             // One‑frame, human‑readable count
//             state.Dependency.Complete();      // wait so Count() is valid
//             EcsLogger.Info(this, $"Spatial hash built: {_gridNative.Count()} entries");
// #endif

            Grid = _gridNative;
        }

        [BurstCompile]
        partial struct BuildJob : IJobEntity
        {
            public float CellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Grid;

            public void Execute(Entity entity, in LocalTransform transform)
            {
                int3 cell = (int3)math.floor(transform.Position / CellSize);

                int key = (int)math.hash(cell);
                Grid.Add(key, entity);
            }
        }
    }
}