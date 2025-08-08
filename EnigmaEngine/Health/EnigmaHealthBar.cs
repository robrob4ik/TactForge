using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OneBitRob.EnigmaEngine
{
    [ExecuteInEditMode]
    public class EnigmaHealthBar : MonoBehaviour
    {
        public enum TimeScales { UnscaledTime, Time }

        [BoxGroup("General"), Tooltip("Whether to use scaled or unscaled time for the health bar.")]
        public TimeScales TimeScale = TimeScales.UnscaledTime;

        [BoxGroup("Layout"), OnValueChanged("UpdateBarSize"), Tooltip("The size of the health bar.")]
        public Vector2 Size = new Vector2(1f, 0.2f);
        [BoxGroup("Layout"), OnValueChanged("UpdateBarSize"), Tooltip("The padding for the background.")]
        public Vector2 BackgroundPadding = new Vector2(0.01f, 0.01f);
        [BoxGroup("Layout"), OnValueChanged("UpdateBarRotation"), Tooltip("The initial rotation angles for the bar.")]
        public Vector3 InitialRotationAngles;
        [BoxGroup("Layout"), Tooltip("The update mode for following the target.")]
        public MMFollowTarget.UpdateModes FollowTargetMode = MMFollowTarget.UpdateModes.LateUpdate;
        [BoxGroup("Layout"), Tooltip("Whether to follow the target's rotation.")]
        public bool FollowRotation = false;
        [BoxGroup("Layout"), Tooltip("Whether to follow the target's scale.")]
        public bool FollowScale = true;
        [BoxGroup("Layout"), Tooltip("Whether to nest the drawn health bar under this object.")]
        public bool NestDrawnHealthBar = false;
        [BoxGroup("Layout"), Tooltip("Whether to billboard the health bar (always face camera).")]
        public bool Billboard = false;

        [BoxGroup("Colors"), OnValueChanged("UpdateBarColors"), Tooltip("The color of the foreground bar.")]
        public Color ForegroundColor = MMColors.BestRed;
        [BoxGroup("Colors"), OnValueChanged("UpdateBarColors"), Tooltip("The color of the delayed bar.")]
        public Color DelayedColor = MMColors.Orange;
        [BoxGroup("Colors"), OnValueChanged("UpdateBarColors"), Tooltip("The color of the border.")]
        public Color BorderColor = MMColors.AntiqueWhite;
        [BoxGroup("Colors"), OnValueChanged("UpdateBarColors"), Tooltip("The color of the background.")]
        public Color BackgroundColor = MMColors.Black;

        [BoxGroup("Rendering"), OnValueChanged("UpdateSortingLayer"), Tooltip("The sorting layer name for rendering.")]
        public string SortingLayerName = "UI";
        [BoxGroup("Rendering"), Tooltip("The delay for the delayed bar.")]
        public float Delay = 0.5f;
        [BoxGroup("Rendering"), Tooltip("Whether to lerp the front bar.")]
        public bool LerpFrontBar = true;
        [BoxGroup("Rendering"), Tooltip("The lerp speed for the front bar.")]
        public float LerpFrontBarSpeed = 15f;
        [BoxGroup("Rendering"), Tooltip("Whether to lerp the delayed bar.")]
        public bool LerpDelayedBar = true;
        [BoxGroup("Rendering"), Tooltip("The lerp speed for the delayed bar.")]
        public float LerpDelayedBarSpeed = 15f;

        [BoxGroup("Position"), OnValueChanged("UpdateBarPosition"), Tooltip("The offset position for the health bar relative to the target.")]
        public Vector3 HealthBarOffset = new Vector3(0f, 1f, 0f);

        [BoxGroup("Visibility"), Tooltip("Whether to hide the bar when health reaches zero.")]
        public bool HideBarAtZero = true;
        [BoxGroup("Visibility"), Tooltip("The delay before hiding the bar at zero.")]
        public float HideBarAtZeroDelay = 1f;

        [Button("Toggle Health Bar Preview"), Tooltip("Toggle the visibility of the health bar for preview in the editor.")]
        public void ToggleHealthBarPreview()
        {
            if (_progressBar == null)
            {
                Initialization();
            }
            else
            {
                ShowBar(!BarIsShown());
            }
            if (BarIsShown())
            {
                UpdateBarPosition();
                UpdateBarColors();
                UpdateBarSize();
                UpdateBarRotation();
                UpdateSortingLayer();
            }
        }

        protected EnigmaProgressBar _progressBar;
        protected MMFollowTarget _followTransform;
        protected Image _backgroundImage = null;
        protected Image _borderImage = null;
        protected Image _foregroundImage = null;
        protected Image _delayedImage = null;
        protected bool _finalHideStarted = false;

        protected virtual void Awake()
        {
            if (Application.isPlaying)
            {
                Initialization();
            }
        }

        protected void OnEnable()
        {
            _finalHideStarted = false;

            SetInitialActiveState();
        }

        public virtual void SetInitialActiveState()
        {
            if (_progressBar != null)
            {
                ShowBar(false);
            }
        }

        public virtual void ShowBar(bool state)
        {
            if (_progressBar == null) { return; }
            _progressBar.gameObject.SetActive(state);
        }

        public virtual bool BarIsShown()
        {
            return (_progressBar != null) && _progressBar.gameObject.activeInHierarchy;
        }

        public virtual void Initialization()
        {
            _finalHideStarted = false;

            if (_progressBar != null)
            {
                return;
            }

            DrawHealthBar();

            if (_progressBar != null)
            {
                _progressBar.SetBar(100f, 0f, 100f);
            }
        }

        protected virtual void DrawHealthBar()
        {
            GameObject newGameObject = new GameObject();
            SceneManager.MoveGameObjectToScene(newGameObject, this.gameObject.scene);
            newGameObject.name = "HealthBar|" + this.gameObject.name;

            if (NestDrawnHealthBar)
            {
                newGameObject.transform.SetParent(this.transform);
            }

            _progressBar = newGameObject.AddComponent<EnigmaProgressBar>();

            _followTransform = newGameObject.AddComponent<MMFollowTarget>();
            _followTransform.Offset = HealthBarOffset;
            _followTransform.Target = this.transform;
            _followTransform.FollowRotation = FollowRotation;
            _followTransform.FollowScale = FollowScale;
            _followTransform.InterpolatePosition = false;
            _followTransform.InterpolateRotation = false;
            _followTransform.UpdateMode = FollowTargetMode;

            Canvas newCanvas = newGameObject.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.WorldSpace;
            newCanvas.transform.localScale = Vector3.one;
            newCanvas.GetComponent<RectTransform>().sizeDelta = Size;
            if (!string.IsNullOrEmpty(SortingLayerName))
            {
                newCanvas.sortingLayerName = SortingLayerName;
            }

            GameObject container = new GameObject();
            container.transform.SetParent(newGameObject.transform);
            container.name = "MMProgressBarContainer";
            container.transform.localScale = Vector3.one;

            GameObject borderImageGameObject = new GameObject();
            borderImageGameObject.transform.SetParent(container.transform);
            borderImageGameObject.name = "HealthBar Border";
            _borderImage = borderImageGameObject.AddComponent<Image>();
            _borderImage.transform.position = Vector3.zero;
            _borderImage.transform.localScale = Vector3.one;
            _borderImage.GetComponent<RectTransform>().sizeDelta = Size;
            _borderImage.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
            _borderImage.color = BorderColor;

            GameObject bgImageGameObject = new GameObject();
            bgImageGameObject.transform.SetParent(container.transform);
            bgImageGameObject.name = "HealthBar Background";
            _backgroundImage = bgImageGameObject.AddComponent<Image>();
            _backgroundImage.transform.position = Vector3.zero;
            _backgroundImage.transform.localScale = Vector3.one;
            _backgroundImage.GetComponent<RectTransform>().sizeDelta = Size - BackgroundPadding * 2;
            _backgroundImage.GetComponent<RectTransform>().anchoredPosition = -_backgroundImage.GetComponent<RectTransform>().sizeDelta / 2;
            _backgroundImage.GetComponent<RectTransform>().pivot = Vector2.zero;
            _backgroundImage.color = BackgroundColor;

            GameObject delayedImageGameObject = new GameObject();
            delayedImageGameObject.transform.SetParent(container.transform);
            delayedImageGameObject.name = "HealthBar Delayed Foreground";
            _delayedImage = delayedImageGameObject.AddComponent<Image>();
            _delayedImage.transform.position = Vector3.zero;
            _delayedImage.transform.localScale = Vector3.one;
            _delayedImage.GetComponent<RectTransform>().sizeDelta = Size - BackgroundPadding * 2;
            _delayedImage.GetComponent<RectTransform>().anchoredPosition = -_delayedImage.GetComponent<RectTransform>().sizeDelta / 2;
            _delayedImage.GetComponent<RectTransform>().pivot = Vector2.zero;
            _delayedImage.color = DelayedColor;

            GameObject frontImageGameObject = new GameObject();
            frontImageGameObject.transform.SetParent(container.transform);
            frontImageGameObject.name = "HealthBar Foreground";
            _foregroundImage = frontImageGameObject.AddComponent<Image>();
            _foregroundImage.transform.position = Vector3.zero;
            _foregroundImage.transform.localScale = Vector3.one;
            _foregroundImage.color = ForegroundColor;
            _foregroundImage.GetComponent<RectTransform>().sizeDelta = Size - BackgroundPadding * 2;
            _foregroundImage.GetComponent<RectTransform>().anchoredPosition = -_foregroundImage.GetComponent<RectTransform>().sizeDelta / 2;
            _foregroundImage.GetComponent<RectTransform>().pivot = Vector2.zero;

            if (Billboard)
            {
                MMBillboard billboard = _progressBar.gameObject.AddComponent<MMBillboard>();
                billboard.NestObject = !NestDrawnHealthBar;
            }

            _progressBar.LerpDecreasingDelayedBar = LerpDelayedBar;
            _progressBar.LerpForegroundBar = LerpFrontBar;
            _progressBar.LerpDecreasingDelayedBarSpeed = LerpDelayedBarSpeed;
            _progressBar.ForegroundBar = _foregroundImage.transform;
            _progressBar.DelayedBarDecreasing = _delayedImage.transform;
            _progressBar.DecreasingDelay = Delay;
            _progressBar.TimeScale = (TimeScale == TimeScales.Time) ? EnigmaProgressBar.TimeScales.Time : EnigmaProgressBar.TimeScales.UnscaledTime;
            container.transform.localEulerAngles = InitialRotationAngles;
            _progressBar.Initialization();
        }

        protected virtual void Update()
        {
            if (_progressBar == null)
            {
                return;
            }

            if (_finalHideStarted)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                UpdateBarPosition();
                if (Billboard)
                {
#if UNITY_EDITOR
                    SceneView sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        Camera editorCamera = sceneView.camera;
                        if (editorCamera != null)
                        {
                            _progressBar.transform.LookAt(_progressBar.transform.position + editorCamera.transform.rotation * Vector3.forward, editorCamera.transform.rotation * Vector3.up);
                        }
                    }
#endif
                }
            }
        }

        protected virtual IEnumerator FinalHideBar()
        {
            _finalHideStarted = true;
            if (HideBarAtZeroDelay == 0)
            {
                ShowBar(false);
                yield return null;
            }
            else
            {
                _progressBar.HideBar(HideBarAtZeroDelay);
            }
        }

        public virtual void UpdateBar(float currentHealth, float minHealth, float maxHealth)
        {
            if (_progressBar == null)
            {
                return;
            }

            _progressBar.UpdateBar(currentHealth, minHealth, maxHealth);

            if (HideBarAtZero && _progressBar.BarTarget <= 0)
            {
                StartCoroutine(FinalHideBar());
            }
        }

        protected virtual void UpdateBarColors()
        {
            if (_borderImage != null) { _borderImage.color = BorderColor; }
            if (_backgroundImage != null) { _backgroundImage.color = BackgroundColor; }
            if (_delayedImage != null) { _delayedImage.color = DelayedColor; }
            if (_foregroundImage != null) { _foregroundImage.color = ForegroundColor; }
        }

        protected virtual void UpdateBarSize()
        {
            if (_progressBar == null) { return; }
            _progressBar.GetComponent<RectTransform>().sizeDelta = Size;
            if (_borderImage != null) { _borderImage.GetComponent<RectTransform>().sizeDelta = Size; }
            Vector2 innerSize = Size - BackgroundPadding * 2;
            if (_backgroundImage != null)
            {
                _backgroundImage.GetComponent<RectTransform>().sizeDelta = innerSize;
                _backgroundImage.GetComponent<RectTransform>().anchoredPosition = -innerSize / 2;
            }
            if (_delayedImage != null)
            {
                _delayedImage.GetComponent<RectTransform>().sizeDelta = innerSize;
                _delayedImage.GetComponent<RectTransform>().anchoredPosition = -innerSize / 2;
            }
            if (_foregroundImage != null)
            {
                _foregroundImage.GetComponent<RectTransform>().sizeDelta = innerSize;
                _foregroundImage.GetComponent<RectTransform>().anchoredPosition = -innerSize / 2;
            }
        }

        protected virtual void UpdateBarPosition()
        {
            if (_followTransform != null) { _followTransform.Offset = HealthBarOffset; }
            if (_progressBar != null)
            {
                _progressBar.transform.position = transform.position + HealthBarOffset;
            }
        }

        protected virtual void UpdateBarRotation()
        {
            if (_progressBar == null) { return; }
            var container = _progressBar.transform.Find("MMProgressBarContainer");
            if (container != null) { container.localEulerAngles = InitialRotationAngles; }
        }

        protected virtual void UpdateSortingLayer()
        {
            if (_progressBar == null) { return; }
            var canvas = _progressBar.GetComponent<Canvas>();
            if (canvas != null && !string.IsNullOrEmpty(SortingLayerName))
            {
                canvas.sortingLayerName = SortingLayerName;
            }
        }

        public virtual void DestroyBar()
        {
            if (_progressBar != null)
            {
                Destroy(_progressBar.gameObject);
            }
        }
    }
}