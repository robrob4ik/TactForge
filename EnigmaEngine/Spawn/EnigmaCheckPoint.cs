using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    /// An event to trigger when a checkpoint is reached
    public struct CheckPointEvent
    {
        public int Order;

        public CheckPointEvent(int order)
        {
            Order = order;
        }

        static CheckPointEvent e;

        public static void Trigger(int order)
        {
            e.Order = order;
            MMEventManager.TriggerEvent(e);
        }
    }


    /// Checkpoint class. Will make the player respawn at this point if it dies.
    [AddComponentMenu("Enigma Engine/Spawn/Enigma Checkpoint")]
    public class EnigmaCheckPoint : MonoBehaviour
    {
        [Title("Spawn")]
        [MMInformation(
            "Add this script to a (preferrably empty) GameObject and it'll be added to the level's checkpoint list, allowing you to respawn from there. If you bind it to the LevelManager's starting point, that's where your character will spawn at the start of the level. And here you can decide whether the character should spawn facing left or right.",
            MMInformationAttribute.InformationType.Info, false)]
        /// the facing direction the character should face when spawning from this checkpoint
        [Tooltip("The facing direction the character should face when spawning from this checkpoint")]
        public EnigmaCharacter.FacingDirections FacingDirection = EnigmaCharacter.FacingDirections.East;

        /// whether or not this checkpoint should override any order and assign itself on entry
        [Tooltip("Whether or not this checkpoint should override any order and assign itself on entry")]
        public bool ForceAssignation = false;

        /// the order of the checkpoint
        [Tooltip("the order of the checkpoint")]
        public int CheckPointOrder;

        protected List<EnigmaRespawnable> _listeners;


        /// Initializes the list of listeners
        protected virtual void Awake()
        {
            _listeners = new List<EnigmaRespawnable>();
        }


        /// Spawns the player at the checkpoint.
        /// <param name="player">Player.</param>
        public virtual void SpawnPlayer(EnigmaCharacter player)
        {
            player.RespawnAt(transform, FacingDirection);

            foreach (EnigmaRespawnable listener in _listeners)
            {
                listener.OnPlayerRespawn(this, player);
            }
        }


        /// Assigns the Respawnable to this checkpoint
        /// <param name="listener"></param>
        public virtual void AssignObjectToCheckPoint(EnigmaRespawnable listener)
        {
            _listeners.Add(listener);
        }


        /// Describes what happens when something enters the checkpoint
        /// <param name="collider">Something colliding with the water.</param>
        protected virtual void OnTriggerEnter2D(Collider2D collider)
        {
            TriggerEnter(collider.gameObject);
        }

        protected virtual void OnTriggerEnter(Collider collider)
        {
            TriggerEnter(collider.gameObject);
        }

        protected virtual void TriggerEnter(GameObject collider)
        {
            EnigmaCharacter character = collider.GetComponent<EnigmaCharacter>();

            if (character == null)
            {
                return;
            }

            if (character.CharacterType != EnigmaCharacter.CharacterTypes.Player)
            {
                return;
            }

            if (!EnigmaLevelManager.HasInstance)
            {
                return;
            }

            EnigmaLevelManager.Instance.SetCurrentCheckpoint(this);
            CheckPointEvent.Trigger(CheckPointOrder);
        }


        /// On DrawGizmos, we draw lines to show the path the object will follow
        protected virtual void OnDrawGizmos()
        {
#if UNITY_EDITOR

            if (!EnigmaLevelManager.HasInstance)
            {
                return;
            }

            if (EnigmaLevelManager.Instance.Checkpoints == null)
            {
                return;
            }

            if (EnigmaLevelManager.Instance.Checkpoints.Count == 0)
            {
                return;
            }

            for (int i = 0; i < EnigmaLevelManager.Instance.Checkpoints.Count; i++)
            {
                // we draw a line towards the next point in the path
                if ((i + 1) < EnigmaLevelManager.Instance.Checkpoints.Count)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(EnigmaLevelManager.Instance.Checkpoints[i].transform.position, EnigmaLevelManager.Instance.Checkpoints[i + 1].transform.position);
                }
            }
#endif
        }
    }
}