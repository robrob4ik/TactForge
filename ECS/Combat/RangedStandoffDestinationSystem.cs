// // FILE: OneBitRob/AI/RangedStandoffDestinationSystem.cs
// using OneBitRob.AI;
// using OneBitRob.ECS;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
//
// namespace OneBitRob.AI
// {
//     /// Keeps ranged units near their ideal standoff distance with a small lateral offset.
//     /// Only applies when outside a comfort band around attackRange.
//     [UpdateInGroup(typeof(AITaskSystemGroup))]
//     public partial struct RangedStandoffDestinationSystem : ISystem
//     {
//         private ComponentLookup<LocalTransform> _posRO;
//         private EntityQuery _q;
//
//         public void OnCreate(ref SystemState state)
//         {
//             _posRO = state.GetComponentLookup<LocalTransform>(true);
//
//             _q = state.GetEntityQuery(new EntityQueryDesc
//             {
//                 All = new[]
//                 {
//                     ComponentType.ReadOnly<LocalTransform>(),
//                     ComponentType.ReadOnly<Target>(),
//                     ComponentType.ReadOnly<CombatStyle>(),
//                     ComponentType.ReadOnly<InAttackRange>(),
//                     ComponentType.ReadWrite<DesiredDestination>()
//                 }
//             });
//
//             state.RequireForUpdate(_q);
//         }
//
//         public void OnUpdate(ref SystemState state)
//         {
//             _posRO.Update(ref state);
//             var em = state.EntityManager;
//
//             var entities = _q.ToEntityArray(Allocator.Temp);
//             for (int i = 0; i < entities.Length; i++)
//             {
//                 var e = entities[i];
//                 var style = em.GetComponentData<CombatStyle>(e).Value;
//                 if (style != 2) continue; // only ranged
//
//                 var tgt = em.GetComponentData<Target>(e).Value;
//                 if (tgt == Entity.Null) continue;
//                 if (!_posRO.HasComponent(e) || !_posRO.HasComponent(tgt)) continue;
//
//                 var brain = UnitBrainRegistry.Get(e);
//                 var rw = brain?.UnitDefinition?.weapon as RangedWeaponDefinition;
//                 if (rw == null) continue;
//
//                 float3 self = _posRO[e].Position;
//                 float3 targ = _posRO[tgt].Position;
//
//                 float  range        = math.max(0.01f, rw.attackRange);
//                 float  dist         = math.distance(self, targ);
//                 const float minBand = 0.85f;   // inside this -> step back a bit
//                 const float maxBand = 1.15f;   // outside this -> step in
//
//                 bool inBand = (dist >= range * minBand) && (dist <= range * maxBand);
//
//                 // If already comfortable and can attack, don't churn destination each frame.
//                 var inRange = em.GetComponentData<InAttackRange>(e).Value != 0;
//                 if (inBand && inRange) continue;
//
//                 // Direction from target to self (stay roughly on where they are now).
//                 float3 toSelf = math.normalizesafe(self - targ, new float3(1, 0, 0));
//
//                 // Lateral (perpendicular) unit vector on XZ plane
//                 float3 right = math.normalizesafe(new float3(-toSelf.z, 0, toSelf.x));
//
//                 // Stable per (self,target) jitter
//                 uint  h        = math.hash(new int2(e.Index, tgt.Index));
//                 float jitter01 = (h / (float)uint.MaxValue);
//                 float sideSign = (jitter01 < 0.5f) ? -1f : 1f;
//                 float sideAmt  = (0.10f + 0.10f * (jitter01 * 2f)) * range; // ~[0.10..0.30]*range
//
//                 // Ideal radius slightly varies
//                 float jitter02 = ((h * 747796405u) / (float)uint.MaxValue);
//                 float targetRadius = range * (0.95f + 0.10f * jitter02); // [0.95..1.05]*range
//
//                 float3 ideal = targ + toSelf * targetRadius + right * (sideAmt * sideSign);
//
//                 // Only move if it actually improves our band error
//                 float futureDist = math.distance(ideal, targ);
//                 bool improves =
//                     (!inBand && math.abs(futureDist - range) < math.abs(dist - range)) ||
//                     (inBand && !inRange);
//
//                 if (!improves) continue;
//
//                 var dd = em.GetComponentData<DesiredDestination>(e);
//                 dd.Position = ideal;
//                 dd.HasValue = 1;
//                 em.SetComponentData(e, dd);
//             }
//
//             entities.Dispose();
//         }
//     }
// }
