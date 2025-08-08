using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using System;
using Sirenix.OdinInspector;
using Unity.Entities;
using Random = UnityEngine.Random;

namespace OneBitRob.EnigmaEngine
{
    [SelectionBase]
    [AddComponentMenu("Enigma Engine/Character/Core/Enigma Character")]
    [TemporaryBakingType]
    public class EnigmaCharacter : MonoBehaviour
    {
        public enum FacingDirections
        {
            West,
            East,
            North,
            South
        }

        public enum CharacterTypes
        {
            Player,
            AI
        }

        [Title("General")]
        public CharacterTypes CharacterType = CharacterTypes.AI;

        public string PlayerID = "";
        public virtual EnigmaCharacterStates enigmaCharacterState { get; protected set; }

        [Title("Animator")]
        public Animator CharacterAnimator;

        [Tooltip("Set this to false if you want to implement your own animation system")]
        public bool UseDefaultMecanim = true;

        public bool RunAnimatorSanityChecks = false;
        public bool DisableAnimatorLogs = true;

        [Title("Bindings")]
        public GameObject CharacterModel;

        [Tooltip("The Health script associated to this Character")]
        public EnigmaHealth CharacterHealth;

        [Title("Events")]
        [Tooltip("If this is true, the Character's state machine will emit events when entering/exiting a state")]
        public bool SendStateChangeEvents = false;

        [Title("Abilities")]
        public List<GameObject> AdditionalAbilityNodes;

        public MMStateMachine<EnigmaCharacterStates.MovementStates> MovementState;
        public MMStateMachine<EnigmaCharacterStates.CharacterConditions> ConditionState;

        public virtual EnigmaInputManager LinkedInputManager { get; protected set; }

        public virtual Animator _animator { get; protected set; }

        public virtual HashSet<int> _animatorParameters { get; set; }

        public virtual EnigmaCharacterOrientation3D Orientation3D { get; protected set; }

        public virtual GameObject CameraTarget { get; set; }
        public virtual Vector3 CameraDirection { get; protected set; }

        protected EnigmaCharacterAbility[] _characterAbilities;
        protected bool _abilitiesCachedOnce = false;
        protected EnigmaController _controller;
        protected float _animatorRandomNumber;
        protected bool _spawnDirectionForced = false;

        protected const string _groundedAnimationParameterName = "Grounded";
        protected const string _aliveAnimationParameterName = "Alive";
        protected const string _currentSpeedAnimationParameterName = "CurrentSpeed";
        protected const string _xSpeedAnimationParameterName = "xSpeed";
        protected const string _ySpeedAnimationParameterName = "ySpeed";
        protected const string _zSpeedAnimationParameterName = "zSpeed";
        protected const string _xVelocityAnimationParameterName = "xVelocity";
        protected const string _yVelocityAnimationParameterName = "yVelocity";
        protected const string _zVelocityAnimationParameterName = "zVelocity";
        protected const string _idleAnimationParameterName = "Idle";
        protected const string _randomAnimationParameterName = "Random";
        protected const string _randomConstantAnimationParameterName = "RandomConstant";

        protected int _groundedAnimationParameter;
        protected int _aliveAnimationParameter;
        protected int _currentSpeedAnimationParameter;
        protected int _xSpeedAnimationParameter;
        protected int _ySpeedAnimationParameter;
        protected int _zSpeedAnimationParameter;
        protected int _xVelocityAnimationParameter;
        protected int _yVelocityAnimationParameter;
        protected int _zVelocityAnimationParameter;

        protected int _idleAnimationParameter;
        protected int _randomAnimationParameter;
        protected int _randomConstantAnimationParameter;
        protected bool _animatorInitialized = false;
        protected EnigmaCharacterPersistence _characterPersistence;
        protected bool _onReviveRegistered;
        protected Coroutine _conditionChangeCoroutine;
        protected EnigmaCharacterStates.CharacterConditions _lastState;

        protected virtual void Awake() { Initialization(); }

        /// Gets and stores input manager, camera and components
        protected virtual void Initialization()
        {
            // we initialize our state machines
            MovementState = new MMStateMachine<EnigmaCharacterStates.MovementStates>(gameObject, SendStateChangeEvents);
            ConditionState = new MMStateMachine<EnigmaCharacterStates.CharacterConditions>(gameObject, SendStateChangeEvents);

            // we get the current input manager
            SetInputManager();

            // we store our components for further use 
            enigmaCharacterState = new EnigmaCharacterStates();
            _controller = this.gameObject.GetComponent<EnigmaController>();

            if (CharacterHealth == null) { CharacterHealth = this.gameObject.GetComponent<EnigmaHealth>(); }

            CacheAbilitiesAtInit();

            Orientation3D = FindAbility<EnigmaCharacterOrientation3D>();
            _characterPersistence = FindAbility<EnigmaCharacterPersistence>();

            AssignAnimator();

            // instantiate camera target
            if (CameraTarget == null) { CameraTarget = new GameObject(); }

            CameraTarget.transform.SetParent(this.transform);
            CameraTarget.transform.localPosition = Vector3.zero;
            CameraTarget.name = "CameraTarget";
        }

        /// Caches abilities if necessary
        protected virtual void CacheAbilitiesAtInit()
        {
            if (_abilitiesCachedOnce) { return; }

            CacheAbilities();
        }

        /// Grabs abilities and caches them for further use
        /// Make sure you call this if you add abilities at runtime
        /// Ideally you'll want to avoid adding components at runtime, it's costly,
        /// and it's best to activate/disable components instead.
        /// But if you need to, call this method.
        public virtual void CacheAbilities()
        {
            // we grab all abilities at our level
            _characterAbilities = this.gameObject.GetComponents<EnigmaCharacterAbility>();

            // if the user has specified more nodes
            if ((AdditionalAbilityNodes != null) && (AdditionalAbilityNodes.Count > 0))
            {
                // we create a temp list
                List<EnigmaCharacterAbility> tempAbilityList = new List<EnigmaCharacterAbility>();

                // we put all the abilities we've already found on the list
                for (int i = 0; i < _characterAbilities.Length; i++) { tempAbilityList.Add(_characterAbilities[i]); }

                // we add the ones from the nodes
                for (int j = 0; j < AdditionalAbilityNodes.Count; j++)
                {
                    EnigmaCharacterAbility[] tempArray = AdditionalAbilityNodes[j].GetComponentsInChildren<EnigmaCharacterAbility>();
                    foreach (EnigmaCharacterAbility ability in tempArray) { tempAbilityList.Add(ability); }
                }

                _characterAbilities = tempAbilityList.ToArray();
            }

            _abilitiesCachedOnce = true;
        }

        public virtual void ForceAbilitiesInitialization()
        {
            for (int i = 0; i < _characterAbilities.Length; i++) { _characterAbilities[i].ForceInitialization(); }

            for (int j = 0; j < AdditionalAbilityNodes.Count; j++)
            {
                EnigmaCharacterAbility[] tempArray = AdditionalAbilityNodes[j].GetComponentsInChildren<EnigmaCharacterAbility>();
                foreach (EnigmaCharacterAbility ability in tempArray) { ability.ForceInitialization(); }
            }
        }

        /// A method to check whether a Character has a certain ability or not
        public T FindAbility<T>() where T : EnigmaCharacterAbility
        {
            CacheAbilitiesAtInit();

            Type searchedAbilityType = typeof(T);

            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability is T characterAbility) { return characterAbility; }
            }

            return null;
        }

        /// A method to check whether a Character has a certain ability or not
        public EnigmaCharacterAbility FindAbilityByString(string abilityName)
        {
            CacheAbilitiesAtInit();

            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.GetType().Name == abilityName) { return ability; }
            }

            return null;
        }

        /// A method to check whether a Character has a certain ability or not
        public List<T> FindAbilities<T>() where T : EnigmaCharacterAbility
        {
            CacheAbilitiesAtInit();

            List<T> resultList = new List<T>();
            Type searchedAbilityType = typeof(T);

            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability is T characterAbility) { resultList.Add(characterAbility); }
            }

            return resultList;
        }

        /// Binds an animator to this character
        public virtual void AssignAnimator(bool forceAssignation = false)
        {
            if (_animatorInitialized && !forceAssignation) { return; }

            _animatorParameters = new HashSet<int>();

            if (CharacterAnimator != null) { _animator = CharacterAnimator; }
            else { _animator = this.gameObject.GetComponent<Animator>(); }

            if (_animator != null)
            {
                if (DisableAnimatorLogs) { _animator.logWarnings = false; }

                InitializeAnimatorParameters();
            }

            _animatorInitialized = true;
        }

        protected virtual void InitializeAnimatorParameters()
        {
            if (_animator == null) { return; }

            if (UseDefaultMecanim)
            {
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _groundedAnimationParameterName, out _groundedAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _currentSpeedAnimationParameterName, out _currentSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _xSpeedAnimationParameterName, out _xSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _ySpeedAnimationParameterName, out _ySpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _zSpeedAnimationParameterName, out _zSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _idleAnimationParameterName, out _idleAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _aliveAnimationParameterName, out _aliveAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _randomAnimationParameterName, out _randomAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _randomConstantAnimationParameterName, out _randomConstantAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _xVelocityAnimationParameterName, out _xVelocityAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _yVelocityAnimationParameterName, out _yVelocityAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
                MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _zVelocityAnimationParameterName, out _zVelocityAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            }

            int randomConstant = Random.Range(0, 1000);
            MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _randomConstantAnimationParameter, randomConstant, _animatorParameters);
        }

        public virtual void SetInputManager()
        {
            if (CharacterType == CharacterTypes.AI)
            {
                LinkedInputManager = null;
                UpdateInputManagersInAbilities();
                return;
            }

            // we get the corresponding input manager
            if (!string.IsNullOrEmpty(PlayerID))
            {
                LinkedInputManager = null;
                EnigmaInputManager[] foundInputManagers = FindObjectsByType<EnigmaInputManager>(FindObjectsSortMode.None);
                foreach (EnigmaInputManager foundInputManager in foundInputManagers)
                {
                    if (foundInputManager.PlayerID == PlayerID) { LinkedInputManager = foundInputManager; }
                }
            }

            UpdateInputManagersInAbilities();
        }

        /// Sets a new input manager for this Character and all its abilities
        public virtual void SetInputManager(EnigmaInputManager inputManager)
        {
            LinkedInputManager = inputManager;
            UpdateInputManagersInAbilities();
        }

        /// Updates the linked input manager for all abilities
        protected virtual void UpdateInputManagersInAbilities()
        {
            if (_characterAbilities == null) { return; }

            for (int i = 0; i < _characterAbilities.Length; i++) { _characterAbilities[i].SetInputManager(LinkedInputManager); }
        }

        public virtual void ResetInput()
        {
            if (_characterAbilities == null) { return; }

            foreach (EnigmaCharacterAbility ability in _characterAbilities) { ability.ResetInput(); }
        }

        public virtual void SetPlayerID(string newPlayerID)
        {
            PlayerID = newPlayerID;
            SetInputManager();
        }

        protected virtual void Update() { EveryFrame(); }

        protected virtual void EveryFrame()
        {
            EarlyProcessAbilities();
            ProcessAbilities();
            LateProcessAbilities();

            // we send our various states to the animator.		 
            UpdateAnimators();
        }

        /// Calls all registered abilities' Early Process methods
        protected virtual void EarlyProcessAbilities()
        {
            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.enabled && ability.AbilityInitialized) { ability.EarlyProcessAbility(); }
            }
        }

        /// Calls all registered abilities' Process methods
        protected virtual void ProcessAbilities()
        {
            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.enabled && ability.AbilityInitialized) { ability.ProcessAbility(); }
            }
        }

        /// Calls all registered abilities' Late Process methods
        protected virtual void LateProcessAbilities()
        {
            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.enabled && ability.AbilityInitialized) { ability.LateProcessAbility(); }
            }
        }

        protected virtual void UpdateAnimators()
        {
            UpdateAnimationRandomNumber();

            if ((UseDefaultMecanim) && (_animator != null))
            {
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _groundedAnimationParameter, _controller.Grounded, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _aliveAnimationParameter, (ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead), _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _currentSpeedAnimationParameter, _controller.CurrentMovement.magnitude, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _xSpeedAnimationParameter, _controller.CurrentMovement.x, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _ySpeedAnimationParameter, _controller.CurrentMovement.y, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _zSpeedAnimationParameter, _controller.CurrentMovement.z, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _idleAnimationParameter, (MovementState.CurrentState == EnigmaCharacterStates.MovementStates.Idle), _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _randomAnimationParameter, _animatorRandomNumber, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _xVelocityAnimationParameter, _controller.Velocity.x, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _yVelocityAnimationParameter, _controller.Velocity.y, _animatorParameters, RunAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _zVelocityAnimationParameter, _controller.Velocity.z, _animatorParameters, RunAnimatorSanityChecks);

                foreach (EnigmaCharacterAbility ability in _characterAbilities)
                {
                    if (ability.enabled && ability.AbilityInitialized) { ability.UpdateAnimator(); }
                }
            }
        }

        public virtual void RespawnAt(Vector3 spawnPosition, FacingDirections facingDirection)
        {
            transform.position = spawnPosition;

            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                //Debug.LogError("Spawn : your Character's gameobject is inactive");
            }

            // we raise it from the dead (if it was dead)
            ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Normal);
            // we re-enable its 2D collider
            if (this.gameObject.MMGetComponentNoAlloc<Collider2D>() != null) { this.gameObject.MMGetComponentNoAlloc<Collider2D>().enabled = true; }

            // we re-enable its 3D collider
            if (this.gameObject.MMGetComponentNoAlloc<Collider>() != null) { this.gameObject.MMGetComponentNoAlloc<Collider>().enabled = true; }

            _controller.enabled = true;
            _controller.CollisionsOn();
            _controller.Reset();

            Reset();
            UnFreeze();

            if (CharacterHealth != null)
            {
                CharacterHealth.StoreInitialPosition();
                if (_characterPersistence != null)
                {
                    if (_characterPersistence.Initialized)
                    {
                        if (CharacterHealth != null) { CharacterHealth.UpdateHealthBar(); }

                        return;
                    }
                }

                CharacterHealth.ResetHealthToMaxHealth();
                CharacterHealth.Revive();
            }

            if (FindAbility<EnigmaCharacterOrientation3D>() != null) { FindAbility<EnigmaCharacterOrientation3D>().Face(facingDirection); }
        }

        /// Makes the player respawn at the location passed in parameters
        public virtual void RespawnAt(Transform spawnPoint, FacingDirections facingDirection) { RespawnAt(spawnPoint.position, facingDirection); }

        /// Calls flip on all abilities
        public virtual void FlipAllAbilities()
        {
            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.enabled) { ability.Flip(); }
            }
        }

        /// Generates a random number to send to the animator
        protected virtual void UpdateAnimationRandomNumber() { _animatorRandomNumber = Random.Range(0f, 1f); }

        /// Use this method to change the character's condition for a specified duration, and resetting it afterwards.
        /// You can also use this to disable gravity for a while, and optionally reset forces too.
        public virtual void ChangeCharacterConditionTemporarily(EnigmaCharacterStates.CharacterConditions newCondition,
            float duration, bool resetControllerForces, bool disableGravity)
        {
            if (_conditionChangeCoroutine != null) { StopCoroutine(_conditionChangeCoroutine); }

            _conditionChangeCoroutine = StartCoroutine(ChangeCharacterConditionTemporarilyCo(newCondition, duration, resetControllerForces, disableGravity));
        }

        /// Coroutine handling the temporary change of condition mandated by ChangeCharacterConditionTemporarily
        protected virtual IEnumerator ChangeCharacterConditionTemporarilyCo(
            EnigmaCharacterStates.CharacterConditions newCondition,
            float duration, bool resetControllerForces, bool disableGravity)
        {
            if (_lastState != newCondition)
                if ((_lastState != newCondition) && (this.ConditionState.CurrentState != newCondition)) { _lastState = this.ConditionState.CurrentState; }

            this.ConditionState.ChangeState(newCondition);
            if (resetControllerForces) { _controller?.SetMovement(Vector2.zero); }

            if (disableGravity && (_controller != null)) { _controller.GravityActive = false; }

            yield return MMCoroutine.WaitFor(duration);
            this.ConditionState.ChangeState(_lastState);
            if (disableGravity && (_controller != null)) { _controller.GravityActive = true; }
        }

        /// Stores the associated camera direction
        public virtual void SetCameraDirection(Vector3 direction) { CameraDirection = direction; }

        /// Freezes this character.
        public virtual void Freeze()
        {
            _controller.SetGravityActive(false);
            _controller.SetMovement(Vector2.zero);
            ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Frozen);
        }

        /// Unfreezes this character
        public virtual void UnFreeze()
        {
            if (ConditionState.CurrentState == EnigmaCharacterStates.CharacterConditions.Frozen)
            {
                _controller.SetGravityActive(true);
                ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Normal);
            }
        }

        /// Called to disable the player (at the end of a level for example. 
        /// It won't move and respond to input after this.
        public virtual void Disable()
        {
            this.enabled = false;
            _controller.enabled = false;
        }

        /// Called when the Character dies. 
        /// Calls every abilities' Reset() method, so you can restore settings to their original value if needed
        public virtual void Reset()
        {
            _spawnDirectionForced = false;
            if (_characterAbilities == null) { return; }

            if (_characterAbilities.Length == 0) { return; }

            foreach (EnigmaCharacterAbility ability in _characterAbilities)
            {
                if (ability.enabled) { ability.ResetAbility(); }
            }
        }

        protected virtual void OnRevive() { }

        protected virtual void OnDeath()
        {
            if (MovementState.CurrentState != EnigmaCharacterStates.MovementStates.FallingDownHole) { MovementState.ChangeState(EnigmaCharacterStates.MovementStates.Idle); }
        }

        protected virtual void OnHit() { }

        /// OnEnable, we register our OnRevive event
        protected virtual void OnEnable()
        {
            if (CharacterHealth != null)
            {
                if (!_onReviveRegistered)
                {
                    CharacterHealth.OnRevive += OnRevive;
                    _onReviveRegistered = true;
                }

                CharacterHealth.OnDeath += OnDeath;
                CharacterHealth.OnHit += OnHit;
            }
        }

        /// OnDisable, we unregister our OnRevive event
        protected virtual void OnDisable()
        {
            if (CharacterHealth != null)
            {
                CharacterHealth.OnDeath -= OnDeath;
                CharacterHealth.OnHit -= OnHit;
            }
        }
    }
}