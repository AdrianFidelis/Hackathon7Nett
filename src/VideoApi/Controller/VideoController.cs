using Microsoft.AspNetCore.Mvc;
using System.Text;
using RabbitMQ.Client;
using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly InMemoryStore _store;
    private readonly ILogger<VideoController> _logger;
    private readonly IConfiguration _config;

    // DTO para upload via multipart/form-data (resolve erro do Swagger)
    public sealed class FileForm
    {
        public IFormFile File { get; set; } = default!;
    }

    public VideoController(InMemoryStore store, ILogger<VideoController> logger, IConfiguration config)
    {
        _store = store;
        _logger = logger;
        _config = config;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Upload([FromForm] FileForm form)
    {
        var file = form.File;
        if (file == null || file.Length == 0)
            return BadRequest("Arquivo inválido");

        var id = Guid.NewGuid().ToString("N");
        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploads);
        var filePath = Path.Combine(uploads, id + Path.GetExtension(file.FileName));

        using (var fs = System.IO.File.Create(filePath))
            await file.CopyToAsync(fs);

        _store.Statuses[id] = new ProcessStatus(id, "Na Fila");

        var host = _config.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var factory = new ConnectionFactory { HostName = host };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync("video_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var message = System.Text.Json.JsonSerializer.Serialize(new { Id = id, Path = filePath });
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(exchange: "", routingKey: "video_queue", mandatory: false, body: body);

        return Accepted(new { id });
    }

    [HttpGet("status/{id}")]
    public IActionResult Status(string id)
    {
        // 1) Tenta arquivo (status/ e uploads/status/)
        var baseDir = Directory.GetCurrentDirectory();
        var statusCandidates = new[]
        {
            Path.Combine(baseDir, "status", id + ".json"),
            Path.Combine(baseDir, "uploads", "status", id + ".json")
        };

        foreach (var path in statusCandidates)
        {
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                return Content(json, "application/json");
            }
        }

        // 2) Fallback: memória
        if (_store.Statuses.TryGetValue(id, out var s)) return Ok(s);

        return NotFound();
    }

    [HttpGet("results/{id}")]
    public IActionResult Results(string id)
    {
        // 1) Tenta arquivo (uploads/results/ e results/)
        var baseDir = Directory.GetCurrentDirectory();
        var resultCandidates = new[]
        {
            Path.Combine(baseDir, "uploads", "results", id + ".json"),
            Path.Combine(baseDir, "results", id + ".json")
        };

        foreach (var path in resultCandidates)
        {
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                return Content(json, "application/json");
            }
        }

        // 2) Fallback: memória
        if (_store.Results.TryGetValue(id, out var list)) return Ok(list);

        return Ok(Array.Empty<object>());
    }
}

