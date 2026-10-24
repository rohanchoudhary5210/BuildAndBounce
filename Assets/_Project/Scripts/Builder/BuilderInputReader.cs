using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Decouples hardware input bindings from LevelBuilder.
    /// Reads from the 'Builder' action map (supporting Keyboard&Mouse, Gamepad, and Touch)
    /// and exposes clean, queryable properties and cursor positions for build mode.
    /// </summary>
    public class BuilderInputReader : MonoBehaviour
    {
        [Header("Action Asset")]
        [SerializeField] private InputActionAsset _actionsAsset;
        [SerializeField] private PlayerInput _playerInput;

        [Header("Virtual Cursor (Gamepad)")]
        [SerializeField] private float _gamepadCursorSpeed = 800f;

        private InputActionMap _builderMap;
        private InputAction _placeAction;
        private InputAction _deleteAction;
        private InputAction _rotateAction;
        private InputAction _undoAction;
        private InputAction _redoAction;
        private InputAction _nextBlockAction;
        private InputAction _prevBlockAction;
        private InputAction _toggleModeAction;
        private InputAction _cursorMoveAction;

        private Vector2 _cursorPosition;
        private Vector2 _virtualGamepadCursor;
        private bool _isUsingGamepad;

        public Vector2 CursorPosition => _cursorPosition;
        public bool PlaceTriggered => _placeAction != null && _placeAction.WasPressedThisFrame();
        public bool DeleteTriggered => _deleteAction != null && _deleteAction.WasPressedThisFrame();
        public bool RotateTriggered => _rotateAction != null && _rotateAction.WasPressedThisFrame();
        public bool UndoTriggered => _undoAction != null && _undoAction.WasPressedThisFrame();
        public bool RedoTriggered => _redoAction != null && _redoAction.WasPressedThisFrame();
        public bool NextBlockTriggered => _nextBlockAction != null && _nextBlockAction.WasPressedThisFrame();
        public bool PrevBlockTriggered => _prevBlockAction != null && _prevBlockAction.WasPressedThisFrame();
        public bool ToggleModeTriggered => _toggleModeAction != null && _toggleModeAction.WasPressedThisFrame();
        public bool IsUsingGamepad => _isUsingGamepad;

        private void Awake()
        {
            InitializeVirtualCursor();
            ResolveBuilderActions();
        }

        private void InitializeVirtualCursor()
        {
            _virtualGamepadCursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _cursorPosition = _virtualGamepadCursor;
        }

        private void OnEnable()
        {
            EnableBuilderInputs();
        }

        private void OnDisable()
        {
            DisableBuilderInputs();
        }

        public void EnableBuilderInputs()
        {
            if (_builderMap != null && !_builderMap.enabled)
            {
                _builderMap.Enable();
            }
        }

        public void DisableBuilderInputs()
        {
            if (_builderMap != null && _builderMap.enabled)
            {
                _builderMap.Disable();
            }
        }

        private void Update()
        {
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            if (_cursorMoveAction == null) return;

            var control = _cursorMoveAction.activeControl;
            if (control != null && control.device is Gamepad)
            {
                _isUsingGamepad = true;
                Vector2 stick = _cursorMoveAction.ReadValue<Vector2>();
                if (stick.sqrMagnitude > 0.01f)
                {
                    _virtualGamepadCursor += stick * _gamepadCursorSpeed * Time.unscaledDeltaTime;
                    _virtualGamepadCursor.x = Mathf.Clamp(_virtualGamepadCursor.x, 0f, Screen.width);
                    _virtualGamepadCursor.y = Mathf.Clamp(_virtualGamepadCursor.y, 0f, Screen.height);
                }
                _cursorPosition = _virtualGamepadCursor;
            }
            else
            {
                // Pointer / Mouse / Touch gives direct screen position
                Vector2 readPos = _cursorMoveAction.ReadValue<Vector2>();
                if (readPos.sqrMagnitude > 0.1f)
                {
                    _cursorPosition = readPos;
                    _virtualGamepadCursor = readPos;
                    _isUsingGamepad = false;
                }
            }
        }

        private void ResolveBuilderActions()
        {
            if (_actionsAsset == null && _playerInput != null)
            {
                _actionsAsset = _playerInput.actions;
            }

            if (_actionsAsset != null)
            {
                _builderMap = _actionsAsset.FindActionMap("Builder", throwIfNotFound: false);
            }

            if (_builderMap == null)
            {
                CreateRuntimeFallbackActions();
            }
            else
            {
                _placeAction = _builderMap.FindAction("Place");
                _deleteAction = _builderMap.FindAction("Delete");
                _rotateAction = _builderMap.FindAction("Rotate");
                _undoAction = _builderMap.FindAction("Undo");
                _redoAction = _builderMap.FindAction("Redo");
                _nextBlockAction = _builderMap.FindAction("NextBlock");
                _prevBlockAction = _builderMap.FindAction("PreviousBlock");
                _toggleModeAction = _builderMap.FindAction("ToggleMode");
                _cursorMoveAction = _builderMap.FindAction("CursorMove");
            }
        }

        private void CreateRuntimeFallbackActions()
        {
            _builderMap = new InputActionMap("Builder");

            _placeAction = _builderMap.AddAction("Place", InputActionType.Button);
            _placeAction.AddBinding("<Mouse>/leftButton");
            _placeAction.AddBinding("<Keyboard>/space");
            _placeAction.AddBinding("<Gamepad>/buttonSouth");
            _placeAction.AddBinding("<Touchscreen>/primaryTouch/tap");

            _deleteAction = _builderMap.AddAction("Delete", InputActionType.Button);
            _deleteAction.AddBinding("<Keyboard>/x");
            _deleteAction.AddBinding("<Keyboard>/delete");
            _deleteAction.AddBinding("<Gamepad>/buttonWest");

            _rotateAction = _builderMap.AddAction("Rotate", InputActionType.Button);
            _rotateAction.AddBinding("<Keyboard>/r");
            _rotateAction.AddBinding("<Gamepad>/buttonNorth");
            _rotateAction.AddBinding("<Gamepad>/rightShoulder");

            _undoAction = _builderMap.AddAction("Undo", InputActionType.Button);
            _undoAction.AddBinding("<Keyboard>/z");
            _undoAction.AddBinding("<Gamepad>/leftShoulder");

            _redoAction = _builderMap.AddAction("Redo", InputActionType.Button);
            _redoAction.AddBinding("<Keyboard>/y");
            _redoAction.AddBinding("<Gamepad>/rightTrigger");

            _nextBlockAction = _builderMap.AddAction("NextBlock", InputActionType.Button);
            _nextBlockAction.AddBinding("<Keyboard>/e");
            _nextBlockAction.AddBinding("<Keyboard>/rightArrow");
            _nextBlockAction.AddBinding("<Gamepad>/dpad/right");

            _prevBlockAction = _builderMap.AddAction("PreviousBlock", InputActionType.Button);
            _prevBlockAction.AddBinding("<Keyboard>/q");
            _prevBlockAction.AddBinding("<Keyboard>/leftArrow");
            _prevBlockAction.AddBinding("<Gamepad>/dpad/left");

            _toggleModeAction = _builderMap.AddAction("ToggleMode", InputActionType.Button);
            _toggleModeAction.AddBinding("<Keyboard>/tab");
            _toggleModeAction.AddBinding("<Gamepad>/select");
            _toggleModeAction.AddBinding("<Gamepad>/start");

            _cursorMoveAction = _builderMap.AddAction("CursorMove", InputActionType.Value);
            _cursorMoveAction.AddBinding("<Pointer>/position");
            _cursorMoveAction.AddBinding("<Gamepad>/leftStick");
            _cursorMoveAction.AddBinding("<Touchscreen>/primaryTouch/position");

            _builderMap.Enable();
        }
    }
}
