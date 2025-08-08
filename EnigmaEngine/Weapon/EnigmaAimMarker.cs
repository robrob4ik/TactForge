using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    public class EnigmaAimMarker : MonoBehaviour
    {
        public enum MovementModes
        {
            Instant,
            Interpolate
        }

        [Title("Movement")]
        [Tooltip("The selected movement mode for this aim marker. Instant will move the marker instantly to its target, Interpolate will animate its position over time")]
        public MovementModes MovementMode;
        
        [Tooltip("An offset to apply to the position of the target (useful if you want, for example, the marker to appear above the target)")]
        public Vector3 Offset;

        [ShowIf("@MovementMode == MovementModes.Interpolate")]
        [Tooltip("When in Interpolate mode, the duration of the movement animation")]
        public float MovementDuration = 0.2f;

        [ShowIf("@MovementMode == MovementModes.Interpolate")]
        [Tooltip("When in Interpolate mode, the curve to animate the movement on")]
        public MMTween.MMTweenCurve MovementCurve = MMTween.MMTweenCurve.EaseInCubic;

        [ShowIf("@MovementMode == MovementModes.Interpolate")]
        [Tooltip("When in Interpolate mode, the delay before the marker moves when changing target")]
        public float MovementDelay = 0f;
        
        [Title("Feedbacks")]
        [Tooltip("A feedback to play when a target is found and we didn't have one already")]
        public MMFeedbacks FirstTargetFeedback;

        [Tooltip("A feedback to play when we already had a target and just found a new one")]
        public MMFeedbacks NewTargetAssignedFeedback;

        [Tooltip("A feedback to play when no more targets are found, and we just lost our last target")]
        public MMFeedbacks NoMoreTargetFeedback;

        protected Transform _target;
        protected Transform _targetLastFrame = null;
        protected WaitForSeconds _movementDelayWFS;
        protected float _lastTargetChangeAt = 0f;

        
        protected virtual void Awake()
        {
            FirstTargetFeedback?.Initialization(this.gameObject);
            NewTargetAssignedFeedback?.Initialization(this.gameObject);
            NoMoreTargetFeedback?.Initialization(this.gameObject);
            if (MovementDelay > 0f)
            {
                _movementDelayWFS = new WaitForSeconds(MovementDelay);
            }
        }


        /// On Update we check if we've changed target, and follow it if needed
        protected virtual void Update()
        {
            HandleTargetChange();
            FollowTarget();
            _targetLastFrame = _target;
        }


        /// Makes this object follow the target's position
        protected virtual void FollowTarget()
        {
            if (MovementMode == MovementModes.Instant)
            {
                this.transform.position = _target.transform.position + Offset;
            }
            else
            {
                if ((_target != null) && (Time.time - _lastTargetChangeAt > MovementDuration))
                {
                    this.transform.position = _target.transform.position + Offset;
                }
            }
        }


        /// Sets a new target for this aim marker
        /// <param name="newTarget"></param>
        public virtual void SetTarget(Transform newTarget)
        {
            _target = newTarget;

            if (newTarget == null)
            {
                return;
            }

            this.gameObject.SetActive(true);

            if (_targetLastFrame == null)
            {
                this.transform.position = _target.transform.position + Offset;
            }

            if (MovementMode == MovementModes.Instant)
            {
                this.transform.position = _target.transform.position + Offset;
            }
            else
            {
                MMTween.MoveTransform(this, this.transform, this.transform.position, _target.transform.position + Offset, _movementDelayWFS, MovementDelay, MovementDuration, MovementCurve);
            }
        }


        /// Checks for target changes and triggers the appropriate methods if needed
        protected virtual void HandleTargetChange()
        {
            if (_target == _targetLastFrame)
            {
                return;
            }

            _lastTargetChangeAt = Time.time;

            if (_target == null)
            {
                NoMoreTargets();
                return;
            }

            if (_targetLastFrame == null)
            {
                FirstTargetFound();
                return;
            }

            if ((_targetLastFrame != null) && (_target != null))
            {
                NewTargetFound();
            }
        }


        /// When no more targets are found, and we just lost one, we play a dedicated feedback
        protected virtual void NoMoreTargets()
        {
            NoMoreTargetFeedback?.PlayFeedbacks();
        }


        /// When a new target is found and we didn't have one already, we play a dedicated feedback
        protected virtual void FirstTargetFound()
        {
            FirstTargetFeedback?.PlayFeedbacks();
        }


        /// When a new target is found, and we previously had another, we play a dedicated feedback
        protected virtual void NewTargetFound()
        {
            NewTargetAssignedFeedback?.PlayFeedbacks();
        }


        /// Hides this object
        public virtual void Disable()
        {
            this.gameObject.SetActive(false);
        }
    }
}