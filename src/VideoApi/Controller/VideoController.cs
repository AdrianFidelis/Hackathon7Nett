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
        var statusDir = Path.Combine(Directory.GetCurrentDirectory(), "status");
        var statusFile = Path.Combine(statusDir, id + ".json");
        if (System.IO.File.Exists(statusFile))
        {
            var json = System.IO.File.ReadAllText(statusFile);
            return Content(json, "application/json");
        }

        if (_store.Statuses.TryGetValue(id, out var s)) return Ok(s);
        return NotFound();
    }

    [HttpGet("results/{id}")]
    public IActionResult Results(string id)
    {
        if (_store.Results.TryGetValue(id, out var list)) return Ok(list);

        var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "results");
        Directory.CreateDirectory(resultsDir);
        var file = Path.Combine(resultsDir, id + ".json");
        if (System.IO.File.Exists(file))
        {
            var json = System.IO.File.ReadAllText(file);
            return Content(json, "application/json");
        }
        return Ok(Array.Empty<object>());
    }
}

