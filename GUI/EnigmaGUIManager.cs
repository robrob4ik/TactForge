using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Tools;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Managers/Enigma GUI Manager")]
    public class EnigmaGUIManager : MMSingleton<EnigmaGUIManager>
    {
        public Canvas MainCanvas;
        public GameObject HUD;
        public EnigmaProgressBar[] HealthBars;
        public EnigmaProgressBar[] DashBars;
        public GameObject PauseScreen;
        public GameObject DeathScreen;
        
        public Text PointsText;
        public string PointsTextPattern = "000000";
        
        protected bool _initialized = false;
        
        
        [Tooltip("the duration of the fade to black at the end of the level")]
        public float FadeOutDuration = 1f;
        [Tooltip("the tween type to use to fade the startscreen in and out ")]
        public MMTweenType Tween = new MMTweenType(MMTween.MMTweenCurve.EaseInOutCubic);

        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        protected static void InitializeStatics() { _instance = null; }
        
        protected override void Awake()
        {
            base.Awake();
            Initialization();
        }

        protected virtual void Initialization()
        {
            if (_initialized) { return; }
            _initialized = true;
        }
        
        protected virtual void Start()
        {
            RefreshPoints();
            SetPauseScreen(false);
            SetDeathScreen(false);
        }
        
        public virtual void SetHUDActive(bool state)
        {
            if (HUD != null) { HUD.SetActive(state); }

            if (PointsText != null) { PointsText.enabled = state; }
        }
        
        public virtual void LoadMenuButton()
        {
            if (EnigmaGameManager.Instance.Paused) { EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.UnPause, null); }

            EnigmaLevelManager.Instance.GotoLevel("MenuScene");
        }

        public virtual void ReloadLevelButton()
        {
            if (EnigmaGameManager.Instance.Paused) { EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.UnPause, null); }

            EnigmaLevelManager.Instance.GotoLevel(SceneManager.GetActiveScene().name);
        }

        public virtual void SetAvatarActive(bool state)
        {
            if (HUD != null) { HUD.SetActive(state); }
        }

        public virtual void SetPauseScreen(bool state)
        {
            if (PauseScreen != null)
            {
                PauseScreen.SetActive(state);
                EventSystem.current.sendNavigationEvents = state;
            }
        }
        
        public virtual void SetDeathScreen(bool state)
        {
            if (DeathScreen != null)
            {
                DeathScreen.SetActive(state);
                EventSystem.current.sendNavigationEvents = state;
            }
        }
        
        public virtual void SetDashBar(bool state, string playerID)
        {
            if (DashBars == null) { return; }

            foreach (EnigmaProgressBar jetpackBar in DashBars)
            {
                if (jetpackBar != null)
                {
                    if (jetpackBar.PlayerID == playerID) { jetpackBar.gameObject.SetActive(state); }
                }
            }
        }

        public virtual void RefreshPoints()
        {
            if (PointsText != null) { PointsText.text = EnigmaGameManager.Instance.CurrentPoints.ToString(PointsTextPattern); }
        }
        
        public virtual void UpdateHealthBar(float currentHealth, float minHealth, float maxHealth, string playerID)
        {
            if (HealthBars == null) { return; }

            if (HealthBars.Length <= 0) { return; }

            foreach (EnigmaProgressBar healthBar in HealthBars)
            {
                if (healthBar == null) { continue; }

                if (healthBar.PlayerID == playerID) { healthBar.UpdateBar(currentHealth, minHealth, maxHealth); }
            }
        }
        
        public virtual void UpdateDashBars(float currentFuel, float minFuel, float maxFuel, string playerID)
        {
            if (DashBars == null) { return; }

            foreach (EnigmaProgressBar dashbar in DashBars)
            {
                if (dashbar == null) { return; }

                if (dashbar.PlayerID == playerID) { dashbar.UpdateBar(currentFuel, minFuel, maxFuel); }
            }
        }
        
      
    }
}