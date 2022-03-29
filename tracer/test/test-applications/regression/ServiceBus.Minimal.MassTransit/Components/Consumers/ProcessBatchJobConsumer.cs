namespace ServiceBus.Minimal.MassTransit.Components.Consumers
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using Contracts.Enums;
    using global::MassTransit;
    using global::MassTransit.Courier;
    using global::MassTransit.Courier.Contracts;
    using Microsoft.Extensions.Logging;


    public class ProcessBatchJobConsumer :
        IConsumer<ProcessBatchJob>
    {
        readonly ILogger<ProcessBatchJobConsumer> _log;

        public ProcessBatchJobConsumer(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<ProcessBatchJobConsumer>();
        }

        public async Task Consume(ConsumeContext<ProcessBatchJob> context)
        {
            using (_log.BeginScope("ProcessBatchJob {BatchJobId}, {OrderId}", context.Message.BatchJobId, context.Message.OrderId))
            {
                var builder = new RoutingSlipBuilder(NewId.NextGuid());

                switch (context.Message.Action)
                {
                    case BatchAction.CancelOrders:
                        builder.AddActivity(
                            "CancelOrder",
                            new Uri("queue:cancel-order_execute"),
                            new
                            {
                                context.Message.OrderId,
                                Reason = "Product discontinued"
                            });

                        await builder.AddSubscription(
                            context.SourceAddress,
                            RoutingSlipEvents.ActivityFaulted,
                            RoutingSlipEventContents.None,
                            "CancelOrder",
                            x => x.Send<BatchJobFailed>(new
                            {
                                context.Message.BatchJobId,
                                context.Message.BatchId,
                                context.Message.OrderId
                            }));
                        break;

                    case BatchAction.SuspendOrders:
                        builder.AddActivity(
                            "SuspendOrder",
                            new Uri("queue:suspend-order_execute"),
                            new {context.Message.OrderId});

                        await builder.AddSubscription(
                            context.SourceAddress,
                            RoutingSlipEvents.ActivityFaulted,
                            RoutingSlipEventContents.None,
                            "SuspendOrder",
                            x => x.Send<BatchJobFailed>(new
                            {
                                context.Message.BatchJobId,
                                context.Message.BatchId,
                                context.Message.OrderId
                            }));
                        break;
                }

                await builder.AddSubscription(
                    context.SourceAddress,
                    RoutingSlipEvents.Completed,
                    x => x.Send<BatchJobCompleted>(new
                    {
                        context.Message.BatchJobId,
                        context.Message.BatchId
                    }));

                await context.Execute(builder.Build());
            }
        }
    }
}