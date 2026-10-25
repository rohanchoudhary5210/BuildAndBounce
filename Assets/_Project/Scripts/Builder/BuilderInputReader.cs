using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public bool PlaceTriggered => Time.timeScale > 0f && (
            (_placeAction != null && _placeAction.WasPressedThisFrame()) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        );

        public bool DeleteTriggered => Time.timeScale > 0f && (
            (_deleteAction != null && _deleteAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && (Keyboard.current.xKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
        );

        public bool RotateTriggered => Time.timeScale > 0f && (
            (_rotateAction != null && _rotateAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current != null && (Gamepad.current.buttonNorth.wasPressedThisFrame || Gamepad.current.rightShoulder.wasPressedThisFrame))
        );

        public bool UndoTriggered => Time.timeScale > 0f && (
            (_undoAction != null && _undoAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
        );

        public bool RedoTriggered => Time.timeScale > 0f && (
            (_redoAction != null && _redoAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame)
        );

        public bool NextBlockTriggered => Time.timeScale > 0f && (
            (_nextBlockAction != null && _nextBlockAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame)
        );

        public bool PrevBlockTriggered => Time.timeScale > 0f && (
            (_prevBlockAction != null && _prevBlockAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame)
        );

        public bool ToggleModeTriggered => Time.timeScale > 0f && (
            (_toggleModeAction != null && _toggleModeAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame) // Start is reserved for the pause menu
        );
        public bool IsUsingGamepad => _isUsingGamepad;

        /// <summary>
        /// True while a UI text field (e.g. the share-code box) has keyboard focus.
        /// </summary>
        public static bool IsTypingInTextField
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;
            }
        }

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
            // If Gamepad left stick is actively moving, update virtual cursor
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.05f)
            {
                _isUsingGamepad = true;
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                _virtualGamepadCursor += stick * _gamepadCursorSpeed * Time.unscaledDeltaTime;
                _virtualGamepadCursor.x = Mathf.Clamp(_virtualGamepadCursor.x, 0f, Screen.width);
                _virtualGamepadCursor.y = Mathf.Clamp(_virtualGamepadCursor.y, 0f, Screen.height);
                _cursorPosition = _virtualGamepadCursor;
            }
            else if (Mouse.current != null)
            {
                // Direct hardware mouse position
                _isUsingGamepad = false;
                _cursorPosition = Mouse.current.position.ReadValue();
                _virtualGamepadCursor = _cursorPosition;
            }
            else if (_cursorMoveAction != null)
            {
                // Fallback through Input System action
                Vector2 readPos = _cursorMoveAction.ReadValue<Vector2>();
                if (readPos.sqrMagnitude > 0.1f)
                {
                    _cursorPosition = readPos;
                    _virtualGamepadCursor = readPos;
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
