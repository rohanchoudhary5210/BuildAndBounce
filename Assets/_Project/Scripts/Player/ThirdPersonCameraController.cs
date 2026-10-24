using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Third-person orbital camera controller with predictive sphere-cast collision.
    /// Handles camera yaw/pitch orbiting from input (mouse & gamepad) and prevents camera
    /// clipping by smoothly tucking the camera in front of obstructing geometry.
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target & Input")]
        [Tooltip("Follow target pivot (e.g. CameraTarget at player chest/head height).")]
        [SerializeField] private Transform _target;

        [Tooltip("Player input reader providing LookInput (mouse delta / gamepad right stick).")]
        [SerializeField] private PlayerInputReader _inputReader;

        [Header("Sensitivity & Limits")]
        [Tooltip("Mouse look sensitivity multiplier.")]
        [SerializeField] private float _mouseSensitivity = 0.15f;

        [Tooltip("Gamepad stick look sensitivity in degrees per second.")]
        [SerializeField] private float _gamepadSensitivity = 120f;

        [Tooltip("Invert vertical camera look axis.")]
        [SerializeField] private bool _invertY = false;

        [Tooltip("Minimum downward pitch angle in degrees (looking up).")]
        [SerializeField] private float _minPitch = -25f;

        [Tooltip("Maximum upward pitch angle in degrees (looking down).")]
        [SerializeField] private float _maxPitch = 70f;

        [Header("Distance & Collision")]
        [Tooltip("Default resting distance behind the target.")]
        [SerializeField] private float _defaultDistance = 5.0f;

        [Tooltip("Minimum distance camera can be pushed towards target by geometry.")]
        [SerializeField] private float _minDistance = 0.8f;

        [Tooltip("Radius of the collision sphere probe to prevent edge clipping.")]
        [SerializeField] private float _collisionRadius = 0.25f;

        [Tooltip("Safety margin distance away from the hit surface.")]
        [SerializeField] private float _collisionMargin = 0.15f;

        [Tooltip("Speed at which the camera returns to resting distance when obstacles clear.")]
        [SerializeField] private float _zoomReturnSpeed = 12f;

        [Tooltip("Layers that block camera line of sight.")]
        [SerializeField] private LayerMask _collisionLayers = ~0;

        private float _yaw;
        private float _pitch = 15f;
        private float _currentDistance;

        public Transform Target => _target;
        public float CurrentDistance => _currentDistance;

        private void Start()
        {
            _currentDistance = _defaultDistance;

            // Initialize yaw and pitch from current camera orientation
            Vector3 angles = transform.eulerAngles;
            _pitch = angles.x;
            _yaw = angles.y;

            if (_target == null)
            {
                var targetComp = Object.FindAnyObjectByType<CameraTarget>();
                if (targetComp != null)
                {
                    _target = targetComp.transform;
                }
            }

            if (_inputReader == null && _target != null)
            {
                _inputReader = _target.GetComponentInParent<PlayerInputReader>();
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            ProcessInput();
            UpdateCameraTransform();
        }

        private void ProcessInput()
        {
            if (_inputReader == null) return;

            Vector2 look = _inputReader.LookInput;
            if (look.sqrMagnitude < 0.0001f) return;

            // Differentiate mouse delta from gamepad stick:
            // Mouse delta is typically unbounded and instantaneous per frame;
            // Gamepad stick is normalized (-1 to 1) and requires delta time scaling.
            float lookX;
            float lookY;

            if (look.sqrMagnitude <= 1.0f)
            {
                lookX = look.x * _gamepadSensitivity * Time.deltaTime;
                lookY = look.y * _gamepadSensitivity * Time.deltaTime * (_invertY ? 1f : -1f);
            }
            else
            {
                lookX = look.x * _mouseSensitivity;
                lookY = look.y * _mouseSensitivity * (_invertY ? 1f : -1f);
            }

            _yaw += lookX;
            _pitch += lookY;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 backwardDir = -(rotation * Vector3.forward);
            Vector3 targetPivot = _target.position;

            float targetDistance = _defaultDistance;

            // SphereCast from target pivot backward towards desired camera position
            if (Physics.SphereCast(
                targetPivot,
                _collisionRadius,
                backwardDir,
                out RaycastHit hit,
                targetDistance,
                _collisionLayers,
                QueryTriggerInteraction.Ignore))
            {
                // Pull camera in front of the hit point
                float safeDistance = Mathf.Max(hit.distance - _collisionMargin, _minDistance);
                _currentDistance = Mathf.Min(_currentDistance, safeDistance);
            }
            else
            {
                // Smoothly zoom camera back to default distance when obstruction clears
                _currentDistance = Mathf.MoveTowards(_currentDistance, targetDistance, _zoomReturnSpeed * Time.deltaTime);
            }

            transform.position = targetPivot + backwardDir * _currentDistance;
            transform.rotation = rotation;
        }

        /// <summary>
        /// Explicitly assigns the tracking target and input reader.
        /// </summary>
        public void Configure(Transform target, PlayerInputReader inputReader)
        {
            _target = target;
            _inputReader = inputReader;
            _currentDistance = _defaultDistance;
        }
    }
}
