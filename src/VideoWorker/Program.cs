using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<VideoProcessorService>(); // Worker real
        // Não registra o Worker de template (classe Worker) para evitar duplicidade
    })
    .Build()
    .Run();
