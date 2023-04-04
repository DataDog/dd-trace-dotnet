using System;
using System.Reflection;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Samples.MassTransit.Client
{
    internal class Program
    {
        static bool? _isRunningInContainer;

        static bool IsRunningInContainer =>
            _isRunningInContainer ??= bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inDocker) && inDocker;

        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions<NodeOptions>();

                    services.AddMassTransit(x =>
                    {
                        x.SetKebabCaseEndpointNameFormatter();

                        var entryAssembly = Assembly.GetEntryAssembly();

                        x.AddConsumers(entryAssembly);

                        x.UsingRabbitMq((context, cfg) =>
                        {
                            cfg.Host(IsRunningInContainer ? "rabbitmq" : "localhost", "/");

                            cfg.ConfigureEndpoints(context);
                        });
                    });

                    services.AddHostedService<StartupService>();
                });
        }
    }
}
