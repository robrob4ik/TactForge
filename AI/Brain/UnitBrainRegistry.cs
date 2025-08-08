using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    /// <summary>
    /// Fast bi‑directional lookup between ECS <see cref="Entity"/> and the
    /// MonoBehaviour‑side <see cref="UnitBrain"/> / <see cref="GameObject"/>.
    /// • Keyed by Entity.Index for zero‑alloc hashing
    /// • Thread‑safe for ‘read’ because MoveToTargetSystem runs on the main thread;
    ///   add your own locking if you read it from worker threads elsewhere.
    /// </summary>
    public static class UnitBrainRegistry
    {
        /* ECS‑>Brain */
        static readonly Dictionary<int, UnitBrain> _entityToBrain = new();

        /* GO‑>Entity  (needed for high‑perf distance calc in MoveToTargetSystem) */
        static readonly Dictionary<int, Entity>    _goToEntity    = new();

        /// <summary>Register a freshly baked UnitBrain.</summary>
        public static void Register(Entity entity, UnitBrain brain)
        {
            int eIdx = entity.Index;
            _entityToBrain[eIdx] = brain;

            int goID = brain.gameObject.GetInstanceID();
            _goToEntity[goID] = entity;
        }

        /// <summary>Lookup the <see cref="UnitBrain"/> owning an Entity.</summary>
        public static UnitBrain Get(Entity entity)
        {
            return _entityToBrain.TryGetValue(entity.Index, out var brain) ? brain : null;
        }

        /// <summary>Fast reverse lookup: get the ECS Entity for a GameObject.</summary>
        public static Entity GetEntity(GameObject go)
        {
            return go && _goToEntity.TryGetValue(go.GetInstanceID(), out var ent)
                ? ent
                : Entity.Null;
        }

        /// <summary>Cleanup when the MonoBehaviour is destroyed.</summary>
        public static void Unregister(Entity entity, GameObject go = null)
        {
            _entityToBrain.Remove(entity.Index);

            if (go)
                _goToEntity.Remove(go.GetInstanceID());
        }
    }
}