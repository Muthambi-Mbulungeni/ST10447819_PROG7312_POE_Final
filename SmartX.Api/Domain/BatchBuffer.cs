namespace SmartX.Api.Domain;

// Jagged array holding raw float batches from sensor nodes before they move into
// the typed collection. Each row is one sensor's batch and rows are variable length
// because nodes report at different rates. This is the ingestion staging area.
// JAGGED ARRAY REQUIREMENT: Demonstrating the use of array of arrays
public sealed class BatchBuffer
{
    private readonly float[]?[] _batches;
    private readonly int        _maxBatches;
    private          int        _head;

    public BatchBuffer(int maxBatches)
    {
        if (maxBatches <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatches));

        _maxBatches = maxBatches;
        _batches    = new float[]?[maxBatches];
        _head       = 0;
    }

    public void WriteBatch(float[] readings)
    {
        if (readings is null || readings.Length == 0) return;

        // Wrap around when the buffer is full. Oldest slot is overwritten.
        int slot = _head % _maxBatches;
        _batches[slot] = readings;
        _head++;
    }

    // Flush walks every non-null row and transfers its values into typed packets.
    // After the flush the buffer is clear and ready for the next ingestion window.
    public List<TelemetryPacket<float>> Flush(string deviceId, string unit)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID required for flush.", nameof(deviceId));

        var result = new List<TelemetryPacket<float>>();

        for (int i = 0; i < _maxBatches; i++)
        {
            if (_batches[i] is null) continue;

            foreach (float v in _batches[i]!)
            {
                
                    result.Add(new TelemetryPacket<float>(deviceId, v, unit));
            }

            _batches[i] = null;
        }

        _head = 0;
        return result;
    }

    public int PendingBatches
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _maxBatches; i++)
                if (_batches[i] is not null) count++;
            return count;
        }
    }
}
