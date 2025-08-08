using UnityEngine;
using MoreMountains.Tools;
using System.Collections.Generic;
using MoreMountains.InventoryEngine;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    public enum PauseMethods
    {
        PauseMenu,
        NoPauseMenu
    }
    
    public class PointsOfEntryStorage
    {
        public string LevelName;
        public int PointOfEntryIndex;
        public EnigmaCharacter.FacingDirections FacingDirection;

        public PointsOfEntryStorage(string levelName, int pointOfEntryIndex, EnigmaCharacter.FacingDirections facingDirection)
        {
            LevelName = levelName;
            FacingDirection = facingDirection;
            PointOfEntryIndex = pointOfEntryIndex;
        }
    }
    
    [AddComponentMenu("Enigma Engine/Managers/Enigma Game Manager")]
    public class EnigmaGameManager : MMPersistentSingleton<EnigmaGameManager>,
        MMEventListener<MMGameEvent>,
        MMEventListener<EnigmaEngineEvent>,
        MMEventListener<EnigmaEnginePointEvent>
    {
        [Tooltip("The target frame rate for the game")]
        public int TargetFrameRate = 60;

        [Title("Bindings")]
        public string GameOverScene;

        [Title("Points")]
        [ReadOnly]
        public int CurrentPoints;

        [Title("Pause")]
        public bool PauseGameWhenInventoryOpens = true;

        public virtual bool Paused { get; set; }
        
        public virtual EnigmaCharacter PersistentCharacter { get; set; }

        public List<PointsOfEntryStorage> PointsOfEntry;

        public virtual EnigmaCharacter StoredCharacter { get; set; }

        protected bool _inventoryOpen = false;
        protected bool _pauseMenuOpen = false;
        protected InventoryInputManager _inventoryInputManager;

        protected override void Awake()
        {
            base.Awake();
            PointsOfEntry = new List<PointsOfEntryStorage>();
        }

        protected virtual void Start()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
        
        public virtual void Reset()
        {
            CurrentPoints = 0;
            MMTimeScaleEvent.Trigger(MMTimeScaleMethods.Reset, 1f, 0f, false, 0f, true);
            Paused = false;
        }
        
        protected virtual void SetActiveInventoryInputManager(bool status)
        {
            _inventoryInputManager = FindObjectsByType<InventoryInputManager>(FindObjectsSortMode.None)[0];
            if (_inventoryInputManager != null)
            {
                _inventoryInputManager.enabled = status;
            }
        }
        
        #region Points
        public virtual void AddPoints(int pointsToAdd)
        {
            CurrentPoints += pointsToAdd;
            EnigmaGUIManager.Instance.RefreshPoints();
        }

        public virtual void SetPoints(int points)
        {
            CurrentPoints = points;
            EnigmaGUIManager.Instance.RefreshPoints();
        }

        
        public virtual void OnMMEvent(EnigmaEnginePointEvent pointEvent)
        {
            switch (pointEvent.PointsMethod)
            {
                case PointsMethods.Set:
                    SetPoints(pointEvent.Points);
                    break;

                case PointsMethods.Add:
                    AddPoints(pointEvent.Points);
                    break;
            }
        }
        #endregion
        
        #region Points of Entry
        public virtual void StorePointsOfEntry(string levelName, int entryIndex, EnigmaCharacter.FacingDirections facingDirection)
        {
            if (PointsOfEntry.Count > 0)
            {
                foreach (PointsOfEntryStorage point in PointsOfEntry)
                {
                    if (point.LevelName == levelName)
                    {
                        point.PointOfEntryIndex = entryIndex;
                        return;
                    }
                }
            }

            PointsOfEntry.Add(new PointsOfEntryStorage(levelName, entryIndex, facingDirection));
        }
        
        public virtual PointsOfEntryStorage GetPointsOfEntry(string levelName)
        {
            if (PointsOfEntry.Count > 0)
            {
                foreach (PointsOfEntryStorage point in PointsOfEntry)
                {
                    if (point.LevelName == levelName)
                    {
                        return point;
                    }
                }
            }

            return null;
        }
        
        public virtual void ClearPointOfEntry(string levelName)
        {
            if (PointsOfEntry.Count > 0)
            {
                foreach (PointsOfEntryStorage point in PointsOfEntry)
                {
                    if (point.LevelName == levelName)
                    {
                        PointsOfEntry.Remove(point);
                    }
                }
            }
        }
        
        public virtual void ClearAllPointsOfEntry()
        {
            PointsOfEntry.Clear();
        }
        #endregion
        
        #region Character Persistence
        public virtual void ResetAllSaves()
        {
            MMSaveLoadManager.DeleteSaveFolder("InventoryEngine");
            MMSaveLoadManager.DeleteSaveFolder("TopDownEngine");
            MMSaveLoadManager.DeleteSaveFolder("MMAchievements");
        }
        
        public virtual void StoreSelectedCharacter(EnigmaCharacter selectedCharacter)
        {
            StoredCharacter = selectedCharacter;
        }
        
        public virtual void ClearSelectedCharacter()
        {
            StoredCharacter = null;
        }
        
        public virtual void SetPersistentCharacter(EnigmaCharacter newCharacter)
        {
            PersistentCharacter = newCharacter;
        }
        
        public virtual void DestroyPersistentCharacter()
        {
            if (PersistentCharacter != null)
            {
                Destroy(PersistentCharacter.gameObject);
                SetPersistentCharacter(null);
            }
            
            if (EnigmaLevelManager.Instance.Players[0] != null)
            {
                if (EnigmaLevelManager.Instance.Players[0].gameObject.MMGetComponentNoAlloc<EnigmaCharacterPersistence>() != null)
                {
                    Destroy(EnigmaLevelManager.Instance.Players[0].gameObject);
                }
            }
        }
        #endregion
        
        #region Pause logic
        
        public virtual void Pause(PauseMethods pauseMethod = PauseMethods.PauseMenu, bool unpauseIfPaused = true)
        {
            if ((pauseMethod == PauseMethods.PauseMenu) && _inventoryOpen)
            {
                return;
            }

            if (Time.timeScale > 0.0f)
            {
                MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0f, 0f, false, 0f, true);
                Instance.Paused = true;
                if ((EnigmaGUIManager.HasInstance) && (pauseMethod == PauseMethods.PauseMenu))
                {
                    EnigmaGUIManager.Instance.SetPauseScreen(true);
                    _pauseMenuOpen = true;
                    SetActiveInventoryInputManager(false);
                }

                if (pauseMethod == PauseMethods.NoPauseMenu)
                {
                    _inventoryOpen = true;
                }
            }
            else
            {
                if (unpauseIfPaused)
                {
                    UnPause(pauseMethod);
                }
            }

            EnigmaLevelManager.Instance.ToggleCharacterPause();
        }
        
        public virtual void UnPause(PauseMethods pauseMethod = PauseMethods.PauseMenu)
        {
            MMTimeScaleEvent.Trigger(MMTimeScaleMethods.Unfreeze, 1f, 0f, false, 0f, false);
            Instance.Paused = false;
            if ((EnigmaGUIManager.HasInstance) && (pauseMethod == PauseMethods.PauseMenu))
            {
                EnigmaGUIManager.Instance.SetPauseScreen(false);
                _pauseMenuOpen = false;
                SetActiveInventoryInputManager(true);
            }

            if (_inventoryOpen)
            {
                _inventoryOpen = false;
            }

            EnigmaLevelManager.Instance.ToggleCharacterPause();
        }
        
        public virtual void OnMMEvent(MMGameEvent gameEvent)
        {
            switch (gameEvent.EventName)
            {
                case "inventoryOpens":
                    if (PauseGameWhenInventoryOpens)
                    {
                        Pause(PauseMethods.NoPauseMenu, false);
                    }

                    break;

                case "inventoryCloses":
                    if (PauseGameWhenInventoryOpens)
                    {
                        UnPause(PauseMethods.NoPauseMenu);
                    }

                    break;
            }
        }

        public virtual void OnMMEvent(EnigmaEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case EnigmaEngineEventTypes.TogglePause:
                    if (Paused)
                    {
                        EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.UnPause, null);
                    }
                    else
                    {
                        EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.Pause, null);
                    }

                    break;
                case EnigmaEngineEventTypes.Pause:
                    Pause();
                    break;
                case EnigmaEngineEventTypes.UnPause:
                    UnPause();
                    break;
                case EnigmaEngineEventTypes.PauseNoMenu:
                    Pause(PauseMethods.NoPauseMenu, false);
                    break;
            }
        }
        #endregion
        
        protected virtual void OnEnable()
        {
            this.MMEventStartListening<MMGameEvent>();
            this.MMEventStartListening<EnigmaEngineEvent>();
            this.MMEventStartListening<EnigmaEnginePointEvent>();
        }
        
        protected virtual void OnDisable()
        {
            this.MMEventStopListening<MMGameEvent>();
            this.MMEventStopListening<EnigmaEngineEvent>();
            this.MMEventStopListening<EnigmaEnginePointEvent>();
        }
    }
}