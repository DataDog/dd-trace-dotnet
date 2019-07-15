using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static int Main(string[] args)
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

                Console.WriteLine("Waiting a little before grabbing a service.");
                // TODO: Make this based on a ping or something
                Task.Delay(4_000, tokenSource.Token).Wait(); // Give the cluster time to start

                Console.WriteLine("Grabbing an orleans singleton service.");
                var helloWorldClient = clientHost.Services.GetService<IHelloWorldHostedService>();
                var helloGrain = helloWorldClient.GimmeTheGrain();

                int weSayHiManyTimes = 5;
                Console.WriteLine($"Calling the service {weSayHiManyTimes} times.");
                while (weSayHiManyTimes-- > 0)
                {
                    var hiMark = helloGrain.SayHello($"{weSayHiManyTimes} - Oh, hi Mark!");
                    hiMark.Wait(tokenSource.Token);
                    var response = hiMark.Result;
                    Console.WriteLine(response);
                    orleansTasks.Add(hiMark);
                }

                var serverStopTask = serverHost.StopAsync(tokenSource.Token);
                var clientStopTask = clientHost.StopAsync(tokenSource.Token);

                orleansTasks.Add(serverStopTask);
                orleansTasks.Add(clientStopTask);

                var ttlMilliseconds = 5_000;
                tokenSource.CancelAfter(ttlMilliseconds);

                var timer = new Stopwatch();
                timer.Start();

                while (!tokenSource.IsCancellationRequested)
                {
                    Task.Delay(250).Wait();

                    if (timer.Elapsed > TimeSpan.FromMilliseconds(ttlMilliseconds * 2))
                    {
                        break;
                    }

                    if (serverStopTask.IsCompleted && clientStopTask.IsCompleted)
                    {
                        break;
                    }
                }

                timer.Stop();

                var faultedTasks = orleansTasks.Where(t => t.IsFaulted).ToList();

                if (faultedTasks.Any())
                {
                    throw faultedTasks.FirstOrDefault()?.Exception ?? new Exception("Something went wrong.");
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
                                        options.ClusterId = "orleans-crash-check";
                                        options.ServiceId = "OrleansCrashCluster";
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
