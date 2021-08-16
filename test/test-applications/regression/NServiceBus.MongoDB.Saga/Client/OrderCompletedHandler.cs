using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MongoDB.Saga.Shared;

namespace NServiceBus.MongoDB.Saga.Client
{
    public class OrderCompletedHandler : IHandleMessages<OrderCompleted>
    {
        static readonly ILog log = LogManager.GetLogger<OrderCompletedHandler>();
        static Task CompletedTask = Task.FromResult(0);
        static bool ContinueSignal = true;

        public Task Handle(OrderCompleted message, IMessageHandlerContext context)
        {
            log.Info($"Received OrderCompleted for OrderId {message.OrderId}");
            if (ContinueSignal)
            {
                // Stop signaling after we've reached zero
                // We may continue to receive events if subsequent program executions
                // left messages in MongoDB
                ContinueSignal = !Program.Countdown.Signal();
            }

            return CompletedTask;
        }
    }
}
