using BuildAndBounce.Level;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// Plays back a previously recorded best run using a translucent ghost avatar.
    /// Interpolates smoothly between recorded frames in sync with the course timer.
    /// </summary>
    public class GhostPlayback : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("Ghost avatar GameObject. Procedurally generated if unassigned.")]
        [SerializeField] private GameObject _ghostAvatar;

        [Tooltip("Translucent tint color for the ghost mesh.")]
        [SerializeField] private Color _ghostColor = new Color(0.2f, 0.85f, 1.0f, 0.45f);

        private GhostRunData _ghostData;
        private bool _isPlaying;
        private float _playbackTime;
        private int _frameCursor;
        private Material _ghostMaterial;

        public bool IsPlaying => _isPlaying;
        public bool HasGhostData => _ghostData != null && _ghostData.FrameCount > 0;
        public GhostRunData ActiveGhost => _ghostData;
        public GameObject GhostAvatar => _ghostAvatar;

        private void Awake()
        {
            EnsureAvatarExists();
        }

        private void OnEnable()
        {
            CourseStart.OnCourseStarted += HandleCourseStarted;
            CourseFinish.OnCourseFinished += HandleCourseFinished;
            CourseStart.OnCourseReset += StopPlayback;
        }

        private void OnDisable()
        {
            CourseStart.OnCourseStarted -= HandleCourseStarted;
            CourseFinish.OnCourseFinished -= HandleCourseFinished;
            CourseStart.OnCourseReset -= StopPlayback;
        }

        public void LoadGhost(GhostRunData ghostData)
        {
            _ghostData = ghostData;
            _frameCursor = 0;
            _playbackTime = 0f;

            EnsureAvatarExists();

            if (_ghostData != null && _ghostData.FrameCount > 0)
            {
                var f0 = _ghostData.frames[0];
                _ghostAvatar.transform.position = f0.position;
                _ghostAvatar.transform.rotation = f0.rotation;
                _ghostAvatar.SetActive(false); // Inactive until race starts
            }
            else
            {
                if (_ghostAvatar != null) _ghostAvatar.SetActive(false);
            }
        }

        private void HandleCourseStarted()
        {
            if (!HasGhostData) return;

            _isPlaying = true;
            _playbackTime = 0f;
            _frameCursor = 0;

            if (_ghostAvatar != null)
            {
                var f0 = _ghostData.frames[0];
                _ghostAvatar.transform.position = f0.position;
                _ghostAvatar.transform.rotation = f0.rotation;
                _ghostAvatar.SetActive(true);
            }
        }

        private void Update()
        {
            if (!_isPlaying || !HasGhostData || _ghostAvatar == null) return;

            _playbackTime += Time.deltaTime;

            var frames = _ghostData.frames;
            int count = frames.Count;

            // If past final frame, park at finish
            if (_playbackTime >= _ghostData.finishTime || _frameCursor >= count - 1)
            {
                var final = frames[count - 1];
                _ghostAvatar.transform.position = final.position;
                _ghostAvatar.transform.rotation = final.rotation;
                _isPlaying = false;
                return;
            }

            // Advance cursor
            while (_frameCursor < count - 2 && frames[_frameCursor + 1].time <= _playbackTime)
            {
                _frameCursor++;
            }

            var frameA = frames[_frameCursor];
            var frameB = frames[_frameCursor + 1];

            float span = frameB.time - frameA.time;
            float t = span > 0.0001f ? Mathf.Clamp01((_playbackTime - frameA.time) / span) : 1f;

            _ghostAvatar.transform.position = Vector3.Lerp(frameA.position, frameB.position, t);
            _ghostAvatar.transform.rotation = Quaternion.Slerp(frameA.rotation, frameB.rotation, t);
        }

        private void HandleCourseFinished()
        {
            _isPlaying = false;
        }

        public void StopPlayback()
        {
            _isPlaying = false;
            if (_ghostAvatar != null) _ghostAvatar.SetActive(false);
        }

        private void EnsureAvatarExists()
        {
            if (_ghostAvatar != null) return;

            // Procedurally create a capsule primitive matching the player
            _ghostAvatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _ghostAvatar.name = "GhostAvatar";
            _ghostAvatar.transform.SetParent(transform, false);

            // Strip collider to ensure no physics interaction
            if (_ghostAvatar.TryGetComponent<Collider>(out var col))
            {
                Destroy(col);
            }

            // Configure transparent ghost material
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");

            _ghostMaterial = new Material(shader) { color = _ghostColor };

            BuildAndBounce.Core.TransparentMaterial.Apply(_ghostMaterial);

            if (_ghostAvatar.TryGetComponent<MeshRenderer>(out var mr))
            {
                mr.sharedMaterial = _ghostMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            _ghostAvatar.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
            {
                Destroy(_ghostMaterial);
            }
        }
    }
}
