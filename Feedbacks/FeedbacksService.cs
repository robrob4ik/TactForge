using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace OneBitRob.FX
{
    /// <summary>
    /// Plays pooled MMFeedbacks with correct activation, placement and auto-release.
    /// </summary>
    public static class FeedbackService
    {
        /// <param name="definition">FeedbackDefinition that references a pool id and/or fallback prefab.</param>
        /// <param name="attach">Optional attachment transform (e.g., the unit).</param>
        /// <param name="worldPosition">World position hint (e.g., muzzle or impact). Used when not attaching.</param>
        /// <param name="overrideIntensity">Optional intensity override.</param>
        public static bool TryPlay(FeedbackDefinition definition, Transform attach, Vector3 worldPosition, float? overrideIntensity = null)
        {
            if (definition == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[FeedbackService] Null FeedbackDefinition passed.");
#endif
                return false;
            }

            GameObject go = null;
            MMObjectPooler pooler = null;
            bool pooled = false;

            // 1) Resolve pooled instance if possible
            if (definition.HasPoolId)
            {
                pooler = FeedbackPoolManager.Resolve(definition.poolId); // ← your existing resolver
                if (pooler != null)
                {
                    go = pooler.GetPooledGameObject();
                    pooled = true;
                }
            }

            // 2) Fallback instantiate (non-pooled)
            if (go == null && definition.fallbackPrefab != null)
            {
                go = Object.Instantiate(definition.fallbackPrefab);
                pooled = false;
            }

            if (go == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[FeedbackService] No pooled object or fallback prefab for '{definition.name}' (poolId='{definition.poolId}').");
#endif
                return false;
            }

            // 3) Place & parent BEFORE activation so initial OnEnable effects use correct transform
            if (definition.attachToTarget && attach != null)
            {
                go.transform.SetParent(attach, worldPositionStays: false);
                go.transform.localPosition = definition.offset;
                go.transform.localRotation = definition.inheritRotation ? attach.localRotation : Quaternion.identity;
            }
            else
            {
                go.transform.SetParent(null);
                go.transform.SetPositionAndRotation(worldPosition + definition.offset,
                    (definition.inheritRotation && attach != null) ? attach.rotation : go.transform.rotation);
            }

            // 4) Activate pooled instance
            if (!go.activeSelf) go.SetActive(true);
            var poolable = go.GetComponent<MMPoolableObject>();
            poolable?.TriggerOnSpawnComplete();

            // 5) Locate player & play
            var player = go.GetComponent<MMFeedbacks>();
            if (player == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[FeedbackService] MMFeedbacks not found on pooled object '{go.name}'.");
#endif
                // Fail-safe: if pooled, return it; else destroy
                if (pooled) { if (poolable != null) poolable.Destroy(); else go.SetActive(false); }
                else Object.Destroy(go);
                return false;
            }

            // Initialization ties owner for channels/position bind
            var owner = (definition.attachToTarget && attach != null) ? attach.gameObject : go;
            player.Initialization(owner);

            var intensity = overrideIntensity.HasValue ? overrideIntensity.Value : definition.intensity;

#if UNITY_EDITOR
            Debug.Log($"[FeedbackService] PLAY '{definition.name}' at {go.transform.position} (pooled={pooled})");
#endif
            player.PlayFeedbacks(go.transform.position, intensity, false);

            // 6) Auto-release after playback duration (or min guard)
            float duration = player.TotalDuration;
            if (duration <= 0f) duration = definition.minAutoReleaseSeconds;
            duration += definition.autoReleasePadding;

            FeedbackServiceRunner.Instance.ReleaseAfter(go, poolable, pooled, duration);
            return true;
        }
    }

    /// <summary>
    /// Coroutine host for auto-releasing feedback instances.
    /// </summary>
    internal sealed class FeedbackServiceRunner : MonoBehaviour
    {
        private static FeedbackServiceRunner _instance;
        public static FeedbackServiceRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[FeedbackServiceRunner]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<FeedbackServiceRunner>();
                return _instance;
            }
        }

        public void ReleaseAfter(GameObject go, MMPoolableObject poolable, bool pooled, float delay)
            => StartCoroutine(ReleaseCo(go, poolable, pooled, delay));

        private static IEnumerator ReleaseCo(GameObject go, MMPoolableObject poolable, bool pooled, float delay)
        {
            // Let the last audio tail ring; we don't force a fade here
            yield return new WaitForSeconds(delay);

            if (pooled)
            {
                if (poolable != null) poolable.Destroy(); // returns to pool (MMTools)
                else go.SetActive(false);
            }
            else
            {
                if (go != null) Object.Destroy(go);
            }
        }
    }
}
