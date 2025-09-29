using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton(context.Configuration);
        services.AddHostedService<VideoProcessorService>();
    })
    .Build();

await host.RunAsync();
