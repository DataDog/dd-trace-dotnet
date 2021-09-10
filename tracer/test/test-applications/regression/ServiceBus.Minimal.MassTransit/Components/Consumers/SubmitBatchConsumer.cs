namespace ServiceBus.Minimal.MassTransit.Components.Consumers
{
    using System.Threading.Tasks;
    using Contracts;
    using global::MassTransit;
    using global::MassTransit.Definition;
    using Microsoft.Extensions.Logging;


    public class SubmitBatchConsumer :
        IConsumer<SubmitBatch>
    {
        readonly ILogger<SubmitBatchConsumer> _log;

        public SubmitBatchConsumer(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<SubmitBatchConsumer>();
        }

        public async Task Consume(ConsumeContext<SubmitBatch> context)
        {
            using (_log.BeginScope("SubmitBatch {BatchId}", context.Message.BatchId))
            {
                if (_log.IsEnabled(LogLevel.Debug))
                    _log.LogDebug("Validating batch {BatchId}", context.Message.BatchId);

                // do some validation
                if (context.Message.OrderIds.Length == 0)
                {
                    await context.RespondAsync<BatchRejected>(new
                    {
                        context.Message.BatchId,
                        InVar.Timestamp,
                        Reason = "Must have at least one OrderId to Process"
                    });

                    return;
                }

                await context.Publish<BatchReceived>(new
                {
                    context.Message.BatchId,
                    InVar.Timestamp,
                    context.Message.Action,
                    context.Message.OrderIds,
                    context.Message.ActiveThreshold,
                    context.Message.DelayInSeconds
                });

                await context.RespondAsync<BatchSubmitted>(new
                {
                    context.Message.BatchId,
                    InVar.Timestamp
                });

                if (_log.IsEnabled(LogLevel.Debug))
                    _log.LogDebug("Accepted order {BatchId}", context.Message.BatchId);
            }
        }
    }


    public class SubmitBatchConsumerDefinition :
        ConsumerDefinition<SubmitBatchConsumer>
    {
        public SubmitBatchConsumerDefinition()
        {
            ConcurrentMessageLimit = 10;

            Request<SubmitBatch>(x =>
            {
                x.Responds<BatchRejected>();
                x.Responds<BatchSubmitted>();

                x.Publishes<BatchReceived>();
            });
        }
    }
}