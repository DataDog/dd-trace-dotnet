using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansCrash.Clients;
using OrleansCrash.Grains;

namespace OrleansCrash
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var orleansTasks = new List<Task>();

            var tokenSource = new CancellationTokenSource();

            var serverHost = BuildClusterHost();
            var hostingTask = serverHost.RunAsync(tokenSource.Token);
            orleansTasks.Add(hostingTask);

            var clientHost = BuildClientHost();
            var clientTask = clientHost.RunAsync(tokenSource.Token);
            orleansTasks.Add(clientTask);

            await Task.Delay(3_000, tokenSource.Token); // Give the cluster some time to start I guess

            var helloWorldClient = clientHost.Services.GetService<IHelloWorldHostedService>();
            var helloGrain = helloWorldClient.GimmeTheGrain();
            var helloTask = helloGrain.SayHello("Oh, hi Mark!");
            orleansTasks.Add(helloTask);

            TriggerCancellationAfterThisManySeconds(15, tokenSource);

            await Task.WhenAll(orleansTasks);

            var faultedTasks = orleansTasks.Where(t => t.IsFaulted).ToList();

            if (faultedTasks.Any())
            {
                return -10;
            }

            return 0;
        }

        private static IHost BuildClusterHost()
        {
            var hostBuilder =
                new HostBuilder()
                   .UseOrleans(
                        builder =>
                        {
                            builder
                               .UseLocalhostClustering()
                               .Configure<ClusterOptions>(
                                    options =>
                                    {
                                        options.ClusterId = "dev";
                                        options.ServiceId = "HelloWorldApp";
                                    })
                               .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)
                               .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(HelloGrain).Assembly).WithReferences());
                        })
                   .ConfigureServices(
                        services =>
                        {
                            services.Configure<ConsoleLifetimeOptions>(
                                options =>
                                {
                                    options.SuppressStatusMessages = true;
                                });
                        })
                   .ConfigureLogging(
                        builder =>
                        {
                            builder.AddConsole();
                        });

            var serverHost = hostBuilder.Build();
            return serverHost;
        }

        private static void TriggerCancellationAfterThisManySeconds(int seconds, CancellationTokenSource cancelTokenSource)
        {
            Task
               .Delay(seconds * 1000, cancelTokenSource.Token)
               .ContinueWith(
                    t =>
                    {
                        cancelTokenSource.Cancel();
                    },
                    cancelTokenSource.Token)
               .Ignore();
        }

        private static IHost BuildClientHost()
        {
            var clientBuilder =
                new HostBuilder()
                   .ConfigureServices(
                        services =>
                        {
                            services.AddSingleton<ClusterClientHostedService>();
                            services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
                            services.AddSingleton(_ => _.GetService<ClusterClientHostedService>().Client);

                            // services.AddHostedService<HelloWorldClientHostedService>();
                            services.AddSingleton<HelloWorldClientHostedService>();
                            services.AddSingleton<IHelloWorldHostedService>(_ => _.GetService<HelloWorldClientHostedService>());

                            services.Configure<ConsoleLifetimeOptions>(
                                options =>
                                {
                                    options.SuppressStatusMessages = true;
                                });
                        })
                   .ConfigureLogging(
                        builder =>
                        {
                            builder.AddConsole();
                        });

            var clientHost = clientBuilder.Build();
            return clientHost;
        }
    }
}
