using System;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Checkpoint trigger that records player progress through a course.
    /// Emits a C# event when first activated to notify the PlayerRespawn system.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Exact transform where the player respawns. Defaults to this transform if null.")]
        [SerializeField] private Transform _respawnPoint;

        [Tooltip("Index order of the checkpoint in the course sequence.")]
        [SerializeField] private int _checkpointIndex = 0;

        [Header("Visual Feedback (Optional)")]
        [Tooltip("Renderer whose color/emission changes when activated.")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Tooltip("Color applied to indicator when checkpoint is activated.")]
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.9f, 0.3f);

        private bool _isActivated;
        private Color _defaultColor;

        public static event Action<Checkpoint> OnCheckpointActivated;

        private void Awake()
        {
            if (_indicatorRenderer != null && _indicatorRenderer.sharedMaterial != null)
            {
                _defaultColor = _indicatorRenderer.sharedMaterial.color;
            }
        }

        private void OnEnable()
        {
            CourseStart.OnCourseReset += ResetCheckpoint;
        }

        private void OnDisable()
        {
            CourseStart.OnCourseReset -= ResetCheckpoint;
        }

        public Vector3 RespawnPosition => _respawnPoint != null 
            ? _respawnPoint.position 
            : transform.position + Vector3.up * (transform.localScale.y * 0.5f + 1.0f);

        public Quaternion RespawnRotation => _respawnPoint != null ? _respawnPoint.rotation : transform.rotation;
        public int CheckpointIndex => _checkpointIndex;
        public bool IsActivated => _isActivated;

        private void OnTriggerEnter(Collider other)
        {
            if (_isActivated) return;

            if (other.TryGetComponent<PlayerController>(out _))
            {
                ActivateCheckpoint();
            }
        }

        public void ActivateCheckpoint()
        {
            if (_isActivated) return;

            _isActivated = true;

            if (_indicatorRenderer != null)
            {
                _indicatorRenderer.material.color = _activeColor;
            }

            OnCheckpointActivated?.Invoke(this);
        }

        public void ResetCheckpoint()
        {
            _isActivated = false;
            if (_indicatorRenderer != null)
            {
                _indicatorRenderer.material.color = _defaultColor;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _isActivated ? Color.green : Color.yellow;
            Vector3 spawnPos = RespawnPosition;
            Gizmos.DrawWireSphere(spawnPos, 0.5f);
            Gizmos.DrawRay(spawnPos, RespawnRotation * Vector3.forward * 1.5f);
        }
    }
}
