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

    public VideoController(InMemoryStore store, ILogger<VideoController> logger, IConfiguration config)
    {
        _store = store;
        _logger = logger;
        _config = config;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Arquivo inválido");

        var id = Guid.NewGuid().ToString("N");
        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploads);
        var filePath = Path.Combine(uploads, id + Path.GetExtension(file.FileName));

        using (var fs = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(fs);
        }

        _store.Statuses[id] = new ProcessStatus(id, "Na Fila");

        var host = _config.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var factory = new ConnectionFactory { HostName = host };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync("video_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var message = System.Text.Json.JsonSerializer.Serialize(new { Id = id, Path = filePath });
        var messageBody = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "video_queue",
            mandatory: false,
            body: messageBody
        );

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

    private static string? ReadQrCode(string path)
    {
        try
        {
            var bmp = (Bitmap)Bitmap.FromFile(path);
            var reader = new BarcodeReader
            {
                AutoRotate = true,
                TryInverted = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    TryHarder = true
                }
            };

            var result = reader.Decode(bmp);
            return result?.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao ler QR code: {ex.Message}");
            return null;
        }
    }

    [HttpPost("readqr")]
    [Consumes("multipart/form-data")]
    public IActionResult ReadQr([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Arquivo inválido");

        var tempPath = Path.GetTempFileName();
        using (var fs = System.IO.File.Create(tempPath))
        {
            file.CopyTo(fs);
        }

        var qr = ReadQrCode(tempPath);
        System.IO.File.Delete(tempPath);
        return Ok(new { qr });
    }
}

