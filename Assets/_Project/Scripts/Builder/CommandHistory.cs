using System;
using System.Collections.Generic;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Manages Undo and Redo stacks for build mode commands.
    /// Emits state change events to update UI button interactability.
    /// </summary>
    public class CommandHistory
    {
        private readonly int _maxCapacity;
        private readonly LinkedList<IBuilderCommand> _undoStack = new LinkedList<IBuilderCommand>();
        private readonly Stack<IBuilderCommand> _redoStack = new Stack<IBuilderCommand>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event Action OnHistoryChanged;

        public CommandHistory(int maxCapacity = 50)
        {
            _maxCapacity = maxCapacity;
        }

        public void ExecuteCommand(IBuilderCommand command)
        {
            if (command == null) return;

            command.Execute();

            _undoStack.AddLast(command);
            if (_undoStack.Count > _maxCapacity)
            {
                _undoStack.RemoveFirst();
            }

            _redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }

        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Last.Value;
            _undoStack.RemoveLast();

            command.Undo();
            _redoStack.Push(command);

            OnHistoryChanged?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.AddLast(command);

            OnHistoryChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }
    }
}