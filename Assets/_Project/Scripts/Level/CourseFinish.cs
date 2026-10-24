using System;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Course finish pad. Emits an event upon player arrival to halt the race timer
    /// and finalize run statistics.
    /// </summary>
    public class CourseFinish : MonoBehaviour
    {
        public static event Action OnCourseFinished;

        private bool _isCompleted;

        public bool IsCompleted => _isCompleted;

        private void OnTriggerEnter(Collider other)
        {
            if (_isCompleted) return;

            if (other.TryGetComponent<PlayerController>(out _))
            {
                _isCompleted = true;
                OnCourseFinished?.Invoke();
            }
        }

        public void ResetFinish()
        {
            _isCompleted = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(2.5f, 0.2f, 2.5f));
        }
    }
}
