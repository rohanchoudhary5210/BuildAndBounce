using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BuildAndBounce.UI
{
    /// <summary>
    /// Handles gamepad UI navigation and auto-selection across modal panels (M7).
    /// Ensures seamless console and gamepad accessibility without requiring a mouse.
    /// </summary>
    public class GamepadUINavigation : MonoBehaviour
    {
        [Header("Default Selectable")]
        [Tooltip("Selectable component to auto-focus when this UI opens with a gamepad.")]
        [SerializeField] private Selectable _firstSelected;

        private void OnEnable()
        {
            AutoFocusFirstSelectable();
        }

        private void Update()
        {
            // If gamepad input is detected and no UI element is currently selected, select first
            if (Gamepad.current != null && EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject == null && _firstSelected != null)
                {
                    if (Gamepad.current.dpad.up.wasPressedThisFrame ||
                        Gamepad.current.dpad.down.wasPressedThisFrame ||
                        Gamepad.current.dpad.left.wasPressedThisFrame ||
                        Gamepad.current.dpad.right.wasPressedThisFrame ||
                        Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.25f)
                    {
                        AutoFocusFirstSelectable();
                    }
                }
            }
        }

        public void AutoFocusFirstSelectable()
        {
            if (EventSystem.current == null) return;

            if (_firstSelected == null)
            {
                _firstSelected = GetComponentInChildren<Selectable>();
            }

            if (_firstSelected != null && _firstSelected.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(_firstSelected.gameObject);
            }
        }
    }
}
