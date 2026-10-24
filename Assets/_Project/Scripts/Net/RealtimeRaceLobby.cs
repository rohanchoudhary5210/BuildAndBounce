using System;
using System.Collections.Generic;
using BuildAndBounce.Level;
using BuildAndBounce.Player;
using BuildAndBounce.Race;
using UnityEngine;

namespace BuildAndBounce.Net
{
    public enum RaceState
    {
        Lobby,
        Countdown,
        Racing,
        Finished
    }

    /// <summary>
    /// Coordinates offline bot race (no networking yet) and experimental race proxies.
    /// Handles spawn positioning, snapshot broadcasting, and optional offline simulated racer bots.
    /// </summary>
    public class RealtimeRaceLobby : MonoBehaviour
    {
        [Header("Race Configuration")]
        [Tooltip("Maximum allowed players in a race session.")]
        [SerializeField] private int _maxPlayers = 4;

        [Tooltip("Offline bot race (no networking yet). Simulates 3 offline racer bots locally.")]
        [SerializeField] private bool _simulateRemoteRacers = false;

        [Tooltip("Distinct colors assigned to players 1 through 4.")]
        [SerializeField] private Color[] _playerColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1.0f),  // P1: Blue (Local)
            new Color(1.0f, 0.4f, 0.2f),  // P2: Orange
            new Color(0.3f, 0.85f, 0.4f), // P3: Green
            new Color(0.9f, 0.3f, 0.8f)   // P4: Purple
        };

        [Header("Scene References")]
        [SerializeField] private PlayerController _localPlayer;
        [SerializeField] private CourseStart _startPad;

        private RaceState _state = RaceState.Lobby;
        private readonly List<NetworkPlayerProxy> _proxies = new List<NetworkPlayerProxy>();
        private float _snapshotTimer;
        private float _raceStartTime;

        public RaceState State => _state;
        public IReadOnlyList<NetworkPlayerProxy> Proxies => _proxies;

        public static event Action<RaceState> OnRaceStateChanged;

        private void Awake()
        {
            if (_localPlayer == null)
            {
                Debug.LogError("[RealtimeRaceLobby] PlayerController reference is missing! Wire it in Inspector.", this);
            }
            if (_startPad == null)
            {
                Debug.LogError("[RealtimeRaceLobby] CourseStart reference is missing! Wire it in Inspector.", this);
            }
        }

        private void Start()
        {
            if (_simulateRemoteRacers)
            {
                SpawnSimulatedRacers();
            }
        }

        private void OnEnable()
        {
            CourseStart.OnCourseStarted += HandleCourseStarted;
            CourseFinish.OnCourseFinished += HandleCourseFinished;
        }

        private void OnDisable()
        {
            CourseStart.OnCourseStarted -= HandleCourseStarted;
            CourseFinish.OnCourseFinished -= HandleCourseFinished;
        }

        private void Update()
        {
            if (_state != RaceState.Racing) return;

            // Broadcast local player transform snapshots at 20 Hz
            _snapshotTimer += Time.deltaTime;
            if (_snapshotTimer >= 0.05f)
            {
                _snapshotTimer = 0f;
                BroadcastLocalSnapshot();
            }

            // If simulated racers are active, simulate their physics/path
            if (_simulateRemoteRacers)
            {
                SimulateBotMovement();
            }
        }

        private void HandleCourseStarted()
        {
            _state = RaceState.Racing;
            _raceStartTime = Time.time;
            OnRaceStateChanged?.Invoke(_state);
        }

        private void HandleCourseFinished()
        {
            _state = RaceState.Finished;
            OnRaceStateChanged?.Invoke(_state);
        }

        private void BroadcastLocalSnapshot()
        {
            if (_localPlayer == null) return;

            var snapshot = new NetworkPlayerSnapshot(
                1,
                Time.time,
                _localPlayer.transform.position,
                _localPlayer.Velocity,
                _localPlayer.transform.rotation
            );

            // In peer-to-peer / relay networking, this payload is sent over the socket.
        }

        public void SpawnSimulatedRacers()
        {
            ClearProxies();

            Vector3 baseOrigin = _startPad != null ? _startPad.SpawnPosition : Vector3.zero;
            Vector3[] offsets = new Vector3[]
            {
                new Vector3(-1.2f, 0f, 0f),
                new Vector3(1.2f, 0f, 0f),
                new Vector3(2.4f, 0f, 0f)
            };

            string[] names = new string[] { "SpeedyBob", "AcrobatCat", "RocketRex" };
            int botCount = Mathf.Clamp(_maxPlayers - 1, 1, 3);

            for (int i = 0; i < botCount; i++)
            {
                var proxyObj = new GameObject($"Proxy_P{i + 2}");
                proxyObj.transform.SetParent(transform, false);
                proxyObj.transform.position = baseOrigin + offsets[i];

                var proxy = proxyObj.AddComponent<NetworkPlayerProxy>();
                proxy.Initialize(i + 2, names[i], _playerColors[i + 1]);
                _proxies.Add(proxy);
            }

            Debug.Log($"[RealtimeRaceLobby] Spawned {_proxies.Count} remote multiplayer racer proxies.");
        }

        private void SimulateBotMovement()
        {
            float elapsed = Time.time - _raceStartTime;
            for (int i = 0; i < _proxies.Count; i++)
            {
                var proxy = _proxies[i];
                if (proxy == null) continue;

                // Move forward along Z with individual speeds and slight sinusoidal weaving
                float speed = 8.5f + i * 0.7f;
                Vector3 currentPos = proxy.transform.position;
                currentPos.z += speed * Time.deltaTime;
                currentPos.x += Mathf.Sin(elapsed * 2.5f + i) * 0.4f * Time.deltaTime;

                var snapshot = new NetworkPlayerSnapshot(
                    proxy.PlayerId,
                    Time.time,
                    currentPos,
                    new Vector3(0f, 0f, speed),
                    Quaternion.LookRotation(Vector3.forward)
                );

                proxy.AddSnapshot(snapshot);
            }
        }

        public void ClearProxies()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                if (_proxies[i] != null) Destroy(_proxies[i].gameObject);
            }
            _proxies.Clear();
        }
    }
}
