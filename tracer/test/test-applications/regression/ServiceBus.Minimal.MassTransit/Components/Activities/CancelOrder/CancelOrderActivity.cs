namespace ServiceBus.Minimal.MassTransit.Components.Activities.CancelOrder
{
    using System;
    using System.Threading.Tasks;
    using global::MassTransit.Courier;
    using global::MassTransit.Courier.Exceptions;
    using Microsoft.Extensions.Logging;


    public class CancelOrderActivity :
        IExecuteActivity<CancelOrderArguments>
    {
        private readonly SampleBatchDbContext _dbContext;
        readonly ILogger _logger;

        public CancelOrderActivity(SampleBatchDbContext dbContext, ILoggerFactory loggerFactory)
        {
            _dbContext = dbContext;
            _logger = loggerFactory.CreateLogger<CancelOrderActivity>();
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<CancelOrderArguments> context)
        {
            _logger.LogInformation("Cancelling {OrderId}, with Reason: {Reason}", context.Arguments.OrderId, context.Arguments.Reason);

            var random = new Random(DateTime.Now.Millisecond);

            if (random.Next(1, 10) == 1)
                throw new RoutingSlipException("Order shipped, cannot cancel");

            await Task.Delay(random.Next(1, 7) * 1000);

            return context.Completed();
        }
    }
}