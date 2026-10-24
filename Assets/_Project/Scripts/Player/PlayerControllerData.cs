using UnityEngine;

namespace BuildAndBounce.Player
{
    /// <summary>
    /// Tunable configuration asset for player locomotion, jump physics, and feel windows.
    /// Uses apex kinematics (jump height + time-to-apex) rather than manual gravity/force values
    /// so level designers can precisely tune jump arcs to match platform heights and gaps.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerControllerData", menuName = "Build & Bounce/Player/Controller Data")]
    public class PlayerControllerData : ScriptableObject
    {
        [Header("Ground Movement")]
        [Tooltip("Maximum horizontal ground speed in meters per second.")]
        [SerializeField] private float _moveSpeed = 8.5f;

        [Tooltip("Horizontal acceleration rate towards target speed.")]
        [SerializeField] private float _acceleration = 45f;

        [Tooltip("Horizontal deceleration rate when coming to a halt.")]
        [SerializeField] private float _deceleration = 35f;

        [Tooltip("Air control multiplier applied to acceleration/deceleration while airborne (0 = no steering, 1 = full control).")]
        [Range(0f, 1f)]
        [SerializeField] private float _airControlRate = 0.65f;

        [Tooltip("Time in seconds to smoothly turn character facing direction towards movement vector.")]
        [SerializeField] private float _rotationSmoothTime = 0.08f;

        [Header("Jump Physics (Height & Time-to-Apex)")]
        [Tooltip("Peak vertical height reached at the apex of a full jump.")]
        [SerializeField] private float _jumpHeight = 3.2f;

        [Tooltip("Time in seconds to travel from ground to apex.")]
        [SerializeField] private float _timeToApex = 0.36f;

        [Tooltip("Gravity multiplier when descending (prevents floaty falls and makes landing snappy).")]
        [SerializeField] private float _fallGravityMultiplier = 1.6f;

        [Tooltip("Gravity multiplier applied when jump button is released early before apex (allows short hops).")]
        [SerializeField] private float _jumpCutGravityMultiplier = 2.4f;

        [Tooltip("Terminal downward fall velocity in meters per second.")]
        [SerializeField] private float _maxFallSpeed = 25f;

        [Header("Feel Windows")]
        [Tooltip("Coyote time: grace duration in seconds after stepping off a ledge during which jumping is still allowed.")]
        [SerializeField] private float _coyoteTime = 0.15f;

        [Tooltip("Jump buffer: grace window before touching ground where a jump input is queued and executed on contact.")]
        [SerializeField] private float _jumpBufferTime = 0.15f;

        [Tooltip("Maximum consecutive jumps permitted before landing (2 allows a standard double jump).")]
        [SerializeField] private int _maxJumps = 2;

        [Header("Dash")]
        [Tooltip("Burst velocity speed during a dash.")]
        [SerializeField] private float _dashSpeed = 22f;

        [Tooltip("Duration of dash state in seconds.")]
        [SerializeField] private float _dashDuration = 0.18f;

        [Tooltip("Cooldown period in seconds before dash can be executed again.")]
        [SerializeField] private float _dashCooldown = 0.75f;

        [Header("Grounding")]
        [Tooltip("Downward probe distance beyond character base to test for solid footing.")]
        [SerializeField] private float _groundCheckDistance = 0.2f;

        [Tooltip("Downforce velocity applied while grounded to keep player snapped to slopes and stairs.")]
        [SerializeField] private float _groundingForce = 3f;

        [Tooltip("Collision layers recognized as ground.")]
        [SerializeField] private LayerMask _groundLayers = ~0;

        // Kinematic derivations from h = 0.5 * g * t^2 and v0 = g * t
        public float BaseGravity => (2f * _jumpHeight) / (_timeToApex * _timeToApex);
        public float InitialJumpVelocity => (2f * _jumpHeight) / _timeToApex;

        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float AirControlRate => _airControlRate;
        public float RotationSmoothTime => _rotationSmoothTime;

        public float FallGravityMultiplier => _fallGravityMultiplier;
        public float JumpCutGravityMultiplier => _jumpCutGravityMultiplier;
        public float MaxFallSpeed => _maxFallSpeed;

        public float CoyoteTime => _coyoteTime;
        public float JumpBufferTime => _jumpBufferTime;
        public int MaxJumps => _maxJumps;

        public float DashSpeed => _dashSpeed;
        public float DashDuration => _dashDuration;
        public float DashCooldown => _dashCooldown;

        public float GroundCheckDistance => _groundCheckDistance;
        public float GroundingForce => _groundingForce;
        public LayerMask GroundLayers => _groundLayers;
    }
}
