using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct AgentTag : IComponentData {}
    public struct AllyTag  : IComponentData {}
    public struct EnemyTag : IComponentData {}

    public struct Alive       : IComponentData { public byte Value; }
    public struct CombatStyle : IComponentData { public byte Value; }
    
    public struct DestroyEntityTag : IComponentData { }
    
}