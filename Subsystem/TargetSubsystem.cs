
using System.Collections.Generic;
using OneBitRob.AI;
using UnityEngine;

public class TargetSubsystem : MonoBehaviour
{
    public HashSet<GameObject> PotentialTargets { get; private set; } = new HashSet<GameObject>();

    public float NextTargetCheckTime { get; set; }

    private LayerMask targetLayerMask;
    private float autoTargetDetectionRange;
    private float retargetCheckInterval;

    public void Initialize(UnitBrain brain)
    {
        targetLayerMask = brain.GetTargetLayerMask();
        autoTargetDetectionRange = brain.UnitDefinition.autoTargetDetectionRange;
        retargetCheckInterval = brain.UnitDefinition.retargetCheckInterval;

        // Configure detection trigger
        var triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = autoTargetDetectionRange;

        // Initial population of potential targets
        var colliders = Physics.OverlapSphere(transform.position, 100f, targetLayerMask);
        foreach (var col in colliders)
        {
            if (col.transform != transform)
            {
                PotentialTargets.Add(col.gameObject);
            }
        }

        // Stagger refind timers to avoid spikes
        NextTargetCheckTime = Time.time + UnityEngine.Random.Range(0f, retargetCheckInterval);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayerMask) != 0)
        {
            PotentialTargets.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PotentialTargets.Remove(other.gameObject);
    }
}