using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using ServiceBus.Minimal.NServiceBus.Shared;

namespace ServiceBus.Minimal.NServiceBus.Server
{
    #region thesaga

    public class OrderSaga :
        Saga<OrderSagaData>,
        IAmStartedByMessages<StartOrder>,
        IHandleTimeouts<CompleteOrder>
    {
        static ILog log = LogManager.GetLogger<OrderSaga>();

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderSagaData> mapper)
        {
            mapper.ConfigureMapping<StartOrder>(message => message.OrderId)
                .ToSaga(sagaData => sagaData.OrderId);
        }

        public Task Handle(StartOrder message, IMessageHandlerContext context)
        {
            Data.OrderId = message.OrderId;
            var orderDescription = $"The saga for order {message.OrderId}";
            Data.OrderDescription = orderDescription;

            log.Info($"Received StartOrder message {Data.OrderId}. Starting Saga");
            log.Info($"Order will complete in {Program.MessageCompletionDurationMs} milliseconds");

            var timeoutData = new CompleteOrder
            {
                OrderDescription = orderDescription
            };

            return RequestTimeout(context, TimeSpan.FromMilliseconds(Program.MessageCompletionDurationMs), timeoutData);
        }

        public Task Timeout(CompleteOrder state, IMessageHandlerContext context)
        {
            log.Info($"Saga with OrderId {Data.OrderId} completed");

            var orderCompleted = new OrderCompleted
            {
                OrderId = Data.OrderId
            };

            MarkAsComplete();

            return context.Publish(orderCompleted);
        }
    }

    #endregion
}
