using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
namespace OneBitRob.EnigmaEngine
{
    public class EnigmaProgressBar : MonoBehaviour
    {
        public enum MMProgressBarStates { Idle, Decreasing, Increasing, InDecreasingDelay, InIncreasingDelay }
        public enum FillModes { LocalScale, FillAmount, Width, Height, Anchor }
        public enum BarDirections { LeftToRight, RightToLeft, UpToDown, DownToUp }
        public enum TimeScales { UnscaledTime, Time }
        public enum BarFillModes { SpeedBased, FixedDuration }

        [FoldoutGroup("Bindings"), Tooltip("The ID of the player this progress bar is associated with.")]
        public string PlayerID;
        [FoldoutGroup("Bindings"), Tooltip("The foreground bar transform to manipulate.")]
        public Transform ForegroundBar;
        [FoldoutGroup("Bindings"), Tooltip("The delayed decreasing bar transform to manipulate.")]
        public Transform DelayedBarDecreasing;

        [FoldoutGroup("Fill Settings"), Tooltip("The minimum fill value for the bar (between 0 and 1).")]
        [Range(0f, 1f)]
        public float MinimumBarFillValue = 0f;
        [FoldoutGroup("Fill Settings"), Tooltip("The maximum fill value for the bar (between 0 and 1).")]
        [Range(0f, 1f)]
        public float MaximumBarFillValue = 1f;
        [FoldoutGroup("Fill Settings"), Tooltip("Whether to set an initial fill value on start.")]
        public bool SetInitialFillValueOnStart = false;
        [FoldoutGroup("Fill Settings"), MMCondition("SetInitialFillValueOnStart", true), Tooltip("The initial fill value if SetInitialFillValueOnStart is true.")]
        [Range(0f, 1f)]
        public float InitialFillValue = 0f;
        [FoldoutGroup("Fill Settings"), Tooltip("The direction the bar fills in.")]
        public BarDirections BarDirection = BarDirections.LeftToRight;
        [FoldoutGroup("Fill Settings"), Tooltip("The mode used to fill the bar (scale, fill amount, etc.).")]
        public FillModes FillMode = FillModes.LocalScale;
        [FoldoutGroup("Fill Settings"), Tooltip("Whether to use scaled or unscaled time for animations.")]
        public TimeScales TimeScale = TimeScales.UnscaledTime;
        [FoldoutGroup("Fill Settings"), Tooltip("The fill mode (speed-based or fixed duration).")]
        public BarFillModes BarFillMode = BarFillModes.SpeedBased;

        [FoldoutGroup("Foreground Bar Settings"), Tooltip("Whether to lerp the foreground bar's fill.")]
        public bool LerpForegroundBar = true;
        [FoldoutGroup("Foreground Bar Settings"), MMCondition("LerpForegroundBar", true), Tooltip("The speed of the foreground bar lerp.")]
        public float LerpForegroundBarSpeed = 15f;
        [FoldoutGroup("Foreground Bar Settings"), MMCondition("LerpForegroundBar", true), Tooltip("The duration of the foreground bar lerp.")]
        public float LerpForegroundBarDuration = 0.2f;
        [FoldoutGroup("Foreground Bar Settings"), MMCondition("LerpForegroundBar", true), Tooltip("The curve for the foreground bar lerp.")]
        public AnimationCurve LerpForegroundBarCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [FoldoutGroup("Delayed Bar Decreasing"), Tooltip("The delay before the delayed bar starts decreasing.")]
        public float DecreasingDelay = 1f;
        [FoldoutGroup("Delayed Bar Decreasing"), Tooltip("Whether to lerp the delayed decreasing bar.")]
        public bool LerpDecreasingDelayedBar = true;
        [FoldoutGroup("Delayed Bar Decreasing"), MMCondition("LerpDecreasingDelayedBar", true), Tooltip("The speed of the delayed bar lerp.")]
        public float LerpDecreasingDelayedBarSpeed = 15f;
        [FoldoutGroup("Delayed Bar Decreasing"), MMCondition("LerpDecreasingDelayedBar", true), Tooltip("The duration of the delayed bar lerp.")]
        public float LerpDecreasingDelayedBarDuration = 0.2f;
        [FoldoutGroup("Delayed Bar Decreasing"), MMCondition("LerpDecreasingDelayedBar", true), Tooltip("The curve for the delayed bar lerp.")]
        public AnimationCurve LerpDecreasingDelayedBarCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [FoldoutGroup("Bump"), Tooltip("Whether to bump the bar on value increase.")]
        public bool BumpOnIncrease = false;
        [FoldoutGroup("Bump"), Tooltip("Whether to bump the bar on value decrease.")]
        public bool BumpOnDecrease = false;
        [FoldoutGroup("Bump"), Tooltip("The duration of the bump animation.")]
        public float BumpDuration = 0.2f;
        [FoldoutGroup("Bump"), Tooltip("Whether to change color during bump.")]
        public bool ChangeColorWhenBumping = true;
        [FoldoutGroup("Bump"), Tooltip("Whether to store the bar color on play.")]
        public bool StoreBarColorOnPlay = true;
        [FoldoutGroup("Bump"), MMCondition("ChangeColorWhenBumping", true), Tooltip("The color to apply during bump.")]
        public Color BumpColor = Color.white;
        [FoldoutGroup("Bump"), Tooltip("The scale animation curve for the bump.")]
        public AnimationCurve BumpScaleAnimationCurve = new AnimationCurve(new Keyframe(1, 1), new Keyframe(0.3f, 1.05f), new Keyframe(1, 1));
        [FoldoutGroup("Bump"), Tooltip("The color animation curve for the bump.")]
        public AnimationCurve BumpColorAnimationCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 1f), new Keyframe(1, 0));
        [FoldoutGroup("Bump"), Tooltip("Whether to apply an intensity multiplier to the bump.")]
        public bool ApplyBumpIntensityMultiplier = false;
        [FoldoutGroup("Bump"), MMCondition("ApplyBumpIntensityMultiplier", true), Tooltip("The intensity multiplier curve for the bump.")]
        public AnimationCurve BumpIntensityMultiplier = new AnimationCurve(new Keyframe(-1, 1), new Keyframe(1, 1));
        public virtual bool Bumping { get; protected set; }

        [FoldoutGroup("Debug Read Only"), ReadOnly, Tooltip("The current progress of the bar (read-only).")]
        [Range(0f, 1f)]
        public float BarProgress;
        [FoldoutGroup("Debug Read Only"), ReadOnly, Tooltip("The target progress of the bar (read-only).")]
        [Range(0f, 1f)]
        public float BarTarget;
        [FoldoutGroup("Debug Read Only"), ReadOnly, Tooltip("The progress of the delayed decreasing bar (read-only).")]
        [Range(0f, 1f)]
        public float DelayedBarDecreasingProgress;

        protected bool _initialized;
        protected Vector2 _initialBarSize;
        protected Color _initialColor;
        protected Vector3 _initialScale;
        protected Image _foregroundImage;
        protected Image _delayedDecreasingImage;
        protected Vector3 _targetLocalScale = Vector3.one;
        protected float _newPercent;
        protected float _percentLastTimeBarWasUpdated;
        protected float _lastUpdateTimestamp;
        protected float _time;
        protected float _deltaTime;
        protected int _direction;
        protected Coroutine _coroutine;
        protected bool _coroutineShouldRun = false;
        protected bool _isDelayedBarDecreasingNotNull;
        protected bool _actualUpdate;
        protected Vector2 _anchorVector;
        protected float _delayedBarDecreasingProgress;
        protected MMProgressBarStates CurrentState = MMProgressBarStates.Idle;
        protected bool _isForegroundBarNotNull;
        protected bool _isForegroundImageNotNull;

        public virtual void UpdateBar01(float normalizedValue)
        {
            UpdateBar(Mathf.Clamp01(normalizedValue), 0f, 1f);
        }

        public virtual void UpdateBar(float currentValue, float minValue, float maxValue)
        {
            if (!_initialized)
            {
                Initialization();
            }

            if (StoreBarColorOnPlay)
            {
                StoreInitialColor();
            }

            if (!this.gameObject.activeInHierarchy)
            {
                this.gameObject.SetActive(true);
            }

            _newPercent = MMMaths.Remap(currentValue, minValue, maxValue, MinimumBarFillValue, MaximumBarFillValue);

            _actualUpdate = (BarTarget != _newPercent);

            if (!_actualUpdate)
            {
                return;
            }

            if (CurrentState != MMProgressBarStates.Idle)
            {
                if ((CurrentState == MMProgressBarStates.Decreasing) ||
                    (CurrentState == MMProgressBarStates.InDecreasingDelay))
                {
                    if (_newPercent >= BarTarget)
                    {
                        StopCoroutine(_coroutine);
                        SetBar01(BarTarget);
                    }
                }
                if ((CurrentState == MMProgressBarStates.Increasing) ||
                    (CurrentState == MMProgressBarStates.InIncreasingDelay))
                {
                    if (_newPercent <= BarTarget)
                    {
                        StopCoroutine(_coroutine);
                        SetBar01(BarTarget);
                    }
                }
            }

            _percentLastTimeBarWasUpdated = BarProgress;
            _delayedBarDecreasingProgress = DelayedBarDecreasingProgress;

            BarTarget = _newPercent;

            if ((_newPercent != _percentLastTimeBarWasUpdated) && !Bumping)
            {
                Bump();
            }

            DetermineDeltaTime();
            _lastUpdateTimestamp = _time;

            DetermineDirection();

            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
            _coroutineShouldRun = true;

            if (this.gameObject.activeInHierarchy)
            {
                _coroutine = StartCoroutine(UpdateBarsCo());
            }
            else
            {
                SetBar(currentValue, minValue, maxValue);
            }
        }

        public virtual void SetBar(float currentValue, float minValue, float maxValue)
        {
            float newPercent = MMMaths.Remap(currentValue, minValue, maxValue, 0f, 1f);
            SetBar01(newPercent);
        }

        public virtual void SetBar01(float newPercent)
        {
            if (!_initialized)
            {
                Initialization();
            }

            newPercent = MMMaths.Remap(newPercent, 0f, 1f, MinimumBarFillValue, MaximumBarFillValue);
            BarProgress = newPercent;
            DelayedBarDecreasingProgress = newPercent;
            BarTarget = newPercent;
            _percentLastTimeBarWasUpdated = newPercent;
            _delayedBarDecreasingProgress = DelayedBarDecreasingProgress;
            SetBarInternal(newPercent, ForegroundBar, _foregroundImage, _initialBarSize);
            SetBarInternal(newPercent, DelayedBarDecreasing, _delayedDecreasingImage, _initialBarSize);
            _coroutineShouldRun = false;
            CurrentState = MMProgressBarStates.Idle;
        }

        protected virtual void Start()
        {
            if (!_initialized)
            {
                Initialization();
            }
        }

        protected virtual void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            StoreInitialColor();
        }

        public virtual void Initialization()
        {
            BarTarget = -1f;
            _isForegroundBarNotNull = ForegroundBar != null;
            _isDelayedBarDecreasingNotNull = DelayedBarDecreasing != null;
            _initialScale = this.transform.localScale;

            if (_isForegroundBarNotNull)
            {
                _foregroundImage = ForegroundBar.GetComponent<Image>();
                _isForegroundImageNotNull = _foregroundImage != null;
                _initialBarSize = _foregroundImage.rectTransform.sizeDelta;
            }
            if (_isDelayedBarDecreasingNotNull)
            {
                _delayedDecreasingImage = DelayedBarDecreasing.GetComponent<Image>();
            }
            _initialized = true;

            StoreInitialColor();

            _percentLastTimeBarWasUpdated = BarProgress;

            if (SetInitialFillValueOnStart)
            {
                SetBar01(InitialFillValue);
            }
        }

        protected virtual void StoreInitialColor()
        {
            if (!Bumping && _isForegroundImageNotNull)
            {
                _initialColor = _foregroundImage.color;
            }
        }

        protected virtual IEnumerator UpdateBarsCo()
        {
            while (_coroutineShouldRun)
            {
                DetermineDeltaTime();
                DetermineDirection();
                UpdateBars();
                yield return null;
            }

            CurrentState = MMProgressBarStates.Idle;
            yield break;
        }

        protected virtual void DetermineDeltaTime()
        {
            _deltaTime = (TimeScale == TimeScales.Time) ? Time.deltaTime : Time.unscaledDeltaTime;
            _time = (TimeScale == TimeScales.Time) ? Time.time : Time.unscaledTime;
        }

        protected virtual void DetermineDirection()
        {
            _direction = (_newPercent > _percentLastTimeBarWasUpdated) ? 1 : -1;
        }

        protected virtual void UpdateBars()
        {
            float newFill;
            float newFillDelayed;
            float t1 = 0f;
            float t2 = 0f;

            if (_direction < 0)
            {
                newFill = ComputeNewFill(LerpForegroundBar, LerpForegroundBarSpeed, LerpForegroundBarDuration, LerpForegroundBarCurve, 0f, _percentLastTimeBarWasUpdated, out t1);
                SetBarInternal(newFill, ForegroundBar, _foregroundImage, _initialBarSize);

                BarProgress = newFill;

                CurrentState = MMProgressBarStates.Decreasing;

                if (_time - _lastUpdateTimestamp > DecreasingDelay)
                {
                    newFillDelayed = ComputeNewFill(LerpDecreasingDelayedBar, LerpDecreasingDelayedBarSpeed, LerpDecreasingDelayedBarDuration, LerpDecreasingDelayedBarCurve, DecreasingDelay, _delayedBarDecreasingProgress, out t2);
                    SetBarInternal(newFillDelayed, DelayedBarDecreasing, _delayedDecreasingImage, _initialBarSize);

                    DelayedBarDecreasingProgress = newFillDelayed;
                    CurrentState = MMProgressBarStates.InDecreasingDelay;
                }
            }
            else
            {
                newFill = ComputeNewFill(LerpForegroundBar, LerpForegroundBarSpeed, LerpForegroundBarDuration, LerpForegroundBarCurve, 0f, _percentLastTimeBarWasUpdated, out t2);
                SetBarInternal(newFill, DelayedBarDecreasing, _delayedDecreasingImage, _initialBarSize);
                SetBarInternal(newFill, ForegroundBar, _foregroundImage, _initialBarSize);

                BarProgress = newFill;
                DelayedBarDecreasingProgress = newFill;
                CurrentState = MMProgressBarStates.InDecreasingDelay;
            }

            if ((t1 >= 1f) && (t2 >= 1f))
            {
                _coroutineShouldRun = false;
            }
        }

        protected virtual float ComputeNewFill(bool lerpBar, float barSpeed, float barDuration, AnimationCurve barCurve, float delay, float lastPercent, out float t)
        {
            float newFill = 0f;
            t = 0f;
            if (lerpBar)
            {
                float delta = 0f;
                float timeSpent = _time - _lastUpdateTimestamp - delay;
                float speed = barSpeed;
                if (speed == 0f) { speed = 1f; }

                float duration = (BarFillMode == BarFillModes.FixedDuration) ? barDuration : (Mathf.Abs(_newPercent - lastPercent)) / speed;

                delta = MMMaths.Remap(timeSpent, 0f, duration, 0f, 1f);
                delta = Mathf.Clamp(delta, 0f, 1f);
                t = delta;
                if (t < 1f)
                {
                    delta = barCurve.Evaluate(delta);
                    newFill = Mathf.LerpUnclamped(lastPercent, _newPercent, delta);
                }
                else
                {
                    newFill = _newPercent;
                }
            }
            else
            {
                newFill = _newPercent;
            }

            newFill = Mathf.Clamp(newFill, 0f, 1f);

            return newFill;
        }

        protected virtual void SetBarInternal(float newAmount, Transform bar, Image image, Vector2 initialSize)
        {
            if (bar == null)
            {
                return;
            }

            switch (FillMode)
            {
                case FillModes.LocalScale:
                    _targetLocalScale = Vector3.one;
                    switch (BarDirection)
                    {
                        case BarDirections.LeftToRight:
                            _targetLocalScale.x = newAmount;
                            break;
                        case BarDirections.RightToLeft:
                            _targetLocalScale.x = 1f - newAmount;
                            break;
                        case BarDirections.DownToUp:
                            _targetLocalScale.y = newAmount;
                            break;
                        case BarDirections.UpToDown:
                            _targetLocalScale.y = 1f - newAmount;
                            break;
                    }

                    bar.localScale = _targetLocalScale;
                    break;

                case FillModes.Width:
                    if (image == null)
                    {
                        return;
                    }
                    float newSizeX = MMMaths.Remap(newAmount, 0f, 1f, 0, initialSize.x);
                    image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newSizeX);
                    break;

                case FillModes.Height:
                    if (image == null)
                    {
                        return;
                    }
                    float newSizeY = MMMaths.Remap(newAmount, 0f, 1f, 0, initialSize.y);
                    image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSizeY);
                    break;

                case FillModes.FillAmount:
                    if (image == null)
                    {
                        return;
                    }
                    image.fillAmount = newAmount;
                    break;
                case FillModes.Anchor:
                    if (image == null)
                    {
                        return;
                    }
                    switch (BarDirection)
                    {
                        case BarDirections.LeftToRight:
                            _anchorVector.x = 0f;
                            _anchorVector.y = 0f;
                            image.rectTransform.anchorMin = _anchorVector;
                            _anchorVector.x = newAmount;
                            _anchorVector.y = 1f;
                            image.rectTransform.anchorMax = _anchorVector;
                            break;
                        case BarDirections.RightToLeft:
                            _anchorVector.x = newAmount;
                            _anchorVector.y = 0f;
                            image.rectTransform.anchorMin = _anchorVector;
                            _anchorVector.x = 1f;
                            _anchorVector.y = 1f;
                            image.rectTransform.anchorMax = _anchorVector;
                            break;
                        case BarDirections.DownToUp:
                            _anchorVector.x = 0f;
                            _anchorVector.y = 0f;
                            image.rectTransform.anchorMin = _anchorVector;
                            _anchorVector.x = 1f;
                            _anchorVector.y = newAmount;
                            image.rectTransform.anchorMax = _anchorVector;
                            break;
                        case BarDirections.UpToDown:
                            _anchorVector.x = 0f;
                            _anchorVector.y = newAmount;
                            image.rectTransform.anchorMin = _anchorVector;
                            _anchorVector.x = 1f;
                            _anchorVector.y = 1f;
                            image.rectTransform.anchorMax = _anchorVector;
                            break;
                    }
                    break;
            }
        }

        public virtual void Bump()
        {
            float delta = _newPercent - _percentLastTimeBarWasUpdated;
            float intensityMultiplier = BumpIntensityMultiplier.Evaluate(delta);

            bool shouldBump = false;

            if (!_initialized)
            {
                return;
            }

            DetermineDirection();

            if (BumpOnIncrease && (_direction > 0))
            {
                shouldBump = true;
            }

            if (BumpOnDecrease && (_direction < 0))
            {
                shouldBump = true;
            }

            if (!shouldBump)
            {
                return;
            }

            if (this.gameObject.activeInHierarchy)
            {
                StartCoroutine(BumpCoroutine(intensityMultiplier));
            }
        }

        protected virtual IEnumerator BumpCoroutine(float intensityMultiplier)
        {
            float journey = 0f;

            Bumping = true;

            while (journey <= BumpDuration)
            {
                journey = journey + _deltaTime;
                float percent = Mathf.Clamp01(journey / BumpDuration);

                float curvePercent = BumpScaleAnimationCurve.Evaluate(percent);

                if (ApplyBumpIntensityMultiplier)
                {
                    float multiplier = Mathf.Abs(1f - curvePercent) * intensityMultiplier;
                    curvePercent = 1 + multiplier;
                }

                float colorCurvePercent = BumpColorAnimationCurve.Evaluate(percent);
                this.transform.localScale = curvePercent * _initialScale;

                if (ChangeColorWhenBumping && _isForegroundImageNotNull)
                {
                    _foregroundImage.color = Color.Lerp(_initialColor, BumpColor, colorCurvePercent);
                }
                yield return null;
            }
            if (ChangeColorWhenBumping && _isForegroundImageNotNull)
            {
                _foregroundImage.color = _initialColor;
            }
            Bumping = false;
            yield return null;
        }

        public virtual void ShowBar()
        {
            this.gameObject.SetActive(true);
        }

        public virtual void HideBar(float delay)
        {
            if (delay <= 0)
            {
                this.gameObject.SetActive(false);
            }
            else if (this.gameObject.activeInHierarchy)
            {
                StartCoroutine(HideBarCo(delay));
            }
        }

        protected virtual IEnumerator HideBarCo(float delay)
        {
            yield return MMCoroutine.WaitFor(delay);
            this.gameObject.SetActive(false);
        }
    }
}
