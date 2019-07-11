using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Orleans;

namespace OrleansCrash.Clients
{
    public class HelloWorldClientHostedService : IHelloWorldHostedService
    {
        private readonly IClusterClient _client;

        public HelloWorldClientHostedService(IClusterClient client, IApplicationLifetime lifetime)
        {
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // example of calling grains from the initialized client
            var friend = GimmeTheGrain();
            var response = await friend.SayHello("Good morning, my friend!");
            Console.WriteLine("\n\n{0}\n\n", response);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Grains.IHello GimmeTheGrain()
        {
            return _client.GetGrain<Grains.IHello>(0);
        }
    }
}
