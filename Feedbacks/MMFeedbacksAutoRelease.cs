// FILE: Assets/PROJECT/Scripts/Runtime/FX/Feedbacks/MMFeedbacksAutoRelease.cs
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace OneBitRob.FX
{
    [DisallowMultipleComponent]
    internal sealed class MMFeedbacksAutoRelease : MonoBehaviour
    {
        private Coroutine _co;

        public void Arm(MMFeedbacks player, float minSeconds, float paddingSeconds)
        {
            if (!isActiveAndEnabled) return;
            if (_co != null) StopCoroutine(_co);

            float baseDur = (player != null) ? Mathf.Max(0f, player.TotalDuration) : 0f;
            float wait = Mathf.Max(minSeconds, baseDur + paddingSeconds);

            // Always use realtime to avoid stalls on global timescale tricks
            _co = StartCoroutine(ReleaseAfter(wait));
        }

        private IEnumerator ReleaseAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
            var poolable = GetComponent<MMPoolableObject>();
            if (poolable != null) poolable.Destroy();
            else Destroy(gameObject);
        }
    }
}