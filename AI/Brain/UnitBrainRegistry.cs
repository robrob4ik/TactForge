using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    /// Fast bi‑directional lookup between ECS Entity and Mono UnitBrain/GameObject.
    public static class UnitBrainRegistry
    {
        // ECS -> Brain
        static readonly Dictionary<(int idx, int ver), UnitBrain> _entityToBrain = new();

        // GO -> Entity
        static readonly Dictionary<int, Entity> _goToEntity = new();

        public static void Register(Entity entity, UnitBrain brain)
        {
            _entityToBrain[(entity.Index, entity.Version)] = brain;
            _goToEntity[brain.gameObject.GetInstanceID()] = entity;
        }

        public static void Unregister(Entity entity, GameObject go = null)
        {
            _entityToBrain.Remove((entity.Index, entity.Version));
            if (go) _goToEntity.Remove(go.GetInstanceID());
        }

        public static UnitBrain Get(Entity entity) =>
            _entityToBrain.TryGetValue((entity.Index, entity.Version), out var brain) ? brain : null;

        public static Entity GetEntity(GameObject go) =>
            go && _goToEntity.TryGetValue(go.GetInstanceID(), out var ent) ? ent : Entity.Null;

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
                    yield return (kv.Key.idx, kv.Key.ver, kv.Value);
        }
#endif
    }
}