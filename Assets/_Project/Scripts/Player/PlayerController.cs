using System;
using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Core 3D platformer motor. Drives character movement, rotation, and jump physics via
    /// a CharacterController on FixedUpdate for predictable, deterministic physics.
    /// Employs jump apex kinematics, coyote time, jump buffering, variable jump height,
    /// air-jump budgeting (double jump), and directional dashing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Tunable locomotion, jump, and ability data asset.")]
        [SerializeField] private PlayerControllerData _data;

        [Header("References (Optional)")]
        [Tooltip("Camera transform used for perspective-relative movement. Defaults to Camera.main if null.")]
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _characterController;
        private PlayerInputReader _inputReader;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private Vector3 _platformVelocity;

        private bool _isGrounded;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private int _remainingJumps;

        private bool _isDashing;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector3 _dashDirection;

        private float _rotationVelocity;

        public Vector3 Velocity => _characterController.velocity;
        public Vector3 HorizontalVelocity => _horizontalVelocity;
        public float VerticalVelocity => _verticalVelocity;
        public bool IsGrounded => _isGrounded;
        public bool IsDashing => _isDashing;

        public event Action OnJumped;
        public event Action OnLanded;
        public event Action OnDashed;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerInputReader>();

            if (!gameObject.CompareTag("Player"))
            {
                gameObject.tag = "Player";
            }

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (_data == null)
            {
                Debug.LogError($"[PlayerController] Missing PlayerControllerData on {gameObject.name}. Assign a data asset in the Inspector.", this);
                return;
            }

            _remainingJumps = _data.MaxJumps;
        }

        private void Update()
        {
            // Jump buffer is polled in Update to capture rapid taps between physics frames
            if (_inputReader.JumpTriggered)
            {
                _jumpBufferTimer = _data.JumpBufferTime;
                _inputReader.ConsumeJumpTrigger();
            }

            // Dash trigger polled similarly
            if (_inputReader.DashTriggered)
            {
                _inputReader.ConsumeDashTrigger();
                TryStartDash();
            }
        }

        private void FixedUpdate()
        {
            if (_data == null) return;

            UpdateGroundState();
            UpdateTimers();

            if (_isDashing)
            {
                ProcessDash();
            }
            else
            {
                ProcessHorizontalMovement();
                ProcessJump();
                ProcessGravity();
            }

            ApplyMovement();
        }

        private void UpdateGroundState()
        {
            bool wasGrounded = _isGrounded;

            // CharacterController.isGrounded can flutter on slopes; a small downward sphere cast ensures solid contact
            Vector3 basePoint = transform.position + _characterController.center - Vector3.up * (_characterController.height * 0.5f - _characterController.radius);
            bool sphereHit = Physics.SphereCast(
                basePoint,
                _characterController.radius * 0.9f,
                Vector3.down,
                out RaycastHit hit,
                _data.GroundCheckDistance,
                _data.GroundLayers,
                QueryTriggerInteraction.Ignore);

            _isGrounded = _characterController.isGrounded || (sphereHit && _verticalVelocity <= 0.1f);

            if (_isGrounded)
            {
                _coyoteTimer = _data.CoyoteTime;
                _remainingJumps = _data.MaxJumps;

                if (!wasGrounded)
                {
                    OnLanded?.Invoke();
                }

                // Snap downward to remain firmly planted when running down slopes or stairs
                if (_verticalVelocity < 0f && !_isDashing)
                {
                    _verticalVelocity = -_data.GroundingForce;
                }
            }
            else if (wasGrounded)
            {
                // Walking off a ledge without jumping consumes one jump from the budget
                _remainingJumps = Mathf.Min(_remainingJumps, _data.MaxJumps - 1);
            }
        }

        private void UpdateTimers()
        {
            float dt = Time.fixedDeltaTime;

            if (!_isGrounded && _coyoteTimer > 0f)
            {
                _coyoteTimer -= dt;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= dt;
            }

            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= dt;
            }
        }

        private void ProcessHorizontalMovement()
        {
            Vector3 moveDir = CalculateCameraRelativeDirection(_inputReader.MoveInput);
            Vector3 targetVelocity = moveDir * _data.MoveSpeed;

            float accel = moveDir.sqrMagnitude > 0.001f ? _data.Acceleration : _data.Deceleration;
            if (!_isGrounded)
            {
                accel *= _data.AirControlRate;
            }

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, accel * Time.fixedDeltaTime);

            // Rotate character to face movement direction smoothly
            if (moveDir.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationVelocity, _data.RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private Vector3 CalculateCameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f) return Vector3.zero;

            Vector3 camForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
            Vector3 camRight = _cameraTransform != null ? _cameraTransform.right : Vector3.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 direction = camForward * input.y + camRight * input.x;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction;
        }

        private void ProcessJump()
        {
            if (_jumpBufferTimer <= 0f) return;

            // Ground or coyote jump
            if (_coyoteTimer > 0f)
            {
                ExecuteJump();
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
            }
            // Air jump / double jump
            else if (_remainingJumps > 0)
            {
                ExecuteJump();
                _jumpBufferTimer = 0f;
            }
        }

        private void ExecuteJump()
        {
            _verticalVelocity = _data.InitialJumpVelocity;
            _remainingJumps--;
            _isGrounded = false;
            OnJumped?.Invoke();
        }

        private void ProcessGravity()
        {
            if (_isGrounded && _verticalVelocity <= 0f) return;

            float gravity;

            // Short hop: button released while still ascending
            if (_verticalVelocity > 0f && !_inputReader.IsJumpHeld)
            {
                gravity = _data.BaseGravity * _data.JumpCutGravityMultiplier;
            }
            // Falling downward: snappier gravity for crisp landing feel
            else if (_verticalVelocity < 0f)
            {
                gravity = _data.BaseGravity * _data.FallGravityMultiplier;
            }
            // Normal ascending jump arc
            else
            {
                gravity = _data.BaseGravity;
            }

            _verticalVelocity -= gravity * Time.fixedDeltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -_data.MaxFallSpeed);
        }

        private void TryStartDash()
        {
            if (_dashCooldownTimer > 0f || _isDashing || _data == null) return;

            Vector3 moveDir = CalculateCameraRelativeDirection(_inputReader.MoveInput);
            _dashDirection = moveDir.sqrMagnitude > 0.001f ? moveDir : transform.forward;

            _isDashing = true;
            _dashTimer = _data.DashDuration;
            _dashCooldownTimer = _data.DashCooldown;
            _verticalVelocity = 0f;

            OnDashed?.Invoke();
        }

        private void ProcessDash()
        {
            _dashTimer -= Time.fixedDeltaTime;
            _horizontalVelocity = _dashDirection * _data.DashSpeed;
            _verticalVelocity = 0f;

            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                // Preserve momentum capped to normal move speed
                _horizontalVelocity = Vector3.ClampMagnitude(_horizontalVelocity, _data.MoveSpeed);
            }
        }

        private void ApplyMovement()
        {
            Vector3 finalVelocity = _horizontalVelocity + Vector3.up * _verticalVelocity + _platformVelocity;
            _characterController.Move(finalVelocity * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Allows moving platforms to apply carrying velocity to the player controller without physics fighting.
        /// Preserves horizontal platform momentum when jumping off a moving platform.
        /// </summary>
        public void SetPlatformVelocity(Vector3 platformVelocity)
        {
            // If leaving a moving platform while airborne (e.g. jumped off), carry the platform's momentum into the jump
            if (platformVelocity == Vector3.zero && _platformVelocity != Vector3.zero && !_isGrounded)
            {
                Vector3 preservedHorizontal = new Vector3(_platformVelocity.x, 0f, _platformVelocity.z);
                _horizontalVelocity += preservedHorizontal;
            }

            _platformVelocity = platformVelocity;
        }

        /// <summary>
        /// Resets all internal velocities and active dash states (useful upon respawn or teleportation).
        /// </summary>
        public void ResetVelocity()
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _platformVelocity = Vector3.zero;
            _isDashing = false;
            _dashTimer = 0f;
        }
    }
}
