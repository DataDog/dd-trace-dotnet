namespace ServiceBus.Minimal.MassTransit.Components.Activities.SuspendOrder
{
    using System;
    using System.Threading.Tasks;
    using global::MassTransit.Courier;
    using global::MassTransit.Courier.Exceptions;
    using Microsoft.Extensions.Logging;


    public class SuspendOrderActivity :
        IExecuteActivity<SuspendOrderArguments>
    {
        private readonly SampleBatchDbContext _dbContext;
        readonly ILogger _logger;

        public SuspendOrderActivity(SampleBatchDbContext dbContext, ILoggerFactory loggerFactory)
        {
            _dbContext = dbContext;
            _logger = loggerFactory.CreateLogger<SuspendOrderActivity>();
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<SuspendOrderArguments> context)
        {
            _logger.LogInformation("Suspending {OrderId}", context.Arguments.OrderId);

            var random = new Random(DateTime.Now.Millisecond);

            if (random.Next(1, 10) == 1)
                throw new RoutingSlipException("Order shipped, cannot suspend");

            await Task.Delay(random.Next(1, 7) * 1000);

            return context.Completed();
        }
    }
}