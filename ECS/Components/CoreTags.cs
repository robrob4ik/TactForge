// Assets/PROJECT/Scripts/Runtime/ECS/Core/Components/CoreTags.cs
using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct AgentTag : IComponentData {}
    public struct AllyTag  : IComponentData {}
    public struct EnemyTag : IComponentData {}

    public struct Alive       : IComponentData { public byte Value; }
    public struct CombatStyle : IComponentData { public byte Value; } // 1=melee, 2=ranged (as in your code)
}