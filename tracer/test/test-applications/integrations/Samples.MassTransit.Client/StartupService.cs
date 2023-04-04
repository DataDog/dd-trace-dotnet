using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransitContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Samples.MassTransit.Client
{
    public class StartupService :
        BackgroundService
    {
        readonly IBus _bus;
        readonly string _nodeId;
        readonly ILogger _logger;

        public StartupService(IBus bus, IOptions<NodeOptions> options, ILogger<StartupService> logger)
        {
            _bus = bus;
            _nodeId = options.Value.NodeId;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Sending service available");
                await _bus.Publish<ClientAvailable>(new { NodeId = _nodeId }, stoppingToken);
                await Task.Delay(30_000, stoppingToken);
            }
        }
    }
}
