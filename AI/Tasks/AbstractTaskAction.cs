using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    public abstract class AbstractTaskAction<TComponent, TTag, TSystem> : ILogicNode, ITaskComponentData
        where TComponent : unmanaged, IBufferElementData
        where TTag : unmanaged, IComponentData, IEnableableComponent
        where TSystem : SystemBase
    {
        [SerializeField] ushort m_Index, m_ParentIndex, m_SiblingIndex;
        public ushort Index { get => m_Index; set => m_Index = value; }
        public ushort ParentIndex { get => m_ParentIndex; set => m_ParentIndex = value; }
        public ushort SiblingIndex { get => m_SiblingIndex; set => m_SiblingIndex = value; }
        public ushort RuntimeIndex { get; set; }

        public virtual ComponentType Tag => typeof(TTag);
        public virtual System.Type SystemType => typeof(TSystem);

        protected abstract TComponent CreateBufferElement(ushort runtimeIndex);

        public void AddBufferElement(World world, Entity entity)
        {
            DynamicBuffer<TComponent> buffer;
            if (world.EntityManager.HasBuffer<TComponent>(entity))
                buffer = world.EntityManager.GetBuffer<TComponent>(entity);
            else
                buffer = world.EntityManager.AddBuffer<TComponent>(entity);

            buffer.Add(CreateBufferElement(RuntimeIndex));
        }

        public void ClearBufferElement(World world, Entity entity)
        {
            if (world.EntityManager.HasBuffer<TComponent>(entity))
            {
                var buffer = world.EntityManager.GetBuffer<TComponent>(entity);
                buffer.Clear();
            }
        }
    }
}
