using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Snaps block placements to the X/Z build grid and stacks them flush on the surface under the cursor,
    /// so placed pieces line up edge to edge without overlapping, sinking or floating.
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
        /// Snaps a block centre on X/Z so the block's edges (not its centre) sit on grid lines.
        /// Centre-snapping put odd-sized blocks (1x1, 3x3) half a cell off from even ones (4x4 start pad).
        /// </summary>
        public Vector3 SnapXZ(Vector3 center, Vector3 blockDimensions)
        {
            return new Vector3(
                SnapEdge(center.x, blockDimensions.x * 0.5f),
                RoundHeight(center.y),
                SnapEdge(center.z, blockDimensions.z * 0.5f));
        }

        private float SnapEdge(float center, float halfSize)
        {
            return Mathf.Round((center - halfSize) / _cellSize) * _cellSize + halfSize;
        }

        // Heights are not grid-snapped: blocks are 0.3 / 0.5 / 1 tall, so rounding Y to whole cells
        // sank thin pads into the floor and left 1x1 cubes floating. Only strip float noise.
        private static float RoundHeight(float y) => Mathf.Round(y * 100f) / 100f;

        /// <summary>
        /// Placement centre for a block against the surface the cursor ray hit.
        /// Top/bottom faces: the block rests exactly on (or hangs under) that surface.
        /// Side faces: the block sits flush against the face, level with the block that was clicked.
        /// </summary>
        public Vector3 GetPlacementPosition(RaycastHit hit, Vector3 blockDimensions)
        {
            Vector3 normal = hit.normal;
            Vector3 half = blockDimensions * 0.5f;
            Vector3 center;

            if (Mathf.Abs(normal.y) > 0.6f)
            {
                float y = normal.y > 0f ? hit.point.y + half.y : hit.point.y - half.y;
                center = new Vector3(hit.point.x, y, hit.point.z);
            }
            else
            {
                Vector3 side = Mathf.Abs(normal.x) > Mathf.Abs(normal.z)
                    ? new Vector3(Mathf.Sign(normal.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(normal.z));

                center = hit.point + Vector3.Scale(side, half);
                center.y = hit.collider.bounds.min.y + half.y;
            }

            return SnapXZ(center, blockDimensions);
        }

        /// <summary>
        /// Placement centre on the infinite build floor when the ray hits nothing.
        /// </summary>
        public Vector3 GetFloorPlacementPosition(Ray ray, Vector3 blockDimensions)
        {
            Plane floor = new Plane(Vector3.up, new Vector3(0f, _baseFloorY, 0f));
            Vector3 point = floor.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.origin + ray.direction * 10f;
            point.y = _baseFloorY + blockDimensions.y * 0.5f;
            return SnapXZ(point, blockDimensions);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.2f);
            Vector3 center = new Vector3(transform.position.x, _baseFloorY, transform.position.z);
            Gizmos.DrawWireCube(center, new Vector3(40f, 0.05f, 40f));
        }
    }
}