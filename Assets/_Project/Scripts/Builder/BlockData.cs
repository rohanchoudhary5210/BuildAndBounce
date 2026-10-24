using System;
using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Metadata definition for a placeable course piece in the Level Builder.
    /// Used by BlockCatalog to populate the build mode palette.
    /// </summary>
    [Serializable]
    public class BlockData
    {
        [Tooltip("Unique serialization ID (e.g. 'platform_cube', 'start_pad', 'checkpoint').")]
        [SerializeField] private string _id;

        [Tooltip("Display name shown in the builder UI.")]
        [SerializeField] private string _displayName;

        [Tooltip("Category for filtering in the palette.")]
        [SerializeField] private BlockCategory _category;

        [Tooltip("Prefab instantiated into the course.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Bounding box dimensions for grid alignment.")]
        [SerializeField] private Vector3 _dimensions = Vector3.one;

        [Tooltip("Placement theme color used for UI and previews.")]
        [SerializeField] private Color _themeColor = Color.white;

        [Tooltip("Palette icon (optional).")]
        [SerializeField] private Sprite _icon;

        public string Id => _id;
        public string DisplayName => _displayName;
        public BlockCategory Category => _category;
        public GameObject Prefab => _prefab;
        public Vector3 Dimensions => _dimensions;
        public Color ThemeColor => _themeColor;
        public Sprite Icon => _icon;

        public BlockData(string id, string displayName, BlockCategory category, GameObject prefab, Vector3 dimensions, Color themeColor)
        {
            _id = id;
            _displayName = displayName;
            _category = category;
            _prefab = prefab;
            _dimensions = dimensions;
            _themeColor = themeColor;
        }
    }
}
