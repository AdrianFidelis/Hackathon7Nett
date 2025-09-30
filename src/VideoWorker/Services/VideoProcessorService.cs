using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ZXing;
using ZXing.Common;
using System.Drawing;
using ZXing.Windows.Compatibility;
using System.Runtime.Versioning;

public class VideoProcessorService : BackgroundService
{
    private readonly ILogger<VideoProcessorService> _logger;
    private readonly IConfiguration _config;

    private IConnection? _connection;
    private IChannel? _channel;

    public VideoProcessorService(ILogger<VideoProcessorService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var host = _config.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var factory = new ConnectionFactory { HostName = host };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(options: null);

        await _channel.QueueDeclareAsync(
            queue: "video_queue",
            durable: true, exclusive: false, autoDelete: false, arguments: null,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(0, prefetchCount: 1, global: false, cancellationToken);

        _logger.LogInformation("Worker conectado ao RabbitMQ em {Host}; aguardando mensagens…", host);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null) throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);    

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Mensagem recebida: {Msg}", message);

            try
            {
                var payload = JsonSerializer.Deserialize<UploadMsg>(message);
                if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Path))
                {
                    _logger.LogWarning("Payload inválido"); 

                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                UpdateStatus(payload.Id, "Processando");
                _logger.LogInformation("Processando {Id}: {Path}", payload.Id, payload.Path);

                var framesDir = Path.Combine(Path.GetDirectoryName(payload.Path) ?? ".", "frames", payload.Id);
                Directory.CreateDirectory(framesDir);

                var ffArgs =
                    $"-y -hide_banner -loglevel error -i \"{payload.Path}\" -vf fps=1 \"{Path.Combine(framesDir, "frame-%04d.png")}\"";
                RunOrThrow("ffmpeg", ffArgs);
                _logger.LogInformation("Frames extraídos em {Dir}", framesDir);

                try
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        UpdateStatus(payload.Id, "Erro: processamento suportado apenas no Windows");
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    await ProcessFramesWindowsOnly(framesDir, payload, stoppingToken);
                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex2)
                {
                    UpdateStatus(payload.Id, "Erro");
                    _logger.LogError(ex2, "Erro no pipeline de processamento");
                    await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro processando mensagem");
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(queue: "video_queue", autoAck: false, consumer: consumer);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { if (_channel is not null) await _channel.CloseAsync(cancellationToken); } catch { }
        try { if (_connection is not null) await _connection.CloseAsync(cancellationToken); } catch { }
        await base.StopAsync(cancellationToken);
    }

    // Executa ffmpeg e lança exceção se falhar
    private static void RunOrThrow(string file, string args)
    {
        var p = new Process
        {
            StartInfo =
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        p.Start();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new Exception($"ffmpeg falhou: {p.StandardError.ReadToEnd()}");
    }

    // Atualiza status em arquivo JSON simples
    private void UpdateStatus(string id, string status)
    {
        var statusDir = Path.Combine(Directory.GetCurrentDirectory(), "status");
        Directory.CreateDirectory(statusDir);
        var statusFile = Path.Combine(statusDir, id + ".json");
        var obj = new { Id = id, Status = status };
        File.WriteAllText(statusFile, JsonSerializer.Serialize(obj));
    }

    private sealed record UploadMsg(string Id, string Path);


    public record QRCodeResult(string Content, double TimestampSeconds);

    [SupportedOSPlatform("windows")]
    private async Task ProcessFramesWindowsOnly(string framesDir, UploadMsg payload, CancellationToken stoppingToken)
    {
        var files = Directory.GetFiles(framesDir, "*.png").OrderBy(f => f).ToArray();
        var resultsBag = new System.Collections.Concurrent.ConcurrentBag<QRCodeResult>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        int idx = -1;
        Parallel.ForEach(files, options, filePath =>
        {
            int frameIndex = Interlocked.Increment(ref idx) + 1;
            try
            {
                using var bmp = (Bitmap)Image.FromFile(filePath);
                var localReader = new BarcodeReader
                {
                    AutoRotate = true,
                    TryInverted = true,
                    Options = new DecodingOptions
                    {
                        PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                        TryHarder = true
                    }
                };
                var res = localReader.Decode(bmp);
                if (res != null)
                {
                    resultsBag.Add(new QRCodeResult(res.Text, frameIndex - 1));
                    _logger.LogInformation("QR detectado em {Frame}: {Text}", filePath, res.Text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao ler frame {File}", filePath);
            }
        });

        var results = resultsBag.OrderBy(r => r.TimestampSeconds).ToList();

        var outDir = Path.Combine(Path.GetDirectoryName(payload.Path) ?? ".", "results");
        Directory.CreateDirectory(outDir);
        var outFile = Path.Combine(outDir, payload.Id + ".json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(results), stoppingToken);

        UpdateStatus(payload.Id, "Concluído");
        _logger.LogInformation("Concluído {Id} — {Count} QR(s). JSON: {File}", payload.Id, results.Count, outFile);
    }
}