using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Smooth follow pivot for the third-person camera. Placed at chest/head height on the player
    /// to isolate camera motion from abrupt footstep collisions, slope snapping, and step offsets.
    /// </summary>
    public class CameraTarget : MonoBehaviour
    {
        [Header("Target Offset")]
        [Tooltip("Target transform to follow. If null, uses the parent transform.")]
        [SerializeField] private Transform _target;

        [Tooltip("Vertical height offset above the target origin (around head/shoulder height).")]
        [SerializeField] private float _heightOffset = 1.4f;

        [Tooltip("Horizontal forward/backward offset.")]
        [SerializeField] private float _forwardOffset = 0f;

        public Transform Target => _target;
        public float HeightOffset => _heightOffset;

        private void Awake()
        {
            if (_target == null && transform.parent != null)
            {
                _target = transform.parent;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPosition = _target.position + Vector3.up * _heightOffset + _target.forward * _forwardOffset;
            transform.position = targetPosition;
        }
    }
}
