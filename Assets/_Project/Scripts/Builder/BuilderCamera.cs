using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Free-flight / orbital editor camera used exclusively during Build Mode.
    /// Supports WASD panning, right-click orbit, Q/E elevation, and scroll-wheel zoom.
    /// </summary>
    public class BuilderCamera : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float _moveSpeed = 16f;
        [SerializeField] private float _fastMoveMultiplier = 2.5f;
        [SerializeField] private float _lookSensitivity = 2.2f;
        [SerializeField] private float _zoomSensitivity = 4f;

        private float _yaw;
        private float _pitch = 35f;
        private Camera _camera;

        private void Awake()
        {
            // Builder camera must never duplicate the primary AudioListener on Main Camera
            if (TryGetComponent<AudioListener>(out var listener))
            {
                listener.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(listener);
                }
                else
                {
                    DestroyImmediate(listener);
                }
            }

            _camera = GetComponent<Camera>();
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void OnEnable()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            HandleOrbit();
            HandleMovement();
            HandleZoom();
        }

        private void HandleOrbit()
        {
            if (Mouse.current == null) return;

            // Orbit only while Right Mouse Button is held
            if (Mouse.current.rightButton.isPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * _lookSensitivity * 0.1f;
                _pitch -= delta.y * _lookSensitivity * 0.1f;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);

                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMovement()
        {
            if (Keyboard.current == null) return;

            float speed = _moveSpeed;
            if (Keyboard.current.leftShiftKey.isPressed)
            {
                speed *= _fastMoveMultiplier;
            }

            Vector3 move = Vector3.zero;

            if (Keyboard.current.wKey.isPressed) move += transform.forward;
            if (Keyboard.current.sKey.isPressed) move -= transform.forward;
            if (Keyboard.current.dKey.isPressed) move += transform.right;
            if (Keyboard.current.aKey.isPressed) move -= transform.right;
            if (Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed) move += Vector3.up;
            if (Keyboard.current.qKey.isPressed || Keyboard.current.leftCtrlKey.isPressed) move -= Vector3.up;

            if (move.sqrMagnitude > 0.001f)
            {
                transform.position += move.normalized * speed * Time.unscaledDeltaTime;
            }
        }

        private void HandleZoom()
        {
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                transform.position += transform.forward * (scroll * _zoomSensitivity * 0.01f);
            }
        }

        public void FocusOn(Vector3 targetPosition, float distance = 15f)
        {
            transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            _yaw = 45f;
            _pitch = 35f;
            transform.position = targetPosition - transform.forward * distance;
        }
    }
}