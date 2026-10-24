using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Command that instantiates and places a block into the course.
    /// Undo destroys the placed block.
    /// </summary>
    public class PlaceBlockCommand : IBuilderCommand
    {
        private readonly LevelBuilder _builder;
        private readonly BlockData _blockData;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;

        private GameObject _placedInstance;

        public GameObject PlacedInstance => _placedInstance;

        public PlaceBlockCommand(LevelBuilder builder, BlockData blockData, Vector3 position, Quaternion rotation)
        {
            _builder = builder;
            _blockData = blockData;
            _position = position;
            _rotation = rotation;
        }

        public void Execute()
        {
            if (_placedInstance != null)
            {
                _placedInstance.SetActive(true);
                _builder.RegisterPlacedBlock(_placedInstance, _blockData);
                return;
            }

            _placedInstance = _builder.InstantiateBlockDirect(_blockData, _position, _rotation);
        }

        public void Undo()
        {
            if (_placedInstance != null)
            {
                _builder.UnregisterPlacedBlock(_placedInstance);
                _placedInstance.SetActive(false);
            }
        }
    }
}