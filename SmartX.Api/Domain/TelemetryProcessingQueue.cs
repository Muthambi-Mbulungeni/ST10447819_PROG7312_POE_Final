namespace SmartX.Api.Domain;

// ============================================================
// QUEUE<T> + PRIORITY QUEUE<T, int> REQUIREMENT — Part 2
// ============================================================
// Normal telemetry packets are handled with a Queue<T> which
// guarantees First-In, First-Out (FIFO) processing order.
//
// Critical packets (severe power spike, moisture drop etc.)
// are placed into a PriorityQueue<IncomingPacket, int> instead.
// A lower int priority value = higher urgency.
//   0 = CRITICAL  (processed before everything else)
//   1 = WARNING
//   2 = NORMAL
//
// When Dequeue() is called, critical packets always emerge
// before any standard queue items, ensuring the dashboard
// surface alerts without any latency.
// ============================================================
public sealed class TelemetryProcessingQueue
{
    // Standard FIFO queue for routine telemetry
    private readonly Queue<IncomingPacket>                _standardQueue = new();
    // Priority queue: lower int = higher urgency
    private readonly PriorityQueue<IncomingPacket, int>   _priorityQueue = new();

    private readonly object _gate        = new();
    private readonly int    _maxCapacity;

    public TelemetryProcessingQueue(int maxCapacity = 500)
    {
        if (maxCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCapacity));
        _maxCapacity = maxCapacity;
    }

    // ── Standard Queue operations ──────────────────────────────

    /// <summary>
    /// Enqueues a normal telemetry packet into the FIFO standard queue.
    /// Evicts the oldest entry when the queue is at capacity.
    /// </summary>
    public void Enqueue(IncomingPacket packet)
    {
        if (packet is null) return;
        lock (_gate)
        {
            if (_standardQueue.Count >= _maxCapacity)
                _standardQueue.Dequeue(); // Drop oldest to make room

            _standardQueue.Enqueue(packet);
        }
    }

    // ── Priority Queue operations ──────────────────────────────

    /// <summary>
    /// Routes a critical packet into the PriorityQueue, bypassing the
    /// standard queue entirely. Priority 0 = absolute critical (first out).
    /// </summary>
    public void EnqueueCritical(IncomingPacket packet, int priority = 0)
    {
        if (packet is null) return;
        lock (_gate)
            _priorityQueue.Enqueue(packet, priority);
    }

    // ── Unified dequeue ────────────────────────────────────────

    /// <summary>
    /// Dequeues the next packet to process. Critical packets (PriorityQueue)
    /// are always served before standard-queue packets.
    /// </summary>
    public IncomingPacket? Dequeue()
    {
        lock (_gate)
        {
            if (_priorityQueue.Count > 0)
                return _priorityQueue.Dequeue();

            return _standardQueue.Count > 0 ? _standardQueue.Dequeue() : null;
        }
    }

    // ── Snapshot helpers (for the dashboard endpoint) ──────────

    public IReadOnlyList<IncomingPacket> StandardSnapshot()
    {
        lock (_gate)
            return _standardQueue.ToList();
    }

    public IReadOnlyList<IncomingPacket> CriticalSnapshot()
    {
        lock (_gate)
        {
            // Materialise priority queue without destroying it
            var result = new List<IncomingPacket>();
            var temp   = new List<(IncomingPacket pkt, int pri)>();

            while (_priorityQueue.TryDequeue(out var item, out int pri))
            {
                result.Add(item);
                temp.Add((item, pri));
            }

            // Restore
            foreach (var (pkt, pri) in temp)
                _priorityQueue.Enqueue(pkt, pri);

            return result;
        }
    }

    public int StandardCount  { get { lock (_gate) return _standardQueue.Count; } }
    public int CriticalCount  { get { lock (_gate) return _priorityQueue.Count; } }
    public int TotalCount     => StandardCount + CriticalCount;
}
