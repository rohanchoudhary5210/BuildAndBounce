using System.Collections.Generic;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Kinematic moving platform that moves via Rigidbody.MovePosition during FixedUpdate.
    /// To ensure jitter-free riding with Unity's CharacterController, the platform calculates
    /// its instantaneous delta velocity each physics tick and feeds it directly into
    /// PlayerController.SetPlatformVelocity, preventing collision fighting and frame desync.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Waypoints")]
        [Tooltip("World or local waypoints the platform moves between. If empty, uses Start Position and Target Offset.")]
        [SerializeField] private Vector3[] _localWaypoints;

        [Tooltip("Speed in meters per second.")]
        [SerializeField] private float _speed = 3.5f;

        [Tooltip("Dwell/pause duration at each waypoint in seconds.")]
        [SerializeField] private float _waitDuration = 1.0f;

        [Tooltip("If true, loops back to the first waypoint. If false, ping-pongs back and forth.")]
        [SerializeField] private bool _loop = false;

        private Rigidbody _rigidbody;
        private Vector3[] _worldWaypoints;
        private int _currentWaypointIndex = 1;
        private int _direction = 1;
        private float _waitTimer;
        private Vector3 _platformVelocity;

        private readonly HashSet<PlayerController> _riders = new HashSet<PlayerController>();

        public Vector3 PlatformVelocity => _platformVelocity;

        /// <summary>
        /// Where the platform was placed (its first waypoint), regardless of where it has moved to.
        /// </summary>
        public Vector3 HomePosition => _worldWaypoints != null && _worldWaypoints.Length > 0 ? _worldWaypoints[0] : transform.position;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void OnEnable()
        {
            PlayerRespawn.OnPlayerTeleported += HandlePlayerTeleported;
        }

        private void Start()
        {
            InitializeWaypoints();
        }

        private void InitializeWaypoints()
        {
            if (_localWaypoints == null || _localWaypoints.Length == 0)
            {
                // Default ping-pong travel of 8 meters forward if no custom waypoints provided
                _worldWaypoints = new Vector3[]
                {
                    transform.position,
                    transform.position + transform.forward * 8f
                };
            }
            else
            {
                _worldWaypoints = new Vector3[_localWaypoints.Length];
                for (int i = 0; i < _localWaypoints.Length; i++)
                {
                    _worldWaypoints[i] = transform.position + _localWaypoints[i];
                }
            }

            _currentWaypointIndex = _worldWaypoints.Length > 1 ? 1 : 0;
        }

        private void FixedUpdate()
        {
            if (_worldWaypoints == null || _worldWaypoints.Length < 2) return;

            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.fixedDeltaTime;
                _platformVelocity = Vector3.zero;
                BroadcastVelocity();
                return;
            }

            Vector3 targetPos = _worldWaypoints[_currentWaypointIndex];
            Vector3 currentPos = _rigidbody.position;
            float step = _speed * Time.fixedDeltaTime;

            Vector3 newPos = Vector3.MoveTowards(currentPos, targetPos, step);
            _platformVelocity = (newPos - currentPos) / Time.fixedDeltaTime;
            _rigidbody.MovePosition(newPos);

            BroadcastVelocity();

            if (Vector3.Distance(newPos, targetPos) < 0.005f)
            {
                _waitTimer = _waitDuration;
                AdvanceWaypoint();
            }
        }

        /// <summary>
        /// Snaps the platform back to its first waypoint (the level builder calls this when returning to
        /// Build mode, so courses are edited and saved with platforms at their placed position).
        /// </summary>
        public void ResetToStart()
        {
            if (_worldWaypoints == null || _worldWaypoints.Length == 0) return;

            _rigidbody.position = _worldWaypoints[0];
            transform.position = _worldWaypoints[0];
            _currentWaypointIndex = _worldWaypoints.Length > 1 ? 1 : 0;
            _direction = 1;
            _waitTimer = 0f;
            _platformVelocity = Vector3.zero;
        }

        private void AdvanceWaypoint()
        {
            if (_loop)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _worldWaypoints.Length;
            }
            else
            {
                if (_currentWaypointIndex >= _worldWaypoints.Length - 1)
                {
                    _direction = -1;
                }
                else if (_currentWaypointIndex <= 0)
                {
                    _direction = 1;
                }

                _currentWaypointIndex += _direction;
            }
        }

        private void BroadcastVelocity()
        {
            foreach (var rider in _riders)
            {
                if (rider != null)
                {
                    rider.SetPlatformVelocity(_platformVelocity);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PlayerController>(out var player))
            {
                _riders.Add(player);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<PlayerController>(out var player))
            {
                if (_riders.Remove(player))
                {
                    player.SetPlatformVelocity(Vector3.zero);
                }
            }
        }

        private void OnDisable()
        {
            PlayerRespawn.OnPlayerTeleported -= HandlePlayerTeleported;

            foreach (var rider in _riders)
            {
                if (rider != null)
                {
                    rider.SetPlatformVelocity(Vector3.zero);
                }
            }
            _riders.Clear();
        }

        private void HandlePlayerTeleported(PlayerController player)
        {
            if (player != null && _riders.Remove(player))
            {
                player.SetPlatformVelocity(Vector3.zero);
                if (player.transform.parent == transform)
                {
                    player.transform.SetParent(null);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_worldWaypoints == null || _worldWaypoints.Length < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _worldWaypoints.Length - 1; i++)
            {
                Gizmos.DrawLine(_worldWaypoints[i], _worldWaypoints[i + 1]);
                Gizmos.DrawSphere(_worldWaypoints[i], 0.25f);
            }
            Gizmos.DrawSphere(_worldWaypoints[_worldWaypoints.Length - 1], 0.25f);
        }
    }
}
