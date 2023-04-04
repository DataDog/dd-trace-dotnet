namespace Samples.MassTransit.Client
{
    using System.Threading.Tasks;
    using MassTransitContracts;
    using Microsoft.Extensions.Logging;
    using global::MassTransit;

    public class ContentReceivedConsumer :
        IConsumer<ContentReceived>
    {
        readonly ILogger<ContentReceivedConsumer> _logger;

        public ContentReceivedConsumer(ILogger<ContentReceivedConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<ContentReceived> context)
        {
            _logger.LogInformation("Content Received: {Id}", context.Message.Id);

            return Task.CompletedTask;
        }
    }
}
