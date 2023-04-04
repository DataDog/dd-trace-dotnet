using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassTransit;
using MassTransitContracts;
using Microsoft.Extensions.Logging;

namespace Samples.MassTransit.Server
{
    public class ClientAvailableConsumer :
        IConsumer<ClientAvailable>
    {
        readonly ILogger<ClientAvailableConsumer> _logger;

        public ClientAvailableConsumer(ILogger<ClientAvailableConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ClientAvailable> context)
        {
            _logger.LogInformation("Received client available: {Id}", context.Message.NodeId);

            await context.Publish<ContentReceived>(new
            {
                InVar.Id,
                context.Message.NodeId
            });
        }
    }
}
