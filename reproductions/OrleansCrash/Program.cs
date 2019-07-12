using System;
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
            try
            {
                var orleansTasks = new List<Task>();

                var tokenSource = new CancellationTokenSource();

                Console.WriteLine("Starting the orleans cluster.");

                var serverHost = BuildClusterHost();
                var hostingTask = serverHost.RunAsync(tokenSource.Token);
                orleansTasks.Add(hostingTask);

                Console.WriteLine("Starting the orleans client.");

                var clientHost = BuildClientHost();
                var clientTask = clientHost.RunAsync(tokenSource.Token);
                orleansTasks.Add(clientTask);

                await Task.Delay(2_000, tokenSource.Token); // Give the cluster some time to start I guess

                Console.WriteLine("Grabbing an orleans singleton service.");
                var helloWorldClient = clientHost.Services.GetService<IHelloWorldHostedService>();
                var helloGrain = helloWorldClient.GimmeTheGrain();

                Task<string> hiMark;
                string response;
                int weSayHiManyTimes = 5;

                Console.WriteLine($"Calling the service {weSayHiManyTimes} times.");
                while (weSayHiManyTimes-- > 0)
                {
                    hiMark = helloGrain.SayHello($"{weSayHiManyTimes} - Oh, hi Mark!");
                    response = await hiMark;
                    Console.WriteLine(response);
                    orleansTasks.Add(hiMark);
                }

                TriggerCancellationAfterThisManySeconds(10, tokenSource);

                while (!tokenSource.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }

                var aggregateTask = Task.WhenAll(orleansTasks);

                var faultedTasks = orleansTasks.Where(t => t.IsFaulted).ToList();

                if (faultedTasks.Any())
                {
                    throw aggregateTask.Exception ?? new Exception("Something went wrong.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
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
