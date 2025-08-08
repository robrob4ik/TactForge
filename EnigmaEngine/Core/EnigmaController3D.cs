using System;
using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Enigma Engine/Character/Core/Enigma Controller 3D")]
    public class EnigmaController3D : EnigmaController
    {
        public enum GroundedComputationModes
        {
            Simple,
            Advanced
        }
        
        public enum UpdateModes
        {
            Update,
            FixedUpdate
        }

        public enum ControllingModes
        {
            Player, 
            AI
        }

        
        [Tooltip("Is character is controlled by Player or AI?")]
        public ControllingModes ControllingMode = ControllingModes.AI;
        
        [ReadOnly] 
        [Tooltip("The current input sent to this character")]
        public Vector3 InputMoveDirection = Vector3.zero;
        
        [Title("Settings")]
        [Tooltip("Whether the movement computation should occur at Update or FixedUpdate. FixedUpdate is the recommended choice.")]
        public UpdateModes UpdateMode = UpdateModes.FixedUpdate;

        [Title("Raycasts")]
        [Tooltip("The layer to consider as obstacles (will prevent movement)")]
        public LayerMask ObstaclesLayerMask = EnigmaLayerManager.ObstaclesLayerMask;

        [Tooltip("The length of the raycasts to cast downwards")]
        public float GroundedRaycastLength = 5f;

        [Tooltip("The distance to the ground beyond which the character isn't considered grounded anymore")]
        public float MinimumGroundedDistance = 0.2f;

        [Tooltip("The selected modes to compute grounded state. Simple should only be used if your ground is even and flat")]
        public GroundedComputationModes GroundedComputationMode = GroundedComputationModes.Advanced;

        [Tooltip("A threshold against which to check when going over steps. Adjust that value if your character has issues going over small steps")]
        public float GroundNormalHeightThreshold = 0.2f;
        [Tooltip("The speed at which external forces get lerped to zero")]
        public float ImpactFalloff = 5f;
        [Title("Movement")]
        [Tooltip("The maximum vertical velocity the character can have while falling")]
        public float MaximumFallSpeed = 20.0f;

        [Tooltip("The factor by which to multiply the speed while walking on a slope. x is the angle, y is the factor")]
        public AnimationCurve SlopeSpeedMultiplier = new AnimationCurve(new Keyframe(-90, 1), new Keyframe(0, 1), new Keyframe(90, 0));

        [Title("Steep Surfaces")]
        [Tooltip("The current surface normal vector")]
        [ReadOnly]
        public Vector3 GroundNormal = Vector3.zero;
        
        public override Vector3 ColliderCenter { get { return this.transform.position + _characterController.center; } }

        public override Vector3 ColliderBottom { get { return this.transform.position + _characterController.center + Vector3.down * _characterController.bounds.extents.y; } }

        public override Vector3 ColliderTop { get { return this.transform.position + _characterController.center + Vector3.up * _characterController.bounds.extents.y; } }
        
        public virtual bool CollidingAbove()
        {
            return (_collisionFlags & CollisionFlags.CollidedAbove) != 0;
        }
        
        protected Transform _transform;
        protected Collider _collider;
        protected CharacterController _characterController;
        protected float _originalColliderHeight;
        protected Vector3 _originalColliderCenter;
        protected Vector3 _originalSizeRaycastOrigin;
        protected Vector3 _lastGroundNormal = Vector3.zero;
        //protected WaitForFixedUpdate _waitForFixedUpdate = new WaitForFixedUpdate();
        protected bool _detached = false;

        // char movement
        protected CollisionFlags _collisionFlags;
        protected Vector3 _frameVelocity = Vector3.zero;
        protected Vector3 _hitPoint = Vector3.zero;
        protected Vector3 _lastHitPoint = new Vector3(Mathf.Infinity, 0, 0);

        // velocity
        protected Vector3 _newVelocity;
        protected Vector3 _lastHorizontalVelocity;
        protected Vector3 _newHorizontalVelocity;
        protected Vector3 _motion;
        protected Vector3 _idealVelocity;
        protected Vector3 _idealDirection;
        protected Vector3 _horizontalVelocityDelta;
        protected float _stickyOffset = 0f;

        // move position
        protected RaycastHit _movePositionHit;
        protected Vector3 _capsulePoint1;
        protected Vector3 _capsulePoint2;
        protected Vector3 _movePositionDirection;
        protected float _movePositionDistance;

        // collision detection
        protected RaycastHit _cardinalRaycast;
        
        // character consts
        protected float   _skinWidth;    
        protected float   _radius;
        protected float   _height;
        protected Vector3 _center;

        protected float _smallestDistance = Single.MaxValue;
        protected float _longestDistance = Single.MinValue;

        protected RaycastHit _smallestRaycast;
        protected RaycastHit _emptyRaycast = new RaycastHit();
        protected Vector3 _downRaycastsOffset;
        protected RaycastHit _raycastDownHit;
        protected Vector3 _raycastDownDirection = Vector3.down;
        protected RaycastHit _canGoBackHeadCheck;
        protected bool _tooSteepLastFrame;
        

        /// On awake we store our various components for future use
        protected override void Awake()
        {
            base.Awake();

            _characterController = this.gameObject.GetComponent<CharacterController>();
            _transform = this.transform;
            _collider = this.gameObject.GetComponent<Collider>();
            _originalColliderHeight = _characterController.height;
            _originalColliderCenter = _characterController.center;
            
            _skinWidth = _characterController.skinWidth;
            _radius    = _characterController.radius;
            _height    = _characterController.height;
            _center    = _characterController.center;
        }

        #region Update

        /// On late update we apply any impact we have in store, and store our velocity for use next frame
        protected override void LateUpdate()
        {
            base.LateUpdate();
            VelocityLastFrame = Velocity;
        }


        /// On Update we process our Update computations if UpdateMode is set to Update
        protected override void Update()
        {
            base.Update();
            if (UpdateMode == UpdateModes.Update)
            {
                ProcessUpdate();
            }
        }
        
        /// On FixedUpdate we process our Update computations if UpdateMode is set to FixedUpdate
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (UpdateMode == UpdateModes.FixedUpdate)
            {
                ProcessUpdate();
            }
        }
        
        protected virtual void ProcessUpdate()
        {
            if (_transform == null) return;
            
            if (!FreeMovement) return;
            
            _newVelocity = Velocity;
            _positionLastFrame = _transform.position;
            
            if (ControllingMode == ControllingModes.Player)
            {
                AddInput();
                AddGravity();
                ComputeVelocityDelta();
                MoveCharacterController();
                ComputeNewVelocity();
                ManualControllerColliderHit();
                HandleGroundContact();
                ComputeSpeed();
            }
            else
            {
                //AddGravity();
                //HandleGroundContact();
            }
           
        }

        /// Determines the new velocity and the input 
        protected virtual void AddInput()
        {
            _idealVelocity = CurrentMovement;
            
            if (Grounded)
            {
                Vector3 sideways = Vector3.Cross(Vector3.up, _idealVelocity);
                _idealVelocity = Vector3.Cross(sideways, GroundNormal).normalized * _idealVelocity.magnitude;
            }

            _newVelocity = _idealVelocity;
            _newVelocity.y = Grounded ? Mathf.Min(_newVelocity.y, 0) : _newVelocity.y;
        }
        
        /// Adds the gravity to the new velocity and any AddedForce we may have
        protected virtual void AddGravity()
        {
            if (GravityActive)
            {
                if (Grounded)
                {
                    _newVelocity.y = Mathf.Min(0, _newVelocity.y) - Gravity * Time.deltaTime;
                }
                else
                {
                    _newVelocity.y = Velocity.y - Gravity * Time.deltaTime;
                    _newVelocity.y = Mathf.Max(_newVelocity.y, -MaximumFallSpeed);
                }
            }

            _newVelocity += AddedForce;
            AddedForce = Vector3.zero;
        }
        
        /// Computes the motion vector to apply to the character controller 
        protected virtual void ComputeVelocityDelta()
        {
            _motion = _newVelocity * Time.deltaTime;
            _horizontalVelocityDelta.x = _motion.x;
            _horizontalVelocityDelta.y = 0f;
            _horizontalVelocityDelta.z = _motion.z;
            _stickyOffset = Mathf.Max(_characterController.stepOffset, _horizontalVelocityDelta.magnitude);
            if (Grounded)
            {
                _motion -= _stickyOffset * Vector3.up;
            }
        }
        
        /// Moves the character controller by the computed _motion 
        protected virtual void MoveCharacterController()
        {
            GroundNormal.x = GroundNormal.y = GroundNormal.z = 0f;

            _collisionFlags = _characterController.Move(_motion);

            _lastHitPoint = _hitPoint;
            _lastGroundNormal = GroundNormal;
        }

        /// Determines the new Velocity value based on our position and our position last frame
        protected virtual void ComputeNewVelocity()
        {
            Velocity = _newVelocity;
            Acceleration = (Velocity - VelocityLastFrame) / Time.deltaTime;
        }
        
        /// We handle ground contact, velocity transfer and moving platforms
        protected virtual void HandleGroundContact()
        {
            Grounded = _characterController.isGrounded;

            if (Grounded && !IsGroundedTest())
            {
                Grounded = false;
            }
            else if (!Grounded && IsGroundedTest())
            {
                if (_detached && Velocity.y <= 0)
                {
                    Grounded = true;
                    _detached = false;
                }
            }
        }

        /// Determines the direction based on the current movement
        protected override void DetermineDirection()
        {
            if (CurrentMovement.magnitude > 0f)
            {
                CurrentDirection = CurrentMovement.normalized;
            }
        }
        #endregion

        #region Rigidbody push mechanics
      
        /// This method compensates for the regular OnControllerColliderHit, which unfortunately generates a lot of garbage.
        /// To do so, it casts a ray downwards to get our ground normal, and a ray in the current movement direction to (potentially) push rigidbodies
        protected virtual void ManualControllerColliderHit()
        {
            HandleAdvancedGroundDetection();
        }

        protected virtual void HandleAdvancedGroundDetection()
        {
            if (GroundedComputationMode != GroundedComputationModes.Advanced)
            {
                return;
            }

            _smallestDistance = Single.MaxValue;
            _longestDistance = Single.MinValue;
            _smallestRaycast = _emptyRaycast;

            // we cast 4 rays downwards to get ground normal
            float offset = _radius;

            _downRaycastsOffset.x = 0f;
            _downRaycastsOffset.y = 0f;
            _downRaycastsOffset.z = 0f;
            CastRayDownwards();
            _downRaycastsOffset.x = -offset;
            _downRaycastsOffset.y = offset;
            _downRaycastsOffset.z = 0f;
            CastRayDownwards();
            _downRaycastsOffset.x = 0f;
            _downRaycastsOffset.y = offset;
            _downRaycastsOffset.z = -offset;
            CastRayDownwards();
            _downRaycastsOffset.x = offset;
            _downRaycastsOffset.y = offset;
            _downRaycastsOffset.z = 0f;
            CastRayDownwards();
            _downRaycastsOffset.x = 0f;
            _downRaycastsOffset.y = offset;
            _downRaycastsOffset.z = offset;
            CastRayDownwards();

            // we handle our shortest ray
            if (_smallestRaycast.collider != null)
            {
                if (_smallestRaycast.normal.y > 0 && _smallestRaycast.normal.y > GroundNormal.y)
                {
                    if ((Mathf.Abs(_smallestRaycast.point.y - _lastHitPoint.y) < GroundNormalHeightThreshold) && ((_smallestRaycast.point != _lastHitPoint) || (_lastGroundNormal == Vector3.zero)))
                    {
                        GroundNormal = _smallestRaycast.normal;
                    }
                    else
                    {
                        GroundNormal = _lastGroundNormal;
                    }

                    _hitPoint = _smallestRaycast.point;
                    _frameVelocity.x = _frameVelocity.y = _frameVelocity.z = 0f;
                }
            }
        }
        
        /// Casts a ray downwards and adjusts distances if needed
        protected virtual void CastRayDownwards()
        {
            if (_smallestDistance <= MinimumGroundedDistance)
            {
                return;
            }

            Physics.Raycast(this._transform.position + _center + _downRaycastsOffset, _raycastDownDirection, out _raycastDownHit,
                _height / 2f + GroundedRaycastLength, ObstaclesLayerMask);

            if (_raycastDownHit.collider != null)
            {
                float adjustedDistance = AdjustDistance(_raycastDownHit.distance);

                if (adjustedDistance < _smallestDistance)
                {
                    _smallestDistance = adjustedDistance;
                    _smallestRaycast = _raycastDownHit;
                }

                if (adjustedDistance > _longestDistance)
                {
                    _longestDistance = adjustedDistance;
                }
            }
        }

        /// Returns the real distance between the extremity of the character and the ground
        protected float AdjustDistance(float distance)
        {
            float adjustedDistance = distance - _height / 2f -
                                     _skinWidth;
            return adjustedDistance;
        }

        protected Vector3 _onTriggerEnterPushbackDirection;
        
        protected virtual void OnTriggerEnter(Collider other)
        {
        }

        #endregion

        #region Collider Resizing
        /// Resizes the collider to the new size set in parameters
        public override void ResizeColliderHeight(float newHeight, bool translateCenter = false)
        {
            float newYOffset = _originalColliderCenter.y - (_originalColliderHeight - newHeight) / 2;
            _characterController.height = newHeight;
            _characterController.center = ((_originalColliderHeight - newHeight) / 2) * Vector3.up;

            if (translateCenter)
            {
                this.transform.Translate((newYOffset / 2f) * Vector3.up);
            }
        }
        
        /// Returns the collider to its initial size
        public override void ResetColliderSize()
        {
            _characterController.height = _originalColliderHeight;
            _characterController.center = _originalColliderCenter;
        }

        #endregion

        #region Grounded Tests
        public virtual bool IsGroundedTest()
        {
            bool grounded = false;
            if (GroundedComputationMode == GroundedComputationModes.Advanced)
            {
                if (_smallestDistance <= MinimumGroundedDistance)
                {
                    grounded = (GroundNormal.y > 0.01);
                }
            }
            else
            {
                grounded = _characterController.isGrounded;
                GroundNormal.x = 0;
                GroundNormal.y = 1;
                GroundNormal.z = 0;
            }

            return grounded;
        }
        
        /// Grounded check
        protected override void CheckIfGrounded()
        {
            JustGotGrounded = (!_groundedLastFrame && Grounded);
            _groundedLastFrame = Grounded;
        }
        #endregion

        #region Public Methods
        public override void CollisionsOn()
        {
            _collider.enabled = true;
        }
        
        public override void CollisionsOff()
        {
            _collider.enabled = false;
        }
        
        public override void AddForce(Vector3 movement)
        {
            AddedForce += movement;
        }
        
        
        public override void SetMovement(Vector3 movement)
        {
            CurrentMovement = movement;

            Vector3 directionVector;
            directionVector = movement;
            if (directionVector != Vector3.zero)
            {
                float directionLength = directionVector.magnitude;
                directionVector = directionVector / directionLength;
                directionLength = Mathf.Min(1, directionLength);
                directionLength = directionLength * directionLength;
                directionVector = directionVector * directionLength;
            }

            InputMoveDirection = transform.rotation * directionVector;
        }

        public override void MovePosition(Vector3 newPosition, bool targetTransform = false)
        {
            _movePositionDirection = (newPosition - this.transform.position);
            _movePositionDistance = Vector3.Distance(this.transform.position, newPosition);

            _capsulePoint1 = this.transform.position
                             + _center
                             - (Vector3.up * _height / 2f)
                             + Vector3.up * _skinWidth
                             + Vector3.up * _radius;
            _capsulePoint2 = this.transform.position
                             + _center
                             + (Vector3.up * _height / 2f)
                             - Vector3.up * _skinWidth
                             - Vector3.up * _radius;

            if (!Physics.CapsuleCast(_capsulePoint1, _capsulePoint2, _radius, _movePositionDirection, out _movePositionHit, _movePositionDistance, ObstaclesLayerMask))
            {
                this.transform.position = newPosition;
            }
        }
        
        public override void Reset()
        {
            base.Reset();
            _idealDirection = Vector3.zero;
            _idealVelocity = Vector3.zero;
            _newVelocity = Vector3.zero;
            _lastGroundNormal = Vector3.zero;
            _detached = false;
            _frameVelocity = Vector3.zero;
            _hitPoint = Vector3.zero;
            _lastHitPoint = new Vector3(Mathf.Infinity, 0, 0);
        }

        #endregion
    }
}