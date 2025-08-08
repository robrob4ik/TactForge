using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    public abstract class EnigmaController : MonoBehaviour
    {
        [Title("Debug")] 
        [ReadOnly] 
        [Tooltip("The current speed of the character")]
        public Vector3 Speed;

        [ReadOnly] 
        [Tooltip("The current velocity in units/second")]
        public Vector3 Velocity;

        [ReadOnly] 
        [Tooltip("The velocity of the character last frame")]
        public Vector3 VelocityLastFrame;

        [ReadOnly] 
        [Tooltip("The current acceleration")]
        public Vector3 Acceleration;

        [ReadOnly] 
        [Tooltip("Whether or not the character is grounded")]
        public bool Grounded;

        [ReadOnly] 
        [Tooltip("Whether or not the character got grounded this frame")]
        public bool JustGotGrounded;

        [ReadOnly] 
        [Tooltip("The current movement of the character")]
        public Vector3 CurrentMovement;

        [ReadOnly] 
        [Tooltip("The direction the character is going in")]
        public Vector3 CurrentDirection;

        [ReadOnly] 
        [Tooltip("The current added force, to be added to the character's movement")]
        public Vector3 AddedForce;

        [ReadOnly] 
        [Tooltip("Whether or not the character is in free movement mode or not")]
        public bool FreeMovement = true;
        
        [Title("Gravity")] 
        [Tooltip("The current gravity to apply to our character (positive goes down, negative goes up, higher value, higher acceleration)")]
        public float Gravity = 40f;

        [Tooltip("Whether or not the gravity is currently being applied to this character")]
        public bool GravityActive = true;
        
        public virtual Vector3 ColliderCenter { get { return Vector3.zero; } }

        public virtual Vector3 ColliderBottom { get { return Vector3.zero; } }

        public virtual Vector3 ColliderTop { get { return Vector3.zero; } }

        public virtual GameObject ObjectBelow { get; set; }

        public virtual Vector3 AppliedImpact { get { return _impact; } }

        protected Vector3 _positionLastFrame;
        protected Vector3 _speedComputation;
        protected bool _groundedLastFrame;
        protected Vector3 _impact;
        protected const float _smallValue = 0.0001f;
        
        protected virtual void Awake()
        {
            CurrentDirection = transform.forward;
        }
        
        protected virtual void Update()
        {
            CheckIfGrounded();
            DetermineDirection();
        }
        
        protected virtual void ComputeSpeed()
        {
            if (Time.deltaTime != 0f)
            {
                Speed = (this.transform.position - _positionLastFrame) / Time.deltaTime;
            }

            // we round the speed to 2 decimals
            Speed.x = Mathf.Round(Speed.x * 100f) / 100f;
            Speed.y = Mathf.Round(Speed.y * 100f) / 100f;
            Speed.z = Mathf.Round(Speed.z * 100f) / 100f;
            _positionLastFrame = this.transform.position;
        }
        
        protected virtual void DetermineDirection() { }
        
        protected virtual void FixedUpdate() { }
        
        protected virtual void LateUpdate() { }
        
        protected virtual void CheckIfGrounded()
        {
            JustGotGrounded = (!_groundedLastFrame && Grounded);
            _groundedLastFrame = Grounded;
        }

        public virtual void Impact(Vector3 direction, float force) { }
        
        public virtual void SetGravityActive(bool status)
        {
            GravityActive = status;
        }
        
        public virtual void AddForce(Vector3 movement) { }
        
        public virtual void SetMovement(Vector3 movement) { }

        public virtual void MovePosition(Vector3 newPosition, bool targetTransform = false) { }

        public virtual void ResizeColliderHeight(float newHeight, bool translateCenter = false) { }
        
        public virtual void ResetColliderSize() { }
        
        public virtual void CollisionsOn() { }

        public virtual void CollisionsOff() { }
        
        public virtual void Reset()
        {
            _impact = Vector3.zero;
            GravityActive = true;
            Speed = Vector3.zero;
            Velocity = Vector3.zero;
            VelocityLastFrame = Vector3.zero;
            Acceleration = Vector3.zero;
            Grounded = true;
            JustGotGrounded = false;
            CurrentMovement = Vector3.zero;
            CurrentDirection = Vector3.zero;
            AddedForce = Vector3.zero;
        }
    }
}