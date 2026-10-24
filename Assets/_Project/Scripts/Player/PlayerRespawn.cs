using System;
using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Manages player respawning at the most recent active checkpoint or initial start point.
    /// Safely handles Unity CharacterController repositioning by briefly disabling the controller
    /// to avoid physics clipping issues during position teleportation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerRespawn : MonoBehaviour
    {
        [Header("Level References")]
        [SerializeField] private CourseStart _courseStart;

        private CharacterController _characterController;
        private PlayerController _playerController;

        private Vector3 _respawnPosition;
        private Quaternion _respawnRotation;

        public event Action OnRespawned;

        public Vector3 CurrentRespawnPosition => _respawnPosition;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerController = GetComponent<PlayerController>();

            _respawnPosition = transform.position;
            _respawnRotation = transform.rotation;

            if (_courseStart == null)
            {
                Debug.LogError($"[PlayerRespawn] CourseStart reference is missing on {gameObject.name}. Wire it in Inspector or run Player/Level Setup Tool.", this);
            }
            else
            {
                _respawnPosition = _courseStart.SpawnPosition;
                _respawnRotation = _courseStart.SpawnRotation;
            }
        }

        private void Start()
        {
            if (_courseStart != null)
            {
                _respawnPosition = _courseStart.SpawnPosition;
                _respawnRotation = _courseStart.SpawnRotation;
            }
        }

        private void OnEnable()
        {
            Checkpoint.OnCheckpointActivated += HandleCheckpointActivated;
            KillZone.OnPlayerEnteredKillZone += HandleKillZoneEntered;
            CourseStart.OnCourseReset += HandleCourseReset;
        }

        private void OnDisable()
        {
            Checkpoint.OnCheckpointActivated -= HandleCheckpointActivated;
            KillZone.OnPlayerEnteredKillZone -= HandleKillZoneEntered;
            CourseStart.OnCourseReset -= HandleCourseReset;
        }

        private void HandleCourseReset()
        {
            if (_courseStart != null)
            {
                _respawnPosition = _courseStart.SpawnPosition;
                _respawnRotation = _courseStart.SpawnRotation;
            }
        }

        private void HandleCheckpointActivated(Checkpoint checkpoint)
        {
            _respawnPosition = checkpoint.RespawnPosition;
            _respawnRotation = checkpoint.RespawnRotation;
        }

        private void HandleKillZoneEntered(PlayerController player)
        {
            if (player == _playerController)
            {
                Respawn();
            }
        }

        /// <summary>
        /// Instantly teleports the character to the last valid checkpoint and zeroes out accumulated velocity.
        /// </summary>
        public void Respawn()
        {
            RespawnAt(_respawnPosition, _respawnRotation);
        }

        /// <summary>
        /// Teleports the character to an explicit position and zeroes out accumulated velocity.
        /// </summary>
        public void RespawnAt(Vector3 position, Quaternion rotation)
        {
            _characterController.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            _characterController.enabled = true;

            _playerController.ResetVelocity();
            OnRespawned?.Invoke();
        }

        public void RespawnAt(Vector3 position) => RespawnAt(position, Quaternion.identity);

        /// <summary>
        /// Explicitly overrides the respawn position (e.g. from editor tools).
        /// </summary>
        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            _respawnPosition = position;
            _respawnRotation = rotation;
        }
    }
}
