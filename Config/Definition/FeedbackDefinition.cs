using UnityEngine;

namespace OneBitRob.FX
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Feedback Definition", fileName = "FeedbackDefinition")]
    public sealed class FeedbackDefinition : ScriptableObject
    {
        [Header("Pool (preferred) + Fallback")]
        [Tooltip("Pool key to resolve in FeedbackPoolManager. Use pooling for common/short feedbacks.")]
        public string poolId = "";
        [Tooltip("Optional fallback prefab with MMFeedbacks on root, used if poolId is empty or not found.")]
        public GameObject fallbackPrefab;

        [Header("Placement")]
        [Tooltip("When true, the spawned feedback will be parented to the provided attach Transform.")]
        public bool attachToTarget = false;
        [Tooltip("Local offset when attached; world offset when not attached.")]
        public Vector3 offset = Vector3.zero;
        [Tooltip("Copy rotation from the attach transform (if any).")]
        public bool inheritRotation = false;

        [Header("Playback")]
        [Tooltip("Intensity passed to MMFeedbacks.PlayFeedbacks(). 1 = default.")]
        [Min(0f)] public float intensity = 1f;
        [Tooltip("If TotalDuration is 0 or unknown, keep the instance alive for at least this many seconds.")]
        [Min(0f)] public float minAutoReleaseSeconds = 0.6f;
        [Tooltip("Extra padding added to TotalDuration for safe auto-release.")]
        [Min(0f)] public float autoReleasePadding = 0.1f;

        internal bool HasPoolId => !string.IsNullOrEmpty(poolId);
    }
}