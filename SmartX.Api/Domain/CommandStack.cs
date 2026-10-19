namespace SmartX.Api.Domain;

// ============================================================
// STACK<T> REQUIREMENT — Part 2
// ============================================================
// A Stack<CommandRecord> tracks every manual override command
// the operator issues. Because a stack is LIFO (Last-In,
// First-Out), popping gives the most-recently-issued command —
// which is exactly what "Undo Last Command" means.
//
// Use-case: operator fires "Shut Down All Valves" by mistake →
// they click Undo → Pop() restores the previous state.
// ============================================================
public sealed class CommandStack
{
    private readonly Stack<CommandRecord> _stack  = new();
    private readonly object               _gate   = new();
    private readonly int                  _maxSize;

    public int Count { get { lock (_gate) return _stack.Count; } }

    public CommandStack(int maxSize = 200)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive.");
        _maxSize = maxSize;
    }

    /// <summary>Pushes a new command onto the top of the history stack.</summary>
    public void Push(CommandRecord command)
    {
        if (command is null) return;

        lock (_gate)
        {
            // Keep the stack bounded. If we are at capacity, remove the oldest entry
            // (bottom of the stack) by rebuilding from a temporary array.
            if (_stack.Count >= _maxSize)
            {
                var arr = _stack.ToArray();
                _stack.Clear();
                // arr[0] is most-recent, arr[^1] is oldest → skip oldest
                foreach (var item in arr.Take(_maxSize - 1).Reverse())
                    _stack.Push(item);
            }

            _stack.Push(command);
        }
    }

    /// <summary>
    /// Pops the most-recently-issued command (Undo). Returns null when history is empty.
    /// </summary>
    public CommandRecord? Pop()
    {
        lock (_gate)
            return _stack.Count > 0 ? _stack.Pop() : null;
    }

    /// <summary>Peeks at the last command without removing it.</summary>
    public CommandRecord? Peek()
    {
        lock (_gate)
            return _stack.Count > 0 ? _stack.Peek() : null;
    }

    /// <summary>
    /// Returns a thread-safe snapshot of the entire command history (most-recent first).
    /// </summary>
    public IReadOnlyList<CommandRecord> Snapshot()
    {
        lock (_gate)
            return _stack.ToList(); // Stack.ToList() preserves top-first order
    }
}
