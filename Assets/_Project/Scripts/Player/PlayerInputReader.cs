using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Decouples hardware input bindings from the movement motor. Reads actions from Unity's
    /// Input System and translates them into clean, pollable properties and events.
    /// This separation ensures movement physics stay deterministic and allows replays, ghosts,
    /// or AI agents to feed input without touching the character controller.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Component Source (Optional - will auto-locate if empty)")]
        [Tooltip("PlayerInput component providing action maps. If unassigned, looks on this GameObject.")]
        [SerializeField] private PlayerInput _playerInput;

        [Header("Action References (Optional - overrides PlayerInput lookup)")]
        [Tooltip("Explicit Move action reference (Vector2).")]
        [SerializeField] private InputActionReference _moveActionRef;

        [Tooltip("Explicit Jump action reference (Button).")]
        [SerializeField] private InputActionReference _jumpActionRef;

        [Tooltip("Explicit Dash action reference (Button).")]
        [SerializeField] private InputActionReference _dashActionRef;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;

        private Vector2 _moveInput;
        private bool _jumpTriggered;
        private bool _isJumpHeld;
        private bool _dashTriggered;

        public Vector2 MoveInput => _moveInput;
        public bool JumpTriggered => _jumpTriggered;
        public bool IsJumpHeld => _isJumpHeld;
        public bool DashTriggered => _dashTriggered;

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnDashPressed;

        private void Awake()
        {
            if (_playerInput == null)
            {
                TryGetComponent(out _playerInput);
            }
        }

        private void OnEnable()
        {
            ResolveActions();
            SubscribeActions();
        }

        private void OnDisable()
        {
            UnsubscribeActions();
        }

        private void Update()
        {
            PollContinuousInputs();
        }

        private void ResolveActions()
        {
            _moveAction = _moveActionRef != null ? _moveActionRef.action : _playerInput?.actions?.FindAction("Move");
            _jumpAction = _jumpActionRef != null ? _jumpActionRef.action : _playerInput?.actions?.FindAction("Jump");
            _dashAction = _dashActionRef != null ? _dashActionRef.action : (_playerInput?.actions?.FindAction("Dash") ?? _playerInput?.actions?.FindAction("Sprint"));
        }

        private void SubscribeActions()
        {
            if (_jumpAction != null)
            {
                _jumpAction.Enable();
                _jumpAction.performed += HandleJumpPerformed;
                _jumpAction.canceled += HandleJumpCanceled;
            }

            if (_dashAction != null)
            {
                _dashAction.Enable();
                _dashAction.performed += HandleDashPerformed;
            }

            if (_moveAction != null)
            {
                _moveAction.Enable();
            }
        }

        private void UnsubscribeActions()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= HandleJumpPerformed;
                _jumpAction.canceled -= HandleJumpCanceled;
            }

            if (_dashAction != null)
            {
                _dashAction.performed -= HandleDashPerformed;
            }
        }

        private void PollContinuousInputs()
        {
            if (_moveAction != null)
            {
                _moveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_jumpAction != null)
            {
                _isJumpHeld = _jumpAction.IsPressed();
            }
        }

        private void HandleJumpPerformed(InputAction.CallbackContext context)
        {
            _jumpTriggered = true;
            _isJumpHeld = true;
            OnJumpPressed?.Invoke();
        }

        private void HandleJumpCanceled(InputAction.CallbackContext context)
        {
            _isJumpHeld = false;
            OnJumpReleased?.Invoke();
        }

        private void HandleDashPerformed(InputAction.CallbackContext context)
        {
            _dashTriggered = true;
            OnDashPressed?.Invoke();
        }

        public void ConsumeJumpTrigger()
        {
            _jumpTriggered = false;
        }

        public void ConsumeDashTrigger()
        {
            _dashTriggered = false;
        }
    }
}
