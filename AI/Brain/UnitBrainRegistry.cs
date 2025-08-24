// Runtime/AI/Brain/UnitBrainRegistry.cs
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    /// Fast bi‑directional lookup between ECS Entity and Mono UnitBrain/GameObject.
    public static class UnitBrainRegistry
    {
        public readonly struct EntityKey
        {
            public readonly int Index;
            public readonly int Version;
            public EntityKey(Entity e) { Index = e.Index; Version = e.Version; }
        }

        // ECS -> Brain
        static readonly Dictionary<EntityKey, UnitBrain> _entityToBrain = new(256);

        // GO -> Entity
        static readonly Dictionary<int, Entity> _goToEntity = new(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnDomainReload()
        {
            _entityToBrain.Clear();
            _goToEntity.Clear();
        }

        public static void Register(Entity entity, UnitBrain brain)
        {
            if (!brain) return;
            _entityToBrain[new EntityKey(entity)] = brain;
            _goToEntity[brain.gameObject.GetInstanceID()] = entity;
        }

        public static void Unregister(Entity entity, GameObject go = null)
        {
            _entityToBrain.Remove(new EntityKey(entity));
            if (go) _goToEntity.Remove(go.GetInstanceID());
        }

        public static UnitBrain Get(Entity entity) =>
            _entityToBrain.TryGetValue(new EntityKey(entity), out var brain) ? brain : null;

        public static bool TryGet(Entity entity, out UnitBrain brain) =>
            _entityToBrain.TryGetValue(new EntityKey(entity), out brain);

        public static bool TryGetEntity(GameObject go, out Entity ent)
        {
            ent = Entity.Null; // ✅ ensure assigned on all paths
            if (!go) return false;
            return _goToEntity.TryGetValue(go.GetInstanceID(), out ent);
        }

        public static Entity GetEntity(GameObject go) =>
            TryGetEntity(go, out var ent) ? ent : Entity.Null;

        public static GameObject GetGameObject(Entity entity)
        {
            var brain = Get(entity);
            return brain ? brain.gameObject : null;
        }

#if UNITY_EDITOR
        public static IEnumerable<(int index, int version, UnitBrain brain)> Debug_All()
        {
            foreach (var kv in _entityToBrain)
                if (kv.Value != null)
                    yield return (kv.Key.Index, kv.Key.Version, kv.Value);
        }
#endif
    }
}
