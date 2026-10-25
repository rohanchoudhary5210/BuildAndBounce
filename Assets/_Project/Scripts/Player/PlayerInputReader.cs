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

        [Tooltip("Explicit Look action reference (Vector2).")]
        [SerializeField] private InputActionReference _lookActionRef;

        [Tooltip("Explicit Jump action reference (Button).")]
        [SerializeField] private InputActionReference _jumpActionRef;

        [Tooltip("Explicit Dash action reference (Button).")]
        [SerializeField] private InputActionReference _dashActionRef;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;
        private InputAction _ownedDashAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private Vector2 _virtualMoveInput;
        private bool _hasVirtualMove;
        private bool _jumpTriggered;
        private bool _isJumpHeld;
        private bool _dashTriggered;

        public Vector2 MoveInput => _moveInput;
        public Vector2 LookInput => _lookInput;
        public bool IsPointerLookControl => _lookAction?.activeControl?.device is Pointer;
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

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorLocked(hasFocus);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnEnable()
        {
            ResolveActions();
            SubscribeActions();
            BuildAndBounce.UI.PauseMenu.OnPauseStateChanged += HandlePauseStateChanged;
        }

        private void OnDisable()
        {
            UnsubscribeActions();
            BuildAndBounce.UI.PauseMenu.OnPauseStateChanged -= HandlePauseStateChanged;
            SetCursorLocked(false);
        }

        private void HandlePauseStateChanged(bool isPaused)
        {
            SetCursorLocked(!isPaused);
        }

        private void Update()
        {
            PollContinuousInputs();

            // Re-lock cursor when game view is clicked during active play
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && Time.timeScale > 0f)
            {
                SetCursorLocked(true);
            }
        }

        private void ResolveActions()
        {
            _moveAction = _moveActionRef != null ? _moveActionRef.action : _playerInput?.actions?.FindAction("Move");
            _lookAction = _lookActionRef != null ? _lookActionRef.action : _playerInput?.actions?.FindAction("Look");
            _jumpAction = _jumpActionRef != null ? _jumpActionRef.action : _playerInput?.actions?.FindAction("Jump");
            _dashAction = _dashActionRef != null ? _dashActionRef.action : _playerInput?.actions?.FindAction("Dash");

            // The default actions asset has no Dash action (Sprint is bound to the left-stick click),
            // so build one with the bindings the README documents.
            if (_dashAction == null)
            {
                _ownedDashAction ??= CreateDefaultDashAction();
                _dashAction = _ownedDashAction;
            }
        }

        private static InputAction CreateDefaultDashAction()
        {
            var dash = new InputAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/leftShift");
            dash.AddBinding("<Keyboard>/f");
            dash.AddBinding("<Gamepad>/buttonWest");
            return dash;
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

            if (_lookAction != null)
            {
                _lookAction.Enable();
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

            _ownedDashAction?.Disable();
        }

        private void OnDestroy()
        {
            _ownedDashAction?.Dispose();
        }

        private void PollContinuousInputs()
        {
            if (_hasVirtualMove)
            {
                _moveInput = _virtualMoveInput;
            }
            else if (_moveAction != null)
            {
                _moveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_lookAction != null)
            {
                _lookInput = _lookAction.ReadValue<Vector2>();
            }

            if (_jumpAction != null)
            {
                _isJumpHeld = _jumpAction.IsPressed();
            }
        }

        public void SetVirtualMoveInput(Vector2 move)
        {
            _virtualMoveInput = move;
            _hasVirtualMove = move.sqrMagnitude > 0.001f;
        }

        public void TriggerVirtualJumpPress()
        {
            _jumpTriggered = true;
            _isJumpHeld = true;
            OnJumpPressed?.Invoke();
        }

        public void TriggerVirtualJumpRelease()
        {
            _isJumpHeld = false;
            OnJumpReleased?.Invoke();
        }

        public void TriggerVirtualDash()
        {
            _dashTriggered = true;
            OnDashPressed?.Invoke();
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
