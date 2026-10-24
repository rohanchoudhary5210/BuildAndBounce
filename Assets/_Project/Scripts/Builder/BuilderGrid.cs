using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Snaps positions to an integer or fractional 3D world grid.
    /// Handles surface-normal offsets so placed pieces stack cleanly without overlapping.
    /// </summary>
    public class BuilderGrid : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Size of each grid cell in world units.")]
        [SerializeField] private float _cellSize = 1.0f;

        [Tooltip("Base height of the build plane.")]
        [SerializeField] private float _baseFloorY = 0f;

        public float CellSize => _cellSize;
        public float BaseFloorY => _baseFloorY;

        /// <summary>
        /// Snaps a raw position to the nearest grid coordinates.
        /// </summary>
        public Vector3 Snap(Vector3 rawPos)
        {
            float x = Mathf.Round(rawPos.x / _cellSize) * _cellSize;
            float y = Mathf.Round(rawPos.y / _cellSize) * _cellSize;
            float z = Mathf.Round(rawPos.z / _cellSize) * _cellSize;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Computes the snapped placement position against a raycast hit surface.
        /// Offsets by half block dimension along the hit normal to prevent interpenetration.
        /// </summary>
        public Vector3 GetPlacementPosition(RaycastHit hit, Vector3 blockDimensions)
        {
            Vector3 normal = hit.normal;

            // Determine dominant axis of normal
            Vector3 snappedNormal = Vector3.zero;
            if (Mathf.Abs(normal.y) > 0.6f)
            {
                snappedNormal = normal.y > 0 ? Vector3.up : Vector3.down;
            }
            else if (Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                snappedNormal = normal.x > 0 ? Vector3.right : Vector3.left;
            }
            else
            {
                snappedNormal = normal.z > 0 ? Vector3.forward : Vector3.back;
            }

            // Target center point offset along surface normal
            Vector3 halfExtents = blockDimensions * 0.5f;
            float offsetAlongNormal = Vector3.Dot(halfExtents, snappedNormal);
            Vector3 targetCenter = hit.point + snappedNormal * (offsetAlongNormal + 0.01f);

            return Snap(targetCenter);
        }

        /// <summary>
        /// Snaps a flat plane raycast (e.g. infinite floor) to grid.
        /// </summary>
        public Vector3 GetFloorPlacementPosition(Ray ray, Vector3 blockDimensions)
        {
            Plane floor = new Plane(Vector3.up, new Vector3(0f, _baseFloorY, 0f));
            if (floor.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                hitPoint.y += blockDimensions.y * 0.5f;
                return Snap(hitPoint);
            }

            return Snap(ray.origin + ray.direction * 10f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.2f);
            Vector3 center = new Vector3(transform.position.x, _baseFloorY, transform.position.z);
            Gizmos.DrawWireCube(center, new Vector3(40f, 0.05f, 40f));
        }
    }
}