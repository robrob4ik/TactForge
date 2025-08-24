// Editor/AI/Brain/UnitBrain.Gizmos.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OneBitRob.AI
{
    public sealed partial class UnitBrain
    {
        private void OnDrawGizmos()
        {
            if (DebugDrawCombatGizmos && DebugAlwaysDraw) DrawCombatGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (DebugDrawCombatGizmos) DrawCombatGizmos();
        }

        private void DrawCombatGizmos()
        {
            var pos = transform.position;

            // Target detection range
            if (UnitDefinition != null && UnitDefinition.targetDetectionRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
                Handles.color = Gizmos.color;
                Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.targetDetectionRange);
            }

            // Weapon attack range
            if (UnitDefinition != null && UnitDefinition.weapon != null && UnitDefinition.weapon.attackRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
                Handles.color = Gizmos.color;
                Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.weapon.attackRange);
            }

            // Stopping distance
            if (UnitDefinition != null)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.65f);
                Handles.color = Gizmos.color;
                Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.stoppingDistance);
            }

            // Retarget hysteresis
            if (UnitDefinition != null && UnitDefinition.autoTargetMinSwitchDistance > 0f)
            {
                Gizmos.color = new Color(0.35f, 0.6f, 1f, 0.5f);
                Handles.color = Gizmos.color;
                Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.autoTargetMinSwitchDistance);
            }

            // Desired destination
            if (CurrentTargetPosition != default)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, CurrentTargetPosition);
                Gizmos.DrawSphere(CurrentTargetPosition, 0.08f);
            }

            // Current target
            if (CurrentTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, CurrentTarget.transform.position);
                Gizmos.DrawSphere(CurrentTarget.transform.position, 0.07f);
            }

            // Facing
            if (DebugDrawFacing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos + Vector3.up * 0.05f, transform.forward * 0.9f);
            }

            // Spell helpers (subset)
            if (DebugDrawSpell && UnitDefinition != null && UnitDefinition.unitSpells != null && UnitDefinition.unitSpells.Count > 0)
            {
                var sd = UnitDefinition.unitSpells[0];
                if (sd != null && sd.Range > 0f)
                {
                    Gizmos.color = new Color(sd.DebugColor.r, sd.DebugColor.g, sd.DebugColor.b, 0.35f);
                    Handles.color = Gizmos.color;
                    Handles.DrawWireDisc(pos, Vector3.up, sd.Range);
                }
            }
        }
    }
}
#endif
