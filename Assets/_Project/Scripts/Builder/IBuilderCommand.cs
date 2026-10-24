namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Command pattern interface for build-mode operations.
    /// Encapsulates an action and its exact inverse for robust Undo/Redo history.
    /// </summary>
    public interface IBuilderCommand
    {
        /// <summary>Executes or re-executes the command.</summary>
        void Execute();

        /// <summary>Reverses the command to restore the previous state.</summary>
        void Undo();
    }
}