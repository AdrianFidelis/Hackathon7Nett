using System.Collections.Concurrent;

// Tipos no namespace global para alinhamento com o uso nos testes
public sealed class InMemoryStore
{
    public ConcurrentDictionary<string, ProcessStatus> Statuses { get; } = new();
    public ConcurrentDictionary<string, List<QRCodeResult>> Results { get; } = new();
}

public sealed record ProcessStatus(string Id, string Status);       

// Usado pelos testes e pelo fallback de memória
public sealed record QRCodeResult(string Content, double TimestampSeconds);