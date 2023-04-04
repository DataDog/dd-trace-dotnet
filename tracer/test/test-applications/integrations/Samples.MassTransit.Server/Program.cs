using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MassTransit;
using MassTransitContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Samples.MassTransit.Server
{
    public class Program
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
                });
        }
    }
}
