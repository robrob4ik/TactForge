#if UNITY_EDITOR
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using OneBitRob.ECS.Link;

[WorldSystemFilter(WorldSystemFilterFlags.Editor)]
public partial struct DebugBrainLinkGizmoSystem : ISystem
{
    ComponentLookup<LocalTransform> _ltLookup;

    public void OnCreate(ref SystemState state)
        => _ltLookup = state.GetComponentLookup<LocalTransform>(true);

    public void OnUpdate(ref SystemState state)
    {
        _ltLookup.Update(ref state);

        // Run on main thread; no Burst, immediate gizmos
        foreach (var (agentRef, brainLt) in
                 SystemAPI.Query<LinkComponents.AgentEntityRef, LocalTransform>())
        {
            if (!_ltLookup.HasComponent(agentRef.Value)) continue;
            var navPos = _ltLookup[agentRef.Value].Position;
            Debug.DrawLine(brainLt.Position, navPos, Color.cyan);
        }
    }
}
#endif