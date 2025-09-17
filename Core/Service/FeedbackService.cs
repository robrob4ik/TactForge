// File: Assets/PROJECT/Scripts/Core/Service/FeedbackService.cs
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    public static class FeedbackService
    {
        /// <summary>
        /// Play an MMFeedbacks effect from a pool id (preferred) or a fallback prefab.
        /// Now uses PoolHub.GetPooled(...) which is exception-safe against broken pool lists.
        /// </summary>
        public static bool TryPlay(FeedbackDefinition definition, Transform attach, Vector3 worldPosition, float? overrideIntensity = null)
        {
            if (definition == null) return false;

            GameObject go = null;
            bool pooled = false;

            if (definition.HasPoolId)
            {
                go = PoolHub.GetPooled(PoolKind.Feedback, definition.poolId); // SAFE pooled retrieval
                pooled = go != null;
            }

            if (go == null && definition.fallbackPrefab != null)
            {
                go = Object.Instantiate(definition.fallbackPrefab);
                pooled = false;
            }
            if (go == null) return false;

            // Placement / parenting
            if (definition.attachToTarget && attach != null)
            {
                go.transform.SetParent(attach, worldPositionStays: false);
                go.transform.localPosition = definition.offset;
                go.transform.localRotation = definition.inheritRotation ? attach.localRotation : Quaternion.identity;
            }
            else
            {
                go.transform.SetParent(null);
                go.transform.SetPositionAndRotation(
                    worldPosition + definition.offset,
                    (definition.inheritRotation && attach != null) ? attach.rotation : go.transform.rotation
                );
            }

            var poolable = go.GetComponent<MMPoolableObject>();
            if (!go.activeSelf) go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();

            var player = go.GetComponent<MMFeedbacks>();
            if (player == null)
            {
                // Return to pool / cleanup gracefully
                if (pooled)
                {
                    if (poolable != null) poolable.Destroy();
                    else go.SetActive(false);
                }
                else Object.Destroy(go);
                return false;
            }

            var owner = (definition.attachToTarget && attach != null) ? attach.gameObject : go;
            player.Initialization(owner);

            var intensity = overrideIntensity.HasValue ? overrideIntensity.Value : definition.intensity;
            player.PlayFeedbacks(go.transform.position, intensity, false);

            float duration = player.TotalDuration;
            if (duration <= 0f) duration = definition.minAutoReleaseSeconds;
            duration += definition.autoReleasePadding;

            FeedbackServiceRunner.Instance.ReleaseAfter(go, poolable, pooled, duration);
            return true;
        }

        /// <summary>
        /// Non-destructive rescue of pooled FX parented under 'root': simply re-parent them to world.
        /// Lets them keep playing & auto-release later, avoiding destruction along with the unit.
        /// </summary>
        public static void RescuePooledChildren(Transform root)
        {
            if (root == null) return;

            // Re-parent ANY pooled FX (MMPoolableObject) away from the dying unit.
            var poolables = root.GetComponentsInChildren<MMPoolableObject>(true);
            for (int i = 0; i < poolables.Length; i++)
            {
                var p = poolables[i];
                if (p == null) continue;
                // Avoid touching the unit itself even if it had an MMPoolableObject (unlikely)
                if (p.transform == root) continue;
                try
                {
                    p.transform.SetParent(null, true); // keep active, world-space
                }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Legacy: Detach by marker OR generically return child pooled FX to pool.
        /// Prefer RescuePooledChildren at unit cleanup to keep death FX visible.
        /// </summary>
        public static void DetachAllPooledChildren(Transform root)
        {
            if (root == null) return;

            // Marker-based detach (if present on FX)
            var markers = root.GetComponentsInChildren<PooledFxAttachment>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null) markers[i].DetachToPoolRoot();
            }

            // Generic fallback: return any orphan poolables to pool
            var poolables = root.GetComponentsInChildren<MMPoolableObject>(true);
            for (int i = 0; i < poolables.Length; i++)
            {
                var p = poolables[i];
                if (p == null) continue;
                if (p.transform == root) continue;
                try { p.Destroy(); } catch { /* ignore */ }
            }
        }
    }

    internal sealed class FeedbackServiceRunner : MonoBehaviour
    {
        private static FeedbackServiceRunner _instance;
        public static FeedbackServiceRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[FeedbackServiceRunner]");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<FeedbackServiceRunner>();
                return _instance;
            }
        }

        public void ReleaseAfter(GameObject go, MMPoolableObject poolable, bool pooled, float delay)
            => StartCoroutine(ReleaseCo(go, poolable, pooled, delay));

        private static IEnumerator ReleaseCo(GameObject go, MMPoolableObject poolable, bool pooled, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (pooled)
            {
                if (poolable != null) poolable.Destroy();
                else if (go != null) go.SetActive(false);
            }
            else
            {
                if (go != null) Object.Destroy(go);
            }
        }
    }
}
