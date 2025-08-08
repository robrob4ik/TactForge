using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.Tools;
using Sirenix.OdinInspector;


namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Managers/Enigma Level Manager")]
    public class EnigmaLevelManager : MMSingleton<EnigmaLevelManager>, MMEventListener<EnigmaEngineEvent>
    {
        [Title("Instantiate Characters")]
        [Tooltip("The list of player prefabs this level manager will instantiate on Start")]
        public EnigmaCharacter[] PlayerPrefabs;

        [Title("Checkpoints")]
        [Tooltip("The checkpoint to use as initial spawn point if no point of entry is specified")]
        public EnigmaCheckPoint InitialSpawnPoint;

        [Tooltip("The currently active checkpoint (the last checkpoint passed by the player)")]
        public EnigmaCheckPoint CurrentCheckpoint;

        [Title("Points of Entry")]
        [Tooltip("A list of this level's points of entry, which can be used from other levels as initial targets")]
        public Transform[] PointsOfEntry;

        [Space(10)]
        [Title("Intro and Outro durations")]
        [Tooltip("The duration of the initial fade in (in seconds)")]
        public float IntroFadeDuration = 1f;

        public float SpawnDelay = 0f;

        [Tooltip("The duration of the fade to black at the end of the level (in seconds)")]
        public float OutroFadeDuration = 1f;

        [Tooltip("The ID to use when triggering the event (should match the ID on the fader you want to use)")]
        public int FaderID = 0;

        [Tooltip("The curve to use for in and out fades")]
        public MMTweenType FadeCurve = new MMTweenType(MMTween.MMTweenCurve.EaseInOutCubic);

        [Tooltip("The duration between a death of the main character and its respawn")]
        public float RespawnDelay = 2f;

        [Title("Respawn Loop")]
        [Tooltip("The delay, in seconds, before displaying the death screen once the player is dead")]
        public float DelayBeforeDeathScreen = 1f;

        [Title("Bounds")]
        [Tooltip("If this is true, this level will use the level bounds defined on this LevelManager. Set it to false when using the Rooms system.")]
        public bool UseLevelBounds = true;

        [Title("Scene Loading")]
        [Tooltip("The method to use to load the destination level")]
        public MMLoadScene.LoadingSceneModes LoadingSceneMode = MMLoadScene.LoadingSceneModes.MMSceneLoadingManager;

        [Tooltip("The name of the MMSceneLoadingManager scene you want to use")]
        [MMEnumCondition("LoadingSceneMode", (int)MMLoadScene.LoadingSceneModes.MMSceneLoadingManager)]
        public string LoadingSceneName = "LoadingScreen";

        [Tooltip("the settings to use when loading the scene in additive mode")]
        [MMEnumCondition("LoadingSceneMode", (int)MMLoadScene.LoadingSceneModes.MMAdditiveSceneLoadingManager)]
        public MMAdditiveSceneLoadingManagerSettings AdditiveLoadingSettings;

        [Title("Feedbacks")]
        [Tooltip("If this is true, an event will be triggered on player instantiation to set the range target of all feedbacks to it")]
        public bool SetPlayerAsFeedbackRangeCenter = false;

        public virtual Bounds LevelBounds { get { return (_collider == null) ? new Bounds() : _collider.bounds; } }

        public virtual Collider BoundsCollider { get; protected set; }

        public virtual TimeSpan RunningTime { get { return DateTime.UtcNow - _started; } }

        public virtual List<EnigmaCheckPoint> Checkpoints { get; protected set; }
        public virtual List<EnigmaCharacter> Players { get; protected set; }

        protected DateTime _started;
        protected int _savedPoints;
        protected Collider _collider;
        protected Collider2D _collider2D;
        protected Vector3 _initialSpawnPointPosition;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        protected static void InitializeStatics() { _instance = null; }
        
        protected override void Awake()
        {
            base.Awake();
            _collider = this.GetComponent<Collider>();
            _collider2D = this.GetComponent<Collider2D>();
        }
        
        protected virtual void Start() { StartCoroutine(InitializationCoroutine()); }

        protected virtual IEnumerator InitializationCoroutine()
        {
            if (SpawnDelay > 0f) { yield return MMCoroutine.WaitFor(SpawnDelay); }

            BoundsCollider = _collider;
            InstantiatePlayableCharacters();

            if (UseLevelBounds) { EnigmaCameraEvent.Trigger(EnigmaCameraEventTypes.SetConfiner, null, BoundsCollider, null); }

            if (Players == null || Players.Count == 0) { yield break; }

            Initialization();

            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.SpawnCharacterStarts, null);

            // we handle the spawn of the character(s)
            if (Players.Count == 1) { SpawnSingleCharacter(); }
            else { SpawnMultipleCharacters(); }

            CheckpointAssignment();

            // we trigger a fade
            MMFadeOutEvent.Trigger(IntroFadeDuration, FadeCurve, FaderID);

            // we trigger a level start event
            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.LevelStart, null);
            MMGameEvent.Trigger("Load");

            if (SetPlayerAsFeedbackRangeCenter) { MMSetFeedbackRangeCenterEvent.Trigger(Players[0].transform); }

            EnigmaCameraEvent.Trigger(EnigmaCameraEventTypes.SetTargetCharacter, Players[0]);
            EnigmaCameraEvent.Trigger(EnigmaCameraEventTypes.StartFollowing);
            MMGameEvent.Trigger("CameraBound");
        }
        
        protected virtual void SpawnMultipleCharacters() { }

        protected virtual void InstantiatePlayableCharacters()
        {
            _initialSpawnPointPosition = (InitialSpawnPoint == null) ? Vector3.zero : InitialSpawnPoint.transform.position;

            Players = new List<EnigmaCharacter>();

            if (EnigmaGameManager.Instance.PersistentCharacter != null)
            {
                Players.Add(EnigmaGameManager.Instance.PersistentCharacter);
                return;
            }

            if (EnigmaGameManager.Instance.StoredCharacter != null)
            {
                EnigmaCharacter newPlayer = Instantiate(EnigmaGameManager.Instance.StoredCharacter, _initialSpawnPointPosition, Quaternion.identity);
                newPlayer.name = EnigmaGameManager.Instance.StoredCharacter.name;
                Players.Add(newPlayer);
                return;
            }

            if (PlayerPrefabs == null) { return; }

            if (PlayerPrefabs.Length != 0)
            {
                foreach (EnigmaCharacter playerPrefab in PlayerPrefabs)
                {
                    EnigmaCharacter newPlayer = Instantiate(playerPrefab, _initialSpawnPointPosition, Quaternion.identity);
                    newPlayer.name = playerPrefab.name;
                    Players.Add(newPlayer);

                    if (playerPrefab.CharacterType != EnigmaCharacter.CharacterTypes.Player)
                    {
                        Debug.LogWarning("LevelManager : The Character you've set in the LevelManager isn't a Player, which means it's probably not going to move. You can change that in the Character component of your prefab.");
                    }
                }
            }
        }
        
        protected virtual void CheckpointAssignment()
        {
            IEnumerable<EnigmaRespawnable> listeners = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<EnigmaRespawnable>();
            EnigmaAutoRespawn autoRespawn;
            foreach (EnigmaRespawnable listener in listeners)
            {
                for (int i = Checkpoints.Count - 1; i >= 0; i--)
                {
                    autoRespawn = (listener as MonoBehaviour).GetComponent<EnigmaAutoRespawn>();
                    if (autoRespawn == null)
                    {
                        Checkpoints[i].AssignObjectToCheckPoint(listener);
                        continue;
                    }
                    else
                    {
                        if (autoRespawn.IgnoreCheckpointsAlwaysRespawn)
                        {
                            Checkpoints[i].AssignObjectToCheckPoint(listener);
                            continue;
                        }
                        else
                        {
                            if (autoRespawn.AssociatedCheckpoints.Contains(Checkpoints[i]))
                            {
                                Checkpoints[i].AssignObjectToCheckPoint(listener);
                                continue;
                            }

                            continue;
                        }
                    }
                }
            }
        }
        
        protected virtual void Initialization()
        {
            Checkpoints = FindObjectsByType<EnigmaCheckPoint>(FindObjectsSortMode.None)
                .OrderBy(o => o.CheckPointOrder)
                .ToList();
            _savedPoints = EnigmaGameManager.Instance.CurrentPoints;
            _started = DateTime.UtcNow;
        }

        protected virtual void SpawnSingleCharacter()
        {
            PointsOfEntryStorage point = EnigmaGameManager.Instance.GetPointsOfEntry(SceneManager.GetActiveScene().name);
            if ((point != null) && (PointsOfEntry.Length >= (point.PointOfEntryIndex + 1)))
            {
                Players[0].RespawnAt(PointsOfEntry[point.PointOfEntryIndex], point.FacingDirection);
                EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.SpawnComplete, Players[0]);
                return;
            }

            if (InitialSpawnPoint != null)
            {
                InitialSpawnPoint.SpawnPlayer(Players[0]);
                EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.SpawnComplete, Players[0]);
                return;
            }
        }
        
        public virtual void GotoLevel(string levelName)
        {
            TriggerEndLevelEvents();
            StartCoroutine(GotoLevelCo(levelName));
        }
        
        public virtual void TriggerEndLevelEvents()
        {
            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.LevelEnd, null);
            MMGameEvent.Trigger("Save");
        }
        
        protected virtual IEnumerator GotoLevelCo(string levelName)
        {
            if (Players != null && Players.Count > 0)
            {
                foreach (EnigmaCharacter player in Players) { player.Disable(); }
            }

            MMFadeInEvent.Trigger(OutroFadeDuration, FadeCurve, FaderID);

            if (Time.timeScale > 0.0f) { yield return new WaitForSeconds(OutroFadeDuration); }

            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.UnPause, null);
            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.LoadNextScene, null);

            string destinationScene = (string.IsNullOrEmpty(levelName)) ? "StartScreen" : levelName;

            switch (LoadingSceneMode)
            {
                case MMLoadScene.LoadingSceneModes.UnityNative:
                    SceneManager.LoadScene(destinationScene);
                    break;
                case MMLoadScene.LoadingSceneModes.MMSceneLoadingManager:
                    MMSceneLoadingManager.LoadScene(destinationScene, LoadingSceneName);
                    break;
                case MMLoadScene.LoadingSceneModes.MMAdditiveSceneLoadingManager:
                    MMAdditiveSceneLoadingManager.LoadScene(levelName, AdditiveLoadingSettings);
                    break;
            }
        }
        
        public virtual void PlayerDead(EnigmaCharacter playerCharacter)
        {
            if (Players.Count < 2) { StartCoroutine(PlayerDeadCo()); }
        }
        
        protected virtual IEnumerator PlayerDeadCo()
        {
            yield return new WaitForSeconds(DelayBeforeDeathScreen);

            EnigmaGUIManager.Instance.SetDeathScreen(true);
        }
        
        protected virtual void Respawn()
        {
            if (Players.Count < 2) { StartCoroutine(SoloModeRestart()); }
        }
        
        protected virtual IEnumerator SoloModeRestart()
        {
            if (PlayerPrefabs.Length <= 0) { yield break; }

            // EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.GameOver, null);
            // if ((EnigmaGameManager.Instance.GameOverScene != null) && (EnigmaGameManager.Instance.GameOverScene != "")) { MMSceneLoadingManager.LoadScene(EnigmaGameManager.Instance.GameOverScene); }
            
            EnigmaCameraEvent.Trigger(EnigmaCameraEventTypes.StopFollowing);

            MMFadeInEvent.Trigger(OutroFadeDuration, FadeCurve, FaderID, true, Players[0].transform.position);
            yield return new WaitForSeconds(OutroFadeDuration);

            yield return new WaitForSeconds(RespawnDelay);
            EnigmaGUIManager.Instance.SetPauseScreen(false);
            EnigmaGUIManager.Instance.SetDeathScreen(false);
            MMFadeOutEvent.Trigger(OutroFadeDuration, FadeCurve, FaderID, true, Players[0].transform.position);

            if (CurrentCheckpoint == null) { CurrentCheckpoint = InitialSpawnPoint; }

            if (Players[0] == null) { InstantiatePlayableCharacters(); }

            if (CurrentCheckpoint != null) { CurrentCheckpoint.SpawnPlayer(Players[0]); }
            else { Debug.LogWarning("LevelManager : no checkpoint or initial spawn point has been defined, can't respawn the Player."); }

            _started = DateTime.UtcNow;

            EnigmaCameraEvent.Trigger(EnigmaCameraEventTypes.StartFollowing);

            // we send a new points event for the GameManager to catch (and other classes that may listen to it too)
            EnigmaEnginePointEvent.Trigger(PointsMethods.Set, 0);
            EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.RespawnComplete, Players[0]);
            yield break;
        }
        
        public virtual void ToggleCharacterPause()
        {
            foreach (EnigmaCharacter player in Players)
            {
                EnigmaCharacterPause characterPause = player.FindAbility<EnigmaCharacterPause>();
                if (characterPause == null) { break; }

                if (EnigmaGameManager.Instance.Paused) { characterPause.PauseCharacter(); }
                else { characterPause.UnPauseCharacter(); }
            }
        }

        public virtual void SetCurrentCheckpoint(EnigmaCheckPoint newCheckPoint)
        {
            if (newCheckPoint.ForceAssignation)
            {
                CurrentCheckpoint = newCheckPoint;
                return;
            }

            if (CurrentCheckpoint == null)
            {
                CurrentCheckpoint = newCheckPoint;
                return;
            }

            if (newCheckPoint.CheckPointOrder >= CurrentCheckpoint.CheckPointOrder) { CurrentCheckpoint = newCheckPoint; }
        }
        
        public virtual void OnMMEvent(EnigmaEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case EnigmaEngineEventTypes.PlayerDeath:
                    PlayerDead(engineEvent.OriginCharacter);
                    break;
                case EnigmaEngineEventTypes.RespawnStarted:
                    Respawn();
                    break;
            }
        }

        protected virtual void OnEnable() { this.MMEventStartListening<EnigmaEngineEvent>(); }

        protected virtual void OnDisable() { this.MMEventStopListening<EnigmaEngineEvent>(); }
    }
}