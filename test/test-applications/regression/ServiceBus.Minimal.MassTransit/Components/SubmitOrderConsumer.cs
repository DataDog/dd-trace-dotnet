using System;
using System.Threading.Tasks;
using MassTransit;
using ServiceBus.Minimal.MassTransit.Contracts;

namespace ServiceBus.Minimal.MassTransit.Components
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