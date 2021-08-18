using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Autofac.Saga.Contracts;

namespace MassTransit.Autofac.Saga.Components
{
    public class OrderStateAuditConsumer :
        IConsumer<OrderStateCreated>
    {
        static bool ContinueSignal = true;

        public async Task Consume(ConsumeContext<OrderStateCreated> context)
        {
            if (context.Message.OrderId != context.ConversationId)
                await Console.Error.WriteLineAsync("ConversationId was not correct!");

            await Console.Out.WriteLineAsync($"OrderState(created): {context.Message.OrderId} ({context.ConversationId})");

            if (ContinueSignal)
            {
                // Stop signaling after we've reached zero
                // We may continue to receive events if subsequent program executions
                // left messages in MongoDB
                ContinueSignal = !Program.Countdown.Signal();
            }
        }
    }
}