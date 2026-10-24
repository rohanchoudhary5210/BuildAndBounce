using System;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Checkpoint trigger that records player progress through a course.
    /// Emits a C# event when first activated to notify the PlayerRespawn system.
    /// </summary>
    [RequireComponent(typeof(Collider))]
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
        private MaterialPropertyBlock _propBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static event Action<Checkpoint> OnCheckpointActivated;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            _propBlock = new MaterialPropertyBlock();

            if (_indicatorRenderer != null && _indicatorRenderer.sharedMaterial != null)
            {
                if (_indicatorRenderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    _defaultColor = _indicatorRenderer.sharedMaterial.GetColor(BaseColorId);
                }
                else if (_indicatorRenderer.sharedMaterial.HasProperty(ColorId))
                {
                    _defaultColor = _indicatorRenderer.sharedMaterial.GetColor(ColorId);
                }
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
            SetIndicatorColor(_activeColor);

            OnCheckpointActivated?.Invoke(this);
        }

        public void ResetCheckpoint()
        {
            _isActivated = false;
            SetIndicatorColor(_defaultColor);
        }

        private void SetIndicatorColor(Color color)
        {
            if (_indicatorRenderer == null) return;

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            _indicatorRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, color);
            _propBlock.SetColor(ColorId, color);
            _indicatorRenderer.SetPropertyBlock(_propBlock);
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
