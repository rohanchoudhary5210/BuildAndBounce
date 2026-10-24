using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BuildAndBounce.Net
{
    /// <summary>
    /// Visual and transform representation of a remote multiplayer racer in the live race (M6).
    /// Performs client-side snapshot interpolation with an interpolation buffer to eliminate network jitter.
    /// </summary>
    public class NetworkPlayerProxy : MonoBehaviour
    {
        [Header("Interpolation Settings")]
        [Tooltip("Buffer delay in seconds behind real-time to allow smooth interpolation.")]
        [SerializeField] private float _interpolationDelay = 0.1f;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private TextMeshPro _nameTag;

        private readonly List<NetworkPlayerSnapshot> _snapshotBuffer = new List<NetworkPlayerSnapshot>(64);
        private int _playerId;
        private string _playerName;
        private Color _playerColor;

        public int PlayerId => _playerId;
        public string PlayerName => _playerName;

        public void Initialize(int playerId, string playerName, Color playerColor)
        {
            _playerId = playerId;
            _playerName = playerName;
            _playerColor = playerColor;
            gameObject.name = $"RemotePlayer_{playerId}_{playerName}";

            EnsureVisuals();
        }

        public void AddSnapshot(NetworkPlayerSnapshot snapshot)
        {
            // Keep buffer ordered by timestamp
            _snapshotBuffer.Add(snapshot);
            if (_snapshotBuffer.Count > 32)
            {
                _snapshotBuffer.RemoveAt(0);
            }
        }

        private void Update()
        {
            if (_snapshotBuffer.Count == 0) return;

            // Target playback time is current time minus interpolation buffer
            float renderTime = Time.time - _interpolationDelay;

            // If we have at least 2 snapshots, find the bounding pair
            if (_snapshotBuffer.Count >= 2)
            {
                for (int i = 0; i < _snapshotBuffer.Count - 1; i++)
                {
                    var s0 = _snapshotBuffer[i];
                    var s1 = _snapshotBuffer[i + 1];

                    if (s0.timestamp <= renderTime && renderTime <= s1.timestamp)
                    {
                        float span = s1.timestamp - s0.timestamp;
                        float t = span > 0.0001f ? (renderTime - s0.timestamp) / span : 1f;

                        transform.position = Vector3.Lerp(s0.position, s1.position, t);
                        transform.rotation = Quaternion.Slerp(s0.rotation, s1.rotation, t);
                        return;
                    }
                }
            }

            // Fallback: extrapolate or use latest snapshot
            var latest = _snapshotBuffer[_snapshotBuffer.Count - 1];
            transform.position = Vector3.Lerp(transform.position, latest.position, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(transform.rotation, latest.rotation, Time.deltaTime * 15f);
        }

        private void EnsureVisuals()
        {
            if (_renderer == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "RemoteVisual";
                capsule.transform.SetParent(transform, false);

                if (capsule.TryGetComponent<Collider>(out var col)) Destroy(col);

                _renderer = capsule.GetComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader) { color = _playerColor };
                _renderer.sharedMaterial = mat;
            }

            if (_nameTag == null)
            {
                var tagObj = new GameObject("NameTag");
                tagObj.transform.SetParent(transform, false);
                tagObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);

                _nameTag = tagObj.AddComponent<TextMeshPro>();
                _nameTag.text = _playerName;
                _nameTag.fontSize = 3;
                _nameTag.alignment = TextAlignmentOptions.Center;
                _nameTag.color = Color.white;
            }
        }
    }
}
