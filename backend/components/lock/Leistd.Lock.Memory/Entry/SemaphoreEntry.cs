namespace Leistd.Lock.Memory.Entry
{
    internal sealed class SemaphoreEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public DateTime LastReleasedAt { get; set; } = DateTime.UtcNow;
    }
}
