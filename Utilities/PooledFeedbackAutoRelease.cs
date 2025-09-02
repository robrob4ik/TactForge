using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace OneBitRob.FX
{
    [DisallowMultipleComponent]
    internal sealed class PooledFeedbackAutoRelease : MonoBehaviour
    {
        private Coroutine _releaseCoroutine;

        public void Arm(MMFeedbacks player, float minSeconds, float paddingSeconds)
        {
            if (!isActiveAndEnabled) return;
            if (_releaseCoroutine != null) StopCoroutine(_releaseCoroutine);

            float baseDuration = (player != null) ? Mathf.Max(0f, player.TotalDuration) : 0f;
            float wait = Mathf.Max(minSeconds, baseDuration + paddingSeconds);

            _releaseCoroutine = StartCoroutine(ReleaseAfter(wait));
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