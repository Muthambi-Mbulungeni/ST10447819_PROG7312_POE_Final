namespace SmartX.Api.Domain;

// Bounded circular buffer backed by a linked list.
// When the buffer is full the oldest packet is evicted before the new one is added.
// This matches the IoT reality that recent readings matter more than stale history.
// Thread safety is explicit. A lock wraps every mutation and every read that could race.
public sealed class IngestionQueue<T> where T : struct
{
    private readonly LinkedList<TelemetryPacket<T>> _store;
    private readonly int    _capacity;
    private readonly object _gate = new();

    public int Capacity => _capacity;

    public IngestionQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be a positive integer.");

        _capacity = capacity;
        _store    = new LinkedList<TelemetryPacket<T>>();
    }

    public void Enqueue(TelemetryPacket<T> packet)
    {
        // Null packet means the caller has nothing to give us. Silently discard.
        if (packet is null) return;

        lock (_gate)
        {
            if (_store.Count >= _capacity)
                _store.RemoveFirst();

            _store.AddLast(packet);
        }
    }

    // Snapshot returns a stable copy of the current state.
    // The caller never races against writes that happen after this call returns.
    public IReadOnlyList<TelemetryPacket<T>> Snapshot()
    {
        lock (_gate)
            return _store.ToList();
    }

    public int Count
    {
        get { lock (_gate) return _store.Count; }
    }

    public void Clear()
    {
        lock (_gate)
            _store.Clear();
    }
}
