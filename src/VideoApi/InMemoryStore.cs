using System.Collections.Concurrent;

public class InMemoryStore
{
    public ConcurrentDictionary<string, ProcessStatus> Statuses { get; } = new();
    public ConcurrentDictionary<string, List<QRCodeResult>> Results { get; } = new();
}

public record ProcessStatus(string Id, string Status);
public record QRCodeResult(string Content, double TimestampSeconds);