using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Command that removes a block from the course.
    /// Undo reactivates the block and restores it to the level builder.
    /// </summary>
    public class DeleteBlockCommand : IBuilderCommand
    {
        private readonly LevelBuilder _builder;
        private readonly GameObject _targetBlock;
        private readonly BlockData _blockData;

        public DeleteBlockCommand(LevelBuilder builder, GameObject targetBlock, BlockData blockData)
        {
            _builder = builder;
            _targetBlock = targetBlock;
            _blockData = blockData;
        }

        public void Execute()
        {
            if (_targetBlock != null)
            {
                _builder.UnregisterPlacedBlock(_targetBlock);
                _targetBlock.SetActive(false);
            }
        }

        public void Undo()
        {
            if (_targetBlock != null)
            {
                _targetBlock.SetActive(true);
                _builder.RegisterPlacedBlock(_targetBlock, _blockData);
            }
        }
    }
}