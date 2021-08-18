using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Autofac.Saga.Contracts;

namespace MassTransit.Autofac.Saga.Components
{
    public class SubmitOrderConsumer :
        IConsumer<SubmitOrder>
    {
        public async Task Consume(ConsumeContext<SubmitOrder> context)
        {
            await Console.Out.WriteLineAsync($"Submit Order Consumer: {context.Message.OrderId} ({context.ConversationId})");

            await context.Publish<OrderReceived>(context.Message, sendContext => sendContext.CorrelationId = NewId.NextGuid());
        }
    }
}