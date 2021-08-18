using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using ServiceBus.Minimal.NServiceBus.Shared;

namespace ServiceBus.Minimal.NServiceBus
{
    class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;
        internal static readonly int MessageCompletionDurationMs = 1000;
        internal static readonly CountdownEvent Countdown = new CountdownEvent(NumMessagesToSend);
        private static readonly string EndpointName = "ServiceBus.Minimal.NServiceBus";

        static async Task Main()
        {
            var endpointConfiguration = new EndpointConfiguration(EndpointName);
            endpointConfiguration.UsePersistence<LearningPersistence>();
            endpointConfiguration.UseTransport<LearningTransport>();
            endpointConfiguration.EnableInstallers();

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            await SendMessagesAsync();

            await endpointInstance.Stop()
                .ConfigureAwait(false);
        }

        static async Task SendMessagesAsync()
        {
            var endpointConfiguration = new EndpointConfiguration(EndpointName);
            endpointConfiguration.UsePersistence<LearningPersistence>();
            endpointConfiguration.UseTransport<LearningTransport>();

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            Console.WriteLine($"Sending {NumMessagesToSend} messages with a {MessageSendDelayMs}ms delay.");

            for (int i = 0; i < NumMessagesToSend; i++)
            {
                var orderId = Guid.NewGuid();
                var startOrder = new StartOrder
                {
                    OrderId = orderId
                };

                await endpointInstance.Send(EndpointName, startOrder)
                    .ConfigureAwait(false);

                Console.WriteLine($"Message #{i}: StartOrder Message sent with OrderId {orderId}");

                await Task.Delay(MessageSendDelayMs);
            }

            await endpointInstance.Stop()
                .ConfigureAwait(false);

            Countdown.Wait(5000);

            // Wait one more second to ensure the state is flushed between consecutive local runs
            await Task.Delay(1000);
        }
    }
}
