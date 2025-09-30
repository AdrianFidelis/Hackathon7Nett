using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VideoApi.Benchmarks
{
    [MemoryDiagnoser]
    public class VideoControllerUploadBenchmark
    {
        private VideoController _controller = default!;
        private InMemoryStore _store = default!;
        private ILogger<VideoController> _logger = default!;
        private IConfiguration _config = default!;

        [GlobalSetup]
        public void Setup()
        {
            _store = new InMemoryStore();
            _logger = new LoggerFactory().CreateLogger<VideoController>();
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "RabbitMQ:Host", "localhost" }
                })
                .Build();

            _controller = new VideoController(_store, _logger, _config);
        }

        [Benchmark]
        public async Task UploadBenchmark()
        {
            using var content = new MemoryStream(new byte[1024 * 1024]); // 1MB
            content.Position = 0;

            var file = new FormFile(content, 0, content.Length, "file", "test.mp4");
            var form = new VideoController.FileForm { File = file };

            await _controller.Upload(form);
        }
    }
}
