using System;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Course starting platform and trigger. Emits an event when the player departs the starting
    /// pad to begin the run timer and ghost recording.
    /// </summary>
    public class CourseStart : MonoBehaviour
    {
        [Tooltip("Exact spawn transform for starting the course. Defaults to this transform if null.")]
        [SerializeField] private Transform _spawnPoint;

        public static event Action OnCourseStarted;
        public static event Action OnCourseReset;

        public static void TriggerCourseReset()
        {
            OnCourseReset?.Invoke();
        }

        private void OnEnable()
        {
            OnCourseReset += ResetStart;
        }

        private void OnDisable()
        {
            OnCourseReset -= ResetStart;
        }

        public Vector3 SpawnPosition => _spawnPoint != null 
            ? _spawnPoint.position 
            : transform.position + Vector3.up * (transform.lossyScale.y * 0.5f + 0.1f); // feet just above the pad top

        public Quaternion SpawnRotation => _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

        private bool _hasStarted;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PlayerController>(out _))
            {
                // Reset start pad state whenever player steps back onto it
                _hasStarted = false;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_hasStarted) return;

            if (other.TryGetComponent<PlayerController>(out _))
            {
                _hasStarted = true;
                OnCourseStarted?.Invoke();
            }
        }

        public void ResetStart()
        {
            _hasStarted = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Vector3 pos = SpawnPosition;
            Gizmos.DrawWireCube(pos, new Vector3(2f, 0.1f, 2f));
            Gizmos.DrawRay(pos, SpawnRotation * Vector3.forward * 2f);
        }
    }
}
