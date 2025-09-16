using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    public static class FeedbackService
    {
        /// <summary>
        /// Plays a feedback instance. If definition.HasPoolId resolves, uses MM pooler; otherwise falls back to instantiate.
        /// Parent/placement logic is unchanged (nested if attachToTarget).
        /// Adds PooledFxAttachment so cleanup can detach to pool root before unit destruction.
        /// </summary>
        public static bool TryPlay(FeedbackDefinition definition, Transform attach, Vector3 worldPosition, float? overrideIntensity = null)
        {
            if (definition == null) return false;

            GameObject go = null;
            MMObjectPooler pooler = null;
            bool pooled = false;

            // 1) Resolve pooled instance if possible
            if (definition.HasPoolId)
            {
                pooler = FeedbackPoolManager.GetPooler(definition.poolId);
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

            if (go == null) return false;

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
                go.transform.SetPositionAndRotation(
                    worldPosition + definition.offset,
                    (definition.inheritRotation && attach != null) ? attach.rotation : go.transform.rotation
                );
            }

            // Mark pooled FX so we can safely detach them on unit death
            var marker = go.GetComponent<PooledFxAttachment>() ?? go.AddComponent<PooledFxAttachment>();
            marker.PoolRoot = pooler != null ? pooler.transform : null;

            if (!go.activeSelf) go.SetActive(true);
            var poolable = go.GetComponent<MMPoolableObject>();
            poolable?.TriggerOnSpawnComplete();

            var player = go.GetComponent<MMFeedbacks>();
            if (player == null)
            {
                if (pooled) { if (poolable != null) poolable.Destroy(); else go.SetActive(false); }
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

        /// <summary>Detach all pooled FX under 'root' back to their pools (used right before destroying a unit).</summary>
        public static void DetachAllPooledChildren(Transform root)
        {
            if (root == null) return;
            var markers = root.GetComponentsInChildren<PooledFxAttachment>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m != null) m.DetachToPoolRoot();
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
                DontDestroyOnLoad(go);
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
