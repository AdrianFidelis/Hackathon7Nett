using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;

namespace VideoApi.Benchmarks
{
    [MemoryDiagnoser]
    public class VideoControllerUploadBenchmark
    {
        private VideoController _controller;
        private InMemoryStore _store;
        private ILogger<VideoController> _logger;
        private IConfiguration _config;

        [GlobalSetup]
        public void Setup()
        {
            _store = new InMemoryStore();
            _logger = new LoggerFactory().CreateLogger<VideoController>();
            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
                {"RabbitMQ:Host", "localhost"}
            }).Build();
            _controller = new VideoController(_store, _logger, _config);
        }

        [Benchmark]
        public async Task UploadBenchmark()
        {
            using var content = new MemoryStream(new byte[1024 * 1024]); // 1MB
            var file = new FormFile(content, 0, content.Length, "file", "test.mp4");
            await _controller.Upload(file);
        }
    }
}
